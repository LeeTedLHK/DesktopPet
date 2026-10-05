using System.Windows;
using DesktopPet.Controllers;
using DesktopPet.Views;
using DesktopPet.Skins;
using DesktopPet.Models;

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
    private DesktopEnvironmentService? _desktop;
    private GlobalHotkeyService? _hotkeys;
    private bool _hotkeysEnabled;
    private bool _exitStarted;
    private bool _disposed;
    private StartupService? _startup;
    private InstanceSignal? _instanceSignal;
    private SkinCatalog? _skins;
    private bool _started;
    private bool _revealPending;

    public ApplicationService(Application application, AppLogger logger)
    {
        _application = application;
        _logger = logger;
    }

    public void Start()
    {
        _instanceSignal = new InstanceSignal(_application.Dispatcher, RevealExisting);
        var settingsService = new SettingsService(SettingsService.DefaultDirectory, _logger);
        var settings = settingsService.Load();
        _pet = new PetWindow(_logger);
        _skins = new SkinCatalog(Path.Combine(AppContext.BaseDirectory, "Assets", "Pet"),
            Path.Combine(SettingsService.DefaultDirectory, "Skins"), message => _logger.Info(message));
        var skin = _skins.Resolve(settings.SelectedSkinId);
        if (skin.Id != "default" && !_pet.TryLoadSkin(skin.RootPath, out _)) skin = _skins.Resolve("default");
        settings = settings with { SelectedSkinId = skin.Id };
        _startup = new StartupService(Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "DesktopPet.exe"));
        if (!_startup.SetEnabled(settings.StartWithWindows)) settings = settings with { StartWithWindows = _startup.IsEnabled };
        _application.MainWindow = _pet;
        _screen = new ScreenBoundsService(_application.Dispatcher);
        _controller = new PetController(_pet, _screen, settingsService, settings, ShowSettings, () => Exit(), ShowCompanion);
        _desktop = new DesktopEnvironmentService(_application.Dispatcher, () => _screen.CurrentMonitor.Id);
        _desktop.FullscreenChanged += _controller.SetFullscreenSuppressed;
        _controller.SetFullscreenSuppressed(_desktop.IsFullscreen);
        _hotkeys = new GlobalHotkeyService(_controller.ToggleVisibility, _controller.ToggleClickThrough, _logger);
        _hotkeysEnabled = settings.GlobalHotkeysEnabled;
        _hotkeys.SetEnabled(_hotkeysEnabled);
        _controller.SettingsChanged += current =>
        {
            if (current.GlobalHotkeysEnabled != _hotkeysEnabled)
            {
                _hotkeysEnabled = current.GlobalHotkeysEnabled;
                _hotkeys.SetEnabled(_hotkeysEnabled);
            }
            _settingsWindow?.LoadSettings(current);
            _settingsWindow?.SetMonitorChoices(_screen.Monitors);
            _settingsWindow?.SetRuntimeStatus(_hotkeys.Status);
            _settingsWindow?.SetStartupStatus(_startup.Status);
            UpdateTray();
        };
        _controller.VisibilityChanged += UpdateTray;
        _controller.SettingsSaved += saved => _settingsWindow?.ShowSaveResult(saved);
        _controller.CompanionChanged += UpdateCompanion;
        _controller.PlayStatusChanged += UpdatePlayStatus;
        _tray = new TrayIconService(_application.Dispatcher, _controller.ShowPet,
            _controller.HidePet, _controller.ToggleAutoBehavior, ShowSettings, () => Exit(),
            _controller.ToggleClickThrough, _controller.PetHead, _controller.Feed,
            ShowCompanion, _controller.StartFocus, _controller.ToggleFocusPause, _controller.StopFocus, _controller.SetTrayMenuOpen,
            _controller.ResetPosition, () => _controller.ThrowToy(ToyKind.Ball),
            () => _controller.ThrowToy(ToyKind.Frisbee), _controller.ReturnHome,
            _controller.FollowCursor, _controller.StartFetchChallenge, _controller.StopPlay, _controller.SavePhoto,
            _controller.SummonToCursor);
        _started = true;
        if (settings.ShowPetOnStartup || _revealPending) _controller.ShowPet();
        UpdateTray();
        UpdateCompanion();
    }

    private void RevealExisting()
    {
        if (_disposed) return;
        _logger.Info("Existing instance revealed by startup signal");
        if (!_started) _revealPending = true;
        else _controller?.ShowPet();
    }

    private void UpdateTray()
    {
        if (_controller is not null)
        {
            _tray?.UpdatePetName(_controller.Settings.PetName);
            _tray?.UpdateState(_controller.IsPetVisible, _controller.Settings.AutoBehaviorEnabled,
                _controller.Settings.ClickThrough, _controller.IsFullscreenSuppressed, _controller.WantsPetVisible,
                _controller.IsPlaying, _controller.Settings.ToysEnabled, _controller.Settings.HomeEnabled);
            _settingsWindow?.SetPetAvailability(_controller.IsPetVisible, _controller.IsFullscreenSuppressed);
        }
    }

    private void ShowSettings()
    {
        if (_disposed || _controller is null) return;
        if (_settingsWindow is not null)
        {
            _settingsWindow.ShowSaveResult(_controller.LastSaveSucceeded);
            if (_settingsWindow.WindowState == WindowState.Minimized) _settingsWindow.WindowState = WindowState.Normal;
            _settingsWindow.Show();
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_controller.Settings);
        _settingsWindow.ShowSaveResult(_controller.LastSaveSucceeded);
        _settingsWindow.SetMonitorChoices(_screen!.Monitors);
        _settingsWindow.SetSkinChoices(_skins!.List());
        _settingsWindow.SetStartupStatus(_startup!.Status);
        _settingsWindow.SetRuntimeStatus(_hotkeys?.Status ?? "快捷键未启用，请使用系统托盘");
        _settingsWindow.SettingsEdited += edited =>
        {
            if (edited.StartWithWindows != _controller.Settings.StartWithWindows && !_startup.SetEnabled(edited.StartWithWindows))
                edited = edited with { StartWithWindows = _startup.IsEnabled };
            _controller.ApplySettings(edited);
        };
        _settingsWindow.ResetPositionRequested += _controller.ResetPosition;
        _settingsWindow.SkinSelected += id => SwitchSkin(id);
        _settingsWindow.SkinImportRequested += ImportSkin;
        _settingsWindow.SkinExportRequested += ExportSkin;
        _settingsWindow.BackupRequested += BackupData;
        _settingsWindow.OpenDataRequested += OpenDataDirectory;
        _settingsWindow.OpenLogsRequested += OpenLogsDirectory;
        _settingsWindow.ShowPetRequested += _controller.ShowPet;
        _settingsWindow.FocusStartRequested += _controller.StartFocus;
        _settingsWindow.FocusPauseRequested += _controller.ToggleFocusPause;
        _settingsWindow.FocusStopRequested += _controller.StopFocus;
        _settingsWindow.SnoozeRemindersRequested += _controller.Companion.SnoozeHealthReminders;
        _settingsWindow.ResumeRemindersRequested += _controller.Companion.ResumeHealthReminders;
        _settingsWindow.ExportJournalRequested += ExportJournal;
        _settingsWindow.ExportFilteredJournalRequested += filter => ExportJournalSelection(filter);
        _settingsWindow.BallRequested += () => _controller.ThrowToy(ToyKind.Ball);
        _settingsWindow.FrisbeeRequested += () => _controller.ThrowToy(ToyKind.Frisbee);
        _settingsWindow.HomeRequested += _controller.ReturnHome;
        _settingsWindow.FollowRequested += _controller.FollowCursor;
        _settingsWindow.ChallengeRequested += _controller.StartFetchChallenge;
        _settingsWindow.StopPlayRequested += _controller.StopPlay;
        _settingsWindow.PhotoRequested += _controller.SavePhoto;
        _settingsWindow.OpenPhotosRequested += _controller.OpenPhotoFolder;
        _settingsWindow.RewardRequested += _controller.SayAchievement;
        UpdateCompanion();
        UpdatePlayStatus();
        _settingsWindow.Closed += (_, _) =>
        {
            _settingsWindow = null;
            if (!_disposed) _controller.SaveNow();
        };
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void ShowCompanion()
    {
        ShowSettings();
        _settingsWindow?.ShowCompanionPage();
    }

    private void SwitchSkin(string id)
    {
        if (_skins is null || _pet is null || _controller is null) return;
        var skin = _skins.Resolve(id);
        if (_pet.TryLoadSkin(skin.RootPath, out var error))
        {
            _controller.ApplySettings(_controller.Settings with { SelectedSkinId = skin.Id });
            _settingsWindow?.SetSkinStatus("皮肤已切换");
        }
        else
        {
            _settingsWindow?.LoadSettings(_controller.Settings);
            _settingsWindow?.SetSkinStatus(error);
        }
    }

    private void ImportSkin(bool zip)
    {
        if (_skins is null) return;
        try
        {
            string? path;
            if (zip)
            {
                var dialog = new Microsoft.Win32.OpenFileDialog { Title = "导入皮肤 ZIP", Filter = "皮肤压缩包 (*.zip)|*.zip" };
                path = dialog.ShowDialog(_settingsWindow) == true ? dialog.FileName : null;
            }
            else
            {
                var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "选择含 Idle 动画的皮肤文件夹" };
                path = dialog.ShowDialog(_settingsWindow) == true ? dialog.FolderName : null;
            }
            if (path is null) return;
            var imported = zip ? _skins.ImportZip(path) : _skins.ImportFolder(path);
            _settingsWindow?.SetSkinChoices(_skins.List());
            SwitchSkin(imported.Id);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { _settingsWindow?.SetSkinStatus("导入失败：" + ex.Message); _logger.Error("Skin import failed", ex); }
    }

    private void ExportSkin()
    {
        if (_skins is null || _controller is null || _settingsWindow is null) return;
        try
        {
            var skin = _skins.Resolve(_controller.Settings.SelectedSkinId);
            var safeName = string.Concat(skin.Name.Select(character =>
                Path.GetInvalidFileNameChars().Contains(character) ? '_' : character)).TrimEnd(' ', '.');
            var dialog = new Microsoft.Win32.SaveFileDialog { Title = "导出当前皮肤", Filter = "皮肤压缩包 (*.zip)|*.zip",
                DefaultExt = ".zip", AddExtension = true, OverwritePrompt = true,
                FileName = (string.IsNullOrEmpty(safeName) ? "DesktopPet" : safeName) + "-皮肤.zip" };
            if (dialog.ShowDialog(_settingsWindow) != true) return;
            _skins.ExportZip(skin.Id, dialog.FileName);
            _settingsWindow.SetSkinStatus("皮肤已导出：" + dialog.FileName);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or System.Security.SecurityException)
        {
            _settingsWindow.SetSkinStatus("导出失败：" + error.Message);
            _logger.Error("Skin export failed", error);
        }
    }

    private void BackupData()
    {
        if (_controller is null || _settingsWindow is null) return;
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog { Title = "备份设置、养成与日记",
                Filter = "DesktopPet 数据备份 (*.zip)|*.zip", DefaultExt = ".zip", AddExtension = true,
                OverwritePrompt = true, FileName = $"DesktopPet-数据备份-{DateTime.Now:yyyyMMdd-HHmmss}.zip" };
            if (dialog.ShowDialog(_settingsWindow) != true) return;
            DataBackupService.Create(dialog.FileName, _controller.Settings, _controller.Companion.Profile, DateTimeOffset.UtcNow);
            _settingsWindow.SetDataStatus("备份已保存：" + dialog.FileName);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or System.Text.Json.JsonException or System.Security.SecurityException)
        {
            _settingsWindow.SetDataStatus("备份失败：" + error.Message);
            _logger.Error("Data backup failed", error);
        }
    }

    private void ExportJournal()
        => ExportJournalSelection(null);

    private void ExportJournalSelection(DesktopPet.Journal.JournalFilter? filter)
    {
        if (_controller is null || _settingsWindow is null) return;
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog { Title = filter is null ? "导出全部陪伴日记" : "导出当前筛选日记",
                Filter = "文本日记 (*.txt)|*.txt", DefaultExt = ".txt", AddExtension = true,
                OverwritePrompt = true, FileName = $"DesktopPet-陪伴日记-{DateTime.Now:yyyyMMdd-HHmmss}.txt" };
            if (dialog.ShowDialog(_settingsWindow) != true) return;
            if (filter is null) JournalExportService.Create(dialog.FileName, _controller.Settings.PetName,
                _controller.Companion.Profile, DateTimeOffset.UtcNow);
            else JournalExportService.CreateFiltered(dialog.FileName, _controller.Settings.PetName,
                _controller.Companion.Profile, DateTimeOffset.UtcNow, filter);
            _settingsWindow.SetJournalExportStatus((filter is null ? "全部日记已保存：" : "筛选日记已保存：") + dialog.FileName);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or System.Security.SecurityException)
        {
            _settingsWindow.SetJournalExportStatus("日记导出失败：" + error.Message);
            _logger.Error("Journal export failed", error);
        }
    }

    private void OpenDataDirectory() => OpenDirectory(SettingsService.DefaultDirectory, "数据目录");
    private void OpenLogsDirectory() => OpenDirectory(Path.Combine(SettingsService.DefaultDirectory, "logs"), "日志目录");

    private void OpenDirectory(string directory, string description)
    {
        if (_settingsWindow is null) return;
        try
        {
            Directory.CreateDirectory(directory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(directory)
                { UseShellExecute = true });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
            System.ComponentModel.Win32Exception or System.Security.SecurityException)
        {
            _settingsWindow.SetDataStatus(description + "打开失败：" + error.Message);
            _logger.Error(description + " open failed", error);
        }
    }

    private void UpdateCompanion()
    {
        if (_disposed || _controller is null) return;
        var companion = _controller.Companion;
        var remaining = companion.Focus.Remaining(DateTimeOffset.UtcNow);
        _settingsWindow?.SetCompanionStatus(companion.Profile, companion.Focus.Phase, companion.Focus.IsPaused, remaining);
        _settingsWindow?.SetFocusDetails(companion.Focus, DateTimeOffset.UtcNow);
        _settingsWindow?.SetReminderStatus(companion.HealthSnoozedUntil, companion.IsQuietFocusActive);
        _tray?.UpdateFocus(companion.Focus.Phase, companion.Focus.IsPaused, remaining);
    }

    private void UpdatePlayStatus()
    {
        if (_disposed || _controller is null) return;
        _settingsWindow?.SetPlayStatus(_controller.PlayStatus, _controller.IsPlaying);
        UpdateTray();
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
        Cleanup(() => _hotkeys?.Dispose());
        Cleanup(() => _instanceSignal?.Dispose());
        Cleanup(() => _desktop?.Dispose());
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
