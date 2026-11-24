using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.ComputerUse.Types;

namespace OpenClaw.Shared.ComputerUse.Safety;

/// <summary>
/// Defensive safety guard enforcing permission gating, emergency stop, sensitive-action confirmation,
/// and untrusted injection protection for desktop control.
/// </summary>
public sealed class ComputerUseSafetyGuard : IComputerUseSafetyGuard
{
    private volatile bool _isDesktopControlPermitted = false;
    private volatile bool _isEmergencyStopped = false;
    private readonly IOpenClawLogger? _logger;

    public bool IsDesktopControlPermitted => _isDesktopControlPermitted;
    public bool IsEmergencyStopped => _isEmergencyStopped;

    public event EventHandler? EmergencyStopTriggered;

    private static readonly Regex DestructiveCommandsRegex = new(
        @"(?:\bdel\s+\/[fsq]|\brmdir\s+\/[sq]|\bformat\s+[a-z]:|\bremove-item\b[\s\S]*?-force|\brm\s+-rf\b|\bdiskpart\b|\bbcdedit\b)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AdminEscalationRegex = new(
        @"\b(net\s+user|net\s+localgroup|reg\s+add|reg\s+delete|takeown|icacls|powershell\s+-enc|net\s+stop)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PromptInjectionRegex = new(
        @"(?:ignore\s+(?:all\s+)?previous\s+instructions|system\s+override|bypass\s+(?:all\s+)?safeguards|admin\s+mode\s+enabled)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] FinancialKeywords =
    {
        "buy", "purchase", "pay", "checkout", "order now", "confirm payment", "transfer", "subscribe", "billing", "₹", "dollar"
    };

    private static readonly string[] CommunicationKeywords =
    {
        "send", "publish", "post", "tweet", "submit", "transfer", "sign out"
    };

    private static string ResolveStateFilePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var devDir = Path.Combine(localAppData, "OpenClawTray-Dev");
        if (Directory.Exists(devDir))
            return Path.Combine(devDir, "computer_use_safety.json");

        return Path.Combine(localAppData, "OpenClawTray", "computer_use_safety.json");
    }

    public ComputerUseSafetyGuard(bool permitDesktopControl = false, IOpenClawLogger? logger = null)
    {
        _logger = logger;
        if (permitDesktopControl)
        {
            _isDesktopControlPermitted = true;
        }
        else
        {
            _isDesktopControlPermitted = LoadPersistedPermission();
        }
    }

