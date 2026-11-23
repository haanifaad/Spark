using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using OpenClaw.Shared.VoiceMode;
using OpenClawTray.Helpers;

namespace OpenClawTray.Pages;

public sealed partial class VoiceModePage : Page
{
    private static App CurrentApp => (App)Microsoft.UI.Xaml.Application.Current!;
    private readonly IVoiceModeDivisionService _voiceService;
    private bool _isMicActive = true;
    private bool _isReasoningActive = false;
    public bool IsReasoningActive => _isReasoningActive;

    public VoiceModePage()
    {
        InitializeComponent();
        _voiceService = CurrentApp.Services?.GetService(typeof(IVoiceModeDivisionService)) as IVoiceModeDivisionService
            ?? new VoiceModeDivisionService(CurrentApp.DivisionManager);

        _voiceService.LogMessageAppended += (s, msg) => AppendLog(msg);
        _voiceService.OutputReceived += (s, msg) => HandleRuntimeOutput(msg);
        _voiceService.ErrorReceived += (s, msg) => AppendLog($"[STDERR] {msg}");

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        AppendLog("[SPARK Voice Mode] Initializing division environment...");
        try
        {
            await _voiceService.EnsureRuntimeStartedAsync();
            AppendLog("[SPARK Voice Mode] Runtime connected. Division capability guard active.");

            // Sync visual mode and model
            var stateRes = await _voiceService.GetStateAsync();
            if (stateRes.Success && !string.IsNullOrEmpty(stateRes.RawJson))
            {
                using var doc = JsonDocument.Parse(stateRes.RawJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("visual_mode", out var vmProp))
                {
                    var mode = vmProp.GetString();
                    SelectComboTag(VisualModeCombo, mode);
                }
                if (root.TryGetProperty("model", out var mProp))
                {
                    var mod = mProp.GetString();
                    SelectComboTag(ModelCombo, mod);
                }
            }
        }
        catch (Exception ex)
        {
            AppendLog($"[SPARK Voice Mode] Runtime warning: {ex.Message}");
        }
    }

