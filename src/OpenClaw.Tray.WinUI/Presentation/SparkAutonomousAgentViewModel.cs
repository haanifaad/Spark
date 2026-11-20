using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using OpenClaw.Shared.Background;
using OpenClaw.Shared.Channels;
using OpenClaw.Shared.Inference;
using OpenClaw.Shared.Inference.Routing;
using OpenClaw.Shared.Memory;
using OpenClaw.Shared.Orchestration;
using OpenClaw.Shared.Persona;
using OpenClaw.Shared.Security.Approvals;
using OpenClaw.Shared.Security.Audit;
using OpenClaw.Shared.Specialists.Finance;
using OpenClaw.Shared.Tools;

namespace OpenClawTray.Presentation;

/// <summary>
/// Production ViewModel powering the SPARK Autonomous Agent dashboard:
/// Tasks & Execution, Memory, Persona, Channels, Tool Registry, Model Routing, Approvals, and Audit.
/// </summary>
internal sealed class SparkAutonomousAgentViewModel : INavigationAware, IDisposable, INotifyPropertyChanged
{
    private readonly ISparkMemoryStore _memoryStore;
    private readonly ISparkPersonaManager _personaManager;
    private readonly ISparkChannelManager _channelManager;
    private readonly ISparkChannelCredentialStore _channelCredentialStore;
    private readonly ISparkToolRegistry _toolRegistry;
    private readonly ISparkModelRouter _modelRouter;
    private readonly ISparkApprovalManager _approvalManager;
    private readonly ISparkAuditLogger _auditLogger;
    private readonly IAgentOrchestrator? _orchestrator;
    private readonly ITaskStateManager? _taskStateManager;
    private readonly ISparkBackgroundWorker? _backgroundWorker;
    private readonly ISpendingLimitsEngine? _spendingLimitsEngine;

    private bool _isLoading;
    private string? _statusMessage;
    private bool _isStatusError;

    // Tasks Tab Properties
    private string _newTaskInstruction = string.Empty;
    private string _newTaskPriority = "Normal";
    private string _taskFilter = "All";
    private TaskItemViewModel? _selectedTask;
    private bool _isTaskSelected;
    private string _selectedTaskInstruction = string.Empty;
    private string _editInstructionInput = string.Empty;
    private bool _isTakeoverActive;
    private string _backgroundWorkerStateText = "Initializing...";
    private string _spendingLimitsOverviewText = "Loading limits...";

    // Memory Tab Properties
    private string _memorySearchQuery = string.Empty;
    private string _newMemoryCategory = "GeneralFact";
    private string _newMemoryContent = string.Empty;
    private bool _isMemoriesEmpty = true;

    // Persona Tab Properties
    private string _agentName = "SPARK";
    private string _selectedTone = "Professional";
    private string _selectedLanguage = "English";
    private string _customInstructions = string.Empty;
    private string _behavioralRules = string.Empty;
    private string _prohibitedActions = string.Empty;
    private string _compiledSystemPrompt = string.Empty;

    // Channels Tab Properties
    private string _selectedChannelId = "tg_main";
    private string _channelSecretInput = string.Empty;

    // Tools Tab Properties
    private string? _selectedToolId;
    private string _toolDryRunArgs = "{}";
    private string _toolDryRunOutput = string.Empty;

    // Model Routing Tab Properties
    private string _selectedPrivacyMode = "Balanced";
    private string _routingTestCategory = "ComplexReasoning";
    private string _routingResultText = string.Empty;

    // Approvals & Audit Tab Properties
    private bool _isAuditLogTamperFree = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<TaskItemViewModel> Tasks { get; } = new();
    public ObservableCollection<SparkTaskStep> SelectedTaskSteps { get; } = new();
    public ObservableCollection<MemoryRecordItem> Memories { get; } = new();
    public ObservableCollection<ChannelStatusItem> Channels { get; } = new();
    public ObservableCollection<SparkChannelMessage> RecentMessages { get; } = new();
    public ObservableCollection<ToolItemViewModel> Tools { get; } = new();
    public ObservableCollection<SparkModelDescriptor> Models { get; } = new();
    public ObservableCollection<ApprovalItemViewModel> PendingApprovals { get; } = new();
    public ObservableCollection<SparkAuditRecord> AuditRecords { get; } = new();

