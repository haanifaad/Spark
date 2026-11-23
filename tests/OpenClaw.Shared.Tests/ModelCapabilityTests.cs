using System;
using System.IO;
using OpenClaw.Shared.Inference;
using Xunit;

namespace OpenClaw.Shared.Tests;

public sealed class ModelCapabilityTests
{
    [Fact]
    public void DetectCapabilities_DetectsLocalInference()
    {
        var caps = ModelCapabilityMatrix.DetectCapabilities(
            "qwen2.5-coder:7b",
            provider: "ollama",
            contextTokens: 24576,
            reasoningFlag: false,
            visionFlag: false,
            toolsFlag: true);

        Assert.True(ModelCapabilityMatrix.HasCapability(caps, ModelCapability.LocalInference));
        Assert.True(ModelCapabilityMatrix.HasCapability(caps, ModelCapability.Coding));
        Assert.True(ModelCapabilityMatrix.HasCapability(caps, ModelCapability.ToolCalling));
        Assert.False(ModelCapabilityMatrix.HasCapability(caps, ModelCapability.Reasoning));
        Assert.False(ModelCapabilityMatrix.HasCapability(caps, ModelCapability.Vision));
    }

    [Fact]
    public void DetectCapabilities_DetectsReasoningAndMath()
    {
        var caps = ModelCapabilityMatrix.DetectCapabilities(
            "deepseek-r1:32b",
            provider: "ollama",
            contextTokens: 32768,
            reasoningFlag: true,
            visionFlag: false,
            toolsFlag: true);

        Assert.True(ModelCapabilityMatrix.HasCapability(caps, ModelCapability.Reasoning));
        Assert.True(ModelCapabilityMatrix.HasCapability(caps, ModelCapability.Mathematics));
        Assert.True(ModelCapabilityMatrix.HasCapability(caps, ModelCapability.LocalInference));
    }

    [Fact]
    public void DetectCapabilities_DetectsVisionAndMultimodal()
    {
        var caps = ModelCapabilityMatrix.DetectCapabilities(
            "gpt-4o",
            provider: "openai",
            contextTokens: 128000,
            reasoningFlag: false,
            visionFlag: true,
            toolsFlag: true);

        Assert.True(ModelCapabilityMatrix.HasCapability(caps, ModelCapability.Vision));
        Assert.True(ModelCapabilityMatrix.HasCapability(caps, ModelCapability.Multimodal));
        Assert.True(ModelCapabilityMatrix.HasCapability(caps, ModelCapability.LongContext));
        Assert.False(ModelCapabilityMatrix.HasCapability(caps, ModelCapability.LocalInference));
    }

    [Fact]
    public void DetectCapabilities_DetectsFastAndLongContext()
    {
        var caps = ModelCapabilityMatrix.DetectCapabilities(
            "claude-3-5-haiku",
            provider: "anthropic",
            contextTokens: 200000,
            reasoningFlag: false,
            visionFlag: true,
            toolsFlag: true);

        Assert.True(ModelCapabilityMatrix.HasCapability(caps, ModelCapability.FastResponse));
        Assert.True(ModelCapabilityMatrix.HasCapability(caps, ModelCapability.LongContext));
    }

    [Fact]
    public void PreferencesStore_TracksFavoritesAndRecents()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"spark_prefs_{Guid.NewGuid():N}.json");
        try
        {
            var store = new ChatModelPreferencesStore(tempFile);

            Assert.False(store.IsFavorite("ollama/qwen2.5-coder:7b"));
            store.ToggleFavorite("ollama/qwen2.5-coder:7b");
            Assert.True(store.IsFavorite("ollama/qwen2.5-coder:7b"));

            store.RecordRecent("ollama/qwen2.5-coder:7b");
            store.RecordRecent("openai/gpt-4o");

            var recents = store.GetRecents();
            Assert.Equal(2, recents.Count);
            Assert.Equal("openai/gpt-4o", recents[0]);
            Assert.Equal("ollama/qwen2.5-coder:7b", recents[1]);

            // Untoggle
            store.ToggleFavorite("ollama/qwen2.5-coder:7b");
            Assert.False(store.IsFavorite("ollama/qwen2.5-coder:7b"));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