    private bool LoadPersistedPermission()
    {
        try
        {
            var path = ResolveStateFilePath();
            if (File.Exists(path))
            {
                var content = File.ReadAllText(path).Trim();
                if (bool.TryParse(content, out var result))
                    return result;
                if (content.Contains("\"DesktopControlPermitted\":true", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("\"DesktopControlPermitted\": true", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch (Exception ex)
        {
            _logger?.Warn($"[ComputerUseSafetyGuard] Could not load persisted desktop control state: {ex.Message}");
        }
        return false;
    }

    public void SetDesktopControlPermitted(bool permitted)
    {
        _isDesktopControlPermitted = permitted;
        _logger?.Info($"[ComputerUseSafetyGuard] Desktop control permission set to: {permitted}");

        try
        {
            var path = ResolveStateFilePath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(path, $"{{\"DesktopControlPermitted\": {(permitted ? "true" : "false")}}}");
        }
        catch (Exception ex)
        {
            _logger?.Warn($"[ComputerUseSafetyGuard] Could not persist desktop control state: {ex.Message}");
        }
    }

    public void TriggerEmergencyStop()
    {
        _isEmergencyStopped = true;
        _logger?.Warn("[ComputerUseSafetyGuard] EMERGENCY STOP TRIGGERED. All actions immediately terminated.");
        EmergencyStopTriggered?.Invoke(this, EventArgs.Empty);
    }

    public void ResetEmergencyStop()
    {
        _isEmergencyStopped = false;
        _logger?.Info("[ComputerUseSafetyGuard] Emergency stop state reset by user.");
    }

    public Task<SafetyEvaluationResult> EvaluateActionAsync(
        ComputerUseAction action,
        PerceptionSnapshot currentPerception,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // 1. Emergency Stop Check
        if (_isEmergencyStopped)
        {
            return Task.FromResult(new SafetyEvaluationResult(
                IsAllowed: false,
                RequiresUserConfirmation: false,
                Reason: "Emergency stop is active. System is latched in protective halt.",
                SensitiveCategory: "EmergencyStop"));
        }

        // 2. Permission Gate Check
        if (!_isDesktopControlPermitted)
        {
            return Task.FromResult(new SafetyEvaluationResult(
                IsAllowed: false,
                RequiresUserConfirmation: false,
                Reason: "Desktop control is disabled. Grant permission in SPARK Settings before continuing.",
                SensitiveCategory: "PermissionRequired"));
        }

        // 3. Prompt Injection Defense
        if (!string.IsNullOrEmpty(action.TextContent) && PromptInjectionRegex.IsMatch(action.TextContent))
        {
            _logger?.Warn($"[ComputerUseSafetyGuard] Suspicious prompt injection detected in text payload: {action.TextContent}");
            return Task.FromResult(new SafetyEvaluationResult(
                IsAllowed: false,
                RequiresUserConfirmation: false,
                Reason: "Action rejected: Prompt injection pattern detected in input payload.",
                SensitiveCategory: "PromptInjection"));
        }

        // 4. Destructive File Operations
        if (!string.IsNullOrEmpty(action.TextContent) && DestructiveCommandsRegex.IsMatch(action.TextContent))
        {
            return Task.FromResult(new SafetyEvaluationResult(
                IsAllowed: true,
                RequiresUserConfirmation: true,
                Reason: "Command contains destructive file system operations.",
                SensitiveCategory: "DestructiveFilesystem"));
        }

        // 5. System/Administrative Privilege Escalations
        if (!string.IsNullOrEmpty(action.TextContent) && AdminEscalationRegex.IsMatch(action.TextContent))
        {
            return Task.FromResult(new SafetyEvaluationResult(
                IsAllowed: true,
                RequiresUserConfirmation: true,
                Reason: "Command involves administrative or registry system configuration.",
                SensitiveCategory: "SystemAdministration"));
        }

        // 6. Financial Transactions and Purchasing
        if (action.ActionType is ComputerUseActionType.Click or ComputerUseActionType.DoubleClick)
        {
            // Check targeted element name
            string? elementName = action.TargetElement?.Name;
            if (string.IsNullOrEmpty(elementName) && action.StartBox != null)
            {
                // Check if any element in the bounding area matches financial keywords
                var targetPt = action.StartBox.ToLogicalPoint(currentPerception.Width, currentPerception.Height);
                foreach (var el in currentPerception.Elements)
                {
                    if (el.Bounds.Contains(targetPt))
                    {
                        elementName = el.Name;
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(elementName))
            {
                var lowerName = elementName.ToLowerInvariant();
                foreach (var kw in FinancialKeywords)
                {
                    if (lowerName.Contains(kw))
                    {
                        return Task.FromResult(new SafetyEvaluationResult(
                            IsAllowed: true,
                            RequiresUserConfirmation: true,
                            Reason: $"Targeted UI element '{elementName}' appears to be a financial or purchasing action.",
                            SensitiveCategory: "FinancialTransaction"));
                    }
                }

                foreach (var kw in CommunicationKeywords)
                {
                    if (lowerName.Equals(kw, StringComparison.OrdinalIgnoreCase))
                    {
                        return Task.FromResult(new SafetyEvaluationResult(
                            IsAllowed: true,
                            RequiresUserConfirmation: true,
                            Reason: $"Targeted UI element '{elementName}' submits an external communication or form.",
                            SensitiveCategory: "ExternalCommunication"));
                    }
                }
            }
        }

        // Action is standard and safe
        return Task.FromResult(new SafetyEvaluationResult(
            IsAllowed: true,
            RequiresUserConfirmation: false,
            Reason: null,
            SensitiveCategory: null));
    }
}
