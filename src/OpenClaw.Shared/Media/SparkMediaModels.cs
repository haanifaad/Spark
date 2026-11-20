namespace OpenClaw.Shared.Media;

/// <summary>
/// Media classification for multimodal processing and analysis.
/// </summary>
public enum SparkMediaKind
{
    AudioVoice,
    Image,
    Document,
    Video
}

/// <summary>
/// Request for processing media attachments, voice inputs, or documents.
/// </summary>
public sealed record SparkMediaProcessingRequest
{
    public required SparkMediaKind MediaKind { get; init; }
    public string? FilePath { get; init; }
    public byte[]? RawBytes { get; init; }
    public string MimeType { get; init; } = "application/octet-stream";
    public string? UserPrompt { get; init; }
    public string SourceLanguage { get; init; } = "auto";
    public string SessionId { get; init; } = "default_session";
    public bool ExecuteAsVoiceTask { get; init; }
    public bool SynthesizeVoiceResponse { get; init; }
}

/// <summary>
/// Comprehensive result of multimodal processing, transcription, or voice execution.
/// </summary>
public sealed record SparkMediaProcessingResult
{
    public bool Success { get; init; }
    public string? TranscribedText { get; init; }
    public string? ExtractedText { get; init; }
    public string? AnalysisSummary { get; init; }
    public byte[]? GeneratedAudioBytes { get; init; }
    public string? ExecutedTaskResult { get; init; }
    public TimeSpan Duration { get; init; }
    public string? ErrorMessage { get; init; }
}
