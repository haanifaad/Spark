using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.ComputerUse.Engine;
using OpenClaw.Shared.ComputerUse.Events;
using OpenClaw.Shared.ComputerUse.Orchestration;
using OpenClaw.Shared.ComputerUse.Safety;
using OpenClaw.Shared.ComputerUse.Sessions;
using OpenClaw.Shared.ComputerUse.Types;
using OpenClaw.Shared.Inference.Routing;

namespace OpenClawTray.Presentation;

public sealed class ComputerUseStepViewModel
{
    public int StepIndex { get; set; }
    public string Thought { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Verification { get; set; } = string.Empty;
    public string TimeFormatted { get; set; } = string.Empty;
    public string DurationFormatted { get; set; } = string.Empty;
    public bool IsSuccess { get; set; } = true;
}

public sealed class ComputerUseSessionItemViewModel
{
    public string SessionId { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int StepCount { get; set; }
    public string StartTimeFormatted { get; set; } = string.Empty;
    public string DurationFormatted { get; set; } = string.Empty;
}

/// <summary>
/// Presentation ViewModel powering the native SPARK Computer Use workspace and page.
/// </summary>
internal sealed class ComputerUseViewModel : INavigationAware, IDisposable, INotifyPropertyChanged
{
    private readonly IComputerUseEngine _engine;
    private readonly ComputerUseOrchestrator _orchestrator;
    private readonly IComputerUseSessionManager _sessionManager;
    private readonly IComputerUseEventHub _eventHub;
    private readonly IComputerUseSafetyGuard _safetyGuard;
    private readonly ISparkModelRouter? _modelRouter;
    private readonly IUiDispatcher? _dispatcher;

    private string _goalInput = string.Empty;
    private string _statusText = "Ready";
    private bool _isRunning;
    private bool _isPaused;
    private bool _isDesktopControlPermitted;
    private bool _isEmergencyStopped;
    private bool _stepByStepMode;
    private int _screenshotQuality = 85;
    private int _actionDelayMs = 400;
    private bool _requireConfirmationForSensitive = true;
    private string _selectedModel = "qwen2.5-vl";
    private string _currentThought = "Awaiting task instruction...";
    private string _currentActionText = "None";
    private string _currentActionType = "Idle";
    private string _currentVerificationText = "Pending";
    private string _currentWindowText = "Desktop";
    private string? _currentScreenshotBase64;
    private string _durationText = "00:00";
    private string _stepCountText = "0 steps";
    private string _tokensText = "0 tokens";
    private bool _isConfirmationPromptVisible;
    private string _confirmationPromptMessage = string.Empty;
    private string _confirmationCategory = string.Empty;
    private string? _currentSessionId;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ComputerUseStepViewModel> Steps { get; } = new();
    public ObservableCollection<string> Logs { get; } = new();
    public ObservableCollection<ComputerUseSessionItemViewModel> Sessions { get; } = new();
    public ObservableCollection<string> AvailableModels { get; } = new();

