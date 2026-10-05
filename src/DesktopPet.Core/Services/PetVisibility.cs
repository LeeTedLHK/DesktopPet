namespace DesktopPet.Services;

/// <summary>Keeps manual visibility independent of temporary desktop suppression.</summary>
public sealed class PetVisibility
{
    public bool RequestedVisible { get; private set; }
    public bool FullscreenSuppressed { get; private set; }
    public bool ShouldShow => RequestedVisible && !FullscreenSuppressed;
    public void RequestVisible(bool visible) => RequestedVisible = visible;
    public void SetFullscreenSuppressed(bool suppressed) => FullscreenSuppressed = suppressed;
}
