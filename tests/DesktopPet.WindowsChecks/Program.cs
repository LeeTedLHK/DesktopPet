using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopPet.Animations;
using DesktopPet.Controllers;
using DesktopPet.Models;
using DesktopPet.Services;
using DesktopPet.Views;

internal static class Program
{
    private static int _passed;
    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
        _passed++;
    }
    private static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
    private static void Render(FrameworkElement element, string path)
    {
        element.UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(element);
        var target = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth * dpi.DpiScaleX),
            (int)Math.Ceiling(element.ActualHeight * dpi.DpiScaleY), 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
        target.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(target));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    [STAThread]
    private static int Main(string[] args)
    {
        var evidence = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/verification/p12-windows");
        Directory.CreateDirectory(evidence);
        var temporary = Path.Combine(Path.GetTempPath(), "DesktopPet.WindowsChecks." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var logger = new AppLogger(temporary);
        var pet = new PetWindow(logger);
        using var screen = new ScreenBoundsService(app.Dispatcher);
        var companionNavigation = false;
        using var controller = new PetController(pet, screen, new SettingsService(temporary, logger),
            new AppSettings { AutoBehaviorEnabled = false, NightSleepEnabled = false, PreciseHitTestEnabled = false }, () => { }, () => { },
            () => companionNavigation = true);
        var state = Field<PetStateMachine>(controller, "_state");
        var surface = Field<FrameworkElement>(pet, "PetSurface");
        try
        {
            controller.ShowPet();
            Pump(100);
            if (args.Contains("--p17-only"))
            {
                P17VisualMotionChecks.Run(controller, pet, Check, Pump, Render, evidence);
                Console.WriteLine($"{_passed} P17 Windows integration checks passed.");
                return 0;
            }
            if (args.Contains("--p16-only"))
            {
                P16NaturalActionChecks.Run(controller, pet, Check, Pump);
                Console.WriteLine($"{_passed} P16 Windows integration checks passed.");
                return 0;
            }
            if (args.Contains("--p15-only"))
            {
                P15DiaryFocusChecks.Run(controller, pet, Check, Pump, Render, evidence);
                Console.WriteLine($"{_passed} P15 Windows integration checks passed.");
                return 0;
            }
            if (args.Contains("--p14-only"))
            {
                P14UsabilityChecks.Run(controller, pet, screen, Check, Pump, Render, evidence);
                Console.WriteLine($"{_passed} P14 Windows integration checks passed.");
                return 0;
            }
            if (args.Contains("--p13-only"))
            {
                P13InteractionChecks.Run(controller, pet, screen, Check, Pump, Render, evidence);
                Console.WriteLine($"{_passed} P13 Windows integration checks passed.");
                return 0;
            }
            if (args.Contains("--p12-only"))
            {
                P12RestAndPollingChecks.Run(controller, pet, Check, Pump);
                Console.WriteLine($"{_passed} P12 Windows integration checks passed.");
                return 0;
            }
            if (args.Contains("--p10-only"))
            {
                P10FeedingChecks.Run(controller, pet, screen, Check, Pump, Render, evidence);
                P10FeedingChecks.VerifyDisposal(controller, pet, Check);
                Console.WriteLine($"{_passed} P10 Windows integration checks passed.");
                return 0;
            }
            if (args.Contains("--p9-only") || args.Contains("--p9-save-only"))
            {
                P9WindowsChecks.Run(controller, pet, Check, Pump, Render, evidence, args.Contains("--p9-save-only"));
                Console.WriteLine($"{_passed} P9 Windows integration checks passed.");
                return 0;
            }
            if (args.Contains("--p8-only") || args.Contains("--p8-tray-only"))
            {
                if (!args.Contains("--p8-tray-only")) P8WindowsChecks.Run(Check, Pump, Render, evidence);
                P8TrayChecks.Run(Check);
                if (!args.Contains("--p8-tray-only")) P8SkinIntegrationChecks.Run(pet, Check, evidence);
                Console.WriteLine($"{_passed} P8 Windows integration checks passed.");
                return 0;
            }
            if (args.Contains("--p7-only"))
            {
                P7WindowsChecks.Run(controller, pet, screen, Check, Pump, Render, evidence);
                Console.WriteLine($"{_passed} P7 Windows integration checks passed.");
                return 0;
            }
            if (args.Contains("--p6-only"))
            {
                P6WindowsChecks.Run(controller, pet, screen, Check, Pump, Render, evidence,
                    args.Contains("--logic-only"));
                Console.WriteLine($"{_passed} P6 Windows integration checks passed.");
                return 0;
            }
            if (args.Contains("--p5-only"))
            {
                P5WindowsChecks.Run(controller,pet,screen,Check,Pump,Render,evidence);
                Console.WriteLine($"{_passed} P5 Windows integration checks passed.");
                return 0;
            }
            if (args.Contains("--p4-only"))
            {
                P4WindowsChecks.Run(controller,pet,screen,Check,Pump,Render,evidence);
                Console.WriteLine($"{_passed} focused P4 Windows checks passed.");
                return 0;
            }
            Check(pet.IsVisible && controller.IsPetVisible, "WPF pet shows");
            Check(Field<MenuItem>(pet, "PetHeadItem").Header.ToString() == "摸摸头" &&
                Field<MenuItem>(pet, "FeedItem").Header.ToString() == "喂食", "interaction menu exists");
            controller.SetFullscreenSuppressed(true);
            Check(!pet.IsVisible && controller.WantsPetVisible, "fullscreen hides actual WPF window");
            controller.HidePet();
            controller.SetFullscreenSuppressed(false);
            Check(!pet.IsVisible, "manual hide survives WPF fullscreen restore");
            controller.ShowPet();
            controller.SetFullscreenSuppressed(true);
            controller.SetFullscreenSuppressed(false);
            Check(pet.IsVisible, "WPF fullscreen exit restores pet");
            controller.ToggleClickThrough();
            var handle = new WindowInteropHelper(pet).Handle;
            Check((GetWindowLongPtr(handle, -20).ToInt64() & 0x20) != 0, "native WS_EX_TRANSPARENT enabled");
            controller.ToggleClickThrough();
            Check((GetWindowLongPtr(handle, -20).ToInt64() & 0x20) == 0, "native mouse interaction restored");
            Check((GetWindowLongPtr(handle, -20).ToInt64() & 0x08000000) != 0, "pet never activates on mouse input");

            controller.PetHead();
            Check(state.State == PetState.Petting, "head petting plays while paused");
            var bubble = Field<Window>(Field<SpeechBubbleService>(controller, "_speech"), "_bubble");
            Check(bubble.IsVisible, "speech bubble visible");
            Check((GetWindowLongPtr(new WindowInteropHelper(bubble).Handle, -20).ToInt64() & 0x20) != 0,
                "speech bubble lets mouse input through");
            Render(surface, Path.Combine(evidence, "petting.png"));
            Render((FrameworkElement)bubble.Content, Path.Combine(evidence, "bubble.png"));
            for (var attempt = 0; state.State != PetState.Paused && attempt < 30; attempt++) Pump(100);
            Check(state.State == PetState.Paused, "generated one-shot returns to paused");
            controller.Feed();
            Check(state.State == PetState.Feeding, "feeding plays");
            controller.Feed();
            Check(Field<TextBlock>(Field<SpeechBubbleService>(controller, "_speech"), "_text").Text.Contains("刚刚吃过"),
                "feeding cooldown responds without restarting");
            controller.ApplySettings(controller.Settings with { EnableAnimations = false });
            Check(state.State == PetState.Paused, "disabling animations completes active interaction");
            controller.PetHead();
            Pump(1650);
            Check(state.State == PetState.Paused, "static interaction completes");

            // Losing mouse capture after interrupting a static interaction must not strand the state.
            controller.PetHead();
            typeof(PetController).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(controller, [surface, new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                    { RoutedEvent = UIElement.MouseLeftButtonDownEvent }]);
            surface.ReleaseMouseCapture();
            Pump(1700);
            Check(state.State == PetState.Paused, "lost capture cancels interrupted static interaction");

            controller.ApplySettings(controller.Settings with { AutoBehaviorEnabled = true,
                NightSleepEnabled = true, NightSleepStartHour = DateTime.Now.Hour,
                NightSleepEndHour = (DateTime.Now.Hour + 1) % 24 });
            Field<object>(controller, "_behavior").GetType().GetField("_wakeUntil", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Field<object>(controller, "_behavior"), DateTime.MinValue);
            Pump(700);
            Check(state.State == PetState.Sleeping, "nighttime starts real WPF sleep state");
            controller.ApplySettings(controller.Settings with { NightSleepEnabled = false });
            Check(state.State == PetState.Idle, "disabling night sleep wakes pet");

            controller.ApplySettings(controller.Settings with { EnableAnimations = true, AnimationSpeedMultiplier = 0.25 });
            var behavior = Field<object>(controller, "_behavior");
            behavior.GetType().GetField("_random", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(behavior, new FixedRandom());
            behavior.GetType().GetMethod("OnDecision", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(behavior, [null, EventArgs.Empty]);
            Check(Field<DispatcherTimer>(behavior, "_sleepTimer").Interval.TotalSeconds >= 6,
                "slow natural animation gets enough time to finish");
            state.CompleteTemporaryAction(PetState.Stretching);
            typeof(PetController).GetMethod("Interact", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(controller, [PetState.Happy, "slow animation check"]);
            Check(Field<DispatcherTimer>(controller, "_interactionTimer").Interval >= pet.ExpectedAnimationDuration(PetState.Happy),
                "slow authored happy animation is not truncated by watchdog");
            controller.ApplySettings(controller.Settings with { AnimationSpeedMultiplier = 1 });
            typeof(PetController).GetMethod("Interact", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(controller, [PetState.Happy, "speed change check"]);
            controller.ApplySettings(controller.Settings with { AnimationSpeedMultiplier = 0.25 });
            var reviewFailures = new List<string>();
            void Probe(bool condition, string name)
            {
                if (condition) Check(true, name);
                else { reviewFailures.Add(name); Console.WriteLine("FAIL: " + name); }
            }
            Probe(Field<DispatcherTimer>(controller, "_interactionTimer").Interval >= pet.ExpectedAnimationDuration(PetState.Happy),
                "mid-action slowdown extends watchdog");
            state.BeginDrag();
            typeof(PetController).GetField("_pointerOrigin", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(controller, (Point?)new Point(0, 0));
            controller.HidePet();
            Probe(!bubble.IsVisible, "hiding during drag leaves bubble hidden");
            controller.ShowPet();
            controller.ApplySettings(controller.Settings with { AnimationSpeedMultiplier = 1,
                NaturalBehaviorsEnabled = false, CursorTrackingEnabled = true });
            GetCursorPos(out var pointer);
            var dpi = VisualTreeHelper.GetDpi(pet);
            var area = screen.WorkArea;
            var near = ScreenGeometry.Clamp(new(pointer.X / dpi.DpiScaleX - 180, pointer.Y / dpi.DpiScaleY - 80),
                new(pet.Width, pet.Height), area);
            pet.Left = near.X;
            pet.Top = near.Y;
            behavior.GetType().GetField("_nextLook", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(behavior, DateTime.MinValue);
            behavior.GetType().GetField("_lastCursor", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(behavior, (Point?)new Point(pointer.X - 20, pointer.Y));
            behavior.GetType().GetMethod("OnContext", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(behavior, [null, EventArgs.Empty]);
            Probe(state.State is PetState.LookingLeft or PetState.LookingRight,
                "cursor tracking works independently of random stretch/yawn");
            behavior.GetType().GetField("_lastCursor", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(behavior, (Point?)new Point(pointer.X - 20, pointer.Y));
            behavior.GetType().GetField("_nextLook", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(behavior, DateTime.UtcNow.AddSeconds(12));
            behavior.GetType().GetMethod("OnContext", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(behavior, [null, EventArgs.Empty]);
            state.CompleteTemporaryAction(state.State);
            behavior.GetType().GetField("_nextLook", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(behavior, DateTime.MinValue);
            behavior.GetType().GetMethod("OnContext", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(behavior, [null, EventArgs.Empty]);
            Probe(state.State == PetState.Idle, "stationary cursor does not cause delayed look after cooldown");
            if (reviewFailures.Count > 0) throw new InvalidOperationException(string.Join("; ", reviewFailures));
            controller.PetHead();

            Field<MenuItem>(pet, "CompanionItem").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Check(companionNavigation, "pet status menu uses companion navigation callback");
            P3WindowsChecks.Run(controller, pet, screen, Check, Pump, Render, evidence);
            P4WindowsChecks.Run(controller, pet, screen, Check, Pump, Render, evidence);
            P5WindowsChecks.Run(controller, pet, screen, Check, Pump, Render, evidence);
            P6WindowsChecks.Run(controller, pet, screen, Check, Pump, Render, evidence);
            P7WindowsChecks.Run(controller, pet, screen, Check, Pump, Render, evidence);
            P8WindowsChecks.Run(Check, Pump, Render, evidence);
            P8TrayChecks.Run(Check);
            P8SkinIntegrationChecks.Run(pet, Check, evidence);
            P9WindowsChecks.Run(controller, pet, Check, Pump, Render, evidence);
            P10FeedingChecks.Run(controller, pet, screen, Check, Pump, Render, evidence);
            P12RestAndPollingChecks.Run(controller, pet, Check, Pump);
            P13InteractionChecks.Run(controller, pet, screen, Check, Pump, Render, evidence);
            P14UsabilityChecks.Run(controller, pet, screen, Check, Pump, Render, evidence);
            P15DiaryFocusChecks.Run(controller, pet, Check, Pump, Render, evidence);
            P16NaturalActionChecks.Run(controller, pet, Check, Pump);
            P17VisualMotionChecks.Run(controller, pet, Check, Pump, Render, evidence);
            using var hotkeys = new GlobalHotkeyService(controller.ToggleVisibility, controller.ToggleClickThrough, logger);
            hotkeys.SetEnabled(true);
            Check(!hotkeys.Status.Contains("失败"), "global hotkeys register");
            var source = Field<HwndSource>(hotkeys, "_source");
            PostMessage(source.Handle, 0x0312, new IntPtr(1), IntPtr.Zero);
            Pump(100);
            Check(!controller.WantsPetVisible && !pet.IsVisible, "WM_HOTKEY hides pet");
            PostMessage(source.Handle, 0x0312, new IntPtr(1), IntPtr.Zero);
            Pump(100);
            Check(controller.WantsPetVisible && pet.IsVisible, "WM_HOTKEY shows pet");
            PostMessage(source.Handle, 0x0312, new IntPtr(2), IntPtr.Zero);
            Pump(100);
            Check(controller.Settings.ClickThrough, "WM_HOTKEY toggles click-through");
            hotkeys.SetEnabled(false);
            Check(RegisterHotKey(IntPtr.Zero, 77, 0x4003, 0x50), "disabling releases native shortcut");
            hotkeys.SetEnabled(true);
            Check(hotkeys.Status.Contains("Ctrl+Alt+P") && hotkeys.Status.Contains("失败"), "hotkey conflicts are visible");
            UnregisterHotKey(IntPtr.Zero, 77);
            hotkeys.SetEnabled(false);

            var settings = new SettingsWindow(controller.Settings);
            settings.SetRuntimeStatus(hotkeys.Status);
            settings.Show();
            Pump(100);
            Render(settings, Path.Combine(evidence, "settings-top.png"));
            var scroll = FindVisual<ScrollViewer>(settings)!;
            scroll.ScrollToBottom();
            Pump(100);
            Render(settings, Path.Combine(evidence, "settings-bottom.png"));
            Check(Field<CheckBox>(settings, "FullscreenAutoHideCheck").IsChecked == true &&
                Field<ComboBox>(settings, "SleepStartCombo").Items.Count == 24,
                "settings load new options and 24-hour times");
            settings.Close();
            controller.HidePet();
            Check(!bubble.IsVisible, "hide also closes bubble");
            P10FeedingChecks.VerifyDisposal(controller, pet, Check);
            controller.Dispose();
            Check(!Field<DispatcherTimer>(pet, "_hitTimer").IsEnabled, "dispose stops alpha cursor polling");
            Check(!Field<DispatcherTimer>(Field<object>(controller, "_behavior"), "_contextTimer").IsEnabled,
                "dispose stops context polling");
            Check(!Field<DispatcherTimer>(controller, "_interactionTimer").IsEnabled, "dispose stops interaction watchdog");
            Check(!Field<DispatcherTimer>(controller.Companion, "_timer").IsEnabled, "dispose stops companion timer");
            Console.WriteLine($"{_passed} Windows integration checks passed.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
        finally
        {
            controller.Dispose();
            pet.CloseForExit();
            app.Shutdown();
            Directory.Delete(temporary, true);
        }
    }

    private static T? FindVisual<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent is T found) return found;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var result = FindVisual<T>(VisualTreeHelper.GetChild(parent, i));
            if (result is not null) return result;
        }
        return null;
    }
    private sealed class FixedRandom : Random
    {
        public override int Next(int maxValue) => 0;
    }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X; public int Y; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr window, int id);
}
