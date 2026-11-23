using System;
using System.Linq;
using OpenClaw.Shared.ComputerUse.Sessions;
using OpenClaw.Shared.ComputerUse.Types;
using Xunit;

namespace OpenClaw.Shared.Tests.ComputerUse;

public class ComputerUseSessionManagerTests
{
    private readonly ComputerUseSessionManager _sessionManager = new();

    [Fact]
    public void CreateSession_InitializesWithRunningOrInitializingStatus()
    {
        var session = _sessionManager.CreateSession("Open browser and search for SPARK");

        Assert.NotNull(session);
        Assert.False(string.IsNullOrWhiteSpace(session.SessionId));
        Assert.Equal("Open browser and search for SPARK", session.Goal);
        Assert.Equal(ComputerUseSessionStatus.Initializing, session.Status);
        Assert.Empty(session.Steps);
    }

    [Fact]
    public void AddStep_AppendsStepAndUpdatesSession()
    {
        var session = _sessionManager.CreateSession("Test Task");

        var step = new ComputerUseStep
        {
            StepIndex = 1,
            Action = new ComputerUseAction
            {
                ActionType = ComputerUseActionType.Click,
                StartBox = new BoundingBox(100, 200, 100, 200)
            },
            Verification = new VerificationResult(
                VerificationStatus.ConfirmedSuccess,
                0.15,
                "Clicked coordinate",
                false)
        };

        _sessionManager.AddStep(session.SessionId, step);

        var retrieved = _sessionManager.GetSession(session.SessionId);
        Assert.NotNull(retrieved);
        Assert.Single(retrieved.Steps);
        Assert.Equal(1, retrieved.Steps[0].StepIndex);
        Assert.Equal(ComputerUseActionType.Click, retrieved.Steps[0].Action?.ActionType);
    }

    [Fact]
    public void UpdateSessionStatus_SetsTerminalStateAndSummary()
    {
        var session = _sessionManager.CreateSession("Quick task");

        _sessionManager.UpdateSessionStatus(
            session.SessionId,
            ComputerUseSessionStatus.Completed,
            summary: "Successfully completed");

        var retrieved = _sessionManager.GetSession(session.SessionId);
        Assert.NotNull(retrieved);
        Assert.Equal(ComputerUseSessionStatus.Completed, retrieved.Status);
        Assert.NotNull(retrieved.EndTime);
        Assert.Equal("Successfully completed", retrieved.ResultSummary);
    }

    [Fact]
    public void ListSessions_ReturnsAllCreatedSessions()
    {
        var s1 = _sessionManager.CreateSession("Task 1");
        var s2 = _sessionManager.CreateSession("Task 2");

        var list = _sessionManager.ListSessions().ToList();

        Assert.Contains(list, s => s.SessionId == s1.SessionId);
        Assert.Contains(list, s => s.SessionId == s2.SessionId);
    }
}
