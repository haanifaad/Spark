// <summary>
// Settings page view for selecting the Gateway default model and completing provider sign-in flows. Its view model
// owns model choices, validation and Gateway writes; this code-behind only renders state and forwards user input.
// It unsubscribes when unloaded so a retained view model cannot update a detached page.
// Usage:
//   frame.Navigate(typeof(DefaultModelPage));
//   // NavigationScopeManager supplies and activates DefaultModelPageViewModel as the page DataContext.
// </summary>
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using OpenClaw.SetupEngine;
using OpenClaw.SetupEngine.UI.Controls;
using OpenClaw.Shared;
using OpenClaw.Shared.VoiceMode;
using OpenClawTray.Helpers;
using OpenClawTray.Presentation;
using OpenClawTray.Services;

namespace OpenClawTray.Pages;

/// <summary>
/// Renders <see cref="DefaultModelPageViewModel"/> state and forwards model and provider actions from the Settings UI.
/// </summary>
public sealed partial class DefaultModelPage : Page
{
    private const double CompactActionsWidth = 720;
    private static App CurrentApp => (App)Microsoft.UI.Xaml.Application.Current!;
    private readonly IVoiceModeConfigurationService _voiceConfigService;
    private VoiceModeModelsConfig? _currentVoiceConfig;
    private bool _voiceConfigLoaded;
    private readonly Dictionary<VoiceCapabilityKind, bool> _keyShowing = new()
    {
        [VoiceCapabilityKind.Hearing] = false,
        [VoiceCapabilityKind.Thinking] = false,
        [VoiceCapabilityKind.Speaking] = false,
        [VoiceCapabilityKind.Background] = false,
        [VoiceCapabilityKind.Coding] = false,
    };

    private DefaultModelPageViewModel? _viewModel;
    private IReadOnlyList<DefaultModelOption>? _renderedOptions;
    private bool _suppressSelection;
    // Hosts Gateway sign-in steps; the view model owns the session.
    private readonly ProviderSetupDialog _providerDialog = new();
    private IReadOnlyList<ModelProviderCatalogEntry>? _renderedCatalog;
    private IReadOnlyList<ModelProviderLoginOption>? _renderedLoginOptions;
    private bool _suppressProviderSelection;
    private bool _isPageLoaded;
    private Task? _providerDialogTask;

    /// <summary>Initializes the page and attaches its view-owned model-provider dialog callbacks.</summary>
    public DefaultModelPage()
    {
        InitializeComponent();
        _voiceConfigService = CurrentApp.Services?.GetService(typeof(IVoiceModeConfigurationService)) as IVoiceModeConfigurationService
            ?? new VoiceModeConfigurationService();
        SizeChanged += OnLayoutSizeChanged;
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _providerDialog.ContinueRequested += OnProviderDialogContinue;
        _providerDialog.RefreshRequested += OnProviderDialogRefresh;
        _providerDialog.CancelRequested += OnProviderDialogCancel;
        _providerDialog.ExternalLinkRequested += OnProviderDialogExternalLink;
    }

    private void OnLayoutSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var orientation = e.NewSize.Width < CompactActionsWidth ? Orientation.Vertical : Orientation.Horizontal;
        ModelActions.Orientation = orientation;
        ProviderApiKeyPanel.Orientation = orientation;
        ProviderSignInPanel.Orientation = orientation;
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = args.NewValue as DefaultModelPageViewModel;
        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        RefreshFromViewModel();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isPageLoaded = true;
        // The view model is attached during navigation, before the ComboBox template applies,
        // and WinUI does not render a selection made then. Re-select once loaded.
        _suppressSelection = true;
        DefaultModelSelector.SelectedItem = null;
        _suppressSelection = false;
        RefreshFromViewModel();
        _ = LoadVoiceModeConfigAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isPageLoaded = false;
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _providerDialogTask = null;
        _providerDialog.Dismiss();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        RefreshFromViewModel();

