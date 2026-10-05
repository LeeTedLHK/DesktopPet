using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using DesktopPet.Controllers;
using DesktopPet.Models;
using DesktopPet.Services;
using DesktopPet.Views;

internal static class P10FeedingChecks
{
    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    public static void Run(PetController controller, PetWindow pet, ScreenBoundsService screen,
        Action<bool, string> check, Action<int> pump, Action<FrameworkElement, string> render, string evidence)
    {
        var failures = new List<string>();
        void Probe(bool condition, string name)
        {
            if (condition) check(true, name);
            else { failures.Add(name); Console.WriteLine("FAIL: " + name); }
        }
        var original = controller.Settings;
        var state = Field<PetStateMachine>(controller, "_state");
        var world = Field<PetWorldService>(controller, "_world");
        void BeginFeed()
        {
            typeof(PetController).GetField("_lastFeed", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(controller, DateTime.MinValue);
            controller.Feed();
        }
        Window? Bowl() => Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.Title == "DesktopPet 食盆");
        try
        {
            controller.ApplySettings(original with { AutoBehaviorEnabled = false, NightSleepEnabled = false,
                ToysEnabled = false, HomeEnabled = false, EnableAnimations = true,
                AnimationSpeedMultiplier = .25, PetScale = 1, ActivityRange = ActivityRange.Full });
            controller.ShowPet();
            var area = screen.WorkArea;
            screen.Move(pet, screen.Clamp(new(area.Left + area.Width / 2 - pet.Width / 2, area.Bottom - pet.Height),
                new(pet.Width, pet.Height)));
            BeginFeed();
            var bowl = Bowl();
            Probe(state.State == PetState.Feeding && bowl?.IsVisible == true,
                "P10 feeding displays a food bowl even with home and toys disabled");
            Probe(pet.CurrentMouthAnchor.X < .5,
                "P10 default feeding uses the dog's left-facing head instead of the fetch fallback");
            if (bowl is not null)
            {
                var position = screen.Position(pet);
                var bowlPosition = screen.Position(bowl);
                Probe(bowlPosition.X + bowl.Width / 2 < position.X + pet.Width / 2 &&
                    bowlPosition.Y > position.Y + pet.Height / 2,
                    "P10 the bowl sits below the head in front of the dog's paws");
                var style = GetWindowLongPtr(new WindowInteropHelper(bowl).Handle, -20).ToInt64();
                Probe((style & 0x20) != 0 && (style & 0x08000000) != 0 && !bowl.IsHitTestVisible,
                    "P10 the bowl passes clicks through and never activates");
                Probe(bowl.Owner == pet && bowl.Topmost == controller.Settings.Topmost,
                    "P10 the bowl remains in front of the dog and follows its topmost preference");
                RenderScene(pet, bowl, screen, render, Path.Combine(evidence, "p10-feeding-scene.png"));
                var beforeWidth = bowl.Width;
                controller.ApplySettings(controller.Settings with { PetScale = 2, Topmost = false });
                Probe(bowl.IsVisible && bowl.Width > beforeWidth * 1.8 && !bowl.Topmost,
                    "P10 an active meal follows scale and topmost changes");
                RenderScene(pet, bowl, screen, render, Path.Combine(evidence, "p10-feeding-large-scene.png"));
            }
            var feedingCount = controller.Companion.Profile.Journal.FeedingCount;
            controller.Feed();
            Probe(controller.Companion.Profile.Journal.FeedingCount == feedingCount && state.State == PetState.Feeding,
                "P10 feeding cooldown does not award another meal or restart the action");
            state.CompleteTemporaryAction(PetState.Feeding);
            Probe(Bowl()?.IsVisible != true, "P10 finishing the feeding action removes its bowl");
            controller.Feed();
            Probe(Bowl()?.IsVisible != true && state.State != PetState.Feeding,
                "P10 feeding during cooldown after completion cannot leave a stray bowl");

            controller.ApplySettings(controller.Settings with { PetScale = 1, Topmost = true });
            BeginFeed(); controller.PetHead();
            Probe(Bowl()?.IsVisible != true && state.State == PetState.Petting,
                "P10 petting interrupts feeding and removes the bowl");
            BeginFeed(); state.BeginDrag();
            Probe(Bowl()?.IsVisible != true && state.State == PetState.Dragging,
                "P10 dragging cannot leave a feeding bowl behind");
            state.EndDrag();
            BeginFeed(); controller.HidePet();
            Probe(Bowl()?.IsVisible != true, "P10 hiding the dog also hides its feeding bowl");
            controller.ShowPet();
            BeginFeed(); controller.SetFullscreenSuppressed(true);
            Probe(Bowl()?.IsVisible != true, "P10 fullscreen hiding removes the bowl");
            controller.SetFullscreenSuppressed(false);

            controller.ApplySettings(controller.Settings with { EnableAnimations = false });
            BeginFeed();
            Probe(state.State == PetState.Feeding && Bowl()?.IsVisible == true,
                "P10 a static feeding action still displays its bowl");
            pump(1700);
            Probe(state.State == PetState.Paused && Bowl()?.IsVisible != true,
                "P10 the static action watchdog removes the bowl without an animation callback");

            controller.ApplySettings(controller.Settings with { EnableAnimations = true, AnimationSpeedMultiplier = 3 });
            BeginFeed(); pump(900);
            Probe(state.State == PetState.Paused && Bowl()?.IsVisible != true,
                "P10 the real feeding animation completes and removes the bowl");

            controller.ApplySettings(controller.Settings with { AnimationSpeedMultiplier = .25,
                ActivityRange = ActivityRange.Custom, ActivityLeftPercent = 90, ActivityTopPercent = 90,
                ActivityWidthPercent = 10, ActivityHeightPercent = 10 });
            BeginFeed();
            if (Bowl() is { } edgeBowl)
            {
                var bounds = screen.ActivityWorkArea(new(pet.Width, pet.Height));
                var point = screen.Position(edgeBowl);
                Probe(point.X >= bounds.Left - 1 && point.Y >= bounds.Top - 1 &&
                    point.X + edgeBowl.Width <= bounds.Right + 1 && point.Y + edgeBowl.Height <= bounds.Bottom + 1,
                    "P10 the bowl stays inside a small edge activity area");
            }
            else Probe(false, "P10 the bowl exists in an edge activity area");
            controller.ApplySettings(controller.Settings with { EnableAnimations = false });
            Probe(Bowl()?.IsVisible != true, "P10 disabling animation mid-meal removes the completed meal's bowl");
        }
        finally
        {
            controller.SetFullscreenSuppressed(false);
            controller.ApplySettings(original);
            controller.HidePet(); controller.ShowPet();
        }
        check(failures.Count == 0, "P10 feeding checks: " + string.Join("; ", failures));
    }

