namespace DesktopPet.Services;

public enum FetchPhase { Chasing, Jumping, PickingUp, Returning, Dropping, Celebrating, Complete, Cancelled }

/// <summary>Deterministic fetch choreography. Positions are window/prop top-left coordinates in DIP.</summary>
public sealed class FetchSequence
{
    private readonly ToyKind _kind;
    private readonly PetSize _petSize;
    private readonly WorkArea _area;
    private readonly PetPoint _origin;
    private readonly double _groundY;
    private readonly double _footY;
    private readonly double _speed;
    private readonly double _animationSpeed;
    private readonly double _celebrationDuration;
    private double _phaseSeconds;
    private double _chaseSeconds;
    private double _runSeconds;
    private double _jumpHeight;
    private double _jumpDuration;
    private bool _jumpAttempted;
    private bool _caught;
    private PetPoint _phaseToyOrigin;
    private PetPoint _dropTarget;

    public FetchPhase Phase { get; private set; } = FetchPhase.Chasing;
    public PetPoint PetPosition { get; private set; }
    public PetPoint ToyPosition { get; private set; }
    public double ToyAngle { get; private set; }
    public bool FacingLeft { get; private set; }
    public ToySimulation Simulation { get; }
    public bool IsComplete => Phase == FetchPhase.Complete;
    public bool IsCancelled => Phase == FetchPhase.Cancelled;
    public double GroundLine => _footY;
    public double PhaseProgress => Phase switch
    {
        FetchPhase.Jumping => Math.Clamp(_phaseSeconds / _jumpDuration, 0, 1),
        FetchPhase.PickingUp => Math.Clamp(_phaseSeconds / (.6 / _animationSpeed), 0, 1),
        FetchPhase.Dropping => Math.Clamp(_phaseSeconds / (.5 / _animationSpeed), 0, 1),
        FetchPhase.Celebrating => Math.Clamp(_phaseSeconds / _celebrationDuration, 0, 1),
        _ => 0,
    };
    public bool ToyIsAttached => Phase == FetchPhase.Returning || Phase == FetchPhase.Jumping && _caught ||
        Phase == FetchPhase.PickingUp && PhaseProgress >= .5;

    public FetchSequence(ToyKind kind, PetPoint origin, PetSize petSize, WorkArea area,
        bool facingLeft, double speed, double animationSpeed = 1, double toyDiameter = ToySimulation.Size,
        double? celebrationDurationSeconds = null)
    {
        _kind = kind;
        _petSize = new(Positive(petSize.Width, 168), Positive(petSize.Height, 168));
        _area = new(Finite(area.Left, 0), Finite(area.Top, 0),
            Positive(area.Width, _petSize.Width), Positive(area.Height, _petSize.Height));
        _origin = ScreenGeometry.Clamp(origin, _petSize, _area);
        PetPosition = _origin;
        _groundY = _origin.Y;
        _footY = Math.Clamp(_groundY + 4 + Math.Max(0, _petSize.Height - 8) * 228 / 256,
            _area.Top, _area.Bottom);
        _speed = Math.Clamp(Positive(speed, 230), 150, 1500);
        _animationSpeed = Math.Clamp(Positive(animationSpeed, 1), .25, 3);
        _celebrationDuration = Positive(celebrationDurationSeconds.GetValueOrDefault(), 1.6 / _animationSpeed);
        FacingLeft = facingLeft;
        var direction = _origin.X + _petSize.Width / 2 > _area.Left + _area.Width / 2 ? -1 : 1;
        // Centering horizontally avoids a false initial turn caused by aiming at the prop's left edge.
        // Frisbees launch at mouth height so the same bounded jump works across pet scales.
        var physicsArea = new WorkArea(_area.Left, _area.Top, _area.Width, Math.Max(0, _footY - _area.Top));
        var launchDiameter = ToySimulation.FitDiameter(toyDiameter, physicsArea);
        var toyY = kind == ToyKind.Frisbee ? MouthCenter().Y - launchDiameter / 2 : _footY - launchDiameter;
        Simulation = new(kind, new(_origin.X + (_petSize.Width - launchDiameter) / 2, toyY),
            new(direction * 250, kind == ToyKind.Ball ? -220 : -160), physicsArea, launchDiameter);
        ToyPosition = ClampToy(Simulation.Position);
    }

