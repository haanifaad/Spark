using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using OpenClaw.Shared.VoiceMode;
using Xunit;

namespace OpenClaw.Shared.Tests.VoiceModeTests;

public class VoiceModeModelConfigTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly VoiceModeCredentialStore _credentialStore;
    private readonly VoiceModeConfigurationService _configService;

    public VoiceModeModelConfigTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "OpenClawVoiceModeTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);

        _credentialStore = new VoiceModeCredentialStore(_testDirectory);
        _configService = new VoiceModeConfigurationService(_credentialStore, divisionService: null, configDirectory: _testDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task DefaultConfiguration_ContainsAllFiveCapabilities()
    {
        var config = await _configService.LoadConfigurationAsync();

        Assert.NotNull(config);
        Assert.NotNull(config.Hearing);
        Assert.NotNull(config.Thinking);
        Assert.NotNull(config.Speaking);
        Assert.NotNull(config.Background);
        Assert.NotNull(config.Coding);

        Assert.True(config.Hearing.IsEnabled);
        Assert.True(config.Thinking.IsEnabled);
        Assert.True(config.Speaking.IsEnabled);
        Assert.True(config.Background.IsEnabled);
        Assert.True(config.Coding.IsEnabled);

        Assert.Equal("Google", config.Hearing.Provider);
        Assert.Equal("Google", config.Thinking.Provider);
        Assert.Equal("EdgeTTS", config.Speaking.Provider);
        Assert.Equal("Google", config.Background.Provider);
        Assert.Equal("Anthropic", config.Coding.Provider);
    }

    [Fact]
    public async Task CredentialStore_StoresAndIsolatesKeysPerCapability()
    {
        // 1. Set independent keys
        await _credentialStore.SetApiKeyAsync(VoiceCapabilityKind.Hearing, "hearing-api-key-111");
        await _credentialStore.SetApiKeyAsync(VoiceCapabilityKind.Thinking, "thinking-api-key-222");
        await _credentialStore.SetApiKeyAsync(VoiceCapabilityKind.Speaking, "speaking-api-key-333");
        await _credentialStore.SetApiKeyAsync(VoiceCapabilityKind.Background, "background-api-key-444");
        await _credentialStore.SetApiKeyAsync(VoiceCapabilityKind.Coding, "coding-api-key-555");

        // 2. Verify all keys are stored and decrypted accurately
        Assert.Equal("hearing-api-key-111", await _credentialStore.GetApiKeyAsync(VoiceCapabilityKind.Hearing));
        Assert.Equal("thinking-api-key-222", await _credentialStore.GetApiKeyAsync(VoiceCapabilityKind.Thinking));
        Assert.Equal("speaking-api-key-333", await _credentialStore.GetApiKeyAsync(VoiceCapabilityKind.Speaking));
        Assert.Equal("background-api-key-444", await _credentialStore.GetApiKeyAsync(VoiceCapabilityKind.Background));
        Assert.Equal("coding-api-key-555", await _credentialStore.GetApiKeyAsync(VoiceCapabilityKind.Coding));

        // 3. Clear one key (Coding) and verify other keys remain intact
        await _credentialStore.ClearApiKeyAsync(VoiceCapabilityKind.Coding);

        Assert.Null(await _credentialStore.GetApiKeyAsync(VoiceCapabilityKind.Coding));
        Assert.False(await _credentialStore.HasApiKeyAsync(VoiceCapabilityKind.Coding));

        // Thinking and background credentials must not be affected
        Assert.Equal("thinking-api-key-222", await _credentialStore.GetApiKeyAsync(VoiceCapabilityKind.Thinking));
        Assert.Equal("background-api-key-444", await _credentialStore.GetApiKeyAsync(VoiceCapabilityKind.Background));
        Assert.Equal("hearing-api-key-111", await _credentialStore.GetApiKeyAsync(VoiceCapabilityKind.Hearing));
        Assert.Equal("speaking-api-key-333", await _credentialStore.GetApiKeyAsync(VoiceCapabilityKind.Speaking));
    }

    [Fact]
    public void CredentialStore_MaskKey_MasksSecretsSecurely()
    {
        var rawKey = "sk-live-1234567890abcdef";
        var masked = _credentialStore.MaskKey(rawKey);

        Assert.Contains("••••", masked);
        Assert.DoesNotContain("1234567890", masked);
        Assert.Equal(string.Empty, _credentialStore.MaskKey(null));
        Assert.Equal(string.Empty, _credentialStore.MaskKey("   "));
    }

    [Fact]
    public async Task SaveConfigurationAsync_PersistsWithoutPlaintextInConfigFile()
    {
        var config = await _configService.LoadConfigurationAsync();
        config.Coding.Provider = "DeepSeek";
        config.Coding.Model = "deepseek-coder-v2";
        config.Coding.Temperature = 0.15;

        var updatedKeys = new Dictionary<VoiceCapabilityKind, string?>
        {
            [VoiceCapabilityKind.Coding] = "deepseek-super-secret-key"
        };

        await _configService.SaveConfigurationAsync(config, updatedKeys);

        // 1. Read config file raw content from disk
        var configFile = Path.Combine(_testDirectory, "models_config.json");
        Assert.True(File.Exists(configFile));
        var rawJson = await File.ReadAllTextAsync(configFile);

        // Raw json contains metadata but NO plaintext API keys
        Assert.Contains("DeepSeek", rawJson);
        Assert.Contains("deepseek-coder-v2", rawJson);
        Assert.DoesNotContain("deepseek-super-secret-key", rawJson);

        // 2. Secret is securely stored in encrypted vault
        var recoveredKey = await _credentialStore.GetApiKeyAsync(VoiceCapabilityKind.Coding);
        Assert.Equal("deepseek-super-secret-key", recoveredKey);
    }

    [Fact]
    public async Task TestConnectionAsync_ValidatesProvidersAccurately()
    {
        // 1. EdgeTTS requires no cloud API key
        var edgeSpeaking = new VoiceModelCapabilityConfig(VoiceCapabilityKind.Speaking, "EdgeTTS", "edge-tts-neural");
        var edgeResult = await _configService.TestConnectionAsync(VoiceCapabilityKind.Speaking, edgeSpeaking);
        Assert.True(edgeResult.Success);
        Assert.Contains("EdgeTTS", edgeResult.Message);

        // 2. Local Whisper requires no cloud API key
        var localHearing = new VoiceModelCapabilityConfig(VoiceCapabilityKind.Hearing, "Local", "faster-whisper-medium");
        var localResult = await _configService.TestConnectionAsync(VoiceCapabilityKind.Hearing, localHearing);
        Assert.True(localResult.Success);
        Assert.Contains("Local engine", localResult.Message);

        // 3. Cloud provider with missing API key reports missing key
        var anthropicCoding = new VoiceModelCapabilityConfig(VoiceCapabilityKind.Coding, "Anthropic", "claude-3-5-sonnet-20241022");
        var failResult = await _configService.TestConnectionAsync(VoiceCapabilityKind.Coding, anthropicCoding, candidateApiKey: "");
        Assert.False(failResult.Success);
        Assert.Contains("No API key configured", failResult.Message);

        // 4. Cloud provider with invalid/fake API key accurately fails authentication or reports network status
        var fakeResult = await _configService.TestConnectionAsync(VoiceCapabilityKind.Coding, anthropicCoding, candidateApiKey: "sk-ant-invalid-key");
        Assert.False(fakeResult.Success);
        Assert.NotEmpty(fakeResult.Message);
    }

    [Fact]
    public async Task VoiceModePreferences_SaveAndLoad_PreservesAllSettings()
    {
        var prefs = new VoiceModePreferences
        {
            WakeWordPhrase = "Hey Spark",
            AutoSleepMinutes = 2,
            MalayalamLanguageSupport = true,
            PrimaryLanguage = "en",
            SecondaryLanguage = "ml",
            DefaultVisualMode = "non-face",
            SpeakingSpeed = 1.15,
            PermissionDesktopAutomation = true,
            PermissionScreenUnderstanding = true,
            EnableFileUnderstanding = true,
            EnableBackgroundTasks = true
        };

        await prefs.SaveAsync();

        var loaded = await VoiceModePreferences.LoadAsync();
        Assert.NotNull(loaded);
        Assert.Equal("Hey Spark", loaded.WakeWordPhrase);
        Assert.Equal(2, loaded.AutoSleepMinutes);
        Assert.True(loaded.MalayalamLanguageSupport);
        Assert.Equal("en", loaded.PrimaryLanguage);
        Assert.Equal("ml", loaded.SecondaryLanguage);
        Assert.Equal("non-face", loaded.DefaultVisualMode);
        Assert.Equal(1.15, loaded.SpeakingSpeed, 2);
        Assert.True(loaded.PermissionDesktopAutomation);
        Assert.True(loaded.PermissionScreenUnderstanding);
        Assert.True(loaded.EnableFileUnderstanding);
        Assert.True(loaded.EnableBackgroundTasks);
    }
}
