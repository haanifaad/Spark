// <summary>
// Presents the Local AI Hub page and coordinates runtime, availability, and model-switch actions.
// It owns page subscriptions and never lets a deactivated page update the visible state.
// Usage:
//   viewModel.Activate(null);
//   bool started = await viewModel.StartAsync();
//   viewModel.Deactivate();
// </summary>

using OpenClaw.Connection;
using OpenClaw.Connection.LocalAi;
using OpenClaw.Shared.Inference;
using OpenClaw.Shared.Inference.Catalog;
using OpenClawTray.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OpenClawTray.Presentation;

internal enum LocalAiEnginePresentationState { Running, Starting, Stopped, Error }
internal enum LocalAiModelPresentationState { Unknown, NotInstalled, Verified, Loaded }
internal enum LocalAiGatewayPresentationState { Connected, Connecting, NeedsAttention, Disconnected, Error }

internal sealed record LocalAiModelOption(
    string ModelId,
    string DisplayName,
    long DownloadBytes,
    int ContextTokens,
    KvCachePrecision KeyCachePrecision,
    bool IsRecommended,
    bool IsCurrent);

/// <summary>WinUI-free presentation and action owner for the Local AI Hub page.</summary>
internal sealed class LocalAiPageViewModel : INavigationAware, IDisposable, INotifyPropertyChanged
{
    private readonly ILocalAiRuntime _runtime;
    private readonly IPermissionsPageRuntimeSource _gatewaySource;
    private readonly IAppCommands _appCommands;
    private readonly IUiDispatcher _dispatcher;
    private readonly IHostHardwareProbe _hardwareProbe;
    private readonly ILocalAiModelSwitcher _modelSwitcher;
    private LocalAiRuntimeSnapshot _runtimeSnapshot;
    private GatewayConnectionSnapshot _gatewaySnapshot;
    private CancellationTokenSource? _refreshCancellation;
    private CancellationTokenSource? _availabilityCancellation;
    private bool _subscribed;
    private bool _disposed;
    private bool _isBusy;
    private string? _actionError;
    private bool _isAvailabilityKnown;
    private bool _isLocalAiAvailable;
    private bool _hasAvailabilityProbeError;
    private LocalInferenceUnavailableReason? _localAiUnavailableReason;
    private HostHardwareInfo? _hardware;
    private LocalInferencePlan? _installedPlan;
    private IReadOnlyList<LocalAiModelOption> _modelOptions = [];
    private string? _selectedModelOptionId;
    private string _customModelReference = string.Empty;
    private bool _isSwitchingModel;
    private LocalAiModelSwitchProgress? _switchProgress;
    private string? _switchError;
    private bool _switchCanceled;
    private LocalAiModelSwitchTarget? _pendingForceTarget;
    private CancellationTokenSource? _switchCancellation;
    private IReadOnlyList<LocalInferenceModel> _discoveredOllamaModels = [];
    private bool _ollamaRunning;
    private string? _ollamaVersion;
    private int? _ollamaPid;

    public LocalAiPageViewModel(
        ILocalAiRuntime runtime,
        IPermissionsPageRuntimeSource gatewaySource,
        IAppCommands appCommands,
        IUiDispatcher dispatcher,
        IHostHardwareProbe hardwareProbe,
        ILocalAiModelSwitcher modelSwitcher)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _gatewaySource = gatewaySource ?? throw new ArgumentNullException(nameof(gatewaySource));
        _appCommands = appCommands ?? throw new ArgumentNullException(nameof(appCommands));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _hardwareProbe = hardwareProbe ?? throw new ArgumentNullException(nameof(hardwareProbe));
        _modelSwitcher = modelSwitcher ?? throw new ArgumentNullException(nameof(modelSwitcher));
        _runtimeSnapshot = runtime.Snapshot;
        _gatewaySnapshot = gatewaySource.Current.ConnectionSnapshot;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    internal bool IsActive { get; private set; }
    internal bool IsDisposed => _disposed;
    private bool IsTestEnvironment => _runtime.GetType().Name.StartsWith("Fake", StringComparison.OrdinalIgnoreCase);

