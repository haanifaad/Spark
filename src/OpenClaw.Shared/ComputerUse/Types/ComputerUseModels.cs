using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.Json.Serialization;

namespace OpenClaw.Shared.ComputerUse.Types;

/// <summary>
/// Status of an active or completed Computer Use session.
/// </summary>
public enum ComputerUseSessionStatus
{
    Initializing,
    Running,
    Paused,
    AwaitingApproval,
    Completed,
    Failed,
    StoppedByUser
}

/// <summary>
/// Supported GUI automation actions, compatible with UI-TARS and SPARK desktop control.
/// </summary>
public enum ComputerUseActionType
{
    Click,
    DoubleClick,
    RightClick,
    MiddleClick,
    Drag,
    MouseMove,
    TypeText,
    KeyPress,
    KeyRelease,
    Hotkey,
    Scroll,
    Wait,
    CallUser,
    Finished,
    UserStop,
    Unknown
}

/// <summary>
/// Direction for mouse scrolling.
/// </summary>
public enum ScrollDirection
{
    Down,
    Up,
    Left,
    Right
}

/// <summary>
/// A normalized bounding box (0..1000) indicating an element region on screen.
/// Adapted from UI-TARS normalized coordinate conventions.
/// </summary>
public sealed record BoundingBox(double X1, double Y1, double X2, double Y2)
{
    public double CenterX => (X1 + X2) / 2.0;
    public double CenterY => (Y1 + Y2) / 2.0;
    public double Width => Math.Max(0.0, X2 - X1);
    public double Height => Math.Max(0.0, Y2 - Y1);

    /// <summary>
    /// De-quantizes and converts normalized coordinates [0..1000] to logical desktop pixels.
    /// </summary>
    public Point ToLogicalPoint(int screenWidth, int screenHeight)
    {
        int x = (int)Math.Round((CenterX / 1000.0) * screenWidth);
        int y = (int)Math.Round((CenterY / 1000.0) * screenHeight);
        return new Point(Math.Clamp(x, 0, Math.Max(0, screenWidth - 1)), Math.Clamp(y, 0, Math.Max(0, screenHeight - 1)));
    }

    /// <summary>
    /// Converts normalized bounding box [0..1000] to a logical screen Rectangle.
    /// </summary>
    public Rectangle ToLogicalRectangle(int screenWidth, int screenHeight)
    {
        int x1 = (int)Math.Round((X1 / 1000.0) * screenWidth);
        int y1 = (int)Math.Round((Y1 / 1000.0) * screenHeight);
        int x2 = (int)Math.Round((X2 / 1000.0) * screenWidth);
        int y2 = (int)Math.Round((Y2 / 1000.0) * screenHeight);
        return new Rectangle(x1, y1, Math.Max(1, x2 - x1), Math.Max(1, y2 - y1));
    }
}

/// <summary>
/// Represents a discrete 2D screen coordinate.
/// </summary>
public sealed record ScreenCoordinate(int X, int Y, bool IsLogical = true);

/// <summary>
/// High-level semantic UI element observed through Windows UI Automation or visual grounding.
/// </summary>
public sealed record PerceptionElement
{
    public string Role { get; init; } = "unknown";
    public string Name { get; init; } = string.Empty;
    public string? Value { get; init; }
    public Rectangle Bounds { get; init; }
    public bool IsEnabled { get; init; } = true;
    public bool IsFocused { get; init; }
    public string Source { get; init; } = "uia"; // "uia", "vision", "ocr"

    public Point Center => new(Bounds.X + Bounds.Width / 2, Bounds.Y + Bounds.Height / 2);
}

