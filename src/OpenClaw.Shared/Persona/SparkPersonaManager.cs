using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Persona;

/// <summary>
/// Thread-safe manager for SPARK persona configurations, presets, and persistence.
/// </summary>
public sealed class SparkPersonaManager : ISparkPersonaManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true
    };

    private static readonly IReadOnlyList<SparkPersonaConfig> BuiltInPresets = new List<SparkPersonaConfig>
    {
        new()
        {
            AgentName = "SPARK",
            PresetName = "Default Autonomous Engineer",
            Tone = AgentTone.Technical,
            PrimaryLanguage = AgentLanguage.English,
            Verbosity = AgentVerbosity.Balanced,
            EnableBilingualCodeSwitching = true,
            MalayalamScriptPreference = "Malayalam Unicode",
            BehavioralRules = new[]
            {
                "Always provide structured, verified solutions with clear rationale.",
                "Verify file paths, schemas, and configurations before modifying them.",
                "Seamlessly code-switch to Malayalam if the user queries in Malayalam."
            }
        },
        new()
        {
            AgentName = "SPARK",
            PresetName = "Concise Executive Assistant",
            Tone = AgentTone.Concise,
            PrimaryLanguage = AgentLanguage.English,
            Verbosity = AgentVerbosity.Concise,
            EnableBilingualCodeSwitching = false,
            BehavioralRules = new[]
            {
                "Keep answers direct, actionable, and under three paragraphs unless requested otherwise.",
                "Focus on outcomes, next actions, and critical blockers."
            }
        },
        new()
        {
            AgentName = "SPARK",
            PresetName = "Malayalam AI Companion (മലയാളി സഹായി)",
            Tone = AgentTone.Friendly,
            PrimaryLanguage = AgentLanguage.Malayalam,
            Verbosity = AgentVerbosity.Balanced,
            EnableBilingualCodeSwitching = true,
            MalayalamScriptPreference = "Malayalam Unicode",
            BehavioralRules = new[]
            {
                "എല്ലാ ചോദ്യങ്ങൾക്കും മലയാളത്തിൽ വ്യക്തവും മനോഹരവുമായ മറുപടി നൽകുക.",
                "സാങ്കേതിക പദങ്ങൾ ആവശ്യമുള്ളപ്പോൾ ഇംഗ്ലീഷിലും മലയാളത്തിലും സൂചിപ്പിക്കുക.",
                "സ്നേഹപൂർണ്ണവും കാര്യക്ഷമവുമായ സമീപനം നിലനിർത്തുക."
            }
        },
        new()
        {
            AgentName = "SPARK",
            PresetName = "Senior Systems Architect",
            Tone = AgentTone.Analytical,
            PrimaryLanguage = AgentLanguage.Bilingual,
            Verbosity = AgentVerbosity.Comprehensive,
            EnableBilingualCodeSwitching = true,
            BehavioralRules = new[]
            {
                "Analyze problems from first principles, considering security, scalability, and maintainability.",
                "Surface potential trade-offs and architectural edge cases upfront.",
                "Enforce strict human-in-the-loop verification for privileged operations."
            }
        }
    };

    private readonly string _baseStoragePath;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _userGates = new(StringComparer.OrdinalIgnoreCase);

    public SparkPersonaManager(string? baseStoragePath = null)
    {
        if (!string.IsNullOrWhiteSpace(baseStoragePath))
        {
            _baseStoragePath = baseStoragePath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _baseStoragePath = Path.Combine(appData, "Spark", "Persona");
        }

        Directory.CreateDirectory(_baseStoragePath);
    }

    private SemaphoreSlim GetGate(string userId) =>
        _userGates.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));

    private string GetPersonaFilePath(string userId)
    {
        var clean = string.IsNullOrWhiteSpace(userId) ? "default" : userId.Trim();
        var safe = string.Join("_", clean.Split(Path.GetInvalidFileNameChars()));
        var dir = Path.Combine(_baseStoragePath, safe);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "persona.json");
    }

    public async Task<SparkPersonaConfig> GetCurrentPersonaAsync(string userId = "default", CancellationToken ct = default)
    {
        var gate = GetGate(userId);
        await gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var file = GetPersonaFilePath(userId);
            if (!File.Exists(file))
            {
                return BuiltInPresets[0];
            }

            await using var stream = File.OpenRead(file);
            var persona = await JsonSerializer.DeserializeAsync<SparkPersonaConfig>(stream, JsonOptions, ct).ConfigureAwait(false);
            return persona ?? BuiltInPresets[0];
        }
        catch
        {
            return BuiltInPresets[0];
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SavePersonaAsync(SparkPersonaConfig config, string userId = "default", CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        var gate = GetGate(userId);
        await gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var file = GetPersonaFilePath(userId);
            var temp = file + ".tmp";

            var updated = config with { LastModified = DateTimeOffset.UtcNow };
            var json = JsonSerializer.Serialize(updated, JsonOptions);

            await File.WriteAllTextAsync(temp, json, ct).ConfigureAwait(false);
            File.Move(temp, file, overwrite: true);
        }
        finally
        {
            gate.Release();
        }
    }

    public Task<IReadOnlyList<SparkPersonaConfig>> GetPresetsAsync(CancellationToken ct = default) =>
        Task.FromResult(BuiltInPresets);

    public async Task ResetToDefaultAsync(string userId = "default", CancellationToken ct = default) =>
        await SavePersonaAsync(BuiltInPresets[0], userId, ct).ConfigureAwait(false);
}
