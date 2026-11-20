namespace OpenClaw.Shared.Media;

/// <summary>
/// Unified media processor for voice transcription, TTS, image analysis, document extraction,
/// and end-to-end voice-to-agent task execution.
/// </summary>
public interface ISparkMediaProcessor
{
    Task<SparkMediaProcessingResult> ProcessMediaAsync(SparkMediaProcessingRequest request, CancellationToken cancellationToken = default);
    Task<string> TranscribeAudioAsync(byte[] audioBytes, string language = "auto", CancellationToken cancellationToken = default);
    Task<byte[]> SynthesizeSpeechAsync(string text, string voice = "default", CancellationToken cancellationToken = default);
    Task<SparkMediaProcessingResult> ExecuteVoiceTaskAsync(byte[] voiceAudioBytes, string sessionId, CancellationToken cancellationToken = default);
}
