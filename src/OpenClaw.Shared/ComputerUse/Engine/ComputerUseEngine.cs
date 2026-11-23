using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.ComputerUse.Actions;
using OpenClaw.Shared.ComputerUse.Events;
using OpenClaw.Shared.ComputerUse.Operators;
using OpenClaw.Shared.ComputerUse.Perception;
using OpenClaw.Shared.ComputerUse.Providers;
using OpenClaw.Shared.ComputerUse.Safety;
using OpenClaw.Shared.ComputerUse.Sessions;
using OpenClaw.Shared.ComputerUse.Types;
using OpenClaw.Shared.ComputerUse.Verification;

namespace OpenClaw.Shared.ComputerUse.Engine;

/// <summary>
/// Native SPARK Computer Use Engine orchestrating the complete OBSERVE -> PLAN -> ACT -> VERIFY execution lifecycle.
/// </summary>
public sealed class ComputerUseEngine : IComputerUseEngine
{
    private readonly IComputerUseOperator _operator;
    private readonly IPerceptionEngine _perceptionEngine;
    private readonly IComputerUseActionParser _actionParser;
    private readonly IComputerUseModelProvider _modelProvider;
    private readonly IActionVerifier _verifier;
    private readonly IComputerUseSafetyGuard _safetyGuard;
    private readonly IComputerUseSessionManager _sessionManager;
    private readonly IComputerUseEventHub _eventHub;
    private readonly IOpenClawLogger? _logger;

    private readonly ConcurrentDictionary<string, CancellationTokenSource> _sessionCancellation = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pendingConfirmations = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ManualResetEventSlim> _pauseEvents = new(StringComparer.OrdinalIgnoreCase);

    public ComputerUseEngine(
        IComputerUseOperator desktopOperator,
        IPerceptionEngine perceptionEngine,
        IComputerUseActionParser actionParser,
        IComputerUseModelProvider modelProvider,
        IActionVerifier verifier,
        IComputerUseSafetyGuard safetyGuard,
        IComputerUseSessionManager sessionManager,
        IComputerUseEventHub eventHub,
        IOpenClawLogger? logger = null)
    {
        _operator = desktopOperator ?? throw new ArgumentNullException(nameof(desktopOperator));
        _perceptionEngine = perceptionEngine ?? throw new ArgumentNullException(nameof(perceptionEngine));
        _actionParser = actionParser ?? throw new ArgumentNullException(nameof(actionParser));
        _modelProvider = modelProvider ?? throw new ArgumentNullException(nameof(modelProvider));
        _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
        _safetyGuard = safetyGuard ?? throw new ArgumentNullException(nameof(safetyGuard));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _eventHub = eventHub ?? throw new ArgumentNullException(nameof(eventHub));
        _logger = logger;

        _safetyGuard.EmergencyStopTriggered += (s, e) =>
        {
            foreach (var cts in _sessionCancellation.Values)
            {
                try { cts.Cancel(); } catch { }
            }
        };
    }

    public Task<ComputerUseSession> StartSessionAsync(string goal, ComputerUseSessionOptions? options = null, CancellationToken ct = default)
    {
        var session = _sessionManager.CreateSession(goal, options);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _sessionCancellation[session.SessionId] = cts;
        _pauseEvents[session.SessionId] = new ManualResetEventSlim(true);

        _eventHub.PublishLog(ComputerUseLogLevel.Info, "Engine", $"Started new Computer Use session [{session.SessionId}] for goal: {goal}");
        _eventHub.PublishSessionStatus(session);

        return Task.FromResult(session);
    }

    public async Task<ComputerUseSession> RunSessionAsync(string goal, ComputerUseSessionOptions? options = null, CancellationToken ct = default)
    {
        var session = await StartSessionAsync(goal, options, ct).ConfigureAwait(false);
        await RunSessionLoopAsync(session.SessionId, ct).ConfigureAwait(false);
        return _sessionManager.GetSession(session.SessionId) ?? session;
    }

