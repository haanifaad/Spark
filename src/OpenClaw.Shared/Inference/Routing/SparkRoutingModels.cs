using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OpenClaw.Shared.Inference.Routing;

/// <summary>
/// Task classifications for capability-based intelligence routing.
/// </summary>
public enum SparkTaskCategory
{
    SimpleConversation = 0,
    ComplexReasoning = 1,
    Coding = 2,
    Mathematics = 3,
    Research = 4,
    ImageUnderstanding = 5,
    Audio = 6,
    DocumentAnalysis = 7,
    ToolOrchestration = 8
}

/// <summary>
/// Privacy modes governing whether data may leave the local host.
/// </summary>
public enum SparkModelPrivacyMode
{
    /// <summary>Never transmit private data to cloud models under any circumstances.</summary>
    StrictLocalOnly = 0,

    /// <summary>Prefer local models if capable; fall back to cloud only when allowed.</summary>
    PreferLocal = 1,

    /// <summary>Select the most cost-effective and capable model across local and cloud.</summary>
    Balanced = 2,

    /// <summary>Prioritize maximum quality and capability regardless of provider.</summary>
    PerformanceFirst = 3
}

/// <summary>
/// Supported inference model providers.
/// </summary>
public enum SparkModelProvider
{
    Local = 0,
    Anthropic = 1,
    OpenAI = 2,
    Google = 3,
    DeepSeek = 4,
    Ollama = 5,
    OpenRouter = 6,
    Custom = 7
}

/// <summary>
/// Detailed specification of an available AI model.
/// </summary>
public class SparkModelDescriptor
{
    [JsonPropertyName("modelId")]
    public string ModelId { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("provider")]
    public SparkModelProvider Provider { get; set; } = SparkModelProvider.Local;

    [JsonPropertyName("capabilities")]
    public ModelCapability Capabilities { get; set; } = ModelCapability.General;

    [JsonPropertyName("contextWindowTokens")]
    public int ContextWindowTokens { get; set; } = 32_768;

    [JsonPropertyName("isLocal")]
    public bool IsLocal { get; set; }

    [JsonPropertyName("isAvailable")]
    public bool IsAvailable { get; set; } = true;

    [JsonPropertyName("costPer1kTokens")]
    public decimal CostPer1kTokens { get; set; }

    [JsonPropertyName("averageLatencyMs")]
    public int AverageLatencyMs { get; set; } = 500;

    [JsonPropertyName("maxOutputTokens")]
    public int MaxOutputTokens { get; set; } = 4_096;

    [JsonPropertyName("priorityWeight")]
    public double PriorityWeight { get; set; } = 1.0;
}

/// <summary>
/// Request to select the optimal model based on task demands and constraints.
/// </summary>
public class SparkRoutingRequest
{
    public SparkTaskCategory TaskCategory { get; set; } = SparkTaskCategory.SimpleConversation;

    public ModelCapability RequiredCapabilities { get; set; } = ModelCapability.None;

    public int? MinimumContextTokens { get; set; }

    public int? PromptTokenEstimate { get; set; }

    public SparkModelPrivacyMode PrivacyMode { get; set; } = SparkModelPrivacyMode.Balanced;

    /// <summary>
    /// When true, strictly forbids routing to any non-local model regardless of PrivacyMode setting.
    /// </summary>
    public bool ContainsSensitiveLocalData { get; set; }

    /// <summary>
    /// Explicit user manual selection overriding automated selection.
    /// </summary>
    public string? UserManualModelOverride { get; set; }

    public decimal? MaxAcceptableCost { get; set; }

    public int? MaxAcceptableLatencyMs { get; set; }
}

/// <summary>
/// Output of routing logic containing chosen model, fallback hierarchy, and explanation.
/// </summary>
public class SparkRoutingDecision
{
    public SparkModelDescriptor SelectedModel { get; set; } = null!;

    public IReadOnlyList<SparkModelDescriptor> FallbackChain { get; set; } = Array.Empty<SparkModelDescriptor>();

    public string RoutingReason { get; set; } = string.Empty;

    public double RoutingScore { get; set; }

    public bool IsLocal => SelectedModel.IsLocal;

    public decimal EstimatedCost { get; set; }

    public int EstimatedLatencyMs => SelectedModel.AverageLatencyMs;
}

/// <summary>
/// Unified result of executing an inference invocation.
/// </summary>
public class SparkInferenceResult
{
    public bool Success { get; set; }
    public string? Content { get; set; }
    public string ModelUsed { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int AttemptCount { get; set; }
    public TimeSpan Duration { get; set; }
    public bool WasFallbackUsed { get; set; }
}
