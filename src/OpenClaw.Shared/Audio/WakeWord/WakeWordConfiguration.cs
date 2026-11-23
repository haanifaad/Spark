using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenClaw.Shared.Audio.WakeWord;

/// <summary>
/// Configuration for custom local wake-word detection.
/// </summary>
public sealed class WakeWordConfiguration
{
    /// <summary>Default primary activation phrases and variants for SPARK.</summary>
    public static readonly IReadOnlyList<string> DefaultPhrases = [
        "Hey Spark",
        "Spark",
        "Spark what's up",
        "Spark whats up",
        "Good morning Spark",
        "Good morning, spark"
    ];

    /// <summary>Whether wake word detection is actively enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>User-configured primary activation phrases.</summary>
    public List<string> ActivationPhrases { get; set; } = [..DefaultPhrases];

    /// <summary>User-configured aliases (alternative trigger phrases).</summary>
    public List<string> Aliases { get; set; } = [
        "Computer",
        "Hey Spark",
        "Spark what's up",
        "Spark whats up",
        "Good morning Spark"
    ];

    /// <summary>
    /// Sensitivity threshold between 0.1 and 1.0 (default 0.65).
    /// Higher values trigger more readily; lower values require higher acoustic confidence.
    /// </summary>
    public float Sensitivity { get; set; } = 0.65f;

    /// <summary>
    /// Auto-sleep timeout in seconds after inactivity / silence (default 120s = 2 minutes).
    /// </summary>
    public float AutoSleepSeconds { get; set; } = 120.0f;

    /// <summary>
    /// When true, requires the user to repeat the activation phrase within the repeat window
    /// (e.g. "Spark, Spark") before the system wakes.
    /// </summary>
    public bool RequireRepeatedPhrase { get; set; } = false;

    /// <summary>Time window in seconds to receive the second activation phrase when repeat is required.</summary>
    public float RepeatWindowSeconds { get; set; } = 2.5f;

    /// <summary>Cooldown period in seconds after a successful trigger before another trigger can fire.</summary>
    public float CooldownSeconds { get; set; } = 1.0f;

    /// <summary>
    /// Gets all active trigger phrases combined (primary phrases + aliases), normalized to lowercase.
    /// </summary>
    public IReadOnlyList<string> GetAllNormalizedPhrases()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in ActivationPhrases)
        {
            if (!string.IsNullOrWhiteSpace(p)) set.Add(p.Trim().ToLowerInvariant());
        }
        foreach (var a in Aliases)
        {
            if (!string.IsNullOrWhiteSpace(a)) set.Add(a.Trim().ToLowerInvariant());
        }
        return set.ToList();
    }
}