    public async Task<ComputerUseStep?> ExecuteStepAsync(string sessionId, CancellationToken ct = default)
    {
        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
        {
            _eventHub.PublishLog(ComputerUseLogLevel.Error, "Engine", $"Session {sessionId} not found.");
            return null;
        }

        if (!_sessionCancellation.TryGetValue(sessionId, out var cts))
        {
            cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _sessionCancellation[sessionId] = cts;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, ct);
        var token = linkedCts.Token;

        // Check pause state
        if (_pauseEvents.TryGetValue(sessionId, out var pauseEvent))
        {
            pauseEvent.Wait(token);
        }

        if (_safetyGuard.IsEmergencyStopped)
        {
            _sessionManager.UpdateSessionStatus(sessionId, ComputerUseSessionStatus.StoppedByUser, error: "Emergency stop is active.");
            _eventHub.PublishSessionStatus(session);
            return null;
        }

        int stepIndex = session.Steps.Count + 1;
        var step = new ComputerUseStep { StepIndex = stepIndex, Timestamp = DateTimeOffset.UtcNow };
        var sw = Stopwatch.StartNew();

        _eventHub.PublishStepStarted(step);
        _eventHub.PublishLog(ComputerUseLogLevel.Info, "Engine", $"--- Step {stepIndex} Beginning ---");

        try
        {
            // ══════════════ 1. OBSERVE ══════════════
            _eventHub.PublishLog(ComputerUseLogLevel.Debug, "Perception", "Capturing desktop observation...");
            var beforeSnapshot = await _perceptionEngine.CapturePerceptionAsync(
                monitorIndex: session.Options.MonitorIndex,
                maxWidth: session.Options.MaxScreenshotWidth,
                inspectAccessibility: true,
                ct: token).ConfigureAwait(false);

            step.ScreenshotBeforeBase64 = beforeSnapshot.ScreenshotBase64;
            step.ScreenWidth = beforeSnapshot.Width;
            step.ScreenHeight = beforeSnapshot.Height;
            step.ScaleFactor = beforeSnapshot.ScaleFactor;
            _eventHub.PublishScreenshot(beforeSnapshot);

            // ══════════════ 2. PLAN ══════════════
            _eventHub.PublishLog(ComputerUseLogLevel.Debug, "Model", "Invoking Vision-Language Model...");
            var modelRequest = new ComputerUseModelRequest
            {
                Goal = session.Goal,
                CurrentScreenshotBase64 = beforeSnapshot.ScreenshotBase64,
                ActiveWindowTitle = beforeSnapshot.ActiveWindowTitle,
                HistorySteps = session.Steps,
                VisibleElements = beforeSnapshot.Elements,
                PreferredModelId = session.Options.PreferredModelId
            };

            var modelResponse = await _modelProvider.GenerateActionAsync(modelRequest, token).ConfigureAwait(false);
            if (!modelResponse.Success)
            {
                _eventHub.PublishLog(ComputerUseLogLevel.Error, "Model", $"Model reasoning failed: {modelResponse.ErrorMessage}");
                step.ExecutionOutput = ActionExecutionOutput.Fail(modelResponse.ErrorMessage ?? "Model failed to return prediction");
                _sessionManager.AddStep(sessionId, step);
                _eventHub.PublishStepCompleted(step);
                return step;
            }

            _sessionManager.SetActiveModel(sessionId, modelResponse.ModelId);
            _sessionManager.RecordTokensUsed(sessionId, modelResponse.TokensUsed);

            // Parse prediction output
            var prediction = _actionParser.ParsePrediction(
                modelResponse.RawPrediction,
                beforeSnapshot.Width,
                beforeSnapshot.Height,
                beforeSnapshot.ScaleFactor);

            step.Thought = prediction.Thought;
            _eventHub.PublishLog(ComputerUseLogLevel.Info, "Planner", $"Thought: {prediction.Thought}");

            if (prediction.Actions.Count == 0)
            {
                _eventHub.PublishLog(ComputerUseLogLevel.Warning, "Planner", "No discrete actions recognized in model prediction.");
                step.ExecutionOutput = ActionExecutionOutput.Fail("No valid action parsed from model response.");
                _sessionManager.AddStep(sessionId, step);
                _eventHub.PublishStepCompleted(step);
                return step;
            }

            var primaryAction = prediction.Actions[0];
            step.Action = primaryAction;
            _eventHub.PublishLog(ComputerUseLogLevel.Info, "Planner", $"Planned Action: {primaryAction}");

            // ══════════════ 3. SAFETY CHECK ══════════════
            var safetyEval = await _safetyGuard.EvaluateActionAsync(primaryAction, beforeSnapshot, token).ConfigureAwait(false);
            if (!safetyEval.IsAllowed)
            {
                _eventHub.PublishLog(ComputerUseLogLevel.Error, "Safety", $"Action BLOCKED by Safety Guard: {safetyEval.Reason}");
                step.ExecutionOutput = ActionExecutionOutput.Fail($"Safety violation: {safetyEval.Reason}");
                _sessionManager.UpdateSessionStatus(sessionId, ComputerUseSessionStatus.Failed, error: safetyEval.Reason);
                _sessionManager.AddStep(sessionId, step);
                _eventHub.PublishStepCompleted(step);
                return step;
            }

            if (safetyEval.RequiresUserConfirmation && session.Options.RequireConfirmationForSensitive)
            {
                _eventHub.PublishLog(ComputerUseLogLevel.Warning, "Safety", $"Action requires explicit user confirmation: {safetyEval.Reason}");
                _sessionManager.UpdateSessionStatus(sessionId, ComputerUseSessionStatus.AwaitingApproval);
                _eventHub.PublishConfirmationRequired(safetyEval);

                var tcs = new TaskCompletionSource<bool>();
                _pendingConfirmations[sessionId] = tcs;

                using var reg = token.Register(() => tcs.TrySetCanceled());
                bool approved = await tcs.Task.ConfigureAwait(false);
                _pendingConfirmations.TryRemove(sessionId, out _);

                if (!approved)
                {
                    _eventHub.PublishLog(ComputerUseLogLevel.Warning, "Safety", "User rejected confirmation for sensitive action.");
                    step.ExecutionOutput = ActionExecutionOutput.Fail("User declined action approval.");
                    _sessionManager.UpdateSessionStatus(sessionId, ComputerUseSessionStatus.Paused);
                    _sessionManager.AddStep(sessionId, step);
                    _eventHub.PublishStepCompleted(step);
                    return step;
                }

                _sessionManager.UpdateSessionStatus(sessionId, ComputerUseSessionStatus.Running);
                _eventHub.PublishLog(ComputerUseLogLevel.Info, "Safety", "User approved sensitive action execution.");
            }

            // ══════════════ 4. ACT ══════════════
            _eventHub.PublishActionExecuting(primaryAction);
            var execOutput = await _operator.ExecuteAsync(
                primaryAction,
                beforeSnapshot.Width,
                beforeSnapshot.Height,
                beforeSnapshot.ScaleFactor,
                token).ConfigureAwait(false);

            step.ExecutionOutput = execOutput;
            _eventHub.PublishLog(ComputerUseLogLevel.Info, "Actuator", $"Executed: {execOutput.Message} ({execOutput.DurationMs:F0}ms)");

            // Allow UI settlement
            if (session.Options.ActionDelayMs > 0)
            {
                await Task.Delay(session.Options.ActionDelayMs, token).ConfigureAwait(false);
            }

            // ══════════════ 5. OBSERVE POST-ACTION & VERIFY ══════════════
            var afterSnapshot = await _perceptionEngine.CapturePerceptionAsync(
                monitorIndex: session.Options.MonitorIndex,
                maxWidth: session.Options.MaxScreenshotWidth,
                inspectAccessibility: true,
                ct: token).ConfigureAwait(false);

            step.ScreenshotAfterBase64 = afterSnapshot.ScreenshotBase64;
            _eventHub.PublishScreenshot(afterSnapshot);

            var verification = await _verifier.VerifyStepAsync(step, beforeSnapshot, afterSnapshot, token).ConfigureAwait(false);
            step.Verification = verification;
            _eventHub.PublishVerification(verification);
            _eventHub.PublishLog(ComputerUseLogLevel.Info, "Verifier", $"Verification: {verification.Status} (Delta: {verification.VisualDifferenceRatio:P1}) - {verification.Description}");

            // Terminal status checks
            if (primaryAction.ActionType == ComputerUseActionType.Finished)
            {
                _sessionManager.UpdateSessionStatus(sessionId, ComputerUseSessionStatus.Completed, summary: prediction.Thought);
                _eventHub.PublishLog(ComputerUseLogLevel.Info, "Engine", $"Task declared COMPLETED by agent: {prediction.Thought}");
            }
            else if (primaryAction.ActionType == ComputerUseActionType.CallUser)
            {
                _sessionManager.UpdateSessionStatus(sessionId, ComputerUseSessionStatus.AwaitingApproval, summary: "Agent requested assistance: " + prediction.Thought);
                _eventHub.PublishLog(ComputerUseLogLevel.Warning, "Engine", $"Agent requested USER ASSISTANCE: {prediction.Thought}");
            }

            sw.Stop();
            step.StepDurationMs = sw.ElapsedMilliseconds;
            _sessionManager.AddStep(sessionId, step);
            _eventHub.PublishStepCompleted(step);
            _eventHub.PublishSessionStatus(session);

            return step;
        }
        catch (OperationCanceledException)
        {
            _sessionManager.UpdateSessionStatus(sessionId, ComputerUseSessionStatus.StoppedByUser, error: "Operation cancelled");
            _eventHub.PublishLog(ComputerUseLogLevel.Warning, "Engine", $"Step {stepIndex} cancelled by user.");
            _eventHub.PublishSessionStatus(session);
            return step;
        }
        catch (Exception ex)
        {
            _logger?.Error($"[ComputerUseEngine] Error during step {stepIndex}: {ex.Message}", ex);
            step.ExecutionOutput = ActionExecutionOutput.Fail($"Unhandled step error: {ex.Message}");
            _sessionManager.UpdateSessionStatus(sessionId, ComputerUseSessionStatus.Failed, error: ex.Message);
            _sessionManager.AddStep(sessionId, step);
            _eventHub.PublishStepCompleted(step);
            _eventHub.PublishSessionStatus(session);
            return step;
        }
    }

