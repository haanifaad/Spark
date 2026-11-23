namespace OpenClaw.Shared.Verification;

/// <summary>
/// Evidentiary classification of information or task outcomes to prevent hallucinations.
/// </summary>
public enum SparkEvidenceCategory
{
    Verified = 0,
    ProviderReturned = 1,
    SourceSupported = 2,
    ModelInference = 3,
    UnverifiedAssumption = 4
}

/// <summary>
/// Calibrated confidence rating for proposed facts or completed operations.
/// </summary>
public enum SparkConfidenceLevel
{
    Low = 0,
    Medium = 1,
    High = 2
}

/// <summary>
/// Source attribution record citing real-world documentation, links, and providers.
/// </summary>
public sealed class SparkSourceCitation
{
    public required string Title { get; init; }
    public required string Url { get; init; }
    public string? Provider { get; init; }
    public DateTimeOffset RetrievalTime { get; init; } = DateTimeOffset.UtcNow;
    public string? SupportingSnippet { get; init; }
}

/// <summary>
/// Structured verification report validating real-world execution success.
/// </summary>
public sealed class SparkVerificationReport
{
    public required string TargetEntityId { get; init; }
    public required bool IsVerifiedSuccessful { get; init; }
    public required SparkEvidenceCategory EvidenceCategory { get; init; }
    public required SparkConfidenceLevel Confidence { get; init; }
    public string? VerifiedProviderReference { get; init; }
    public List<SparkSourceCitation> Citations { get; init; } = [];
    public List<string> VerificationNotes { get; init; } = [];
    public DateTimeOffset VerifiedAt { get; init; } = DateTimeOffset.UtcNow;
}
