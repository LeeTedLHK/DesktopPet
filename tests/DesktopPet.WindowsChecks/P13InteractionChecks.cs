using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopPet.Animations;
using DesktopPet.Controllers;
using DesktopPet.Models;
using DesktopPet.Services;
using DesktopPet.Views;

internal static class P13InteractionChecks
{
    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    private static AppSettings Edit(AppSettings settings, params (string Name, object Value)[] values)
    {
        var json = JsonSerializer.SerializeToNode(settings)!.AsObject();
        foreach (var (name, value) in values) json[name] = JsonSerializer.SerializeToNode(value);
        return json.Deserialize<AppSettings>()!;
    }
    private static double Value(AppSettings settings, string name) =>
        (double)(typeof(AppSettings).GetProperty(name)?.GetValue(settings) ?? double.NaN);

    public static void Run(PetController controller, PetWindow pet, ScreenBoundsService screen,
        Action<bool, string> check, Action<int> pump, Action<FrameworkElement, string> render, string evidence)
    {
        var failures = new List<string>();
        void Probe(bool condition, string name)
        { if (condition) check(true, name); else { failures.Add(name); Console.WriteLine("FAIL: " + name); } }
        var original = controller.Settings;
        var world = Field<PetWorldService>(controller, "_world");
        var worldTimer = Field<DispatcherTimer>(world, "_timer");
        var interval = worldTimer.Interval;
        var state = Field<PetStateMachine>(controller, "_state");
        try
        {
            var native = typeof(PetWindow).Assembly.GetType("DesktopPet.Services.NativeWindowService")!;
            var extent = native.GetMethod("PixelExtent", BindingFlags.Static | BindingFlags.NonPublic)!;
            Probe((int)extent.Invoke(null, [29 / 1.75, 1.75])! == 29 &&
                (int)extent.Invoke(null, [29.01 / 1.75, 1.75])! == 30,
                "P13 native pixel extent distinguishes 175 percent rounding noise from a genuine fractional pixel");
            var dpiProbe = new Window { WindowStyle = WindowStyle.None, ShowInTaskbar = false,
                ShowActivated = false, AllowsTransparency = true, Background = Brushes.Transparent,
                Width = 32, Height = 32 };
            try
            {
                dpiProbe.Show();
                dpiProbe.Width = dpiProbe.Height = 30 / screen.CurrentMonitor.Scale;
                native.GetMethod("Place")!.Invoke(null, [dpiProbe, screen.CurrentMonitor.ToPixels(new(50, 50)), screen.CurrentMonitor.Scale]);
                GetWindowRect(new System.Windows.Interop.WindowInteropHelper(dpiProbe).Handle, out var rect);
                Probe(rect.Right - rect.Left == 30 && rect.Bottom - rect.Top == 30,
                    "P13 actual monitor DPI places a pixel-aligned prop at its exact physical HWND extent");
            }
            finally { dpiProbe.Close(); }
            controller.ApplySettings(Edit(original with { AutoBehaviorEnabled = false, NightSleepEnabled = false,
                ActivityRange = ActivityRange.Full, PetScale = .8, ToysEnabled = true,
                EnableAnimations = true, AnimationSpeedMultiplier = 3 },
                ("BallScale", 1.5), ("FrisbeeScale", .6), ("FeedingBowlScale", 1.2), ("PowerSavingEnabled", true)));
            controller.ShowPet();
            worldTimer.Interval = TimeSpan.FromSeconds(30);
            foreach (var kind in new[] { ToyKind.Ball, ToyKind.Frisbee })
            {
                controller.ThrowToy(kind);
                var toy = Field<Window>(world, "_toy");
                var simulation = Field<ToySimulation>(world, "_simulation");
                var expected = 32 * (pet.Width - 8) / 160 * (kind == ToyKind.Ball ? 1.5 : .6);
                Console.WriteLine($"P13 probe {kind}: pet={pet.Width:R}, toy={toy.Width:R}, expected={expected:R}, diameter={simulation.GetType().GetProperty("Diameter")?.GetValue(simulation)}, settings={Value(controller.Settings, kind == ToyKind.Ball ? "BallScale" : "FrisbeeScale"):R}");
                Probe(Math.Abs(toy.Width - expected) <= 1 / screen.CurrentMonitor.Scale + .001 &&
                    Math.Abs(toy.Height - expected) <= 1 / screen.CurrentMonitor.Scale + .001,
                    $"P13 {kind} size combines fitted pet dimensions with its independent preference");
                var diameter = simulation.GetType().GetProperty("Diameter")?.GetValue(simulation);
                Probe(diameter is double d && Math.Abs(d - toy.Width) < .01,
                    $"P13 {kind} physics and actual window use the same diameter");
                toy.UpdateLayout();
                Probe(toy.Content is Viewbox box && box.ActualWidth > 0 && Math.Abs(box.ActualWidth - toy.Width) < .5,
                    $"P13 {kind} vector artwork fits its scaled window");
                controller.StopPlay();
            }
            controller.ApplySettings(controller.Settings with { PetScale = 2 });
            controller.ThrowToy(ToyKind.Ball);
            Probe(Math.Abs(Field<Window>(world, "_toy").Width - 96) < .01,
                "P13 large pets scale the same ball proportionally");
            controller.StopPlay();

            // Exercise complete real WPF world choreography with differently sized props.
            var advance = typeof(PetWorldService).GetMethod("AdvanceFetch", BindingFlags.Instance | BindingFlags.NonPublic)!;
            foreach (var scale in new[] { .8, 2d })
            foreach (var kind in new[] { ToyKind.Ball, ToyKind.Frisbee })
            foreach (var left in new[] { false, true })
            {
                controller.ApplySettings(controller.Settings with { PetScale = scale });
                var area = screen.ActivityWorkArea(new(pet.Width, pet.Height));
                screen.Move(pet, new(area.Left + (area.Width - pet.Width) * (left ? .85 : .15), area.Bottom - pet.Height));
                controller.ThrowToy(kind);
                var toy = Field<Window>(world, "_toy");
                var fetch = Field<FetchSequence>(world, "_fetch");
                var attached = false; var dropped = false; var contained = true; var mouthAligned = true;
                for (var i = 0; i < 2000 && world.IsToyVisible; i++)
                {
                    advance.Invoke(world, [.05]);
                    if (!world.IsToyVisible) break;
                    var position = screen.Position(toy);
                    contained &= position.X >= area.Left - 1 && position.Y >= area.Top - 1 &&
                        position.X + toy.Width <= area.Right + 1 && position.Y + toy.Height <= area.Bottom + 1;
                    if (fetch.Phase == FetchPhase.Returning)
                    {
                        attached = true;
                        var petPosition = screen.Position(pet);
                        var mouthX = petPosition.X + 4 + (pet.Width - 8) * pet.CurrentMouthAnchor.X;
                        var mouthY = petPosition.Y + 4 + (pet.Height - 8) * pet.CurrentMouthAnchor.Y;
                        mouthAligned &= Math.Abs(position.X + toy.Width / 2 - mouthX) <= toy.Width / 4 + 1 &&
                            Math.Abs(position.Y + toy.Height / 2 - mouthY) <= toy.Height / 3 + 1;
                    }
                    if (fetch.Phase == FetchPhase.Celebrating)
                        dropped |= Math.Abs(position.Y + ToyVisualFactory.GroundContactY(kind, toy.Height) - fetch.GroundLine) <= 1;
                }
                Probe(attached && dropped && contained && mouthAligned && !world.IsToyVisible,
                    $"P13 {kind} scale {scale} facing {(left ? "left" : "right")} carries at the actual mouth, drops on the paw floor and completes within bounds");
                controller.StopPlay();
            }

            var manager = Field<AnimationManager>(pet, "_animationManager");
            var animationTimer = Field<DispatcherTimer>(manager, "_timer");
            pet.UpdateAnimation(PetState.Idle, true);
            for (var i = 0; i < 30 && manager.CurrentKind != AnimationKind.Idle; i++) pump(20);
            Console.WriteLine($"P13 probe idle: kind={manager.CurrentKind}, interval={animationTimer.Interval}, enabled={animationTimer.IsEnabled}");
            Probe(animationTimer.IsEnabled && animationTimer.Interval >= TimeSpan.FromMilliseconds(250),
                "P13 optional power saving caps idle animation even at 3x speed");
            pet.UpdateAnimation(PetState.Sleeping, true);
            Probe(animationTimer.IsEnabled && animationTimer.Interval >= TimeSpan.FromMilliseconds(500),
                "P13 optional power saving caps sleep animation");
            pet.UpdateAnimation(PetState.Feeding, true);
            Probe(Math.Abs(animationTimer.Interval.TotalSeconds - 1d / 12) < .001 &&
                Math.Abs(manager.ExpectedDuration(PetState.Feeding).TotalSeconds - .5) < .001,
                "P13 feeding cadence and completion duration retain the selected 3x speed");
            pet.UpdateAnimation(PetState.Sleeping, false);
            Probe(!animationTimer.IsEnabled, "P13 hidden restful animation remains stopped");
            controller.ApplySettings(controller.Settings with { EnableAnimations = false });
            pet.UpdateAnimation(PetState.Sleeping, true);
            Probe(!animationTimer.IsEnabled, "P13 power saving cannot restart disabled animations");
            controller.ApplySettings(Edit(controller.Settings with { EnableAnimations = true }, ("PowerSavingEnabled", false)));
            pet.UpdateAnimation(PetState.Idle, true);
            Probe(Math.Abs(animationTimer.Interval.TotalSeconds - 1d / 15) < .001,
                "P13 disabling power saving restores selected idle speed immediately");
            controller.ApplySettings(Edit(controller.Settings, ("PowerSavingEnabled", true)));
            Probe(pet.TryLoadSkin(Path.Combine(AppContext.BaseDirectory, "Assets", "Pet"), out _),
                "P13 skin reload accepts the complete default asset set");
            pet.UpdateAnimation(PetState.Sleeping, true);
            manager = Field<AnimationManager>(pet, "_animationManager");
            animationTimer = Field<DispatcherTimer>(manager, "_timer");
            Probe(animationTimer.Interval >= TimeSpan.FromMilliseconds(500),
                "P13 rest power saving survives skin replacement");

            controller.ApplySettings(controller.Settings with { PetScale = 1, AnimationSpeedMultiplier = .25 });
            typeof(PetController).GetField("_lastFeed", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(controller, DateTime.MinValue);
            controller.Feed();
            var bowl = Field<Window>(world, "_feedingBowl");
            var firstX = screen.Position(bowl).X;
            Console.WriteLine($"P13 probe meal: state={state.State}, visible={bowl.IsVisible}, pet={pet.Width:R}, bowl={bowl.Width:R}, scale={Value(controller.Settings, "FeedingBowlScale"):R}");
            Probe(bowl.IsVisible && Math.Abs(bowl.Width - pet.Width * .3 * 1.2) <= 1 / screen.CurrentMonitor.Scale + .001,
                "P13 independent food bowl scale retains the compact default proportion");
            var tick = typeof(AnimationManager).GetMethod("OnTick", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var mouthYs = new List<double>();
            for (var i = 0; i < 6; i++)
            {
                mouthYs.Add(pet.CurrentMouthAnchor.Y);
                P10FeedingChecks.RenderScene(pet, bowl, screen, render, Path.Combine(evidence, $"p13-meal-{i}.png"));
                if (i < 5) tick.Invoke(manager, [null, EventArgs.Empty]);
                world.RefreshFeedingBowl();
                Probe(Math.Abs(screen.Position(bowl).X - firstX) <= 1,
                    $"P13 chewing frame {i} leaves the bowl planted");
            }
            Probe(mouthYs[2] > mouthYs[0] && mouthYs[3] > mouthYs[5],
                "P13 authored feeding contacts move down to eat and return up to lick");
            controller.ApplySettings(Edit(controller.Settings, ("FeedingBowlScale", .6)));
            Probe(bowl.IsVisible && Math.Abs(bowl.Width - pet.Width * .3 * .6) <= 1 / screen.CurrentMonitor.Scale + .001,
                "P13 bowl size updates during a meal without restarting it");
            state.CompleteTemporaryAction(PetState.Feeding);
            Probe(!bowl.IsVisible, "P13 resized bowl disappears when the meal completes");

            var custom = Edit(controller.Settings, ("BallScale", .873), ("FrisbeeScale", 1.127), ("FeedingBowlScale", .943));
            var settings = new SettingsWindow(custom);
            try
            {
                var edits = new List<AppSettings>();
                settings.SettingsEdited += value => edits.Add(value);
                settings.LoadSettings(custom);
                Probe(edits.Count == 0, "P13 loading settings does not emit edits");
                var ball = settings.FindName("BallScaleSlider") as Slider;
                var frisbee = settings.FindName("FrisbeeScaleSlider") as Slider;
                var bowlSlider = settings.FindName("FeedingBowlScaleSlider") as Slider;
                var power = settings.FindName("PowerSavingCheck") as CheckBox;
                Probe(ball is not null && frisbee is not null && bowlSlider is not null && power is not null,
                    "P13 all independent size and power-saving controls are reachable");
                if (ball is not null && frisbee is not null && bowlSlider is not null && power is not null)
                {
                    Probe(Math.Abs(ball.Value - 87.3) < .001 && Math.Abs(frisbee.Value - 112.7) < .001 &&
                        Math.Abs(bowlSlider.Value - 94.3) < .001,
                        "P13 loading preserves custom size values between slider ticks");
                    power.IsChecked = false;
                    Probe(edits.Count == 1 && Math.Abs(Value(edits[^1], "BallScale") - .873) < .001 &&
                        Math.Abs(Value(edits[^1], "FrisbeeScale") - 1.127) < .001 &&
                        Math.Abs(Value(edits[^1], "FeedingBowlScale") - .943) < .001,
                        "P13 unrelated edits preserve every custom prop size");
                    ball.Value = 140;
                    Probe(edits.Count == 2 && Math.Abs(Value(edits[^1], "BallScale") - 1.4) < .001 &&
                        Math.Abs(Value(edits[^1], "FrisbeeScale") - 1.127) < .001,
                        "P13 one size gesture emits one edit and leaves other props unchanged");
                    settings.Show(); pump(70);
                    Field<TabControl>(settings, "SettingsTabs").SelectedIndex = 3; pump(70);
                    render(settings, Path.Combine(evidence, "p13-settings-props.png"));
                    Field<TabControl>(settings, "SettingsTabs").SelectedIndex = 0;
                    power.BringIntoView(); pump(70);
                    render(settings, Path.Combine(evidence, "p13-settings-power.png"));
                }
            }
            finally { settings.Close(); }
        }
        finally
        {
            worldTimer.Interval = interval;
            controller.StopPlay();
            controller.ApplySettings(original);
            controller.HidePet(); controller.ShowPet();
        }
        check(failures.Count == 0, "P13 interaction checks: " + string.Join("; ", failures));
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
}
