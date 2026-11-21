using System.Threading.Tasks;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Infrastructure;
using Xunit;

namespace OpenClaw.Shared.Tests.Study;

public sealed class StudyPermissionTests
{
    [Fact]
    public async Task ReadOnly_IsPermittedByDefaultWithoutUserPrompt()
    {
        var manager = new StudyPermissionManager();

        var decision = await manager.AuthorizeAsync(
            StudyPermissionKind.ReadOnly,
            "Course.cs-101",
            "Reading syllabus");

        Assert.True(decision.IsAllowed);
        Assert.False(decision.RequiredUserApproval);
    }

    [Fact]
    public async Task SensitiveOperations_RequireManualApprovalAndFailClosedByDefault()
    {
        var manager = new StudyPermissionManager();

        var codeDecision = await manager.AuthorizeAsync(
            StudyPermissionKind.CodeExecution,
            "Python.Sandbox.Eval",
            "Executing learner code script");

        Assert.False(codeDecision.IsAllowed, "Code execution must fail closed without approval.");
        Assert.True(codeDecision.RequiredUserApproval);

        var fileModDecision = await manager.AuthorizeAsync(
            StudyPermissionKind.FileModification,
            "Project/Main.cs",
            "Modifying project source file");

        Assert.False(fileModDecision.IsAllowed, "File modification must fail closed without approval.");
        Assert.True(fileModDecision.RequiredUserApproval);
    }

    [Fact]
    public async Task UserPromptHandler_WhenProvided_HonorsUserDecision()
    {
        var manager = new StudyPermissionManager
        {
            UserPromptHandler = (kind, desc) => Task.FromResult(true)
        };

        var decision = await manager.AuthorizeAsync(
            StudyPermissionKind.CodeExecution,
            "Sandbox.Run",
            "Executing practice lab");

        Assert.True(decision.IsAllowed);
        Assert.True(decision.RequiredUserApproval);
    }
}
