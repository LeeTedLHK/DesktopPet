namespace DesktopPet.Services;

public static class StartupCommand
{
    public static string Build(string executable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        if (executable.Contains('"') || executable.Contains('\r') || executable.Contains('\n'))
            throw new ArgumentException("Invalid startup executable path.", nameof(executable));
        return $"\"{executable}\" --startup";
    }
}
