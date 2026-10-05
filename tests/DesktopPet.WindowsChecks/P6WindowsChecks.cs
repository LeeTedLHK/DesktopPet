using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopPet.Animations;
using DesktopPet.Controllers;
using DesktopPet.Models;
using DesktopPet.Services;
using DesktopPet.Views;

internal static class P6WindowsChecks
{
    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    public static void Run(PetController controller, PetWindow pet, ScreenBoundsService screen,
        Action<bool, string> check, Action<int> pump, Action<FrameworkElement, string> render,
        string evidence, bool logicOnly = false)
    {
        controller.ApplySettings(controller.Settings with { AutoBehaviorEnabled = false,
            NightSleepEnabled = false, ToysEnabled = true, HomeEnabled = true, EnableAnimations = true,
            ActivityRange = ActivityRange.Full, AnimationSpeedMultiplier = 1 });
        var state = Field<PetStateMachine>(controller, "_state");
        var world = Field<PetWorldService>(controller, "_world");
        var timer = Field<DispatcherTimer>(world, "_timer");
        var interval = timer.Interval;
        var area = screen.ActivityWorkArea(new(pet.Width, pet.Height));
        PetPoint Start(double fraction) => new(area.Left + (area.Width - pet.Width) * fraction,
            area.Bottom - pet.Height);
        var surface = (FrameworkElement)pet.FindName("PetSurface");
        foreach (var kind in new[] { ToyKind.Ball, ToyKind.Frisbee })
        {
            foreach (var left in new[] { false, true })
            {
                screen.Move(pet, Start(left ? .85 : .15));
                // Freeze this owned world's automatic physics before waiting for
                // animation. One real tick is bounded to 100 ms, so a frisbee
                // cannot cross its 120 ms jump threshold during this run check.
                timer.Interval = TimeSpan.FromSeconds(30);
                controller.ThrowToy(kind);
                var expectedState = left ? "RunningLeft" : "RunningRight";
                check(world.IsToyVisible && world.IsMoving && state.State.ToString() == expectedState,
                    $"{kind} immediately starts {expectedState} while automatic behavior is paused");
                var simulation = Field<ToySimulation>(world, "_simulation");
                var position = screen.Position(pet);
                check(Math.Abs(simulation.Position.X + simulation.Diameter / 2 -
                    (position.X + pet.Width / 2)) < 1,
                    "thrown toy center starts on pet center to prevent initial opposite-direction steering");
                typeof(PetWorldService).GetMethod("OnTick", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(world, [null, EventArgs.Empty]);
                pump(110);
                check(state.State.ToString() == expectedState,
                    $"{kind} world ticks keep the running direction");
                check(!Field<ScaleTransform>(pet, "BreathingScale").HasAnimatedProperties &&
                    !Field<TranslateTransform>(pet, "WalkingOffset").HasAnimatedProperties,
                    "authored running frames have no extra breathing or synthetic bob transform");
                if (!logicOnly)
                {
                    var animation = Field<AnimationManager>(pet, "_animationManager");
                    timer.Interval = TimeSpan.FromSeconds(30);
                    for (var i = 0; i < 35 && animation.CurrentKind is AnimationKind.StartLeft or AnimationKind.StartRight; i++) pump(30);
                    var clip = Field<AnimationClip>(animation, "_currentClip");
                    check(clip.Kind.ToString() == (left ? "RunLeft" : "RunRight") &&
                        clip.Frames.Count == 8 && !clip.IsFallback && clip.FramesPerSecond == 12,
                        $"{kind} displays its authored eight-frame 12fps running clip (actual {clip.Kind}, {clip.Frames.Count} frames, {clip.FramesPerSecond} fps, state {state.State})");
                    // Freeze only this owned world's physics timer, allowing the real
                    // animation timer to advance through the entire cycle.
                    timer.Interval = TimeSpan.FromSeconds(30);
                    var before = pet.CurrentFrame;
                    pump(120);
                    check(!ReferenceEquals(before, pet.CurrentFrame), "running displays a new bitmap over time");
                    render(surface, Path.Combine(evidence, $"{kind}-{expectedState}.png"));
                    timer.Interval = interval;
                }
                timer.Interval = interval;
                controller.StopPlay();
                check(state.State == PetState.Paused && !world.IsMoving && !world.IsToyVisible,
                    "stopping a run returns to paused and hides the toy");
            }
        }

        // Exercise the actual world tick with controlled targets on both sides.
        // This timer and simulation belong to this temporary test controller.
        screen.Move(pet, new(area.Left + (area.Width - pet.Width) / 2,
            area.Bottom - pet.Height - 120));
        timer.Interval = TimeSpan.FromSeconds(30);
        controller.ThrowToy(ToyKind.Ball);
        void TargetAndTick(double deltaX)
        {
            var position = screen.Position(pet);
            var simulation = Field<FetchSequence>(world, "_fetch").Simulation;
            typeof(ToySimulation).GetProperty(nameof(ToySimulation.Position))!.SetValue(simulation,
                new PetPoint(position.X + pet.Width / 2 - 16 + deltaX, position.Y + pet.Height - 32));
            typeof(ToySimulation).GetField("_velocity", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(simulation, new PetPoint(0, -100));
            typeof(PetWorldService).GetMethod("OnTick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(world, [null, EventArgs.Empty]);
        }
        TargetAndTick(-100);
        check(state.State.ToString() == "RunningLeft", "toy target reversal selects running left");
        TargetAndTick(0);
        check(state.State.ToString() == "RunningLeft", "vertical toy target preserves current facing");
        TargetAndTick(100);
        check(state.State.ToString() == "RunningRight", "opposite toy target selects running right");
        state.BeginDrag();
        TargetAndTick(100);
        check(state.State == PetState.Dragging && !world.IsMoving && !world.IsToyVisible,
            "dragging cancels a chase without overwriting the dragging state");
        state.EndDrag();
        timer.Interval = interval;

        screen.Move(pet, Start(.15));
        controller.StartFetchChallenge();
        check(state.State.ToString() == "RunningRight" && world.IsToyVisible,
            "fetch challenge uses running for its actual ball chase");
        controller.SetFullscreenSuppressed(true);
        check(!world.IsMoving && !world.IsToyVisible && state.State == PetState.Paused,
            "fullscreen suppression cancels running and its challenge");
        controller.SetFullscreenSuppressed(false);

        screen.Move(pet, Start(.15));
        controller.ThrowToy(ToyKind.Frisbee);
        controller.PetHead();
        check(state.State == PetState.Petting && !world.IsMoving && !world.IsToyVisible,
            "petting interrupts a running chase cleanly");
        controller.HidePet();
        controller.ShowPet();
        controller.ReturnHome();
        check(state.State is PetState.WalkingLeft or PetState.WalkingRight,
            "returning home keeps its walking animation");
        controller.StopPlay();
        controller.FollowCursor();
        pump(100);
        check(world.IsFollowingCursor && state.State is PetState.WalkingLeft or PetState.WalkingRight or PetState.Idle or PetState.Paused,
            "cursor following walks or waits without selecting a running clip");
        controller.StopPlay();

        if (!logicOnly)
        {
            var clips = new AnimationLoader(Path.Combine(AppContext.BaseDirectory, "Assets", "Pet")).Load();
            foreach (var name in new[] { "WalkLeft", "WalkRight", "RunLeft", "RunRight" })
            {
                var clip = clips[Enum.Parse<AnimationKind>(name)];
                var expectedFrames = 8;
                check(clip.Frames.Count == expectedFrames && !clip.IsFallback,
                    $"published {name} contains exactly {expectedFrames} generated frames");
            }
        }
    }
}
