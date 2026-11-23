using System;
using OpenClaw.Shared.ComputerUse.Events;
using OpenClaw.Shared.ComputerUse.Types;
using Xunit;

namespace OpenClaw.Shared.Tests.ComputerUse;

public class ComputerUseEventHubTests
{
    private readonly ComputerUseEventHub _eventHub = new();

    [Fact]
    public void PublishSessionStatus_InvokesSubscribedHandler()
    {
        ComputerUseSession? receivedSession = null;

        _eventHub.SessionStatusChanged += (sender, session) =>
        {
            receivedSession = session;
        };

        var session = new ComputerUseSession
        {
            SessionId = "session-1",
            Status = ComputerUseSessionStatus.Running,
            Goal = "Test Goal"
        };

        _eventHub.PublishSessionStatus(session);

        Assert.NotNull(receivedSession);
        Assert.Equal("session-1", receivedSession.SessionId);
        Assert.Equal(ComputerUseSessionStatus.Running, receivedSession.Status);
    }

    [Fact]
    public void PublishLog_InvokesSubscribedHandler()
    {
        ComputerUseLogEntry? receivedLog = null;

        _eventHub.LogEmitted += (sender, log) =>
        {
            receivedLog = log;
        };

        _eventHub.PublishLog(ComputerUseLogLevel.Info, "Engine", "Test log message");

        Assert.NotNull(receivedLog);
        Assert.Equal(ComputerUseLogLevel.Info, receivedLog.Level);
        Assert.Equal("Engine", receivedLog.Source);
        Assert.Equal("Test log message", receivedLog.Message);
    }

    [Fact]
    public void PublishActionExecuting_InvokesSubscribedHandler()
    {
        ComputerUseAction? executingAction = null;

        _eventHub.ActionExecuting += (sender, action) =>
        {
            executingAction = action;
        };

        var action = new ComputerUseAction
        {
            ActionType = ComputerUseActionType.Click,
            StartBox = new BoundingBox(50, 50, 50, 50)
        };

        _eventHub.PublishActionExecuting(action);

        Assert.NotNull(executingAction);
        Assert.Equal(ComputerUseActionType.Click, executingAction.ActionType);
    }
}
