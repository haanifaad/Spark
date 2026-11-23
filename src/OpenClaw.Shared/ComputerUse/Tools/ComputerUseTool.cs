using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.ComputerUse.Engine;
using OpenClaw.Shared.ComputerUse.Orchestration;
using OpenClaw.Shared.ComputerUse.Types;
using OpenClaw.Shared.Tools;

namespace OpenClaw.Shared.ComputerUse.Tools;

/// <summary>
/// Exposes Computer Use capabilities as a native SPARK tool in <see cref="ISparkToolRegistry"/>.
/// Enables other SPARK agents and MCP clients to invoke desktop automation tasks seamlessly.
/// </summary>
public sealed class ComputerUseTool : ISparkTool
{
    private readonly IComputerUseEngine _engine;
    private readonly ComputerUseOrchestrator _orchestrator;

    public SparkToolMetadata Metadata { get; } = new()
    {
        Id = "computer_use",
        Name = "Computer Use",
        Description = "Autonomously observes and interacts with the user's desktop via GUI automation to complete user goals.",
        Category = SparkToolCategory.System,
        RiskLevel = SparkToolRiskLevel.High,
        RequiredPermissions = new List<string> { "desktop.control", "screen.capture" },
        InputSchemaJson = @"{
            ""type"": ""object"",
            ""properties"": {
                ""goal"": { ""type"": ""string"", ""description"": ""The task to complete on the computer."" },
                ""max_steps"": { ""type"": ""integer"", ""description"": ""Maximum execution steps allowed (default 30)."" },
                ""step_by_step"": { ""type"": ""boolean"", ""description"": ""Whether to pause for user confirmation after each step."" }
            },
            ""required"": [""goal""]
        }",
        OutputSchemaJson = @"{
            ""type"": ""object"",
            ""properties"": {
                ""sessionId"": { ""type"": ""string"" },
                ""status"": { ""type"": ""string"" },
                ""stepsCount"": { ""type"": ""integer"" },
                ""report"": { ""type"": ""string"" }
            }
        }",
        DefaultTimeout = TimeSpan.FromMinutes(10),
        IsEnabled = true
    };

    public ComputerUseTool(IComputerUseEngine engine, ComputerUseOrchestrator orchestrator)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    }

    public async Task<SparkToolExecutionResponse> ExecuteAsync(
        SparkToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        string? goal = request.GetArgument<string>("goal");

        if (string.IsNullOrWhiteSpace(goal))
        {
            return SparkToolExecutionResponse.Fail(
                Metadata.Id,
                "Missing required argument 'goal'.",
                Metadata.RiskLevel,
                sw.Elapsed);
        }

        int maxSteps = request.GetArgument<int>("max_steps", 30);
        bool stepByStep = request.GetArgument<bool>("step_by_step", false);

        var options = new ComputerUseSessionOptions
        {
            MaxSteps = maxSteps,
            StepByStepMode = stepByStep
        };

        try
        {
            string report = await _orchestrator.RunTaskWithReportAsync(goal, options, cancellationToken).ConfigureAwait(false);
            sw.Stop();

            return SparkToolExecutionResponse.Ok(
                Metadata.Id,
                new
                {
                    goal,
                    report,
                    durationMs = sw.ElapsedMilliseconds
                },
                Metadata.RiskLevel,
                sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return SparkToolExecutionResponse.Fail(
                Metadata.Id,
                $"Computer Use failed: {ex.Message}",
                Metadata.RiskLevel,
                sw.Elapsed);
        }
    }
}
