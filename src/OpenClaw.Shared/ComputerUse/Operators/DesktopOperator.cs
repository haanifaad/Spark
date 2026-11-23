using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Capabilities;
using OpenClaw.Shared.ComputerUse.Types;

namespace OpenClaw.Shared.ComputerUse.Operators;

/// <summary>
/// Native Windows desktop operator providing high-speed Win32 actuation and screen observation.
/// Adapts UI-TARS nut.js operator principles into high-performance, native C# Windows P/Invoke calls.
/// </summary>
public sealed class DesktopOperator : IComputerUseOperator
{
    private readonly IOpenClawLogger? _logger;
    private Func<ScreenCaptureArgs, CancellationToken, Task<ScreenCaptureResult>>? _externalCaptureHandler;

    public DesktopOperator(IOpenClawLogger? logger = null, Func<ScreenCaptureArgs, CancellationToken, Task<ScreenCaptureResult>>? captureHandler = null)
    {
        _logger = logger;
        _externalCaptureHandler = captureHandler;
    }

    public void SetExternalCaptureHandler(Func<ScreenCaptureArgs, CancellationToken, Task<ScreenCaptureResult>> handler)
    {
        _externalCaptureHandler = handler;
    }

    #region Win32 P/Invoke Declarations

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern short VkKeyScan(char ch);

