using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using DesktopPet.Controllers;
using DesktopPet.Models;
using DesktopPet.Services;
using DesktopPet.Views;

internal static class P5WindowsChecks
{
    public static void Run(PetController controller, PetWindow pet, ScreenBoundsService screen,
        Action<bool,string> check, Action<int> pump, Action<FrameworkElement,string> render, string evidence)
    {
        var failures = new List<string>();
        void Probe(bool condition, string name)
        {
            if (condition) check(true, name);
            else { failures.Add(name); Console.WriteLine("FAIL: " + name); }
        }
        controller.ApplySettings(controller.Settings with { AutoBehaviorEnabled=false, NightSleepEnabled=false });
        var world = (PetWorldService)typeof(PetController).GetField("_world",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(controller)!;
        controller.FollowCursor();
        pump(80);
        Probe(world.IsMoving && !world.IsToyVisible, "cursor-follow session starts while automatic behavior is paused");
        controller.HidePet();
        Probe(!world.IsMoving, "hidden pet cancels cursor-follow session");
        controller.ShowPet();
        controller.StartFetchChallenge();
        Probe(world.IsMoving && world.IsToyVisible, "fetch challenge starts a real ball chase");
        controller.HidePet();
        controller.ShowPet();
        var petMenu = (System.Windows.Controls.ContextMenu)pet.FindName("PetMenu");
        petMenu.IsOpen = true;
        pump(30);
        ((System.Windows.Controls.MenuItem)pet.FindName("ChallengeItem")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));
        Probe(controller.IsPlaying && !world.IsMoving, "challenge requested inside pet menu waits for its close event");
        petMenu.IsOpen = false;
        pump(30);
        Probe(world.IsMoving && world.IsToyVisible, "closing the pet menu starts the requested challenge");
        controller.HidePet();
        controller.ShowPet();
        controller.ApplySettings(controller.Settings with { EnableAnimations=false });
        var challengeTimer = (System.Windows.Threading.DispatcherTimer)typeof(PetController).GetField("_challengeTimer",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(controller)!;
        challengeTimer.Interval = TimeSpan.FromSeconds(30);
        controller.StartFetchChallenge();
        for (var attempt=0; !challengeTimer.IsEnabled && attempt<150; attempt++) pump(100);
        Probe(challengeTimer.IsEnabled, "first actual catch schedules the next challenge round");
        petMenu.IsOpen = true;
        pump(30);
        var state = (PetStateMachine)typeof(PetController).GetField("_state",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(controller)!;
        Probe(!challengeTimer.IsEnabled && controller.IsPlaying && state.State == PetState.Happy,
            "opening menu between static challenge rounds pauses waiting and preserves happy state");
        petMenu.IsOpen = false;
        pump(30);
        Probe(challengeTimer.IsEnabled && controller.IsPlaying, "closing menu resumes waiting for the next challenge round");
        controller.StopPlay();
        challengeTimer.Interval = TimeSpan.FromSeconds(1.2);
        var followTarget = PetWorldService.FollowPosition(new(500,150),new(100,100),new(100,100),new(0,0,1000,800));
        Probe(Math.Abs(followTarget.X-355.289321881345)<.0001 && followTarget.Y==100,
            "following stops outside the pet footprint instead of covering the cursor");
        Probe(PetWorldService.FollowPosition(new(170,150),new(100,100),new(100,100),new(0,0,1000,800))==new PetPoint(100,100),
            "nearby cursor keeps the pet still without walking in place");
        Probe(PetWorldService.FollowPosition(new(10000,150),new(100,100),new(100,100),new(0,0,500,500)).X==400,
            "cursor-follow target stays inside the activity area");
        controller.FollowCursor();
        typeof(PetWorldService).GetField("_followStarted",BindingFlags.Instance|BindingFlags.NonPublic)!
            .SetValue(world,Stopwatch.GetTimestamp()-31*Stopwatch.Frequency);
        pump(80);
        Probe(!world.IsMoving && !controller.IsPlaying && controller.PlayStatus.Contains("散步结束"),
            "cursor-follow monotonic timeout ends the session and restores idle behavior");
        controller.FollowCursor();
        controller.SetFullscreenSuppressed(true);
        controller.SetFullscreenSuppressed(false);
        Probe(!world.IsMoving && !world.IsFollowingCursor, "fullscreen cancellation does not restart cursor following");
        controller.StartFetchChallenge();
        controller.ApplySettings(controller.Settings with { ToysEnabled=false });
        controller.StartFetchChallenge();
        Probe(!controller.IsPlaying && !challengeTimer.IsEnabled, "disabled toys cancel and block fetch challenges");
        controller.ApplySettings(controller.Settings with { ToysEnabled=true });
        var catches=0;
        void Catch(ToyKind _) => catches++;
        world.ToyCaught += Catch;
        controller.StartFetchChallenge();
        for (var attempt=0; controller.IsPlaying && attempt<350; attempt++) pump(100);
        Probe(catches==3 && !controller.IsPlaying && controller.PlayStatus.Contains("3/3") && !challengeTimer.IsEnabled,
            "three actual simulation catches finish the challenge without a fourth throw");
        pump(1400);
        Probe(catches==3 && !world.IsToyVisible && !world.IsMoving, "completed challenge stays stopped after next-round delay");
        world.ToyCaught -= Catch;
        controller.StartFetchChallenge();
        controller.SetTrayMenuOpen(true);
        controller.SetTrayMenuOpen(false);
        Probe(controller.IsPlaying && world.IsMoving && world.IsToyVisible && !challengeTimer.IsEnabled,
            "closing tray menu resumes the active challenge without a duplicate round timer");
        controller.StopPlay();
        var photoDirectory = Path.Combine(evidence, "photos-"+Guid.NewGuid().ToString("N"));
        var frame = pet.CurrentFrame!;
        var files = PetPhotoService.Save(frame, "团团", photoDirectory);
        Probe(Directory.GetFiles(photoDirectory,"*.png").Length == 2,
            "pet photograph exports a postcard and transparent portrait without desktop capture");
        var card = Load(files.PostcardPath);
        var portrait = Load(files.PortraitPath);
        Probe(card.PixelWidth==640 && card.PixelHeight==760 && portrait.PixelWidth==frame.PixelWidth &&
            portrait.PixelHeight==frame.PixelHeight && !new PixelHitMap(portrait).Hit(0,0),
            "exported PNGs decode with full postcard size and original portrait transparency");
        var previous=File.ReadAllBytes(files.PostcardPath);
        var again=PetPhotoService.Save(frame,"../../CON:团团?",photoDirectory);
        Probe(again.PostcardPath!=files.PostcardPath && Directory.GetFiles(photoDirectory,"*.png").Length==4 &&
            previous.SequenceEqual(File.ReadAllBytes(files.PostcardPath)), "repeated photos preserve earlier files and names never become paths");
        File.Copy(files.PostcardPath,Path.Combine(evidence,"pet-postcard.png"),true);
        File.Copy(files.PortraitPath,Path.Combine(evidence,"pet-portrait.png"),true);
        var assets=Path.Combine(AppContext.BaseDirectory,"Assets","Pet");
        var clips=new DesktopPet.Animations.AnimationLoader(assets).Load();
        Probe(clips[DesktopPet.Animations.AnimationKind.WalkLeft].Frames.Count==8 &&
            clips[DesktopPet.Animations.AnimationKind.WalkRight].Frames.Count==8,
            "runtime loads eight video-guided walking frames in each direction without stale originals");
        var settings = new SettingsWindow(controller.Settings);
        void UpdateSettingsPlay() => settings.SetPlayStatus(controller.PlayStatus,controller.IsPlaying);
        controller.PlayStatusChanged += UpdateSettingsPlay;
        try
        {
            settings.SetPlayStatus(controller.PlayStatus,controller.IsPlaying);
            settings.FollowRequested += controller.FollowCursor;
            settings.StopPlayRequested += controller.StopPlay;
            ((TabControl)settings.FindName("SettingsTabs")).SelectedIndex=3;
            settings.Show(); pump(60);
            ((Button)settings.FindName("FollowButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            settings.SetPlayStatus(controller.PlayStatus,controller.IsPlaying);
            Probe(world.IsFollowingCursor && ((Button)settings.FindName("StopPlayButton")).IsEnabled,
                "settings follow button starts gameplay and exposes its stop control");
            ((Button)settings.FindName("StopPlayButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Probe(!world.IsMoving && !controller.IsPlaying, "settings stop button ends a cursor walk");
            ((ScrollViewer)settings.FindName("WorldScroll")).ScrollToVerticalOffset(325); pump(60);
            render(settings,Path.Combine(evidence,"settings-surprises.png"));
        }
        finally { controller.PlayStatusChanged -= UpdateSettingsPlay; settings.Close(); }
        controller.ApplySettings(controller.Settings with { EnableAnimations=true });
        if (failures.Count > 0) throw new InvalidOperationException(string.Join("; ", failures));
    }
    private static BitmapImage Load(string path)
    {
        var source=new BitmapImage(); source.BeginInit(); source.CacheOption=BitmapCacheOption.OnLoad;
        source.UriSource=new Uri(path); source.EndInit(); source.Freeze(); return source;
    }
}
