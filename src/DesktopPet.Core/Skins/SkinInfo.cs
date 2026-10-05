using DesktopPet.Animations;

namespace DesktopPet.Skins;

public sealed record SkinInfo(string Id, string Name, string RootPath, string? PreviewPath,
    IReadOnlyList<AnimationKind> MissingActions)
{
    public string MissingActionsSummary => MissingActions.Count == 0
        ? "All actions available"
        : $"Missing actions (fallback used): {string.Join(", ", MissingActions)}";

    public string DisplaySummary => $"{Name} — {MissingActionsSummary}";

    public override string ToString() => Name;
}
