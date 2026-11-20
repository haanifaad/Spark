namespace OpenClaw.Shared.Browser;

/// <summary>
/// Real-time activity state of the automated browser session.
/// </summary>
public enum SparkBrowserActivityState
{
    Idle = 0,
    Navigating = 1,
    ExtractingData = 2,
    InteractingWithForm = 3,
    PreparingCheckout = 4,
    Paused = 5,
    UserTakeover = 6,
    Closed = 7
}

/// <summary>
/// Live snapshot of the browser session for desktop UI observation.
/// </summary>
public sealed class SparkBrowserSessionSnapshot
{
    public string SessionId { get; init; } = Guid.NewGuid().ToString("N");
    public string ActiveUrl { get; set; } = "about:blank";
    public string PageTitle { get; set; } = "New Tab";
    public SparkBrowserActivityState State { get; set; } = SparkBrowserActivityState.Idle;
    public string CurrentActionDescription { get; set; } = "Idle";
    public bool IsUserTakeoverActive { get; set; }
    public List<string> NavigationHistory { get; init; } = [];
    public DateTimeOffset LastActivityAt { get; set; } = DateTimeOffset.UtcNow;
    public string? LatestScreenshotBase64 { get; set; }
}

/// <summary>
/// Record of an action executed within the browser.
/// </summary>
public sealed class SparkBrowserActionRecord
{
    public string ActionId { get; init; } = Guid.NewGuid().ToString("N");
    public required string ActionType { get; init; } // Navigate, Click, Fill, Screenshot, Download
    public required string TargetUrl { get; init; }
    public string? SelectorOrDetails { get; init; }
    public bool Success { get; set; } = true;
    public DateTimeOffset ExecutedAt { get; init; } = DateTimeOffset.UtcNow;
}
