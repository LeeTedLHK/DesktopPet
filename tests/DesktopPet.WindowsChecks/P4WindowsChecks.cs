using System.Reflection;
using System.Windows;
using DesktopPet.Controllers;
using DesktopPet.Models;
using DesktopPet.Services;
using DesktopPet.Views;

internal static class P4WindowsChecks
{
    public static void Run(PetController controller, PetWindow pet, ScreenBoundsService screen,
        Action<bool,string> check, Action<int> pump, Action<FrameworkElement,string> render, string evidence)
    {
        check(screen.Monitors.Count > 0 && screen.CurrentMonitor.Scale > 0, "native monitor enumeration includes valid DPI");
        controller.ApplySettings(controller.Settings with { ActivityRange=ActivityRange.Full, ClickThrough=false,
            AutoBehaviorEnabled=false, NightSleepEnabled=false, PreciseHitTestEnabled=true });
        var origin = screen.Position(pet);
        screen.Move(pet, screen.Clamp(new(origin.X-20,origin.Y-20),new(pet.Width,pet.Height)));
        var moved = screen.Position(pet);
        check(Math.Abs(moved.X-(origin.X-20)) <= 1 && Math.Abs(moved.Y-(origin.Y-20)) <= 1,
            "native pet movement round-trips through monitor DIP geometry");
        check(!pet.IsOpaqueAtScreenPoint(pet.PointToScreen(new Point(1,1))), "transparent pet margin does not receive pixel hit");
        var transparent = pet.PointToScreen(new Point(1,1));
        var handle = new System.Windows.Interop.WindowInteropHelper(pet).Handle;
        var packed = new IntPtr(((int)transparent.X & 0xffff) | (((int)transparent.Y & 0xffff) << 16));
        check(SendMessage(handle,0x84,IntPtr.Zero,packed).ToInt64()==-1,
            "native WM_NCHITTEST marks actual transparent frame margin as pass-through");
        var frame = System.Windows.Media.Imaging.BitmapSource.Create(2,2,96,96,System.Windows.Media.PixelFormats.Bgra32,null,
            new byte[] {0,0,0,0, 0,0,0,255, 0,0,0,255, 0,0,0,0},8);
        var surface = new PixelHitMap(frame);
        check(!surface.Hit(0,0) && surface.Hit(1,0), "WPF alpha conversion retains real transparent and opaque pixels");
        var dogImage=(System.Windows.Controls.Image)pet.FindName("PetImage");
        var oldFrame=dogImage.Source;
        var dpiEncoder=new System.Windows.Media.Imaging.PngBitmapEncoder();
        dpiEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(System.Windows.Media.Imaging.BitmapSource.Create(
            32,32,72,144,System.Windows.Media.PixelFormats.Bgra32,null,Enumerable.Repeat((byte)255,32*32*4).ToArray(),128)));
        using var dpiStream=new MemoryStream(); dpiEncoder.Save(dpiStream); dpiStream.Position=0;
        var anisotropic=new System.Windows.Media.Imaging.BitmapImage();
        anisotropic.BeginInit(); anisotropic.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        anisotropic.StreamSource=dpiStream;
        anisotropic.EndInit(); anisotropic.Freeze(); dogImage.Source=anisotropic; pet.UpdateLayout();
        check(anisotropic.DpiX<anisotropic.DpiY && new PixelHitMap(anisotropic).Hit(16,16), "anisotropic PNG fixture has unequal DPI and valid opaque pixels");
        check(!pet.IsOpaqueAtScreenPoint(dogImage.PointToScreen(new Point(dogImage.ActualWidth/2,-5))) &&
            pet.IsOpaqueAtScreenPoint(dogImage.PointToScreen(new Point(dogImage.ActualWidth*.1,dogImage.ActualHeight*.5))) &&
            pet.IsOpaqueAtScreenPoint(dogImage.PointToScreen(new Point(dogImage.ActualWidth/2,dogImage.ActualHeight*.5))),
            "PNG with different horizontal and vertical DPI hits only its actual displayed pixels");
        dogImage.Source=oldFrame;
        var registryPath = "Software\\DesktopPet.IntegrationChecks."+Guid.NewGuid().ToString("N");
        var startup = new StartupService("C:\\Desktop Pet\\DesktopPet.exe", registryPath);
        try
        {
            check(startup.SetEnabled(true) && startup.IsEnabled, "startup writes quoted HKCU entry");
            check(startup.SetEnabled(false) && !startup.IsEnabled, "startup disables its own HKCU entry");
        }
        finally { Microsoft.Win32.Registry.CurrentUser.DeleteSubKey(registryPath,false); }
        var eventName = "Local\\DesktopPet.TestShow."+Guid.NewGuid().ToString("N");
        using (var signal = new InstanceSignal(pet.Dispatcher, controller.ShowPet,eventName))
        {
            controller.HidePet();
            check(InstanceSignal.Notify(eventName), "second instance signal reaches named event");
            pump(100);
            check(controller.IsPetVisible, "existing instance signal reveals hidden pet");
            controller.SetFullscreenSuppressed(true);
            InstanceSignal.Notify(eventName);
            pump(100);
            check(!controller.IsPetVisible && controller.WantsPetVisible, "instance reveal respects fullscreen suppression");
            controller.SetFullscreenSuppressed(false);
        }
        controller.ApplySettings(controller.Settings with { ClickThrough=true });
        controller.ResetPosition();
        check(!controller.Settings.ClickThrough && controller.IsPetVisible && controller.Settings.SavedPositionUsesPixels,
            "recovery resets native position and restores interaction");
        var world = (PetWorldService)typeof(PetController).GetField("_world",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(controller)!;
        var state = (PetStateMachine)typeof(PetController).GetField("_state",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(controller)!;
        controller.ApplySettings(controller.Settings with { HomeEnabled=true, ToysEnabled=true,AutoBehaviorEnabled=false });
        check(world.IsHomeVisible, "enabled home appears alongside pet");
        controller.ThrowToy(ToyKind.Ball); pump(100);
        check(world.IsToyVisible && world.IsMoving && state.State is PetState.RunningLeft or PetState.RunningRight,
            "ball throw starts visible chase with automatic behavior paused");
        controller.HidePet();
        check(!world.IsToyVisible && !world.IsHomeVisible && !world.IsMoving, "hiding pet cancels toys and home prop");
        controller.ShowPet(); controller.ThrowToy(ToyKind.Frisbee); pump(100);
        check(world.IsToyVisible, "frisbee uses same explicit chase path");
        controller.PetHead();
        check(!world.IsToyVisible && !world.IsMoving && state.State==PetState.Petting, "petting interrupts chase cleanly");
        controller.ApplySettings(controller.Settings with { ToysEnabled=false }); controller.ThrowToy(ToyKind.Ball);
        check(!world.IsToyVisible && !world.IsMoving, "disabled toy setting blocks throw");
        controller.ApplySettings(controller.Settings with { ToysEnabled=true });
        screen.Move(pet, world.HomeTarget);
        controller.ReturnHome(); pump(150);
        check(state.State==PetState.Sleeping && !world.IsMoving, "return home reaches nest and sleeps while paused");
        controller.PetHead();
        check(state.State==PetState.Petting, "head petting wakes manual home sleep");
        var careBefore = controller.Companion.Profile;
        controller.ApplySettings(controller.Settings with { GrowthEnabled=false, JournalEnabled=false });
        controller.Companion.RecordInteraction(false);
        check(controller.Companion.Profile.Journal.PettingCount==careBefore.Journal.PettingCount &&
            controller.Companion.Profile.Affection==careBefore.Affection,
            "disabled journal and care preserve counters and values while interaction remains available");
        controller.ApplySettings(controller.Settings with { JournalEnabled=true });
        controller.Companion.Journal.Record(DesktopPet.Journal.JournalAction.Play,DateTimeOffset.UtcNow.AddMinutes(2));
        controller.Companion.Save();
        var settingsService=(SettingsService)typeof(PetController).GetField("_settingsService",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(controller)!;
        var profileDirectory=Path.GetDirectoryName(settingsService.SettingsPath)!;
        controller.SaveNow();
        check(File.Exists(settingsService.SettingsPath) && settingsService.Load().SavedPositionUsesPixels,
            "settings persist even when no save-result listener is subscribed");
        var standaloneProfiles=new PetProfileService(Path.Combine(profileDirectory,"standalone"),new AppLogger(profileDirectory));
        using (var standalone=new CompanionService(pet.Dispatcher,standaloneProfiles,
            new AppSettings {GrowthEnabled=false,JournalEnabled=true},()=>true,()=>false,()=>false))
        {
            standalone.RecordInteraction(false);
            check(File.Exists(standaloneProfiles.ProfilePath) && standaloneProfiles.Load().Journal.PettingCount==1,
                "journal profile persists without profile-save notification listeners");
        }
        var restoredProfile=new PetProfileService(profileDirectory,new AppLogger(profileDirectory)).Load();
        check(restoredProfile.Journal.PlayCount>0 && restoredProfile.Journal.HomeCount>0 && restoredProfile.Affection==controller.Companion.Profile.Affection,
            "world play and home records merge into persisted care profile");
        controller.ThrowToy(ToyKind.Ball); controller.SetFullscreenSuppressed(true);
        check(!world.IsToyVisible && !world.IsHomeVisible && !world.IsMoving, "fullscreen cancels world props");
        controller.SetFullscreenSuppressed(false);
        controller.ApplySettings(controller.Settings with { AutoBehaviorEnabled=true, NightSleepEnabled=false });
        controller.ThrowToy(ToyKind.Ball);
        controller.ApplySettings(controller.Settings with { SpeechBubblesEnabled=false });
        var behavior = typeof(PetController).GetField("_behavior",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(controller)!;
        var decision=(System.Windows.Threading.DispatcherTimer)behavior.GetType().GetField("_decisionTimer",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(behavior)!;
        check(world.IsMoving && world.IsToyVisible && state.State is PetState.RunningLeft or PetState.RunningRight && !decision.IsEnabled,
            "ordinary speech setting preserves chase and keeps automatic decisions suspended");
        controller.StopPlay();
        check(!world.IsMoving && state.State==PetState.Idle && decision.IsEnabled,
            "explicitly stopping a preserved chase restores automatic behavior");
        controller.ThrowToy(ToyKind.Frisbee);
        typeof(PetController).GetMethod("OnBoundsChanged",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(controller,null);
        check(!world.IsMoving && state.State==PetState.Idle && decision.IsEnabled,
            "display geometry change interrupting chase resumes automatic behavior");
        var worldTick=(System.Windows.Threading.DispatcherTimer)typeof(PetWorldService).GetField("_timer",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(world)!;
        var normalWorldTick=worldTick.Interval;
        worldTick.Interval=TimeSpan.FromSeconds(30); // Hold the next world tick until both real Popup events dispatch.
        controller.ThrowToy(ToyKind.Ball);
        var petMenu=(System.Windows.Controls.ContextMenu)pet.FindName("PetMenu");
        petMenu.IsOpen=true; pump(20); petMenu.IsOpen=false; pump(20);
        check(world.IsMoving && world.IsToyVisible && !decision.IsEnabled,
            "opening and immediately closing pet menu resumes chase without an automatic timer race");
        controller.StopPlay();
        worldTick.Interval=normalWorldTick;
        controller.ApplySettings(controller.Settings with { AutoBehaviorEnabled=false, SpeechBubblesEnabled=true });
        var homeWindow=(Window)typeof(PetWorldService).GetField("_home",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(world)!;
        var homeBefore=homeWindow.PointToScreen(new Point(0,0));
        var originalMonitors=screen.Monitors;
        var originalMonitor=screen.CurrentMonitor;
        var virtualMonitor=originalMonitor with { Id="fixture-screen", Name="Fixture",
            PixelBounds=originalMonitor.PixelBounds with {Left=originalMonitor.PixelBounds.Left-500},
            PixelWorkArea=originalMonitor.PixelWorkArea with {Left=originalMonitor.PixelWorkArea.Left-500} };
        try
        {
            typeof(ScreenBoundsService).GetProperty("Monitors")!.SetValue(screen,new MonitorInfo[] {originalMonitor,virtualMonitor});
            var capturedSurface=(FrameworkElement)pet.FindName("PetSurface");
            state.BeginDrag(); capturedSurface.CaptureMouse();
            screen.Select(virtualMonitor.Id);
            var homeAfter=homeWindow.PointToScreen(new Point(0,0));
            check(Math.Abs(homeAfter.X-homeBefore.X+500)<=2 && capturedSurface.IsMouseCaptured && state.State==PetState.Dragging,
                "monitor selection repositions home without breaking captured drag (synthetic monitor fixture)");
            capturedSurface.ReleaseMouseCapture(); state.EndDrag();
        }
        finally
        {
            typeof(ScreenBoundsService).GetProperty("Monitors")!.SetValue(screen,originalMonitors);
            screen.Select(originalMonitor.Id);
            controller.ResetPosition();
        }
        var toyWindow=(Window)typeof(PetWorldService).GetField("_toy",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(world)!;
        render((FrameworkElement)homeWindow.Content,Path.Combine(evidence,"home-prop.png"));
        controller.ThrowToy(ToyKind.Ball); pump(100);
        render((FrameworkElement)toyWindow.Content,Path.Combine(evidence,"ball-prop.png"));
        for (var i=0;i<160 && world.IsMoving;i++) pump(100);
        check(!world.IsMoving && !world.IsToyVisible && state.State==PetState.Happy,
            "real timer chase catches settled ball and responds without auto behavior");
        var settingsUi = new SettingsWindow(controller.Settings);
        try
        {
            settingsUi.SetMonitorChoices(screen.Monitors);
            settingsUi.SetSkinChoices(new DesktopPet.Skins.SkinCatalog(Path.Combine(AppContext.BaseDirectory,"Assets","Pet"),Path.Combine(evidence,"test-skins")).List());
            settingsUi.SetCompanionStatus(controller.Companion.Profile,DesktopPet.Companions.FocusPhase.Stopped,false,TimeSpan.Zero);
            settingsUi.Show();
            var tabs=(System.Windows.Controls.TabControl)settingsUi.FindName("SettingsTabs");
            tabs.SelectedIndex=2; pump(100);
            var headers = tabs.Items.OfType<System.Windows.Controls.TabItem>().Select(tab => tab.Header?.ToString()).ToArray();
            check(headers.Contains("屏幕与皮肤") && headers.Contains("玩具与日记") &&
                ((System.Windows.Controls.ComboBox)settingsUi.FindName("MonitorCombo")).SelectedValue as string==controller.Settings.SelectedMonitorId,
                "new monitor skin and world settings pages load actual choices");
            render(settingsUi,Path.Combine(evidence,"settings-screen-skin.png"));
            tabs.SelectedIndex=3; pump(100);
            render(settingsUi,Path.Combine(evidence,"settings-world-journal.png"));
            ((System.Windows.Controls.ScrollViewer)settingsUi.FindName("WorldScroll")).ScrollToBottom(); pump(100);
            render(settingsUi,Path.Combine(evidence,"settings-journal-bottom.png"));
        }
        finally { settingsUi.Close(); }
        var builtIn = Path.Combine(AppContext.BaseDirectory,"Assets","Pet");
        var importRoot = Path.Combine(evidence,"import-source");
        Directory.CreateDirectory(Path.Combine(importRoot,"Idle"));
        var png = new System.Windows.Media.Imaging.PngBitmapEncoder();
        var pixels = Enumerable.Repeat((byte)255,32*32*4).ToArray();
        png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(System.Windows.Media.Imaging.BitmapSource.Create(32,32,96,96,System.Windows.Media.PixelFormats.Bgra32,null,pixels,128)));
        using (var output=File.Create(Path.Combine(importRoot,"Idle","000.png"))) png.Save(output);
        var catalog = new DesktopPet.Skins.SkinCatalog(builtIn,Path.Combine(evidence,"test-skins"));
        var imported = catalog.ImportFolder(importRoot,"验证皮肤");
        check(pet.TryLoadSkin(imported.RootPath,out _) && imported.MissingActions.Count>0,
            "valid imported PNG skin switches with missing-action fallback");
        var sourceAfter = ((System.Windows.Controls.Image)pet.FindName("PetImage")).Source;
        var corruptRoot = Path.Combine(evidence,"decode-invalid");
        Directory.CreateDirectory(Path.Combine(corruptRoot,"Idle"));
        File.WriteAllBytes(Path.Combine(corruptRoot,"Idle","000.png"),File.ReadAllBytes(Path.Combine(importRoot,"Idle","000.png"))[..33]);
        var previewUi=new SettingsWindow(controller.Settings);
        try
        {
            previewUi.SetSkinChoices(new[] {new DesktopPet.Skins.SkinInfo("default","坏预览",corruptRoot,Path.Combine(corruptRoot,"Idle","000.png"),Array.Empty<DesktopPet.Animations.AnimationKind>())});
            check(((System.Windows.Controls.Image)previewUi.FindName("SkinPreview")).Source is null &&
                ((System.Windows.Controls.TextBlock)previewUi.FindName("SkinDetails")).Text.Contains("预览无法读取"),
                "corrupt skin preview is handled inside settings without app exit");
        }
        finally { previewUi.Close(); }
        check(!pet.TryLoadSkin(corruptRoot,out var decodeError) && decodeError.Length>0 &&
            ReferenceEquals(((System.Windows.Controls.Image)pet.FindName("PetImage")).Source,sourceAfter),
            "failed full pixel decode retains active skin without crashing");
        check(pet.TryLoadSkin(builtIn,out _), "default bichon restores after switching imported skin");
        controller.ApplySettings(controller.Settings);
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wParam,IntPtr lParam);
}
