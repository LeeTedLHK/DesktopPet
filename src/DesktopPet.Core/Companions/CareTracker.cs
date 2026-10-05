using DesktopPet.Models;

namespace DesktopPet.Companions;

public sealed class CareTracker
{
    private DateTimeOffset _lastTick;
    public PetProfile Profile { get; private set; }

    public CareTracker(PetProfile profile, DateTimeOffset now)
    {
        Profile = profile.Normalize() with { UpdatedUtc = now,
            LastBondUtc = profile.LastBondUtc > now ? null : profile.LastBondUtc };
        _lastTick = now;
    }

    public void Advance(DateTimeOffset now, bool enabled, bool sleeping)
    {
        if (now <= _lastTick) return;
        // Suspension/clock jumps never accumulate hours of unattended penalties.
        var hours = Math.Min((now - _lastTick).TotalMinutes, 5) / 60;
        _lastTick = now;
        if (!enabled) return;
        Profile = (Profile with { Fullness = Profile.Fullness - hours * 3,
            Energy = Profile.Energy + hours * (sleeping ? 12 : -6), UpdatedUtc = now }).Normalize();
    }

    public void Pet(DateTimeOffset now, bool enabled) => Reward(now, enabled, 1, false);
    public void Feed(DateTimeOffset now, bool enabled) => Reward(now, enabled, 2, true);

    private void Reward(DateTimeOffset now, bool enabled, double affection, bool feed)
    {
        if (!enabled) return;
        var reward = Profile.LastBondUtc is not { } last || now < last || now - last >= TimeSpan.FromMinutes(1);
        Profile = (Profile with { Affection = Profile.Affection + (reward ? affection : 0),
            Fullness = Profile.Fullness + (feed ? 20 : 0),
            LastBondUtc = reward ? now : Profile.LastBondUtc, UpdatedUtc = now }).Normalize();
    }
}
