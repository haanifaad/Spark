using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.DecisionIntelligence;
using OpenClaw.Shared.Inference;
using OpenClaw.Shared.Security.Sentinel;
using OpenClaw.Shared.Tools;
using Xunit;

namespace OpenClaw.Shared.Tests;

public sealed class SparkDecisionIntelligenceTests : IDisposable
{
    private readonly string _testBaseDir;
    private readonly LayaRuntimeManager _runtimeManager;
    private readonly SparkDecisionIntelligenceEngine _engine;

    public SparkDecisionIntelligenceTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), "Spark_Laya_Tests_" + Guid.NewGuid().ToString("N"));
        _runtimeManager = new LayaRuntimeManager(_testBaseDir);
        _engine = new SparkDecisionIntelligenceEngine(_runtimeManager);
    }

    [Fact]
    public void InitialStatus_ShouldBe_NotInstalled()
    {
        Assert.Equal(LayaRuntimeStatus.NotInstalled, _runtimeManager.Status);
        Assert.False(_engine.IsAvailable);
    }

    [Fact]
    public void HardwareCompatibilityProbe_DetectsRequirementsAccurately()
    {
        var probe = new CudaHostHardwareProbe();
        var hardware = probe.Probe();
        var report = LayaHardwareCompatibilityProbe.EvaluateCompatibility(hardware, _testBaseDir, "laya-english");

        Assert.NotNull(report);
        Assert.True(report.CpuSupported);
        Assert.NotEmpty(report.OperatingSystem);
        Assert.NotEmpty(report.Architecture);
        Assert.True(report.DiskSpaceSufficient);
    }

    [Fact]
    public async Task InstallAsync_SetsUpIsolatedRuntimeAndReceipt()
    {
        var progressList = new List<LayaProgressReport>();
        var progress = new Progress<LayaProgressReport>(p => progressList.Add(p));

        bool success = await _runtimeManager.InstallAsync("laya-english", progress, CancellationToken.None);

        Assert.True(success);
        Assert.Equal(LayaRuntimeStatus.InstalledDisabled, _runtimeManager.Status);
        Assert.True(File.Exists(Path.Combine(_runtimeManager.RuntimeDirectory, "install_receipt.json")));
        Assert.True(File.Exists(Path.Combine(_runtimeManager.ModelsDirectory, "laya-english", "config.json")));
        Assert.NotEmpty(progressList);
    }

    [Fact]
    public async Task StartStopRestart_ManagesLifecycleAccurately()
    {
        await _runtimeManager.InstallAsync("laya-english");

        bool started = await _runtimeManager.StartAsync();
        Assert.True(started);
        Assert.Equal(LayaRuntimeStatus.EnabledOperational, _runtimeManager.Status);
        Assert.True(_engine.IsAvailable);

        bool stopped = await _runtimeManager.StopAsync();
        Assert.True(stopped);
        Assert.Equal(LayaRuntimeStatus.InstalledDisabled, _runtimeManager.Status);
        Assert.False(_engine.IsAvailable);

        bool restarted = await _runtimeManager.RestartAsync();
        Assert.True(restarted);
        Assert.Equal(LayaRuntimeStatus.EnabledOperational, _runtimeManager.Status);
    }

    [Fact]
    public async Task SinglePassEvaluation_ComputesCalibratedChoicesAndProbabilities()
    {
        await _runtimeManager.InstallAsync("laya-english");
        await _runtimeManager.StartAsync();

        var stateText = "High transaction amount request: The user wants to execute a ₹50,000 transfer to an unverified recipient.";
        var questions = new List<LayaQuestion>
        {
            new LayaQuestion
            {
                Id = "risk_category",
                Kind = LayaQuestionKind.Choice,
                Instruction = "Select risk level",
                Options = new List<string> { "low_risk", "high_risk" }
            },
            new LayaQuestion
            {
                Id = "is_urgent",
                Kind = LayaQuestionKind.Noul,
                Instruction = "Is this urgent action?"
            },
            new LayaQuestion
            {
                Id = "urgency_score",
                Kind = LayaQuestionKind.Score,
                MinScore = 0.0,
                MaxScore = 10.0
            }
        };

        var result = await _engine.EvaluateAsync(stateText, questions);

        Assert.True(result.Success);
        Assert.False(result.IsFallback);
        Assert.Equal(3, result.Answers.Count);
        Assert.NotNull(result["risk_category"]);
        Assert.NotNull(result["is_urgent"]);
        Assert.NotNull(result["urgency_score"]);
        Assert.True(result.ExecutionTimeMs >= 0.0);
    }

    [Fact]
    public async Task FallbackGracefulDegradation_WorksWhenDisabledOrUninstalled()
    {
        // Engine is not installed
        _engine.IsEnabled = false;

        var stateText = "Simple test query";
        var choice = await _engine.ClassifyChoiceAsync(stateText, "Choose category", new[] { "cat1", "cat2" });

        Assert.NotNull(choice);
        Assert.NotEmpty(choice.SelectedOption!);
    }

    [Fact]
    public async Task SentinelReviewer_IntegratesWithLayaDecisionIntelligence()
    {
        await _runtimeManager.InstallAsync("laya-english");
        await _runtimeManager.StartAsync();

        var sentinel = new SentinelReviewer(_engine);
        var request = new SentinelEvaluationRequest
        {
            TaskId = "test_task_1",
            ProposedToolName = "read_file",
            TaskOriginalInstruction = "Read the project documentation file",
            DataPayloadText = "path: README.md",
            FinancialAmount = 0
        };

        var review = await sentinel.ReviewOperationAsync(request);

        Assert.NotNull(review);
        Assert.Equal(SentinelDecisionKind.Approved, review.Decision);
        Assert.Equal(SparkToolRiskLevel.Low, review.RiskLevel);
    }

    [Fact]
    public async Task UninstallAsync_PurgesAllFilesAndRestoresInitialState()
    {
        await _runtimeManager.InstallAsync("laya-english");
        await _runtimeManager.StartAsync();

        bool uninstalled = await _runtimeManager.UninstallAsync();

        Assert.True(uninstalled);
        Assert.Equal(LayaRuntimeStatus.NotInstalled, _runtimeManager.Status);
        Assert.False(File.Exists(Path.Combine(_runtimeManager.RuntimeDirectory, "install_receipt.json")));
    }

    public void Dispose()
    {
        try
        {
            _runtimeManager.Dispose();
            if (Directory.Exists(_testBaseDir))
            {
                Directory.Delete(_testBaseDir, recursive: true);
            }
        }
        catch { }
    }
}
