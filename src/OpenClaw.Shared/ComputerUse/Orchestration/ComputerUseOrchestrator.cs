using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.ComputerUse.Engine;
using OpenClaw.Shared.ComputerUse.Events;
using OpenClaw.Shared.ComputerUse.Sessions;
using OpenClaw.Shared.ComputerUse.Types;

namespace OpenClaw.Shared.ComputerUse.Orchestration;

/// <summary>
/// Autonomous agent orchestrator coordinating high-level task decomposition, loop monitoring,
/// dynamic replanning upon unexpected UI state changes, and final task reporting.
/// </summary>
public sealed class ComputerUseOrchestrator
{
    private readonly IComputerUseEngine _engine;
    private readonly IComputerUseSessionManager _sessionManager;
    private readonly IComputerUseEventHub _eventHub;
    private readonly IOpenClawLogger? _logger;

    public ComputerUseOrchestrator(
        IComputerUseEngine engine,
        IComputerUseSessionManager sessionManager,
        IComputerUseEventHub eventHub,
        IOpenClawLogger? logger = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _eventHub = eventHub ?? throw new ArgumentNullException(nameof(eventHub));
        _logger = logger;
    }

    /// <summary>
    /// Executes a complete autonomous computer-use task with automated replanning and final reporting.
    /// </summary>
    public async Task<string> RunTaskWithReportAsync(
        string goal,
        ComputerUseSessionOptions? options = null,
        CancellationToken ct = default)
    {
        var session = await _engine.StartSessionAsync(goal, options, ct).ConfigureAwait(false);
        int consecutiveUnchangedSteps = 0;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (session.Status is ComputerUseSessionStatus.Completed or ComputerUseSessionStatus.Failed or ComputerUseSessionStatus.StoppedByUser)
                {
                    break;
                }

                if (session.Steps.Count >= session.Options.MaxSteps)
                {
                    _engine.StopSession(session.SessionId, "Maximum step budget exceeded.");
                    break;
                }

                var step = await _engine.ExecuteStepAsync(session.SessionId, ct).ConfigureAwait(false);
                if (step == null) break;

                // Monitor verification for stuck loops
                if (step.Verification?.Status == VerificationStatus.ScreenUnchanged)
                {
                    consecutiveUnchangedSteps++;
                    if (consecutiveUnchangedSteps >= 3)
                    {
                        _eventHub.PublishLog(ComputerUseLogLevel.Warning, "Orchestrator",
                            "Action executed 3 consecutive times with no perceptible UI change. Requesting replanning.");
                        consecutiveUnchangedSteps = 0;
                    }
                }
                else
                {
                    consecutiveUnchangedSteps = 0;
                }

                if (session.Options.StepByStepMode)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _engine.StopSession(session.SessionId, "Task cancelled by user.");
        }

        return GenerateTaskReport(session);
    }

    /// <summary>
    /// Produces a detailed Markdown execution report summarizing actions, metrics, and outcomes.
    /// </summary>
    public string GenerateTaskReport(ComputerUseSession session)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# SPARK Computer Use Task Report");
        sb.AppendLine($"**Task Goal:** {session.Goal}");
        sb.AppendLine($"**Session ID:** `{session.SessionId}`");
        sb.AppendLine($"**Status:** {session.Status}");
        sb.AppendLine($"**Duration:** {session.ElapsedTime.TotalSeconds:F1}s");
        sb.AppendLine($"**Total Steps:** {session.Steps.Count}");
        sb.AppendLine($"**Tokens Consumed:** {session.TotalTokensUsed}");
        if (!string.IsNullOrEmpty(session.ActiveModel))
        {
            sb.AppendLine($"**Model:** `{session.ActiveModel}`");
        }
        if (!string.IsNullOrEmpty(session.ErrorMessage))
        {
            sb.AppendLine($"**Error:** {session.ErrorMessage}");
        }
        if (!string.IsNullOrEmpty(session.ResultSummary))
        {
            sb.AppendLine($"**Result Summary:** {session.ResultSummary}");
        }

        sb.AppendLine();
        sb.AppendLine("## Execution Timeline");
        sb.AppendLine("| Step | Thought | Action | Result | Verification | Latency |");
        sb.AppendLine("| :--- | :--- | :--- | :--- | :--- | :--- |");

        foreach (var step in session.Steps)
        {
            string thought = (step.Thought ?? "").Replace("|", "\\|").Replace("\n", " ");
            if (thought.Length > 60) thought = thought[..57] + "...";

            string action = (step.Action?.ToString() ?? "None").Replace("|", "\\|");
            if (action.Length > 40) action = action[..37] + "...";

            string result = (step.ExecutionOutput?.Message ?? "").Replace("|", "\\|");
            if (result.Length > 30) result = result[..27] + "...";

            string ver = step.Verification?.Status.ToString() ?? "N/A";
            sb.AppendLine($"| {step.StepIndex} | {thought} | `{action}` | {result} | {ver} | {step.StepDurationMs:F0}ms |");
        }

        return sb.ToString();
    }
}
