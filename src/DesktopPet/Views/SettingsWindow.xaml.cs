using System.Windows;
using System.Windows.Controls;
using DesktopPet.Models;
using DesktopPet.Companions;
using DesktopPet.Services;
using DesktopPet.Skins;
using System.Windows.Media.Imaging;
using System.Reflection;
using DesktopPet.Journal;

namespace DesktopPet.Views;

public partial class SettingsWindow : Window
{
    private bool _loading = true;
    private AppSettings _settings;
    private DesktopPet.Journal.JournalState? _journalSnapshot;
    private DateOnly _journalFilterDate = DateOnly.FromDateTime(DateTime.Now);
    private bool _changingJournalFilter;
    private IReadOnlyList<JournalEntry> _visibleJournal = [];
    private sealed record JournalChoice(string Label, object? Value);
    private bool _petAvailable = true;
    private bool _fullscreenSuppressed;
    public event Action<AppSettings>? SettingsEdited;
    public event Action? FocusStartRequested;
    public event Action? FocusPauseRequested;
    public event Action? FocusStopRequested;
    public event Action<int>? SnoozeRemindersRequested;
    public event Action? ResumeRemindersRequested;
    public event Action? ExportJournalRequested;
    public event Action<JournalFilter>? ExportFilteredJournalRequested;
    public event Action? ResetPositionRequested;
    public event Action<string>? SkinSelected;
    public event Action<bool>? SkinImportRequested;
    public event Action? BallRequested;
    public event Action? FrisbeeRequested;
    public event Action? HomeRequested;
    public event Action? FollowRequested;
    public event Action? ChallengeRequested;
    public event Action? StopPlayRequested;
    public event Action? PhotoRequested;
    public event Action? OpenPhotosRequested;
    public event Action<string>? RewardRequested;
    public event Action? BackupRequested;
    public event Action? OpenDataRequested;
    public event Action? OpenLogsRequested;
    public event Action? ShowPetRequested;
    public event Action? SkinExportRequested;

    public SettingsWindow(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        JournalKindCombo.ItemsSource = new[] { new JournalChoice("全部类型", null) }.Concat(
            Enum.GetValues<JournalEntryKind>().Select(kind => new JournalChoice(JournalQuery.KindLabel(kind), kind))).ToArray();
        JournalKindCombo.SelectedIndex = 0;
        JournalRangeCombo.ItemsSource = new[] {
            new JournalChoice("全部日期", JournalDateRange.All), new JournalChoice("今天", JournalDateRange.Today),
            new JournalChoice("近7天", JournalDateRange.Last7Days), new JournalChoice("近30天", JournalDateRange.Last30Days) };
        JournalRangeCombo.SelectedIndex = 0;
        var assembly = typeof(SettingsWindow).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
            ?? assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
            ?? assembly.GetName().Version?.ToString() ?? "未知版本";
        VersionBadge.Text = version;
        VersionReadout.Text = $"DesktopPet {version} · Windows 桌面宠物";
        SleepStartCombo.ItemsSource = Enumerable.Range(0, 24).Select(hour => $"{hour:00}:00").ToArray();
        SleepEndCombo.ItemsSource = Enumerable.Range(0, 24).Select(hour => $"{hour:00}:00").ToArray();
        MaxHeight = Math.Max(300, SystemParameters.WorkArea.Height - 24);
        LoadSettings(settings);
        SetCompanionStatus(new PetProfile(), FocusPhase.Stopped, false, TimeSpan.Zero);
    }

