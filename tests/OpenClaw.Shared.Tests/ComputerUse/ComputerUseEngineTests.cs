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
using OpenClaw.Shared.ComputerUse.Perception;
using OpenClaw.Shared.ComputerUse.Providers;
using OpenClaw.Shared.ComputerUse.Safety;
using OpenClaw.Shared.ComputerUse.Sessions;
using OpenClaw.Shared.ComputerUse.Types;
using OpenClaw.Shared.ComputerUse.Verification;
using Xunit;

namespace OpenClaw.Shared.Tests.ComputerUse;

public class ComputerUseEngineTests
{
    private class MockOperator : IComputerUseOperator
    {
        public List<ComputerUseAction> ExecutedActions { get; } = new();

        public Task<ScreenCaptureResult> CaptureScreenshotAsync(int monitorIndex = 0, int maxWidth = 1920, int quality = 85, CancellationToken ct = default)
        {
            return Task.FromResult(new ScreenCaptureResult
            {
                Format = "png",
                Width = 1920,
                Height = 1080,
                Base64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="
            });
        }

        public Task<ActionExecutionOutput> ExecuteAsync(ComputerUseAction action, int screenWidth, int screenHeight, double scaleFactor = 1.0, CancellationToken ct = default)
        {
            ExecutedActions.Add(action);
            return Task.FromResult(new ActionExecutionOutput
            {
                Success = true,
                Message = $"Executed {action.ActionType}"
            });
        }

        public Task MoveMouseAsync(int x, int y, CancellationToken ct = default) => Task.CompletedTask;
        public Task ClickAsync(int x, int y, ComputerUseActionType clickType = ComputerUseActionType.Click, CancellationToken ct = default) => Task.CompletedTask;
        public Task DragAsync(int startX, int startY, int endX, int endY, CancellationToken ct = default) => Task.CompletedTask;
        public Task ScrollAsync(int x, int y, ScrollDirection direction, int amount = 500, CancellationToken ct = default) => Task.CompletedTask;
        public Task TypeTextAsync(string text, CancellationToken ct = default) => Task.CompletedTask;
        public Task HotkeyAsync(string keyCombination, CancellationToken ct = default) => Task.CompletedTask;
        public Task KeyPressAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
        public Task KeyReleaseAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
        public string? GetActiveWindowTitle() => "SPARK Test Window";
        public Rectangle GetScreenBounds(int monitorIndex = 0) => new Rectangle(0, 0, 1920, 1080);
    }

    private class MockModelProvider : IComputerUseModelProvider
    {
        private readonly Queue<string> _responses = new();

        public MockModelProvider(params string[] responses)
        {
            foreach (var r in responses)
            {
                _responses.Enqueue(r);
            }
        }

        public Task<ComputerUseModelResponse> GenerateActionAsync(ComputerUseModelRequest request, CancellationToken ct = default)
        {
            var raw = _responses.Count > 0 ? _responses.Dequeue() : "Action: finished()";
            return Task.FromResult(ComputerUseModelResponse.Ok(raw, "mock-vlm", 50, 10.0));
        }
    }

    [Fact]
    public async Task RunSessionLoopAsync_CompletesSuccessfully_WhenFinishedActionReturned()
    {
        var mockOp = new MockOperator();
        var mockProvider = new MockModelProvider(
            "Thought: Click submit.\nAction: click(start_box='[500,500]')",
            "Thought: Task completed.\nAction: finished()"
        );

        var safety = new ComputerUseSafetyGuard();
        safety.SetDesktopControlPermitted(true);

        var sessionManager = new ComputerUseSessionManager();
        var eventHub = new ComputerUseEventHub();
        var parser = new ComputerUseActionParser();
        var verifier = new ActionVerifier();
        var perception = new PerceptionEngine(mockOp);

        var engine = new ComputerUseEngine(
            desktopOperator: mockOp,
            perceptionEngine: perception,
            actionParser: parser,
            modelProvider: mockProvider,
            verifier: verifier,
            safetyGuard: safety,
            sessionManager: sessionManager,
            eventHub: eventHub);

        var options = new ComputerUseSessionOptions
        {
            MaxSteps = 5,
            ActionDelayMs = 0
        };

        var session = await engine.StartSessionAsync("Click submit button", options);
        await engine.RunSessionLoopAsync(session.SessionId);

        var finalSession = sessionManager.GetSession(session.SessionId);
        Assert.NotNull(finalSession);
        Assert.Equal(ComputerUseSessionStatus.Completed, finalSession.Status);
        Assert.Equal(2, finalSession.Steps.Count);
        Assert.Equal(ComputerUseActionType.Click, finalSession.Steps[0].Action?.ActionType);
        Assert.Equal(ComputerUseActionType.Finished, finalSession.Steps[1].Action?.ActionType);
    }

    [Fact]
    public async Task RunSessionLoopAsync_HaltsGracefully_OnCancellation()
    {
        var mockOp = new MockOperator();
        var mockProvider = new MockModelProvider(
            "Action: click(start_box='[100,100]')",
            "Action: click(start_box='[200,200]')"
        );

        var safety = new ComputerUseSafetyGuard();
        safety.SetDesktopControlPermitted(true);

        var sessionManager = new ComputerUseSessionManager();
        var eventHub = new ComputerUseEventHub();
        var parser = new ComputerUseActionParser();
        var verifier = new ActionVerifier();
        var perception = new PerceptionEngine(mockOp);

        var engine = new ComputerUseEngine(
            desktopOperator: mockOp,
            perceptionEngine: perception,
            actionParser: parser,
            modelProvider: mockProvider,
            verifier: verifier,
            safetyGuard: safety,
            sessionManager: sessionManager,
            eventHub: eventHub);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel

        var options = new ComputerUseSessionOptions { MaxSteps = 10, ActionDelayMs = 0 };
        var session = await engine.StartSessionAsync("Cancelled task", options);
        await engine.RunSessionLoopAsync(session.SessionId, cts.Token);

        var finalSession = sessionManager.GetSession(session.SessionId);
        Assert.NotNull(finalSession);
        // On cancellation token signaled, loop halts without failing
        Assert.True(finalSession.Status is ComputerUseSessionStatus.Initializing or ComputerUseSessionStatus.StoppedByUser);
    }

    [Fact]
    public async Task RunSessionLoopAsync_EmergencyStop_AbortsExecution()
    {
        var mockOp = new MockOperator();
        var mockProvider = new MockModelProvider(
            "Action: click(start_box='[100,100]')"
        );

        var safety = new ComputerUseSafetyGuard();
        safety.SetDesktopControlPermitted(true);
        safety.TriggerEmergencyStop();

        var sessionManager = new ComputerUseSessionManager();
        var eventHub = new ComputerUseEventHub();
        var parser = new ComputerUseActionParser();
        var verifier = new ActionVerifier();
        var perception = new PerceptionEngine(mockOp);

        var engine = new ComputerUseEngine(
            desktopOperator: mockOp,
            perceptionEngine: perception,
            actionParser: parser,
            modelProvider: mockProvider,
            verifier: verifier,
            safetyGuard: safety,
            sessionManager: sessionManager,
            eventHub: eventHub);

        var options = new ComputerUseSessionOptions { MaxSteps = 5, ActionDelayMs = 0 };
        var session = await engine.StartSessionAsync("Emergency task", options);
        await engine.RunSessionLoopAsync(session.SessionId);

        var finalSession = sessionManager.GetSession(session.SessionId);
        Assert.NotNull(finalSession);
        Assert.Equal(ComputerUseSessionStatus.StoppedByUser, finalSession.Status);
        Assert.Contains("Emergency stop is active", finalSession.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