    internal static void RenderScene(PetWindow pet, Window bowl, ScreenBoundsService screen,
        Action<FrameworkElement, string> render, string path)
    {
        // Only our owned dog and bowl visuals are composed; no desktop pixels.
        var position = screen.Position(pet);
        var bowlPosition = screen.Position(bowl);
        var scene = new Canvas { Width = pet.Width + 64, Height = pet.Height + 64,
            Background = new SolidColorBrush(Color.FromRgb(48, 69, 80)) };
        var image = new Image { Source = pet.CurrentFrame, Width = pet.Width - 8, Height = pet.Height - 8 };
        Canvas.SetLeft(image, 36); Canvas.SetTop(image, 36); scene.Children.Add(image);
        var prop = new System.Windows.Shapes.Rectangle { Width = bowl.Width, Height = bowl.Height,
            Fill = new VisualBrush((Visual)bowl.Content) };
        Canvas.SetLeft(prop, 32 + bowlPosition.X - position.X);
        Canvas.SetTop(prop, 32 + bowlPosition.Y - position.Y); scene.Children.Add(prop);
        scene.Measure(new Size(scene.Width, scene.Height)); scene.Arrange(new Rect(0, 0, scene.Width, scene.Height));
        render(scene, path);
    }

    public static void VerifyDisposal(PetController controller, PetWindow pet, Action<bool, string> check)
    {
        controller.ApplySettings(controller.Settings with { AutoBehaviorEnabled = false, NightSleepEnabled = false,
            EnableAnimations = true, AnimationSpeedMultiplier = .25 });
        controller.ShowPet();
        typeof(PetController).GetField("_lastFeed", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(controller, DateTime.MinValue);
        controller.Feed();
        var bowl = Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.Owner == pet && window.Title == "DesktopPet 食盆");
        var visibleBeforeDisposal = bowl?.IsVisible == true;
        controller.Dispose();
        check(visibleBeforeDisposal && bowl is not null && !bowl.IsVisible && !Application.Current.Windows.OfType<Window>().Contains(bowl),
            "P10 disposing during an active meal closes the bowl window");
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
}
