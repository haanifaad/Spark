using System.Text;
using OpenClaw.Shared.Audio;
using OpenClaw.Shared.Inference.Routing;
using OpenClaw.Shared.Media;
using OpenClaw.Shared.Security;
using Xunit;

namespace OpenClaw.Shared.Tests;

public sealed class SparkMediaProcessorTests
{
    [Fact]
    public async Task TranscribeAudio_EnforcesMicrophonePrivacyPermission()
    {
        var privacyService = new SparkVoicePrivacyService();
        privacyService.SetPermission(SparkMediaDeviceType.Microphone, SparkPermissionState.Denied);

        var sttMock = new FakeSpeechToTextService("Hello world");
        var processor = new SparkMediaProcessor(sttMock, privacyService: privacyService);

        var dummyAudio = new byte[320]; // 10ms of 16kHz audio

        await Assert.ThrowsAsync<SparkPrivacyException>(async () =>
        {
            await processor.TranscribeAudioAsync(dummyAudio);
        });

        // Grant permission and verify it passes
        privacyService.SetPermission(SparkMediaDeviceType.Microphone, SparkPermissionState.Granted);
        var transcript = await processor.TranscribeAudioAsync(dummyAudio);
        Assert.Equal("Hello world", transcript);
    }

    [Fact]
    public async Task SynthesizeSpeech_GeneratesValidWavFileHeader()
    {
        var processor = new SparkMediaProcessor();
        var wavBytes = await processor.SynthesizeSpeechAsync("This is a synthesized test message.");

        Assert.NotNull(wavBytes);
        Assert.True(wavBytes.Length > 44);

        // Verify RIFF and WAVE magic bytes
        var riff = Encoding.ASCII.GetString(wavBytes, 0, 4);
        var wave = Encoding.ASCII.GetString(wavBytes, 8, 4);
        var fmt = Encoding.ASCII.GetString(wavBytes, 12, 4);

        Assert.Equal("RIFF", riff);
        Assert.Equal("WAVE", wave);
        Assert.Equal("fmt ", fmt);
    }

    [Fact]
    public async Task ProcessMedia_ImageAnalysis_RoutesToVisionModel()
    {
        var router = new SparkModelRouter();
        var processor = new SparkMediaProcessor(modelRouter: router);

        var imageBytes = new byte[1024]; // 1KB dummy image
        var request = new SparkMediaProcessingRequest
        {
            MediaKind = SparkMediaKind.Image,
            RawBytes = imageBytes,
            UserPrompt = "Identify the diagrams in this image",
            SessionId = "session_vision_1"
        };

        var result = await processor.ProcessMediaAsync(request);

        Assert.True(result.Success);
        Assert.NotNull(result.AnalysisSummary);
        Assert.Contains("Identify the diagrams in this image", result.AnalysisSummary);
    }

    [Fact]
    public async Task ProcessMedia_ImageExceedingSizeLimit_ReturnsError()
    {
        var processor = new SparkMediaProcessor();
        var largeImageBytes = new byte[21 * 1024 * 1024]; // 21MB, exceeds 20MB cap

        var request = new SparkMediaProcessingRequest
        {
            MediaKind = SparkMediaKind.Image,
            RawBytes = largeImageBytes
        };

        var result = await processor.ProcessMediaAsync(request);

        Assert.False(result.Success);
        Assert.Contains("20MB", result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessMedia_DocumentAnalysis_ExtractsContentAndSummarizes()
    {
        var router = new SparkModelRouter();
        var processor = new SparkMediaProcessor(modelRouter: router);

        var documentContent = "Architecture RFC: SPARK Multi-Model Routing Strategy\n1. Local Models for privacy.\n2. Cloud models for heavy reasoning.";
        var docBytes = Encoding.UTF8.GetBytes(documentContent);

        var request = new SparkMediaProcessingRequest
        {
            MediaKind = SparkMediaKind.Document,
            RawBytes = docBytes,
            UserPrompt = "Extract key architectural principles"
        };

        var result = await processor.ProcessMediaAsync(request);

        Assert.True(result.Success);
        Assert.Equal(documentContent, result.ExtractedText);
        Assert.NotNull(result.AnalysisSummary);
        Assert.Contains("Extract key architectural principles", result.AnalysisSummary);
    }

    [Fact]
    public async Task ExecuteVoiceTask_EndToEnd_TranscribesRoutesAndSynthesizesResponse()
    {
        var sttMock = new FakeSpeechToTextService("Summarize project status");
        var router = new SparkModelRouter();
        var privacyService = new SparkVoicePrivacyService();
        privacyService.SetPermission(SparkMediaDeviceType.Microphone, SparkPermissionState.Granted);

        var processor = new SparkMediaProcessor(sttMock, router, privacyService);

        var dummyAudio = new byte[640];
        var result = await processor.ExecuteVoiceTaskAsync(dummyAudio, "voice_session_77");

        Assert.True(result.Success);
        Assert.Equal("Summarize project status", result.TranscribedText);
        Assert.NotNull(result.ExecutedTaskResult);
        Assert.NotNull(result.GeneratedAudioBytes);
        Assert.True(result.GeneratedAudioBytes.Length > 44);
    }

    private sealed class FakeSpeechToTextService : ISpeechToTextService
    {
        private readonly string _transcript;

        public bool IsModelLoaded => true;

        public FakeSpeechToTextService(string transcript)
        {
            _transcript = transcript;
        }

        public void LoadModel(string modelPath) { }

        public Task<List<TranscriptionResult>> TranscribeAsync(float[] samples, string language = "auto", CancellationToken cancellationToken = default)
        {
            var list = new List<TranscriptionResult>
            {
                new TranscriptionResult
                {
                    Text = _transcript,
                    Start = TimeSpan.Zero,
                    End = TimeSpan.FromSeconds(1),
                    Language = language
                }
            };
            return Task.FromResult(list);
        }

        public void Dispose() { }
    }
}
