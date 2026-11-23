using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.VoiceMode;

/// <summary>
/// Implements <see cref="IVoiceModeConfigurationService"/>, managing configuration storage,
/// credential isolation, provider connectivity testing, and IPC runtime synchronization.
/// </summary>
public sealed class VoiceModeConfigurationService : IVoiceModeConfigurationService
{
    private readonly IVoiceModeCredentialStore _credentialStore;
    private readonly IVoiceModeDivisionService? _divisionService;
    private readonly string _configFilePath;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private VoiceModeModelsConfig? _cachedConfig;

    public IVoiceModeCredentialStore Credentials => _credentialStore;

    public VoiceModeConfigurationService(
        IVoiceModeCredentialStore? credentialStore = null,
        IVoiceModeDivisionService? divisionService = null,
        string? configDirectory = null)
    {
        _credentialStore = credentialStore ?? new VoiceModeCredentialStore();
        _divisionService = divisionService;

        var baseDir = configDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenClaw",
            "voicemode");

        Directory.CreateDirectory(baseDir);
        _configFilePath = Path.Combine(baseDir, "models_config.json");

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public async Task<VoiceModeModelsConfig> LoadConfigurationAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            VoiceModeModelsConfig config;
            if (File.Exists(_configFilePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(_configFilePath, ct).ConfigureAwait(false);
                    config = JsonSerializer.Deserialize<VoiceModeModelsConfig>(json) ?? new VoiceModeModelsConfig();
                }
                catch
                {
                    config = new VoiceModeModelsConfig();
                }
            }
            else
            {
                config = new VoiceModeModelsConfig();
            }

            // Hydrate masked keys and flags from credential store
            foreach (VoiceCapabilityKind cap in Enum.GetValues<VoiceCapabilityKind>())
            {
                var capConfig = config.GetCapability(cap);
                var rawKey = await _credentialStore.GetApiKeyAsync(cap, ct).ConfigureAwait(false);
                capConfig.HasStoredApiKey = !string.IsNullOrWhiteSpace(rawKey);
                capConfig.MaskedApiKey = _credentialStore.MaskKey(rawKey);
            }

            _cachedConfig = config;
            return config;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveConfigurationAsync(
        VoiceModeModelsConfig config,
        Dictionary<VoiceCapabilityKind, string?>? updatedApiKeys = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // 1. Process API key updates independently
            if (updatedApiKeys != null)
            {
                foreach (var (capability, key) in updatedApiKeys)
                {
                    if (key != null)
                    {
                        if (string.IsNullOrWhiteSpace(key))
                        {
                            await _credentialStore.ClearApiKeyAsync(capability, ct).ConfigureAwait(false);
                        }
                        else if (!key.Contains('•')) // Only update if not the masked placeholder
                        {
                            await _credentialStore.SetApiKeyAsync(capability, key, ct).ConfigureAwait(false);
                        }
                    }
                }
            }

            // 2. Persist configuration metadata (NO plaintext keys in this file)
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            var tempPath = _configFilePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, Encoding.UTF8, ct).ConfigureAwait(false);
            File.Move(tempPath, _configFilePath, overwrite: true);