    public string GoalInput
    {
        get => _goalInput;
        set => SetField(ref _goalInput, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        set => SetField(ref _isRunning, value);
    }

    public bool IsPaused
    {
        get => _isPaused;
        set => SetField(ref _isPaused, value);
    }

    public bool IsDesktopControlPermitted
    {
        get => _isDesktopControlPermitted;
        set
        {
            if (SetField(ref _isDesktopControlPermitted, value))
            {
                _safetyGuard.SetDesktopControlPermitted(value);
            }
        }
    }

    public bool IsEmergencyStopped
    {
        get => _isEmergencyStopped;
        set => SetField(ref _isEmergencyStopped, value);
    }

    public bool StepByStepMode
    {
        get => _stepByStepMode;
        set => SetField(ref _stepByStepMode, value);
    }

    public int ScreenshotQuality
    {
        get => _screenshotQuality;
        set => SetField(ref _screenshotQuality, value);
    }

    public int ActionDelayMs
    {
        get => _actionDelayMs;
        set => SetField(ref _actionDelayMs, value);
    }

    public bool RequireConfirmationForSensitive
    {
        get => _requireConfirmationForSensitive;
        set => SetField(ref _requireConfirmationForSensitive, value);
    }

    public string SelectedModel
    {
        get => _selectedModel;
        set => SetField(ref _selectedModel, value);
    }

    public string CurrentThought
    {
        get => _currentThought;
        set => SetField(ref _currentThought, value);
    }

    public string CurrentActionText
    {
        get => _currentActionText;
        set => SetField(ref _currentActionText, value);
    }

    public string CurrentActionType
    {
        get => _currentActionType;
        set => SetField(ref _currentActionType, value);
    }

    public string CurrentVerificationText
    {
        get => _currentVerificationText;
        set => SetField(ref _currentVerificationText, value);
    }

    public string CurrentWindowText
    {
        get => _currentWindowText;
        set => SetField(ref _currentWindowText, value);
    }

    public string? CurrentScreenshotBase64
    {
        get => _currentScreenshotBase64;
        set => SetField(ref _currentScreenshotBase64, value);
    }

    public string DurationText
    {
        get => _durationText;
        set => SetField(ref _durationText, value);
    }

    public string StepCountText
    {
        get => _stepCountText;
        set => SetField(ref _stepCountText, value);
    }

    public string TokensText
    {
        get => _tokensText;
        set => SetField(ref _tokensText, value);
    }

    public bool IsConfirmationPromptVisible
    {
        get => _isConfirmationPromptVisible;
        set => SetField(ref _isConfirmationPromptVisible, value);
    }

    public string ConfirmationPromptMessage
    {
        get => _confirmationPromptMessage;
        set => SetField(ref _confirmationPromptMessage, value);
    }

    public string ConfirmationCategory
    {
        get => _confirmationCategory;
        set => SetField(ref _confirmationCategory, value);
    }

    public ComputerUseViewModel(
        IComputerUseEngine engine,
        ComputerUseOrchestrator orchestrator,
        IComputerUseSessionManager sessionManager,
        IComputerUseEventHub eventHub,
        IComputerUseSafetyGuard safetyGuard,
        ISparkModelRouter? modelRouter = null,
        IUiDispatcher? dispatcher = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _eventHub = eventHub ?? throw new ArgumentNullException(nameof(eventHub));
        _safetyGuard = safetyGuard ?? throw new ArgumentNullException(nameof(safetyGuard));
        _modelRouter = modelRouter;
        _dispatcher = dispatcher;

        _isDesktopControlPermitted = _safetyGuard.IsDesktopControlPermitted;
        _isEmergencyStopped = _safetyGuard.IsEmergencyStopped;

        // Wire event hub listeners
        _eventHub.ScreenshotUpdated += OnScreenshotUpdated;
        _eventHub.StepStarted += OnStepStarted;
        _eventHub.ActionExecuting += OnActionExecuting;
        _eventHub.StepCompleted += OnStepCompleted;
        _eventHub.VerificationCompleted += OnVerificationCompleted;
        _eventHub.SessionStatusChanged += OnSessionStatusChanged;
        _eventHub.LogEmitted += OnLogEmitted;
        _eventHub.ConfirmationRequired += OnConfirmationRequired;

        // Populate available models
        AvailableModels.Add("qwen2.5-vl");
        AvailableModels.Add("ui-tars-7b");
        AvailableModels.Add("gpt-4o");
        AvailableModels.Add("claude-3-5-sonnet");
        AvailableModels.Add("llama3.2-vision");

        if (_modelRouter != null)
        {
            foreach (var m in _modelRouter.ListModels())
            {
                if (!AvailableModels.Contains(m.ModelId))
                {
                    AvailableModels.Add(m.ModelId);
                }
            }
        }
    }

    public void OnNavigatedTo(object? parameter)
    {
        RefreshSessions();
    }

    public void OnNavigatedFrom()
    {
    }

    public async Task StartTaskAsync()
    {
        if (string.IsNullOrWhiteSpace(GoalInput))
        {
            StatusText = "Please enter a task goal.";
            return;
        }

        if (!_safetyGuard.IsDesktopControlPermitted)
        {
            StatusText = "Permission Required: Please enable Desktop Control in settings.";
            return;
        }

        if (_safetyGuard.IsEmergencyStopped)
        {
            StatusText = "Emergency stop is active. Reset emergency stop first.";
            return;
        }

        Steps.Clear();
        Logs.Clear();
        IsRunning = true;
        IsPaused = false;
        StatusText = "Starting session...";

        var options = new ComputerUseSessionOptions
        {
            MaxSteps = 40,
            ActionDelayMs = ActionDelayMs,
            ScreenshotQuality = ScreenshotQuality,
            StepByStepMode = StepByStepMode,
            RequireConfirmationForSensitive = RequireConfirmationForSensitive,
            PreferredModelId = SelectedModel
        };

        var session = await _engine.StartSessionAsync(GoalInput, options).ConfigureAwait(false);
        _currentSessionId = session.SessionId;

        _ = Task.Run(async () =>
        {
            try
            {
                await _engine.RunSessionLoopAsync(session.SessionId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                RunOnUi(() =>
                {
                    StatusText = $"Session error: {ex.Message}";
                    IsRunning = false;
                });
            }
        });
    }

    public async Task ExecuteStepAsync()
    {
        if (string.IsNullOrEmpty(_currentSessionId))
        {
            if (string.IsNullOrWhiteSpace(GoalInput))
            {
                StatusText = "Please enter a task goal.";
                return;
            }

            var session = await _engine.StartSessionAsync(GoalInput, new ComputerUseSessionOptions
            {
                StepByStepMode = true,
                PreferredModelId = SelectedModel
            }).ConfigureAwait(false);
            _currentSessionId = session.SessionId;
        }

        IsRunning = true;
        StatusText = "Executing single step...";
        _ = Task.Run(async () =>
        {
            await _engine.ExecuteStepAsync(_currentSessionId).ConfigureAwait(false);
            RunOnUi(() => IsRunning = false);
        });
    }

    public void PauseTask()
    {
        if (!string.IsNullOrEmpty(_currentSessionId))
        {
            _engine.PauseSession(_currentSessionId);
            IsPaused = true;
            StatusText = "Paused";
        }
    }

    public void ResumeTask()
    {
        if (!string.IsNullOrEmpty(_currentSessionId))
        {
            _engine.ResumeSession(_currentSessionId);
            IsPaused = false;
            StatusText = "Running";
        }
    }

    public void StopTask()
    {
        if (!string.IsNullOrEmpty(_currentSessionId))
        {
            _engine.StopSession(_currentSessionId, "Stopped by user.");
            IsRunning = false;
            IsPaused = false;
            StatusText = "Stopped by user";
        }
    }

    public void TriggerEmergencyStop()
    {
        _engine.TriggerEmergencyStop();
        IsEmergencyStopped = true;
        IsRunning = false;
        IsPaused = false;
        StatusText = "EMERGENCY STOPPED";
    }

    public void ResetEmergencyStop()
    {
        _safetyGuard.ResetEmergencyStop();
        IsEmergencyStopped = false;
        StatusText = "Emergency stop reset. Ready.";
    }

    public void ApproveSensitiveAction(bool approved)
    {
        if (!string.IsNullOrEmpty(_currentSessionId))
        {
            _engine.ConfirmSensitiveAction(_currentSessionId, approved);
            IsConfirmationPromptVisible = false;
        }
    }

    public void RefreshSessions()
    {
        Sessions.Clear();
        foreach (var s in _sessionManager.ListSessions())
        {
            Sessions.Add(new ComputerUseSessionItemViewModel
            {
                SessionId = s.SessionId,
                Goal = s.Goal,
                Status = s.Status.ToString(),
                StepCount = s.Steps.Count,
                StartTimeFormatted = s.StartTime.ToString("HH:mm:ss"),
                DurationFormatted = $"{s.ElapsedTime.TotalSeconds:F0}s"
            });
        }
    }

    #region Event Handlers

    private void OnScreenshotUpdated(object? sender, PerceptionSnapshot snapshot)
    {
        RunOnUi(() =>
        {
            CurrentScreenshotBase64 = snapshot.ScreenshotBase64;
            CurrentWindowText = snapshot.ActiveWindowTitle ?? "Desktop";
        });
    }

    private void OnStepStarted(object? sender, ComputerUseStep step)
    {
        RunOnUi(() =>
        {
            StatusText = $"Executing Step {step.StepIndex}...";
        });
    }

    private void OnActionExecuting(object? sender, ComputerUseAction action)
    {
        RunOnUi(() =>
        {
            CurrentActionType = action.ActionType.ToString();
            CurrentActionText = action.ToString();
            if (!string.IsNullOrEmpty(action.Thought))
            {
                CurrentThought = action.Thought;
            }
        });
    }

    private void OnStepCompleted(object? sender, ComputerUseStep step)
    {
        RunOnUi(() =>
        {
            Steps.Insert(0, new ComputerUseStepViewModel
            {
                StepIndex = step.StepIndex,
                Thought = step.Thought ?? "No thought recorded",
                Action = step.Action?.ToString() ?? "None",
                Result = step.ExecutionOutput?.Message ?? "Executed",
                Verification = step.Verification?.Status.ToString() ?? "Checked",
                TimeFormatted = step.Timestamp.ToString("HH:mm:ss"),
                DurationFormatted = $"{step.StepDurationMs:F0}ms",
                IsSuccess = step.ExecutionOutput?.Success ?? true
            });

            StepCountText = $"{Steps.Count} steps";
        });
    }

    private void OnVerificationCompleted(object? sender, VerificationResult verification)
    {
        RunOnUi(() =>
        {
            CurrentVerificationText = $"{verification.Status} ({verification.VisualDifferenceRatio:P0} delta) - {verification.Description}";
        });
    }

    private void OnSessionStatusChanged(object? sender, ComputerUseSession session)
    {
        RunOnUi(() =>
        {
            StatusText = session.Status.ToString();
            DurationText = $"{session.ElapsedTime.TotalSeconds:F0}s";
            TokensText = $"{session.TotalTokensUsed} tokens";

            if (session.Status is ComputerUseSessionStatus.Completed or ComputerUseSessionStatus.Failed or ComputerUseSessionStatus.StoppedByUser)
            {
                IsRunning = false;
                IsPaused = false;
                RefreshSessions();
            }
        });
    }

    private void OnLogEmitted(object? sender, ComputerUseLogEntry entry)
    {
        RunOnUi(() =>
        {
            string line = $"[{entry.Timestamp:HH:mm:ss}] [{entry.Source}] {entry.Message}";
            Logs.Insert(0, line);
            if (Logs.Count > 200) Logs.RemoveAt(Logs.Count - 1);
        });
    }

    private void OnConfirmationRequired(object? sender, SafetyEvaluationResult safety)
    {
        RunOnUi(() =>
        {
            ConfirmationPromptMessage = safety.Reason ?? "Sensitive desktop action requires confirmation.";
            ConfirmationCategory = safety.SensitiveCategory ?? "Security";
            IsConfirmationPromptVisible = true;
        });
    }

    private void RunOnUi(Action action)
    {
        if (_dispatcher != null)
        {
            _dispatcher.TryEnqueue(action);
        }
        else
        {
            action();
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    public void Activate(object? parameter)
    {
    }

    public void Deactivate()
    {
    }

    public void Dispose()
    {
        _eventHub.ScreenshotUpdated -= OnScreenshotUpdated;
        _eventHub.StepStarted -= OnStepStarted;
        _eventHub.ActionExecuting -= OnActionExecuting;
        _eventHub.StepCompleted -= OnStepCompleted;
        _eventHub.VerificationCompleted -= OnVerificationCompleted;
        _eventHub.SessionStatusChanged -= OnSessionStatusChanged;
        _eventHub.LogEmitted -= OnLogEmitted;
        _eventHub.ConfirmationRequired -= OnConfirmationRequired;
    }

    #endregion
}