    public void LoadSettings(AppSettings settings)
    {
        _loading = true;
        _settings = settings;
        MonitorCombo.SelectedValue = settings.SelectedMonitorId;
        SkinCombo.SelectedValue = settings.SelectedSkinId;
        CrossMonitorCheck.IsChecked = settings.AllowCrossMonitorDrag;
        PreciseHitCheck.IsChecked = settings.PreciseHitTestEnabled;
        StartWindowsCheck.IsChecked = settings.StartWithWindows;
        ToysCheck.IsChecked = settings.ToysEnabled;
        BallScaleSlider.Value = settings.BallScale * 100;
        BallScaleLabel.Text = $"{settings.BallScale * 100:0.##}%";
        FrisbeeScaleSlider.Value = settings.FrisbeeScale * 100;
        FrisbeeScaleLabel.Text = $"{settings.FrisbeeScale * 100:0.##}%";
        FeedingBowlScaleSlider.Value = settings.FeedingBowlScale * 100;
        FeedingBowlScaleLabel.Text = $"{settings.FeedingBowlScale * 100:0.##}%";
        HomeCheck.IsChecked = settings.HomeEnabled;
        HomeThemeCombo.SelectedIndex = (int)settings.HomeTheme;
        HomeThemeCombo.IsEnabled = settings.HomeEnabled;
        PercentOptions(HomeXCombo, 0, 100, settings.HomeXPercent);
        PercentOptions(HomeYCombo, 0, 100, settings.HomeYPercent);
        JournalCheck.IsChecked = settings.JournalEnabled;
        GrowthCheck.IsChecked = settings.GrowthEnabled;
        if (!PetNameText.IsKeyboardFocusWithin && PetNameText.Text != settings.PetName) PetNameText.Text = settings.PetName;
        PersonalityCombo.SelectedIndex = (int)settings.PetPersonality;
        ActivityCombo.SelectedIndex = (int)settings.ActivityRange;
        PercentOptions(AreaLeftCombo, 0, 90, settings.ActivityLeftPercent);
        PercentOptions(AreaTopCombo, 0, 90, settings.ActivityTopPercent);
        PercentOptions(AreaWidthCombo, 10, 100, settings.ActivityWidthPercent);
        PercentOptions(AreaHeightCombo, 10, 100, settings.ActivityHeightPercent);
        WaterReminderCheck.IsChecked = settings.WaterReminderEnabled;
        MoveReminderCheck.IsChecked = settings.MoveReminderEnabled;
        RestReminderCheck.IsChecked = settings.RestReminderEnabled;
        MinutesOptions(WaterIntervalCombo, [15, 30, 45, 60, 90, 120], settings.WaterReminderMinutes);
        MinutesOptions(MoveIntervalCombo, [15, 30, 45, 60, 90, 120], settings.MoveReminderMinutes);
        MinutesOptions(RestIntervalCombo, [15, 30, 45, 60, 90, 120], settings.RestReminderMinutes);
        MinutesOptions(FocusMinutesCombo, [1, 15, 25, 30, 45, 60, 90, 120], settings.FocusMinutes);
        MinutesOptions(BreakMinutesCombo, [1, 5, 10, 15, 20, 30, 60], settings.BreakMinutes);
        FocusQuietCheck.IsChecked = settings.FocusQuietEnabled;
        UpdateCompanionControls();
        TopmostCheck.IsChecked = settings.Topmost;
        AutoBehaviorCheck.IsChecked = settings.AutoBehaviorEnabled;
        ShowOnStartupCheck.IsChecked = settings.ShowPetOnStartup;
        EnableAnimationsCheck.IsChecked = settings.EnableAnimations;
        PowerSavingCheck.IsChecked = settings.PowerSavingEnabled;
        FullscreenAutoHideCheck.IsChecked = settings.FullscreenAutoHide;
        ClickThroughCheck.IsChecked = settings.ClickThrough;
        GlobalHotkeysCheck.IsChecked = settings.GlobalHotkeysEnabled;
        SpeechBubblesCheck.IsChecked = settings.SpeechBubblesEnabled;
        NaturalBehaviorsCheck.IsChecked = settings.NaturalBehaviorsEnabled;
        CursorTrackingCheck.IsChecked = settings.CursorTrackingEnabled;
        NightSleepCheck.IsChecked = settings.NightSleepEnabled;
        SleepStartCombo.SelectedIndex = settings.NightSleepStartHour;
        SleepEndCombo.SelectedIndex = settings.NightSleepEndHour;
        SleepStartCombo.IsEnabled = settings.NightSleepEnabled;
        SleepEndCombo.IsEnabled = settings.NightSleepEnabled;
        var animationSpeed = settings.Normalize().AnimationSpeedMultiplier;
        AnimationSpeedCombo.ItemsSource = Enumerable.Range(1, 12).Select(step => step / 4d)
            .Append(animationSpeed).Distinct().Order().ToArray();
        AnimationSpeedCombo.SelectedItem = animationSpeed;
        ScaleSlider.Value = settings.PetScale * 100;
        ScaleLabel.Text = $"{settings.PetScale * 100:0}%";
        SpeedCombo.SelectedIndex = (int)settings.MoveSpeed;
        FrequencyCombo.SelectedIndex = (int)settings.BehaviorFrequency;
        UpdatePlayAvailability();
        _loading = false;
        UpdateSkinPreview();
    }

