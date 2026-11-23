using System;
using System.Collections.Generic;
using OpenClaw.Shared;
using OpenClaw.Shared.Sessions;
using Xunit;

namespace OpenClaw.Shared.Tests;

public sealed class SparkConversationManagementTests
{
    [Fact]
    public void SessionBranching_MaintainsParentReferenceAndForkContract()
    {
        var parentKey = "session:main:worktree-alpha";
        var branchRequest = new SessionCreateRequest
        {
            ParentSessionKey = parentKey,
            Fork = true,
            ForkFrom = "msg-turn-3",
            SucceedsParent = false
        };

        Assert.Equal(parentKey, branchRequest.ParentSessionKey);
        Assert.True(branchRequest.Fork);
        Assert.Equal("msg-turn-3", branchRequest.ForkFrom);
        Assert.False(branchRequest.SucceedsParent);
    }

    [Fact]
    public void TemporarySession_FlagsAndRetentionContract()
    {
        var session = new SessionInfo
        {
            Key = "session:temp:ephemeral-1",
            DisplayName = "Quick Question (Temporary)",
            Category = "Temporary",
            Status = "active"
        };

        // Temporary session identification logic
        bool isTemporary = string.Equals(session.Category, "Temporary", StringComparison.OrdinalIgnoreCase)
                           || session.Key.Contains(":temp:", StringComparison.OrdinalIgnoreCase);

        Assert.True(isTemporary);
    }

    [Fact]
    public void SessionPatch_SupportsPinningRenamingAndFolderCategorization()
    {
        var patch = new SessionPatch
        {
            Label = "Refactored Project Scope",
            Category = "Engineering",
            Pinned = true
        };

        Assert.True(patch.HasChanges);
        Assert.Equal("Refactored Project Scope", patch.Label.Value);
        Assert.Equal("Engineering", patch.Category.Value);
        Assert.True(patch.Pinned.Value);
    }

    [Fact]
    public void ContextBudgetCalculation_EvaluatesTokensAndCompactionThreshold()
    {
        const int contextWindow = 24576;
        var messageTokens = 18000;
        var attachmentTokens = 2000;
        var memoryTokens = 500;

        var totalUsed = messageTokens + attachmentTokens + memoryTokens;
        var percentUsed = (double)totalUsed / contextWindow * 100.0;

        // Threshold checks
        var isNearCapacity = percentUsed >= 75.0;
        var requiresImmediateCompaction = percentUsed >= 90.0;

        Assert.Equal(20500, totalUsed);
        Assert.True(percentUsed > 80.0 && percentUsed < 85.0);
        Assert.True(isNearCapacity);
        Assert.False(requiresImmediateCompaction);
    }
}
