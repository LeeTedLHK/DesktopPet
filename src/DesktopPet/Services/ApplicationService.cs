using System.Windows;
using DesktopPet.Controllers;
using DesktopPet.Views;

namespace DesktopPet.Services;

public sealed class ApplicationService : IDisposable
{
    private readonly Application _application;
    private readonly AppLogger _logger;
    private PetWindow? _pet;
    private SettingsWindow? _settingsWindow;
    private PetController? _controller;
    private ScreenBoundsService? _screen;
    private TrayIconService? _tray;
    private bool _exitStarted;
    private bool _disposed;

    public ApplicationService(Application application, AppLogger logger)
    {
        _application = application;
        _logger = logger;
    }

    public void Start()
    {
        var settingsService = new SettingsService(SettingsService.DefaultDirectory, _logger);
        var settings = settingsService.Load();
        _pet = new PetWindow(_logger);
        _application.MainWindow = _pet;
        _screen = new ScreenBoundsService(_application.Dispatcher);
        _controller = new PetController(_pet, _screen, settingsService, settings, ShowSettings, () => Exit());
        _controller.SettingsChanged += current =>
        {
            _settingsWindow?.LoadSettings(current);
            UpdateTray();
        };
        _controller.VisibilityChanged += UpdateTray;
        _controller.SettingsSaved += saved => _settingsWindow?.ShowSaveResult(saved);
        _tray = new TrayIconService(_application.Dispatcher, _controller.ShowPet,
            _controller.HidePet, _controller.ToggleAutoBehavior, ShowSettings, () => Exit());
        if (settings.ShowPetOnStartup) _controller.ShowPet();
        UpdateTray();
    }

    private void UpdateTray()
    {
        if (_controller is not null)
            _tray?.UpdateState(_controller.IsPetVisible, _controller.Settings.AutoBehaviorEnabled);
    }

    private void ShowSettings()
    {
        if (_disposed || _controller is null) return;
        if (_settingsWindow is not null)
        {
            if (_settingsWindow.WindowState == WindowState.Minimized) _settingsWindow.WindowState = WindowState.Normal;
            _settingsWindow.Show();
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_controller.Settings);
        _settingsWindow.SettingsEdited += _controller.ApplySettings;
        _settingsWindow.Closed += (_, _) =>
        {
            _settingsWindow = null;
            if (!_disposed) _controller.SaveNow();
        };
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    public void Exit(int exitCode = 0)
    {
        if (_exitStarted) return;
        _exitStarted = true;
        Dispose();
        _application.Shutdown(exitCode);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _logger.Info("Application exiting");
        // Each cleanup step is independent, so a failure cannot strand the tray or timers.
        Cleanup(() => _controller?.Dispose());
        Cleanup(() => _tray?.Dispose());
        Cleanup(() => _settingsWindow?.Close());
        Cleanup(() => _pet?.CloseForExit());
        Cleanup(() => _screen?.Dispose());
    }

    private void Cleanup(Action action)
    {
        try { action(); }
        catch (Exception exception) { _logger.Error("Unexpected exception during cleanup", exception); }
    }
}
