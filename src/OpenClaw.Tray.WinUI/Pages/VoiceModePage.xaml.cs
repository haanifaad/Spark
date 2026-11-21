using System;
using System.Collections.Generic;
using System.IO;
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

    public VoiceModePage()
    {
        InitializeComponent();
        _voiceService = CurrentApp.Services?.GetService(typeof(IVoiceModeDivisionService)) as IVoiceModeDivisionService
            ?? new VoiceModeDivisionService(CurrentApp.DivisionManager);

        _voiceService.LogMessageAppended += (s, msg) => AppendLog(msg);
        _voiceService.OutputReceived += (s, msg) => AppendLog($"[STDOUT] {msg}");
        _voiceService.ErrorReceived += (s, msg) => AppendLog($"[STDERR] {msg}");

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        AppendLog("[SPARK Voice Mode] Initializing division environment...");
        try
        {
            await _voiceService.EnsureRuntimeStartedAsync();
            AppendLog("[SPARK Voice Mode] Runtime connected. Ready for commands.");
        }
        catch (Exception ex)
        {
            AppendLog($"[SPARK Voice Mode] Runtime warning: {ex.Message}");
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

    private async void OnLaunchAdaptiveUiClicked(object sender, RoutedEventArgs e)
    {
        AppendLog("[Action] Launching SPARK Voice Mode Adaptive UI...");
        try
        {
            await _voiceService.LaunchVoiceModeUiAsync();
            AppendLog("[Action] Adaptive UI process started.");
        }
        catch (Exception ex)
        {
            AppendLog($"[Error] Failed to launch Adaptive UI: {ex.Message}");
        }
    }

    private void OnMicToggleClicked(object sender, RoutedEventArgs e)
    {
        _isMicActive = !_isMicActive;
        MicStatusText.Text = _isMicActive ? "Mic: Ready" : "Mic: Muted";
        MicIcon.Glyph = _isMicActive ? "\uE720" : "\uE74F";
        AppendLog($"[Audio] Microphone status: {(_isMicActive ? "Active" : "Muted")}");
    }

    private async void OnTestPingClicked(object sender, RoutedEventArgs e)
    {
        AppendLog("[Group 1 - Voice] Sending ping to runtime...");
        var res = await _voiceService.SendCommandAsync("ping");
        AppendLog($"[Group 1 - Voice] Result: success={res.Success}, output={res.Result ?? res.Error}");
    }

    private void OnMuteToggleClicked(object sender, RoutedEventArgs e)
    {
        OnMicToggleClicked(sender, e);
    }

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

        var res = await _voiceService.SendCommandAsync("text_interact", new() { ["text"] = cmd });
        AppendLog($"[Response] success={res.Success}, output={res.Result ?? res.Error}");
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