    public void Advance(double seconds)
    {
        if (IsComplete || IsCancelled || !double.IsFinite(seconds) || seconds <= 0) return;
        var dt = Math.Min(seconds, .1);
        _phaseSeconds += dt;
        switch (Phase)
        {
            case FetchPhase.Chasing:
                _chaseSeconds += dt;
                AdvanceToy(dt);
                ChaseHorizontally(dt);
                if (_kind == ToyKind.Frisbee && CanJump())
                {
                    _jumpAttempted = true;
                    _jumpHeight = Math.Min(Math.Min(60, _groundY - _area.Top),
                        Math.Max(24, MouthCenter().Y - ToyPosition.Y - Simulation.Diameter / 2 + 25));
                    _jumpDuration = Math.Clamp(.7 / _animationSpeed, .28, .7);
                    SetPhase(FetchPhase.Jumping);
                }
                else if (Simulation.IsSettled && AtToy())
                {
                    _phaseToyOrigin = ToyPosition;
                    SetPhase(FetchPhase.PickingUp);
                }
                break;

            case FetchPhase.Jumping:
                if (!_caught)
                {
                    AdvanceToy(dt);
                    // During a jump align the mouth rather than the body's center. If the mouth
                    // is already ahead, let the frisbee approach instead of reversing in midair.
                    var mouthOffset = 4 + Math.Max(0, _petSize.Width - 8) * (FacingLeft ? .22 : .78);
                    var catchX = ToyPosition.X + Simulation.Diameter / 2 - mouthOffset;
                    if (FacingLeft ? catchX < PetPosition.X : catchX > PetPosition.X)
                        MoveHorizontally(catchX, dt);
                }
                var progress = Math.Clamp(_phaseSeconds / _jumpDuration, 0, 1);
                PetPosition = ClampPet(new(PetPosition.X, _groundY - 4 * _jumpHeight * progress * (1 - progress)));
                if (!_caught && !Simulation.IsSettled && progress >= .3 && progress <= .85)
                {
                    var mouth = MouthCenter();
                    _caught = Math.Abs(mouth.X - ToyPosition.X - Simulation.Diameter / 2) <= 30 &&
                        Math.Abs(mouth.Y - ToyPosition.Y - Simulation.Diameter / 2) <= 24;
                }
                if (_caught) AttachToy();
                if (progress >= 1)
                {
                    PetPosition = PetPosition with { Y = _groundY };
                    if (_caught) StartReturn();
                    else SetPhase(FetchPhase.Chasing);
                }
                break;

            case FetchPhase.PickingUp:
                var pickupProgress = Math.Clamp(_phaseSeconds / (.6 / _animationSpeed), 0, 1);
                ToyPosition = ClampToy(Lerp(_phaseToyOrigin, MouthToyPosition(), pickupProgress * pickupProgress));
                if (pickupProgress >= 1) StartReturn();
                break;

            case FetchPhase.Returning:
                MoveHorizontally(_origin.X, dt);
                AttachToy();
                if (Math.Abs(PetPosition.X - _origin.X) <= .001)
                {
                    _phaseToyOrigin = ToyPosition;
                    _dropTarget = ClampToy(new(ToyPosition.X, _footY - Simulation.Diameter));
                    SetPhase(FetchPhase.Dropping);
                }
                break;

            case FetchPhase.Dropping:
                var dropProgress = Math.Clamp(_phaseSeconds / (.5 / _animationSpeed), 0, 1);
                // Accelerate down from the mouth instead of teleporting the prop to the floor.
                ToyPosition = ClampToy(Lerp(_phaseToyOrigin, _dropTarget, dropProgress * dropProgress));
                if (dropProgress >= 1) SetPhase(FetchPhase.Celebrating);
                break;

            case FetchPhase.Celebrating:
                if (_phaseSeconds >= _celebrationDuration) SetPhase(FetchPhase.Complete);
                break;
        }
    }

