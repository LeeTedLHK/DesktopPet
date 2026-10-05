using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopPet.Models;
using DesktopPet.Views;

public static class P8WindowsChecks
{
    public static void Run(Action<bool, string> check, Action<int> pump,
        Action<FrameworkElement, string> render, string evidence)
    {
        var failures = new List<string>();
        void Probe(bool condition, string name)
        {
            if (condition) check(true, name);
            else { failures.Add(name); Console.WriteLine("FAIL: " + name); }
        }

        // An unrelated edit must never quantize a saved, valid animation speed.
        var settings = new SettingsWindow(new AppSettings { AnimationSpeedMultiplier = .63 });
        try
        {
            var animationSpeed = (ComboBox)settings.FindName("AnimationSpeedCombo");
            var topmost = (CheckBox)settings.FindName("TopmostCheck");
            AppSettings? edited = null;
            var editCount = 0;
            settings.SettingsEdited += value => { edited = value; editCount++; };
            foreach (var speed in new[] { .25, .63, 1.125, 2.75, 3d })
            {
                var input = new AppSettings { AnimationSpeedMultiplier = speed, Topmost = true };
                settings.LoadSettings(input);
                edited = null;
                topmost.IsChecked = false;
                Probe(edited?.AnimationSpeedMultiplier == speed,
                    $"P8 unrelated settings edit preserves exact {speed:G}x speed (actual {edited?.AnimationSpeedMultiplier:G})");
                Probe(input.AnimationSpeedMultiplier == speed && input.Topmost,
                    "P8 settings UI does not mutate its input snapshot");
            }
            Probe(animationSpeed.Items.OfType<double>().Contains(.25) &&
                animationSpeed.Items.OfType<double>().Contains(1) &&
                animationSpeed.Items.OfType<double>().Contains(3),
                "P8 animation choices cover slow, normal and maximum supported speed");
            animationSpeed.SelectedItem = 2.5d;
            Probe(edited?.AnimationSpeedMultiplier == 2.5,
                "P8 choosing a numeric animation speed publishes that speed immediately");

            settings.LoadSettings(new AppSettings { ToysEnabled = true, HomeEnabled = true });
            var toys = (CheckBox)settings.FindName("ToysCheck");
            var challenge = (Button)settings.FindName("ChallengeButton");
            toys.IsChecked = false;
            Probe(!challenge.IsEnabled && !((Button)settings.FindName("BallButton")).IsEnabled &&
                !((Button)settings.FindName("FrisbeeButton")).IsEnabled,
                "P8 disabling toys immediately disables ball, frisbee and challenge");
            toys.IsChecked = true;
            Probe(challenge.IsEnabled && ((Button)settings.FindName("BallButton")).IsEnabled &&
                ((Button)settings.FindName("FrisbeeButton")).IsEnabled,
                "P8 enabling toys immediately restores all three actions");
            ((CheckBox)settings.FindName("HomeCheck")).IsChecked = false;
            Probe(!((Button)settings.FindName("HomeButton")).IsEnabled &&
                !((ComboBox)settings.FindName("HomeThemeCombo")).IsEnabled,
                "P8 disabling home immediately disables its action and theme");

            foreach (var (eventName, buttonName) in new[]
                {
                    ("BackupRequested", "BackupButton"),
                    ("OpenDataRequested", "OpenDataButton"),
                    ("SkinExportRequested", "SkinExportButton"),
                })
            {
                var action = typeof(SettingsWindow).GetEvent(eventName);
                var button = settings.FindName(buttonName) as Button;
                if (action?.EventHandlerType != typeof(Action) || button is null)
                {
                    Probe(false, $"P8 {buttonName} exposes its {eventName} action");
                    continue;
                }
                var requested = 0;
                action.AddEventHandler(settings, (Action)(() => requested++));
                var beforeEdits = editCount;
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Probe(requested == 1 && editCount == beforeEdits,
                    $"P8 {buttonName} requests its action once without editing settings");
            }

            var setDataStatus = typeof(SettingsWindow).GetMethod("SetDataStatus", [typeof(string)]);
            var dataStatus = settings.FindName("DataStatus") as TextBlock;
            if (setDataStatus is null || dataStatus is null)
                Probe(false, "P8 data operations have a visible application status destination");
            else
            {
                setDataStatus.Invoke(settings, ["备份已保存到所选位置，设置、养成与完整日记已包含。"]);
                Probe(dataStatus.Text.Contains("完整日记"), "P8 application data status reaches the settings page");
            }

            var version = typeof(SettingsWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion.Split('+')[0] ??
                typeof(SettingsWindow).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ??
                typeof(SettingsWindow).Assembly.GetName().Version?.ToString();
            Probe(version is not null && (settings.FindName("VersionBadge") as TextBlock)?.Text == version &&
                (settings.FindName("VersionReadout") as TextBlock)?.Text.Contains(version) == true,
                "P8 settings header and help display the running assembly version");

            var tabs = (TabControl)settings.FindName("SettingsTabs");
            var help = tabs.Items.OfType<TabItem>().FirstOrDefault(tab => tab.Header?.ToString() == "数据与帮助");
            var scroll = settings.FindName("DataHelpScroll") as ScrollViewer;
            if (help is null || scroll is null)
                Probe(false, "P8 settings include an accessible data and help page");
            else
            {
                // Scale only our owned settings content; never capture desktop pixels or open external UI.
                ((FrameworkElement)settings.Content).LayoutTransform = new ScaleTransform(1.5, 1.5);
                tabs.SelectedItem = help;
                settings.ShowActivated = false;
                settings.Show();
                pump(100);
                Probe(scroll.ViewportWidth > 0 && scroll.ScrollableHeight > 0,
                    "P8 help stays accessible through vertical scrolling at 150 percent scale");
                Probe(scroll.ExtentWidth <= scroll.ViewportWidth + 1,
                    "P8 help wraps to the available width at 150 percent scale");
                render(settings, Path.Combine(evidence, "p8-help-top-150pct.png"));
                scroll.ScrollToBottom();
                pump(100);
                Probe(scroll.VerticalOffset > 0, "P8 the lower help instructions remain reachable at 150 percent scale");
                render(settings, Path.Combine(evidence, "p8-help-bottom-150pct.png"));
            }
        }
        finally { settings.Close(); }
        check(failures.Count == 0, "P8 native settings checks: " + string.Join("; ", failures));
    }
}
