using DesktopPet.Services;

internal static class P7Checks
{
    public static void Run(Action<bool, string> check)
    {
        var area = new WorkArea(0, 0, 1440, 900);
        var size = new PetSize(168, 168);
        var origin = new PetPoint(180, 732);
        var ball = new FetchSequence(ToyKind.Ball, origin, size, area, false, 150);
        var groundOnly = true;
        var sawPickup = false;
        var sawCarry = false;
        var sawDrop = false;
        var sawCelebrate = false;
        var carriedAtMouth = true;
        var pickupSettled = true;
        var phaseOrder = new List<FetchPhase> { ball.Phase };
        var elapsed = 0d;
        while (!ball.IsComplete && elapsed < 15)
        {
            ball.Advance(.02);
            elapsed += .02;
            groundOnly &= Math.Abs(ball.PetPosition.Y - 732) < .001;
            if (phaseOrder[^1] != ball.Phase) phaseOrder.Add(ball.Phase);
            sawPickup |= ball.Phase == FetchPhase.PickingUp;
            if (ball.Phase == FetchPhase.PickingUp) pickupSettled &= ball.Simulation.IsSettled;
            if (ball.Phase == FetchPhase.Returning)
            {
                sawCarry = true;
                // 168-DIP window has 160-DIP image: mouth center at x + 39.2 or 128.8, y + 77.6.
                carriedAtMouth &= Math.Abs(ball.ToyPosition.X + 16 - ball.PetPosition.X - (ball.FacingLeft ? 39.2 : 128.8)) < .001 &&
                    Math.Abs(ball.ToyPosition.Y + 16 - ball.PetPosition.Y - 77.6) < .001;
            }
            sawDrop |= ball.Phase == FetchPhase.Dropping;
            sawCelebrate |= ball.Phase == FetchPhase.Celebrating;
        }
        check(groundOnly, "P7 ball chase and return stay on one ground plane");
        check(sawPickup && pickupSettled, "P7 ball settles before pickup animation");
        check(sawCarry && carriedAtMouth, "P7 returning toy stays attached to directional mouth");
        check(sawDrop && sawCelebrate && phaseOrder.SequenceEqual(new[] { FetchPhase.Chasing, FetchPhase.PickingUp,
            FetchPhase.Returning, FetchPhase.Dropping, FetchPhase.Celebrating, FetchPhase.Complete }),
            "P7 delivery completes only after pickup carry drop and celebration");
        check(ball.IsComplete && Math.Abs(ball.PetPosition.X - 180) < .001 && Math.Abs(ball.PetPosition.Y - 732) < .001,
            "P7 fetch returns to exact throw origin");
        check(Math.Abs(ball.ToyPosition.Y + 32 - 878.5) < .001, "P7 dropped ball rests at opaque paw baseline");
        check(elapsed * 3 < 35, "P7 normal three-round fetch fits about thirty-five seconds");
        var finalPosition = ball.PetPosition;
        var finalToy = ball.ToyPosition;
        ball.Advance(1);
        check(ball.IsComplete && ball.PetPosition == finalPosition && ball.ToyPosition == finalToy,
            "P7 completed fetch remains inert with delivered toy visible");

        var frisbee = new FetchSequence(ToyKind.Frisbee, origin, size, area, false, 230);
        var jumped = false;
        var airCaught = false;
        var boundedJump = true;
        var groundedReturn = true;
        var previous = frisbee.PetPosition;
        for (var i = 0; i < 750 && !frisbee.IsComplete; i++)
        {
            frisbee.Advance(.02);
            jumped |= frisbee.Phase == FetchPhase.Jumping;
            if (frisbee.Phase == FetchPhase.Jumping)
            {
                boundedJump &= frisbee.PetPosition.Y >= 672 && frisbee.PetPosition.Y <= 732 &&
                    Math.Abs(frisbee.PetPosition.Y - previous.Y) < 12;
                airCaught |= !frisbee.Simulation.IsSettled &&
                    Math.Abs(frisbee.ToyPosition.Y + 16 - frisbee.PetPosition.Y - 77.6) < .001;
            }
            if (frisbee.Phase == FetchPhase.Returning) groundedReturn &= Math.Abs(frisbee.PetPosition.Y - 732) < .001;
            previous = frisbee.PetPosition;
        }
        check(jumped && airCaught, "P7 accessible frisbee is caught in air during jump");
        check(boundedJump && groundedReturn, "P7 jump lifts at most sixty DIP then lands before carry");
        check(frisbee.IsComplete && frisbee.PetPosition == origin, "P7 frisbee completes a delivery to origin");

        foreach (var petWidth in new[] { 136d, 168d, 328d })
        foreach (var movementSpeed in new[] { 150d, 240d })
        {
            var scaled = new FetchSequence(ToyKind.Frisbee, new(180, 900 - petWidth), new(petWidth, petWidth),
                area, false, movementSpeed);
            for (var i = 0; i < 1000 && scaled.Phase != FetchPhase.Returning && !scaled.IsComplete; i++) scaled.Advance(.02);
            check(scaled.Phase == FetchPhase.Returning && !scaled.Simulation.IsSettled,
                $"P7 accessible frisbee air catch works at pet size {petWidth} speed {movementSpeed}");
        }

        foreach (var phase in new[] { FetchPhase.Chasing, FetchPhase.Jumping, FetchPhase.PickingUp,
                     FetchPhase.Returning, FetchPhase.Dropping, FetchPhase.Celebrating })
        {
            var sequence = new FetchSequence(phase == FetchPhase.Jumping ? ToyKind.Frisbee : ToyKind.Ball,
                origin, size, area, false, 230);
            for (var i = 0; i < 1000 && sequence.Phase != phase && !sequence.IsComplete; i++) sequence.Advance(.02);
            var reached = sequence.Phase == phase;
            var pet = sequence.PetPosition;
            var toy = sequence.ToyPosition;
            sequence.Cancel();
            sequence.Advance(.1);
            check(reached && sequence.IsCancelled && !sequence.IsComplete && sequence.Phase == FetchPhase.Cancelled &&
                sequence.PetPosition == pet && sequence.ToyPosition == toy, $"P7 cancelling {phase} freezes fetch without completion");
        }

        foreach (var left in new[] { false, true })
        {
            var x = left ? 1092 : 180;
            var sequence = new FetchSequence(ToyKind.Ball, new(x, 732), size, area, left, 230);
            sequence.Advance(.02);
            check(sequence.FacingLeft == left && (left ? sequence.PetPosition.X <= x : sequence.PetPosition.X >= x),
                $"P7 {(left ? "left" : "right")} throw has no opposite first-frame turn");
            for (var i = 0; i < 1000 && !sequence.IsComplete; i++) sequence.Advance(.02);
            check(sequence.IsComplete && Math.Abs(sequence.PetPosition.X - x) < .001,
                $"P7 {(left ? "left" : "right")} delivery returns correctly");
        }

        var invalidTime = new FetchSequence(ToyKind.Ball, origin, size, area, false, 230);
        foreach (var dt in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1d, 0d }) invalidTime.Advance(dt);
        check(invalidTime.PetPosition == origin && invalidTime.Phase == FetchPhase.Chasing,
            "P7 invalid elapsed inputs cannot move or advance fetch");
        var largeStep = new FetchSequence(ToyKind.Ball, origin, size, area, false, 230);
        var boundedStep = new FetchSequence(ToyKind.Ball, origin, size, area, false, 230);
        largeStep.Advance(1000);
        boundedStep.Advance(.1);
        check(largeStep.PetPosition == boundedStep.PetPosition && largeStep.ToyPosition == boundedStep.ToyPosition,
            "P7 long dispatcher gaps advance at most one tenth second");

