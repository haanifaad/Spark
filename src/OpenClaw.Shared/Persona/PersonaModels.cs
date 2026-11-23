using System;
using System.Collections.Generic;

namespace OpenClaw.Shared.Persona;

/// <summary>
/// Supported primary languages for SPARK communication.
/// </summary>
public enum AgentLanguage
{
    English,
    Malayalam,
    Bilingual
}

/// <summary>
/// Communication tone presets.
/// </summary>
public enum AgentTone
{
    Professional,
    Concise,
    Friendly,
    Technical,
    Analytical
}

/// <summary>
/// Verbosity level presets.
/// </summary>
public enum AgentVerbosity
{
    Concise,
    Balanced,
    Comprehensive
}

/// <summary>
/// Configurable persona and behavioral configuration for the SPARK autonomous personal AI agent.
/// </summary>
public sealed record SparkPersonaConfig
{
    public string AgentName { get; init; } = "SPARK";
    public string PresetName { get; init; } = "Default Engineer";
    public AgentTone Tone { get; init; } = AgentTone.Technical;
    public AgentLanguage PrimaryLanguage { get; init; } = AgentLanguage.English;
    public AgentVerbosity Verbosity { get; init; } = AgentVerbosity.Balanced;

    /// <summary>
    /// When true, allows seamless code-switching between Malayalam and English based on user input.
    /// </summary>
    public bool EnableBilingualCodeSwitching { get; init; } = true;

    /// <summary>
    /// Preferred Malayalam script style: Malayalam Unicode (മലയാളം) or phonetic Latin script (Manglish).
    /// </summary>
    public string MalayalamScriptPreference { get; init; } = "Malayalam Unicode";

    /// <summary>
    /// User-defined freeform instructions for persona personalization.
    /// Note: Under the SPARK security model, custom instructions can never override security boundaries.
    /// </summary>
    public string? CustomInstructions { get; init; }

    /// <summary>
    /// Explicit behavioral directives the agent must adhere to.
    /// </summary>
    public IReadOnlyList<string> BehavioralRules { get; init; } = new[]
    {
        "Always explain actions clearly before executing modifications.",
        "Provide verifiable facts with citations or sources when researching.",
        "Maintain confidentiality and never transmit sensitive local data to unauthorized hosts."
    };

    /// <summary>
    /// Prohibited actions that the agent must strictly refuse.
    /// </summary>
    public IReadOnlyList<string> ProhibitedActions { get; init; } = new[]
    {
        "Never execute unapproved file deletions outside designated temp locations.",
        "Never disclose credentials, API keys, or private tokens in plain text.",
        "Never approve or self-sign human-in-the-loop permission requests.",
        "Never bypass permission gates or sandbox constraints."
    };

    /// <summary>
    /// Tool categories the agent is authorized to leverage.
    /// </summary>
    public IReadOnlyList<string> AllowedToolCategories { get; init; } = new[]
    {
        "browser",
        "file",
        "development",
        "system",
        "audio",
        "mcp"
    };

    /// <summary>
    /// Enforces human-in-the-loop approval before executing high-impact tools.
    /// </summary>
    public bool RequireApprovalForHighImpactTools { get; init; } = true;

    public DateTimeOffset LastModified { get; init; } = DateTimeOffset.UtcNow;
}