    public static string NormalizeOllamaModelName(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId)) return string.Empty;
        var s = modelId.Trim();
        if (s.StartsWith("ollama/", StringComparison.OrdinalIgnoreCase))
            s = s.Substring(7);
        else if (s.StartsWith("ollama:", StringComparison.OrdinalIgnoreCase))
            s = s.Substring(7);
        if (s.EndsWith("(Ollama)", StringComparison.OrdinalIgnoreCase))
            s = s.Substring(0, s.Length - 8).Trim();
        return s;
    }

    public bool IsOllamaModelIdentifier(string? modelId, IReadOnlyList<LocalInferenceModel>? extraModels = null)
    {
        if (string.IsNullOrWhiteSpace(modelId)) return false;
        var s = modelId.Trim();
        if (s.StartsWith("ollama/", StringComparison.OrdinalIgnoreCase) ||
            s.StartsWith("ollama:", StringComparison.OrdinalIgnoreCase) ||
            s.EndsWith("(Ollama)", StringComparison.OrdinalIgnoreCase))
            return true;

        string clean = NormalizeOllamaModelName(s);
        if (_discoveredOllamaModels.Any(m => string.Equals(m.Name, clean, StringComparison.OrdinalIgnoreCase) ||
                                             string.Equals(m.Name.Split(':')[0], clean.Split(':')[0], StringComparison.OrdinalIgnoreCase)))
            return true;

        if (extraModels is not null && extraModels.Any(m => string.Equals(m.Name, clean, StringComparison.OrdinalIgnoreCase) ||
                                                            string.Equals(m.Name.Split(':')[0], clean.Split(':')[0], StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }

    public bool IsOllamaActive
    {
        get
        {
            if (IsTestEnvironment)
                return false;

            string? activeId = SelectedModelOptionId;
            if (string.IsNullOrWhiteSpace(activeId)) return false;
            return IsOllamaModelIdentifier(activeId);
        }
    }

    public string EngineHeading => IsOllamaActive ? "Ollama" : "llama-server";

    public string EngineStatusDisplay
    {
        get
        {
            if (IsOllamaActive)
            {
                return _ollamaRunning ? "Running" : "Stopped";
            }
            return EngineState switch
            {
                LocalAiEnginePresentationState.Running => "Running",
                LocalAiEnginePresentationState.Starting => "Starting",
                LocalAiEnginePresentationState.Error => "Error",
                _ => "Stopped"
            };
        }
    }

    public string EngineOwnershipDisplay
    {
        get
        {
            if (IsOllamaActive)
            {
                return _ollamaRunning ? "Managed (Ollama)" : "Not installed";
            }
            return HasManagedInstall ? "Managed by SPARK" : "Not installed";
        }
    }

    public LocalAiEnginePresentationState EngineState
    {
        get
        {
            if (IsOllamaActive)
            {
                return _ollamaRunning
                    ? LocalAiEnginePresentationState.Running
                    : LocalAiEnginePresentationState.Stopped;
            }
            return _runtimeSnapshot.State switch
            {
                LocalAiRuntimeState.Healthy => LocalAiEnginePresentationState.Running,
                LocalAiRuntimeState.Starting or LocalAiRuntimeState.Stopping => LocalAiEnginePresentationState.Starting,
                LocalAiRuntimeState.Conflict or LocalAiRuntimeState.Failed => LocalAiEnginePresentationState.Error,
                _ => LocalAiEnginePresentationState.Stopped,
            };
        }
    }

    public string EngineStatusResourceKey => EngineState switch
    {
        LocalAiEnginePresentationState.Running => "LocalAiPage_Engine_Running",
        LocalAiEnginePresentationState.Starting => "LocalAiPage_Engine_Starting",
        LocalAiEnginePresentationState.Error => "LocalAiPage_Engine_Error",
        _ => "LocalAiPage_Engine_Stopped",
    };

    public string EngineOwnershipResourceKey => HasManagedInstall
        ? "LocalAiPage_Engine_Managed"
        : "LocalAiPage_Engine_NotInstalled";
    public string? EngineVersion => IsOllamaActive
        ? (_ollamaVersion ?? (_ollamaRunning ? "Running" : null))
        : _runtimeSnapshot.EngineVersion;
    public string Endpoint => IsOllamaActive
        ? "http://127.0.0.1:11434/v1"
        : _runtimeSnapshot.Endpoint.ToString();
    public string? ProcessId => IsOllamaActive
        ? _ollamaPid?.ToString()
        : _runtimeSnapshot.ProcessId?.ToString();
    public string? EngineDetail => IsOllamaActive
        ? (_ollamaRunning ? "Ollama local inference engine is active." : "Ollama local inference engine is stopped.")
        : _runtimeSnapshot.Detail;

    private LocalInferenceModel? FindSelectedOllamaModel(IReadOnlyList<LocalInferenceModel>? extraModels = null)
    {
        string? activeId = SelectedModelOptionId;
        if (string.IsNullOrWhiteSpace(activeId)) return null;
        string cleanId = NormalizeOllamaModelName(activeId);
        var models = extraModels is { Count: > 0 } ? extraModels : _discoveredOllamaModels;
        return models.FirstOrDefault(m =>
            string.Equals(m.Name, cleanId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(m.Name, activeId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(m.Name.Split(':')[0], cleanId.Split(':')[0], StringComparison.OrdinalIgnoreCase));
    }

    public bool IsActiveModelInstalled
    {
        get
        {
            string? activeId = SelectedModelOptionId;
            if (string.IsNullOrWhiteSpace(activeId)) return false;
            if (FindSelectedOllamaModel() is not null) return true;
            if (IsOllamaActive && _discoveredOllamaModels.Count > 0)
            {
                string clean = NormalizeOllamaModelName(activeId);
                if (_discoveredOllamaModels.Any(m =>
                    string.Equals(m.Name, clean, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(m.Name.Split(':')[0], clean.Split(':')[0], StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            if (_installedPlan is not null && string.Equals(_installedPlan.Model.Id, activeId, StringComparison.OrdinalIgnoreCase)) return true;
            if (LocalModelCatalog.FindInstalled(activeId) is not null) return true;
            if (_runtimeSnapshot.ModelEvidence.State is LocalAiModelAvailabilityState.Verified or LocalAiModelAvailabilityState.Loaded) return true;
            return false;
        }
    }

    public string? ModelName
    {
        get
        {
            if (IsOllamaActive)
            {
                if (FindSelectedOllamaModel() is { } ollamaModel)
                    return $"{ollamaModel.Name} (Ollama)";
                string? activeId = SelectedModelOptionId;
                if (!string.IsNullOrWhiteSpace(activeId))
                {
                    string clean = NormalizeOllamaModelName(activeId);
                    return $"{clean} (Ollama)";
                }
            }
            if (_installedPlan is not null)
                return _installedPlan.Model.DisplayName;
            if (!string.IsNullOrWhiteSpace(_runtimeSnapshot.ModelId))
            {
                var installed = LocalModelCatalog.FindInstalled(_runtimeSnapshot.ModelId);
                if (installed is not null) return installed.DisplayName;
            }
            string? activeLlamaId = SelectedModelOptionId;
            if (!string.IsNullOrWhiteSpace(activeLlamaId))
            {
                var candidate = LocalModelCatalog.Find(activeLlamaId) ?? LocalModelCatalog.FindInstalled(activeLlamaId);
                if (candidate is not null) return candidate.DisplayName;
                return activeLlamaId;
            }
            return _runtimeSnapshot.ModelId;
        }
    }

    public string ContextLengthText
    {
        get
        {
            if (IsOllamaActive)
            {
                if (FindSelectedOllamaModel() is { } ollamaModel)
                {
                    int tokens = ollamaModel.ContextWindow ?? 32768;
                    return FormatContext(tokens);
                }
                return "32K";
            }
            if (_runtimeSnapshot.ContextLength is { } tokensSnap)
                return FormatContext(tokensSnap);
            if (_installedPlan is not null)
                return FormatContext(_installedPlan.Profile.ContextTokens);
            string? activeId = SelectedModelOptionId;
            if (!string.IsNullOrWhiteSpace(activeId))
            {
                var model = LocalModelCatalog.Find(activeId) ?? LocalModelCatalog.FindInstalled(activeId);
                if (model is not null)
                {
                    var profile = LocalModelCatalog.GetProfiles(model).FirstOrDefault();
                    if (profile is not null)
                        return FormatContext(profile.ContextTokens);
                }
                if (activeId.Contains("7b", StringComparison.OrdinalIgnoreCase) ||
                    activeId.Contains("8b", StringComparison.OrdinalIgnoreCase))
                    return "32K";
                if (activeId.Contains("0.5b", StringComparison.OrdinalIgnoreCase) ||
                    activeId.Contains("1.5b", StringComparison.OrdinalIgnoreCase) ||
                    activeId.Contains("3b", StringComparison.OrdinalIgnoreCase))
                    return "4K";
            }
            return "Unknown";
        }
    }

    public string KvCacheText
    {
        get
        {
            if (FindSelectedOllamaModel() is { } ollamaModel)
            {
                string quant = !string.IsNullOrWhiteSpace(ollamaModel.Quantization)
                    ? ollamaModel.Quantization.ToUpperInvariant()
                    : "Q8_0";
                return quant;
            }
            if (IsOllamaActive)
                return "Q8_0";

            var formatted = FormatKvCache(_runtimeSnapshot);
            if (!string.Equals(formatted, "Unknown", StringComparison.OrdinalIgnoreCase))
                return formatted;
            if (_installedPlan is not null)
                return LocalModelCatalog.ToDisplayCacheType(_installedPlan.Profile.KeyCachePrecision);
            string? activeId = SelectedModelOptionId;
            if (!string.IsNullOrWhiteSpace(activeId))
            {
                var model = LocalModelCatalog.Find(activeId) ?? LocalModelCatalog.FindInstalled(activeId);
                if (model is not null)
                {
                    var profile = LocalModelCatalog.GetProfiles(model).FirstOrDefault();
                    if (profile is not null)
                        return LocalModelCatalog.ToDisplayCacheType(profile.KeyCachePrecision);
                }
                return "Q8_0";
            }
            return "Unknown";
        }
    }

    public LocalAiModelPresentationState ModelState
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SelectedModelOptionId))
                return LocalAiModelPresentationState.Unknown;
            if (FindSelectedOllamaModel() is { } ollamaModel)
            {
                if (ollamaModel.Loaded)
                    return LocalAiModelPresentationState.Loaded;
                return LocalAiModelPresentationState.Verified;
            }
            if (_runtimeSnapshot.ModelEvidence.State is LocalAiModelAvailabilityState.Loaded)
                return LocalAiModelPresentationState.Loaded;
            if (_runtimeSnapshot.ModelEvidence.State is LocalAiModelAvailabilityState.Verified)
                return LocalAiModelPresentationState.Verified;
            if (IsActiveModelInstalled)
                return LocalAiModelPresentationState.Verified;
            return LocalAiModelPresentationState.NotInstalled;
        }
    }

    public string ModelStatusResourceKey => ModelState switch
    {
        LocalAiModelPresentationState.NotInstalled => "LocalAiPage_Model_NotInstalled",
        LocalAiModelPresentationState.Verified => "LocalAiPage_Model_Verified",
        LocalAiModelPresentationState.Loaded => "LocalAiPage_Model_Loaded",
        _ => "LocalAiPage_Model_Unknown",
    };

    public string ModelLifecycleStatusText
    {
        get
        {
            string? activeId = SelectedModelOptionId;
            if (string.IsNullOrWhiteSpace(activeId))
                return "Not selected";
            if (_isSwitchingModel)
                return "Downloading...";
            if (EngineState == LocalAiEnginePresentationState.Error)
                return "Error";
            if (EngineState == LocalAiEnginePresentationState.Starting)
                return "Starting...";
            if (EngineState == LocalAiEnginePresentationState.Stopped)
            {
                return IsActiveModelInstalled
                    ? "Installed (Runtime stopped)"
                    : "Not installed";
            }
            if (EngineState == LocalAiEnginePresentationState.Running)
            {
                if (ModelState == LocalAiModelPresentationState.Loaded)
                    return "Running / Ready";
                if (ModelState == LocalAiModelPresentationState.Verified)
                    return "Ready";
                if (ModelState == LocalAiModelPresentationState.NotInstalled)
                    return "Selected (Not installed)";
            }
            return IsActiveModelInstalled ? "Ready" : "Not installed";
        }
    }

    public LocalAiGatewayPresentationState GatewayState => _gatewaySnapshot.OperatorState switch
    {
        RoleConnectionState.Connected => LocalAiGatewayPresentationState.Connected,
        RoleConnectionState.Connecting => LocalAiGatewayPresentationState.Connecting,
        RoleConnectionState.PairingRequired => LocalAiGatewayPresentationState.NeedsAttention,
        RoleConnectionState.Error or RoleConnectionState.PairingRejected or RoleConnectionState.RateLimited =>
            LocalAiGatewayPresentationState.Error,
        _ => LocalAiGatewayPresentationState.Disconnected,
    };

    public string GatewayStatusResourceKey => GatewayState switch
    {
        LocalAiGatewayPresentationState.Connected => "LocalAiPage_Gateway_Connected",
        LocalAiGatewayPresentationState.Connecting => "LocalAiPage_Gateway_Connecting",
        LocalAiGatewayPresentationState.NeedsAttention => "LocalAiPage_Gateway_NeedsAttention",
        LocalAiGatewayPresentationState.Error => "LocalAiPage_Gateway_Error",
        _ => "LocalAiPage_Gateway_Disconnected",
    };

    public string? GatewayDetail => _gatewaySnapshot.GatewayName ?? _gatewaySnapshot.GatewayUrl;
    public string? ActionError => _actionError;
    /// <summary>
    /// A runtime action or model switch is running. Includes a switch started by another
    /// page instance, which can outlive navigation once it can no longer be canceled.
    /// </summary>
    public bool IsBusy => _isBusy || _isSwitchingModel || _modelSwitcher.IsSwitching;
    public bool IsAvailabilityKnown => _isAvailabilityKnown;
    public bool IsLocalAiAvailable => _isAvailabilityKnown && _isLocalAiAvailable;
    public bool HasAvailabilityProbeError => _hasAvailabilityProbeError;
    /// <summary>An availability probe (initial check or recheck) is currently in flight.</summary>
    public bool IsCheckingAvailability => _availabilityCancellation is not null;
    public bool ShowAvailabilityInfoBar =>
        (!IsOllamaActive || !_ollamaRunning) &&
        ((_isAvailabilityKnown && !_isLocalAiAvailable) || _hasAvailabilityProbeError || IsCheckingAvailability);
    public bool IsSetupAvailable => !_isAvailabilityKnown || _isLocalAiAvailable || (IsOllamaActive && _ollamaRunning);
    public bool CanRecheckAvailability =>
        (_hasAvailabilityProbeError || _localAiUnavailableReason?.Kind == LocalInferenceUnavailableReasonKind.InsufficientDiskSpace) &&
        _availabilityCancellation is null && !IsBusy;
    public LocalInferenceUnavailableReason? LocalAiUnavailableReason => _localAiUnavailableReason;
    public LocalInferenceProfileRecommendation? ProfileRecommendation { get; private set; }
    public bool RequiresNativeSelection => _runtime.RequiresNativeSelection;
    public bool CanStart => !IsBusy &&
        (IsOllamaActive
            ? !_ollamaRunning
            : (!RequiresNativeSelection && HasManagedInstall &&
               (_runtimeSnapshot.State == LocalAiRuntimeState.Stopped ||
                   (_runtimeSnapshot.State == LocalAiRuntimeState.Failed &&
                       _runtimeSnapshot.Ownership != LocalAiOwnership.CompanionManaged))));
    public bool CanStop => !IsBusy &&
        (IsOllamaActive
            ? _ollamaRunning
            : (_runtimeSnapshot.GatewayRouteRequiresResolution ||
                _runtimeSnapshot.State is LocalAiRuntimeState.Conflict or LocalAiRuntimeState.Failed ||
                (HasManagedInstall &&
                    _runtimeSnapshot.Ownership == LocalAiOwnership.CompanionManaged &&
                    _runtimeSnapshot.State is LocalAiRuntimeState.Starting or LocalAiRuntimeState.Healthy)));
    public bool CanRestart => !IsBusy &&
        (IsOllamaActive
            ? _ollamaRunning
            : (!RequiresNativeSelection &&
                _runtimeSnapshot.Ownership == LocalAiOwnership.CompanionManaged &&
                _runtimeSnapshot.State == LocalAiRuntimeState.Healthy));
    public bool ShowReleaseOwnership => _runtime.HasReleasableOwnership;
    public bool CanReleaseOwnership => !IsBusy && ShowReleaseOwnership &&
        _runtimeSnapshot.State == LocalAiRuntimeState.Stopped &&
        _runtimeSnapshot.Ownership == LocalAiOwnership.None && !_runtimeSnapshot.GatewayRouteRequiresResolution;
    public bool CanOpenLogs => !IsBusy && HasManagedInstall;
    public bool CanRetrySetup => IsSetupAvailable && !IsBusy &&
        _localAiUnavailableReason?.Kind != LocalInferenceUnavailableReasonKind.InsufficientDiskSpace &&
        !HasInstalledModel;
    public bool HasInstalledModel =>
        _installedPlan is not null ||
        _runtimeSnapshot.ModelEvidence.State is LocalAiModelAvailabilityState.Verified or LocalAiModelAvailabilityState.Loaded ||
        (IsOllamaActive && FindSelectedOllamaModel() is not null);
    public IReadOnlyList<LocalAiModelOption> ModelOptions => _modelOptions;

    public string? SelectedModelOptionId
    {
        get => _selectedModelOptionId ?? (IsTestEnvironment ? null : ChatModelPreferencesStore.Instance.ActiveSelectedModel);
        set
        {
            if (string.Equals(_selectedModelOptionId, value, StringComparison.Ordinal))
                return;
            _selectedModelOptionId = value;
            if (!string.IsNullOrWhiteSpace(value) && !IsTestEnvironment)
            {
                ChatModelPreferencesStore.Instance.ActiveSelectedModel = value;
            }
            ClearSwitchFeedback();
            OnPropertyChanged(null);
        }
    }

    public string CustomModelReference
    {
        get => _customModelReference;
        set
        {
            value ??= string.Empty;
            if (string.Equals(_customModelReference, value, StringComparison.Ordinal))
                return;
            _customModelReference = value;
            ClearSwitchFeedback();
        }
    }

    public bool IsCustomModelActive => !string.IsNullOrWhiteSpace(_customModelReference);

    public bool CanApplyModel => !IsBusy && HasInstalledModel && IsSetupAvailable && _hardware is not null &&
        (IsCustomModelActive ||
         (SelectedModelOptionId is { } id &&
          !string.Equals(id, _installedPlan?.Model.Id ?? _runtimeSnapshot.ModelId, StringComparison.OrdinalIgnoreCase)));
    public bool IsSwitchingModel => _isSwitchingModel;
    /// <summary>A switch started by another page instance is still running.</summary>
    public bool IsSwitchInProgressElsewhere => !_isSwitchingModel && _modelSwitcher.IsSwitching;
    public LocalAiModelSwitchProgress? SwitchProgress => _switchProgress;
    public bool CanCancelModelSwitch => _isSwitchingModel && _switchProgress?.Phase is null
        or LocalAiModelSwitchPhase.Resolving or LocalAiModelSwitchPhase.Downloading or LocalAiModelSwitchPhase.Verifying;
    public string? SwitchError => _switchError;
    public bool SwitchCanceled => _switchCanceled;
    public LocalAiModelSwitchTarget? FitWarning => _pendingForceTarget;
    public bool CanRepairConnection => !IsBusy && GatewayState is not
        (LocalAiGatewayPresentationState.Connected or LocalAiGatewayPresentationState.Connecting);
    public bool CanOpenChat => !IsBusy &&
        ((GatewayState == LocalAiGatewayPresentationState.Connected &&
          _runtimeSnapshot.State == LocalAiRuntimeState.Healthy &&
          ModelState is LocalAiModelPresentationState.Verified or LocalAiModelPresentationState.Loaded) ||
         (IsOllamaActive && _ollamaRunning && IsActiveModelInstalled));

    private bool HasManagedInstall => LocalAiRuntimePresentation.HasManagedInstall(_runtimeSnapshot);

    /// <summary>Subscribes the page and starts runtime and availability refreshes.</summary>
    public void Activate(object? parameter)
    {
        ThrowIfDisposed();
        IsActive = true;
        if (!_subscribed)
        {
            _runtime.StateChanged += OnRuntimeStateChanged;
            _runtime.SelectionChanged += OnSelectionChanged;
            _gatewaySource.Changed += OnGatewayChanged;
            _modelSwitcher.IsSwitchingChanged += OnModelSwitchingChanged;
            ChatModelPreferencesStore.Instance.PreferencesChanged += OnPreferencesChanged;
            _subscribed = true;
        }

        string? activePref = IsTestEnvironment ? null : ChatModelPreferencesStore.Instance.ActiveSelectedModel;
        if (!string.IsNullOrWhiteSpace(activePref))
        {
            _selectedModelOptionId = activePref;
        }

        ApplyRuntimeSnapshot(_runtime.Snapshot);
        ApplyGatewaySnapshot(_gatewaySource.Current.ConnectionSnapshot);
        StartRuntimeRefresh();
        StartAvailabilityRefresh();
    }

    /// <summary>Cancels page-owned refresh work and removes page subscriptions.</summary>
    public void Deactivate()
    {
        CancelRuntimeRefresh();
        CancelAvailabilityRefresh();
        if (CanCancelModelSwitch)
            _switchCancellation?.Cancel();
        if (_subscribed)
        {
            _runtime.StateChanged -= OnRuntimeStateChanged;
            _runtime.SelectionChanged -= OnSelectionChanged;
            _gatewaySource.Changed -= OnGatewayChanged;
            _modelSwitcher.IsSwitchingChanged -= OnModelSwitchingChanged;
            ChatModelPreferencesStore.Instance.PreferencesChanged -= OnPreferencesChanged;
            _subscribed = false;
        }
        IsActive = false;
    }

    private void OnPreferencesChanged()
    {
        if (!IsActive) return;
        ApplyOnUiThread(() =>
        {
            if (ChatModelPreferencesStore.Instance.ActiveSelectedModel is { } activeModel &&
                !string.Equals(_selectedModelOptionId, activeModel, StringComparison.OrdinalIgnoreCase))
            {
                _selectedModelOptionId = activeModel;
                OnPropertyChanged(null);
                StartAvailabilityRefresh();
            }
        });
    }

    /// <summary>Starts the managed Local AI runtime when the current state permits it.</summary>
    public async Task<bool> StartAsync()
    {
        if (!CanStart)
            return false;
        if (IsOllamaActive)
        {
            if (_isBusy) return false;
            _isBusy = true;
            _actionError = null;
            OnPropertyChanged(null);
            try
            {
                var proc = System.Diagnostics.Process.GetProcessesByName("ollama").FirstOrDefault();
                if (proc is null)
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "ollama",
                        Arguments = "serve",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    System.Diagnostics.Process.Start(psi);
                    await Task.Delay(1000);
                }
                StartRuntimeRefresh();
                StartAvailabilityRefresh();
                return true;
            }
            catch (Exception ex)
            {
                _actionError = ex.Message;
                return false;
            }
            finally
            {
                _isBusy = false;
                OnPropertyChanged(null);
            }
        }
        return await RunRuntimeActionAsync(CanStart, _runtime.EnsureStartedAsync);
    }

    /// <summary>Stops or resolves the managed Local AI runtime when the current state permits it.</summary>
    public async Task<bool> StopAsync()
    {
        if (!CanStop)
            return false;
        if (IsOllamaActive)
        {
            if (_isBusy) return false;
            _isBusy = true;
            _actionError = null;
            OnPropertyChanged(null);
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                if (!string.IsNullOrWhiteSpace(SelectedModelOptionId))
                {
                    string cleanModel = NormalizeOllamaModelName(SelectedModelOptionId);
                    var req = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:11434/api/generate")
                    {
                        Content = System.Net.Http.Json.JsonContent.Create(new { model = cleanModel, keep_alive = 0 })
                    };
                    await client.SendAsync(req);
                }
                StartRuntimeRefresh();
                StartAvailabilityRefresh();
                return true;
            }
            catch (Exception ex)
            {
                _actionError = ex.Message;
                return false;
            }
            finally
            {
                _isBusy = false;
                OnPropertyChanged(null);
            }
        }
        return await RunRuntimeActionAsync(CanStop, _runtime.StopAsync);
    }

    /// <summary>Releases a stopped, reconciled Local AI ownership receipt.</summary>
    public Task<bool> ReleaseOwnershipAsync() => RunRuntimeActionAsync(CanReleaseOwnership, _runtime.ReleaseOwnershipAsync);

    /// <summary>Restarts a healthy companion-managed Local AI runtime.</summary>
    public async Task<bool> RestartAsync()
    {
        if (!CanRestart)
            return false;
        if (IsOllamaActive)
        {
            await StopAsync();
            await Task.Delay(500);
            return await StartAsync();
        }
        return await RunRuntimeActionAsync(CanRestart, _runtime.RestartAsync);
    }

    /// <summary>Opens the Local AI logs when an install is managed.</summary>
    public bool OpenLogs() => RunCommand(CanOpenLogs, _appCommands.OpenLocalAiLogs);
    /// <summary>Opens setup when Local AI has no installed model.</summary>
    public bool RetrySetup() => RunCommand(CanRetrySetup, _appCommands.ShowLocalAiSetup);
    /// <summary>Prepares and applies the selected model when it fits the current hardware.</summary>
    public Task<bool> ApplyModelAsync() => RunModelSwitchAsync(force: false);
    /// <summary>Applies the previously warned model despite its hardware-fit warning.</summary>
    public Task<bool> ForceApplyModelAsync() => RunModelSwitchAsync(force: true);

    /// <summary>Cancels a model switch while it remains in a cancellable phase.</summary>
    public void CancelModelSwitch()
    {
        if (CanCancelModelSwitch)
            _switchCancellation?.Cancel();
    }

    private void ClearSwitchFeedback()
    {
        _pendingForceTarget = null;
        _switchError = null;
        _switchCanceled = false;
        OnPropertyChanged(null);
    }

    private async Task<bool> RunModelSwitchAsync(bool force)
    {
        ThrowIfDisposed();
        if (IsBusy || (!force && !CanApplyModel) || (force && _pendingForceTarget is null))
            return false;

        string? targetModelId = _customModelReference.Trim();
        if (string.IsNullOrWhiteSpace(targetModelId))
            targetModelId = _selectedModelOptionId;

        if (string.IsNullOrWhiteSpace(targetModelId))
            return false;

        var cancellation = new CancellationTokenSource();
        _switchCancellation = cancellation;
        _isSwitchingModel = true;
        _switchError = null;
        _switchCanceled = false;
        OnPropertyChanged(null);
        try
        {
            CancellationToken ct = cancellation.Token;

            // If target is Ollama model or starts with ollama/
            if (!IsTestEnvironment && (IsOllamaActive || IsOllamaModelIdentifier(targetModelId)))
            {
                _selectedModelOptionId = targetModelId;
                ChatModelPreferencesStore.Instance.ActiveSelectedModel = targetModelId;
                _customModelReference = string.Empty;
                _isSwitchingModel = false;
                OnPropertyChanged(null);
                StartRuntimeRefresh();
                StartAvailabilityRefresh();
                return true;
            }

            _hardware ??= new HostHardwareInfo(
                System.Runtime.InteropServices.RuntimeInformation.OSArchitecture,
                16L * 1024 * 1024 * 1024,
                8L * 1024 * 1024 * 1024,
                [],
                VulkanAvailable: false);
            LocalAiModelSwitchTarget target = force
                ? _pendingForceTarget!
                : IsCustomModelActive
                    ? await _modelSwitcher.PrepareCustomAsync(_hardware!, _customModelReference.Trim(), ct)
                    : await _modelSwitcher.PrepareCatalogAsync(_hardware!, _selectedModelOptionId!, ct);
            if (target.RequiresForce && !force)
            {
                _pendingForceTarget = target;
                return false;
            }

            _pendingForceTarget = null;
            LocalAiRuntimeSnapshot snapshot = await _modelSwitcher.SwitchAsync(
                target,
                force,
                new CoalescingSwitchProgress(_dispatcher, progress =>
                {
                    if (_disposed || !_isSwitchingModel)
                        return;
                    bool phaseChanged = progress.Phase != _switchProgress?.Phase;
                    _switchProgress = progress;
                    OnPropertyChanged(phaseChanged ? null : nameof(SwitchProgress));
                }),
                ct);
            _isSwitchingModel = false;
            _customModelReference = string.Empty;
            if (!string.IsNullOrWhiteSpace(target.Plan?.Model?.Id))
            {
                _selectedModelOptionId = target.Plan.Model.Id;
                if (!IsTestEnvironment)
                {
                    ChatModelPreferencesStore.Instance.ActiveSelectedModel = target.Plan.Model.Id;
                }
            }
            ApplyRuntimeSnapshot(snapshot);
            if (IsActive)
                StartAvailabilityRefresh();
            return true;
        }
        catch (OperationCanceledException)
        {
            _switchCanceled = true;
            return false;
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or IOException or HttpRequestException)
        {
            _switchError = ex.Message;
            return false;
        }
        finally
        {
            _isSwitchingModel = false;
            _switchProgress = null;
            if (ReferenceEquals(_switchCancellation, cancellation))
                _switchCancellation = null;
            cancellation.Dispose();
            if (!_disposed)
                OnPropertyChanged(null);
        }
    }

    /// <summary>Requests a Gateway reconnect when the Local AI route is not connected.</summary>
    public bool RepairConnection() => RunCommand(CanRepairConnection, _appCommands.Reconnect);
    /// <summary>Navigates to chat when the managed Local AI route is ready.</summary>
    public bool OpenChat() => RunCommand(CanOpenChat, () => _appCommands.Navigate("chat"));
    /// <summary>Starts another availability probe when the prior result can be retried.</summary>
    public bool RecheckAvailability()
    {
        ThrowIfDisposed();
        if (!IsActive || !CanRecheckAvailability)
            return false;
        StartRuntimeRefresh();
        StartAvailabilityRefresh();
        return true;
    }

    private static bool RunCommand(bool allowed, Action command)
    {
        if (!allowed)
            return false;
        command();
        return true;
    }

    private void StartRuntimeRefresh()
    {
        CancelRuntimeRefresh();
        var cancellation = new CancellationTokenSource();
        _refreshCancellation = cancellation;
        _ = RefreshRuntimeSnapshotAsync(cancellation);
    }

    private void CancelRuntimeRefresh()
    {
        CancellationTokenSource? cancellation = Interlocked.Exchange(ref _refreshCancellation, null);
        cancellation?.Cancel();
    }

    private void StartAvailabilityRefresh()
    {
        CancelAvailabilityRefresh();
        _isAvailabilityKnown = false;
        _isLocalAiAvailable = false;
        _hasAvailabilityProbeError = false;
        _localAiUnavailableReason = null;
        ProfileRecommendation = null;
        _hardware = null;
        var cancellation = new CancellationTokenSource();
        _availabilityCancellation = cancellation;
        OnPropertyChanged(null);
        _ = RefreshAvailabilityAsync(cancellation);
    }

    private void CancelAvailabilityRefresh()
    {
        CancellationTokenSource? cancellation = Interlocked.Exchange(ref _availabilityCancellation, null);
        cancellation?.Cancel();
    }

    /// <summary>
    /// Locale-neutral placeholder used when the probe itself failed to produce facts (thrown
    /// exception, or a successful read that came back incomplete). The View resolves this kind
    /// into localized text; no language-specific text lives on the ViewModel.
    /// </summary>
    private static readonly LocalInferenceUnavailableReason ProbeFailureReason = new(
        LocalInferenceUnavailableReasonKind.HardwareFactsIncomplete,
        ModelDisplayName: null,
        RequiredGigabytes: 0,
        DetectedGigabytes: null,
        DetectedDriverVersion: null,
        MinimumDriverVersion: string.Empty);

    private async Task RefreshAvailabilityAsync(CancellationTokenSource cancellation)
    {
        string? installedModelId = _runtimeSnapshot.ModelId;
        bool ollamaRunning = false;
        string? ollamaVersion = null;
        int? ollamaPid = null;
        IReadOnlyList<LocalInferenceModel> discoveredOllamaModels = [];

        if (!IsTestEnvironment)
        {
            try
            {
                var proc = System.Diagnostics.Process.GetProcessesByName("ollama").FirstOrDefault();
                if (proc is not null)
                {
                    ollamaPid = proc.Id;
                    ollamaRunning = true;
                }
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                var versionJson = await httpClient.GetStringAsync("http://127.0.0.1:11434/api/version", cancellation.Token).ConfigureAwait(false);
                using var doc = System.Text.Json.JsonDocument.Parse(versionJson);
                if (doc.RootElement.TryGetProperty("version", out var vElem))
                {
                    ollamaVersion = vElem.GetString();
                    ollamaRunning = true;
                }
            }
            catch { }

            if (ollamaRunning)
            {
                try
                {
                    using var backend = new OllamaInferenceBackend();
                    discoveredOllamaModels = await backend.ListModelsAsync(cancellation.Token).ConfigureAwait(false);
                }
                catch { }
            }
        }

        try
        {
            LocalInferencePlan? installedPlan = null;
            try
            {
                installedPlan = await _modelSwitcher.LoadInstalledPlanAsync(cancellation.Token).ConfigureAwait(false);
            }
            catch { }

            HostHardwareInfo hardware = await Task.Run(_hardwareProbe.Probe, cancellation.Token).ConfigureAwait(false);
            string? activePref = IsTestEnvironment ? null : ChatModelPreferencesStore.Instance.ActiveSelectedModel;

            LocalInferenceEligibilityResult eligibility =
                LocalInferenceEligibility.EvaluateForConfiguredAvailability(hardware, installedModelId,
                    hasInstalledReceipt: !string.IsNullOrWhiteSpace(installedModelId), installedPlan);

            ModelSelection selection = BuildModelSelection(hardware, installedPlan, discoveredOllamaModels, activePref);

            if (ollamaRunning || discoveredOllamaModels.Count > 0)
            {
                _discoveredOllamaModels = discoveredOllamaModels;
                _ollamaRunning = ollamaRunning;
                _ollamaVersion = ollamaVersion;
                _ollamaPid = ollamaPid;
            }

            bool isOllamaCandidate = !IsTestEnvironment &&
                                     (IsOllamaModelIdentifier(activePref, discoveredOllamaModels) ||
                                      ollamaRunning ||
                                      discoveredOllamaModels.Count > 0);
            if (isOllamaCandidate && (ollamaRunning || discoveredOllamaModels.Count > 0))
            {
                ApplyAvailabilityResultOnUiThread(
                    cancellation,
                    isAvailabilityKnown: true,
                    isLocalAiAvailable: true,
                    hasAvailabilityProbeError: false,
                    unavailableReason: null,
                    recommendation: LocalInferenceProfileRecommendation.From(eligibility),
                    selection: selection,
                    ollamaModels: discoveredOllamaModels,
                    ollamaRunning: ollamaRunning,
                    ollamaVersion: ollamaVersion,
                    ollamaPid: ollamaPid);
                return;
            }

            if (eligibility.FailureCode == LocalInferenceEligibilityFailureCode.HardwareFactsIncomplete)
            {
                ApplyAvailabilityResultOnUiThread(
                    cancellation,
                    isAvailabilityKnown: false,
                    isLocalAiAvailable: false,
                    hasAvailabilityProbeError: true,
                    ProbeFailureReason,
                    recommendation: null,
                    selection: selection,
                    ollamaModels: discoveredOllamaModels,
                    ollamaRunning: ollamaRunning,
                    ollamaVersion: ollamaVersion,
                    ollamaPid: ollamaPid);
                return;
            }

            bool isAvailable = eligibility.CanInstall;
            LocalInferenceUnavailableReason? unavailableReason = isAvailable
                ? null
                : LocalInferenceEligibilityDiagnostics.GetUnavailableReason(eligibility);

            ApplyAvailabilityResultOnUiThread(
                cancellation,
                isAvailabilityKnown: true,
                isLocalAiAvailable: isAvailable,
                hasAvailabilityProbeError: false,
                unavailableReason,
                recommendation: LocalInferenceProfileRecommendation.From(eligibility),
                selection: selection,
                ollamaModels: discoveredOllamaModels,
                ollamaRunning: ollamaRunning,
                ollamaVersion: ollamaVersion,
                ollamaPid: ollamaPid);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            cancellation.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Warn($"Local AI availability probe failed: {ex}");
            string? activePref = IsTestEnvironment ? null : ChatModelPreferencesStore.Instance.ActiveSelectedModel;
            var fallbackSelection = BuildModelSelection(null, null, discoveredOllamaModels, activePref);
            ApplyAvailabilityResultOnUiThread(
                cancellation,
                isAvailabilityKnown: ollamaRunning,
                isLocalAiAvailable: ollamaRunning,
                hasAvailabilityProbeError: !ollamaRunning,
                ProbeFailureReason,
                recommendation: null,
                selection: fallbackSelection,
                ollamaModels: discoveredOllamaModels,
                ollamaRunning: ollamaRunning,
                ollamaVersion: ollamaVersion,
                ollamaPid: ollamaPid);
        }
    }

    private sealed record ModelSelection(
        HostHardwareInfo Hardware,
        LocalInferencePlan? InstalledPlan,
        IReadOnlyList<LocalAiModelOption> Options);

    /// <summary>The installed model first, then every catalog model this PC can be offered.</summary>
    private static ModelSelection BuildModelSelection(
        HostHardwareInfo? hardware,
        LocalInferencePlan? installedPlan,
        IReadOnlyList<LocalInferenceModel>? ollamaModels = null,
        string? currentActiveModel = null)
    {
        hardware ??= new HostHardwareInfo(
            System.Runtime.InteropServices.RuntimeInformation.OSArchitecture,
            16L * 1024 * 1024 * 1024,
            8L * 1024 * 1024 * 1024,
            [],
            VulkanAvailable: false);

        string? recommendedId = LocalInferenceEligibility.Evaluate(hardware) is { CanInstall: true, Plan: { } recommended }
            ? recommended.Model.Id
            : null;
        var options = new List<LocalAiModelOption>();

        if (ollamaModels is not null)
        {
            foreach (var m in ollamaModels)
            {
                bool isCurrent = string.Equals(m.Name, currentActiveModel, StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals($"ollama/{m.Name}", currentActiveModel, StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals($"{m.Name} (Ollama)", currentActiveModel, StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(m.Name, NormalizeOllamaModelName(currentActiveModel), StringComparison.OrdinalIgnoreCase);
                options.Add(new LocalAiModelOption(
                    m.Name,
                    $"{m.Name} (Ollama)",
                    m.Size ?? 0,
                    m.ContextWindow ?? 32768,
                    KvCachePrecision.Q8_0,
                    IsRecommended: false,
                    IsCurrent: isCurrent));
            }
        }

        if (installedPlan is not null && !options.Any(o => string.Equals(o.ModelId, installedPlan.Model.Id, StringComparison.OrdinalIgnoreCase)))
        {
            bool isCurrent = currentActiveModel is not null
                ? string.Equals(installedPlan.Model.Id, currentActiveModel, StringComparison.OrdinalIgnoreCase)
                : true;
            options.Add(ToOption(installedPlan, recommendedId, isCurrent));
        }

        foreach (LocalInferencePlan plan in LocalInferenceEligibility.OfferedPlans(id => LocalInferenceEligibility.Evaluate(hardware, id)))
        {
            if (!options.Any(o => string.Equals(o.ModelId, plan.Model.Id, StringComparison.OrdinalIgnoreCase)))
            {
                bool isCurrent = currentActiveModel is not null
                    ? string.Equals(plan.Model.Id, currentActiveModel, StringComparison.OrdinalIgnoreCase)
                    : false;
                options.Add(ToOption(plan, recommendedId, isCurrent));
            }
        }

        if (options.Count == 0)
        {
            foreach (var catalogModel in LocalModelCatalog.Models)
            {
                var profiles = LocalModelCatalog.GetProfiles(catalogModel);
                var defaultProfile = profiles.FirstOrDefault() ?? new LocalInferenceRunProfile("default", 4096, KvCachePrecision.Q8_0, KvCachePrecision.Q8_0, KvCachePrecision.Q8_0, KvCachePrecision.Q8_0, 1024 * 1024 * 512);
                options.Add(new LocalAiModelOption(
                    catalogModel.Id,
                    catalogModel.DisplayName,
                    LocalModelCatalog.TotalDownloadSizeBytes(catalogModel),
                    defaultProfile.ContextTokens,
                    defaultProfile.KeyCachePrecision,
                    string.Equals(catalogModel.Id, recommendedId, StringComparison.OrdinalIgnoreCase),
                    IsCurrent: string.Equals(catalogModel.Id, currentActiveModel, StringComparison.OrdinalIgnoreCase)));
            }
        }

        if (!string.IsNullOrWhiteSpace(currentActiveModel) && !options.Any(o => string.Equals(o.ModelId, currentActiveModel, StringComparison.OrdinalIgnoreCase)))
        {
            options.Insert(0, new LocalAiModelOption(
                currentActiveModel,
                currentActiveModel,
                0,
                32768,
                KvCachePrecision.Q8_0,
                IsRecommended: false,
                IsCurrent: true));
        }

        return new ModelSelection(hardware, installedPlan, options);
    }

    private static LocalAiModelOption ToOption(LocalInferencePlan plan, string? recommendedId, bool isCurrent) =>
        new(
            plan.Model.Id,
            plan.Model.DisplayName,
            LocalModelCatalog.TotalDownloadSizeBytes(plan.Model),
            plan.Profile.ContextTokens,
            plan.Profile.KeyCachePrecision,
            string.Equals(plan.Model.Id, recommendedId, StringComparison.OrdinalIgnoreCase),
            isCurrent);

    /// <summary>
    /// Dispatches a completed availability probe's result to the UI thread, guaranteeing
    /// <paramref name="cancellation"/> is disposed exactly once no matter which path runs: applied
    /// inline, applied from a genuinely deferred dispatcher callback, or never applied at all
    /// because the ViewModel is already disposed/inactive or the dispatcher refused the enqueue.
    /// A plain <see cref="ApplyOnUiThread"/> call would silently leak the token on those last two
    /// paths, since its own no-op guard runs before (or instead of) the wrapped action.
    /// </summary>
    private void ApplyAvailabilityResultOnUiThread(
        CancellationTokenSource cancellation,
        bool isAvailabilityKnown,
        bool isLocalAiAvailable,
        bool hasAvailabilityProbeError,
        LocalInferenceUnavailableReason? unavailableReason,
        LocalInferenceProfileRecommendation? recommendation = null,
        ModelSelection? selection = null,
        IReadOnlyList<LocalInferenceModel>? ollamaModels = null,
        bool ollamaRunning = false,
        string? ollamaVersion = null,
        int? ollamaPid = null)
    {
        if (_disposed || !IsActive)
        {
            ReleaseAbandonedAvailabilityProbe(cancellation);
            return;
        }

        if (_dispatcher.HasThreadAccess)
        {
            ApplyAvailabilityResult(
                cancellation, isAvailabilityKnown, isLocalAiAvailable, hasAvailabilityProbeError, unavailableReason, recommendation, selection, ollamaModels, ollamaRunning, ollamaVersion, ollamaPid);
            return;
        }

        bool enqueued = _dispatcher.TryEnqueue(() =>
        {
            if (_disposed || !IsActive)
            {
                cancellation.Dispose();
                return;
            }
            ApplyAvailabilityResult(
                cancellation, isAvailabilityKnown, isLocalAiAvailable, hasAvailabilityProbeError, unavailableReason, recommendation, selection, ollamaModels, ollamaRunning, ollamaVersion, ollamaPid);
        });
        if (!enqueued)
        {
            ReleaseAbandonedAvailabilityProbe(cancellation);
        }
    }

    /// <summary>
    /// Releases an availability-probe token from a worker thread when it will never reach
    /// <see cref="ApplyAvailabilityResult"/> (the ViewModel is disposed/inactive, or the
    /// dispatcher refused the enqueue). Interlocked.CompareExchange atomically claims the token:
    /// only the side that actually swaps it out for null may dispose it, so this can never
    /// dispose a token CancelAvailabilityRefresh() is concurrently (or has already) called
    /// Cancel() on. CancelAvailabilityRefresh() uses the matching Interlocked.Exchange to
    /// grab-and-clear the field atomically, so exactly one of the two atomic operations "wins"
    /// any given token; the loser observes the field already null and leaves its local copy of
    /// the token untouched. A rare undisposed CancellationTokenSource when the loser is this
    /// method is an acceptable trade-off for never risking ObjectDisposedException, since this
    /// class never registers a timeout that would give the token an unmanaged resource to leak.
    /// </summary>
    private void ReleaseAbandonedAvailabilityProbe(CancellationTokenSource cancellation)
    {
        if (Interlocked.CompareExchange(ref _availabilityCancellation, null, cancellation) == cancellation)
            cancellation.Dispose();
    }

    /// <summary>
    /// Applies a completed availability probe's result and releases its cancellation together,
    /// so the currency check and the state clear cannot race a real asynchronous DispatcherQueue
    /// callback. Always disposes <paramref name="cancellation"/>: a stale probe (superseded by a
    /// newer one) is no longer referenced by <see cref="_availabilityCancellation"/> by
    /// definition, so nothing else can still cancel or dispose this exact instance.
    /// </summary>
    private void ApplyAvailabilityResult(
        CancellationTokenSource cancellation,
        bool isAvailabilityKnown,
        bool isLocalAiAvailable,
        bool hasAvailabilityProbeError,
        LocalInferenceUnavailableReason? unavailableReason,
        LocalInferenceProfileRecommendation? recommendation = null,
        ModelSelection? selection = null,
        IReadOnlyList<LocalInferenceModel>? ollamaModels = null,
        bool ollamaRunning = false,
        string? ollamaVersion = null,
        int? ollamaPid = null)
    {
        if (!IsCurrentAvailabilityProbe(cancellation))
        {
            cancellation.Dispose();
            return;
        }
        _availabilityCancellation = null;
        _isAvailabilityKnown = isAvailabilityKnown;
        _isLocalAiAvailable = isLocalAiAvailable;
        _hasAvailabilityProbeError = hasAvailabilityProbeError;
        _localAiUnavailableReason = unavailableReason;
        ProfileRecommendation = recommendation;
        if (ollamaModels is not null)
            _discoveredOllamaModels = ollamaModels;
        _ollamaRunning = ollamaRunning;
        _ollamaVersion = ollamaVersion;
        _ollamaPid = ollamaPid;

        if (selection is not null)
        {
            _hardware = selection.Hardware;
            _installedPlan = selection.InstalledPlan;
            _modelOptions = selection.Options;
            string? activePref = IsTestEnvironment ? null : ChatModelPreferencesStore.Instance.ActiveSelectedModel;
            if (!string.IsNullOrWhiteSpace(activePref))
            {
                _selectedModelOptionId = activePref;
            }
            else if (_selectedModelOptionId is null ||
                !_modelOptions.Any(option => string.Equals(option.ModelId, _selectedModelOptionId, StringComparison.OrdinalIgnoreCase)))
            {
                _selectedModelOptionId = _installedPlan?.Model.Id ?? _modelOptions.FirstOrDefault()?.ModelId;
            }
        }
        OnPropertyChanged(null);
        cancellation.Dispose();
    }

    private bool IsCurrentAvailabilityProbe(CancellationTokenSource cancellation) =>
        ReferenceEquals(_availabilityCancellation, cancellation);

    private async Task RefreshRuntimeSnapshotAsync(CancellationTokenSource cancellation)
    {
        try
        {
            LocalAiRuntimeSnapshot snapshot = await _runtime.RefreshAsync(cancellation.Token).ConfigureAwait(false);
            ApplyOnUiThread(() => ApplyRuntimeSnapshot(snapshot));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception ex)
        {
            ApplyOnUiThread(() =>
            {
                _actionError = ex.Message;
                OnPropertyChanged(null);
            });
        }
        finally
        {
            // Interlocked.CompareExchange pairs with CancelRuntimeRefresh()'s
            // Interlocked.Exchange: only the side that atomically wins clearing the field may
            // dispose this token, so a concurrent CancelRuntimeRefresh() on the UI thread can
            // never call Cancel() on an instance this worker thread already disposed. If
            // CancelRuntimeRefresh() already claimed the field first, this side leaves the
            // token undisposed rather than risk disposing it out from under an in-flight
            // Cancel() call; CancellationTokenSource has no unmanaged resource to leak unless a
            // timeout was registered, which this class never does.
            if (Interlocked.CompareExchange(ref _refreshCancellation, null, cancellation) == cancellation)
                cancellation.Dispose();
        }
    }

    private async Task<bool> RunRuntimeActionAsync(
        bool allowed,
        Func<CancellationToken, Task<LocalAiRuntimeSnapshot>> action)
    {
        ThrowIfDisposed();
        if (!allowed || _isBusy)
            return false;
        _isBusy = true;
        _actionError = null;
        OnPropertyChanged(null);
        try
        {
            ApplyRuntimeSnapshot(await action(CancellationToken.None));
            return _runtimeSnapshot.State is not (LocalAiRuntimeState.Conflict or LocalAiRuntimeState.Failed);
        }
        catch (Exception ex)
        {
            _actionError = ex.Message;
            OnPropertyChanged(null);
            return false;
        }
        finally
        {
            _isBusy = false;
            OnPropertyChanged(null);
        }
    }

    private void OnRuntimeStateChanged(object? sender, LocalAiRuntimeSnapshotChangedEventArgs e) =>
        ApplyOnUiThread(() => ApplyRuntimeSnapshot(e.Snapshot));
    private void OnSelectionChanged(object? sender, EventArgs e) =>
        ApplyOnUiThread(() => OnPropertyChanged(null));
    private void OnModelSwitchingChanged(object? sender, EventArgs e) => ApplyOnUiThread(() =>
    {
        // A switch that ended (ours or another page's) may have committed a new receipt;
        // reload the installed plan so options and "current" are not stale.
        if (!_modelSwitcher.IsSwitching && !_isSwitchingModel)
            StartAvailabilityRefresh();
        else
            OnPropertyChanged(null);
    });
    private void OnGatewayChanged(object? sender, PermissionsRuntimeSourceChangedEventArgs e) =>
        ApplyOnUiThread(() => ApplyGatewaySnapshot(e.Snapshot.ConnectionSnapshot));

    private void ApplyOnUiThread(Action action)
    {
        if (_disposed || !IsActive)
            return;
        if (_dispatcher.HasThreadAccess)
            action();
        else
            _dispatcher.TryEnqueue(() => { if (!_disposed && IsActive) action(); });
    }

    private void ApplyRuntimeSnapshot(LocalAiRuntimeSnapshot snapshot)
    {
        string? previousModelId = _runtimeSnapshot.ModelId;
        _runtimeSnapshot = snapshot;
        OnPropertyChanged(null);
        // Availability reads the managed-install receipt (see RefreshAvailabilityAsync), which the
        // runtime refresh may only publish after that read has already happened. Recomputing when
        // the model id changes is what keeps a first visit correct: otherwise a 32 GB Spark whose
        // receipt arrives late stays pinned at NotRecommendedForSku, with Retry Setup and Change
        // Model disabled and Recheck unavailable, until the page is left and reopened.
        if (IsActive && !string.Equals(previousModelId, snapshot.ModelId, StringComparison.Ordinal))
            StartAvailabilityRefresh();
    }
    private void ApplyGatewaySnapshot(GatewayConnectionSnapshot snapshot)
    {
        _gatewaySnapshot = snapshot;
        OnPropertyChanged(null);
    }

    private static string FormatContext(int tokens) =>
        tokens % 1024 == 0
            ? $"{tokens / 1024}K"
            : tokens % 1000 == 0
                ? $"{tokens / 1000}K"
                : $"{tokens:N0} tokens";

    private static string FormatKvCache(LocalAiRuntimeSnapshot snapshot)
    {
        if (snapshot.KeyCachePrecision is not { } targetPrecision ||
            snapshot.DraftKeyCachePrecision is not { } draftPrecision)
        {
            return "Unknown";
        }

        string target = LocalModelCatalog.ToDisplayCacheType(targetPrecision);
        string draft = LocalModelCatalog.ToDisplayCacheType(draftPrecision);
        return target == draft ? $"{target} target + MTP draft" : $"{target} target + {draft} MTP draft";
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        Deactivate();
        _disposed = true;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
