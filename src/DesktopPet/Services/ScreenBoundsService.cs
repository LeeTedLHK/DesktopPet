using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DesktopPet.Services;

public sealed class ScreenBoundsService : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private bool _disposed;
    public event Action? BoundsChanged;

    public ScreenBoundsService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public WorkArea WorkArea
    {
        get
        {
            var area = SystemParameters.WorkArea;
            return new WorkArea(area.Left, area.Top, area.Width, area.Height);
        }
    }

    public PetPoint Clamp(PetPoint point, PetSize size) => ScreenGeometry.Clamp(point, size, WorkArea);
    public PetPoint Restore(double? x, double? y, PetSize size) => ScreenGeometry.Restore(x, y, size, WorkArea);

    private void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.WorkArea)) NotifyBoundsChanged();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => NotifyBoundsChanged();

    private void NotifyBoundsChanged()
    {
        if (_disposed || _dispatcher.HasShutdownStarted) return;
        _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (!_disposed) BoundsChanged?.Invoke();
        }));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }
}
