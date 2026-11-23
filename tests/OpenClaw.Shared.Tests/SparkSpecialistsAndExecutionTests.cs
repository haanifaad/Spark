using OpenClaw.Shared.Browser;
using OpenClaw.Shared.Specialists.Email;
using OpenClaw.Shared.Specialists.Finance;
using OpenClaw.Shared.Specialists.Forms;
using OpenClaw.Shared.Specialists.Shopping;
using OpenClaw.Shared.Specialists.Travel;
using OpenClaw.Shared.Tools;
using OpenClaw.Shared.Tools.Reversal;
using OpenClaw.Shared.Verification;
using Xunit;

namespace OpenClaw.Shared.Tests;

public sealed class SparkSpecialistsAndExecutionTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SpendingLimitsEngine _limitsEngine;
    private readonly SparkReversalManager _reversalManager;
    private readonly SparkResultVerifier _verifier;

    public SparkSpecialistsAndExecutionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SparkSpecialistsTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _limitsEngine = new SpendingLimitsEngine(Path.Combine(_tempDir, "Finance"));
        _reversalManager = new SparkReversalManager(Path.Combine(_tempDir, "Backups"));
        _verifier = new SparkResultVerifier();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch
        {
            // Ignore temp cleanup errors
        }
    }

    [Fact]
    public async Task SpendingLimits_EnforcesPerTransactionAndDailyLimitsInBackend()
    {
        var config = new SparkSpendingLimitsConfig
        {
            MaxPerTransactionLimit = 2000.00m,
            DailySpendingLimit = 5000.00m,
            MonthlySpendingLimit = 20000.00m
        };
        await _limitsEngine.UpdateConfigAsync(config);

        // 1. Transaction exceeding per-transaction cap throws
        var ex1 = await Assert.ThrowsAsync<SpendingLimitExceededException>(() =>
            _limitsEngine.InitiateTransactionAsync("task-1", "Amazon India", "Shopping", 2500.00m));
        Assert.Equal("PerTransaction", ex1.LimitType);

        // 2. Transaction within per-transaction cap succeeds
        var tx1 = await _limitsEngine.InitiateTransactionAsync("task-1", "Amazon India", "Shopping", 1800.00m);
        Assert.Equal(SparkPaymentLifecycleStage.WaitingForUserAuthorization, tx1.Stage);
        Assert.Contains("upi://pay", tx1.UpiIntentString);

        // Mark tx1 as confirmed
        await _limitsEngine.UpdateTransactionStageAsync(tx1.TransactionId, SparkPaymentLifecycleStage.PaymentConfirmed, "prov_ref_001");

        // 3. Second transaction within limits
        var tx2 = await _limitsEngine.InitiateTransactionAsync("task-2", "IRCTC", "Travel", 1900.00m);
        await _limitsEngine.UpdateTransactionStageAsync(tx2.TransactionId, SparkPaymentLifecycleStage.PaymentConfirmed, "prov_ref_002");

        // Current today sum = 1800 + 1900 = 3700. Daily limit = 5000. Remaining = 1300.
        // 4. Third transaction of 1500 pushes today total to 5200 > 5000 -> throws Daily limit exception
        var ex2 = await Assert.ThrowsAsync<SpendingLimitExceededException>(() =>
            _limitsEngine.InitiateTransactionAsync("task-3", "Flipkart", "Shopping", 1500.00m));
        Assert.Equal("Daily", ex2.LimitType);
    }

    [Fact]
    public async Task ReversalManager_CreatesBackup_AndRestoresFileOnReversal()
    {
        var testFile = Path.Combine(_tempDir, "document.txt");
        await File.WriteAllTextAsync(testFile, "Original Content Line 1");

        var toolRegistry = new SparkToolRegistry();

        // Capture pre-execution state for fs_write_file
        var meta = await _reversalManager.CapturePreExecutionStateAsync(
            "fs_write_file",
            new Dictionary<string, object?> { ["path"] = testFile },
            SparkToolReversibility.Reversible);

        Assert.NotNull(meta.BackupFilePath);
        Assert.True(File.Exists(meta.BackupFilePath));

        // Overwrite original file
        await File.WriteAllTextAsync(testFile, "Mutated Destructive Content");
        Assert.Equal("Mutated Destructive Content", await File.ReadAllTextAsync(testFile));

        // Reversal restores original content
        var success = await _reversalManager.AttemptReversalAsync(meta.OperationId, toolRegistry);
        Assert.True(success);
        Assert.Equal("Original Content Line 1", await File.ReadAllTextAsync(testFile));
    }

    [Fact]
    public async Task EmailAgent_SearchesDraftsAndGeneratesProviderId()
    {
        var agent = new SparkEmailAgent();

        var searchResults = await agent.SearchEmailsAsync("Sprint");
        Assert.NotEmpty(searchResults);
        Assert.Contains(searchResults, e => e.Subject.Contains("Sprint"));

        var draft = await agent.PrepareDraftAsync(
            "colleague@spark.local",
            "Project Update",
            "The update has been compiled and verified.");
        Assert.True(draft.RequiresApproval);

        var providerId = await agent.SendApprovedEmailAsync(draft, "appr_123");
        Assert.StartsWith("sent_gmail_", providerId);
    }

    [Fact]
    public async Task TravelAgent_SearchesTrainsAndBooksWithVerifiedPnr()
    {
        var agent = new SparkTravelAgent();

        var trains = await agent.SearchTrainsAsync("Thiruvananthapuram", "Kochi", DateTime.Today.AddDays(1));
        Assert.NotEmpty(trains);

        var vandeBharat = Assert.Single(trains, t => t.RouteName.Contains("Vande Bharat"));
        Assert.True(vandeBharat.Price > 0);

        var booking = await agent.BookApprovedTravelAsync(vandeBharat, "appr_456");
        Assert.NotNull(booking.BookingReference);
        Assert.StartsWith("PNR_", booking.PnrOrTicketNumber);
    }

    [Fact]
    public async Task ShoppingAgent_SearchesAndCreatesHostedCheckoutViaLimits()
    {
        var shopping = new SparkShoppingAgent(_limitsEngine);

        var offers = await shopping.SearchAndCompareAsync("Wireless Earbuds");
        Assert.Equal(2, offers.Count);

        var cart = await shopping.PrepareCartAsync("Amazon India", [offers[0]]);
        Assert.Equal(1899.00m, cart.TotalAmount);

        var checkoutTx = await shopping.PrepareCheckoutSessionAsync("task-shop-1", cart);
        Assert.Equal(SparkPaymentLifecycleStage.WaitingForUserAuthorization, checkoutTx.Stage);
        Assert.NotNull(checkoutTx.HostedCheckoutUrl);
    }

    [Fact]
    public async Task BrowserWorker_TracksLiveActivity_RedactsSensitiveFields_AndManagesTakeover()
    {
        var browser = new SparkBrowserWorker();

        // 1. Navigation
        var snapshot = await browser.NavigateAsync("https://news.ycombinator.com");
        Assert.Equal("https://news.ycombinator.com", snapshot.ActiveUrl);
        Assert.Contains("https://news.ycombinator.com", snapshot.NavigationHistory);

        // 2. Sensitive Form Filling (password/PIN must be redacted in history)
        await browser.FillFieldAsync("passwordField", "secret_pass_123", isSensitive: true);
        var history = browser.GetActionHistory();
        var fillAction = Assert.Single(history, a => a.ActionType == "FillField");
        Assert.Contains("[REDACTED_SECRET]", fillAction.SelectorOrDetails);
        Assert.DoesNotContain("secret_pass_123", fillAction.SelectorOrDetails);

        // 3. User Takeover
        var takeoverSnap = await browser.InitiateUserTakeoverAsync();
        Assert.True(takeoverSnap.IsUserTakeoverActive);
        Assert.Equal(SparkBrowserActivityState.UserTakeover, takeoverSnap.State);

        // Automated actions should be blocked during takeover
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            browser.NavigateAsync("https://another-site.com"));

        // Release takeover
        var releasedSnap = await browser.ReleaseUserTakeoverAsync();
        Assert.False(releasedSnap.IsUserTakeoverActive);
        Assert.Equal(SparkBrowserActivityState.Idle, releasedSnap.State);
    }

    [Fact]
    public async Task ResultVerifier_ValidatesRealFilesAndProviderConfirmations()
    {
        // 1. Existing non-empty file -> verified
        var sampleFile = Path.Combine(_tempDir, "verified_report.txt");
        await File.WriteAllTextAsync(sampleFile, "Report verified content");

        var fileReport = await _verifier.VerifyFileCreationAsync(sampleFile);
        Assert.True(fileReport.IsVerifiedSuccessful);
        Assert.Equal(SparkConfidenceLevel.High, fileReport.Confidence);
        Assert.Equal(SparkEvidenceCategory.Verified, fileReport.EvidenceCategory);

        // 2. Non-existent file -> rejected
        var missingReport = await _verifier.VerifyFileCreationAsync(Path.Combine(_tempDir, "ghost.txt"));
        Assert.False(missingReport.IsVerifiedSuccessful);

        // 3. Email verification
        var emailReport = await _verifier.VerifyEmailSentAsync("sent_msg_999", "user@test.com");
        Assert.True(emailReport.IsVerifiedSuccessful);
        Assert.Equal("sent_msg_999", emailReport.VerifiedProviderReference);

        // 4. Booking verification
        var bookingReport = await _verifier.VerifyBookingConfirmationAsync("IRCTC", "BK_123", "PNR_987654321");
        Assert.True(bookingReport.IsVerifiedSuccessful);
        Assert.Equal("PNR_987654321", bookingReport.VerifiedProviderReference);

        // 5. Build verification
        var buildReport = await _verifier.VerifySoftwareBuildAsync(buildSucceeded: true, exitCode: 0, "Build succeeded with 0 warnings");
        Assert.True(buildReport.IsVerifiedSuccessful);
    }
}
