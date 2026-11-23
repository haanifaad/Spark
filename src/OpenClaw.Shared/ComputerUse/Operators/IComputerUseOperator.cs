using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Capabilities;
using OpenClaw.Shared.ComputerUse.Types;

namespace OpenClaw.Shared.ComputerUse.Operators;

/// <summary>
/// Native operating system actuation and screen capture contract for computer use.
/// </summary>
public interface IComputerUseOperator
{
    /// <summary>
    /// Captures the desktop display.
    /// </summary>
    Task<ScreenCaptureResult> CaptureScreenshotAsync(
        int monitorIndex = 0,
        int maxWidth = 1920,
        int quality = 85,
        CancellationToken ct = default);

    /// <summary>
    /// Executes a parsed computer use action against the physical/logical desktop.
    /// </summary>
    Task<ActionExecutionOutput> ExecuteAsync(
        ComputerUseAction action,
        int screenWidth,
        int screenHeight,
        double scaleFactor = 1.0,
        CancellationToken ct = default);

    /// <summary>
    /// Moves the mouse cursor to logical coordinates (x, y).
    /// </summary>
    Task MoveMouseAsync(int x, int y, CancellationToken ct = default);

    /// <summary>
    /// Clicks at logical coordinates (x, y).
    /// </summary>
    Task ClickAsync(int x, int y, ComputerUseActionType clickType = ComputerUseActionType.Click, CancellationToken ct = default);

    /// <summary>
    /// Performs drag-and-drop between two logical coordinates.
    /// </summary>
    Task DragAsync(int startX, int startY, int endX, int endY, CancellationToken ct = default);

    /// <summary>
    /// Scrolls the mouse wheel at (x, y).
    /// </summary>
    Task ScrollAsync(int x, int y, ScrollDirection direction, int amount = 500, CancellationToken ct = default);

    /// <summary>
    /// Sends text input to the active window using Unicode key simulation.
    /// </summary>
    Task TypeTextAsync(string text, CancellationToken ct = default);

    /// <summary>
    /// Presses a hotkey combination (e.g. "ctrl+c", "alt+tab", "ctrl+shift+esc").
    /// </summary>
    Task HotkeyAsync(string keyCombination, CancellationToken ct = default);

    /// <summary>
    /// Presses a single key down.
    /// </summary>
    Task KeyPressAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Releases a single key up.
    /// </summary>
    Task KeyReleaseAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Gets the title of the current foreground window.
    /// </summary>
    string? GetActiveWindowTitle();

    /// <summary>
    /// Gets screen bounds for the given monitor.
    /// </summary>
    Rectangle GetScreenBounds(int monitorIndex = 0);
}
