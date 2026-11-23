using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OpenClaw.Shared.VoiceMode;
using OpenClawTray.Presentation;

namespace OpenClawTray.Pages;

public sealed partial class VoiceModeSettingsPage : Page
{
    private static App CurrentApp => (App)Application.Current!;
    private readonly IVoiceModeDivisionService? _voiceService;
    private VoiceModePreferences _prefs = new();

    public VoiceModeSettingsPage()
    {
        InitializeComponent();
        _voiceService = CurrentApp.Services?.GetService(typeof(IVoiceModeDivisionService)) as IVoiceModeDivisionService;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            _prefs = await VoiceModePreferences.LoadAsync();
            PopulateUi(_prefs);
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to load preferences: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void PopulateUi(VoiceModePreferences p)
    {
        // 1. Voice Interaction
        VoiceModeToggle.IsOn = p.VoiceModeEnabled;
        ContinuousToggle.IsOn = p.ContinuousConversation;
        PushToTalkToggle.IsOn = p.PushToTalk;
        AutoListeningToggle.IsOn = p.AutoListening;
        WakeWordToggle.IsOn = p.WakeWordEnabled;
        SelectItemByTag(AutoSleepCombo, p.AutoSleepMinutes.ToString());
        BargeInToggle.IsOn = p.BargeInSupport;
        VadToggle.IsOn = p.VoiceActivityDetection;
        EchoToggle.IsOn = p.EchoCancellation;
        NoiseToggle.IsOn = p.NoiseSuppression;
        SensitivitySlider.Value = p.InputSensitivity;
        SensitivityLabel.Text = $"{(int)(p.InputSensitivity * 100)}%";

        // 2. Language Settings
        SelectItemByTag(PrimaryLangCombo, p.PrimaryLanguage);
        SelectItemByTag(SecondaryLangCombo, p.SecondaryLanguage);
        AutoLangDetectToggle.IsOn = p.AutoLanguageDetection;
        MalayalamToggle.IsOn = p.MalayalamLanguageSupport;
        MultilingualToggle.IsOn = p.MultilingualConversation;
        AutoSwitchToggle.IsOn = p.AutoLanguageSwitching;

        // 3. Speaking Settings
        SelectItemByTag(VoicePersonaCombo, p.SelectedVoice);
        SpeedSlider.Value = p.SpeakingSpeed;
        SpeedLabel.Text = $"{p.SpeakingSpeed:F2}x";
        VolumeSlider.Value = p.SpeakingVolume;
        VolumeLabel.Text = $"{(int)(p.SpeakingVolume * 100)}%";
        StreamingSpeechToggle.IsOn = p.StreamingSpeechOutput;
        AutoInterruptToggle.IsOn = p.AutoSpeakingInterruption;

        // 4. Face Mode and Non-Face Mode
        SelectItemByTag(DefaultVisualModeCombo, p.DefaultVisualMode);
        AvatarAnimToggle.IsOn = p.AvatarAnimationsEnabled;
        LipSyncToggle.IsOn = p.LipSyncEnabled;
        ExpressionsToggle.IsOn = p.FacialExpressionsEnabled;
        BlinkingToggle.IsOn = p.BlinkingEnabled;
        GazeToggle.IsOn = p.EyeMovementEnabled;
        IdleAnimToggle.IsOn = p.IdleAnimationsEnabled;
        HologramToggle.IsOn = p.HolographicVisualizerEnabled;
        ParticlesToggle.IsOn = p.ParticleEffectsEnabled;
        SelectItemByTag(WaveformIntensityCombo, p.WaveformIntensity);

        // 5. Reasoning and Task Display
        TaskProgressToggle.IsOn = p.ShowTaskProgress;
        TaskStagesToggle.IsOn = p.ShowTaskStages;
        ExecutionActivityToggle.IsOn = p.ShowExecutionActivity;
        ToolActivityToggle.IsOn = p.ShowToolActivity;
        ReasoningWorkspaceToggle.IsOn = p.EnableReasoningWorkspace;
        SelectItemByTag(DisplayStyleCombo, p.ActivityDisplayStyle);

        // 6. Computer & Screen Permissions
        ScreenPermToggle.IsOn = p.PermissionScreenUnderstanding;
        DesktopPermToggle.IsOn = p.PermissionMouseKeyboardControl;
        AppLaunchPermToggle.IsOn = p.PermissionAppLaunching;
        BrowserPermToggle.IsOn = p.PermissionBrowserAutomation;
        FilePermToggle.IsOn = p.PermissionFileAccess;
        WebcamPermToggle.IsOn = p.PermissionWebcamAccess;

        // 7. Memory Settings
        MemoryToggle.IsOn = p.EnableVoiceModeMemory;
        ContinuityToggle.IsOn = p.SessionContinuity;
        SharedMemoryToggle.IsOn = p.SharedMemoryIntegration;

        // 8. Background Tasks
        BackgroundTasksToggle.IsOn = p.EnableBackgroundTasks;
        SelectItemByTag(MaxTasksCombo, p.MaxConcurrentTasks.ToString());
        NotifyCompleteToggle.IsOn = p.NotifyTaskCompletion;
        NotifyFailureToggle.IsOn = p.NotifyTaskFailure;

        // 9. File Intelligence
        FileUnderstandingToggle.IsOn = p.EnableFileUnderstanding;
        DragDropToggle.IsOn = p.EnableDragAndDropUploads;
        PdfToggle.IsOn = p.SupportPdfProcessing;
        CodeAnalysisToggle.IsOn = p.SupportSourceCodeAnalysis;

        // 10. Privacy & Security
        SelectItemByTag(RetentionPolicyCombo, p.DataRetentionPolicy);

        // 11. General & Shortcuts
        SelectItemByTag(StartupBehaviorCombo, p.StartupBehavior);
        SelectItemByTag(ShortcutCombo, p.PushToTalkShortcut);
        SfxToggle.IsOn = p.SoundEffectsEnabled;
    }

    private void CollectUiIntoPreferences(VoiceModePreferences p)
    {
        // 1. Voice Interaction
        p.VoiceModeEnabled = VoiceModeToggle.IsOn;
        p.ContinuousConversation = ContinuousToggle.IsOn;
        p.PushToTalk = PushToTalkToggle.IsOn;
        p.AutoListening = AutoListeningToggle.IsOn;
        p.WakeWordEnabled = WakeWordToggle.IsOn;
        p.AutoSleepMinutes = int.TryParse(GetSelectedTag(AutoSleepCombo), out var sleep) ? sleep : 2;
        p.BargeInSupport = BargeInToggle.IsOn;
        p.VoiceActivityDetection = VadToggle.IsOn;
        p.EchoCancellation = EchoToggle.IsOn;
        p.NoiseSuppression = NoiseToggle.IsOn;
        p.InputSensitivity = SensitivitySlider.Value;

        // 2. Language Settings
        p.PrimaryLanguage = GetSelectedTag(PrimaryLangCombo) ?? "en";
        p.SecondaryLanguage = GetSelectedTag(SecondaryLangCombo) ?? "ml";
        p.AutoLanguageDetection = AutoLangDetectToggle.IsOn;
        p.MalayalamLanguageSupport = MalayalamToggle.IsOn;
        p.MultilingualConversation = MultilingualToggle.IsOn;
        p.AutoLanguageSwitching = AutoSwitchToggle.IsOn;

        // 3. Speaking Settings
        p.SelectedVoice = GetSelectedTag(VoicePersonaCombo) ?? "SPARK Nova Neural";
        p.SpeakingSpeed = SpeedSlider.Value;
        p.SpeakingVolume = VolumeSlider.Value;
        p.StreamingSpeechOutput = StreamingSpeechToggle.IsOn;
        p.AutoSpeakingInterruption = AutoInterruptToggle.IsOn;

        // 4. Face Mode and Non-Face Mode
        p.DefaultVisualMode = GetSelectedTag(DefaultVisualModeCombo) ?? "non-face";
        p.AvatarAnimationsEnabled = AvatarAnimToggle.IsOn;
        p.LipSyncEnabled = LipSyncToggle.IsOn;
        p.FacialExpressionsEnabled = ExpressionsToggle.IsOn;
        p.BlinkingEnabled = BlinkingToggle.IsOn;
        p.EyeMovementEnabled = GazeToggle.IsOn;
        p.IdleAnimationsEnabled = IdleAnimToggle.IsOn;
        p.HolographicVisualizerEnabled = HologramToggle.IsOn;
        p.ParticleEffectsEnabled = ParticlesToggle.IsOn;
        p.WaveformIntensity = GetSelectedTag(WaveformIntensityCombo) ?? "High";

        // 5. Reasoning and Task Display
        p.ShowTaskProgress = TaskProgressToggle.IsOn;
        p.ShowTaskStages = TaskStagesToggle.IsOn;
        p.ShowExecutionActivity = ExecutionActivityToggle.IsOn;
        p.ShowToolActivity = ToolActivityToggle.IsOn;
        p.EnableReasoningWorkspace = ReasoningWorkspaceToggle.IsOn;
        p.ActivityDisplayStyle = GetSelectedTag(DisplayStyleCombo) ?? "Detailed";

        // 6. Computer & Screen Permissions
        p.PermissionScreenUnderstanding = ScreenPermToggle.IsOn;
        p.PermissionMouseKeyboardControl = DesktopPermToggle.IsOn;
        p.PermissionAppLaunching = AppLaunchPermToggle.IsOn;
        p.PermissionBrowserAutomation = BrowserPermToggle.IsOn;
        p.PermissionFileAccess = FilePermToggle.IsOn;
        p.PermissionWebcamAccess = WebcamPermToggle.IsOn;

        // 7. Memory Settings
        p.EnableVoiceModeMemory = MemoryToggle.IsOn;
        p.SessionContinuity = ContinuityToggle.IsOn;
        p.SharedMemoryIntegration = SharedMemoryToggle.IsOn;

        // 8. Background Tasks
        p.EnableBackgroundTasks = BackgroundTasksToggle.IsOn;
        p.MaxConcurrentTasks = int.TryParse(GetSelectedTag(MaxTasksCombo), out var maxT) ? maxT : 4;
        p.NotifyTaskCompletion = NotifyCompleteToggle.IsOn;
        p.NotifyTaskFailure = NotifyFailureToggle.IsOn;

        // 9. File Intelligence
        p.EnableFileUnderstanding = FileUnderstandingToggle.IsOn;
        p.EnableDragAndDropUploads = DragDropToggle.IsOn;
        p.SupportPdfProcessing = PdfToggle.IsOn;
        p.SupportSourceCodeAnalysis = CodeAnalysisToggle.IsOn;

        // 10. Privacy & Security
        p.DataRetentionPolicy = GetSelectedTag(RetentionPolicyCombo) ?? "SessionOnly";

        // 11. General & Shortcuts
        p.StartupBehavior = GetSelectedTag(StartupBehaviorCombo) ?? "Manual";
        p.PushToTalkShortcut = GetSelectedTag(ShortcutCombo) ?? "Ctrl+Space";
        p.SoundEffectsEnabled = SfxToggle.IsOn;
    }

    private async void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        SaveButton.IsEnabled = false;
        try
        {
            CollectUiIntoPreferences(_prefs);
            await _prefs.SaveAsync();
            ShowStatus("Voice Mode settings saved and persisted successfully.", InfoBarSeverity.Success);

            // Sync visual mode to runtime if active
            if (_voiceService != null && _voiceService.IsRuntimeRunning)
            {
                await _voiceService.SetVisualModeAsync(_prefs.DefaultVisualMode);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to save settings: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private void OnBackClicked(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
        else
        {
            Frame.Navigate(typeof(VoiceModePage));
        }
    }

    private void OnOpenModelsClicked(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(DefaultModelPage));
    }

    private async void OnRestoreDefaultsClicked(object sender, RoutedEventArgs e)
    {
        _prefs = new VoiceModePreferences();
        PopulateUi(_prefs);
        await _prefs.SaveAsync();
        ShowStatus("Restored Voice Mode settings to default values.", InfoBarSeverity.Informational);
    }

    private void OnPreviewVoiceClicked(object sender, RoutedEventArgs e)
    {
        ShowStatus($"Playing sample audio for voice '{GetSelectedTag(VoicePersonaCombo)}' at {SpeedSlider.Value:F1}x speed...", InfoBarSeverity.Informational);
    }

    private void OnClearHistoryClicked(object sender, RoutedEventArgs e)
    {
        ShowStatus("Voice Mode local conversation transcript and buffer cleared.", InfoBarSeverity.Success);
    }

    private void OnRevokePermissionsClicked(object sender, RoutedEventArgs e)
    {
        ScreenPermToggle.IsOn = false;
        DesktopPermToggle.IsOn = false;
        AppLaunchPermToggle.IsOn = false;
        BrowserPermToggle.IsOn = false;
        FilePermToggle.IsOn = false;
        WebcamPermToggle.IsOn = false;
        ShowStatus("All elevated computer, screen, and automation permissions revoked.", InfoBarSeverity.Warning);
    }

    private void OnSensitivityChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (SensitivityLabel != null)
        {
            SensitivityLabel.Text = $"{(int)(e.NewValue * 100)}%";
        }
    }

    private void OnSpeedChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (SpeedLabel != null)
        {
            SpeedLabel.Text = $"{e.NewValue:F2}x";
        }
    }

    private void OnVolumeChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (VolumeLabel != null)
        {
            VolumeLabel.Text = $"{(int)(e.NewValue * 100)}%";
        }
    }

    private void OnVisualModeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Visual mode preview or state update
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }

    private static string? GetSelectedTag(ComboBox combo)
    {
        if (combo.SelectedItem is ComboBoxItem item)
            return item.Tag as string ?? item.Content as string;
        return combo.SelectedItem as string;
    }

    private static void SelectItemByTag(ComboBox combo, string? tagValue)
    {
        if (string.IsNullOrEmpty(tagValue)) return;
        foreach (var obj in combo.Items)
        {
            if (obj is ComboBoxItem item)
            {
                var tag = item.Tag as string;
                var content = item.Content as string;
                if (string.Equals(tag, tagValue, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(content, tagValue, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
        }
    }
}
