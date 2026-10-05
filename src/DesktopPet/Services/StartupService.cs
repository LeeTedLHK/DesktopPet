using Microsoft.Win32;

namespace DesktopPet.Services;

public sealed class StartupService(string executable, string registryPath = @"Software\Microsoft\Windows\CurrentVersion\Run")
{
    private const string ValueName = "DesktopPet";
    public string Status { get; private set; } = "开机自启已关闭";
    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(registryPath);
                return string.Equals(key?.GetValue(ValueName) as string, StartupCommand.Build(executable), StringComparison.Ordinal);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            { Status = "无法读取开机自启设置"; return false; }
        }
    }
    public bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(registryPath, true);
            if (enabled) key.SetValue(ValueName, StartupCommand.Build(executable), RegistryValueKind.String);
            else key.DeleteValue(ValueName, false);
            Status = enabled ? "已开启开机自启" : "开机自启已关闭";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { Status = "开机自启设置失败，请检查系统权限"; return false; }
    }
}
