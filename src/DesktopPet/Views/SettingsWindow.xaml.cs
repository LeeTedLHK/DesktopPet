using System.Windows;
using System.Windows.Controls;
using DesktopPet.Models;

namespace DesktopPet.Views;

public partial class SettingsWindow : Window
{
    private bool _loading = true;
    private AppSettings _settings;
    public event Action<AppSettings>? SettingsEdited;

    public SettingsWindow(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        MaxHeight = Math.Max(300, SystemParameters.WorkArea.Height - 24);
        LoadSettings(settings);
    }

    public void LoadSettings(AppSettings settings)
    {
        _loading = true;
        _settings = settings;
        TopmostCheck.IsChecked = settings.Topmost;
        AutoBehaviorCheck.IsChecked = settings.AutoBehaviorEnabled;
        ShowOnStartupCheck.IsChecked = settings.ShowPetOnStartup;
        EnableAnimationsCheck.IsChecked = settings.EnableAnimations;
        AnimationSpeedCombo.SelectedIndex = settings.AnimationSpeedMultiplier switch
        {
            <= 0.875 => 0,
            >= 1.25 => 2,
            _ => 1,
        };
        ScaleSlider.Value = settings.PetScale * 100;
        ScaleLabel.Text = $"{settings.PetScale * 100:0}%";
        SpeedCombo.SelectedIndex = (int)settings.MoveSpeed;
        FrequencyCombo.SelectedIndex = (int)settings.BehaviorFrequency;
        _loading = false;
    }

    public void ShowSaveResult(bool saved) => SaveStatus.Text = saved
        ? "已自动保存" : "保存失败，请检查本地日志。";

    private void OnControlChanged(object sender, RoutedEventArgs e) => ApplyChanges();
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyChanges();
    private void OnScaleChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ScaleLabel is not null) ScaleLabel.Text = $"{e.NewValue:0}%";
        ApplyChanges();
    }

    private void ApplyChanges()
    {
        if (_loading) return;
        SaveStatus.Text = "设置已生效，正在保存…";
        _settings = _settings with
        {
            Topmost = TopmostCheck.IsChecked == true,
            AutoBehaviorEnabled = AutoBehaviorCheck.IsChecked == true,
            ShowPetOnStartup = ShowOnStartupCheck.IsChecked == true,
            EnableAnimations = EnableAnimationsCheck.IsChecked == true,
            AnimationSpeedMultiplier = AnimationSpeedCombo.SelectedIndex switch
            {
                0 => 0.75,
                2 => 1.5,
                _ => 1d,
            },
            PetScale = ScaleSlider.Value / 100,
            MoveSpeed = (MovementSpeed)SpeedCombo.SelectedIndex,
            BehaviorFrequency = (BehaviorFrequency)FrequencyCombo.SelectedIndex
        };
        SettingsEdited?.Invoke(_settings);
    }

    private void OnDone(object sender, RoutedEventArgs e) => Close();
}