    private void SelectComboTag(ComboBox combo, string? tag)
    {
        if (string.IsNullOrEmpty(tag)) return;
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem item && string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedIndex = i;
                break;
            }
        }
    }

    private void AppendLog(string message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            if (string.IsNullOrEmpty(LogOutputTextBox.Text))
            {
                LogOutputTextBox.Text = line;
            }
            else
            {
                LogOutputTextBox.Text += Environment.NewLine + line;
            }
            LogScrollViewer.ChangeView(null, LogScrollViewer.ScrollableHeight, null);
        });
    }

    private void HandleRuntimeOutput(string jsonLine)
    {
        AppendLog($"[STDOUT] {jsonLine}");
        try
        {
            using var doc = JsonDocument.Parse(jsonLine);
            var root = doc.RootElement;
            if (root.TryGetProperty("voice_state", out var vs))
            {
                var st = vs.GetString()?.ToUpperInvariant() ?? "LISTENING";
                DispatcherQueue.TryEnqueue(() =>
                {
                    LiveStateTitleText.Text = $"{st}...";
                    if (st == "SPEAKING")
                    {
                        LiveSubtitleText.Text = "Audio response playing...";
                        VoiceLevelBar.Value = 75;
                    }
                    else if (st == "THINKING")
                    {
                        LiveSubtitleText.Text = "Let me process that, Sir...";
                        VoiceLevelBar.Value = 25;
                    }
                    else
                    {
                        LiveSubtitleText.Text = "I'm listening, Sir...";
                        VoiceLevelBar.Value = 45;
                    }
                });
            }
        }
        catch { }
    }

    // ── View Switcher: Voice UI vs Reasoning UI ─────────────────────────────
    private void OnVoiceUiViewClicked(object sender, RoutedEventArgs e)
    {
        _isReasoningActive = false;
        VoiceUiContainer.Visibility = Visibility.Visible;
        ReasoningUiContainer.Visibility = Visibility.Collapsed;
        VoiceUiViewButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        ReasoningViewButton.Style = (Style)Application.Current.Resources["DefaultButtonStyle"];
        AppendLog("[Navigation] Switched to Voice UI workspace.");
    }

    private void OnReasoningViewClicked(object sender, RoutedEventArgs e)
    {
        _isReasoningActive = true;
        VoiceUiContainer.Visibility = Visibility.Collapsed;
        ReasoningUiContainer.Visibility = Visibility.Visible;
        VoiceUiViewButton.Style = (Style)Application.Current.Resources["DefaultButtonStyle"];
        ReasoningViewButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        AppendLog("[Navigation] Switched to Reasoning Core operational view.");
    }

    // ── Visual Experience & Model Switcher ──────────────────────────────────
    private async void OnVisualModeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VisualModeCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            AppendLog($"[Visual Experience] Switching to {item.Content} ({tag})...");
            var res = await _voiceService.SetVisualModeAsync(tag);
            AppendLog($"[Visual Experience] Active mode: {tag} (success={res.Success})");
        }
    }

    private async void OnModelSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModelCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            AppendLog($"[Model Switcher] Switching to model: {item.Content} ({tag})...");
            var res = await _voiceService.SetModelAsync(tag);
            AppendLog($"[Model Switcher] Model updated: {tag} (success={res.Success})");
        }
    }

    private async void OnLaunchAdaptiveUiClicked(object sender, RoutedEventArgs e)
    {
        AppendLog("[Action] Launching SPARK Voice Mode Desktop Hologram...");
        try
        {
            await _voiceService.LaunchVoiceModeUiAsync();
            AppendLog("[Action] Desktop Hologram window dispatched.");
        }
        catch (Exception ex)
        {
            AppendLog($"[Error] Failed to launch Desktop Hologram: {ex.Message}");
        }
    }

    private void OnMicToggleClicked(object sender, RoutedEventArgs e)
    {
        _isMicActive = !_isMicActive;
        MicStatusText.Text = _isMicActive ? "Mic: Ready" : "Mic: Muted";
        MicIcon.Glyph = _isMicActive ? "\uE720" : "\uE74F";
        LiveStateTitleText.Text = _isMicActive ? "LISTENING..." : "SLEEPING...";
        LiveSubtitleText.Text = _isMicActive ? "I'm listening, Sir..." : "SPARK is asleep. Say 'Hey Spark' to wake.";
        AppendLog($"[Audio] Microphone status: {(_isMicActive ? "Active" : "Muted")}");
    }

    // ── Group 1: Voice Intelligence ─────────────────────────────────────────
    private async void OnTestPingClicked(object sender, RoutedEventArgs e)
    {
        AppendLog("[Group 1 - Voice] Sending ping to runtime...");
        var res = await _voiceService.SendCommandAsync("ping");
        AppendLog($"[Group 1 - Voice] Result: success={res.Success}, output={res.Result ?? res.Error}");
    }

    private async void OnInterruptVoiceClicked(object sender, RoutedEventArgs e)
    {
        AppendLog("[Group 1 - Voice] Requesting audio response interruption...");
        var res = await _voiceService.InterruptVoiceAsync();
        LiveStateTitleText.Text = "LISTENING...";
        LiveSubtitleText.Text = "I'm listening, Sir...";
        AppendLog($"[Group 1 - Voice] Interruption result: success={res.Success}");
    }

    // ── Group 2: Visual Awareness ───────────────────────────────────────────
    private async void OnScreenCaptureClicked(object sender, RoutedEventArgs e)
    {
        AppendLog("[Group 2 - Visual] Capturing screen context...");
        var res = await _voiceService.ScreenCaptureAsync();
        AppendLog($"[Group 2 - Visual] Screen capture: success={res.Success}, output={res.Result ?? res.RawJson ?? res.Error}");
    }

    private async void OnVisualQAClicked(object sender, RoutedEventArgs e)
    {
        AppendLog("[Group 2 - Visual] Querying vision module...");
        var res = await _voiceService.VisualQuestionAnsweringAsync("What is currently visible on the screen?");
        AppendLog($"[Group 2 - Visual] VQA response: success={res.Success}, output={res.Result ?? res.Error}");
    }

    // ── Group 3: Computer Control ───────────────────────────────────────────
    private async void OnScreenshotClicked(object sender, RoutedEventArgs e)
    {
        AppendLog("[Group 3 - Computer] Taking desktop screenshot...");
        var res = await _voiceService.ExecuteComputerSettingAsync("screenshot");
        AppendLog($"[Group 3 - Computer] Screenshot result: success={res.Success}, output={res.Result ?? res.Error}");
    }

    private async void OnBrowserOpenClicked(object sender, RoutedEventArgs e)
    {
        AppendLog("[Group 3 - Computer] Automating browser navigation to https://google.com...");
        var res = await _voiceService.BrowserControlAsync("go_to", new() { ["url"] = "https://google.com" });
        AppendLog($"[Group 3 - Computer] Browser result: success={res.Success}, output={res.Result ?? res.Error}");
    }

    private async void OnBrowserCloseClicked(object sender, RoutedEventArgs e)
    {
        AppendLog("[Group 3 - Computer] Closing automated browser...");
        var res = await _voiceService.BrowserControlAsync("close");
        AppendLog($"[Group 3 - Computer] Browser result: success={res.Success}, output={res.Result ?? res.Error}");
    }

    private async void OnLaunchAppClicked(object sender, RoutedEventArgs e)
    {
        AppendLog("[Group 3 - Computer] Launching application 'calc'...");
        var res = await _voiceService.OpenApplicationAsync("calc");
        AppendLog($"[Group 3 - Computer] Open app result: success={res.Success}, output={res.Result ?? res.Error}");
    }

    // ── Group 4 & 5: Autonomous Tasks & Memory ──────────────────────────────
    private async void OnLoadMemoryClicked(object sender, RoutedEventArgs e)
    {
        AppendLog("[Group 5 - Memory] Retrieving persistent memory stores...");
        var res = await _voiceService.LoadMemoryAsync();
        AppendLog($"[Group 5 - Memory] Memory store loaded: success={res.Success}, json={res.RawJson}");
    }

    private async void OnSubmitTaskClicked(object sender, RoutedEventArgs e)
    {
        AppendLog("[Group 4 - Tasks] Submitting autonomous multi-step task...");
        var res = await _voiceService.SubmitAutonomousTaskAsync("Research current system status and report.");
        AppendLog($"[Group 4 - Tasks] Task submitted: success={res.Success}, output={res.RawJson ?? res.Error}");
    }

    // ── Group 6: File Intelligence ──────────────────────────────────────────
    private async void OnChooseFileClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var filePath = await Win32FilePickerHelper.PickSingleFileAsync(IntPtr.Zero, "Select a file to analyze");
            if (!string.IsNullOrEmpty(filePath))
            {
                AppendLog($"[Group 6 - Files] Selected: {filePath}");
                var res = await _voiceService.ProcessFileAsync(filePath, "summarize");
                AppendLog($"[Group 6 - Files] Analysis result: success={res.Success}, output={res.Result ?? res.Error}");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"[Group 6 - Files] File selector error: {ex.Message}");
        }
    }

    private void OnClearOutputClicked(object sender, RoutedEventArgs e)
    {
        LogOutputTextBox.Text = string.Empty;
    }

    private async void OnSendCommandClicked(object sender, RoutedEventArgs e)
    {
        var cmd = CommandInputTextBox.Text.Trim();
        if (string.IsNullOrEmpty(cmd)) return;

        CommandInputTextBox.Text = string.Empty;
        AppendLog($"[Prompt] {cmd}");

        LiveStateTitleText.Text = "THINKING...";
        LiveSubtitleText.Text = "Let me process that, Sir...";

        var res = await _voiceService.VoiceInteractAsync(cmd);
        LiveStateTitleText.Text = "SPEAKING...";
        LiveSubtitleText.Text = res.Result ?? "Here you go.";
        AppendLog($"[Response] success={res.Success}, output={res.Result ?? res.Error}");
    }

    private void OnVoiceSettingsClicked(object sender, RoutedEventArgs e)
    {
        AppendLog("[Navigation] Opening dedicated Voice Mode Settings page...");
        Frame.Navigate(typeof(VoiceModeSettingsPage));
    }

    private void OnCommandInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == global::Windows.System.VirtualKey.Enter)
        {
            OnSendCommandClicked(sender, e);
            e.Handled = true;
        }
    }
}
