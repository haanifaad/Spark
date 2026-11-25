using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OpenClaw.Shared.DecisionIntelligence;

/// <summary>
/// Status of the Laya runtime lifecycle.
/// </summary>
public enum LayaRuntimeStatus
{
    NotInstalled = 0,
    CompatibilityChecking = 1,
    Compatible = 2,
    Incompatible = 3,
    Downloading = 4,
    DownloadPaused = 5,
    Installing = 6,
    Verifying = 7,
    Starting = 8,
    InstalledDisabled = 9,
    EnabledOperational = 10,
    Updating = 11,
    UpdateFailed = 12,
    RuntimeError = 13,
    Uninstalling = 14
}

/// <summary>
/// Question types supported by the non-autoregressive decision engine.
/// </summary>
public enum LayaQuestionKind
{
    Choice = 0,
    Score = 1,
    Noul = 2
}

/// <summary>
/// Specification for an individual decision question passed to Laya.
/// </summary>
public sealed class LayaQuestion
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("kind")]
    public LayaQuestionKind Kind { get; set; } = LayaQuestionKind.Choice;

    [JsonPropertyName("instruction")]
    public string Instruction { get; set; } = string.Empty;

    [JsonPropertyName("options")]
    public List<string> Options { get; set; } = new();

    [JsonPropertyName("minScore")]
    public double? MinScore { get; set; }

    [JsonPropertyName("maxScore")]
    public double? MaxScore { get; set; }

    [JsonPropertyName("confidenceThreshold")]
    public double? ConfidenceThreshold { get; set; }
}

/// <summary>
/// Structured result returned from evaluating an individual question.
/// </summary>
public sealed class LayaAnswer
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public LayaQuestionKind Kind { get; set; }

    [JsonPropertyName("selectedOption")]
    public string? SelectedOption { get; set; }

    [JsonPropertyName("numericScore")]
    public double? NumericScore { get; set; }

    [JsonPropertyName("booleanDecision")]
    public bool? BooleanDecision { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("isAbstention")]
    public bool IsAbstention { get; set; }

    [JsonPropertyName("requiresReview")]
    public bool RequiresReview { get; set; }

    [JsonPropertyName("probabilities")]
    public Dictionary<string, double> Probabilities { get; set; } = new();
}

/// <summary>
/// Container for batch decision intelligence results.
/// </summary>
public sealed class DecisionIntelligenceResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("modelId")]
    public string ModelId { get; set; } = string.Empty;

    [JsonPropertyName("executionTimeMs")]
    public double ExecutionTimeMs { get; set; }

    [JsonPropertyName("answers")]
    public Dictionary<string, LayaAnswer> Answers { get; set; } = new();

    [JsonPropertyName("isFallback")]
    public bool IsFallback { get; set; }

    [JsonPropertyName("fallbackReason")]
    public string? FallbackReason { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    public LayaAnswer? this[string id] =>
        Answers.TryGetValue(id, out var answer) ? answer : null;
}

/// <summary>
/// Metadata for a verified Laya model checkpoint.
/// </summary>
public sealed record LayaModelDescriptor(
    string ModelId,
    string DisplayName,
    string CheckpointVariant, // "english" | "multilingual" | "typed-decisions"
    string BaseArchitecture,
    int ContextWindowTokens,
    long DownloadSizeBytes,
    long ExtractedSizeBytes,
    long MinimumRamBytes,
    string Sha256Checksum,
    string HuggingFaceRepo,
    string Description,
    bool IsRecommended = false);

/// <summary>
/// Hardware compatibility diagnostics report for Laya execution.
/// </summary>
public sealed class LayaCompatibilityReport
{
    public bool IsCompatible { get; set; }
    public string OperatingSystem { get; set; } = "Windows";
    public string Architecture { get; set; } = "x64";
    public bool CpuSupported { get; set; }
    public string CpuName { get; set; } = string.Empty;
    public long TotalRamBytes { get; set; }
    public long AvailableRamBytes { get; set; }
    public bool RamSufficient { get; set; }
    public string GpuName { get; set; } = string.Empty;
    public bool HasGpuAcceleration { get; set; }
    public string AccelerationType { get; set; } = "CPU"; // "CUDA", "DirectML", or "CPU"
    public long AvailableDiskSpaceBytes { get; set; }
    public bool DiskSpaceSufficient { get; set; }
    public string RecommendedModelId { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new();
    public List<string> IncompatibilityReasons { get; set; } = new();
}

/// <summary>
/// Download and installation progress payload.
/// </summary>
public sealed class LayaProgressReport
{
    public string Stage { get; set; } = string.Empty;
    public double Percentage { get; set; }
    public long BytesTransferred { get; set; }
    public long TotalBytesExpected { get; set; }
    public double TransferRateMbps { get; set; }
    public string Details { get; set; } = string.Empty;
}