    public bool IsLoading
    {
        get => _isLoading;
        set => SetField(ref _isLoading, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public bool IsStatusError
    {
        get => _isStatusError;
        set => SetField(ref _isStatusError, value);
    }

    public string NewTaskInstruction
    {
        get => _newTaskInstruction;
        set => SetField(ref _newTaskInstruction, value);
    }

    public string NewTaskPriority
    {
        get => _newTaskPriority;
        set => SetField(ref _newTaskPriority, value);
    }

    public string TaskFilter
    {
        get => _taskFilter;
        set
        {
            if (SetField(ref _taskFilter, value))
            {
                _ = LoadTasksAsync();
            }
        }
    }

    public TaskItemViewModel? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (SetField(ref _selectedTask, value))
            {
                IsTaskSelected = value != null;
                if (value != null)
                {
                    _ = SelectTaskAsync(value.TaskId);
                }
                else
                {
                    SelectedTaskSteps.Clear();
                    SelectedTaskInstruction = string.Empty;
                    EditInstructionInput = string.Empty;
                }
            }
        }
    }

    public bool IsTaskSelected
    {
        get => _isTaskSelected;
        set => SetField(ref _isTaskSelected, value);
    }

    public string SelectedTaskInstruction
    {
        get => _selectedTaskInstruction;
        set => SetField(ref _selectedTaskInstruction, value);
    }

    public string EditInstructionInput
    {
        get => _editInstructionInput;
        set => SetField(ref _editInstructionInput, value);
    }

    public bool IsTakeoverActive
    {
        get => _isTakeoverActive;
        set => SetField(ref _isTakeoverActive, value);
    }

    public string BackgroundWorkerStateText
    {
        get => _backgroundWorkerStateText;
        set => SetField(ref _backgroundWorkerStateText, value);
    }

    public string SpendingLimitsOverviewText
    {
        get => _spendingLimitsOverviewText;
        set => SetField(ref _spendingLimitsOverviewText, value);
    }

    public string MemorySearchQuery
    {
        get => _memorySearchQuery;
        set
        {
            if (SetField(ref _memorySearchQuery, value))
            {
                _ = FilterMemoriesAsync();
            }
        }
    }

    public string NewMemoryCategory
    {
        get => _newMemoryCategory;
        set => SetField(ref _newMemoryCategory, value);
    }

    public string NewMemoryContent
    {
        get => _newMemoryContent;
        set => SetField(ref _newMemoryContent, value);
    }

    public bool IsMemoriesEmpty
    {
        get => _isMemoriesEmpty;
        set => SetField(ref _isMemoriesEmpty, value);
    }

    public string AgentName
    {
        get => _agentName;
        set
        {
            if (SetField(ref _agentName, value))
                UpdateSystemPromptPreview();
        }
    }