    public void SetMonitorChoices(IReadOnlyList<MonitorInfo> monitors)
    { _loading = true; MonitorCombo.ItemsSource = monitors; MonitorCombo.SelectedValue = _settings.SelectedMonitorId; _loading = false; }
    public void SetSkinChoices(IReadOnlyList<SkinInfo> skins)
    { _loading = true; SkinCombo.ItemsSource = skins; SkinCombo.SelectedValue = _settings.SelectedSkinId; _loading = false; UpdateSkinPreview(); }
    public void SetStartupStatus(string status) => StartupStatus.Text = status;
    public void SetSkinStatus(string status) => SkinStatus.Text = status;
    public void SetDataStatus(string status) => DataStatus.Text = status;
    public void SetJournalExportStatus(string status) => JournalExportStatus.Text = status;
    public void SetReminderStatus(DateTimeOffset? until, bool quiet)
    {
        var snoozed = until is { } deadline && deadline > DateTimeOffset.Now;
        ResumeRemindersButton.IsEnabled = snoozed;
        SnoozeStatus.Text = snoozed ? $"健康提醒延后至 {until!.Value.ToLocalTime():HH:mm}；可提前恢复。" :
            quiet ? "正在安静专注，健康提醒暂缓；专注结束仍会提示。" : "延后只影响健康提醒，专注结束仍会提示。";
    }
    private void OnResetPosition(object sender, RoutedEventArgs e) => ResetPositionRequested?.Invoke();
    private void OnImportFolder(object sender, RoutedEventArgs e) => SkinImportRequested?.Invoke(false);
    private void OnImportZip(object sender, RoutedEventArgs e) => SkinImportRequested?.Invoke(true);
    private void OnDefaultSkin(object sender, RoutedEventArgs e) => SkinSelected?.Invoke("default");
    private void OnExportSkin(object sender, RoutedEventArgs e) => SkinExportRequested?.Invoke();
    private void OnBackup(object sender, RoutedEventArgs e) => BackupRequested?.Invoke();
    private void OnOpenData(object sender, RoutedEventArgs e) => OpenDataRequested?.Invoke();
    private void OnOpenLogs(object sender, RoutedEventArgs e) => OpenLogsRequested?.Invoke();
    private void OnShowPet(object sender, RoutedEventArgs e)
    {
        if (!_petAvailable && !_fullscreenSuppressed) ShowPetRequested?.Invoke();
    }
    public void SetPetAvailability(bool visible, bool fullscreenSuppressed)
    {
        _petAvailable = visible;
        _fullscreenSuppressed = fullscreenSuppressed;
        UpdatePlayAvailability();
    }
    private void UpdatePlayAvailability()
    {
        BallButton.IsEnabled = _petAvailable && _settings.ToysEnabled;
        FrisbeeButton.IsEnabled = _petAvailable && _settings.ToysEnabled;
        ChallengeButton.IsEnabled = _petAvailable && _settings.ToysEnabled;
        HomeButton.IsEnabled = _petAvailable && _settings.HomeEnabled;
        FollowButton.IsEnabled = _petAvailable;
        PlayAvailability.Visibility = _petAvailable ? Visibility.Collapsed : Visibility.Visible;
        PlayAvailability.Text = _fullscreenSuppressed
            ? "全屏期间宠物暂时隐藏，请退出全屏后再显示或玩耍。"
            : "宠物已隐藏，显示后就可以投玩具、散步或回小窝。";
        ShowPetButton.Visibility = _petAvailable ? Visibility.Collapsed : Visibility.Visible;
        ShowPetButton.IsEnabled = !_fullscreenSuppressed;
    }
    private void OnBall(object sender, RoutedEventArgs e) => BallRequested?.Invoke();
    private void OnFrisbee(object sender, RoutedEventArgs e) => FrisbeeRequested?.Invoke();
    private void OnHome(object sender, RoutedEventArgs e) => HomeRequested?.Invoke();
    private void OnFollow(object sender, RoutedEventArgs e) => FollowRequested?.Invoke();
    private void OnChallenge(object sender, RoutedEventArgs e) => ChallengeRequested?.Invoke();
    private void OnStopPlay(object sender, RoutedEventArgs e) => StopPlayRequested?.Invoke();
    private void OnPhoto(object sender, RoutedEventArgs e) => PhotoRequested?.Invoke();
    private void OnOpenPhotos(object sender, RoutedEventArgs e) => OpenPhotosRequested?.Invoke();
    public void SetPlayStatus(string text, bool active) { PlayStatus.Text = text; StopPlayButton.IsEnabled = active; }
    private void OnReward(object sender, RoutedEventArgs e)
    {
        if (AchievementList.SelectedItem is DesktopPet.Journal.JournalAchievement achievement)
        {
            JournalStatus.Text = achievement.Unlocked ? achievement.RewardDialogue : "这个成就还没解锁，继续一起积累吧～";
            if (achievement.Unlocked) RewardRequested?.Invoke(achievement.Id);
        }
        else JournalStatus.Text = "先选一个成就吧～";
    }
    private void OnSkinSelection(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        UpdateSkinPreview();
        if (SkinCombo.SelectedValue is string id) SkinSelected?.Invoke(id);
    }
    private void UpdateSkinPreview()
    {
        SkinPreview.Source = null;
        if (SkinCombo.SelectedItem is not SkinInfo skin) return;
        SkinDetails.Text = skin.MissingActions.Count == 0 ? "动作齐全" : "缺少动作，将使用备用帧：" + string.Join("、", skin.MissingActions);
        if (skin.PreviewPath is null) return;
        try
        {
            var source = new BitmapImage(); source.BeginInit(); source.CacheOption = BitmapCacheOption.OnLoad;
            source.DecodePixelWidth = 256; source.UriSource = new Uri(skin.PreviewPath); source.EndInit(); source.Freeze();
            SkinPreview.Source = source;
        }
        catch (Exception ex) when (ex is IOException or System.IO.FileFormatException or NotSupportedException or ArgumentException) { SkinDetails.Text += "（预览无法读取）"; }
    }

