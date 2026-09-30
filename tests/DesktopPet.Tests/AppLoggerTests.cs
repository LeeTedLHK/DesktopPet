using DesktopPet.Services;

namespace DesktopPet.Tests;

public sealed class AppLoggerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"DesktopPet.Tests.{Guid.NewGuid():N}");

    [Fact]
    public void InfoAndErrorAppendToDailyLog()
    {
        var logger = new AppLogger(_directory);

        logger.Info("started");
        logger.Error("failed", new InvalidOperationException("bad operation"));

        var path = Assert.Single(Directory.GetFiles(Path.Combine(_directory, "logs"), "*.log"));
        var content = File.ReadAllText(path);
        Assert.Contains("INFO", content);
        Assert.Contains("started", content);
        Assert.Contains("ERROR", content);
        Assert.Contains("failed", content);
        Assert.Contains("InvalidOperationException", content);
    }

    [Fact]
    public void LoggingFailureIsSwallowed()
    {
        Directory.CreateDirectory(_directory);
        var fileInsteadOfDirectory = Path.Combine(_directory, "blocked");
        File.WriteAllText(fileInsteadOfDirectory, "occupied");
        var logger = new AppLogger(fileInsteadOfDirectory);

        var exception = Record.Exception(() => logger.Info("cannot be written"));

        Assert.Null(exception);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
