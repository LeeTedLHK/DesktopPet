using System.Diagnostics;
using System.Reflection;
using System.Windows.Threading;
using DesktopPet.Animations;
using DesktopPet.Behaviors;
using DesktopPet.Controllers;
using DesktopPet.Models;
using DesktopPet.Views;

internal static class P16NaturalActionChecks
{
    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;

    public static void Run(PetController controller, PetWindow pet, Action<bool, string> check, Action<int> pump)
    {
        var original = controller.Settings;
        var state = Field<PetStateMachine>(controller, "_state");
        var behavior = Field<BehaviorManager>(controller, "_behavior");
        var animation = Field<AnimationManager>(pet, "_animationManager");
        var randomField = typeof(BehaviorManager).GetField("_random", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var originalRandom = randomField.GetValue(behavior);
        var failures = new List<string>();
        void Probe(bool value, string label)
        {
            if (value) check(true, label);
            else { failures.Add(label); Console.WriteLine("FAIL: " + label); }
        }

        void Prepare(double speed = 1, bool enabled = true, bool powerSaving = false)
        {
            controller.StopPlay();
            controller.ApplySettings(controller.Settings with { AutoBehaviorEnabled = true,
                NaturalBehaviorsEnabled = true, NightSleepEnabled = false, CursorTrackingEnabled = false,
                GrowthEnabled = false, PetPersonality = PetPersonality.Default,
                EnableAnimations = enabled, AnimationSpeedMultiplier = speed, PowerSavingEnabled = powerSaving });
            controller.ShowPet();
            behavior.Resume(TimeSpan.FromMinutes(1));
            Field<DispatcherTimer>(behavior, "_contextTimer").Stop();
        }

        void Begin(PetState action)
        {
            randomField.SetValue(behavior, new ActionRandom(action == PetState.Stretching ? 0 : 12));
            typeof(BehaviorManager).GetMethod("OnDecision", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(behavior, [null, EventArgs.Empty]);
        }

        try
        {
            foreach (var action in new[] { PetState.Stretching, PetState.Yawning })
            {
                Prepare();
                var completions = 0;
                void Completed(AnimationKind kind) { if (kind == AnimationDefinition.ForState(action)) completions++; }
                animation.PlaybackCompleted += Completed;
                try
                {
                    Begin(action);
                    var clock = Stopwatch.StartNew();
                    Probe(state.State == action, $"P16 natural decision starts {action}");
                    var expected = pet.ExpectedAnimationDuration(action);
                    Probe(expected >= TimeSpan.FromSeconds(2.8) && expected <= TimeSpan.FromSeconds(3.2),
                        $"P16 {action} has an approximately three-second authored action at default speed");
                    Probe(Field<DispatcherTimer>(behavior, "_sleepTimer").Interval > expected,
                        $"P16 {action} watchdog allows the entire extended animation");
                    pump(1850);
                    Probe(state.State == action && completions == 0 && animation.CurrentFrameIndex > 0,
                        $"P16 {action} is still progressing after its former completion time");
                    while (state.State == action && clock.Elapsed < TimeSpan.FromSeconds(5)) pump(100);
                    Probe(state.State == PetState.Idle && completions == 1 && clock.Elapsed >= TimeSpan.FromSeconds(2.8),
                        $"P16 {action} plays once and naturally returns to idle after the longer duration");
                    Probe(!Field<DispatcherTimer>(behavior, "_sleepTimer").IsEnabled,
                        $"P16 {action} completion clears its watchdog");
                }
                finally { animation.PlaybackCompleted -= Completed; }

                Prepare(.25, powerSaving: true);
                Begin(action);
                Probe(pet.ExpectedAnimationDuration(action) >= TimeSpan.FromSeconds(11.8) &&
                    Field<DispatcherTimer>(behavior, "_sleepTimer").Interval > pet.ExpectedAnimationDuration(action),
                    $"P16 slow {action} respects the user speed and keeps a sufficient watchdog with power saving");
                controller.PetHead();
                Probe(state.State == PetState.Petting,
                    $"P16 the longer {action} can still be interrupted by petting");

                Prepare(3);
                Begin(action);
                Probe(pet.ExpectedAnimationDuration(action) >= TimeSpan.FromSeconds(.98) &&
                    pet.ExpectedAnimationDuration(action) <= TimeSpan.FromSeconds(1.02),
                    $"P16 fast {action} respects the existing speed preference");
                controller.HidePet();
                Probe(!pet.IsVisible && state.State != action && !Field<DispatcherTimer>(behavior, "_sleepTimer").IsEnabled,
                    $"P16 hiding cancels the longer {action} and its timer");
            }

            Prepare();
            behavior.PlayReminderAction();
            Probe(state.State == PetState.Stretching &&
                Field<DispatcherTimer>(behavior, "_sleepTimer").Interval > pet.ExpectedAnimationDuration(PetState.Stretching),
                "P16 reminder stretching uses the same extended animation and watchdog");
            controller.HidePet();

            Prepare(enabled: false);
            Begin(PetState.Yawning);
            pump(2800);
            Probe(state.State == PetState.Idle,
                "P16 disabling animations retains finite static natural-action completion");
        }
        finally
        {
            controller.HidePet();
            randomField.SetValue(behavior, originalRandom);
            controller.ApplySettings(original);
            controller.ShowPet();
        }
        if (failures.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
    }

    private sealed class ActionRandom(int roll) : Random
    {
        public override int Next(int maxValue) => Math.Min(roll, maxValue - 1);
    }
}
