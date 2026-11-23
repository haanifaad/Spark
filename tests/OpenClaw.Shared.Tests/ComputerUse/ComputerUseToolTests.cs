using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Capabilities;
using OpenClaw.Shared.ComputerUse.Actions;
using OpenClaw.Shared.ComputerUse.Engine;
using OpenClaw.Shared.ComputerUse.Events;
using OpenClaw.Shared.ComputerUse.Operators;
using OpenClaw.Shared.ComputerUse.Orchestration;
using OpenClaw.Shared.ComputerUse.Perception;
using OpenClaw.Shared.ComputerUse.Providers;
using OpenClaw.Shared.ComputerUse.Safety;
using OpenClaw.Shared.ComputerUse.Sessions;
using OpenClaw.Shared.ComputerUse.Tools;
using OpenClaw.Shared.ComputerUse.Types;
using OpenClaw.Shared.ComputerUse.Verification;
using OpenClaw.Shared.Tools;
using Xunit;

namespace OpenClaw.Shared.Tests.ComputerUse;

public class ComputerUseToolTests
{
    private class MockOperator : IComputerUseOperator
    {
        public Task<ScreenCaptureResult> CaptureScreenshotAsync(int monitorIndex = 0, int maxWidth = 1920, int quality = 85, CancellationToken ct = default)
            => Task.FromResult(new ScreenCaptureResult
            {
                Format = "png",
                Width = 1920,
                Height = 1080,
                Base64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="
            });

        public Task<ActionExecutionOutput> ExecuteAsync(ComputerUseAction action, int screenWidth, int screenHeight, double scaleFactor = 1.0, CancellationToken ct = default)
            => Task.FromResult(new ActionExecutionOutput { Success = true, Message = "OK" });

        public Task MoveMouseAsync(int x, int y, CancellationToken ct = default) => Task.CompletedTask;
        public Task ClickAsync(int x, int y, ComputerUseActionType clickType = ComputerUseActionType.Click, CancellationToken ct = default) => Task.CompletedTask;
        public Task DragAsync(int startX, int startY, int endX, int endY, CancellationToken ct = default) => Task.CompletedTask;
        public Task ScrollAsync(int x, int y, ScrollDirection direction, int amount = 500, CancellationToken ct = default) => Task.CompletedTask;
        public Task TypeTextAsync(string text, CancellationToken ct = default) => Task.CompletedTask;
        public Task HotkeyAsync(string keyCombination, CancellationToken ct = default) => Task.CompletedTask;
        public Task KeyPressAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
        public Task KeyReleaseAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
        public string? GetActiveWindowTitle() => "Test Window";
        public Rectangle GetScreenBounds(int monitorIndex = 0) => new Rectangle(0, 0, 1920, 1080);
    }

    private class MockModelProvider : IComputerUseModelProvider
    {
        public Task<ComputerUseModelResponse> GenerateActionAsync(ComputerUseModelRequest request, CancellationToken ct = default)
            => Task.FromResult(ComputerUseModelResponse.Ok("Thought: Done\nAction: finished()", "mock-vlm", 20, 5.0));
    }

    private ComputerUseTool CreateTestTool()
    {
        var mockOp = new MockOperator();
        var mockProvider = new MockModelProvider();
        var safety = new ComputerUseSafetyGuard();
        safety.SetDesktopControlPermitted(true);
        var sessionManager = new ComputerUseSessionManager();
        var eventHub = new ComputerUseEventHub();
        var parser = new ComputerUseActionParser();
        var verifier = new ActionVerifier();
        var perception = new PerceptionEngine(mockOp);

        var engine = new ComputerUseEngine(
            mockOp,
            perception,
            parser,
            mockProvider,
            verifier,
            safety,
            sessionManager,
            eventHub);

        var orchestrator = new ComputerUseOrchestrator(engine, sessionManager, eventHub);

        return new ComputerUseTool(engine, orchestrator);
    }

    [Fact]
    public void Metadata_HasExpectedAttributes()
    {
        var tool = CreateTestTool();

        Assert.Equal("computer_use", tool.Metadata.Id);
        Assert.Equal("Computer Use", tool.Metadata.Name);
        Assert.Contains("GUI automation", tool.Metadata.Description);
        Assert.Equal(SparkToolRiskLevel.High, tool.Metadata.RiskLevel);
        Assert.Contains("desktop.control", tool.Metadata.RequiredPermissions);
    }

    [Fact]
    public async Task ExecuteAsync_MissingGoal_ReturnsFailureResponse()
    {
        var tool = CreateTestTool();

        var request = new SparkToolExecutionRequest
        {
            ToolId = "computer_use",
            CallerIdentity = "unit-test",
            Arguments = new Dictionary<string, object?>()
        };

        var response = await tool.ExecuteAsync(request);

        Assert.False(response.Success);
        Assert.Contains("Missing required argument 'goal'", response.Error);
    }

    [Fact]
    public async Task ExecuteAsync_WithGoal_RunsOrchestrationSuccessfully()
    {
        var tool = CreateTestTool();

        var request = new SparkToolExecutionRequest
        {
            ToolId = "computer_use",
            CallerIdentity = "unit-test",
            Arguments = new Dictionary<string, object?>
            {
                ["goal"] = "Verify desktop state",
                ["max_steps"] = 3
            }
        };

        var response = await tool.ExecuteAsync(request);

        Assert.True(response.Success);
        Assert.NotNull(response.Result);
    }
}