    public void ShowSaveResult(bool saved) => SaveStatus.Text = saved
        ? "已自动保存" : "保存失败，可在“数据与帮助”页打开日志目录。";

    public void SetRuntimeStatus(string status) => HotkeyStatus.Text = status;
    public void ShowCompanionPage() => SettingsTabs.SelectedIndex = 1;

    public void SetCompanionStatus(PetProfile profile, FocusPhase phase, bool paused, TimeSpan remaining)
    {
        var journalChanged = !ReferenceEquals(_journalSnapshot, profile.Journal);
        if (journalChanged)
        {
            _journalSnapshot = profile.Journal;
            var tracker = new DesktopPet.Journal.JournalTracker(profile, false, DateTimeOffset.Now);
            var selected = (AchievementList.SelectedItem as DesktopPet.Journal.JournalAchievement)?.Id;
            AchievementList.ItemsSource = tracker.Achievements;
            AchievementList.SelectedItem = tracker.Achievements.FirstOrDefault(item => item.Id == selected);
            JournalSummary.Text = $"陪伴 {profile.Journal.VisitedDays.Count} 天 · 喂食 {profile.Journal.FeedingCount} 次 · 玩耍 {profile.Journal.PlayCount} 次 · 完成专注 {profile.Journal.FocusCompletedCount} 次 · 日记 {profile.Journal.Entries.Count} 条";
        }
        if (journalChanged || _journalFilterDate != DateOnly.FromDateTime(DateTime.Now)) RefreshJournalView();
        CareSummary.Text = $"亲密度 {profile.Affection:0} · 饱腹 {profile.Fullness:0} · 精力 {profile.Energy:0}" +
            (_settings.GrowthEnabled ? "" : "（养成已关闭）");
        AffectionBar.Value = profile.Affection;
        FullnessBar.Value = profile.Fullness;
        EnergyBar.Value = profile.Energy;
        var active = phase is FocusPhase.Focus or FocusPhase.Break;
        var seconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
        FocusStatus.Text = active ? $"{(phase == FocusPhase.Focus ? "专注" : "休息")} · {seconds / 60:00}:{seconds % 60:00}{(paused ? "（已暂停）" : "")}" :
            phase == FocusPhase.Completed ? "这一轮完成啦，准备好再开始" : "准备开始一轮专注";
        FocusStartButton.IsEnabled = !active;
        FocusPauseButton.IsEnabled = active;
        FocusPauseButton.Content = paused ? "继续" : "暂停";
        FocusStopButton.IsEnabled = active;
    }