/// <summary>
/// Complete multimodal perception snapshot of the user's desktop at a point in time.
/// </summary>
public sealed class PerceptionSnapshot
{
    public int Width { get; init; }
    public int Height { get; init; }
    public double ScaleFactor { get; init; } = 1.0;
    public string ScreenshotBase64 { get; init; } = string.Empty;
    public string? ActiveWindowTitle { get; init; }
    public IReadOnlyList<PerceptionElement> Elements { get; init; } = Array.Empty<PerceptionElement>();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Discrete executable GUI action parsed from VLM prediction or planner.
/// </summary>
public sealed class ComputerUseAction
{
    public ComputerUseActionType ActionType { get; init; } = ComputerUseActionType.Unknown;
    public Dictionary<string, string> RawInputs { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public BoundingBox? StartBox { get; init; }
    public BoundingBox? EndBox { get; init; }
    public string? TextContent { get; init; }
    public string? Key { get; init; }
    public ScrollDirection Direction { get; init; } = ScrollDirection.Down;
    public int ScrollAmount { get; init; } = 500;
    public PerceptionElement? TargetElement { get; set; }
    public string? Thought { get; init; }
    public string? Reflection { get; init; }

    public override string ToString() => ActionType switch
    {
        ComputerUseActionType.Click => $"click(start_box='[{StartBox?.X1},{StartBox?.Y1},{StartBox?.X2},{StartBox?.Y2}]')",
        ComputerUseActionType.DoubleClick => $"left_double(start_box='[{StartBox?.X1},{StartBox?.Y1},{StartBox?.X2},{StartBox?.Y2}]')",
        ComputerUseActionType.RightClick => $"right_single(start_box='[{StartBox?.X1},{StartBox?.Y1},{StartBox?.X2},{StartBox?.Y2}]')",
        ComputerUseActionType.Drag => $"drag(start_box='[{StartBox?.X1},{StartBox?.Y1}]', end_box='[{EndBox?.X1},{EndBox?.Y1}]')",
        ComputerUseActionType.TypeText => $"type(content='{TextContent?.Replace("\n", "\\n")}')",
        ComputerUseActionType.Hotkey => $"hotkey(key='{Key}')",
        ComputerUseActionType.Scroll => $"scroll(direction='{Direction.ToString().ToLowerInvariant()}')",
        ComputerUseActionType.Wait => "wait()",
        ComputerUseActionType.Finished => "finished()",
        ComputerUseActionType.CallUser => "call_user()",
        _ => $"{ActionType}()"
    };
}

/// <summary>
/// Parsed VLM prediction output containing thought chain and actions.
/// </summary>
public sealed record PredictionOutput(
    string Thought,
    string? Reflection,
    IReadOnlyList<ComputerUseAction> Actions,
    string RawOutput);

/// <summary>
/// Result of executing an action on the desktop.
/// </summary>
public sealed class ActionExecutionOutput
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public double DurationMs { get; init; }
    public bool ChangedDetected { get; init; }
    public string? ErrorMessage { get; init; }

    public static ActionExecutionOutput Ok(string message, double durationMs = 0, bool changed = true) =>
        new() { Success = true, Message = message, DurationMs = durationMs, ChangedDetected = changed };

    public static ActionExecutionOutput Fail(string error, double durationMs = 0) =>
        new() { Success = false, ErrorMessage = error, Message = error, DurationMs = durationMs };
}

/// <summary>
/// Post-action verification status.
/// </summary>
public enum VerificationStatus
{
    ConfirmedSuccess,
    ScreenUnchanged,
    StateChanged,
    FailureDetected,
    Skipped
}

/// <summary>
/// Verification evaluation comparing pre- and post-action states.
/// </summary>
public sealed record VerificationResult(
    VerificationStatus Status,
    double VisualDifferenceRatio,
    string Description,
    bool RequiresReplan);

/// <summary>
/// An individual step in a computer use execution session.
/// </summary>
public sealed class ComputerUseStep
{
    public int StepIndex { get; set; }
    public string? ScreenshotBeforeBase64 { get; set; }
    public string? ScreenshotAfterBase64 { get; set; }
    public int ScreenWidth { get; set; }
    public int ScreenHeight { get; set; }
    public double ScaleFactor { get; set; } = 1.0;
    public string? Thought { get; set; }
    public ComputerUseAction? Action { get; set; }
    public ActionExecutionOutput? ExecutionOutput { get; set; }
    public VerificationResult? Verification { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public double StepDurationMs { get; set; }
}

/// <summary>
/// Options configuring a Computer Use session run.
/// </summary>
public sealed class ComputerUseSessionOptions
{
    public int MaxSteps { get; set; } = 50;
    public int ActionDelayMs { get; set; } = 400;
    public int ScreenshotQuality { get; set; } = 85;
    public int MaxScreenshotWidth { get; set; } = 1920;
    public bool RequireConfirmationForSensitive { get; set; } = true;
    public bool StepByStepMode { get; set; } = false;
    public string? PreferredModelId { get; set; }
    public bool StrictLocalOnly { get; set; } = false;
    public int MonitorIndex { get; set; } = 0;
}

/// <summary>
/// A persistent Computer Use session with its history of steps and telemetry.
/// </summary>
public sealed class ComputerUseSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
    public string Goal { get; set; } = string.Empty;
    public ComputerUseSessionStatus Status { get; set; } = ComputerUseSessionStatus.Initializing;
    public List<ComputerUseStep> Steps { get; set; } = new();
    public string? ActiveModel { get; set; }
    public int TotalTokensUsed { get; set; }
    public DateTimeOffset StartTime { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndTime { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ResultSummary { get; set; }
    public ComputerUseSessionOptions Options { get; set; } = new();

    public TimeSpan ElapsedTime => (EndTime ?? DateTimeOffset.UtcNow) - StartTime;
}

/// <summary>
/// Log severity level for computer use streaming events.
/// </summary>
public enum ComputerUseLogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

/// <summary>
/// Real-time log message emitted during computer use execution.
/// </summary>
public sealed record ComputerUseLogEntry(
    DateTimeOffset Timestamp,
    ComputerUseLogLevel Level,
    string Source,
    string Message);
