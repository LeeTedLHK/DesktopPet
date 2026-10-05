using DesktopPet.Models;

namespace DesktopPet.Services;

public enum ToyKind { Ball, Frisbee }

public sealed class ToySimulation
{
    public const double Size = 32;
    private readonly ToyKind _kind;
    private readonly WorkArea _area;
    private PetPoint _velocity;
    private double _elapsed;
    public double Diameter { get; }
    public PetPoint Position { get; private set; }
    public double Angle { get; private set; }
    public bool IsSettled { get; private set; }
    public ToySimulation(ToyKind kind, PetPoint position, PetPoint velocity, WorkArea area, double diameter = Size)
    {
        _kind = kind;
        _area = new(double.IsFinite(area.Left) ? area.Left : 0, double.IsFinite(area.Top) ? area.Top : 0,
            Nonnegative(area.Width), Nonnegative(area.Height));
        _velocity = velocity;
        Diameter = FitDiameter(diameter, _area);
        Position = ScreenGeometry.Clamp(position, new(Diameter, Diameter), _area);
    }
    public void Advance(double seconds)
    {
        if (IsSettled || !double.IsFinite(seconds) || seconds <= 0) return;
        var dt = Math.Min(seconds, .1);
        _elapsed += dt;
        _velocity = _velocity with { Y = _velocity.Y + (_kind == ToyKind.Ball ? 850 : 340) * dt };
        var next = new PetPoint(Position.X + _velocity.X * dt, Position.Y + _velocity.Y * dt);
        var clamped = ScreenGeometry.Clamp(next, new(Diameter, Diameter), _area);
        if (clamped.X != next.X) _velocity = _velocity with { X = -_velocity.X * .5 };
        if (clamped.Y != next.Y)
        {
            _velocity = _velocity with { Y = -_velocity.Y * (_kind == ToyKind.Ball ? .55 : .28), X = _velocity.X * .7 };
            if (next.Y > clamped.Y && Math.Abs(_velocity.Y) < 36) IsSettled = true;
        }
        Position = clamped;
        Angle = (Angle + (_kind == ToyKind.Ball ? 240 : 540) * dt) % 360;
        if (_elapsed >= 4) { IsSettled = true; Position = Position with { Y = Math.Max(_area.Top, _area.Bottom - Diameter) }; }
    }

    internal static double FitDiameter(double diameter, WorkArea area) =>
        Math.Min(double.IsFinite(diameter) && diameter > 0 ? diameter : Size,
            Math.Min(Nonnegative(area.Width), Nonnegative(area.Height)));

    private static double Nonnegative(double value) => double.IsFinite(value) && value > 0 ? value : 0;
}

public static class WorldGeometry
{
    public static PetPoint HomePosition(AppSettings settings, PetSize pet, WorkArea area) => ScreenGeometry.Clamp(
        new(area.Left + Math.Max(0, area.Width - pet.Width) * settings.HomeXPercent / 100,
            area.Top + Math.Max(0, area.Height - pet.Height) * settings.HomeYPercent / 100), pet, area);
}