    // GDI Win32 for native screen snapshot fallback
    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, [Out] byte[]? lpvBits, ref BITMAPINFO lpbi, uint uUsage);

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public uint bmiColors;
    }

    // Input Structures
    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public MOUSEKEYBDHARDWAREINPUT mkhi;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct MOUSEKEYBDHARDWAREINPUT
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    private const int INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint SRCCOPY = 0x00CC0020;
    private const int MONITORINFOF_PRIMARY = 0x00000001;

    #endregion

    public async Task<ScreenCaptureResult> CaptureScreenshotAsync(
        int monitorIndex = 0,
        int maxWidth = 1920,
        int quality = 85,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // 1. Try host-injected capture service if available (e.g. from WinUI app)
        if (_externalCaptureHandler != null)
        {
            try
            {
                var result = await _externalCaptureHandler(new ScreenCaptureArgs
                {
                    MonitorIndex = monitorIndex,
                    MaxWidth = maxWidth,
                    Quality = quality,
                    IncludePointer = true,
                    Format = "png"
                }, ct).ConfigureAwait(false);

                if (result != null && !string.IsNullOrEmpty(result.Base64))
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger?.Warn($"[DesktopOperator] External screen capture failed, falling back to native GDI: {ex.Message}");
            }
        }

        // 2. Native Win32 GDI Screen Capture Fallback
        return await Task.Run(() => CaptureNativeScreen(monitorIndex, maxWidth), ct).ConfigureAwait(false);
    }

    private ScreenCaptureResult CaptureNativeScreen(int monitorIndex, int maxWidth)
    {
        var bounds = GetScreenBounds(monitorIndex);
        int width = bounds.Width;
        int height = bounds.Height;

        IntPtr hdcScreen = GetDC(IntPtr.Zero);
        IntPtr hdcMem = CreateCompatibleDC(hdcScreen);
        IntPtr hBitmap = CreateCompatibleBitmap(hdcScreen, width, height);
        IntPtr hOld = SelectObject(hdcMem, hBitmap);

        try
        {
            BitBlt(hdcMem, 0, 0, width, height, hdcScreen, bounds.X, bounds.Y, SRCCOPY);

            // Read DIB bits to produce a standard BMP format memory stream
            var bmi = new BITMAPINFO();
            bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            bmi.bmiHeader.biWidth = width;
            bmi.bmiHeader.biHeight = -height; // Top-down
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = 0; // BI_RGB

            int imageSize = width * height * 4;
            byte[] pixelData = new byte[imageSize];
            GetDIBits(hdcMem, hBitmap, 0, (uint)height, pixelData, ref bmi, 0);

            // Construct 24-bit/32-bit BMP file header
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            const int bmpHeaderSize = 14;
            const int dibHeaderSize = 40;
            int totalFileSize = bmpHeaderSize + dibHeaderSize + imageSize;

            // BMP Header
            bw.Write((byte)'B');
            bw.Write((byte)'M');
            bw.Write(totalFileSize);
            bw.Write((short)0); // reserved1
            bw.Write((short)0); // reserved2
            bw.Write(bmpHeaderSize + dibHeaderSize); // Pixel offset

            // DIB Header
            bw.Write(dibHeaderSize);
            bw.Write(width);
            bw.Write(-height); // top-down
            bw.Write((short)1); // planes
            bw.Write((short)32); // 32 bpp
            bw.Write(0); // compression BI_RGB
            bw.Write(imageSize);
            bw.Write(3780); // 96 DPI
            bw.Write(3780);
            bw.Write(0);
            bw.Write(0);

            // Write pixels (BGRA)
            bw.Write(pixelData);
            bw.Flush();

            var base64 = Convert.ToBase64String(ms.ToArray());
            return new ScreenCaptureResult
            {
                Base64 = base64,
                Width = width,
                Height = height,
                Format = "bmp"
            };
        }
        finally
        {
            SelectObject(hdcMem, hOld);
            DeleteObject(hBitmap);
            DeleteDC(hdcMem);
            ReleaseDC(IntPtr.Zero, hdcScreen);
        }
    }

    public async Task<ActionExecutionOutput> ExecuteAsync(
        ComputerUseAction action,
        int screenWidth,
        int screenHeight,
        double scaleFactor = 1.0,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var sw = Stopwatch.StartNew();

        try
        {
            Point startPoint = Point.Empty;
            if (action.StartBox != null)
            {
                startPoint = action.StartBox.ToLogicalPoint(screenWidth, screenHeight);
            }
            else if (action.TargetElement != null)
            {
                startPoint = action.TargetElement.Center;
            }

            switch (action.ActionType)
            {
                case ComputerUseActionType.Click:
                    await ClickAsync(startPoint.X, startPoint.Y, ComputerUseActionType.Click, ct).ConfigureAwait(false);
                    return ActionExecutionOutput.Ok($"Left click at ({startPoint.X}, {startPoint.Y})", sw.ElapsedMilliseconds);

                case ComputerUseActionType.DoubleClick:
                    await ClickAsync(startPoint.X, startPoint.Y, ComputerUseActionType.DoubleClick, ct).ConfigureAwait(false);
                    return ActionExecutionOutput.Ok($"Double click at ({startPoint.X}, {startPoint.Y})", sw.ElapsedMilliseconds);

                case ComputerUseActionType.RightClick:
                    await ClickAsync(startPoint.X, startPoint.Y, ComputerUseActionType.RightClick, ct).ConfigureAwait(false);
                    return ActionExecutionOutput.Ok($"Right click at ({startPoint.X}, {startPoint.Y})", sw.ElapsedMilliseconds);

                case ComputerUseActionType.MiddleClick:
                    await ClickAsync(startPoint.X, startPoint.Y, ComputerUseActionType.MiddleClick, ct).ConfigureAwait(false);
                    return ActionExecutionOutput.Ok($"Middle click at ({startPoint.X}, {startPoint.Y})", sw.ElapsedMilliseconds);

                case ComputerUseActionType.MouseMove:
                    await MoveMouseAsync(startPoint.X, startPoint.Y, ct).ConfigureAwait(false);
                    return ActionExecutionOutput.Ok($"Moved mouse to ({startPoint.X}, {startPoint.Y})", sw.ElapsedMilliseconds);

                case ComputerUseActionType.Drag:
                    Point endPoint = Point.Empty;
                    if (action.EndBox != null)
                    {
                        endPoint = action.EndBox.ToLogicalPoint(screenWidth, screenHeight);
                    }
                    else
                    {
                        endPoint = startPoint;
                    }
                    await DragAsync(startPoint.X, startPoint.Y, endPoint.X, endPoint.Y, ct).ConfigureAwait(false);
                    return ActionExecutionOutput.Ok($"Dragged from ({startPoint.X}, {startPoint.Y}) to ({endPoint.X}, {endPoint.Y})", sw.ElapsedMilliseconds);

                case ComputerUseActionType.TypeText:
                    if (!string.IsNullOrEmpty(action.TextContent))
                    {
                        await TypeTextAsync(action.TextContent, ct).ConfigureAwait(false);
                        return ActionExecutionOutput.Ok($"Typed text ({action.TextContent.Length} chars)", sw.ElapsedMilliseconds);
                    }
                    return ActionExecutionOutput.Ok("Empty text input skipped", sw.ElapsedMilliseconds, changed: false);

                case ComputerUseActionType.Hotkey:
                    if (!string.IsNullOrEmpty(action.Key))
                    {
                        await HotkeyAsync(action.Key, ct).ConfigureAwait(false);
                        return ActionExecutionOutput.Ok($"Pressed hotkey '{action.Key}'", sw.ElapsedMilliseconds);
                    }
                    return ActionExecutionOutput.Fail("Missing key for hotkey action", sw.ElapsedMilliseconds);

                case ComputerUseActionType.KeyPress:
                    if (!string.IsNullOrEmpty(action.Key))
                    {
                        await KeyPressAsync(action.Key, ct).ConfigureAwait(false);
                        return ActionExecutionOutput.Ok($"Key pressed down '{action.Key}'", sw.ElapsedMilliseconds);
                    }
                    return ActionExecutionOutput.Fail("Missing key for key_press", sw.ElapsedMilliseconds);

                case ComputerUseActionType.KeyRelease:
                    if (!string.IsNullOrEmpty(action.Key))
                    {
                        await KeyReleaseAsync(action.Key, ct).ConfigureAwait(false);
                        return ActionExecutionOutput.Ok($"Key released '{action.Key}'", sw.ElapsedMilliseconds);
                    }
                    return ActionExecutionOutput.Fail("Missing key for key_release", sw.ElapsedMilliseconds);

                case ComputerUseActionType.Scroll:
                    await ScrollAsync(startPoint.X, startPoint.Y, action.Direction, action.ScrollAmount, ct).ConfigureAwait(false);
                    return ActionExecutionOutput.Ok($"Scrolled {action.Direction} by {action.ScrollAmount}", sw.ElapsedMilliseconds);

                case ComputerUseActionType.Wait:
                    await Task.Delay(1500, ct).ConfigureAwait(false);
                    return ActionExecutionOutput.Ok("Waited 1.5 seconds for UI to settle", sw.ElapsedMilliseconds, changed: false);

                case ComputerUseActionType.Finished:
                    return ActionExecutionOutput.Ok("Task declared finished by agent", sw.ElapsedMilliseconds, changed: false);

                case ComputerUseActionType.CallUser:
                    return ActionExecutionOutput.Ok("Agent requested user assistance", sw.ElapsedMilliseconds, changed: false);

                case ComputerUseActionType.UserStop:
                    return ActionExecutionOutput.Ok("Execution halted by user", sw.ElapsedMilliseconds, changed: false);

                default:
                    return ActionExecutionOutput.Fail($"Unsupported action: {action.ActionType}", sw.ElapsedMilliseconds);
            }
        }
        catch (OperationCanceledException)
        {
            return ActionExecutionOutput.Fail("Action cancelled", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger?.Error($"[DesktopOperator] Execution failed: {ex.Message}", ex);
            return ActionExecutionOutput.Fail($"Execution error: {ex.Message}", sw.ElapsedMilliseconds);
        }
    }

    public Task MoveMouseAsync(int x, int y, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        SetCursorPos(x, y);
        return Task.CompletedTask;
    }

    public async Task ClickAsync(int x, int y, ComputerUseActionType clickType = ComputerUseActionType.Click, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        SetCursorPos(x, y);
        await Task.Delay(50, ct).ConfigureAwait(false);

        switch (clickType)
        {
            case ComputerUseActionType.DoubleClick:
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                await Task.Delay(100, ct).ConfigureAwait(false);
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                break;

            case ComputerUseActionType.RightClick:
                mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, UIntPtr.Zero);
                await Task.Delay(50, ct).ConfigureAwait(false);
                mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
                break;

            case ComputerUseActionType.MiddleClick:
                mouse_event(MOUSEEVENTF_MIDDLEDOWN, 0, 0, 0, UIntPtr.Zero);
                await Task.Delay(50, ct).ConfigureAwait(false);
                mouse_event(MOUSEEVENTF_MIDDLEUP, 0, 0, 0, UIntPtr.Zero);
                break;

            default:
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                await Task.Delay(50, ct).ConfigureAwait(false);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                break;
        }

        await Task.Delay(50, ct).ConfigureAwait(false);
    }

    public async Task DragAsync(int startX, int startY, int endX, int endY, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        SetCursorPos(startX, startY);
        await Task.Delay(100, ct).ConfigureAwait(false);

        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        await Task.Delay(50, ct).ConfigureAwait(false);

        // Smooth movement interpolation
        const int steps = 15;
        for (int i = 1; i <= steps; i++)
        {
            ct.ThrowIfCancellationRequested();
            double t = (double)i / steps;
            int cx = (int)Math.Round(startX + (endX - startX) * t);
            int cy = (int)Math.Round(startY + (endY - startY) * t);
            SetCursorPos(cx, cy);
            await Task.Delay(10, ct).ConfigureAwait(false);
        }

        await Task.Delay(50, ct).ConfigureAwait(false);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        await Task.Delay(50, ct).ConfigureAwait(false);
    }

    public async Task ScrollAsync(int x, int y, ScrollDirection direction, int amount = 500, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (x > 0 || y > 0)
        {
            SetCursorPos(x, y);
            await Task.Delay(50, ct).ConfigureAwait(false);
        }

        int delta = direction switch
        {
            ScrollDirection.Up => amount,
            ScrollDirection.Down => -amount,
            _ => -amount
        };

        mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)delta, UIntPtr.Zero);
        await Task.Delay(100, ct).ConfigureAwait(false);
    }

    public async Task TypeTextAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(text)) return;
        ct.ThrowIfCancellationRequested();

        bool hasTrailingEnter = text.EndsWith("\n", StringComparison.Ordinal) || text.EndsWith(@"\n", StringComparison.Ordinal);
        string cleanText = text.TrimEnd('\r', '\n');

        // Send Unicode characters directly using SendInput
        var inputs = new List<INPUT>();
        foreach (char c in cleanText)
        {
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                mkhi = new MOUSEKEYBDHARDWAREINPUT
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = c,
                        dwFlags = KEYEVENTF_UNICODE,
                        time = 0,
                        dwExtraInfo = UIntPtr.Zero
                    }
                }
            });

            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                mkhi = new MOUSEKEYBDHARDWAREINPUT
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = c,
                        dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = UIntPtr.Zero
                    }
                }
            });
        }

        if (inputs.Count > 0)
        {
            SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
            await Task.Delay(50, ct).ConfigureAwait(false);
        }

        if (hasTrailingEnter)
        {
            await HotkeyAsync("enter", ct).ConfigureAwait(false);
        }
    }

    public async Task HotkeyAsync(string keyCombination, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keyCombination)) return;
        ct.ThrowIfCancellationRequested();

        var parts = keyCombination.Split(new[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var vks = new List<ushort>();

        foreach (var p in parts)
        {
            ushort vk = ResolveVirtualKey(p.Trim().ToLowerInvariant());
            if (vk != 0) vks.Add(vk);
        }

        if (vks.Count == 0) return;

        // Press all down in order
        var downInputs = new INPUT[vks.Count];
        for (int i = 0; i < vks.Count; i++)
        {
            downInputs[i] = new INPUT
            {
                type = INPUT_KEYBOARD,
                mkhi = new MOUSEKEYBDHARDWAREINPUT
                {
                    ki = new KEYBDINPUT { wVk = vks[i], dwFlags = 0 }
                }
            };
        }
        SendInput((uint)downInputs.Length, downInputs, Marshal.SizeOf(typeof(INPUT)));
        await Task.Delay(50, ct).ConfigureAwait(false);

        // Release all in reverse order
        var upInputs = new INPUT[vks.Count];
        for (int i = 0; i < vks.Count; i++)
        {
            int idx = vks.Count - 1 - i;
            upInputs[i] = new INPUT
            {
                type = INPUT_KEYBOARD,
                mkhi = new MOUSEKEYBDHARDWAREINPUT
                {
                    ki = new KEYBDINPUT { wVk = vks[idx], dwFlags = KEYEVENTF_KEYUP }
                }
            };
        }
        SendInput((uint)upInputs.Length, upInputs, Marshal.SizeOf(typeof(INPUT)));
        await Task.Delay(50, ct).ConfigureAwait(false);
    }

    public Task KeyPressAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ushort vk = ResolveVirtualKey(key);
        if (vk != 0)
        {
            var input = new INPUT[]
            {
                new()
                {
                    type = INPUT_KEYBOARD,
                    mkhi = new MOUSEKEYBDHARDWAREINPUT
                    {
                        ki = new KEYBDINPUT { wVk = vk, dwFlags = 0 }
                    }
                }
            };
            SendInput(1, input, Marshal.SizeOf(typeof(INPUT)));
        }
        return Task.CompletedTask;
    }

    public Task KeyReleaseAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ushort vk = ResolveVirtualKey(key);
        if (vk != 0)
        {
            var input = new INPUT[]
            {
                new()
                {
                    type = INPUT_KEYBOARD,
                    mkhi = new MOUSEKEYBDHARDWAREINPUT
                    {
                        ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP }
                    }
                }
            };
            SendInput(1, input, Marshal.SizeOf(typeof(INPUT)));
        }
        return Task.CompletedTask;
    }

    public string? GetActiveWindowTitle()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;

            var sb = new StringBuilder(256);
            if (GetWindowText(hwnd, sb, sb.Capacity) > 0)
            {
                return sb.ToString();
            }
        }
        catch { }
        return null;
    }

    public Rectangle GetScreenBounds(int monitorIndex = 0)
    {
        var screens = new List<(Rectangle bounds, bool isPrimary)>();
        try
        {
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                var info = new MONITORINFOEX();
                info.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
                if (GetMonitorInfo(hMonitor, ref info))
                {
                    var bounds = new Rectangle(
                        info.rcMonitor.Left,
                        info.rcMonitor.Top,
                        info.rcMonitor.Right - info.rcMonitor.Left,
                        info.rcMonitor.Bottom - info.rcMonitor.Top);
                    bool isPrimary = (info.dwFlags & MONITORINFOF_PRIMARY) != 0;
                    screens.Add((bounds, isPrimary));
                }
                return true;
            }, IntPtr.Zero);
        }
        catch { }

        if (screens.Count > 0)
        {
            if (monitorIndex >= 0 && monitorIndex < screens.Count)
                return screens[monitorIndex].bounds;

            var primary = screens.Find(s => s.isPrimary);
            if (!primary.bounds.IsEmpty) return primary.bounds;
            return screens[0].bounds;
        }

        // Fallback to PrimaryScreen metrics
        int w = GetSystemMetrics(0); // SM_CXSCREEN
        int h = GetSystemMetrics(1); // SM_CYSCREEN
        return new Rectangle(0, 0, w > 0 ? w : 1920, h > 0 ? h : 1080);
    }

    private static ushort ResolveVirtualKey(string key) => key.ToLowerInvariant() switch
    {
        "ctrl" or "control" or "leftctrl" => 0x11, // VK_CONTROL
        "alt" or "leftalt" => 0x12, // VK_MENU
        "shift" or "leftshift" => 0x10, // VK_SHIFT
        "win" or "meta" or "command" or "cmd" or "windows" => 0x5B, // VK_LWIN
        "enter" or "return" => 0x0D, // VK_RETURN
        "esc" or "escape" => 0x1B, // VK_ESCAPE
        "tab" => 0x09, // VK_TAB
        "backspace" or "back" => 0x08, // VK_BACK
        "delete" or "del" => 0x2E, // VK_DELETE
        "insert" or "ins" => 0x2D, // VK_INSERT
        "space" or "spacebar" => 0x20, // VK_SPACE
        "pageup" => 0x21, // VK_PRIOR
        "pagedown" => 0x22, // VK_NEXT
        "end" => 0x23, // VK_END
        "home" => 0x24, // VK_HOME
        "left" or "arrowleft" => 0x25, // VK_LEFT
        "up" or "arrowup" => 0x26, // VK_UP
        "right" or "arrowright" => 0x27, // VK_RIGHT
        "down" or "arrowdown" => 0x28, // VK_DOWN
        "f1" => 0x70,
        "f2" => 0x71,
        "f3" => 0x72,
        "f4" => 0x73,
        "f5" => 0x74,
        "f6" => 0x75,
        "f7" => 0x76,
        "f8" => 0x77,
        "f9" => 0x78,
        "f10" => 0x79,
        "f11" => 0x7A,
        "f12" => 0x7B,
        "a" => 0x41, "b" => 0x42, "c" => 0x43, "d" => 0x44, "e" => 0x45,
        "f" => 0x46, "g" => 0x47, "h" => 0x48, "i" => 0x49, "j" => 0x4A,
        "k" => 0x4B, "l" => 0x4C, "m" => 0x4D, "n" => 0x4E, "o" => 0x4F,
        "p" => 0x50, "q" => 0x51, "r" => 0x52, "s" => 0x53, "t" => 0x54,
        "u" => 0x55, "v" => 0x56, "w" => 0x57, "x" => 0x58, "y" => 0x59,
        "z" => 0x5A,
        "0" => 0x30, "1" => 0x31, "2" => 0x32, "3" => 0x33, "4" => 0x34,
        "5" => 0x35, "6" => 0x36, "7" => 0x37, "8" => 0x38, "9" => 0x39,
        _ => (ushort)(key.Length == 1 ? (ushort)char.ToUpperInvariant(key[0]) : 0)
    };
}
