using System;
using System.Collections.Generic;

namespace OpenClaw.Shared.Inference;

/// <summary>
/// Capability flags for AI models in the SPARK catalog.
/// </summary>
[Flags]
public enum ModelCapability
{
    None = 0,
    General = 1 << 0,
    Reasoning = 1 << 1,
    Coding = 1 << 2,
    Mathematics = 1 << 3,
    Vision = 1 << 4,
    Multimodal = 1 << 5,
    ToolCalling = 1 << 6,
    FastResponse = 1 << 7,
    LongContext = 1 << 8,
    LocalInference = 1 << 9,
    AudioInput = 1 << 10,
    AudioOutput = 1 << 11,
    RealtimeLive = 1 << 12
}

/// <summary>
/// Verified capability analyzer and metadata evaluator for AI models.
/// Inspects model IDs, provider types, and gateway metadata without inventing unsupported features.
/// </summary>
public static class ModelCapabilityMatrix
{
    public static ModelCapability DetectCapabilities(
        string modelId,
        string? provider,
        int? contextTokens,
        bool? reasoningFlag,
        bool? visionFlag,
        bool? toolsFlag)
    {
        var caps = ModelCapability.General;
        var id = modelId ?? string.Empty;
        var prov = provider ?? string.Empty;

        // Local inference
        if (string.Equals(prov, "local", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(prov, "ollama", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(prov, "llama", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("gguf", StringComparison.OrdinalIgnoreCase))
        {
            caps |= ModelCapability.LocalInference;
        }

        // Reasoning / Thinking
        if (reasoningFlag == true ||
            id.Contains("deepseek-r1", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("o1", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("o3", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("reason", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("thinking", StringComparison.OrdinalIgnoreCase))
        {
            caps |= ModelCapability.Reasoning;
        }

        // Coding
        if (id.Contains("coder", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("code", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("dev", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("sonnet", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("4o", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("opus", StringComparison.OrdinalIgnoreCase))
        {
            caps |= ModelCapability.Coding;
        }

        // Mathematics
        if (id.Contains("math", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("deepseek-r1", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("o1", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("o3", StringComparison.OrdinalIgnoreCase))
        {
            caps |= ModelCapability.Mathematics;
        }

        // Vision & Multimodal
        if (visionFlag == true ||
            id.Contains("vision", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("vl", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("omni", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("4o", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("flash", StringComparison.OrdinalIgnoreCase) && prov.Contains("google", StringComparison.OrdinalIgnoreCase))
        {
            caps |= ModelCapability.Vision | ModelCapability.Multimodal;
        }

        // Tool calling
        if (toolsFlag != false &&
            !id.Contains("base", StringComparison.OrdinalIgnoreCase) &&
            !id.Contains("text-only", StringComparison.OrdinalIgnoreCase))
        {
            caps |= ModelCapability.ToolCalling;
        }

        // Fast response
        if (id.Contains("flash", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("mini", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("haiku", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("turbo", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("small", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("1.5b", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("3b", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("7b", StringComparison.OrdinalIgnoreCase) && (caps & ModelCapability.LocalInference) != 0)
        {
            caps |= ModelCapability.FastResponse;
        }

        // Long context (>= 64k tokens)
        if ((contextTokens ?? 0) >= 64_000 ||
            id.Contains("128k", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("1m", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("gemini", StringComparison.OrdinalIgnoreCase))
        {
            caps |= ModelCapability.LongContext;
        }

        // Audio Input
        if (id.Contains("realtime", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("audio", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("live", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("whisper", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("omni", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("gemini-2.0", StringComparison.OrdinalIgnoreCase))
        {
            caps |= ModelCapability.AudioInput;
        }

        // Audio Output (Speech synthesis or native audio tokens)
        if (id.Contains("realtime", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("audio", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("live", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("tts", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("omni", StringComparison.OrdinalIgnoreCase))
        {
            caps |= ModelCapability.AudioOutput;
        }

        // Realtime Live bidirectional streaming
        if (id.Contains("realtime", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("live", StringComparison.OrdinalIgnoreCase))
        {
            caps |= ModelCapability.RealtimeLive;
        }

        return caps;
    }

    public static bool HasCapability(ModelCapability targetCaps, ModelCapability required)
    {
        if (required == ModelCapability.None) return true;
        return (targetCaps & required) == required;
    }

    public static IReadOnlyList<string> GetCapabilityLabels(ModelCapability caps)
    {
        var labels = new List<string>();
        if ((caps & ModelCapability.LocalInference) != 0) labels.Add("Local");
        if ((caps & ModelCapability.Reasoning) != 0) labels.Add("Reasoning");
        if ((caps & ModelCapability.Coding) != 0) labels.Add("Coding");
        if ((caps & ModelCapability.Mathematics) != 0) labels.Add("Math");
        if ((caps & ModelCapability.Vision) != 0) labels.Add("Vision");
        if ((caps & ModelCapability.AudioInput) != 0) labels.Add("Audio");
        if ((caps & ModelCapability.RealtimeLive) != 0) labels.Add("Live");
        if ((caps & ModelCapability.ToolCalling) != 0) labels.Add("Tools");
        if ((caps & ModelCapability.FastResponse) != 0) labels.Add("Fast");
        if ((caps & ModelCapability.LongContext) != 0) labels.Add("Long Context");
        return labels;
    }

    public static string FormatCapabilities(ModelCapability caps) =>
        string.Join(" \u00b7 ", GetCapabilityLabels(caps));
}
