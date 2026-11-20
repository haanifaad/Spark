namespace OpenClaw.Shared.Verification;

/// <summary>
/// Contract for post-execution verification proving real-world outcome success rather than relying on model assertions.
/// </summary>
public interface IResultVerifier
{
    Task<SparkVerificationReport> VerifyFileCreationAsync(string filePath, CancellationToken ct = default);
    Task<SparkVerificationReport> VerifyEmailSentAsync(string providerMessageId, string recipient, CancellationToken ct = default);
    Task<SparkVerificationReport> VerifyBookingConfirmationAsync(string provider, string bookingReference, string pnrOrTicket, CancellationToken ct = default);
    Task<SparkVerificationReport> VerifyFinancialTransactionAsync(string transactionId, string providerRef, decimal expectedAmount, string currency = "INR", CancellationToken ct = default);
    Task<SparkVerificationReport> VerifySoftwareBuildAsync(bool buildSucceeded, int exitCode, string outputSummary, CancellationToken ct = default);
}
