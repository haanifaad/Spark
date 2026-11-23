using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Commands;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;
using OpenClaw.Shared.Study.Infrastructure;
using OpenClaw.Shared.Study.Orchestration;
using OpenClaw.Shared.Study.Skills;
using OpenClaw.Shared.Study.Tools;
using Xunit;

namespace OpenClaw.Shared.Tests.Study;

public sealed class StudySecurityAndApprovalTests : IDisposable
{
    private readonly string _tempDir;
    private readonly StudyDataStore _dataStore;
    private readonly StudyPermissionManager _permissionManager;
    private readonly StudySkillRegistry _skillRegistry;
    private readonly StudyToolRegistry _toolRegistry;
    private readonly StudyCommandRegistry _commandRegistry;
    private readonly StudyCommandParser _commandParser;
    private readonly StudyExecutionPipeline _pipeline;

    public StudySecurityAndApprovalTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "spark_sec_tests_" + Guid.NewGuid().ToString("N"));
        _dataStore = new StudyDataStore(_tempDir);
        _permissionManager = new StudyPermissionManager();
        _skillRegistry = new StudySkillRegistry(_dataStore);
        _toolRegistry = new StudyToolRegistry(_dataStore, _permissionManager);
        _commandRegistry = new StudyCommandRegistry(_dataStore);
        _commandParser = new StudyCommandParser();

        _pipeline = new StudyExecutionPipeline(
            _skillRegistry,
            _toolRegistry,
            _commandRegistry,
            _commandParser,
            _permissionManager);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task SafeTools_WithReadOnlyPermission_ExecuteWithoutManualApproval()
    {
        var request = new StudyToolExecutionRequest(
            toolId: "tool-calculator",
            arguments: new Dictionary<string, object?> { ["expression"] = "2 + 2" },
            userId: "learner-safe"
        );

        var result = await _toolRegistry.ExecuteToolAsync(request);

        Assert.True(result.Success);
        Assert.Equal(ToolExecutionStatus.Completed, result.Status);
        Assert.Null(result.ApprovalRecord);
    }

    [Fact]
    public async Task SensitiveTools_RequiringHighPermission_RequireExplicitApproval()
    {
        // Register a sensitive dummy tool that requires CodeExecution
        var sensitiveTool = new StudyToolDefinition(
            id: "tool-sensitive-runner",
            name: "Sandbox Code Runner",
            description: "Runs arbitrary code in a sandbox",
            category: ToolCategory.Programming,
            version: "1.0.0",
            inputSchema: "{}",
            outputSchema: "{}",
            requiredPermission: StudyPermissionKind.CodeExecution
        );

        _toolRegistry.RegisterTool(sensitiveTool, new MockSensitiveToolHandler());

        var req = new StudyToolExecutionRequest(
            toolId: "tool-sensitive-runner",
            arguments: new Dictionary<string, object?> { ["code"] = "print('hello')" },
            userId: "learner-sec-1"
        );

        var result = await _toolRegistry.ExecuteToolAsync(req);

        // Security policy dictates fail-closed: requires user approval
        Assert.False(result.Success);
        Assert.Equal(ToolExecutionStatus.AwaitingApproval, result.Status);
        Assert.NotNull(result.ApprovalRecord);
        Assert.False(result.ApprovalRecord.IsApproved);
    }

    [Fact]
    public async Task ApprovalFlow_ApproveAndExecute_SuccessfullyExecutesPendingTool()
    {
        var sensitiveTool = new StudyToolDefinition(
            id: "tool-sensitive-eval",
            name: "Sensitive Evaluator",
            description: "Requires explicit user confirmation",
            category: ToolCategory.Programming,
            version: "1.0.0",
            inputSchema: "{}",
            outputSchema: "{}",
            requiredPermission: StudyPermissionKind.CodeExecution
        );

        _toolRegistry.RegisterTool(sensitiveTool, new MockSensitiveToolHandler());

        var req = new StudyToolExecutionRequest(
            toolId: "tool-sensitive-eval",
            arguments: new Dictionary<string, object?> { ["input"] = "test-payload" },
            userId: "learner-sec-2"
        );

        // 1. Initial execution attempt pauses at AwaitingApproval
        var pendingResult = await _toolRegistry.ExecuteToolAsync(req);
        Assert.Equal(ToolExecutionStatus.AwaitingApproval, pendingResult.Status);

        // 2. User approves the execution
        var approvedResult = await _toolRegistry.ApproveAndExecutePendingAsync(
            pendingResult.RequestId,
            approvedBy: "LearnerSupervisor",
            userId: "learner-sec-2"
        );

        Assert.True(approvedResult.Success);
        Assert.Equal(ToolExecutionStatus.Completed, approvedResult.Status);
        Assert.NotNull(approvedResult.ApprovalRecord);
        Assert.True(approvedResult.ApprovalRecord.IsApproved);
        Assert.Equal("LearnerSupervisor", approvedResult.ApprovalRecord.ApprovedBy);
    }

    [Fact]
    public async Task ApprovalFlow_RejectPending_CancelsToolExecution()
    {
        var sensitiveTool = new StudyToolDefinition(
            id: "tool-sensitive-reject",
            name: "Sensitive Tool for Rejection",
            description: "Will be rejected by user",
            category: ToolCategory.Programming,
            version: "1.0.0",
            inputSchema: "{}",
            outputSchema: "{}",
            requiredPermission: StudyPermissionKind.DestructiveOperation
        );

        _toolRegistry.RegisterTool(sensitiveTool, new MockSensitiveToolHandler());

        var req = new StudyToolExecutionRequest(
            toolId: "tool-sensitive-reject",
            arguments: new Dictionary<string, object?>(),
            userId: "learner-sec-3"
        );

        var pendingResult = await _toolRegistry.ExecuteToolAsync(req);
        Assert.Equal(ToolExecutionStatus.AwaitingApproval, pendingResult.Status);

        // User explicitly rejects
        var rejectedResult = await _toolRegistry.RejectPendingAsync(
            pendingResult.RequestId,
            reason: "User considered operation unsafe.",
            rejectedBy: "LearnerAdmin",
            userId: "learner-sec-3"
        );

        Assert.False(rejectedResult.Success);
        Assert.Equal(ToolExecutionStatus.Cancelled, rejectedResult.Status);
        Assert.NotNull(rejectedResult.ApprovalRecord);
        Assert.False(rejectedResult.ApprovalRecord.IsApproved);
        Assert.Contains("User considered operation unsafe", rejectedResult.ErrorMessage);
    }

    [Fact]
    public async Task DataStore_UserDirectoryIsolation_PreventsCrossUserAccess()
    {
        var skillUserA = new StudySkillDefinition(
            id: "skill-private-a",
            name: "User A Skill",
            description: "Private skill for User A",
            category: SkillCategory.Learning,
            version: "1.0.0",
            instructions: "Private instructions"
        );

        await _skillRegistry.RegisterSkillAsync(skillUserA, "user-alice");

        // Alice sees her skill
        var aliceSkill = await _skillRegistry.GetSkillAsync("skill-private-a", "user-alice");
        Assert.NotNull(aliceSkill);

        // Bob cannot see Alice's skill
        var bobSkill = await _skillRegistry.GetSkillAsync("skill-private-a", "user-bob");
        Assert.Null(bobSkill);
    }

    private sealed class MockSensitiveToolHandler : IStudyToolHandler
    {
        public Task<object?> InvokeAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken ct = default)
        {
            return Task.FromResult<object?>(new { message = "Sensitive operation executed safely." });
        }
    }
}