    public async Task RunSessionLoopAsync(string sessionId, CancellationToken ct = default)
    {
        var session = _sessionManager.GetSession(sessionId);
        if (session == null) return;

        _sessionManager.UpdateSessionStatus(sessionId, ComputerUseSessionStatus.Running);
        _eventHub.PublishSessionStatus(session);

        if (!_sessionCancellation.TryGetValue(sessionId, out var cts))
        {
            cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _sessionCancellation[sessionId] = cts;
        }

        int maxSteps = session.Options.MaxSteps;

        while (!cts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            if (session.Status is ComputerUseSessionStatus.Completed or ComputerUseSessionStatus.Failed or ComputerUseSessionStatus.StoppedByUser)
            {
                break;
            }

            if (session.Steps.Count >= maxSteps)
            {
                _eventHub.PublishLog(ComputerUseLogLevel.Warning, "Engine", $"Maximum step limit ({maxSteps}) reached. Halting session.");
                _sessionManager.UpdateSessionStatus(sessionId, ComputerUseSessionStatus.Completed, summary: "Reached maximum allowed steps.");
                _eventHub.PublishSessionStatus(session);
                break;
            }

            var step = await ExecuteStepAsync(sessionId, ct).ConfigureAwait(false);
            if (step == null) break;

            if (session.Options.StepByStepMode)
            {
                PauseSession(sessionId);
                _eventHub.PublishLog(ComputerUseLogLevel.Info, "Engine", "Step-by-step mode active: Pausing after step execution.");
                break;
            }
        }

        if (cts.IsCancellationRequested || ct.IsCancellationRequested)
        {
            if (session.Status is not (ComputerUseSessionStatus.Completed or ComputerUseSessionStatus.Failed or ComputerUseSessionStatus.StoppedByUser))
            {
                _sessionManager.UpdateSessionStatus(sessionId, ComputerUseSessionStatus.StoppedByUser, summary: "Session stopped due to cancellation request.");
                _eventHub.PublishSessionStatus(session);
            }
        }
    }

