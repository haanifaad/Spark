using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenClaw.Shared.Divisions;
using OpenClaw.Shared.VoiceMode;
using Xunit;

namespace OpenClaw.Shared.Tests.VoiceModeTests;

public class VoiceModeIntegrationTests
{
    private sealed class FakeDivisionManager : IDivisionManager
    {
        public SparkDivision ActiveDivision { get; set; } = SparkDivision.Chat;
        public DivisionMetadata ActiveDivisionMetadata => DivisionCatalog.GetMetadata(ActiveDivision);
        public IReadOnlyList<DivisionMetadata> AvailableDivisions => DivisionCatalog.AllDivisions;

#pragma warning disable CS0067
        public event EventHandler<DivisionChangingEventArgs>? DivisionChanging;
#pragma warning restore CS0067
        public event EventHandler<DivisionChangedEventArgs>? DivisionChanged;

        public bool IsDivisionAvailable(SparkDivision division) => true;
        public void RegisterNavigationHandler(Func<SparkDivision, object?, Task<bool>> handler) { }
        public Task<bool> SwitchDivisionAsync(SparkDivision targetDivision, object? parameter = null)
        {
            var prev = ActiveDivision;
            ActiveDivision = targetDivision;
            DivisionChanged?.Invoke(this, new DivisionChangedEventArgs(prev, targetDivision, DivisionCatalog.GetMetadata(targetDivision)));
            return Task.FromResult(true);
        }
        public void NotifyDivisionActive(SparkDivision division)
        {
            var prev = ActiveDivision;
            ActiveDivision = division;
            DivisionChanged?.Invoke(this, new DivisionChangedEventArgs(prev, division, DivisionCatalog.GetMetadata(division)));
        }
        public void SetDivisionState(SparkDivision division, object state) { }
        public T? GetDivisionState<T>(SparkDivision division) where T : class => null;
    }

    [Fact]
    public void DivisionCatalog_ContainsVoiceMode_AsSixthDivision()
    {
        Assert.Equal(6, DivisionCatalog.AllDivisions.Count);
        Assert.True(DivisionCatalog.TryGetDivision("voice-mode", out var div));
        Assert.Equal(SparkDivision.VoiceMode, div);
        Assert.Equal("Voice Mode", DivisionCatalog.VoiceMode.DisplayName);
        Assert.Equal("voice-mode", DivisionCatalog.VoiceMode.Key);
        Assert.True(DivisionCatalog.VoiceMode.IsActive);
    }

    [Fact]
    public void CapabilityGuard_Rejects_WhenDivisionIsNotVoiceMode()
    {
        var divMgr = new FakeDivisionManager { ActiveDivision = SparkDivision.Chat };
        var guard = new VoiceModeCapabilityGuard(divMgr);

        Assert.False(guard.IsVoiceModeActive());
        Assert.False(guard.CanExecute("screen_capture", out var reason));
        Assert.NotNull(reason);
        Assert.Contains("requires activeDivision === 'voice-mode'", reason);

        Assert.Throws<InvalidOperationException>(() => guard.AssertVoiceModeActive("open_app"));
    }

    [Fact]
    public void CapabilityGuard_Permits_WhenDivisionIsVoiceMode()
    {
        var divMgr = new FakeDivisionManager { ActiveDivision = SparkDivision.VoiceMode };
        var guard = new VoiceModeCapabilityGuard(divMgr);

        Assert.True(guard.IsVoiceModeActive());
        Assert.True(guard.CanExecute("screen_capture", out var reason));
        Assert.Null(reason);
        guard.AssertVoiceModeActive("open_app"); // Should not throw
    }

    [Fact]
    public async Task VoiceModeDivisionService_EnforcesGuard_AcrossDivisions()
    {
        var divMgr = new FakeDivisionManager { ActiveDivision = SparkDivision.Work };
        using var service = new VoiceModeDivisionService(divMgr);

        var result = await service.SendCommandAsync("ping");
        Assert.False(result.Success);
        Assert.Contains("requires activeDivision === 'voice-mode'", result.Error);
    }

    [Fact]
    public async Task VoiceModeDivisionService_AllowsExecution_WhenActiveDivisionIsVoiceMode()
    {
        var divMgr = new FakeDivisionManager { ActiveDivision = SparkDivision.VoiceMode };
        using var service = new VoiceModeDivisionService(divMgr);

        var result = await service.SendCommandAsync("ping");
        Assert.True(result.Success);
        Assert.Contains("pong", result.Result ?? result.RawJson);
    }
}
