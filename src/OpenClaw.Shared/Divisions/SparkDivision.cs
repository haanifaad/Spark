using System;
using System.Collections.Generic;

namespace OpenClaw.Shared.Divisions;

/// <summary>
/// Identifies the five primary architectural divisions of SPARK.
/// </summary>
public enum SparkDivision
{
    /// <summary>
    /// Standard conversational AI workspace.
    /// </summary>
    Chat,

    /// <summary>
    /// Productivity workspace housing Workboard, Worktrees, Automations, Dashboards, and Sessions.
    /// </summary>
    Work,

    /// <summary>
    /// Dedicated development workspace architected for OpenCode and Hermes Agent.
    /// </summary>
    Code,

    /// <summary>
    /// Dedicated learning, research, reading, and knowledge synthesis workspace.
    /// </summary>
    Study,

    /// <summary>
    /// Dedicated generative and creative application workspace.
    /// </summary>
    Creative
}

/// <summary>
/// Metadata describing a SPARK division, its route identity, and boundary status.
/// </summary>
public sealed record DivisionMetadata(
    SparkDivision Division,
    string Key,
    string DisplayName,
    string Description,
    string DefaultRoute,
    string Icon,
    bool IsActive)
{
    public override string ToString() => DisplayName;
}

/// <summary>
/// Registry catalog for the five primary architectural divisions of SPARK.
/// </summary>
public static class DivisionCatalog
{
    public static DivisionMetadata Chat { get; } = new(
        SparkDivision.Chat,
        "chat",
        "Chat",
        "Standard conversational AI workspace for interactive reasoning and execution.",
        "chat",
        "Chat",
        IsActive: true);

    public static DivisionMetadata Work { get; } = new(
        SparkDivision.Work,
        "work",
        "Work",
        "Productivity workspace housing Workboard, Worktrees, Automations, Dashboards, and Sessions.",
        "work",
        "Workspace",
        IsActive: true);

    public static DivisionMetadata Code { get; } = new(
        SparkDivision.Code,
        "code",
        "Code",
        "Dedicated development workspace with autonomous coding, planning, debugging, and testing engines.",
        "code",
        "Code",
        IsActive: true);

    public static DivisionMetadata Study { get; } = new(
        SparkDivision.Study,
        "study",
        "Study",
        "Dedicated learning, research, reading, and knowledge synthesis workspace.",
        "study",
        "Book",
        IsActive: false);

    public static DivisionMetadata Creative { get; } = new(
        SparkDivision.Creative,
        "creative",
        "Creative",
        "Dedicated generative studio and creative application workspace.",
        "creative",
        "Wand",
        IsActive: false);

    public static IReadOnlyList<DivisionMetadata> AllDivisions { get; } =
    [
        Chat,
        Work,
        Code,
        Study,
        Creative
    ];

    public static DivisionMetadata GetMetadata(SparkDivision division) => division switch
    {
        SparkDivision.Chat => Chat,
        SparkDivision.Work => Work,
        SparkDivision.Code => Code,
        SparkDivision.Study => Study,
        SparkDivision.Creative => Creative,
        _ => throw new ArgumentOutOfRangeException(nameof(division), division, null)
    };

    public static bool TryGetDivision(string? key, out SparkDivision division)
    {
        switch (key?.ToLowerInvariant())
        {
            case "chat":
            case "home":
                division = SparkDivision.Chat;
                return true;
            case "work":
            case "workspace":
            case "workboard":
            case "worktrees":
            case "cron":
            case "dashboards":
            case "sessions":
                division = SparkDivision.Work;
                return true;
            case "code":
                division = SparkDivision.Code;
                return true;
            case "study":
                division = SparkDivision.Study;
                return true;
            case "creative":
                division = SparkDivision.Creative;
                return true;
            default:
                division = default;
                return false;
        }
    }
}