    public JournalFilter CurrentJournalFilter => new(JournalSearchText.Text,
        JournalKindCombo.SelectedValue as JournalEntryKind?,
        JournalRangeCombo.SelectedValue is JournalDateRange range ? range : JournalDateRange.All, _journalFilterDate);

    private void RefreshJournalView()
    {
        if (_journalSnapshot is null) return;
        _journalFilterDate = DateOnly.FromDateTime(DateTime.Now);
        var selected = DiaryList.SelectedItem as JournalEntry;
        var filter = CurrentJournalFilter;
        _visibleJournal = JournalQuery.Filter(_journalSnapshot.Entries, filter);
        DiaryList.ItemsSource = _visibleJournal;
        if (selected is not null) DiaryList.SelectedItem = _visibleJournal.FirstOrDefault(entry => entry == selected);
        JournalFilterStatus.Text = $"显示 {_visibleJournal.Count}/{_journalSnapshot.Entries.Count} 条 · {JournalQuery.Description(filter)}";
        JournalEmptyHint.Visibility = _visibleJournal.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        JournalEmptyHint.Text = _journalSnapshot.Entries.Count == 0 ? "还没有日记，陪小狗互动后再来看吧。" :
            "没有符合条件的日记，试试其他关键词或清除筛选。";
        ExportFilteredJournalButton.IsEnabled = _visibleJournal.Count > 0;
    }
    private void OnJournalFilterChanged(object sender, TextChangedEventArgs e)
    { if (!_loading && !_changingJournalFilter) RefreshJournalView(); }
    private void OnJournalFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
    { if (!_loading && !_changingJournalFilter) RefreshJournalView(); }
    private void OnClearJournalFilter(object sender, RoutedEventArgs e)
    {
        _changingJournalFilter = true;
        try { JournalSearchText.Text = ""; JournalKindCombo.SelectedIndex = 0; JournalRangeCombo.SelectedIndex = 0; }
        finally { _changingJournalFilter = false; }
        RefreshJournalView();
    }
    private void OnExportFilteredJournal(object sender, RoutedEventArgs e)
    { if (_visibleJournal.Count > 0) ExportFilteredJournalRequested?.Invoke(CurrentJournalFilter); }

    public void SetFocusDetails(FocusTimer timer, DateTimeOffset now)
    {
        var percent = timer.Progress(now) * 100;
        FocusProgressBar.Value = percent;
        FocusDetailsText.Text = timer.EstimatedEndUtc is { } end ?
            $"本阶段进度 {percent:0}% · 预计 {end.ToLocalTime().ToString(end.ToLocalTime().Date == now.ToLocalTime().Date ? "HH:mm" : "MM-dd HH:mm")} 结束" :
            timer.IsPaused ? $"本阶段进度 {percent:0}% · 已暂停，继续后更新预计结束时间。" :
            timer.Phase == FocusPhase.Completed ? "本轮已完成，准备好后可以再开始。" : "开始后可查看进度和预计结束时间。";
    }

