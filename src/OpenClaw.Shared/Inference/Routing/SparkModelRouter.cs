using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Inference.Routing;

/// <summary>
/// Capability-based multi-model intelligence router.
/// Evaluates task demands, required capabilities, privacy constraints, latency, and costs.
/// </summary>
public class SparkModelRouter : ISparkModelRouter
{
    private readonly ConcurrentDictionary<string, SparkModelDescriptor> _models = new(StringComparer.OrdinalIgnoreCase);

    public SparkModelRouter(bool populateDefaultTiers = true)
    {
        if (populateDefaultTiers)
        {
            RegisterDefaultModelTiers();
        }
    }

    public void RegisterModel(SparkModelDescriptor model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (string.IsNullOrWhiteSpace(model.ModelId))
        {
            throw new ArgumentException("ModelId is required.", nameof(model));
        }

        _models[model.ModelId] = model;
    }

    public bool UnregisterModel(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId)) return false;
        return _models.TryRemove(modelId, out _);
    }

    public IReadOnlyList<SparkModelDescriptor> ListModels(bool availableOnly = true)
    {
        var list = _models.Values.AsEnumerable();
        if (availableOnly)
        {
            list = list.Where(m => m.IsAvailable);
        }
        return list.OrderBy(m => m.IsLocal ? 0 : 1).ThenBy(m => m.DisplayName).ToList();
    }

    public SparkModelDescriptor? GetModel(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId)) return null;
        return _models.TryGetValue(modelId, out var model) ? model : null;
    }

    public SparkRoutingDecision Route(SparkRoutingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Manual user override check
        if (!string.IsNullOrWhiteSpace(request.UserManualModelOverride))
        {
            if (_models.TryGetValue(request.UserManualModelOverride, out var manualModel))
            {
                // Strict privacy guardrail: user cannot force cloud model on sensitive local data
                if (request.ContainsSensitiveLocalData && !manualModel.IsLocal)
                {
                    throw new InvalidOperationException(
                        $"Manual selection of cloud model '{manualModel.DisplayName}' rejected: request contains sensitive local data.");
                }

                var fallbacks = _models.Values
                    .Where(m => m.IsAvailable && m.ModelId != manualModel.ModelId)
                    .Where(m => !request.ContainsSensitiveLocalData || m.IsLocal)
                    .OrderByDescending(m => ScoreModel(m, request))
                    .Take(3)
                    .ToList();

                return new SparkRoutingDecision
                {
                    SelectedModel = manualModel,
                    FallbackChain = fallbacks,
                    RoutingReason = $"Manual user override selected: {manualModel.DisplayName}.",
                    RoutingScore = 100.0,
                    EstimatedCost = manualModel.CostPer1kTokens * ((request.PromptTokenEstimate ?? 1000) / 1000m)
                };
            }
        }

        // 2. Derive task-specific required capabilities
        var requiredCaps = request.RequiredCapabilities | DeriveCategoryCapabilities(request.TaskCategory);

        // 3. Filter candidates based on availability, privacy, and context limits
        var candidates = _models.Values.Where(m => m.IsAvailable);

        if (request.ContainsSensitiveLocalData || request.PrivacyMode == SparkModelPrivacyMode.StrictLocalOnly)
        {
            candidates = candidates.Where(m => m.IsLocal);
        }

        if (request.MinimumContextTokens.HasValue)
        {
            candidates = candidates.Where(m => m.ContextWindowTokens >= request.MinimumContextTokens.Value);
        }

        if (request.MaxAcceptableCost.HasValue)
        {
            candidates = candidates.Where(m => m.CostPer1kTokens <= request.MaxAcceptableCost.Value);
        }

        if (request.MaxAcceptableLatencyMs.HasValue)
        {
            candidates = candidates.Where(m => m.AverageLatencyMs <= request.MaxAcceptableLatencyMs.Value);
        }

        var candidateList = candidates.ToList();

        if (candidateList.Count == 0)
        {
            if (request.ContainsSensitiveLocalData || request.PrivacyMode == SparkModelPrivacyMode.StrictLocalOnly)
            {
                throw new InvalidOperationException(
                    "Routing blocked: Request contains sensitive local data or requires StrictLocalOnly mode, but no suitable local model is available. Cloud transmission refused.");
            }

            throw new InvalidOperationException(
                $"No available model satisfies the requirements for task '{request.TaskCategory}'.");
        }

        // 4. Score and rank candidates
        var ranked = candidateList
            .Select(m => new { Model = m, Score = ScoreModel(m, request, requiredCaps) })
            .OrderByDescending(x => x.Score)
            .ToList();

        var top = ranked[0];
        var fallbackChain = ranked.Skip(1).Select(x => x.Model).Take(3).ToList();

        var reason = $"Auto-selected '{top.Model.DisplayName}' for {request.TaskCategory} (Score: {top.Score:0.#}, IsLocal: {top.Model.IsLocal}).";

        return new SparkRoutingDecision
        {
            SelectedModel = top.Model,
            FallbackChain = fallbackChain,
            RoutingReason = reason,
            RoutingScore = top.Score,
            EstimatedCost = top.Model.CostPer1kTokens * ((request.PromptTokenEstimate ?? 1000) / 1000m)
        };
    }

    public async Task<SparkInferenceResult> ExecuteWithFallbackAsync(
        SparkRoutingRequest request,
        Func<SparkModelDescriptor, CancellationToken, Task<string>> modelInvoker,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(modelInvoker);

        var decision = Route(request);
        var modelsToTry = new List<SparkModelDescriptor> { decision.SelectedModel };
        modelsToTry.AddRange(decision.FallbackChain);

        var sw = Stopwatch.StartNew();
        int attempts = 0;
        string? lastError = null;

        foreach (var model in modelsToTry)
        {
            attempts++;
            try
            {
                var content = await modelInvoker(model, cancellationToken).ConfigureAwait(false);
                sw.Stop();
                return new SparkInferenceResult
                {
                    Success = true,
                    Content = content,
                    ModelUsed = model.ModelId,
                    AttemptCount = attempts,
                    Duration = sw.Elapsed,
                    WasFallbackUsed = model.ModelId != decision.SelectedModel.ModelId
                };
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                lastError = ex.Message;
                // Continue to next model in fallback chain
            }
        }

        sw.Stop();
        return new SparkInferenceResult
        {
            Success = false,
            ErrorMessage = $"All {attempts} candidate model invocations failed. Last error: {lastError}",
            ModelUsed = decision.SelectedModel.ModelId,
            AttemptCount = attempts,
            Duration = sw.Elapsed,
            WasFallbackUsed = attempts > 1
        };
    }

    private static ModelCapability DeriveCategoryCapabilities(SparkTaskCategory category)
    {
        return category switch
        {
            SparkTaskCategory.SimpleConversation => ModelCapability.General | ModelCapability.FastResponse,
            SparkTaskCategory.ComplexReasoning => ModelCapability.Reasoning,
            SparkTaskCategory.Coding => ModelCapability.Coding | ModelCapability.ToolCalling,
            SparkTaskCategory.Mathematics => ModelCapability.Mathematics | ModelCapability.Reasoning,
            SparkTaskCategory.Research => ModelCapability.LongContext | ModelCapability.Reasoning,
            SparkTaskCategory.ImageUnderstanding => ModelCapability.Vision | ModelCapability.Multimodal,
            SparkTaskCategory.Audio => ModelCapability.AudioInput,
            SparkTaskCategory.DocumentAnalysis => ModelCapability.Vision | ModelCapability.LongContext,
            SparkTaskCategory.ToolOrchestration => ModelCapability.ToolCalling,
            _ => ModelCapability.General
        };
    }

    private static double ScoreModel(
        SparkModelDescriptor model,
        SparkRoutingRequest request,
        ModelCapability requiredCaps = ModelCapability.None)
    {
        double score = 50.0 * model.PriorityWeight;

        // Capability alignment (+10 for each matching required flag)
        foreach (ModelCapability flag in Enum.GetValues<ModelCapability>())
        {
            if (flag == ModelCapability.None) continue;
            if ((requiredCaps & flag) != 0)
            {
                if ((model.Capabilities & flag) != 0)
                {
                    score += 15.0;
                }
                else
                {
                    score -= 25.0; // Heavy penalty for missing a required capability
                }
            }
        }

        // Privacy bonuses / penalties
        if (request.PrivacyMode == SparkModelPrivacyMode.PreferLocal)
        {
            if (model.IsLocal) score += 30.0;
        }

        // Latency penalty (prefer faster models for conversation)
        if (request.TaskCategory == SparkTaskCategory.SimpleConversation)
        {
            if ((model.Capabilities & ModelCapability.FastResponse) != 0) score += 15.0;
            score -= (model.AverageLatencyMs / 200.0);
        }

        // Cost penalty for balanced mode
        if (request.PrivacyMode == SparkModelPrivacyMode.Balanced)
        {
            score -= (double)(model.CostPer1kTokens * 2.0m);
        }

        return Math.Max(0.0, score);
    }

    private void RegisterDefaultModelTiers()
    {
        // 1. Primary Local Model (Fast, Zero Cost, Private)
        RegisterModel(new SparkModelDescriptor
        {
            ModelId = "spark-local-default",
            DisplayName = "SPARK Local Offline Engine",
            Provider = SparkModelProvider.Local,
            IsLocal = true,
            ContextWindowTokens = 32_768,
            Capabilities = ModelCapability.General | ModelCapability.Coding | ModelCapability.ToolCalling | ModelCapability.LocalInference | ModelCapability.FastResponse,
            CostPer1kTokens = 0.0m,
            AverageLatencyMs = 250,
            PriorityWeight = 1.2
        });

        // 2. High-Speed Cloud Tier (Gemini Flash / Claude Haiku tier)
        RegisterModel(new SparkModelDescriptor
        {
            ModelId = "spark-cloud-fast",
            DisplayName = "SPARK Cloud Fast Tier",
            Provider = SparkModelProvider.Google,
            IsLocal = false,
            ContextWindowTokens = 1_000_000,
            Capabilities = ModelCapability.General | ModelCapability.Coding | ModelCapability.Vision | ModelCapability.Multimodal | ModelCapability.ToolCalling | ModelCapability.FastResponse | ModelCapability.LongContext,
            CostPer1kTokens = 0.0001m,
            AverageLatencyMs = 300,
            PriorityWeight = 1.0
        });

        // 3. Deep Reasoning Cloud Tier (DeepSeek-R1 / o1 / Sonnet tier)
        RegisterModel(new SparkModelDescriptor
        {
            ModelId = "spark-cloud-reasoning",
            DisplayName = "SPARK Deep Reasoning Tier",
            Provider = SparkModelProvider.Anthropic,
            IsLocal = false,
            ContextWindowTokens = 200_000,
            Capabilities = ModelCapability.General | ModelCapability.Reasoning | ModelCapability.Coding | ModelCapability.Mathematics | ModelCapability.ToolCalling | ModelCapability.LongContext,
            CostPer1kTokens = 0.003m,
            AverageLatencyMs = 1200,
            PriorityWeight = 1.1
        });

        // 4. Multimodal & Vision Specialist
        RegisterModel(new SparkModelDescriptor
        {
            ModelId = "spark-cloud-vision",
            DisplayName = "SPARK Multimodal Vision Specialist",
            Provider = SparkModelProvider.OpenAI,
            IsLocal = false,
            ContextWindowTokens = 128_000,
            Capabilities = ModelCapability.General | ModelCapability.Vision | ModelCapability.Multimodal | ModelCapability.LongContext | ModelCapability.ToolCalling,
            CostPer1kTokens = 0.0025m,
            AverageLatencyMs = 600,
            PriorityWeight = 1.0
        });
    }
}
