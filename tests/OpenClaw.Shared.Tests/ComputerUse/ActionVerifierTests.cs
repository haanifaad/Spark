using System;
using System.Threading.Tasks;
using OpenClaw.Shared.ComputerUse.Types;
using OpenClaw.Shared.ComputerUse.Verification;
using Xunit;

namespace OpenClaw.Shared.Tests.ComputerUse;

public class ActionVerifierTests
{
    private readonly ActionVerifier _verifier = new();

    [Fact]
    public void ComputeVisualDelta_IdenticalStrings_ReturnsZero()
    {
        var base64 = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
        var delta = _verifier.ComputeVisualDelta(base64, base64);
        Assert.Equal(0.0, delta);
    }

    [Fact]
    public void ComputeVisualDelta_DifferentStrings_ReturnsNonZero()
    {
        var b1 = Convert.ToBase64String(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
        var b2 = Convert.ToBase64String(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 });
        var delta = _verifier.ComputeVisualDelta(b1, b2);
        Assert.True(delta > 0.1);
    }

    [Fact]
    public async Task VerifyStepAsync_FinishedAction_AlwaysSucceeds()
    {
        var step = new ComputerUseStep
        {
            StepIndex = 1,
            Action = new ComputerUseAction { ActionType = ComputerUseActionType.Finished }
        };
        var before = new PerceptionSnapshot { Timestamp = DateTimeOffset.UtcNow };
        var after = new PerceptionSnapshot { Timestamp = DateTimeOffset.UtcNow };

        var result = await _verifier.VerifyStepAsync(step, before, after);

        Assert.Equal(VerificationStatus.ConfirmedSuccess, result.Status);
        Assert.False(result.RequiresReplan);
        Assert.Contains("Terminal action", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyStepAsync_ActiveWindowChange_ReturnsStateChanged()
    {
        var step = new ComputerUseStep
        {
            StepIndex = 1,
            Action = new ComputerUseAction { ActionType = ComputerUseActionType.Click }
        };
        var before = new PerceptionSnapshot
        {
            ActiveWindowTitle = "Browser - Tab 1",
            ScreenshotBase64 = "AAAA"
        };
        var after = new PerceptionSnapshot
        {
            ActiveWindowTitle = "Browser - Tab 2",
            ScreenshotBase64 = "BBBB"
        };

        var result = await _verifier.VerifyStepAsync(step, before, after);

        Assert.Equal(VerificationStatus.StateChanged, result.Status);
        Assert.False(result.RequiresReplan);
        Assert.Contains("Active window changed", result.Description);
    }

    [Fact]
    public async Task VerifyStepAsync_ClickWithoutVisualChange_ReturnsScreenUnchanged()
    {
        var imgBase64 = Convert.ToBase64String(new byte[100]);
        var step = new ComputerUseStep
        {
            StepIndex = 1,
            Action = new ComputerUseAction { ActionType = ComputerUseActionType.Click }
        };
        var before = new PerceptionSnapshot
        {
            ActiveWindowTitle = "App",
            ScreenshotBase64 = imgBase64
        };
        var after = new PerceptionSnapshot
        {
            ActiveWindowTitle = "App",
            ScreenshotBase64 = imgBase64
        };

        var result = await _verifier.VerifyStepAsync(step, before, after);

        Assert.Equal(VerificationStatus.ScreenUnchanged, result.Status);
        Assert.True(result.RequiresReplan);
    }
}
