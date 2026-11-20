using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Inference;
using OpenClaw.Shared.Inference.Routing;
using Xunit;

namespace OpenClaw.Shared.Tests;

public class SparkModelRouterTests
{
    [Fact]
    public void ListModels_ReturnsConfiguredDefaultTiers()
    {
        var router = new SparkModelRouter();
        var models = router.ListModels();

        Assert.NotEmpty(models);
        Assert.Contains(models, m => m.IsLocal);
        Assert.Contains(models, m => !m.IsLocal);
    }

    [Fact]
    public void Route_ComplexReasoning_SelectsReasoningTier()
    {
        var router = new SparkModelRouter();
        var request = new SparkRoutingRequest
        {
            TaskCategory = SparkTaskCategory.ComplexReasoning,
            PrivacyMode = SparkModelPrivacyMode.PerformanceFirst
        };

        var decision = router.Route(request);
        Assert.NotNull(decision);
        Assert.Equal("spark-cloud-reasoning", decision.SelectedModel.ModelId);
        Assert.True((decision.SelectedModel.Capabilities & ModelCapability.Reasoning) != 0);
        Assert.NotEmpty(decision.FallbackChain);
    }

    [Fact]
    public void Route_ImageUnderstanding_SelectsVisionSpecialist()
    {
        var router = new SparkModelRouter();
        var request = new SparkRoutingRequest
        {
            TaskCategory = SparkTaskCategory.ImageUnderstanding,
            PrivacyMode = SparkModelPrivacyMode.PerformanceFirst
        };

        var decision = router.Route(request);
        Assert.NotNull(decision);
        Assert.True((decision.SelectedModel.Capabilities & ModelCapability.Vision) != 0);
    }

    [Fact]
    public void Route_SensitiveData_GuaranteesLocalModelOnly()
    {
        var router = new SparkModelRouter();
        var request = new SparkRoutingRequest
        {
            TaskCategory = SparkTaskCategory.Coding,
            ContainsSensitiveLocalData = true // STRICT PRIVACY GUARD
        };

        var decision = router.Route(request);
        Assert.NotNull(decision);
        Assert.True(decision.IsLocal);
        Assert.Equal("spark-local-default", decision.SelectedModel.ModelId);

        // Every model in the fallback chain must also be local!
        foreach (var fallback in decision.FallbackChain)
        {
            Assert.True(fallback.IsLocal);
        }
    }

    [Fact]
    public void Route_StrictLocalMode_WhenNoLocalModelAvailable_ThrowsRefusal()
    {
        var router = new SparkModelRouter(populateDefaultTiers: false);

        // Register ONLY a cloud model
        router.RegisterModel(new SparkModelDescriptor
        {
            ModelId = "cloud-only",
            DisplayName = "Cloud Only",
            IsLocal = false,
            IsAvailable = true
        });

        var request = new SparkRoutingRequest
        {
            TaskCategory = SparkTaskCategory.SimpleConversation,
            PrivacyMode = SparkModelPrivacyMode.StrictLocalOnly
        };

        var ex = Assert.Throws<InvalidOperationException>(() => router.Route(request));
        Assert.Contains("Cloud transmission refused", ex.Message);
    }

    [Fact]
    public void Route_ManualOverrideWithSensitiveData_RejectsCloudModel()
    {
        var router = new SparkModelRouter();
        var request = new SparkRoutingRequest
        {
            TaskCategory = SparkTaskCategory.Coding,
            ContainsSensitiveLocalData = true,
            UserManualModelOverride = "spark-cloud-reasoning" // user attempted to force cloud on private data
        };

        var ex = Assert.Throws<InvalidOperationException>(() => router.Route(request));
        Assert.Contains("rejected: request contains sensitive local data", ex.Message);
    }

    [Fact]
    public void Route_ValidManualOverride_HonorsUserSelection()
    {
        var router = new SparkModelRouter();
        var request = new SparkRoutingRequest
        {
            TaskCategory = SparkTaskCategory.SimpleConversation,
            UserManualModelOverride = "spark-cloud-reasoning"
        };

        var decision = router.Route(request);
        Assert.Equal("spark-cloud-reasoning", decision.SelectedModel.ModelId);
    }

    [Fact]
    public async Task ExecuteWithFallback_WhenPrimaryFails_UsesFallbackChain()
    {
        var router = new SparkModelRouter();
        var request = new SparkRoutingRequest
        {
            TaskCategory = SparkTaskCategory.ComplexReasoning,
            PrivacyMode = SparkModelPrivacyMode.PerformanceFirst
        };

        // Simulated model invoker: fails on primary "spark-cloud-reasoning", succeeds on fallback
        var result = await router.ExecuteWithFallbackAsync(request, (model, ct) =>
        {
            if (model.ModelId == "spark-cloud-reasoning")
            {
                throw new HttpRequestException("Simulated cloud connection timeout.");
            }
            return Task.FromResult($"Generated response from {model.DisplayName}");
        });

        Assert.True(result.Success);
        Assert.True(result.WasFallbackUsed);
        Assert.Equal(2, result.AttemptCount);
        Assert.Contains("Generated response from", result.Content);
    }
}
