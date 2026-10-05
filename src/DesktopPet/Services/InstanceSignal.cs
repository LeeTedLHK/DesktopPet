using System.Windows.Threading;

namespace DesktopPet.Services;

public sealed class InstanceSignal : IDisposable
{
    public const string DefaultName = @"Local\DesktopPet.ShowExisting";
    private readonly EventWaitHandle _signal;
    private readonly RegisteredWaitHandle _wait;
    private volatile bool _disposed;
    public InstanceSignal(Dispatcher dispatcher, Action show, string name = DefaultName)
    {
        _signal = new EventWaitHandle(false, EventResetMode.AutoReset, name);
        _wait = ThreadPool.RegisterWaitForSingleObject(_signal, (_, _) =>
        {
            if (_disposed || dispatcher.HasShutdownStarted) return;
            dispatcher.BeginInvoke(new Action(() => { if (!_disposed) show(); }));
        }, null, Timeout.Infinite, false);
    }
    public static bool Notify(string name = DefaultName)
    {
        try { using var signal = EventWaitHandle.OpenExisting(name); return signal.Set(); }
        catch (Exception ex) when (ex is WaitHandleCannotBeOpenedException or UnauthorizedAccessException) { return false; }
    }
    public void Dispose()
    {
        _disposed = true;
        _wait.Unregister(null);
        _signal.Dispose();
    }
}