    public void PauseSession(string sessionId)
    {
        if (_pauseEvents.TryGetValue(sessionId, out var pauseEvent))
        {
            pauseEvent.Reset();
            _sessionManager.UpdateSessionStatus(sessionId, ComputerUseSessionStatus.Paused);
            var session = _sessionManager.GetSession(sessionId);
            if (session != null) _eventHub.PublishSessionStatus(session);
            _eventHub.PublishLog(ComputerUseLogLevel.Info, "Engine", $"Session {sessionId} paused.");
        }
    }

    public void ResumeSession(string sessionId)
    {
        if (_pauseEvents.TryGetValue(sessionId, out var pauseEvent))
        {
            pauseEvent.Set();
            _sessionManager.UpdateSessionStatus(sessionId, ComputerUseSessionStatus.Running);
            var session = _sessionManager.GetSession(sessionId);
            if (session != null) _eventHub.PublishSessionStatus(session);
            _eventHub.PublishLog(ComputerUseLogLevel.Info, "Engine", $"Session {sessionId} resumed.");
        }
    }

    public void StopSession(string sessionId, string reason = "User stopped")
    {
        if (_sessionCancellation.TryGetValue(sessionId, out var cts))
        {
            try { cts.Cancel(); } catch { }
        }
        _sessionManager.UpdateSessionStatus(sessionId, ComputerUseSessionStatus.StoppedByUser, summary: reason);
        var session = _sessionManager.GetSession(sessionId);
        if (session != null) _eventHub.PublishSessionStatus(session);
        _eventHub.PublishLog(ComputerUseLogLevel.Info, "Engine", $"Session {sessionId} stopped: {reason}");
    }

    public void TriggerEmergencyStop()
    {
        _safetyGuard.TriggerEmergencyStop();
    }

    public void ConfirmSensitiveAction(string sessionId, bool approved)
    {
        if (_pendingConfirmations.TryGetValue(sessionId, out var tcs))
        {
            tcs.TrySetResult(approved);
        }
    }
}
