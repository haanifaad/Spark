using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenClaw.Shared.Divisions;
using Xunit;

namespace OpenClaw.Shared.Tests;

public sealed class DivisionManagerTests
{
    [Fact]
    public void DivisionManager_InitialState_DefaultsToChat()
    {
        var manager = new DivisionManager();

        Assert.Equal(SparkDivision.Chat, manager.ActiveDivision);
        Assert.Equal("Chat", manager.ActiveDivisionMetadata.DisplayName);
        Assert.Equal("chat", manager.ActiveDivisionMetadata.Key);
        Assert.True(manager.ActiveDivisionMetadata.IsActive);
        Assert.Equal(5, manager.AvailableDivisions.Count);
    }

    [Theory]
    [InlineData(SparkDivision.Chat)]
    [InlineData(SparkDivision.Work)]
    [InlineData(SparkDivision.Code)]
    [InlineData(SparkDivision.Study)]
    [InlineData(SparkDivision.Creative)]
    public void DivisionManager_IsDivisionAvailable_AllFiveDivisionsAreAvailable(SparkDivision division)
    {
        var manager = new DivisionManager();
        Assert.True(manager.IsDivisionAvailable(division));
    }

    [Theory]
    [InlineData(SparkDivision.Chat)]
    [InlineData(SparkDivision.Work)]
    [InlineData(SparkDivision.Code)]
    [InlineData(SparkDivision.Study)]
    [InlineData(SparkDivision.Creative)]
    public async Task DivisionManager_SwitchDivisionAsync_SwitchesToTargetDivision(SparkDivision target)
    {
        var manager = new DivisionManager();
        var navigatedTarget = (SparkDivision?)null;
        manager.RegisterNavigationHandler((division, _) =>
        {
            navigatedTarget = division;
            return Task.FromResult(true);
        });

        var changedFired = false;
        manager.DivisionChanged += (_, args) =>
        {
            if (args.ActiveDivision == target)
                changedFired = true;
        };

        var result = await manager.SwitchDivisionAsync(target);

        Assert.True(result);
        Assert.Equal(target, manager.ActiveDivision);
        if (target != SparkDivision.Chat)
        {
            Assert.Equal(target, navigatedTarget);
            Assert.True(changedFired);
        }
    }

    [Fact]
    public async Task DivisionManager_SwitchDivisionAsync_TriggersChangingAndAllowsCancellation()
    {
        var manager = new DivisionManager();
        var changingFired = false;
        var changedFired = false;

        manager.DivisionChanging += (_, args) =>
        {
            changingFired = true;
            args.Cancel = true; // Cancel transition
        };

        manager.DivisionChanged += (_, _) => changedFired = true;

        var result = await manager.SwitchDivisionAsync(SparkDivision.Work);

        Assert.False(result);
        Assert.True(changingFired);
        Assert.False(changedFired);
        Assert.Equal(SparkDivision.Chat, manager.ActiveDivision);
    }

    [Fact]
    public void DivisionManager_StatePreservation_StoresAndRetrievesTypedState()
    {
        var manager = new DivisionManager();

        manager.SetDivisionState(SparkDivision.Chat, "session-alpha-123");
        manager.SetDivisionState(SparkDivision.Work, @"C:\Intel\Spark\Spark-main");
        manager.SetDivisionState(SparkDivision.Code, new { TaskId = 42, Plan = "Refactor" });

        Assert.Equal("session-alpha-123", manager.GetDivisionState<string>(SparkDivision.Chat));
        Assert.Equal(@"C:\Intel\Spark\Spark-main", manager.GetDivisionState<string>(SparkDivision.Work));
        Assert.Null(manager.GetDivisionState<string>(SparkDivision.Study));
    }

    [Fact]
    public void DivisionManager_NotifyDivisionActive_UpdatesWithoutNavigationHandler()
    {
        var manager = new DivisionManager();
        var changedFired = false;

        manager.DivisionChanged += (_, args) =>
        {
            if (args.ActiveDivision == SparkDivision.Code)
                changedFired = true;
        };

        manager.NotifyDivisionActive(SparkDivision.Code);

        Assert.Equal(SparkDivision.Code, manager.ActiveDivision);
        Assert.True(changedFired);
    }

    [Fact]
    public async Task DivisionManager_RapidSwitching_MaintainsConsistency()
    {
        var manager = new DivisionManager();
        var divisions = new[]
        {
            SparkDivision.Work,
            SparkDivision.Code,
            SparkDivision.Study,
            SparkDivision.Creative,
            SparkDivision.Chat,
            SparkDivision.Work
        };

        foreach (var div in divisions)
        {
            var success = await manager.SwitchDivisionAsync(div);
            Assert.True(success);
            Assert.Equal(div, manager.ActiveDivision);
        }

        Assert.Equal(SparkDivision.Work, manager.ActiveDivision);
    }
}
