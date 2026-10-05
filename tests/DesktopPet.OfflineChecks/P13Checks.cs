using System.Text.Json;
using DesktopPet.Models;
using DesktopPet.Services;

internal static class P13Checks
{
    private static readonly string[] PropScaleNames = ["BallScale", "FrisbeeScale", "FeedingBowlScale"];

    public static void Run(Action<bool, string> check)
    {
        CheckSettings(check);
        CheckPhysics(check);
        CheckFetch(check);
    }

    private static void CheckSettings(Action<bool, string> check)
    {
        var legacy = JsonSerializer.Deserialize<AppSettings>(
            "{\"PetScale\":1.5,\"ToysEnabled\":false,\"PetName\":\"Legacy\",\"AnimationSpeedMultiplier\":0.63}")!.Normalize();
        var legacyJson = JsonSerializer.SerializeToElement(legacy);
        check(PropScaleNames.All(name => ReadScale(legacy, name) == 1) &&
            legacyJson.TryGetProperty("PowerSavingEnabled", out var saving) && !saving.GetBoolean(),
            "P13 legacy JSON defaults all prop scales to one and power saving off");
        check(legacy.PetScale == 1.5 && !legacy.ToysEnabled && legacy.PetName == "Legacy" &&
            legacy.AnimationSpeedMultiplier == .63, "P13 new settings preserve legacy preferences");

        foreach (var name in PropScaleNames)
        foreach (var (input, expected) in new[] { (-20d, .6), (.6, .6), (.85, .85), (1d, 1d), (1.5, 1.5), (20d, 1.5),
                     (double.NaN, 1d), (double.PositiveInfinity, 1d), (double.NegativeInfinity, 1d) })
        {
            var settings = new AppSettings();
            typeof(AppSettings).GetProperty(name)?.SetValue(settings, input);
            var normalized = settings.Normalize();
            check(ReadScale(normalized, name) == expected,
                $"P13 {name} normalizes {input} to {expected}");
            check(PropScaleNames.Where(other => other != name).All(other => ReadScale(normalized, other) == 1),
                $"P13 changing {name} preserves the other independent scales for {input}");
        }

        var custom = JsonSerializer.Deserialize<AppSettings>(
            "{\"BallScale\":0.65,\"FrisbeeScale\":1.4,\"FeedingBowlScale\":1.15,\"PowerSavingEnabled\":true}")!.Normalize();
        var restored = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(custom))!.Normalize();
        var restoredJson = JsonSerializer.SerializeToElement(restored);
        check(ReadScale(restored, "BallScale") == .65 && ReadScale(restored, "FrisbeeScale") == 1.4 &&
            ReadScale(restored, "FeedingBowlScale") == 1.15 &&
            restoredJson.TryGetProperty("PowerSavingEnabled", out var enabled) && enabled.GetBoolean(),
            "P13 all three custom scales and power saving survive JSON roundtrip");
    }

    private static void CheckPhysics(Action<bool, string> check)
    {
        var area = new WorkArea(-200, -80, 120, 90);
        var legacy = new ToySimulation(ToyKind.Ball, new(100, 100), new(0, 0), area);
        check(legacy.Position == new PetPoint(-112, -22), "P13 omitted diameter retains 32 DIP physics");

        foreach (var kind in Enum.GetValues<ToyKind>())
        foreach (var diameter in new[] { 19.2, 32d, 48d, 96d })
        {
            var toy = CreateToy(kind, new(-500, -500), new(360, -160), area, diameter);
            var expected = Math.Min(diameter, 90);
            var contained = Inside(toy.Position, expected, area);
            for (var i = 0; i < 450; i++)
            {
                toy.Advance(.01);
                contained &= Inside(toy.Position, expected, area);
            }
            check(Diameter(toy) == expected && contained && toy.IsSettled &&
                Math.Abs(toy.Position.Y + expected - 10) < .001,
                $"P13 {kind} diameter {diameter} stays contained and settles at its actual floor");
        }

        var wall = CreateToy(ToyKind.Ball, new(-130, -70), new(400, 0), area, 48);
        wall.Advance(.1);
        var hitWall = wall.Position.X;
        wall.Advance(.1);
        check(hitWall == -128 && wall.Position.X < hitWall, "P13 scaled toy bounces at right edge minus its diameter");
        var floor = CreateToy(ToyKind.Ball, new(-180, -50), new(0, 200), area, 48);
        floor.Advance(.1);
        var hitFloor = floor.Position.Y;
        floor.Advance(.1);
        check(hitFloor == -38 && floor.Position.Y < hitFloor, "P13 scaled ball bounces at ground minus its diameter");

        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1d, 0d })
        {
            var toy = CreateToy(ToyKind.Frisbee, new(-100, 0), new(0, 0), area, invalid);
            for (var i = 0; i < 450; i++) toy.Advance(.01);
            check(Diameter(toy) == 32 && toy.IsSettled && Inside(toy.Position, 32, area),
                $"P13 invalid diameter {invalid} recovers to the compatible size");
        }

        foreach (var (bounds, expected) in new[] {
                     (new WorkArea(-31, -19, 21, 11), 11d), (new WorkArea(-31, -19, 0, 11), 0d),
                     (new WorkArea(-31, -19, 21, 0), 0d) })
        {
            var toy = CreateToy(ToyKind.Ball, new(100, 100), new(250, -220), bounds, 500);
            var contained = Inside(toy.Position, expected, bounds);
            for (var i = 0; i < 450; i++) { toy.Advance(.01); contained &= Inside(toy.Position, expected, bounds); }
            check(Diameter(toy) == expected && contained && toy.IsSettled,
                $"P13 toy fits even a {bounds.Width} by {bounds.Height} offset physics area");
        }
    }

    private static void CheckFetch(Action<bool, string> check)
    {
        // Literal diameters cover pet image widths 128, 160 and 320 at 60%, 100% and 150% prop scale.
        foreach (var (petWidth, diameter) in new[] { (136d, 15.36), (136d, 25.6), (136d, 38.4),
                     (168d, 19.2), (168d, 32d), (168d, 48d), (328d, 38.4), (328d, 64d), (328d, 96d) })
        foreach (var kind in Enum.GetValues<ToyKind>())
        foreach (var compact in new[] { false, true })
        {
            var area = compact ? new WorkArea(-400, -100, petWidth, petWidth) : new WorkArea(-1440, -200, 1440, 900);
            var origin = compact ? new PetPoint(-400, -100) : new PetPoint(-1260, 700 - petWidth);
            var pet = new PetSize(petWidth, petWidth);
            var fetch = CreateFetch(kind, origin, pet, area, false, 150, 1, diameter);
            var contained = true;
            var attached = true;
            var carried = false;
            var sawDrop = false;
            for (var i = 0; i < 2000 && !fetch.IsComplete; i++)
            {
                fetch.Advance(.02);
                contained &= Inside(fetch.PetPosition, petWidth, area) && Inside(fetch.ToyPosition, diameter, area);
                if (fetch.Phase == FetchPhase.Returning || fetch.Phase == FetchPhase.Jumping && fetch.ToyIsAttached)
                {
                    carried |= fetch.Phase == FetchPhase.Returning;
                    attached &= Close(fetch.ToyPosition, ExpectedMouthToy(fetch, pet, area, diameter));
                }
                sawDrop |= fetch.Phase == FetchPhase.Dropping;
            }
            check(Diameter(fetch.Simulation) == diameter && contained && attached && carried && sawDrop &&
                fetch.IsComplete && Close(fetch.PetPosition, origin) &&
                Math.Abs(fetch.ToyPosition.Y + diameter - fetch.GroundLine) < .001,
                $"P13 {kind} fetch pet {petWidth} toy {diameter} completes with contained mouth attachment in {(compact ? "pet-sized" : "negative-origin")} area");
        }

        foreach (var kind in Enum.GetValues<ToyKind>())
        foreach (var diameter in new[] { 19.2, 48d, 500d })
        {
            var bounds = new WorkArea(-31, -19, 21, 11);
            var origin = new PetPoint(-31, -19);
            var fetch = CreateFetch(kind, origin, new(168, 168), bounds, true, 150, 1, diameter);
            var contained = true;
            for (var i = 0; i < 2000 && !fetch.IsComplete; i++)
            {
                fetch.Advance(.02);
                contained &= Inside(fetch.ToyPosition, 11, bounds) && fetch.PetPosition == origin;
            }
            check(Diameter(fetch.Simulation) == 11 && contained && fetch.IsComplete &&
                Math.Abs(fetch.ToyPosition.Y + 11 - bounds.Bottom) < .001,
                $"P13 {kind} fetch bounds requested diameter {diameter} to tiny negative-origin area and terminates");
        }

        foreach (var facingLeft in new[] { false, true })
        {
            var origin = facingLeft ? new PetPoint(-348, 532) : new PetPoint(-1260, 532);
            var fetch = CreateFetch(ToyKind.Ball, origin, new(168, 168), new(-1440, -200, 1440, 900), facingLeft, 230, 1, 48);
            fetch.Advance(.02);
            check(fetch.FacingLeft == facingLeft && (facingLeft ? fetch.PetPosition.X < origin.X : fetch.PetPosition.X > origin.X),
                $"P13 diameter-aware {(facingLeft ? "left" : "right")} chase keeps throw direction on its first frame");
        }

        var bounded = CreateFetch(ToyKind.Ball, new(-400, -100), new(168, 168), new(-400, -100, 168, 168), false, 230, 1, 500);
        for (var i = 0; i < 2000 && !bounded.IsComplete; i++) bounded.Advance(.02);
        check(Diameter(bounded.Simulation) == 146.5 && bounded.IsComplete &&
            Math.Abs(bounded.ToyPosition.Y + 146.5 - 46.5) < .001,
            "P13 fetch clamps oversized prop to the paw ground physics area rather than the window bottom");
    }

    // Reflection lets these behavior probes compile against the pre-P13 implementation for the red run.
    private static ToySimulation CreateToy(ToyKind kind, PetPoint position, PetPoint velocity, WorkArea area, double diameter)
    {
        var constructor = typeof(ToySimulation).GetConstructor([typeof(ToyKind), typeof(PetPoint), typeof(PetPoint), typeof(WorkArea), typeof(double)]);
        return constructor is null ? new(kind, position, velocity, area) :
            (ToySimulation)constructor.Invoke([kind, position, velocity, area, diameter]);
    }

    private static FetchSequence CreateFetch(ToyKind kind, PetPoint origin, PetSize pet, WorkArea area,
        bool facingLeft, double speed, double animationSpeed, double diameter)
    {
        return new(kind, origin, pet, area, facingLeft, speed, animationSpeed, diameter);
    }

    private static double Diameter(ToySimulation toy) =>
        typeof(ToySimulation).GetProperty("Diameter")?.GetValue(toy) is double diameter ? diameter : ToySimulation.Size;

    private static double ReadScale(AppSettings settings, string name) =>
        JsonSerializer.SerializeToElement(settings).TryGetProperty(name, out var value) ? value.GetDouble() : double.NaN;

    private static bool Inside(PetPoint p, double diameter, WorkArea area) =>
        double.IsFinite(p.X) && double.IsFinite(p.Y) && p.X >= area.Left - .001 && p.Y >= area.Top - .001 &&
        p.X + diameter <= area.Right + .001 && p.Y + diameter <= area.Bottom + .001;

    private static bool Close(PetPoint a, PetPoint b) => Math.Abs(a.X - b.X) < .001 && Math.Abs(a.Y - b.Y) < .001;

    private static PetPoint ExpectedMouthToy(FetchSequence fetch, PetSize pet, WorkArea area, double diameter)
    {
        var mouthX = fetch.PetPosition.X + 4 + (pet.Width - 8) * (fetch.FacingLeft ? .22 : .78);
        var mouthY = fetch.PetPosition.Y + 4 + (pet.Height - 8) * .46;
        return new(Math.Clamp(mouthX - diameter / 2, area.Left, Math.Max(area.Left, area.Right - diameter)),
            Math.Clamp(mouthY - diameter / 2, area.Top, Math.Max(area.Top, area.Bottom - diameter)));
    }
}