    private void OnFocusPreset(object sender, RoutedEventArgs e)
    {
        if (_loading || sender is not Button { Tag: string preset }) return;
        var parts = preset.Split(',');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var focus) || !int.TryParse(parts[1], out var rest)) return;
        _loading = true;
        try
        {
            MinutesOptions(FocusMinutesCombo, [1, 15, 25, 30, 45, 60, 90, 120], focus);
            MinutesOptions(BreakMinutesCombo, [1, 5, 10, 15, 20, 30, 60], rest);
        }
        finally { _loading = false; }
        ApplyChanges();
    }
    private static void MinutesOptions(ComboBox combo, int[] options, int selected)
    {
        combo.ItemsSource = options.Append(selected).Distinct().Order().ToArray();
        combo.ItemStringFormat = "{0} 分钟";
        combo.SelectedItem = selected;
    }
    private static void PercentOptions(ComboBox combo, int min, int max, double selected)
    {
        combo.ItemsSource = Enumerable.Range(min, max - min + 1).Select(i => (double)i).Append(selected).Distinct().Order().ToArray();
        combo.SelectedItem = selected;
    }
    private void UpdateCompanionControls()
    {
        CustomAreaPanel.Visibility = _settings.ActivityRange == ActivityRange.Custom ? Visibility.Visible : Visibility.Collapsed;
        WaterIntervalCombo.IsEnabled = _settings.WaterReminderEnabled;
        MoveIntervalCombo.IsEnabled = _settings.MoveReminderEnabled;
        RestIntervalCombo.IsEnabled = _settings.RestReminderEnabled;
    }
    private void OnNameChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loading && !string.IsNullOrWhiteSpace(PetNameText.Text)) ApplyChanges();
    }
    private void OnNameFocusLost(object sender, RoutedEventArgs e)
    {
        var wasLoading = _loading;
        _loading = true;
        try { PetNameText.Text = _settings.PetName; }
        finally { _loading = wasLoading; }
    }
    private void OnFocusStart(object sender, RoutedEventArgs e) => FocusStartRequested?.Invoke();
    private void OnFocusPause(object sender, RoutedEventArgs e) => FocusPauseRequested?.Invoke();
    private void OnFocusStop(object sender, RoutedEventArgs e) => FocusStopRequested?.Invoke();
    private void OnSnoozeReminders(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && int.TryParse(button.Tag?.ToString(), out var minutes))
            SnoozeRemindersRequested?.Invoke(minutes);
    }
    private void OnResumeReminders(object sender, RoutedEventArgs e) => ResumeRemindersRequested?.Invoke();
    private void OnExportJournal(object sender, RoutedEventArgs e) => ExportJournalRequested?.Invoke();

    private void OnControlChanged(object sender, RoutedEventArgs e) => ApplyChanges();
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyChanges();
    private void OnScaleChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ScaleLabel is not null) ScaleLabel.Text = $"{e.NewValue:0}%";
        ApplyChanges();
    }

    private void OnPropScaleChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        // Keep loaded custom values intact when a different setting changes.
        if (sender == BallScaleSlider)
        {
            BallScaleLabel.Text = $"{e.NewValue:0.##}%";
            _settings = _settings with { BallScale = e.NewValue / 100 };
        }
        else if (sender == FrisbeeScaleSlider)
        {
            FrisbeeScaleLabel.Text = $"{e.NewValue:0.##}%";
            _settings = _settings with { FrisbeeScale = e.NewValue / 100 };
        }
        else if (sender == FeedingBowlScaleSlider)
        {
            FeedingBowlScaleLabel.Text = $"{e.NewValue:0.##}%";
            _settings = _settings with { FeedingBowlScale = e.NewValue / 100 };
        }
        else return;
        ApplyChanges();
    }

    private void ApplyChanges()
    {
        if (_loading) return;
        SaveStatus.Text = "设置已生效，正在保存…";
        _settings = _settings with
        {
            SelectedMonitorId = MonitorCombo.SelectedValue as string ?? _settings.SelectedMonitorId,
            AllowCrossMonitorDrag = CrossMonitorCheck.IsChecked == true,
            PreciseHitTestEnabled = PreciseHitCheck.IsChecked == true,
            StartWithWindows = StartWindowsCheck.IsChecked == true,
            ToysEnabled = ToysCheck.IsChecked == true,
            HomeEnabled = HomeCheck.IsChecked == true,
            HomeTheme = (HomeTheme)HomeThemeCombo.SelectedIndex,
            HomeXPercent = HomeXCombo.SelectedItem is double homeX ? homeX : _settings.HomeXPercent,
            HomeYPercent = HomeYCombo.SelectedItem is double homeY ? homeY : _settings.HomeYPercent,
            JournalEnabled = JournalCheck.IsChecked == true,
            GrowthEnabled = GrowthCheck.IsChecked == true,
            PetName = string.IsNullOrWhiteSpace(PetNameText.Text) ? _settings.PetName : PetNameText.Text,
            PetPersonality = (PetPersonality)PersonalityCombo.SelectedIndex,
            ActivityRange = (ActivityRange)ActivityCombo.SelectedIndex,
            ActivityLeftPercent = AreaLeftCombo.SelectedItem is double left ? left : _settings.ActivityLeftPercent,
            ActivityTopPercent = AreaTopCombo.SelectedItem is double top ? top : _settings.ActivityTopPercent,
            ActivityWidthPercent = AreaWidthCombo.SelectedItem is double width ? width : _settings.ActivityWidthPercent,
            ActivityHeightPercent = AreaHeightCombo.SelectedItem is double height ? height : _settings.ActivityHeightPercent,
            WaterReminderEnabled = WaterReminderCheck.IsChecked == true,
            MoveReminderEnabled = MoveReminderCheck.IsChecked == true,
            RestReminderEnabled = RestReminderCheck.IsChecked == true,
            WaterReminderMinutes = WaterIntervalCombo.SelectedItem is int water ? water : _settings.WaterReminderMinutes,
            MoveReminderMinutes = MoveIntervalCombo.SelectedItem is int move ? move : _settings.MoveReminderMinutes,
            RestReminderMinutes = RestIntervalCombo.SelectedItem is int rest ? rest : _settings.RestReminderMinutes,
            FocusMinutes = FocusMinutesCombo.SelectedItem is int focus ? focus : _settings.FocusMinutes,
            BreakMinutes = BreakMinutesCombo.SelectedItem is int pause ? pause : _settings.BreakMinutes,
            FocusQuietEnabled = FocusQuietCheck.IsChecked == true,
            Topmost = TopmostCheck.IsChecked == true,
            AutoBehaviorEnabled = AutoBehaviorCheck.IsChecked == true,
            ShowPetOnStartup = ShowOnStartupCheck.IsChecked == true,
            EnableAnimations = EnableAnimationsCheck.IsChecked == true,
            PowerSavingEnabled = PowerSavingCheck.IsChecked == true,
            FullscreenAutoHide = FullscreenAutoHideCheck.IsChecked == true,
            ClickThrough = ClickThroughCheck.IsChecked == true,
            GlobalHotkeysEnabled = GlobalHotkeysCheck.IsChecked == true,
            SpeechBubblesEnabled = SpeechBubblesCheck.IsChecked == true,
            NaturalBehaviorsEnabled = NaturalBehaviorsCheck.IsChecked == true,
            CursorTrackingEnabled = CursorTrackingCheck.IsChecked == true,
            NightSleepEnabled = NightSleepCheck.IsChecked == true,
            NightSleepStartHour = SleepStartCombo.SelectedIndex,
            NightSleepEndHour = SleepEndCombo.SelectedIndex,
            AnimationSpeedMultiplier = AnimationSpeedCombo.SelectedItem is double animationSpeed
                ? animationSpeed : _settings.AnimationSpeedMultiplier,
            PetScale = ScaleSlider.Value / 100,
            MoveSpeed = (MovementSpeed)SpeedCombo.SelectedIndex,
            BehaviorFrequency = (BehaviorFrequency)FrequencyCombo.SelectedIndex
        };
        _settings = _settings.Normalize();
        UpdateCompanionControls();
        HomeThemeCombo.IsEnabled = _settings.HomeEnabled;
        UpdatePlayAvailability();
        SleepStartCombo.IsEnabled = _settings.NightSleepEnabled;
        SleepEndCombo.IsEnabled = _settings.NightSleepEnabled;
        SettingsEdited?.Invoke(_settings);
    }

    private void OnDone(object sender, RoutedEventArgs e) => Close();
}