    private void RefreshFromViewModel()
    {
        if (_viewModel is not { } vm)
            return;
        DisconnectedInfoBar.IsOpen = !vm.IsConnected;
        ReadOnlyInfoBar.IsOpen = vm.IsConnected && !vm.CanWrite;

        if (!ReferenceEquals(_renderedOptions, vm.Options))
        {
            _renderedOptions = vm.Options;
            _suppressSelection = true;
            DefaultModelSelector.Items.Clear();
            foreach (DefaultModelOption option in vm.Options)
            {
                // A provider that still needs sign-in cannot become the default; the current
                // primary stays selectable so the combo can show it.
                DefaultModelSelector.Items.Add(new ComboBoxItem
                {
                    Tag = option.Id,
                    Content = Format(option),
                    IsEnabled = !option.RequiresAuth || option.Id == vm.CurrentOptionId,
                });
            }
            _suppressSelection = false;
        }
        _suppressSelection = true;
        DefaultModelSelector.SelectedItem = DefaultModelSelector.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag as string, vm.SelectedOptionId, StringComparison.Ordinal));
        _suppressSelection = false;
        DefaultModelSelector.IsEnabled = vm.IsConnected && vm.CanWrite && !vm.IsApplying;

        if (vm.Error is { } error)
        {
            DefaultModelStatusText.Text = error;
            DefaultModelStatusText.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
            DefaultModelStatusText.Visibility = Visibility.Visible;
        }
        else if (vm.StatusApplied)
        {
            DefaultModelStatusText.Text = LocalizationHelper.GetString("DefaultModelPage_Applied");
            DefaultModelStatusText.ClearValue(TextBlock.ForegroundProperty);
            DefaultModelStatusText.Visibility = Visibility.Visible;
        }
        else
        {
            DefaultModelStatusText.Visibility = Visibility.Collapsed;
        }

        ApplyDefaultModelButton.IsEnabled = vm.CanApply;
        DefaultModelBusyIndicator.IsActive = vm.IsApplying;
        DefaultModelBusyIndicator.Visibility = vm.IsApplying ? Visibility.Visible : Visibility.Collapsed;
        RefreshProvidersFromViewModel(vm);
    }

    private void RefreshProvidersFromViewModel(DefaultModelPageViewModel vm)
    {
        ModelProviderSetupViewModel p = vm.Providers;
        ModelProviderCapabilities caps = p.Capabilities;
        AddProviderCard.Visibility = vm.IsConnected && caps.CanUseGatewayProviders ? Visibility.Visible : Visibility.Collapsed;
        MoreProvidersCard.Visibility = vm.IsConnected && caps.CanAddCustom ? Visibility.Visible : Visibility.Collapsed;

        GatewayProviderPanel.Visibility = p.CatalogState == ModelProviderCatalogState.Ready
            ? Visibility.Visible : Visibility.Collapsed;

        string? catalogStatus = p.CatalogState switch
        {
            ModelProviderCatalogState.Loading => LocalizationHelper.GetString("DefaultModelPage_ProviderCatalogLoading"),
            ModelProviderCatalogState.Empty => LocalizationHelper.GetString("DefaultModelPage_ProviderCatalogEmpty"),
            ModelProviderCatalogState.Failed => LocalizationHelper.Format("DefaultModelPage_ProviderCatalogFailedFormat", p.CatalogErrorDetail ?? string.Empty),
            ModelProviderCatalogState.Unavailable => LocalizationHelper.GetString("DefaultModelPage_ProviderCatalogUnsupported"),
            _ => null,
        };
        ProviderCatalogStatusText.Text = catalogStatus ?? string.Empty;
        ProviderCatalogStatusText.Visibility = catalogStatus is null ? Visibility.Collapsed : Visibility.Visible;

        _suppressProviderSelection = true;
        if (!ReferenceEquals(_renderedCatalog, p.Catalog))
        {
            _renderedCatalog = p.Catalog;
            ProviderCatalogSelector.Items.Clear();
            foreach (ModelProviderCatalogEntry entry in p.Catalog)
            {
                ProviderCatalogSelector.Items.Add(new ComboBoxItem
                {
                    Tag = entry.Provider,
                    Content = entry.IsConfigured
                        ? LocalizationHelper.Format("DefaultModelPage_ProviderConnectedFormat", entry.DisplayName)
                        : entry.DisplayName,
                });
            }
        }
        ProviderCatalogSelector.SelectedItem = FindItem(ProviderCatalogSelector, p.SelectedProviderId);
        if (!ReferenceEquals(_renderedLoginOptions, p.LoginOptions))
        {
            _renderedLoginOptions = p.LoginOptions;
            ProviderLoginSelector.Items.Clear();
            foreach (ModelProviderLoginOption option in p.LoginOptions)
                ProviderLoginSelector.Items.Add(new ComboBoxItem { Tag = option.Id, Content = option.Label });
        }
        ProviderLoginSelector.SelectedItem = FindItem(ProviderLoginSelector, p.SelectedLoginOptionId);

        // Populate EcosystemProviderSelector presets if not already loaded
        if (EcosystemProviderSelector.Items.Count == 0)
        {
            foreach (var preset in ExtendedModelProviderCatalog.Presets)
            {
                EcosystemProviderSelector.Items.Add(new ComboBoxItem
                {
                    Tag = preset.Id,
                    Content = preset.DisplayName,
                });
            }
            if (EcosystemProviderSelector.Items.Count > 0)
            {
                EcosystemProviderSelector.SelectedIndex = 0;
            }
        }

        _suppressProviderSelection = false;

        ProviderApiKeyPanel.Visibility = caps.CanSetApiKey && p.SelectedProvider?.ApiKeySupported == true
            ? Visibility.Visible : Visibility.Collapsed;
        SaveProviderApiKeyButton.IsEnabled = p.CanSaveApiKey;
        ProviderSignInPanel.Visibility = p.LoginOptions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        StartProviderSignInButton.IsEnabled = p.CanStartLogin;
        AddEcosystemProviderButton.IsEnabled = caps.CanAddCustom;
        AddCustomProviderButton.IsEnabled = caps.CanAddCustom;

        (string? status, bool isError) = FormatProviderStatus(p);
        ProviderStatusText.Text = status ?? string.Empty;
        ProviderStatusText.Visibility = status is null ? Visibility.Collapsed : Visibility.Visible;
        if (isError)
            ProviderStatusText.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
        else
            ProviderStatusText.ClearValue(TextBlock.ForegroundProperty);

        RefreshProviderDialog(p);
    }
    private void RefreshProviderDialog(ModelProviderSetupViewModel p)
    {
        if (!p.LoginActive)
        {
            if (_providerDialogTask is not null)
            {
                _providerDialogTask = null;
                _providerDialog.Dismiss();
            }
            return;
        }
        string? error = p.LoginError switch
        {
            ModelProviderLoginError.Request => p.LoginErrorDetail,
            ModelProviderLoginError.TimedOut => LocalizationHelper.GetString("DefaultModelPage_ProviderSignInTimedOut"),
            ModelProviderLoginError.UnsupportedStep => LocalizationHelper.GetString("DefaultModelPage_ProviderSignInUnsupported"),
            ModelProviderLoginError.CancelUnconfirmed => LocalizationHelper.GetString("DefaultModelPage_ProviderSignInCancelUnconfirmed"),
            _ => null,
        };
        string status = p.LoginBusy ? LocalizationHelper.GetString("DefaultModelPage_ProviderSignInWaiting") : string.Empty;
        _providerDialog.Update(p.LoginStep, p.LoginPhase, p.LoginBusy, canCancel: true, p.LoginCancelling,
            status, error, p.LoginTitle);
        if (XamlRoot is null)
            return;
        Task showing = _providerDialog.ShowOwnedAsync(XamlRoot, ActualTheme);
        if (!ReferenceEquals(showing, _providerDialogTask))
        {
            _providerDialogTask = showing;
            _ = ObserveProviderDialogAsync(showing, p);
        }
    }

    private static async Task ObserveProviderDialogAsync(Task showing, ModelProviderSetupViewModel p)
    {
        try
        {
            await showing;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            // Another dialog already owns this window; abandon the sign-in instead of hiding it.
            Logger.Warn($"Provider sign-in dialog could not open: {ex.Message}");
            AsyncEventHandlerGuard.Run(p.CancelLoginAsync, new AppLogger(), nameof(ObserveProviderDialogAsync));
        }
    }

    private static (string? Text, bool IsError) FormatProviderStatus(ModelProviderSetupViewModel p)
    {
        string subject = p.StatusSubject ?? string.Empty;
        return p.Status switch
        {
            ModelProviderStatus.ApiKeySaved => (Append(LocalizationHelper.Format("DefaultModelPage_ProviderApiKeySaved", subject), p.StatusDetail), false),
            ModelProviderStatus.CustomProviderAdded => (LocalizationHelper.Format("DefaultModelPage_CustomProviderAdded", subject), false),
            ModelProviderStatus.SignedIn => (LocalizationHelper.Format("DefaultModelPage_ProviderSignedIn", subject), false),
            ModelProviderStatus.SignedInRestartRequired => (LocalizationHelper.Format("DefaultModelPage_ProviderSignedInRestart", subject), false),
            ModelProviderStatus.SignInCancelled => (LocalizationHelper.GetString("DefaultModelPage_ProviderSignInCancelled"), false),
            ModelProviderStatus.SignInFailed => (p.StatusDetail is { } detail
                ? LocalizationHelper.Format("DefaultModelPage_ProviderRequestFailedFormat", detail)
                : LocalizationHelper.GetString("DefaultModelPage_ProviderSignInFailed"), true),
            ModelProviderStatus.SignInInterrupted => (LocalizationHelper.GetString("DefaultModelPage_ProviderSignInInterrupted"), true),
            ModelProviderStatus.RequestFailed => (LocalizationHelper.Format("DefaultModelPage_ProviderRequestFailedFormat", p.StatusDetail ?? string.Empty), true),
            ModelProviderStatus.Invalid => (LocalizationHelper.GetString(p.ValidationError switch
            {
                CustomProviderValidationError.InvalidProviderId => "DefaultModelPage_CustomInvalidProviderId",
                CustomProviderValidationError.ProviderIdReserved => "DefaultModelPage_CustomProviderIdReserved",
                CustomProviderValidationError.ProviderIdInUse => "DefaultModelPage_CustomProviderIdInUse",
                CustomProviderValidationError.InvalidBaseUrl => "DefaultModelPage_CustomInvalidBaseUrl",
                CustomProviderValidationError.ApiKeyRequired => "DefaultModelPage_CustomApiKeyRequired",
                CustomProviderValidationError.MissingModels => "DefaultModelPage_CustomMissingModels",
                _ => "DefaultModelPage_CustomInvalidModelId",
            }), true),
            _ => (null, false),
        };

        static string Append(string text, string? detail) => detail is null ? text : $"{text} {detail}";
    }

    private static ComboBoxItem? FindItem(ComboBox combo, string? tag) =>
        combo.Items.OfType<ComboBoxItem>().FirstOrDefault(item => string.Equals(item.Tag as string, tag, StringComparison.Ordinal));

    private static string Format(DefaultModelOption option)
    {
        string label = option.IsLocalAi
            ? LocalizationHelper.Format("DefaultModelPage_LocalAiOptionFormat", option.DisplayName)
            : option.Provider is null ? option.DisplayName : $"{option.DisplayName} ({option.Provider})";
        return option.RequiresAuth ? LocalizationHelper.Format("DefaultModelPage_RequiresAuthFormat", label) : label;
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelection || _viewModel is null)
            return;
        if (DefaultModelSelector.SelectedItem is ComboBoxItem { Tag: string id })
        {
            _viewModel.SelectedOptionId = id;
            OpenClaw.Shared.Inference.ChatModelPreferencesStore.Instance.ActiveSelectedModel = id;
        }
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.SelectedOptionId is { } selId)
        {
            OpenClaw.Shared.Inference.ChatModelPreferencesStore.Instance.ActiveSelectedModel = selId;
        }
        AsyncEventHandlerGuard.Run(() => _viewModel?.ApplyAsync() ?? Task.FromResult(false), new AppLogger(), nameof(OnApply));
    }
    private void OnRefresh(object sender, RoutedEventArgs e) => _viewModel?.Refresh();
    private void OnOpenConnection(object sender, RoutedEventArgs e) => _viewModel?.OpenConnection();
    private void OnManageLocalAi(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.SelectedOptionId is { } selId)
        {
            OpenClaw.Shared.Inference.ChatModelPreferencesStore.Instance.ActiveSelectedModel = selId;
        }
        _viewModel?.OpenLocalAi();
    }

    private void OnMoreProviderKindChanged(object sender, SelectionChangedEventArgs e)
    {
        bool isCustom = MoreProviderKindSelector.SelectedIndex == 1;
        EcosystemProviderPanel.Visibility = isCustom ? Visibility.Collapsed : Visibility.Visible;
        CustomProviderPanel.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
        MoreProviderStatusText.Visibility = Visibility.Collapsed;
    }

    private void OnEcosystemProviderSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EcosystemProviderSelector.SelectedItem is ComboBoxItem { Tag: string presetId } &&
            ExtendedModelProviderCatalog.Find(presetId) is { } preset)
        {
            EcosystemProviderBaseUrlBox.Text = preset.DefaultBaseUrl;
            EcosystemProviderModelsBox.Text = preset.DefaultModels;
            EcosystemProviderApiSelector.SelectedItem = FindItem(EcosystemProviderApiSelector, preset.ApiType);
            MoreProviderStatusText.Visibility = Visibility.Collapsed;
        }
    }

    private void OnProviderCatalogSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_suppressProviderSelection && _viewModel is not null &&
            ProviderCatalogSelector.SelectedItem is ComboBoxItem { Tag: string provider })
            _viewModel.Providers.SelectedProviderId = provider;
    }

    private void OnProviderLoginSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_suppressProviderSelection && _viewModel is not null &&
            ProviderLoginSelector.SelectedItem is ComboBoxItem { Tag: string option })
            _viewModel.Providers.SelectedLoginOptionId = option;
    }

    private void OnSaveProviderApiKey(object sender, RoutedEventArgs e)
    {
        string apiKey = ProviderApiKeyBox.Password;
        ProviderApiKeyBox.Password = string.Empty;
        if (_viewModel is { } vm)
            AsyncEventHandlerGuard.Run(() => vm.Providers.SaveApiKeyAsync(apiKey), new AppLogger(), nameof(OnSaveProviderApiKey));
    }

    private void OnStartProviderSignIn(object sender, RoutedEventArgs e)
    {
        if (_viewModel is { } vm)
            AsyncEventHandlerGuard.Run(vm.Providers.StartLoginAsync, new AppLogger(), nameof(OnStartProviderSignIn));
    }

    private void OnAddEcosystemProvider(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not { } vm)
            return;
        string presetId = (EcosystemProviderSelector.SelectedItem as ComboBoxItem)?.Tag as string ?? string.Empty;
        string baseUrl = EcosystemProviderBaseUrlBox.Text;
        string api = (EcosystemProviderApiSelector.SelectedItem as ComboBoxItem)?.Tag as string ?? ModelProviderWire.OpenAiCompletionsApi;
        string apiKey = EcosystemProviderApiKeyBox.Password;
        string models = EcosystemProviderModelsBox.Text;
        AsyncEventHandlerGuard.Run(async () =>
        {
            bool added = await vm.Providers.AddCustomProviderAsync(presetId, baseUrl, api, apiKey, models);
            EcosystemProviderApiKeyBox.Password = string.Empty;
            if (added)
            {
                var preset = ExtendedModelProviderCatalog.Find(presetId);
                string name = preset?.DisplayName ?? presetId;
                ShowMoreProviderStatus($"Added provider '{name}' successfully! Its models are now available in the Default Model list above.", isError: false);
                vm.Refresh();
            }
            else
            {
                (string? status, _) = FormatProviderStatus(vm.Providers);
                ShowMoreProviderStatus(status ?? "Failed to add provider. Check configuration.", isError: true);
            }
        }, new AppLogger(), nameof(OnAddEcosystemProvider));
    }

    private void OnAddCustomProvider(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not { } vm)
            return;
        string providerId = CustomProviderIdBox.Text;
        string baseUrl = CustomProviderBaseUrlBox.Text;
        string api = (CustomProviderApiSelector.SelectedItem as ComboBoxItem)?.Tag as string ?? ModelProviderWire.OpenAiCompletionsApi;
        string apiKey = CustomProviderApiKeyBox.Password;
        string models = CustomProviderModelsBox.Text;
        AsyncEventHandlerGuard.Run(async () =>
        {
            bool added = await vm.Providers.AddCustomProviderAsync(providerId, baseUrl, api, apiKey, models);
            CustomProviderApiKeyBox.Password = string.Empty;
            if (added)
            {
                CustomProviderIdBox.Text = string.Empty;
                CustomProviderBaseUrlBox.Text = string.Empty;
                CustomProviderModelsBox.Text = string.Empty;
                ShowMoreProviderStatus($"Added custom provider '{providerId}' successfully! Its models are now available in the Default Model list above.", isError: false);
                vm.Refresh();
            }
            else
            {
                (string? status, _) = FormatProviderStatus(vm.Providers);
                ShowMoreProviderStatus(status ?? "Failed to add custom provider. Check configuration.", isError: true);
            }
        }, new AppLogger(), nameof(OnAddCustomProvider));
    }

    private void ShowMoreProviderStatus(string message, bool isError)
    {
        MoreProviderStatusText.Text = message;
        MoreProviderStatusText.Visibility = Visibility.Visible;
        if (isError)
            MoreProviderStatusText.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
        else
            MoreProviderStatusText.Foreground = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
    }
    private void OnProviderDialogContinue()
    {
        if (_viewModel is not { } vm || vm.Providers.LoginStep is not { } step || !_providerDialog.CanSubmit)
            return;
        JsonElement? answer = _providerDialog.TakeAnswer();
        AsyncEventHandlerGuard.Run(() => vm.Providers.SubmitLoginAnswerAsync(step.Id, answer), new AppLogger(), nameof(OnProviderDialogContinue));
    }

    private void OnProviderDialogRefresh()
    {
        if (_viewModel is { } vm)
            AsyncEventHandlerGuard.Run(vm.Providers.RefreshLoginAsync, new AppLogger(), nameof(OnProviderDialogRefresh));
    }

    private void OnProviderDialogCancel()
    {
        if (_viewModel is { } vm)
            AsyncEventHandlerGuard.Run(vm.Providers.CancelLoginAsync, new AppLogger(), nameof(OnProviderDialogCancel));
    }

    private void OnProviderDialogExternalLink(string? url) =>
        AsyncEventHandlerGuard.Run(() => OpenExternalLinkAsync(url), new AppLogger(), nameof(OnProviderDialogExternalLink));

    private static async Task OpenExternalLinkAsync(string? url)
    {
        if (!GatewayAiSetupPresentation.TryGetExternalUri(url, out Uri uri))
        {
            Logger.Warn("Provider sign-in link was not a valid http(s) URL.");
            return;
        }
        if (!await global::Windows.System.Launcher.LaunchUriAsync(uri))
            Logger.Warn("Provider sign-in link could not be opened.");
    }

    #region Voice Mode Capability Configuration Compartment

    private void OnGeneralModelsTabClicked(object sender, RoutedEventArgs e)
    {
        GeneralModelsContainer.Visibility = Visibility.Visible;
        VoiceModeModelsContainer.Visibility = Visibility.Collapsed;
        GeneralModelsTabButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        VoiceModeModelsTabButton.Style = (Style)Application.Current.Resources["DefaultButtonStyle"];
    }

    private void OnVoiceModeModelsTabClicked(object sender, RoutedEventArgs e)
    {
        GeneralModelsContainer.Visibility = Visibility.Collapsed;
        VoiceModeModelsContainer.Visibility = Visibility.Visible;
        VoiceModeModelsTabButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        GeneralModelsTabButton.Style = (Style)Application.Current.Resources["DefaultButtonStyle"];

        if (!_voiceConfigLoaded)
        {
            _ = LoadVoiceModeConfigAsync();
        }
    }

    private async Task LoadVoiceModeConfigAsync()
    {
        try
        {
            _currentVoiceConfig = await _voiceConfigService.LoadConfigurationAsync();
            _voiceConfigLoaded = true;
            await PopulateVoiceConfigUiAsync(_currentVoiceConfig);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to load Voice Mode models config: {ex.Message}");
        }
    }

    private async Task PopulateVoiceConfigUiAsync(VoiceModeModelsConfig config)
    {
        // 1. Hearing
        HearingEnabledToggle.IsOn = config.Hearing.IsEnabled;
        SelectItemByTagOrContent(HearingProviderCombo, config.Hearing.Provider);
        UpdateHearingModelsForProvider(config.Hearing.Provider, config.Hearing.Model);
        SelectItemByTagOrContent(HearingLanguageCombo, config.Hearing.Language);
        HearingEndpointBox.Text = config.Hearing.Endpoint ?? string.Empty;
        var rawHearing = await _voiceConfigService.Credentials.GetApiKeyAsync(VoiceCapabilityKind.Hearing);
        var hearingKey = _voiceConfigService.Credentials.MaskKey(rawHearing);
        HearingApiKeyBox.Password = hearingKey;
        HearingApiKeyTextBox.Text = hearingKey;

        // 2. Thinking
        ThinkingEnabledToggle.IsOn = config.Thinking.IsEnabled;
        SelectItemByTagOrContent(ThinkingProviderCombo, config.Thinking.Provider);
        UpdateThinkingModelsForProvider(config.Thinking.Provider, config.Thinking.Model);
        ThinkingTempSlider.Value = config.Thinking.Temperature;
        ThinkingTempLabel.Text = config.Thinking.Temperature.ToString("0.00");
        ThinkingEndpointBox.Text = config.Thinking.Endpoint ?? string.Empty;
        var rawThinking = await _voiceConfigService.Credentials.GetApiKeyAsync(VoiceCapabilityKind.Thinking);
        var thinkingKey = _voiceConfigService.Credentials.MaskKey(rawThinking);
        ThinkingApiKeyBox.Password = thinkingKey;
        ThinkingApiKeyTextBox.Text = thinkingKey;

        // 3. Speaking
        SpeakingEnabledToggle.IsOn = config.Speaking.IsEnabled;
        SelectItemByTagOrContent(SpeakingProviderCombo, config.Speaking.Provider);
        UpdateSpeakingVoicesForProvider(config.Speaking.Provider, config.Speaking.Voice ?? config.Speaking.Model);
        SpeakingSpeedSlider.Value = config.Speaking.Speed;
        SpeakingSpeedLabel.Text = $"{config.Speaking.Speed:0.00}x";
        SelectItemByTagOrContent(SpeakingLanguageCombo, config.Speaking.Language);
        var rawSpeaking = await _voiceConfigService.Credentials.GetApiKeyAsync(VoiceCapabilityKind.Speaking);
        var speakingKey = _voiceConfigService.Credentials.MaskKey(rawSpeaking);
        SpeakingApiKeyBox.Password = speakingKey;
        SpeakingApiKeyTextBox.Text = speakingKey;

        // 4. Background
        BackgroundEnabledToggle.IsOn = config.Background.IsEnabled;
        SelectItemByTagOrContent(BackgroundProviderCombo, config.Background.Provider);
        UpdateBackgroundModelsForProvider(config.Background.Provider, config.Background.Model);
        BackgroundTimeoutSlider.Value = config.Background.TimeoutSeconds;
        BackgroundTimeoutLabel.Text = $"{config.Background.TimeoutSeconds}s";
        SelectItemByTagOrContent(BackgroundTokensCombo, config.Background.MaxOutputTokens.ToString());
        var rawBg = await _voiceConfigService.Credentials.GetApiKeyAsync(VoiceCapabilityKind.Background);
        var bgKey = _voiceConfigService.Credentials.MaskKey(rawBg);
        BackgroundApiKeyBox.Password = bgKey;
        BackgroundApiKeyTextBox.Text = bgKey;

        // 5. Coding
        CodingEnabledToggle.IsOn = config.Coding.IsEnabled;
        SelectItemByTagOrContent(CodingProviderCombo, config.Coding.Provider);
        UpdateCodingModelsForProvider(config.Coding.Provider, config.Coding.Model);
        CodingTempSlider.Value = config.Coding.Temperature;
        CodingTempLabel.Text = config.Coding.Temperature.ToString("0.00");
        CodingEndpointBox.Text = config.Coding.Endpoint ?? string.Empty;
        var rawCoding = await _voiceConfigService.Credentials.GetApiKeyAsync(VoiceCapabilityKind.Coding);
        var codingKey = _voiceConfigService.Credentials.MaskKey(rawCoding);
        CodingApiKeyBox.Password = codingKey;
        CodingApiKeyTextBox.Text = codingKey;
    }

    private void OnHearingProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isPageLoaded || HearingProviderCombo == null || HearingModelCombo == null) return;
        var provider = GetSelectedTagOrContent(HearingProviderCombo!);
        UpdateHearingModelsForProvider(provider);
    }

    private void UpdateHearingModelsForProvider(string? provider, string? selectModel = null)
    {
        if (!_isPageLoaded || HearingModelCombo == null) return;
        var models = (provider?.ToLowerInvariant()) switch
        {
            "google" => new[] { "gemini-2.5-flash-native-audio-preview-12-2025", "gemini-2.5-flash" },
            "openai" => new[] { "whisper-1" },
            "groq" => new[] { "whisper-large-v3", "whisper-large-v3-turbo" },
            "local" => new[] { "faster-whisper-medium", "whisper-base-ml (Malayalam)", "faster-whisper-tiny" },
            _ => new[] { "gemini-2.5-flash-native-audio-preview-12-2025", "whisper-1", "custom-stt-model" }
        };
        SetComboOptions(HearingModelCombo, models, selectModel);
    }

    private void OnThinkingProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isPageLoaded || ThinkingProviderCombo == null || ThinkingModelCombo == null) return;
        var provider = GetSelectedTagOrContent(ThinkingProviderCombo!);
        UpdateThinkingModelsForProvider(provider);
    }

    private void UpdateThinkingModelsForProvider(string? provider, string? selectModel = null)
    {
        if (!_isPageLoaded || ThinkingModelCombo == null) return;
        var models = (provider?.ToLowerInvariant()) switch
        {
            "google" => new[] { "gemini-2.5-flash-native-audio-preview-12-2025", "gemini-2.5-pro", "gemini-2.5-flash" },
            "anthropic" => new[] { "claude-3-5-sonnet-20241022", "claude-3-5-haiku-20241022" },
            "openai" => new[] { "gpt-4o", "gpt-4o-mini", "o1-preview" },
            "deepseek" => new[] { "deepseek-r1", "deepseek-v3" },
            "ollama" => new[] { "llama3.3", "qwen2.5", "mistral" },
            _ => new[] { "gemini-2.5-flash-native-audio-preview-12-2025", "gpt-4o", "custom-model" }
        };
        SetComboOptions(ThinkingModelCombo, models, selectModel);
    }

    private void OnSpeakingProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isPageLoaded || SpeakingProviderCombo == null || SpeakingVoiceCombo == null) return;
        var provider = GetSelectedTagOrContent(SpeakingProviderCombo!);
        UpdateSpeakingVoicesForProvider(provider);
    }

    private void UpdateSpeakingVoicesForProvider(string? provider, string? selectVoice = null)
    {
        if (!_isPageLoaded || SpeakingVoiceCombo == null) return;
        var voices = (provider?.ToLowerInvariant()) switch
        {
            "edgetts" => new[] { "en-US-GuyNeural (Male - English US)", "en-US-JennyNeural (Female - English US)", "ml-IN-SobhanaNeural (Female - Malayalam)", "ml-IN-MidhunNeural (Male - Malayalam)" },
            "elevenlabs" => new[] { "Charon (Male - Deep)", "Rachel (Female - Calm)", "Adam (Male - Conversational)", "Bella (Female - Expressive)" },
            "openai" => new[] { "alloy", "echo", "fable", "onyx", "nova", "shimmer" },
            "kokoro" => new[] { "af_bella", "am_adam", "bf_emma", "bm_george" },
            "system" => new[] { "Windows Default Voice" },
            _ => new[] { "en-US-GuyNeural", "ml-IN-SobhanaNeural", "custom-voice" }
        };
        SetComboOptions(SpeakingVoiceCombo, voices, selectVoice);
    }

    private void OnBackgroundProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isPageLoaded || BackgroundProviderCombo == null || BackgroundModelCombo == null) return;
        var provider = GetSelectedTagOrContent(BackgroundProviderCombo!);
        UpdateBackgroundModelsForProvider(provider);
    }

    private void UpdateBackgroundModelsForProvider(string? provider, string? selectModel = null)
    {
        if (!_isPageLoaded || BackgroundModelCombo == null) return;
        var models = (provider?.ToLowerInvariant()) switch
        {
            "google" => new[] { "gemini-2.5-flash", "gemini-2.5-pro" },
            "openai" => new[] { "gpt-4o-mini", "gpt-4o" },
            "anthropic" => new[] { "claude-3-5-haiku-20241022", "claude-3-5-sonnet-20241022" },
            "groq" => new[] { "llama-3.3-70b-versatile", "mixtral-8x7b-32768" },
            "ollama" => new[] { "mistral", "llama3.2", "phi3" },
            _ => new[] { "gemini-2.5-flash", "gpt-4o-mini", "custom-background-model" }
        };
        SetComboOptions(BackgroundModelCombo, models, selectModel);
    }

    private void OnCodingProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isPageLoaded || CodingProviderCombo == null || CodingModelCombo == null) return;
        var provider = GetSelectedTagOrContent(CodingProviderCombo!);
        UpdateCodingModelsForProvider(provider);
    }

    private void UpdateCodingModelsForProvider(string? provider, string? selectModel = null)
    {
        if (!_isPageLoaded || CodingModelCombo == null) return;
        var models = (provider?.ToLowerInvariant()) switch
        {
            "anthropic" => new[] { "claude-3-5-sonnet-20241022", "claude-3-5-haiku-20241022" },
            "deepseek" => new[] { "deepseek-coder-v2", "deepseek-r1" },
            "openai" => new[] { "gpt-4o", "gpt-4o-mini", "o1-mini" },
            "google" => new[] { "gemini-2.5-pro", "gemini-2.5-flash" },
            "ollama" => new[] { "qwen2.5-coder-32b", "deepseek-coder", "codellama" },
            _ => new[] { "claude-3-5-sonnet-20241022", "deepseek-coder-v2", "custom-coding-model" }
        };
        SetComboOptions(CodingModelCombo, models, selectModel);
    }

    private void OnThinkingTempChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (ThinkingTempLabel is not null)
            ThinkingTempLabel.Text = e.NewValue.ToString("0.00");
    }

    private void OnSpeakingSpeedChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (SpeakingSpeedLabel is not null)
            SpeakingSpeedLabel.Text = $"{e.NewValue:0.00}x";
    }

    private void OnBackgroundTimeoutChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (BackgroundTimeoutLabel is not null)
            BackgroundTimeoutLabel.Text = $"{(int)e.NewValue}s";
    }

    private void OnCodingTempChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (CodingTempLabel is not null)
            CodingTempLabel.Text = e.NewValue.ToString("0.00");
    }

    private void OnToggleHearingKeyVisibility(object sender, RoutedEventArgs e) =>
        ToggleKeyVisibility(VoiceCapabilityKind.Hearing, HearingApiKeyBox, HearingApiKeyTextBox, HearingShowKeyButton);

    private void OnClearHearingKeyClicked(object sender, RoutedEventArgs e) =>
        ClearKey(VoiceCapabilityKind.Hearing, HearingApiKeyBox, HearingApiKeyTextBox);

    private void OnToggleThinkingKeyVisibility(object sender, RoutedEventArgs e) =>
        ToggleKeyVisibility(VoiceCapabilityKind.Thinking, ThinkingApiKeyBox, ThinkingApiKeyTextBox, ThinkingShowKeyButton);

    private void OnClearThinkingKeyClicked(object sender, RoutedEventArgs e) =>
        ClearKey(VoiceCapabilityKind.Thinking, ThinkingApiKeyBox, ThinkingApiKeyTextBox);

    private void OnToggleSpeakingKeyVisibility(object sender, RoutedEventArgs e) =>
        ToggleKeyVisibility(VoiceCapabilityKind.Speaking, SpeakingApiKeyBox, SpeakingApiKeyTextBox, SpeakingShowKeyButton);

    private void OnClearSpeakingKeyClicked(object sender, RoutedEventArgs e) =>
        ClearKey(VoiceCapabilityKind.Speaking, SpeakingApiKeyBox, SpeakingApiKeyTextBox);

    private void OnToggleBackgroundKeyVisibility(object sender, RoutedEventArgs e) =>
        ToggleKeyVisibility(VoiceCapabilityKind.Background, BackgroundApiKeyBox, BackgroundApiKeyTextBox, BackgroundShowKeyButton);

    private void OnClearBackgroundKeyClicked(object sender, RoutedEventArgs e) =>
        ClearKey(VoiceCapabilityKind.Background, BackgroundApiKeyBox, BackgroundApiKeyTextBox);

    private void OnToggleCodingKeyVisibility(object sender, RoutedEventArgs e) =>
        ToggleKeyVisibility(VoiceCapabilityKind.Coding, CodingApiKeyBox, CodingApiKeyTextBox, CodingShowKeyButton);

    private void OnClearCodingKeyClicked(object sender, RoutedEventArgs e) =>
        ClearKey(VoiceCapabilityKind.Coding, CodingApiKeyBox, CodingApiKeyTextBox);

    private async void ToggleKeyVisibility(VoiceCapabilityKind kind, PasswordBox pwdBox, TextBox txtBox, Button btn)
    {
        bool isShowing = _keyShowing[kind];
        if (!isShowing)
        {
            var text = pwdBox.Password;
            if (string.IsNullOrWhiteSpace(text) || text.Contains('•'))
            {
                var realKey = await _voiceConfigService.Credentials.GetApiKeyAsync(kind);
                text = realKey ?? string.Empty;
            }
            txtBox.Text = text;
            pwdBox.Visibility = Visibility.Collapsed;
            txtBox.Visibility = Visibility.Visible;
            btn.Content = "🔒 Hide";
            _keyShowing[kind] = true;
        }
        else
        {
            pwdBox.Password = txtBox.Text;
            txtBox.Visibility = Visibility.Collapsed;
            pwdBox.Visibility = Visibility.Visible;
            btn.Content = "👁 Show";
            _keyShowing[kind] = false;
        }
    }

    private async void ClearKey(VoiceCapabilityKind kind, PasswordBox pwdBox, TextBox txtBox)
    {
        pwdBox.Password = string.Empty;
        txtBox.Text = string.Empty;
        await _voiceConfigService.Credentials.ClearApiKeyAsync(kind);
        ShowStatusMessage($"{kind} API key cleared.", InfoBarSeverity.Informational);
    }

    private void OnTestHearingClicked(object sender, RoutedEventArgs e) =>
        RunCapabilityTest(
            VoiceCapabilityKind.Hearing,
            HearingTestButton,
            HearingTestRing,
            HearingTestStatus,
            () => new VoiceModelCapabilityConfig
            {
                Capability = VoiceCapabilityKind.Hearing,
                IsEnabled = HearingEnabledToggle.IsOn,
                Provider = GetSelectedTagOrContent(HearingProviderCombo) ?? "Google",
                Model = GetSelectedTagOrContent(HearingModelCombo) ?? "gemini-2.5-flash-native-audio-preview-12-2025",
                Language = GetSelectedTagOrContent(HearingLanguageCombo) ?? "auto",
                Endpoint = string.IsNullOrWhiteSpace(HearingEndpointBox.Text) ? null : HearingEndpointBox.Text.Trim()
            },
            GetActiveKeyForCapability(VoiceCapabilityKind.Hearing, HearingApiKeyBox, HearingApiKeyTextBox)
        );

    private void OnTestThinkingClicked(object sender, RoutedEventArgs e) =>
        RunCapabilityTest(
            VoiceCapabilityKind.Thinking,
            ThinkingTestButton,
            ThinkingTestRing,
            ThinkingTestStatus,
            () => new VoiceModelCapabilityConfig
            {
                Capability = VoiceCapabilityKind.Thinking,
                IsEnabled = ThinkingEnabledToggle.IsOn,
                Provider = GetSelectedTagOrContent(ThinkingProviderCombo) ?? "Google",
                Model = GetSelectedTagOrContent(ThinkingModelCombo) ?? "gemini-2.5-flash-native-audio-preview-12-2025",
                Temperature = ThinkingTempSlider.Value,
                Endpoint = string.IsNullOrWhiteSpace(ThinkingEndpointBox.Text) ? null : ThinkingEndpointBox.Text.Trim()
            },
            GetActiveKeyForCapability(VoiceCapabilityKind.Thinking, ThinkingApiKeyBox, ThinkingApiKeyTextBox)
        );

    private void OnTestSpeakingClicked(object sender, RoutedEventArgs e) =>
        RunCapabilityTest(
            VoiceCapabilityKind.Speaking,
            SpeakingTestButton,
            SpeakingTestRing,
            SpeakingTestStatus,
            () => new VoiceModelCapabilityConfig
            {
                Capability = VoiceCapabilityKind.Speaking,
                IsEnabled = SpeakingEnabledToggle.IsOn,
                Provider = GetSelectedTagOrContent(SpeakingProviderCombo) ?? "EdgeTTS",
                Model = GetSelectedTagOrContent(SpeakingVoiceCombo) ?? "en-US-GuyNeural",
                Voice = GetSelectedTagOrContent(SpeakingVoiceCombo) ?? "en-US-GuyNeural",
                Speed = SpeakingSpeedSlider.Value,
                Language = GetSelectedTagOrContent(SpeakingLanguageCombo) ?? "en"
            },
            GetActiveKeyForCapability(VoiceCapabilityKind.Speaking, SpeakingApiKeyBox, SpeakingApiKeyTextBox)
        );

    private void OnTestBackgroundClicked(object sender, RoutedEventArgs e) =>
        RunCapabilityTest(
            VoiceCapabilityKind.Background,
            BackgroundTestButton,
            BackgroundTestRing,
            BackgroundTestStatus,
            () => new VoiceModelCapabilityConfig
            {
                Capability = VoiceCapabilityKind.Background,
                IsEnabled = BackgroundEnabledToggle.IsOn,
                Provider = GetSelectedTagOrContent(BackgroundProviderCombo) ?? "Google",
                Model = GetSelectedTagOrContent(BackgroundModelCombo) ?? "gemini-2.5-flash",
                TimeoutSeconds = (int)BackgroundTimeoutSlider.Value,
                MaxOutputTokens = int.TryParse(GetSelectedTagOrContent(BackgroundTokensCombo), out var tok) ? tok : 4096
            },
            GetActiveKeyForCapability(VoiceCapabilityKind.Background, BackgroundApiKeyBox, BackgroundApiKeyTextBox)
        );

    private void OnTestCodingClicked(object sender, RoutedEventArgs e) =>
        RunCapabilityTest(
            VoiceCapabilityKind.Coding,
            CodingTestButton,
            CodingTestRing,
            CodingTestStatus,
            () => new VoiceModelCapabilityConfig
            {
                Capability = VoiceCapabilityKind.Coding,
                IsEnabled = CodingEnabledToggle.IsOn,
                Provider = GetSelectedTagOrContent(CodingProviderCombo) ?? "Anthropic",
                Model = GetSelectedTagOrContent(CodingModelCombo) ?? "claude-3-5-sonnet-20241022",
                Temperature = CodingTempSlider.Value,
                Endpoint = string.IsNullOrWhiteSpace(CodingEndpointBox.Text) ? null : CodingEndpointBox.Text.Trim()
            },
            GetActiveKeyForCapability(VoiceCapabilityKind.Coding, CodingApiKeyBox, CodingApiKeyTextBox)
        );

    private string? GetActiveKeyForCapability(VoiceCapabilityKind kind, PasswordBox pwdBox, TextBox txtBox)
    {
        if (_keyShowing[kind] && txtBox.Visibility == Visibility.Visible)
            return txtBox.Text;
        return pwdBox.Password;
    }

    private void RunCapabilityTest(
        VoiceCapabilityKind kind,
        Button btn,
        ProgressRing ring,
        TextBlock statusBlock,
        Func<VoiceModelCapabilityConfig> buildConfig,
        string? activeKey)
    {
        ring.Visibility = Visibility.Visible;
        ring.IsActive = true;
        btn.IsEnabled = false;
        statusBlock.Text = "Testing connection...";
        statusBlock.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];

        AsyncEventHandlerGuard.Run(async () =>
        {
            try
            {
                var cfg = buildConfig();
                var result = await _voiceConfigService.TestConnectionAsync(kind, cfg, activeKey);
                DispatcherQueue.TryEnqueue(() =>
                {
                    ring.IsActive = false;
                    ring.Visibility = Visibility.Collapsed;
                    btn.IsEnabled = true;
                    if (result.Success)
                    {
                        statusBlock.Text = $"✓ {result.Message} ({result.LatencyMs}ms)";
                        statusBlock.Foreground = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
                    }
                    else
                    {
                        statusBlock.Text = $"✗ {result.Message}";
                        statusBlock.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
                    }
                });
            }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    ring.IsActive = false;
                    ring.Visibility = Visibility.Collapsed;
                    btn.IsEnabled = true;
                    statusBlock.Text = $"✗ Test error: {ex.Message}";
                    statusBlock.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
                });
            }
        }, new AppLogger(), nameof(RunCapabilityTest));
    }

    private async void OnSaveVoiceConfigClicked(object sender, RoutedEventArgs e)
    {
        SaveVoiceConfigButton.IsEnabled = false;
        try
        {
            var config = new VoiceModeModelsConfig
            {
                Hearing = new VoiceModelCapabilityConfig
                {
                    Capability = VoiceCapabilityKind.Hearing,
                    IsEnabled = HearingEnabledToggle.IsOn,
                    Provider = GetSelectedTagOrContent(HearingProviderCombo) ?? "Google",
                    Model = GetSelectedTagOrContent(HearingModelCombo) ?? "gemini-2.5-flash-native-audio-preview-12-2025",
                    Language = GetSelectedTagOrContent(HearingLanguageCombo) ?? "auto",
                    Endpoint = string.IsNullOrWhiteSpace(HearingEndpointBox.Text) ? null : HearingEndpointBox.Text.Trim()
                },
                Thinking = new VoiceModelCapabilityConfig
                {
                    Capability = VoiceCapabilityKind.Thinking,
                    IsEnabled = ThinkingEnabledToggle.IsOn,
                    Provider = GetSelectedTagOrContent(ThinkingProviderCombo) ?? "Google",
                    Model = GetSelectedTagOrContent(ThinkingModelCombo) ?? "gemini-2.5-flash-native-audio-preview-12-2025",
                    Temperature = ThinkingTempSlider.Value,
                    Endpoint = string.IsNullOrWhiteSpace(ThinkingEndpointBox.Text) ? null : ThinkingEndpointBox.Text.Trim()
                },
                Speaking = new VoiceModelCapabilityConfig
                {
                    Capability = VoiceCapabilityKind.Speaking,
                    IsEnabled = SpeakingEnabledToggle.IsOn,
                    Provider = GetSelectedTagOrContent(SpeakingProviderCombo) ?? "EdgeTTS",
                    Model = GetSelectedTagOrContent(SpeakingVoiceCombo) ?? "en-US-GuyNeural",
                    Voice = GetSelectedTagOrContent(SpeakingVoiceCombo) ?? "en-US-GuyNeural",
                    Speed = SpeakingSpeedSlider.Value,
                    Language = GetSelectedTagOrContent(SpeakingLanguageCombo) ?? "en"
                },
                Background = new VoiceModelCapabilityConfig
                {
                    Capability = VoiceCapabilityKind.Background,
                    IsEnabled = BackgroundEnabledToggle.IsOn,
                    Provider = GetSelectedTagOrContent(BackgroundProviderCombo) ?? "Google",
                    Model = GetSelectedTagOrContent(BackgroundModelCombo) ?? "gemini-2.5-flash",
                    TimeoutSeconds = (int)BackgroundTimeoutSlider.Value,
                    MaxOutputTokens = int.TryParse(GetSelectedTagOrContent(BackgroundTokensCombo), out var tok) ? tok : 4096
                },
                Coding = new VoiceModelCapabilityConfig
                {
                    Capability = VoiceCapabilityKind.Coding,
                    IsEnabled = CodingEnabledToggle.IsOn,
                    Provider = GetSelectedTagOrContent(CodingProviderCombo) ?? "Anthropic",
                    Model = GetSelectedTagOrContent(CodingModelCombo) ?? "claude-3-5-sonnet-20241022",
                    Temperature = CodingTempSlider.Value,
                    Endpoint = string.IsNullOrWhiteSpace(CodingEndpointBox.Text) ? null : CodingEndpointBox.Text.Trim()
                }
            };

            var updatedKeys = new Dictionary<VoiceCapabilityKind, string?>
            {
                [VoiceCapabilityKind.Hearing] = GetActiveKeyForCapability(VoiceCapabilityKind.Hearing, HearingApiKeyBox, HearingApiKeyTextBox),
                [VoiceCapabilityKind.Thinking] = GetActiveKeyForCapability(VoiceCapabilityKind.Thinking, ThinkingApiKeyBox, ThinkingApiKeyTextBox),
                [VoiceCapabilityKind.Speaking] = GetActiveKeyForCapability(VoiceCapabilityKind.Speaking, SpeakingApiKeyBox, SpeakingApiKeyTextBox),
                [VoiceCapabilityKind.Background] = GetActiveKeyForCapability(VoiceCapabilityKind.Background, BackgroundApiKeyBox, BackgroundApiKeyTextBox),
                [VoiceCapabilityKind.Coding] = GetActiveKeyForCapability(VoiceCapabilityKind.Coding, CodingApiKeyBox, CodingApiKeyTextBox)
            };

            await _voiceConfigService.SaveConfigurationAsync(config, updatedKeys);
            ShowStatusMessage("Voice Mode models and encrypted API keys saved successfully.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatusMessage($"Failed to save configuration: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            SaveVoiceConfigButton.IsEnabled = true;
        }
    }

    private async void OnSyncVoiceRuntimeClicked(object sender, RoutedEventArgs e)
    {
        SyncVoiceRuntimeButton.IsEnabled = false;
        try
        {
            await _voiceConfigService.SyncToRuntimeAsync();
            ShowStatusMessage("Voice Mode runtime synchronized with capability configurations.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatusMessage($"Sync failed: {ex.Message}", InfoBarSeverity.Warning);
        }
        finally
        {
            SyncVoiceRuntimeButton.IsEnabled = true;
        }
    }

    private void ShowStatusMessage(string message, InfoBarSeverity severity)
    {
        VoiceModeStatusInfoBar.Message = message;
        VoiceModeStatusInfoBar.Severity = severity;
        VoiceModeStatusInfoBar.IsOpen = true;
    }

    private static string? GetSelectedTagOrContent(ComboBox? combo)
    {
        if (combo == null) return null;
        if (combo.SelectedItem is ComboBoxItem item)
            return item.Tag as string ?? item.Content as string;
        return combo.SelectedItem as string ?? combo.SelectedValue as string;
    }

    private static void SelectItemByTagOrContent(ComboBox combo, string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        foreach (var obj in combo.Items)
        {
            if (obj is ComboBoxItem item)
            {
                var tag = item.Tag as string;
                var content = item.Content as string;
                if (string.Equals(tag, value, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(content, value, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
            else if (obj is string str && string.Equals(str, value, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = obj;
                return;
            }
        }
    }

    private static void SetComboOptions(ComboBox? combo, IEnumerable<string> options, string? defaultSelection = null)
    {
        if (combo == null) return;
        combo.Items.Clear();
        ComboBoxItem? selectedItem = null;
        foreach (var opt in options)
        {
            var item = new ComboBoxItem { Content = opt, Tag = opt };
            combo.Items.Add(item);
            if (selectedItem == null && (defaultSelection == null || string.Equals(opt, defaultSelection, StringComparison.OrdinalIgnoreCase) || opt.StartsWith(defaultSelection, StringComparison.OrdinalIgnoreCase)))
            {
                selectedItem = item;
            }
        }
        if (selectedItem != null)
        {
            combo.SelectedItem = selectedItem;
        }
        else if (combo.Items.Count > 0)
        {
            combo.SelectedIndex = 0;
        }
    }

    #endregion
}
