namespace DesktopPet.Services;

public sealed class AppLogger
{
    private static readonly object FileLock = new();
    private readonly string _logDirectory;

    public AppLogger(string baseDirectory)
    {
        _logDirectory = Path.Combine(baseDirectory, "logs");
    }

    public void Info(string message)
    {
        Write("INFO", message, null);
    }

    public void Error(string message, Exception? error = null)
    {
        Write("ERROR", message, error);
    }

    private void Write(string level, string message, Exception? error)
    {
        try
        {
            var suffix = error is null ? string.Empty : $"{Environment.NewLine}{error}";
            var entry = $"{DateTimeOffset.Now:O} [{level}] {message}{suffix}{Environment.NewLine}";

            lock (FileLock)
            {
                Directory.CreateDirectory(_logDirectory);
                var path = Path.Combine(_logDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllText(path, entry);
            }
        }
        catch
        {
            // Logging must never bring down the desktop pet.
        }
    }
}