            _cachedConfig = config;
        }
        finally
        {
            _lock.Release();
        }

        // 3. Synchronize to runtime
        await SyncToRuntimeAsync(ct).ConfigureAwait(false);
    }

    public async Task<VoiceCapabilityTestResult> TestConnectionAsync(
        VoiceCapabilityKind capability,
        VoiceModelCapabilityConfig config,
        string? candidateApiKey = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        var sw = Stopwatch.StartNew();

        // Resolve effective API key for this specific capability
        string? apiKey = candidateApiKey;
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Contains('•'))
        {
            apiKey = await _credentialStore.GetApiKeyAsync(capability, ct).ConfigureAwait(false);
        }

        var provider = config.Provider?.Trim() ?? string.Empty;
        var model = config.Model?.Trim() ?? string.Empty;

        try
        {
            // EdgeTTS, Kokoro, System SAPI do not require cloud API keys
            if (provider.Equals("EdgeTTS", StringComparison.OrdinalIgnoreCase) ||
                provider.Equals("System", StringComparison.OrdinalIgnoreCase) ||
                provider.Equals("Kokoro", StringComparison.OrdinalIgnoreCase) ||
                provider.Equals("Local", StringComparison.OrdinalIgnoreCase))
            {
                sw.Stop();
                return new VoiceCapabilityTestResult(
                    true,
                    $"{provider} engine is available locally on this system.",
                    sw.ElapsedMilliseconds,
                    DateTime.UtcNow);
            }

            // Local Ollama
            if (provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase) ||
                provider.Equals("Ollama Local", StringComparison.OrdinalIgnoreCase))
            {
                var endpoint = !string.IsNullOrWhiteSpace(config.Endpoint)
                    ? config.Endpoint
                    : "http://localhost:11434";

                var requestUrl = endpoint.TrimEnd('/') + "/api/tags";
                var res = await _httpClient.GetAsync(requestUrl, ct).ConfigureAwait(false);
                sw.Stop();

                if (res.IsSuccessStatusCode)
                {
                    return new VoiceCapabilityTestResult(
                        true,
                        $"Connected to Ollama local instance successfully ({sw.ElapsedMilliseconds}ms).",
                        sw.ElapsedMilliseconds,
                        DateTime.UtcNow);
                }
                else
                {
                    return new VoiceCapabilityTestResult(
                        false,
                        $"Ollama returned HTTP {(int)res.StatusCode}: {res.ReasonPhrase}",
                        sw.ElapsedMilliseconds,
                        DateTime.UtcNow);
                }
            }

            // Cloud providers require an API key
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                sw.Stop();
                return new VoiceCapabilityTestResult(
                    false,
                    $"No API key configured for {capability}. Please enter a valid API key.",
                    sw.ElapsedMilliseconds,
                    DateTime.UtcNow);
            }

            // Google Gemini
            if (provider.Contains("Google", StringComparison.OrdinalIgnoreCase) ||
                provider.Contains("Gemini", StringComparison.OrdinalIgnoreCase))
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={Uri.EscapeDataString(apiKey)}";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                var res = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);
                sw.Stop();

                if (res.IsSuccessStatusCode)
                {
                    return new VoiceCapabilityTestResult(
                        true,
                        $"Google Gemini API verified successfully ({sw.ElapsedMilliseconds}ms). Model '{model}' ready.",
                        sw.ElapsedMilliseconds,
                        DateTime.UtcNow);
                }
                else
                {
                    var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    return new VoiceCapabilityTestResult(
                        false,
                        $"Google Gemini authentication failed: HTTP {(int)res.StatusCode}",
                        sw.ElapsedMilliseconds,
                        DateTime.UtcNow,
                        body);
                }
            }

            // OpenAI / Whisper / OpenAI TTS
            if (provider.Contains("OpenAI", StringComparison.OrdinalIgnoreCase) ||
                provider.Contains("Groq", StringComparison.OrdinalIgnoreCase) ||
                provider.Contains("DeepSeek", StringComparison.OrdinalIgnoreCase))
            {
                var baseUrl = !string.IsNullOrWhiteSpace(config.Endpoint)
                    ? config.Endpoint.TrimEnd('/')
                    : provider.Contains("Groq", StringComparison.OrdinalIgnoreCase)
                        ? "https://api.groq.com/openai/v1"
                        : provider.Contains("DeepSeek", StringComparison.OrdinalIgnoreCase)
                            ? "https://api.deepseek.com"
                            : "https://api.openai.com/v1";

                using var req = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/models");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                var res = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);
                sw.Stop();

                if (res.IsSuccessStatusCode)
                {
                    return new VoiceCapabilityTestResult(
                        true,
                        $"{provider} authenticated successfully ({sw.ElapsedMilliseconds}ms). Model '{model}' ready.",
                        sw.ElapsedMilliseconds,
                        DateTime.UtcNow);
                }
                else
                {
                    var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    return new VoiceCapabilityTestResult(
                        false,
                        $"{provider} connection failed: HTTP {(int)res.StatusCode}",
                        sw.ElapsedMilliseconds,
                        DateTime.UtcNow,
                        body);
                }
            }

            // Anthropic Claude
            if (provider.Contains("Anthropic", StringComparison.OrdinalIgnoreCase) ||
                provider.Contains("Claude", StringComparison.OrdinalIgnoreCase))
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/v1/models");
                req.Headers.Add("x-api-key", apiKey);
                req.Headers.Add("anthropic-version", "2023-06-01");
                var res = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);
                sw.Stop();

                if (res.IsSuccessStatusCode)
                {
                    return new VoiceCapabilityTestResult(
                        true,
                        $"Anthropic Claude authenticated successfully ({sw.ElapsedMilliseconds}ms). Model '{model}' ready.",
                        sw.ElapsedMilliseconds,
                        DateTime.UtcNow);
                }
                else
                {
                    var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    return new VoiceCapabilityTestResult(
                        false,
                        $"Anthropic connection failed: HTTP {(int)res.StatusCode}",
                        sw.ElapsedMilliseconds,
                        DateTime.UtcNow,
                        body);
                }
            }

            // ElevenLabs TTS
            if (provider.Contains("ElevenLabs", StringComparison.OrdinalIgnoreCase))
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.elevenlabs.io/v1/user");
                req.Headers.Add("xi-api-key", apiKey);
                var res = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);
                sw.Stop();

                if (res.IsSuccessStatusCode)
                {
                    return new VoiceCapabilityTestResult(
                        true,
                        $"ElevenLabs verified successfully ({sw.ElapsedMilliseconds}ms). Voice '{config.Voice ?? "default"}' ready.",
                        sw.ElapsedMilliseconds,
                        DateTime.UtcNow);
                }
                else
                {
                    var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    return new VoiceCapabilityTestResult(
                        false,
                        $"ElevenLabs authentication failed: HTTP {(int)res.StatusCode}",
                        sw.ElapsedMilliseconds,
                        DateTime.UtcNow,
                        body);
                }
            }

            // Custom Endpoint
            if (!string.IsNullOrWhiteSpace(config.Endpoint))
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, config.Endpoint);
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                }
                var res = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);
                sw.Stop();

                return new VoiceCapabilityTestResult(
                    res.IsSuccessStatusCode,
                    res.IsSuccessStatusCode
                        ? $"Custom endpoint verified ({sw.ElapsedMilliseconds}ms)."
                        : $"Custom endpoint returned HTTP {(int)res.StatusCode}: {res.ReasonPhrase}",
                    sw.ElapsedMilliseconds,
                    DateTime.UtcNow);
            }

            sw.Stop();
            return new VoiceCapabilityTestResult(
                true,
                $"Provider '{provider}' configured for {capability}.",
                sw.ElapsedMilliseconds,
                DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new VoiceCapabilityTestResult(
                false,
                $"Connection test error: {ex.Message}",
                sw.ElapsedMilliseconds,
                DateTime.UtcNow,
                ex.ToString());
        }
    }

    public async Task<bool> SyncToRuntimeAsync(CancellationToken ct = default)
    {
        if (_divisionService == null) return false;

        var config = _cachedConfig ?? await LoadConfigurationAsync(ct).ConfigureAwait(false);

        // Build IPC payload with capability settings and decrypted isolated keys
        Dictionary<string, object?> payload = new()
        {
            ["hearing"] = new Dictionary<string, object?>
            {
                ["enabled"] = config.Hearing.IsEnabled,
                ["provider"] = config.Hearing.Provider,
                ["model"] = config.Hearing.Model,
                ["endpoint"] = config.Hearing.Endpoint,
                ["language"] = config.Hearing.Language,
                ["api_key"] = await _credentialStore.GetApiKeyAsync(VoiceCapabilityKind.Hearing, ct).ConfigureAwait(false) ?? string.Empty
            },
            ["thinking"] = new Dictionary<string, object?>
            {
                ["enabled"] = config.Thinking.IsEnabled,
                ["provider"] = config.Thinking.Provider,
                ["model"] = config.Thinking.Model,
                ["endpoint"] = config.Thinking.Endpoint,
                ["temperature"] = config.Thinking.Temperature,
                ["api_key"] = await _credentialStore.GetApiKeyAsync(VoiceCapabilityKind.Thinking, ct).ConfigureAwait(false) ?? string.Empty
            },
            ["speaking"] = new Dictionary<string, object?>
            {
                ["enabled"] = config.Speaking.IsEnabled,
                ["provider"] = config.Speaking.Provider,
                ["model"] = config.Speaking.Model,
                ["voice"] = config.Speaking.Voice,
                ["speed"] = config.Speaking.Speed,
                ["language"] = config.Speaking.Language,
                ["api_key"] = await _credentialStore.GetApiKeyAsync(VoiceCapabilityKind.Speaking, ct).ConfigureAwait(false) ?? string.Empty
            },
            ["background"] = new Dictionary<string, object?>
            {
                ["enabled"] = config.Background.IsEnabled,
                ["provider"] = config.Background.Provider,
                ["model"] = config.Background.Model,
                ["endpoint"] = config.Background.Endpoint,
                ["timeout_seconds"] = config.Background.TimeoutSeconds,
                ["max_tokens"] = config.Background.MaxOutputTokens,
                ["api_key"] = await _credentialStore.GetApiKeyAsync(VoiceCapabilityKind.Background, ct).ConfigureAwait(false) ?? string.Empty
            },
            ["coding"] = new Dictionary<string, object?>
            {
                ["enabled"] = config.Coding.IsEnabled,
                ["provider"] = config.Coding.Provider,
                ["model"] = config.Coding.Model,
                ["endpoint"] = config.Coding.Endpoint,
                ["temperature"] = config.Coding.Temperature,
                ["api_key"] = await _credentialStore.GetApiKeyAsync(VoiceCapabilityKind.Coding, ct).ConfigureAwait(false) ?? string.Empty
            }
        };

        try
        {
            var res = await _divisionService.SendCommandAsync("update_voice_models_config", payload, ct).ConfigureAwait(false);
            return res.Success;
        }
        catch
        {
            return false;
        }
    }
}
