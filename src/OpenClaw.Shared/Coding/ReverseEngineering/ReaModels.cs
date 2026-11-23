using System;
using System.Collections.Generic;

namespace OpenClaw.Shared.Coding.ReverseEngineering;

/// <summary>
/// Categories of targets inspectable by the Reverse Engineering engine.
/// </summary>
public enum TargetCategory
{
    NativeBinary,
    NetAssembly,
    WebApplication,
    ElectronAsar,
    AndroidApk
}

/// <summary>
/// Verified piece of evidence extracted during reverse engineering analysis.
/// Matches Rea's evidence and limitations architecture.
/// </summary>
public sealed record ReaEvidenceRecord
{
    public required string Id { get; init; }
    public required string Target { get; init; }
    public required string Category { get; init; } // "Decompilation", "API Surface", "State Model", "Xref"
    public string Confidence { get; init; } = "High";
    public required string Summary { get; init; }
    public string? CodeSnippet { get; init; }
    public string? AssemblyDisassembly { get; init; }
    public IReadOnlyList<string> CallingXrefs { get; init; } = Array.Empty<string>();
    public string Limitations { get; init; } = "Static analysis only; obfuscated branches may be unverified.";
}

/// <summary>
/// Complete reverse engineering inspection report and clean-room architecture specification.
/// </summary>
public sealed record ReaInspectionResult
{
    public required string TargetName { get; init; }
    public required string TargetPath { get; init; }
    public TargetCategory Category { get; init; }
    public string Architecture { get; set; } = "x64";
    public IReadOnlyList<string> DiscoveredEndpoints { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ExtractedLibraries { get; set; } = Array.Empty<string>();
    public List<ReaEvidenceRecord> Evidence { get; init; } = new();
    public string CleanRoomSpecification { get; set; } = string.Empty;
    public DateTimeOffset InspectedAt { get; init; } = DateTimeOffset.UtcNow;
}