        var slowMovement = new FetchSequence(ToyKind.Ball, origin, size, area, false, 150);
        var fastMovement = new FetchSequence(ToyKind.Ball, origin, size, area, false, 240);
        slowMovement.Advance(.02);
        var firstStep = slowMovement.PetPosition.X - origin.X;
        slowMovement.Advance(.02);
        var secondStep = slowMovement.PetPosition.X - origin.X - firstStep;
        check(firstStep > 0 && secondStep > firstStep * 1.5 && secondStep < 3,
            "P7 chase accelerates gradually instead of starting at full speed");
        for (var i = 0; i < 8; i++) slowMovement.Advance(.02);
        for (var i = 0; i < 10; i++) fastMovement.Advance(.02);
        check(fastMovement.PetPosition.X - origin.X > (slowMovement.PetPosition.X - origin.X) * 1.4,
            "P7 selected movement speed affects chase distance");

        foreach (var tiny in new[] { false, true })
        {
            var bounds = tiny ? new WorkArea(-400, -100, 168, 168) : new WorkArea(-1440, -200, 1440, 900);
            var point = tiny ? new PetPoint(-400, -100) : new PetPoint(-1260, 532);
            var sequence = new FetchSequence(ToyKind.Frisbee, point, size, bounds, true, 150);
            var contained = true;
            for (var i = 0; i < 1500 && !sequence.IsComplete; i++)
            {
                sequence.Advance(.02);
                contained &= Inside(sequence.PetPosition, 168, 168, bounds) && Inside(sequence.ToyPosition, 32, 32, bounds);
            }
            check(contained && sequence.IsComplete && sequence.PetPosition == point,
                $"P7 {(tiny ? "pet-sized" : "negative-origin")} area contains toy and entire pet through fetch");
        }

        var slowAnimation = PhaseSeconds(.25);
        var fastAnimation = PhaseSeconds(3);
        check(slowAnimation.Pickup > fastAnimation.Pickup * 5 && slowAnimation.Drop > fastAnimation.Drop * 5,
            "P7 pickup and drop durations respect animation speed");
        var sanitized = new FetchSequence(ToyKind.Ball, origin, size, area, false, double.NaN, double.PositiveInfinity);
        for (var i = 0; i < 2000 && !sanitized.IsComplete; i++) sanitized.Advance(.02);
        check(sanitized.IsComplete && double.IsFinite(sanitized.PetPosition.X) && double.IsFinite(sanitized.ToyAngle),
            "P7 invalid speed inputs recover with finite delivery");
    }

    private static bool Inside(PetPoint p, double width, double height, WorkArea a) =>
        double.IsFinite(p.X) && double.IsFinite(p.Y) && p.X >= a.Left - .001 && p.Y >= a.Top - .001 &&
        p.X + width <= a.Right + .001 && p.Y + height <= a.Bottom + .001;

    private static (double Pickup, double Drop) PhaseSeconds(double animationSpeed)
    {
        var sequence = new FetchSequence(ToyKind.Ball, new(180, 732), new(168, 168), new(0, 0, 1440, 900), false, 230, animationSpeed);
        var pickup = 0d;
        var drop = 0d;
        for (var i = 0; i < 2000 && !sequence.IsComplete; i++)
        {
            if (sequence.Phase == FetchPhase.PickingUp) pickup += .02;
            if (sequence.Phase == FetchPhase.Dropping) drop += .02;
            sequence.Advance(.02);
        }
        return (pickup, drop);
    }
}
