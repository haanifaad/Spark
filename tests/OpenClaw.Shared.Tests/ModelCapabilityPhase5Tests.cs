using OpenClaw.Chat;
using OpenClaw.Shared.Inference;
using Xunit;

namespace OpenClaw.Shared.Tests;

public sealed class ModelCapabilityPhase5Tests
{
    [Fact]
    public void GeminiRealtimeModel_DetectsAudioAndRealtimeCapabilities()
    {
        var caps = ModelCapabilityMatrix.DetectCapabilities(
            modelId: "gemini-2.0-flash-realtime",
            provider: "google",
            contextTokens: 128_000,
            reasoningFlag: false,
            visionFlag: true,
            toolsFlag: true);

        Assert.True((caps & ModelCapability.AudioInput) != 0);
        Assert.True((caps & ModelCapability.AudioOutput) != 0);
        Assert.True((caps & ModelCapability.RealtimeLive) != 0);
        Assert.True((caps & ModelCapability.Vision) != 0);
        Assert.True((caps & ModelCapability.ToolCalling) != 0);
    }

    [Fact]
    public void Gpt4oRealtime_DetectsAudioAndLiveCapabilities()
    {
        var caps = ModelCapabilityMatrix.DetectCapabilities(
            modelId: "gpt-4o-realtime-preview",
            provider: "openai",
            contextTokens: 128_000,
            reasoningFlag: false,
            visionFlag: true,
            toolsFlag: true);

        Assert.True((caps & ModelCapability.AudioInput) != 0);
        Assert.True((caps & ModelCapability.AudioOutput) != 0);
        Assert.True((caps & ModelCapability.RealtimeLive) != 0);
    }

    [Fact]
    public void StandardTextAndCodingModels_DoNotInventAudioOrLiveCapabilities()
    {
        var coderCaps = ModelCapabilityMatrix.DetectCapabilities(
            modelId: "qwen2.5-coder:7b",
            provider: "ollama",
            contextTokens: 32_000,
            reasoningFlag: false,
            visionFlag: false,
            toolsFlag: true);

        Assert.False((coderCaps & ModelCapability.AudioInput) != 0);
        Assert.False((coderCaps & ModelCapability.AudioOutput) != 0);
        Assert.False((coderCaps & ModelCapability.RealtimeLive) != 0);
        Assert.True((coderCaps & ModelCapability.Coding) != 0);
        Assert.True((coderCaps & ModelCapability.LocalInference) != 0);

        var reasoningCaps = ModelCapabilityMatrix.DetectCapabilities(
            modelId: "deepseek-r1:32b",
            provider: "ollama",
            contextTokens: 64_000,
            reasoningFlag: true,
            visionFlag: false,
            toolsFlag: false);

        Assert.False((reasoningCaps & ModelCapability.AudioInput) != 0);
        Assert.False((reasoningCaps & ModelCapability.AudioOutput) != 0);
        Assert.False((reasoningCaps & ModelCapability.RealtimeLive) != 0);
        Assert.True((reasoningCaps & ModelCapability.Reasoning) != 0);
    }

    [Fact]
    public void ChatModelChoice_ExposesPhase5AudioProperties()
    {
        var realtimeChoice = new ChatModelChoice(
            Id: "gemini-2.0-flash-realtime",
            DisplayName: "Gemini 2.0 Flash Realtime",
            Provider: "google");

        Assert.True(realtimeChoice.IsAudioInput);
        Assert.True(realtimeChoice.IsAudioOutput);
        Assert.True(realtimeChoice.IsRealtimeLive);

        var standardChoice = new ChatModelChoice(
            Id: "claude-sonnet-4.6",
            DisplayName: "Claude 3.7 Sonnet",
            Provider: "anthropic");

        Assert.False(standardChoice.IsAudioInput);
        Assert.False(standardChoice.IsAudioOutput);
        Assert.False(standardChoice.IsRealtimeLive);
    }
}
