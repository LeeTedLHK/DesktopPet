using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopPet.Animations;
using DesktopPet.Controllers;
using DesktopPet.Models;
using DesktopPet.Services;
using DesktopPet.Views;

internal static class P7WindowsChecks
{
    private static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(obj)!;

    public static void Run(PetController controller, PetWindow pet, ScreenBoundsService screen,
        Action<bool, string> check, Action<int> pump, Action<FrameworkElement, string> render, string evidence)
    {
        VerifyTransitions(pet, check, pump, evidence);
        controller.ApplySettings(controller.Settings with { AutoBehaviorEnabled = false,
            NightSleepEnabled = false, EnableAnimations = true, AnimationSpeedMultiplier = 1,
            ActivityRange = ActivityRange.Full, ToysEnabled = true });
        var world = Field<PetWorldService>(controller, "_world");
        var state = Field<PetStateMachine>(controller, "_state");
        var timer = Field<DispatcherTimer>(world, "_timer");
        var interval = timer.Interval;
        var area = screen.ActivityWorkArea(new(pet.Width, pet.Height));
        var surface = (FrameworkElement)pet.FindName("PetSurface");
        void Advance()
        {
            typeof(PetWorldService).GetMethod("AdvanceFetch", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(world, [.033]);
        }
        bool Until(FetchPhase target)
        {
            for (var i = 0; i < 1800 && world.CurrentFetchPhase != target && world.IsMoving; i++) Advance();
            return world.CurrentFetchPhase == target;
        }
        foreach (var toy in new[] { ToyKind.Ball, ToyKind.Frisbee })
        {
            foreach (var left in new[] { false, true })
            {
                controller.StopPlay();
                var origin = new PetPoint(area.Left + (area.Width - pet.Width) * (left ? .8 : .2), area.Bottom - pet.Height);
                screen.Move(pet, origin);
                controller.ThrowToy(toy);
                timer.Interval = TimeSpan.FromSeconds(30);
                var initial = screen.Position(pet);
                Advance();
                check(Math.Abs(screen.Position(pet).Y - initial.Y) < 1, $"P7 {toy} chases on the ground without following the flight arc");
                check(Until(FetchPhase.Returning), $"P7 {toy} reaches a carrying return phase");
                check(state.State is PetState.CarryingLeft or PetState.CarryingRight && world.IsToyVisible,
                    "P7 carrying dog keeps toy visible and selects a carrying pose");
                check(controller.PlayStatus.Contains("正在把玩具叼回"), "P7 catch alone does not prematurely complete delivery");
                var animation = Field<AnimationManager>(pet, "_animationManager");
                for (var i = 0; i < 30 && animation.CurrentKind is AnimationKind.TurnLeft or AnimationKind.TurnRight; i++) pump(30);
                pump(130);
                typeof(PetWorldService).GetMethod("AdvanceFetch", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(world,[0d]);
                render(surface, Path.Combine(evidence, $"{toy}-Carry-{(left ? "Left" : "Right")}.png"));
                RenderFetchScene(pet, world, screen, render, Path.Combine(evidence,$"{toy}-Carry-Scene-{(left ? "Left" : "Right")}.png"));
                check(Until(FetchPhase.Dropping), $"P7 {toy} returns before dropping");
                check(Math.Abs(screen.Position(pet).X - origin.X) < 2, "P7 delivery returns to the original throw position");
                check(world.IsToyVisible && world.IsMoving, "P7 dropping remains part of the active fetch session");
                check(Until(FetchPhase.Celebrating), "P7 drop remains visible during the wait for the next throw");
                check(world.IsToyVisible, "P7 delivered toy is briefly shown on the ground");
                for (var i = 0; world.IsMoving && i < 100; i++) Advance();
                check(!world.IsMoving && !world.IsToyVisible && controller.PlayStatus.Contains("叼回啦"),
                    "P7 only complete delivery ends fetch and awards its response");
                timer.Interval = interval;
            }
        }
        foreach (var phase in new[] { FetchPhase.PickingUp, FetchPhase.Returning, FetchPhase.Dropping })
        {
            controller.StopPlay();
            screen.Move(pet, new(area.Left + (area.Width - pet.Width) * .2, area.Bottom - pet.Height));
            controller.ThrowToy(ToyKind.Ball); timer.Interval = TimeSpan.FromSeconds(30);
            check(Until(phase), $"P7 interruption fixture reaches {phase}");
            controller.PetHead();
            check(!world.IsMoving && !world.IsToyVisible && state.State == PetState.Petting,
                $"P7 petting cancels {phase} without stale fetch completion");
            timer.Interval = interval;
        }
        controller.StopPlay(); controller.HidePet(); controller.ShowPet();
    }

    private static void RenderFetchScene(PetWindow pet, PetWorldService world, ScreenBoundsService screen,
        Action<FrameworkElement,string> render, string path)
    {
        // Compose only this test's two owned visuals. No desktop pixels or user windows are captured.
        var toy = Field<Window>(world,"_toy");
        var position = screen.Position(pet);
        var toyPosition = screen.Position(toy);
        var scene = new Canvas { Width=pet.Width+80,Height=pet.Height+80,Background=new SolidColorBrush(Color.FromRgb(48,69,80)) };
        var image = new Image { Source=pet.CurrentFrame,Width=pet.Width-8,Height=pet.Height-8 };
        Canvas.SetLeft(image,44);Canvas.SetTop(image,44);scene.Children.Add(image);
        var prop = new System.Windows.Shapes.Rectangle { Width=32,Height=32,Fill=new VisualBrush((Visual)toy.Content) };
        Canvas.SetLeft(prop,40+toyPosition.X-position.X);Canvas.SetTop(prop,40+toyPosition.Y-position.Y);scene.Children.Add(prop);
        scene.Measure(new Size(scene.Width,scene.Height));scene.Arrange(new Rect(0,0,scene.Width,scene.Height));
        render(scene,path);
    }

    private static void VerifyTransitions(PetWindow pet, Action<bool, string> check, Action<int> pump, string evidence)
    {
        var root = Path.Combine(evidence, "transition-fixture");
        Directory.CreateDirectory(root);
        var source = Path.Combine(AppContext.BaseDirectory, "Assets", "Pet", "WalkRight");
        var images = Directory.GetFiles(source, "*.png").Order().Take(4).ToArray();
        foreach (var kind in new[] { AnimationKind.Idle, AnimationKind.WalkLeft, AnimationKind.WalkRight,
            AnimationKind.RunLeft, AnimationKind.RunRight, AnimationKind.StartLeft, AnimationKind.StartRight,
            AnimationKind.StopLeft, AnimationKind.StopRight, AnimationKind.TurnLeft, AnimationKind.TurnRight,
            AnimationKind.PickupRight, AnimationKind.JumpLeft, AnimationKind.CarryRight })
        {
            Directory.CreateDirectory(Path.Combine(root, kind.ToString()));
            for (var i = 0; i < images.Length; i++) File.Copy(images[i], Path.Combine(root, kind.ToString(), $"frame_{i}.png"), true);
        }
        File.WriteAllText(Path.Combine(root, "mouth-anchors.json"),
            "{\"actions\":{\"CarryRight\":[{\"x\":0.77,\"y\":0.46},{\"x\":0.78,\"y\":0.47},{\"x\":0.79,\"y\":0.48},{\"x\":0.80,\"y\":0.49}]}}");
        var fallback = pet.CurrentFrame!;
        using var manager = new AnimationManager(pet.Dispatcher, _ => { }, root, fallback,
            new AppLogger(evidence), () => 3);
        var completed = new List<AnimationKind>(); manager.PlaybackCompleted += completed.Add;
        void WaitFor(AnimationKind expected)
        {
            for (var i = 0; i < 30 && manager.CurrentKind != expected; i++) pump(30);
        }
        manager.SetState(PetState.Paused, true, true);
        manager.SetState(PetState.RunningRight, true, true);
        check(Field<AnimationKind>(manager, "_currentKind") == AnimationKind.StartRight,
            "P7 movement begins with an authored start presentation");
        manager.SetState(PetState.RunningRight, true, true);
        WaitFor(AnimationKind.RunRight);
        check(Field<AnimationKind>(manager, "_currentKind") == AnimationKind.RunRight,
            "P7 repeated state updates allow start to complete into running");
        manager.SetState(PetState.RunningLeft, true, true);
        check(Field<AnimationKind>(manager, "_currentKind") == AnimationKind.TurnLeft,
            "P7 direction reversal shows the turn pose first");
        manager.SetState(PetState.Dragging, true, true); pump(250);
        check(Field<AnimationKind>(manager, "_currentKind") == AnimationKind.Dragging,
            "P7 dragging discards a queued running transition");
        manager.SetState(PetState.Paused, true, true); manager.SetState(PetState.WalkingLeft, true, true);
        WaitFor(AnimationKind.WalkLeft); manager.SetState(PetState.Paused, true, true);
        check(Field<AnimationKind>(manager, "_currentKind") == AnimationKind.StopLeft,
            "P7 movement brakes before returning to a paused pose");
        WaitFor(AnimationKind.Idle);
        check(Field<AnimationKind>(manager, "_currentKind") == AnimationKind.Idle &&
            !Field<DispatcherTimer>(manager, "_timer").IsEnabled, "P7 stop completes without restarting paused animation");
        check(completed.Count == 0, "P7 motion joins never notify interaction completion handlers");
        manager.SetState(PetState.WalkingRight, true, true); manager.SetState(PetState.WalkingRight, false, true);
        pump(230);
        check(!Field<DispatcherTimer>(manager, "_timer").IsEnabled, "P7 hiding immediately clears presentation playback");
        manager.SetState(PetState.WalkingRight, true, false); pump(230);
        check(!Field<DispatcherTimer>(manager, "_timer").IsEnabled, "P7 disabled animations cannot resume a queued movement");
        manager.SetState(PetState.PickingUpRight, true, true); manager.SetPoseProgress(.8);
        check(manager.CurrentFrameIndex == 3 && !Field<DispatcherTimer>(manager, "_timer").IsEnabled,
            "P7 fetch progress synchronizes pickup pose without a competing animation clock");
        manager.SetState(PetState.JumpingLeft, true, false); manager.SetPoseProgress(.8);
        check(manager.CurrentFrameIndex == 0, "P7 disabled fetch animations retain a stable pose");
        manager.SetState(PetState.CarryingRight, true, true);
        WaitFor(AnimationKind.CarryRight);
        check(manager.CurrentMouthAnchor == new PetPoint(.77, .46), "P7 carrying uses the measured mouth anchor of the current frame");
        typeof(AnimationManager).GetMethod("OnTick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(manager,[null,EventArgs.Empty]);
        check(manager.CurrentMouthAnchor == new PetPoint(.78,.47), "P7 toy anchor follows the next carrying bitmap");

        foreach (var malformed in new[] { "7", "{\"x\":\"invalid\",\"y\":0.46}" })
        {
            File.WriteAllText(Path.Combine(root, "mouth-anchors.json"),
                "{\"actions\":{\"PickupRight\":[" + malformed + "," + malformed + "," + malformed + "," + malformed +
                "],\"CarryRight\":[{\"x\":0.77,\"y\":0.46},{\"x\":0.78,\"y\":0.47},{\"x\":0.79,\"y\":0.48},{\"x\":0.80,\"y\":0.49}]}}");
            using var partial = new AnimationManager(pet.Dispatcher, _ => { }, root, fallback,
                new AppLogger(evidence), () => 1);
            partial.SetState(PetState.CarryingRight, true, false);
            check(partial.CurrentMouthAnchor == new PetPoint(.77, .46),
                $"P7 malformed anchor {malformed} does not discard later valid action anchors");
        }

        var oldRoot = Path.Combine(evidence, "old-skin-fixture");
        Directory.CreateDirectory(Path.Combine(oldRoot, "Idle"));
        File.Copy(images[0], Path.Combine(oldRoot, "Idle", "idle.png"), true);
        using var old = new AnimationManager(pet.Dispatcher, _ => { }, oldRoot, fallback, new AppLogger(evidence), () => 1);
        old.SetState(PetState.Idle, true, true); old.SetState(PetState.RunningLeft, true, true);
        check(Field<AnimationKind>(old, "_currentKind") == AnimationKind.RunLeft,
            "P7 old skin skips a missing start clip safely");
    }
}
