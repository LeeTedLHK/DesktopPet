using System.Windows;
using System.Windows.Threading;
using DesktopPet.Services;

namespace DesktopPet;

public partial class App : Application
{
    private Mutex? _instanceMutex;
    private bool _ownsMutex;
    private AppLogger? _logger;
    private ApplicationService? _applicationService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _logger = new AppLogger(SettingsService.DefaultDirectory);
        DispatcherUnhandledException += OnDispatcherException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainException;

        try
        {
            _instanceMutex = new Mutex(true, @"Local\DesktopPet.SingleInstance", out _ownsMutex);
            if (!_ownsMutex)
            {
                Shutdown();
                return;
            }

            _logger.Info("Application started");
            _applicationService = new ApplicationService(this, _logger);
            _applicationService.Start();
        }
        catch (Exception exception)
        {
            _logger.Error("Unexpected exception during startup", exception);
            MessageBox.Show("DesktopPet 启动失败。请查看本地日志：\n" +
                Path.Combine(SettingsService.DefaultDirectory, "logs"), "DesktopPet",
                MessageBoxButton.OK, MessageBoxImage.Error);
            if (_applicationService is not null) _applicationService.Exit(1);
            else Shutdown(1);
        }
    }

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.Error("Unexpected exception on UI thread", e.Exception);
        e.Handled = true;
        MessageBox.Show("DesktopPet 遇到异常，将保存设置并退出。详细信息已记录到日志。",
            "DesktopPet", MessageBoxButton.OK, MessageBoxImage.Error);
        if (_applicationService is not null) _applicationService.Exit(1);
        else Shutdown(1);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger?.Error("Unexpected exception in background task", e.Exception);
        e.SetObserved();
    }

    private void OnDomainException(object sender, UnhandledExceptionEventArgs e) =>
        _logger?.Error("Unexpected exception terminating application", e.ExceptionObject as Exception);

    protected override void OnExit(ExitEventArgs e)
    {
        _applicationService?.Dispose();
        DispatcherUnhandledException -= OnDispatcherException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException -= OnDomainException;
        if (_ownsMutex) _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
