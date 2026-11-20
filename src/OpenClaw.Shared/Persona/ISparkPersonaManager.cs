using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Persona;

/// <summary>
/// Contract for managing SPARK persona configurations, presets, and lifecycle.
/// </summary>
public interface ISparkPersonaManager
{
    /// <summary>
    /// Retrieves the active persona configuration for a given user.
    /// </summary>
    Task<SparkPersonaConfig> GetCurrentPersonaAsync(string userId = "default", CancellationToken ct = default);

    /// <summary>
    /// Saves and updates the active persona configuration.
    /// </summary>
    Task SavePersonaAsync(SparkPersonaConfig config, string userId = "default", CancellationToken ct = default);

    /// <summary>
    /// Gets built-in persona presets (e.g. Technical Mentor, Concise Assistant, Bilingual Companion).
    /// </summary>
    Task<IReadOnlyList<SparkPersonaConfig>> GetPresetsAsync(CancellationToken ct = default);

    /// <summary>
    /// Resets the user's persona to the system default configuration.
    /// </summary>
    Task ResetToDefaultAsync(string userId = "default", CancellationToken ct = default);
}
