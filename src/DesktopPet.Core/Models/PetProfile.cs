using DesktopPet.Journal;

namespace DesktopPet.Models;

public sealed record PetProfile
{
    public double Affection { get; init; } = 20;
    public double Fullness { get; init; } = 70;
    public double Energy { get; init; } = 80;
    public DateTimeOffset UpdatedUtc { get; init; }
    public DateTimeOffset? LastBondUtc { get; init; }
    public JournalState Journal { get; init; } = new();

    public PetProfile Normalize() => this with
    {
        Affection = Value(Affection, 20), Fullness = Value(Fullness, 70), Energy = Value(Energy, 80),
        Journal = (Journal ?? new JournalState()).Normalize()
    };
    private static double Value(double value, double fallback) => double.IsFinite(value) ? Math.Clamp(value, 0, 100) : fallback;
}
