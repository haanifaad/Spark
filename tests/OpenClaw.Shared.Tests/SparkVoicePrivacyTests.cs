using System;
using System.Threading.Tasks;
using OpenClaw.Shared.Security;
using Xunit;

namespace OpenClaw.Shared.Tests;

public class SparkVoicePrivacyTests
{
    [Fact]
    public void Permission_DefaultsToNotDetermined_WhenNotDefaultGranted()
    {
        var service = new SparkVoicePrivacyService(defaultGranted: false);
        Assert.Equal(SparkPermissionState.NotDetermined, service.GetPermission(SparkMediaDeviceType.Microphone));
        Assert.Equal(SparkPermissionState.NotDetermined, service.GetPermission(SparkMediaDeviceType.Camera));
        Assert.Equal(SparkPermissionState.NotDetermined, service.GetPermission(SparkMediaDeviceType.ScreenCapture));
    }

    [Fact]
    public async Task RequestPermission_GrantsPermission_AndAllowsEnsure()
    {
        var service = new SparkVoicePrivacyService(defaultGranted: false);
        var result = await service.RequestPermissionAsync(SparkMediaDeviceType.Microphone);

        Assert.Equal(SparkPermissionState.Granted, result);
        Assert.Equal(SparkPermissionState.Granted, service.GetPermission(SparkMediaDeviceType.Microphone));

        // Should not throw
        service.EnsurePermission(SparkMediaDeviceType.Microphone);
    }

    [Fact]
    public void EnsurePermission_Throws_WhenDeniedOrNotDetermined()
    {
        var service = new SparkVoicePrivacyService(defaultGranted: false);

        var exNotDetermined = Assert.Throws<SparkPrivacyException>(() =>
            service.EnsurePermission(SparkMediaDeviceType.Camera));
        Assert.Equal(SparkMediaDeviceType.Camera, exNotDetermined.DeviceType);
        Assert.Equal(SparkPermissionState.NotDetermined, exNotDetermined.State);

        service.SetPermission(SparkMediaDeviceType.Camera, SparkPermissionState.Denied);
        var exDenied = Assert.Throws<SparkPrivacyException>(() =>
            service.EnsurePermission(SparkMediaDeviceType.Camera));
        Assert.Equal(SparkPermissionState.Denied, exDenied.State);
    }

    [Fact]
    public void SetDeviceActive_Throws_WhenPermissionNotGranted()
    {
        var service = new SparkVoicePrivacyService(defaultGranted: false);
        service.SetPermission(SparkMediaDeviceType.Microphone, SparkPermissionState.Denied);

        Assert.Throws<SparkPrivacyException>(() =>
            service.SetDeviceActive(SparkMediaDeviceType.Microphone, true));
        Assert.False(service.IsMicrophoneActive);
    }

    [Fact]
    public void SetDeviceActive_FiresEvent_WhenStateChanges()
    {
        var service = new SparkVoicePrivacyService(defaultGranted: true);
        SparkMediaDeviceType? notified = null;
        service.DeviceActivityChanged += (_, type) => notified = type;

        service.SetDeviceActive(SparkMediaDeviceType.Camera, true);

        Assert.True(service.IsCameraActive);
        Assert.Equal(SparkMediaDeviceType.Camera, notified);

        service.SetDeviceActive(SparkMediaDeviceType.Camera, false);
        Assert.False(service.IsCameraActive);
    }

    [Fact]
    public void SanitizeLog_RedactsSensitiveTokensAndKeys()
    {
        var service = new SparkVoicePrivacyService();

        var logWithBearer = "Request headers: Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.payload.sig";
        var sanitizedBearer = service.SanitizeLog(logWithBearer);
        Assert.Contains("Bearer [REDACTED_TOKEN]", sanitizedBearer);
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9", sanitizedBearer);

        var logWithOpenAi = "Connecting to OpenAI realtime with key sk-proj-1234567890abcdef12345678";
        var sanitizedOpenAi = service.SanitizeLog(logWithOpenAi);
        Assert.Contains("sk-***", sanitizedOpenAi);
        Assert.DoesNotContain("sk-proj-1234567890abcdef12345678", sanitizedOpenAi);

        var logWithGoogle = "Connecting to Gemini live with key AIzaSyD9876543210abcdefghijklmnop123456";
        var sanitizedGoogle = service.SanitizeLog(logWithGoogle);
        Assert.Contains("AIzaSy***", sanitizedGoogle);
        Assert.DoesNotContain("AIzaSyD9876543210abcdefghijklmnop123456", sanitizedGoogle);

        var jsonWithSecret = "{\"token\": \"secret_value_12345\", \"user\": \"spark\"}";
        var sanitizedJson = service.SanitizeLog(jsonWithSecret);
        Assert.Contains("\"token\": \"[REDACTED]\"", sanitizedJson);
        Assert.Contains("\"user\": \"spark\"", sanitizedJson);
    }

    [Fact]
    public void SanitizeUrl_RedactsSensitiveQueryParams()
    {
        var service = new SparkVoicePrivacyService();

        var url = "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1alpha.GenerativeService.BidiGenerateContent?key=AIzaSySecret123&client=desktop";
        var sanitized = service.SanitizeUrl(url);

        Assert.Contains("key=[REDACTED]", sanitized);
        Assert.DoesNotContain("AIzaSySecret123", sanitized);
        Assert.Contains("&client=desktop", sanitized);
    }

    [Fact]
    public void VerifyZeroRetentionPolicy_EnforcedByDefault()
    {
        var service = new SparkVoicePrivacyService();
        Assert.True(service.VerifyZeroRetentionPolicy());

        var debugService = new SparkVoicePrivacyService(allowDiskRetentionForDebug: true);
        Assert.False(debugService.VerifyZeroRetentionPolicy());
    }
}