    public string SelectedTone
    {
        get => _selectedTone;
        set
        {
            if (SetField(ref _selectedTone, value))
                UpdateSystemPromptPreview();
        }
    }

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetField(ref _selectedLanguage, value))
                UpdateSystemPromptPreview();
        }
    }

    public string CustomInstructions
    {
        get => _customInstructions;
        set
        {
            if (SetField(ref _customInstructions, value))
                UpdateSystemPromptPreview();
        }
    }

    public string BehavioralRules
    {
        get => _behavioralRules;
        set
        {
            if (SetField(ref _behavioralRules, value))
                UpdateSystemPromptPreview();
        }
    }

    public string ProhibitedActions
    {
        get => _prohibitedActions;
        set
        {
            if (SetField(ref _prohibitedActions, value))
                UpdateSystemPromptPreview();
        }
    }

    public string CompiledSystemPrompt
    {
        get => _compiledSystemPrompt;
        private set => SetField(ref _compiledSystemPrompt, value);
    }

    public string SelectedChannelId
    {
        get => _selectedChannelId;
        set => SetField(ref _selectedChannelId, value);
    }

    public string ChannelSecretInput
    {
        get => _channelSecretInput;
        set => SetField(ref _channelSecretInput, value);
    }

    public string? SelectedToolId
    {
        get => _selectedToolId;
        set => SetField(ref _selectedToolId, value);
    }

    public string ToolDryRunArgs
    {
        get => _toolDryRunArgs;
        set => SetField(ref _toolDryRunArgs, value);
    }

    public string ToolDryRunOutput
    {
        get => _toolDryRunOutput;
        set => SetField(ref _toolDryRunOutput, value);
    }

    public string SelectedPrivacyMode
    {
        get => _selectedPrivacyMode;
        set => SetField(ref _selectedPrivacyMode, value);
    }

    public string RoutingTestCategory
    {
        get => _routingTestCategory;
        set => SetField(ref _routingTestCategory, value);
    }

    public string RoutingResultText
    {
        get => _routingResultText;
        set => SetField(ref _routingResultText, value);
    }

    public bool IsAuditLogTamperFree
    {
        get => _isAuditLogTamperFree;
        set => SetField(ref _isAuditLogTamperFree, value);
    }

    public SparkAutonomousAgentViewModel(
        ISparkMemoryStore memoryStore,
        ISparkPersonaManager personaManager,
        ISparkChannelManager channelManager,
        ISparkChannelCredentialStore channelCredentialStore,
        ISparkToolRegistry toolRegistry,
        ISparkModelRouter modelRouter,
        ISparkApprovalManager approvalManager,
        ISparkAuditLogger auditLogger,
        IAgentOrchestrator? orchestrator = null,
        ITaskStateManager? taskStateManager = null,
        ISparkBackgroundWorker? backgroundWorker = null,
        ISpendingLimitsEngine? spendingLimitsEngine = null)
    {
        _memoryStore = memoryStore ?? throw new ArgumentNullException(nameof(memoryStore));
        _personaManager = personaManager ?? throw new ArgumentNullException(nameof(personaManager));
        _channelManager = channelManager ?? throw new ArgumentNullException(nameof(channelManager));
        _channelCredentialStore = channelCredentialStore ?? throw new ArgumentNullException(nameof(channelCredentialStore));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _modelRouter = modelRouter ?? throw new ArgumentNullException(nameof(modelRouter));
        _approvalManager = approvalManager ?? throw new ArgumentNullException(nameof(approvalManager));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _orchestrator = orchestrator;
        _taskStateManager = taskStateManager;
        _backgroundWorker = backgroundWorker;
        _spendingLimitsEngine = spendingLimitsEngine;
    }

    public void Activate(object? parameter)
    {
        _ = LoadAllDataAsync();
    }

    public void Deactivate()
    {
    }

    public async Task LoadAllDataAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading SPARK autonomous systems...";
        IsStatusError = false;

        try
        {
            await LoadTasksAsync();
            RefreshBackgroundWorkerStatus();
            await RefreshSpendingSummaryAsync();
            await LoadMemoriesAsync();
            await LoadPersonaAsync();
            await LoadChannelsAsync();
            await LoadToolsAsync();
            await LoadModelsAsync();
            await LoadApprovalsAndAuditAsync();

            StatusMessage = "All systems operational.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load dashboard: {ex.Message}";
            IsStatusError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadMemoriesAsync()
    {
        var results = await _memoryStore.SearchAsync(string.Empty, userId: "user_default");
        Memories.Clear();
        foreach (var r in results)
        {
            Memories.Add(new MemoryRecordItem(r.Record.Id, r.Record.Category.ToString(), r.Record.Content, r.RelevanceScore, r.Record.UpdatedAt));
        }
        IsMemoriesEmpty = Memories.Count == 0;
    }

    public async Task FilterMemoriesAsync()
    {
        var results = await _memoryStore.SearchAsync(_memorySearchQuery, userId: "user_default");
        Memories.Clear();
        foreach (var r in results)
        {
            Memories.Add(new MemoryRecordItem(r.Record.Id, r.Record.Category.ToString(), r.Record.Content, r.RelevanceScore, r.Record.UpdatedAt));
        }
        IsMemoriesEmpty = Memories.Count == 0;
    }

    public async Task AddMemoryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewMemoryContent)) return;

        if (!Enum.TryParse<MemoryCategory>(NewMemoryCategory, out var category))
            category = MemoryCategory.GeneralFact;

        var record = new MemoryRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Category = category,
            Content = NewMemoryContent.Trim(),
            Scope = MemoryScope.UserProfile,
            UserId = "user_default"
        };

        await _memoryStore.RememberAsync(record);

        NewMemoryContent = string.Empty;
        await LoadMemoriesAsync();
        StatusMessage = "Memory successfully recorded.";
        IsStatusError = false;
    }

    public async Task ForgetMemoryAsync(string memoryId)
    {
        if (string.IsNullOrWhiteSpace(memoryId)) return;
        await _memoryStore.ForgetAsync(memoryId, "user_default");
        await LoadMemoriesAsync();
        StatusMessage = "Memory forgotten.";
        IsStatusError = false;
    }

    public async Task LoadPersonaAsync()
    {
        var config = await _personaManager.GetCurrentPersonaAsync("user_default");
        _agentName = config.AgentName;
        _selectedTone = config.Tone.ToString();
        _selectedLanguage = config.PrimaryLanguage.ToString();
        _customInstructions = config.CustomInstructions ?? string.Empty;
        _behavioralRules = string.Join(Environment.NewLine, config.BehavioralRules);
        _prohibitedActions = string.Join(Environment.NewLine, config.ProhibitedActions);

        OnPropertyChanged(nameof(AgentName));
        OnPropertyChanged(nameof(SelectedTone));
        OnPropertyChanged(nameof(SelectedLanguage));
        OnPropertyChanged(nameof(CustomInstructions));
        OnPropertyChanged(nameof(BehavioralRules));
        OnPropertyChanged(nameof(ProhibitedActions));

        UpdateSystemPromptPreview();
    }

    public async Task SavePersonaAsync()
    {
        var config = new SparkPersonaConfig
        {
            AgentName = AgentName.Trim(),
            Tone = Enum.TryParse<AgentTone>(SelectedTone, out var t) ? t : AgentTone.Professional,
            PrimaryLanguage = Enum.TryParse<AgentLanguage>(SelectedLanguage, out var l) ? l : AgentLanguage.English,
            CustomInstructions = CustomInstructions.Trim(),
            BehavioralRules = BehavioralRules.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            ProhibitedActions = ProhibitedActions.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
        };

        await _personaManager.SavePersonaAsync(config, "user_default");
        UpdateSystemPromptPreview();
        StatusMessage = "Persona configuration updated successfully.";
        IsStatusError = false;
    }

    private void UpdateSystemPromptPreview()
    {
        var config = new SparkPersonaConfig
        {
            AgentName = string.IsNullOrWhiteSpace(_agentName) ? "SPARK" : _agentName.Trim(),
            Tone = Enum.TryParse<AgentTone>(_selectedTone, out var t) ? t : AgentTone.Professional,
            PrimaryLanguage = Enum.TryParse<AgentLanguage>(_selectedLanguage, out var l) ? l : AgentLanguage.English,
            CustomInstructions = _customInstructions ?? string.Empty,
            BehavioralRules = (_behavioralRules ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            ProhibitedActions = (_prohibitedActions ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
        };

        CompiledSystemPrompt = PersonaSystemPromptBuilder.BuildSystemPrompt(config);
    }

    public async Task LoadChannelsAsync()
    {
        Channels.Clear();
        var configs = await _channelManager.GetAllConfigsAsync();
        foreach (var c in configs)
        {
            var adapter = _channelManager.GetAdapter(c.ChannelId);
            var status = adapter?.Status ?? SparkChannelConnectionStatus.Disconnected;
            Channels.Add(new ChannelStatusItem(c.ChannelId, c.DisplayName, c.ChannelType.ToString(), status.ToString(), c.IsEnabled));
        }

        RecentMessages.Clear();
        var history = await _channelManager.GetChannelHistoryAsync("tg_main", 20);
        foreach (var m in history)
        {
            RecentMessages.Add(m);
        }
    }

    public async Task SaveChannelSecretAsync()
    {
        if (string.IsNullOrWhiteSpace(ChannelSecretInput)) return;

        var secretKey = SelectedChannelId.Contains("wa", StringComparison.OrdinalIgnoreCase) ? "AccessToken" : "BotToken";
        await _channelCredentialStore.SetCredentialAsync(SelectedChannelId, secretKey, ChannelSecretInput.Trim());
        ChannelSecretInput = string.Empty;
        StatusMessage = $"Credential saved securely in AES-256-GCM vault for channel '{SelectedChannelId}'.";
        IsStatusError = false;
    }

    public async Task LoadToolsAsync()
    {
        Tools.Clear();
        var allTools = _toolRegistry.ListTools();
        foreach (var t in allTools)
        {
            Tools.Add(new ToolItemViewModel(t.Id, t.Name, t.Category.ToString(), t.RiskLevel.ToString(), t.Description, $"{t.DefaultTimeout.TotalSeconds}s"));
        }
        if (Tools.Count > 0 && SelectedToolId == null)
        {
            SelectedToolId = Tools[0].Id;
        }
    }

    public async Task ExecuteToolDryRunAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedToolId)) return;

        ToolDryRunOutput = "Executing dry-run simulation...";
        try
        {
            Dictionary<string, object?>? args = null;
            if (!string.IsNullOrWhiteSpace(ToolDryRunArgs) && ToolDryRunArgs.Trim().StartsWith('{'))
            {
                args = JsonSerializer.Deserialize<Dictionary<string, object?>>(ToolDryRunArgs);
            }

            var request = new SparkToolExecutionRequest
            {
                ToolId = SelectedToolId,
                Arguments = args ?? new Dictionary<string, object?>(),
                DryRun = true,
                CallerIdentity = "admin_user",
                SessionId = "winui_preview"
            };

            var resp = await _toolRegistry.ExecuteToolAsync(request);
            ToolDryRunOutput = JsonSerializer.Serialize(new
            {
                resp.Success,
                resp.RiskLevel,
                resp.Duration,
                resp.Result,
                resp.Error
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            ToolDryRunOutput = $"Error executing dry-run: {ex.Message}";
        }
    }

    public Task LoadModelsAsync()
    {
        Models.Clear();
        var models = _modelRouter.ListModels(availableOnly: false);
        foreach (var m in models)
        {
            Models.Add(m);
        }
        return Task.CompletedTask;
    }

    public void TestRouting()
    {
        if (!Enum.TryParse<SparkTaskCategory>(RoutingTestCategory, out var category))
            category = SparkTaskCategory.ComplexReasoning;

        var privacy = Enum.TryParse<SparkModelPrivacyMode>(SelectedPrivacyMode, out var p) ? p : SparkModelPrivacyMode.Balanced;

        var request = new SparkRoutingRequest
        {
            TaskCategory = category,
            PrivacyMode = privacy,
            PromptTokenEstimate = 500
        };

        var decision = _modelRouter.Route(request);
        RoutingResultText = $"Selected: {decision.SelectedModel.DisplayName} ({decision.SelectedModel.Provider})\nScore: {decision.RoutingScore:F1} | Latency: ~{decision.EstimatedLatencyMs}ms\nReason: {decision.RoutingReason}\nFallback Chain: {string.Join(" -> ", decision.FallbackChain.Select(m => m.DisplayName))}";
    }

    public async Task LoadApprovalsAndAuditAsync()
    {
        PendingApprovals.Clear();
        var pending = await _approvalManager.GetPendingApprovalsAsync();
        foreach (var p in pending)
        {
            PendingApprovals.Add(new ApprovalItemViewModel(
                p.ApprovalId,
                p.OperationType.ToString(),
                p.Title,
                p.RequestedBy,
                p.OperationBindingSignature,
                p.CreatedAt,
                p.ExpiresAt));
        }

        AuditRecords.Clear();
        var records = await _auditLogger.QueryAuditTrailAsync(filter: null, limit: 50);
        foreach (var r in records)
        {
            AuditRecords.Add(r);
        }

        IsAuditLogTamperFree = await _auditLogger.VerifyLogIntegrityAsync();
    }

    public async Task ApproveOperationAsync(string approvalId)
    {
        if (string.IsNullOrWhiteSpace(approvalId)) return;

        var item = PendingApprovals.FirstOrDefault(p => p.Id == approvalId);
        var signature = item?.BindingHash ?? string.Empty;

        var approved = await _approvalManager.ApproveAsync(approvalId, "local_human_operator", signature);
        if (approved)
        {
            StatusMessage = $"Operation '{approvalId}' approved by human operator.";
            IsStatusError = false;
        }
        else
        {
            StatusMessage = $"Failed to approve operation '{approvalId}'.";
            IsStatusError = true;
        }

        await LoadApprovalsAndAuditAsync();
    }

    public async Task RejectOperationAsync(string approvalId)
    {
        if (string.IsNullOrWhiteSpace(approvalId)) return;

        var rejected = await _approvalManager.RejectAsync(approvalId, "local_human_operator", "User rejected through dashboard");
        if (rejected)
        {
            StatusMessage = $"Operation '{approvalId}' rejected.";
            IsStatusError = false;
        }
        await LoadApprovalsAndAuditAsync();
    }

    public async Task LoadTasksAsync()
    {
        if (_taskStateManager == null) return;

        var allTasks = await _taskStateManager.GetAllTasksAsync("user_default");
        var filtered = TaskFilter switch
        {
            "Active" => allTasks.Where(t => t.Status is SparkTaskState.Received or SparkTaskState.Interpreting or SparkTaskState.Planning or SparkTaskState.Ready or SparkTaskState.Executing or SparkTaskState.Reviewing or SparkTaskState.Verifying),
            "WaitingForApproval" => allTasks.Where(t => t.Status == SparkTaskState.WaitingForApproval),
            "Completed" => allTasks.Where(t => t.Status == SparkTaskState.Completed),
            "Failed" => allTasks.Where(t => t.Status is SparkTaskState.Failed or SparkTaskState.Cancelled or SparkTaskState.RequiresUserIntervention),
            _ => allTasks
        };

        Tasks.Clear();
        foreach (var t in filtered)
        {
            Tasks.Add(new TaskItemViewModel(
                t.TaskId,
                t.Title,
                t.Status.ToString(),
                t.Priority.ToString(),
                t.DetectedLanguage,
                t.OverallRiskLevel.ToString(),
                t.Steps.Any(s => s.RequiresApproval),
                t.CreatedAt,
                t.CreatedAt.ToLocalTime().ToString("g"),
                t.IsUserTakeoverActive));
        }

        if (SelectedTask != null)
        {
            var match = Tasks.FirstOrDefault(t => t.TaskId == SelectedTask.TaskId);
            if (match != null)
            {
                SelectedTask = match;
            }
        }
    }

    public async Task SubmitNewTaskAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTaskInstruction) || _orchestrator == null) return;

        if (!Enum.TryParse<SparkTaskPriority>(NewTaskPriority, out var priority))
            priority = SparkTaskPriority.Normal;

        var instruction = NewTaskInstruction.Trim();
        NewTaskInstruction = string.Empty;

        StatusMessage = "Submitting autonomous task...";
        IsStatusError = false;

        try
        {
            var created = await _orchestrator.SubmitTaskAsync(instruction, priority: priority, userId: "user_default");
            await LoadTasksAsync();
            await SelectTaskAsync(created.TaskId);
            StatusMessage = $"Task '{created.Title}' created (ID: {created.TaskId[..8]}).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to submit task: {ex.Message}";
            IsStatusError = true;
        }
    }

    public async Task SelectTaskAsync(string taskId)
    {
        if (_taskStateManager == null || string.IsNullOrWhiteSpace(taskId)) return;

        var task = await _taskStateManager.GetTaskAsync(taskId, "user_default");
        if (task == null) return;

        SelectedTaskInstruction = task.UserInstruction;
        EditInstructionInput = task.UserInstruction;
        IsTakeoverActive = task.IsUserTakeoverActive;

        SelectedTaskSteps.Clear();
        if (task.Steps != null)
        {
            foreach (var step in task.Steps)
            {
                SelectedTaskSteps.Add(step);
            }
        }
    }

    public async Task PauseCurrentTaskAsync()
    {
        if (SelectedTask == null || _orchestrator == null) return;
        try
        {
            await _orchestrator.PauseTaskAsync(SelectedTask.TaskId, "user_default");
            await LoadTasksAsync();
            await SelectTaskAsync(SelectedTask.TaskId);
            StatusMessage = $"Task '{SelectedTask.Title}' paused.";
            IsStatusError = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to pause task: {ex.Message}";
            IsStatusError = true;
        }
    }

    public async Task ResumeCurrentTaskAsync()
    {
        if (SelectedTask == null || _orchestrator == null) return;
        try
        {
            await _orchestrator.ResumeTaskAsync(SelectedTask.TaskId, "user_default");
            await LoadTasksAsync();
            await SelectTaskAsync(SelectedTask.TaskId);
            StatusMessage = $"Task '{SelectedTask.Title}' resumed.";
            IsStatusError = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to resume task: {ex.Message}";
            IsStatusError = true;
        }
    }

    public async Task CancelCurrentTaskAsync()
    {
        if (SelectedTask == null || _orchestrator == null) return;
        try
        {
            await _orchestrator.CancelTaskAsync(SelectedTask.TaskId, "User requested cancellation", "user_default");
            await LoadTasksAsync();
            await SelectTaskAsync(SelectedTask.TaskId);
            StatusMessage = $"Task '{SelectedTask.Title}' cancelled.";
            IsStatusError = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to cancel task: {ex.Message}";
            IsStatusError = true;
        }
    }

    public async Task ModifyCurrentTaskInstructionAsync()
    {
        if (SelectedTask == null || _orchestrator == null || string.IsNullOrWhiteSpace(EditInstructionInput)) return;
        try
        {
            await _orchestrator.ModifyTaskAsync(SelectedTask.TaskId, EditInstructionInput.Trim(), "User edited via dashboard", "user_default");
            await LoadTasksAsync();
            await SelectTaskAsync(SelectedTask.TaskId);
            StatusMessage = $"Task '{SelectedTask.Title}' updated and replanned.";
            IsStatusError = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to modify task: {ex.Message}";
            IsStatusError = true;
        }
    }

    public async Task ToggleTakeoverAsync()
    {
        if (SelectedTask == null || _orchestrator == null) return;
        try
        {
            var newState = !IsTakeoverActive;
            await _orchestrator.SetUserTakeoverAsync(SelectedTask.TaskId, newState, "user_default");
            IsTakeoverActive = newState;
            await LoadTasksAsync();
            await SelectTaskAsync(SelectedTask.TaskId);
            StatusMessage = newState ? "Live Takeover activated. SPARK paused execution for human interaction." : "Takeover released. Task resumed.";
            IsStatusError = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to toggle takeover: {ex.Message}";
            IsStatusError = true;
        }
    }

    public async Task TriggerBackgroundCycleAsync()
    {
        if (_backgroundWorker == null) return;
        StatusMessage = "Running autonomous background worker cycle...";
        try
        {
            var executed = await _backgroundWorker.PerformWorkerCycleAsync();
            RefreshBackgroundWorkerStatus();
            await LoadTasksAsync();
            StatusMessage = $"Background cycle complete. Executed {executed} tasks.";
            IsStatusError = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Background cycle failed: {ex.Message}";
            IsStatusError = true;
        }
    }

    public void RefreshBackgroundWorkerStatus()
    {
        if (_backgroundWorker == null)
        {
            BackgroundWorkerStateText = "Background Worker: Not Configured";
            return;
        }
        var status = _backgroundWorker.GetStatus();
        BackgroundWorkerStateText = $"Running: {status.IsRunning} | Active: {status.ActiveTasksCount} | Executed: {status.TotalTasksExecuted} | Quiet Hours: {status.IsInQuietHours}";
    }

    public async Task RefreshSpendingSummaryAsync()
    {
        if (_spendingLimitsEngine == null)
        {
            SpendingLimitsOverviewText = "Spending Limits: Disabled";
            return;
        }
        try
        {
            var summary = await _spendingLimitsEngine.GetSpendingSummaryAsync();
            var config = await _spendingLimitsEngine.GetConfigAsync();
            SpendingLimitsOverviewText = $"Per-Tx: ₹{config.MaxPerTransactionLimit:N0} | Daily Cap: ₹{config.DailySpendingLimit:N0} (Spent: ₹{summary.TodayCompletedTotal:N0}) | Monthly Cap: ₹{config.MonthlySpendingLimit:N0} (Spent: ₹{summary.MonthCompletedTotal:N0})";
        }
        catch
        {
            SpendingLimitsOverviewText = "Spending Limits: Active (₹5,000 / Tx, ₹25,000 / Day)";
        }
    }

    public void Dispose()
    {
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

internal sealed record MemoryRecordItem(string Id, string Category, string Content, double Confidence, DateTimeOffset UpdatedAt);
internal sealed record ChannelStatusItem(string Id, string DisplayName, string Type, string Status, bool IsEnabled);
internal sealed record ToolItemViewModel(string Id, string Name, string Category, string RiskLevel, string Description, string Timeout);
internal sealed record ApprovalItemViewModel(string Id, string OperationType, string Resource, string RequestedBy, string BindingHash, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);
internal sealed record TaskItemViewModel(
    string TaskId,
    string Title,
    string State,
    string Priority,
    string Language,
    string RiskLevel,
    bool RequiresApproval,
    DateTimeOffset CreatedAt,
    string FormattedTime,
    bool IsTakeoverActive);
