using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using DesktopPet.Models;
using Forms = System.Windows.Forms;

namespace DesktopPet.Services;

public sealed class ScreenBoundsService : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private bool _disposed;
    private Func<AppSettings> _settings = () => new AppSettings();
    private string _activeId = "";
    private PetPoint? _lastPixels;
    public event Action? BoundsChanged;
    public event Action<string>? ActiveMonitorChanged;

    public ScreenBoundsService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        Monitors = ReadMonitors();
        _activeId = MonitorGeometry.Resolve(Monitors, null).Id;
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public IReadOnlyList<MonitorInfo> Monitors { get; private set; }
    public MonitorInfo CurrentMonitor => MonitorGeometry.Resolve(Monitors, _activeId, _lastPixels);
    public WorkArea WorkArea => CurrentMonitor.DipWorkArea;

    public void Configure(Func<AppSettings> settings)
    {
        _settings = settings;
        var value = settings();
        if (value.SavedPositionUsesPixels && value.LastPetX is {} x && value.LastPetY is {} y)
            _lastPixels = new PetPoint(x, y);
        _activeId = MonitorGeometry.Resolve(Monitors, value.SelectedMonitorId, _lastPixels).Id;
    }

    public bool Select(string? id)
    {
        var selected = MonitorGeometry.Resolve(Monitors, id, _lastPixels).Id;
        if (selected == _activeId) return false;
        _activeId = selected;
        ActiveMonitorChanged?.Invoke(selected);
        return true;
    }
    public WorkArea ActivityWorkArea(PetSize size) => ActivityAreaPolicy.Resolve(_settings(), size, WorkArea);
    public PetPoint Clamp(PetPoint point, PetSize size) => ScreenGeometry.Clamp(point, size, ActivityWorkArea(size));
    public PetPoint Restore(double? x, double? y, PetSize size) => ScreenGeometry.Restore(x, y, size, ActivityWorkArea(size));

    public PetPoint Pixels(Window window) => NativeWindowService.PixelPosition(window);
    public PetPoint Position(Window window) => CurrentMonitor.ToDip(Pixels(window));
    public void Move(Window window, PetPoint point)
    {
        var pixels = CurrentMonitor.ToPixels(point);
        NativeWindowService.Place(window, pixels, CurrentMonitor.Scale);
        _lastPixels = pixels;
    }

    public void RestoreWindow(Window window)
    {
        var settings = _settings();
        var size = new PetSize(window.Width, window.Height);
        if (settings.LastPetX is not {} x || settings.LastPetY is not {} y)
        {
            Move(window, Restore(null, null, size));
            return;
        }
        var point = new PetPoint(x, y);
        if (settings.SavedPositionUsesPixels) point = CurrentMonitor.ToDip(point);
        Move(window, Restore(point.X, point.Y, size));
    }

    public void DragTo(Window window, PetPoint pixels, Point cursor)
    {
        if (_settings().AllowCrossMonitorDrag)
            Select(MonitorGeometry.At(Monitors, new PetPoint(cursor.X, cursor.Y)).Id);
        Move(window, Clamp(CurrentMonitor.ToDip(pixels), new PetSize(window.Width, window.Height)));
    }

    public void ClampWindow(Window window) => Move(window,
        Clamp(Position(window), new PetSize(window.Width, window.Height)));

    private static IReadOnlyList<MonitorInfo> ReadMonitors() => Forms.Screen.AllScreens.Select((screen, i) =>
    {
        var bounds = screen.Bounds;
        var work = screen.WorkingArea;
        return new MonitorInfo(screen.DeviceName, $"显示器 {i + 1}",
            new WorkArea(bounds.X, bounds.Y, bounds.Width, bounds.Height),
            new WorkArea(work.X, work.Y, work.Width, work.Height),
            NativeWindowService.MonitorScale(new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2)),
            screen.Primary);
    }).ToArray();

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
            if (_disposed) return;
            Monitors = ReadMonitors();
            Select(_activeId);
            BoundsChanged?.Invoke();
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
