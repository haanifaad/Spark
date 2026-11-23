using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.ComputerUse.Operators;
using OpenClaw.Shared.ComputerUse.Types;

namespace OpenClaw.Shared.ComputerUse.Perception;

/// <summary>
/// Dual perception engine combining visual screenshot capture with Windows UI Automation / Win32 tree inspection.
/// Guarantees that clicks prioritize semantic elements with exact bounds, falling back to visual coordinates.
/// </summary>
public sealed class PerceptionEngine : IPerceptionEngine
{
    private readonly IComputerUseOperator _operator;
    private readonly IOpenClawLogger? _logger;

    public PerceptionEngine(IComputerUseOperator desktopOperator, IOpenClawLogger? logger = null)
    {
        _operator = desktopOperator ?? throw new ArgumentNullException(nameof(desktopOperator));
        _logger = logger;
    }

    #region Win32 Window Tree Enumeration

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowEnabled(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetFocus();

    #endregion

    public async Task<PerceptionSnapshot> CapturePerceptionAsync(
        int monitorIndex = 0,
        int maxWidth = 1920,
        bool inspectAccessibility = true,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // 1. Capture screen snapshot
        var screenResult = await _operator.CaptureScreenshotAsync(monitorIndex, maxWidth, 85, ct).ConfigureAwait(false);

        // 2. Query active foreground window title
        string? activeTitle = _operator.GetActiveWindowTitle();

        // 3. Inspect semantic elements if requested
        var elements = new List<PerceptionElement>();
        if (inspectAccessibility)
        {
            elements.AddRange(EnumerateVisibleWindowElements());
        }

        return new PerceptionSnapshot
        {
            Width = screenResult.Width,
            Height = screenResult.Height,
            ScaleFactor = 1.0,
            ScreenshotBase64 = screenResult.Base64,
            ActiveWindowTitle = activeTitle,
            Elements = elements,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    public Point GroundToLogicalPoint(BoundingBox? box, PerceptionSnapshot snapshot, string? semanticQuery = null)
    {
        // 1. If semantic query was provided, try matching element first
        if (!string.IsNullOrWhiteSpace(semanticQuery))
        {
            var matches = QueryElements(snapshot, semanticQuery);
            if (matches.Count > 0)
            {
                return matches[0].Center;
            }
        }

        if (box == null)
        {
            return new Point(snapshot.Width / 2, snapshot.Height / 2);
        }

        // 2. Calculate visual coordinates from normalized box [0..1000]
        var visualPoint = box.ToLogicalPoint(snapshot.Width, snapshot.Height);

        // 3. Ground against known semantic elements
        foreach (var el in snapshot.Elements)
        {
            if (el.Bounds.Contains(visualPoint))
            {
                // Found containing element - snap to element center for reliability
                return el.Center;
            }
        }

        // 4. Return direct visual point fallback
        return visualPoint;
    }

    public IReadOnlyList<PerceptionElement> QueryElements(PerceptionSnapshot snapshot, string query, string? requiredRole = null)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<PerceptionElement>();

        var results = new List<PerceptionElement>();
        var cleanQuery = query.Trim().ToLowerInvariant();

        foreach (var el in snapshot.Elements)
        {
            if (!string.IsNullOrEmpty(requiredRole) && !el.Role.Equals(requiredRole, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (el.Name.ToLowerInvariant().Contains(cleanQuery) || (el.Value?.ToLowerInvariant().Contains(cleanQuery) == true))
            {
                results.Add(el);
            }
        }

        return results;
    }

    private List<PerceptionElement> EnumerateVisibleWindowElements()
    {
        var elements = new List<PerceptionElement>();
        var fgHwnd = GetForegroundWindow();
        var focusHwnd = GetFocus();

        try
        {
            // First inspect top-level windows
            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;

                if (GetWindowRect(hWnd, out var rect))
                {
                    int w = rect.Right - rect.Left;
                    int h = rect.Bottom - rect.Top;
                    if (w > 20 && h > 20)
                    {
                        var text = GetWindowTextSafe(hWnd);
                        var className = GetClassNameSafe(hWnd);
                        var role = MapClassToRole(className);

                        if (!string.IsNullOrEmpty(text) || role != "window")
                        {
                            elements.Add(new PerceptionElement
                            {
                                Name = text,
                                Role = role,
                                Bounds = new Rectangle(rect.Left, rect.Top, w, h),
                                IsEnabled = IsWindowEnabled(hWnd),
                                IsFocused = (hWnd == fgHwnd || hWnd == focusHwnd),
                                Source = "uia"
                            });
                        }

                        // If it's the foreground window or an interactive window, inspect its child controls
                        if (hWnd == fgHwnd)
                        {
                            EnumChildWindows(hWnd, (childHwnd, childParam) =>
                            {
                                if (!IsWindowVisible(childHwnd)) return true;
                                if (GetWindowRect(childHwnd, out var cRect))
                                {
                                    int cw = cRect.Right - cRect.Left;
                                    int ch = cRect.Bottom - cRect.Top;
                                    if (cw > 10 && ch > 10)
                                    {
                                        var cText = GetWindowTextSafe(childHwnd);
                                        var cClass = GetClassNameSafe(childHwnd);
                                        elements.Add(new PerceptionElement
                                        {
                                            Name = cText,
                                            Role = MapClassToRole(cClass),
                                            Bounds = new Rectangle(cRect.Left, cRect.Top, cw, ch),
                                            IsEnabled = IsWindowEnabled(childHwnd),
                                            IsFocused = (childHwnd == focusHwnd),
                                            Source = "uia"
                                        });
                                    }
                                }
                                return true;
                            }, IntPtr.Zero);
                        }
                    }
                }
                return true;
            }, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            _logger?.Warn($"[PerceptionEngine] Window enumeration error: {ex.Message}");
        }

        return elements;
    }

    private static string GetWindowTextSafe(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        return GetWindowText(hWnd, sb, sb.Capacity) > 0 ? sb.ToString() : string.Empty;
    }

    private static string GetClassNameSafe(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        return GetClassName(hWnd, sb, sb.Capacity) > 0 ? sb.ToString() : string.Empty;
    }

    private static string MapClassToRole(string className) => className.ToLowerInvariant() switch
    {
        "button" => "button",
        "edit" or "richedit" or "richedit20w" or "richedit50w" => "edit",
        "static" => "text",
        "combobox" => "combobox",
        "listbox" => "listbox",
        "systreeview32" => "tree",
        "syslistview32" => "list",
        "systabcontrol32" => "tab",
        "scrollbar" => "scrollbar",
        _ when className.Contains("Button", StringComparison.OrdinalIgnoreCase) => "button",
        _ when className.Contains("Edit", StringComparison.OrdinalIgnoreCase) => "edit",
        _ => "window"
    };
}