    public void Cancel()
    {
        if (IsComplete || IsCancelled) return;
        SetPhase(FetchPhase.Cancelled);
    }

    private void AdvanceToy(double dt)
    {
        Simulation.Advance(dt);
        ToyPosition = ClampToy(Simulation.Position);
        ToyAngle = Simulation.Angle;
    }

    private void ChaseHorizontally(double dt) =>
        MoveHorizontally(ToyPosition.X + (Simulation.Diameter - _petSize.Width) / 2, dt);

    private void MoveHorizontally(double targetX, double dt)
    {
        var target = ClampPet(new(targetX, _groundY));
        var delta = target.X - PetPosition.X;
        _runSeconds += dt;
        var step = _speed * Math.Min(1, _runSeconds / .25) * dt;
        if (Math.Abs(delta) > 2) FacingLeft = delta < 0;
        var nextX = Math.Abs(delta) <= step ? target.X : PetPosition.X + Math.Sign(delta) * step;
        PetPosition = ClampPet(new(nextX, _groundY));
    }

    private bool CanJump()
    {
        if (_jumpAttempted || Simulation.IsSettled || _chaseSeconds < .12 || _groundY - _area.Top < 12) return false;
        var mouth = MouthCenter();
        var toyCenter = new PetPoint(ToyPosition.X + Simulation.Diameter / 2, ToyPosition.Y + Simulation.Diameter / 2);
        return Math.Abs(mouth.X - toyCenter.X) <= 70 && toyCenter.Y >= mouth.Y - 60 && toyCenter.Y <= mouth.Y + 20;
    }

    private bool AtToy()
    {
        var target = ClampPet(new(ToyPosition.X + (Simulation.Diameter - _petSize.Width) / 2, _groundY));
        return Math.Abs(target.X - PetPosition.X) <= 2;
    }

    private void StartReturn()
    {
        SetPhase(FetchPhase.Returning);
        _runSeconds = 0;
        if (Math.Abs(_origin.X - PetPosition.X) > 2) FacingLeft = _origin.X < PetPosition.X;
        AttachToy();
    }

    private PetPoint MouthCenter() => new(PetPosition.X + 4 + Math.Max(0, _petSize.Width - 8) * (FacingLeft ? .22 : .78),
        PetPosition.Y + 4 + Math.Max(0, _petSize.Height - 8) * .46);

    private PetPoint MouthToyPosition()
    {
        var mouth = MouthCenter();
        return ClampToy(new(mouth.X - Simulation.Diameter / 2, mouth.Y - Simulation.Diameter / 2));
    }

    private void AttachToy() { ToyPosition = MouthToyPosition(); ToyAngle = 0; }
    private PetPoint ClampPet(PetPoint point) => ScreenGeometry.Clamp(point, _petSize, _area);
    private PetPoint ClampToy(PetPoint point) => ScreenGeometry.Clamp(point, new(Simulation.Diameter, Simulation.Diameter), _area);
    private void SetPhase(FetchPhase phase) { Phase = phase; _phaseSeconds = 0; }
    private static PetPoint Lerp(PetPoint from, PetPoint to, double progress) =>
        new(from.X + (to.X - from.X) * progress, from.Y + (to.Y - from.Y) * progress);
    private static double Positive(double value, double fallback) => double.IsFinite(value) && value > 0 ? value : fallback;
    private static double Finite(double value, double fallback) => double.IsFinite(value) ? value : fallback;
}
