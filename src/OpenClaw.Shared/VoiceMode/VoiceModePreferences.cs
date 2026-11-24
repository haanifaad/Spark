using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.VoiceMode;

/// <summary>
/// Comprehensive settings model for all non-model Voice Mode behaviors,
/// preferences, permissions, and visual styling.
/// Persisted to %LOCALAPPDATA%\OpenClaw\voicemode\preferences.json.
/// </summary>
public sealed class VoiceModePreferences
{
    // 1. Voice Interaction
    public bool VoiceModeEnabled { get; set; } = true;
    public bool ContinuousConversation { get; set; } = true;
    public bool PushToTalk { get; set; } = false;
    public bool AutoListening { get; set; } = true;
    public string MicrophoneDevice { get; set; } = "Default";
    public string SpeakerDevice { get; set; } = "Default";
    public double InputSensitivity { get; set; } = 0.75;
    public bool VoiceActivityDetection { get; set; } = true;
    public bool SilenceDetection { get; set; } = true;
    public bool SpeechInterruption { get; set; } = true;
    public bool BargeInSupport { get; set; } = true;
    public bool EchoCancellation { get; set; } = true;
    public bool NoiseSuppression { get; set; } = true;
    public bool AutoSpeechDetection { get; set; } = true;
    public int ConversationTimeoutSeconds { get; set; } = 120;
    public bool WakeWordEnabled { get; set; } = true;
    public string WakeWordPhrase { get; set; } = "Hey Spark";
    public int AutoSleepMinutes { get; set; } = 2;

    // 2. Language Settings
    public string PrimaryLanguage { get; set; } = "en";
    public string SecondaryLanguage { get; set; } = "ml";
    public bool AutoLanguageDetection { get; set; } = true;
    public bool MalayalamLanguageSupport { get; set; } = true;
    public bool EnglishLanguageSupport { get; set; } = true;
    public bool MultilingualConversation { get; set; } = true;
    public bool AutoLanguageSwitching { get; set; } = true;
    public string RecognitionLanguage { get; set; } = "auto";
    public string OutputSpeechLanguage { get; set; } = "en";

    // 3. Speaking Settings
    public string SelectedVoice { get; set; } = "SPARK Nova Neural";
    public double SpeakingSpeed { get; set; } = 1.0;
    public double SpeakingPitch { get; set; } = 0.0;
    public double SpeakingVolume { get; set; } = 1.0;
    public string SpeechStyle { get; set; } = "Natural";
    public bool AutoSpeechGeneration { get; set; } = true;
    public bool StreamingSpeechOutput { get; set; } = true;
    public int PauseBetweenSentencesMs { get; set; } = 250;
    public bool AutoSpeakingInterruption { get; set; } = true;

    // 4. Face Mode, Reactor Mode, Standard HUD, and Orb Mode
    public string DefaultVisualMode { get; set; } = "standard"; // "standard" | "face" | "reactor" | "non-face"
    public bool AvatarAnimationsEnabled { get; set; } = true;
    public bool LipSyncEnabled { get; set; } = true;
    public bool FacialExpressionsEnabled { get; set; } = true;
    public bool EyeMovementEnabled { get; set; } = true;
    public bool BlinkingEnabled { get; set; } = true;
    public bool IdleAnimationsEnabled { get; set; } = true;
    public bool SpeakingAnimationsEnabled { get; set; } = true;
    public bool ListeningAnimationsEnabled { get; set; } = true;
    public bool ThinkingAnimationsEnabled { get; set; } = true;
    public bool HolographicVisualizerEnabled { get; set; } = true;
    public string WaveformIntensity { get; set; } = "High";
    public bool ParticleEffectsEnabled { get; set; } = true;
    public double VisualAnimationIntensity { get; set; } = 1.0;

    // 5. Reasoning and Task Display
    public bool ShowTaskProgress { get; set; } = true;
    public bool ShowTaskStages { get; set; } = true;
    public bool ShowExecutionActivity { get; set; } = true;
    public bool ShowToolActivity { get; set; } = true;
    public bool ShowVerificationStatus { get; set; } = true;
    public bool ShowCompletedTasks { get; set; } = true;
    public bool ShowBackgroundTaskNotifications { get; set; } = true;
    public string ActivityDisplayStyle { get; set; } = "Detailed"; // "Compact" | "Detailed"
    public bool EnableReasoningWorkspace { get; set; } = true;

    // 6. Computer & Screen Permissions
    public bool PermissionScreenUnderstanding { get; set; } = true;
    public bool PermissionScreenshotAnalysis { get; set; } = true;
    public bool PermissionWebcamAccess { get; set; } = false;
    public bool PermissionMouseKeyboardControl { get; set; } = true;
    public bool PermissionAppLaunching { get; set; } = true;
    public bool PermissionBrowserAutomation { get; set; } = true;
    public bool PermissionFileAccess { get; set; } = true;
    public bool PermissionDesktopAutomation { get; set; } = true;
    public bool RequireApprovalForDestructiveActions { get; set; } = true;

    // 7. Memory Settings
    public bool EnableVoiceModeMemory { get; set; } = true;
    public bool ConversationHistoryAccess { get; set; } = true;
    public bool MemoryRetrieval { get; set; } = true;
    public bool MemoryUpdatePermissions { get; set; } = true;
    public bool SessionContinuity { get; set; } = true;
    public bool SharedMemoryIntegration { get; set; } = true;

    // 8. Background Tasks
    public bool EnableBackgroundTasks { get; set; } = true;
    public bool NotifyTaskCompletion { get; set; } = true;
    public bool NotifyTaskFailure { get; set; } = true;
    public int MaxConcurrentTasks { get; set; } = 4;
    public int TaskTimeoutSeconds { get; set; } = 180;
    public bool ConfirmBackgroundWork { get; set; } = false;

    // 9. File Intelligence
    public bool EnableFileUnderstanding { get; set; } = true;
    public bool SupportPdfProcessing { get; set; } = true;
    public bool SupportImageUnderstanding { get; set; } = true;
    public bool SupportDocumentAnalysis { get; set; } = true;
    public bool SupportSourceCodeAnalysis { get; set; } = true;
    public bool EnableDragAndDropUploads { get; set; } = true;
    public int MaxFileSizeMb { get; set; } = 50;

    // 10. Privacy & Security
    public string DataRetentionPolicy { get; set; } = "SessionOnly";
    public bool SensitiveActionConfirmation { get; set; } = true;
    public bool SecureCredentialIsolation { get; set; } = true;

    // 11. General
    public string StartupBehavior { get; set; } = "Manual"; // "Manual" | "AutoStart"
    public string QualityProfile { get; set; } = "Balanced"; // "FullQuality" | "Balanced" | "BatterySaver"
    public bool SoundEffectsEnabled { get; set; } = true;
    public string PushToTalkShortcut { get; set; } = "Ctrl+Space";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string GetPreferencesPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenClaw",
            "voicemode");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "preferences.json");
    }

    public static async Task<VoiceModePreferences> LoadAsync(CancellationToken ct = default)
    {
        var path = GetPreferencesPath();
        if (File.Exists(path))
        {
            try
            {
                var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
                return JsonSerializer.Deserialize<VoiceModePreferences>(json) ?? new VoiceModePreferences();
            }
            catch
            {
                return new VoiceModePreferences();
            }
        }
        return new VoiceModePreferences();
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        var path = GetPreferencesPath();
        var json = JsonSerializer.Serialize(this, JsonOptions);
        await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
    }
}
