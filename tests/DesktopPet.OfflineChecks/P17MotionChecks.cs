using DesktopPet.Animations;
using DesktopPet.Services;

internal static class P17MotionChecks
{
    public static void Run(Action<bool, string> check)
    {
        var failures = new List<string>();
        void Probe(bool value, string name)
        { if (value) check(true, name); else { failures.Add(name); Console.WriteLine("FAIL: " + name); } }
        var clip = AnimationDefinition.CreateClip(AnimationKind.Happy,
            Enumerable.Range(0, 8).Select(i => new AnimationFrame("fixture.png", i)).ToArray());
        var baseDuration = clip.Frames.Count / clip.FramesPerSecond;
        Probe(baseDuration >= 1.5 && baseDuration <= 1.8 && clip.Playback == AnimationPlayback.Once,
            "P17 happy gesture is a relaxed one-shot lasting about 1.6 seconds");
        FetchSequence Sequence(double speed) => new(ToyKind.Ball, new(180, 732), new(168, 168),
            new(0, 0, 1440, 900), false, 230, speed);
        bool ReachCelebration(FetchSequence sequence)
        {
            for (var i = 0; i < 1000 && sequence.Phase != FetchPhase.Celebrating && !sequence.IsComplete; i++) sequence.Advance(.02);
            return sequence.Phase == FetchPhase.Celebrating;
        }
        foreach (var speed in new[] { .25, 1d, 3d })
        {
            var sequence = Sequence(speed);
            Probe(ReachCelebration(sequence), $"P17 fetch reaches celebration at animation speed {speed}");
            var duration = baseDuration / speed;
            var elapsed = 0d;
            while (elapsed + .02 <= duration * .5) { sequence.Advance(.02); elapsed += .02; }
            Probe(sequence.Phase == FetchPhase.Celebrating && Math.Abs(sequence.PhaseProgress - elapsed / duration) < .001,
                $"P17 celebration progresses at the same relaxed rate as happy animation at speed {speed}");
            var position = sequence.PetPosition;
            var toy = sequence.ToyPosition;
            while (!sequence.IsComplete && elapsed < duration + .1) { sequence.Advance(.02); elapsed += .02; }
            Probe(sequence.IsComplete && elapsed >= duration - .03 && sequence.PetPosition == position && sequence.ToyPosition == toy,
                $"P17 celebration finishes after the full happy duration without moving the delivered toy at speed {speed}");
        }
        var constructor = typeof(FetchSequence).GetConstructors().SingleOrDefault(c =>
            c.GetParameters().Any(p => p.Name == "celebrationDurationSeconds"));
        Probe(constructor is not null, "P17 fetch can capture the actual current skin happy duration");
        if (constructor is not null)
        {
            FetchSequence Captured(double? duration) => (FetchSequence)constructor.Invoke([
                ToyKind.Ball, new PetPoint(180, 732), new PetSize(168, 168), new WorkArea(0, 0, 1440, 900),
                false, 230d, 2d, ToySimulation.Size, duration]);
            var custom = Captured(4);
            ReachCelebration(custom);
            for (var i = 0; i < 100; i++) custom.Advance(.02);
            Probe(custom.Phase == FetchPhase.Celebrating && Math.Abs(custom.PhaseProgress - .5) < .001,
                "P17 custom skin duration is captured in real seconds without applying speed twice");
            custom.Cancel(); var pet = custom.PetPosition; var toy = custom.ToyPosition; custom.Advance(.1);
            Probe(custom.IsCancelled && custom.PetPosition == pet && custom.ToyPosition == toy,
                "P17 longer celebration remains immediately cancellable");
            foreach (var invalid in new double?[] { null, 0, -1, double.NaN, double.PositiveInfinity })
            {
                var sequence = Captured(invalid); ReachCelebration(sequence); sequence.Advance(.1);
                Probe(sequence.Phase == FetchPhase.Celebrating && Math.Abs(sequence.PhaseProgress - .125) < .001,
                    $"P17 invalid or absent celebration duration falls back to a finite speed-adjusted happy duration ({invalid})");
            }
        }
        if (failures.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
    }
}
