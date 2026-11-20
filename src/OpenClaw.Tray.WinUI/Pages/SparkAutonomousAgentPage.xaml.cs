using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OpenClawTray.Presentation;

namespace OpenClawTray.Pages;

public sealed partial class SparkAutonomousAgentPage : Page
{
    private SparkAutonomousAgentViewModel? _viewModel;

    public SparkAutonomousAgentPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (DataContext is SparkAutonomousAgentViewModel vm)
        {
            _viewModel = vm;
            AttachViewModel(vm);
        }
    }

    internal void Initialize(SparkAutonomousAgentViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        AttachViewModel(viewModel);
    }

    private void AttachViewModel(SparkAutonomousAgentViewModel vm)
    {
        vm.PropertyChanged += (s, args) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (args.PropertyName == nameof(SparkAutonomousAgentViewModel.StatusMessage))
                {
                    if (!string.IsNullOrWhiteSpace(vm.StatusMessage))
                    {
                        DashboardInfoBar.Message = vm.StatusMessage;
                        DashboardInfoBar.Severity = vm.IsStatusError ? InfoBarSeverity.Error : InfoBarSeverity.Success;
                        DashboardInfoBar.IsOpen = true;
                    }
                    else
                    {
                        DashboardInfoBar.IsOpen = false;
                    }
                }
                else if (args.PropertyName == nameof(SparkAutonomousAgentViewModel.IsMemoriesEmpty))
                {
                    MemoryEmptyBanner.Visibility = vm.IsMemoriesEmpty ? Visibility.Visible : Visibility.Collapsed;
                }
                else if (args.PropertyName == nameof(SparkAutonomousAgentViewModel.CompiledSystemPrompt))
                {
                    SystemPromptPreviewBox.Text = vm.CompiledSystemPrompt;
                }
                else if (args.PropertyName == nameof(SparkAutonomousAgentViewModel.ToolDryRunOutput))
                {
                    ToolOutputBox.Text = vm.ToolDryRunOutput;
                }
                else if (args.PropertyName == nameof(SparkAutonomousAgentViewModel.RoutingResultText))
                {
                    RoutingDecisionBox.Text = vm.RoutingResultText;
                }
                else if (args.PropertyName == nameof(SparkAutonomousAgentViewModel.BackgroundWorkerStateText))
                {
                    BackgroundWorkerStatusBlock.Text = vm.BackgroundWorkerStateText;
                }
                else if (args.PropertyName == nameof(SparkAutonomousAgentViewModel.SpendingLimitsOverviewText))
                {
                    SpendingLimitsStatusBlock.Text = vm.SpendingLimitsOverviewText;
                }
                else if (args.PropertyName == nameof(SparkAutonomousAgentViewModel.IsTaskSelected))
                {
                    TaskInspectorPanel.Visibility = vm.IsTaskSelected ? Visibility.Visible : Visibility.Collapsed;
                }
                else if (args.PropertyName == nameof(SparkAutonomousAgentViewModel.SelectedTask))
                {
                    InspectorTitleText.Text = vm.SelectedTask != null ? $"{vm.SelectedTask.Title} ({vm.SelectedTask.State})" : "Task Details";
                }
                else if (args.PropertyName == nameof(SparkAutonomousAgentViewModel.SelectedTaskInstruction))
                {
                    EditInstructionBox.Text = vm.SelectedTaskInstruction;
                }
                else if (args.PropertyName == nameof(SparkAutonomousAgentViewModel.IsTakeoverActive))
                {
                    TakeoverToggleButton.Content = vm.IsTakeoverActive ? "Release Takeover" : "Take Over";
                }
            });
        };

        TaskListView.ItemsSource = vm.Tasks;
        TaskStepsListView.ItemsSource = vm.SelectedTaskSteps;
        MemoryListView.ItemsSource = vm.Memories;
        ChannelMessageListView.ItemsSource = vm.RecentMessages;
        ToolListView.ItemsSource = vm.Tools;
        ModelTiersListView.ItemsSource = vm.Models;
        ApprovalsListView.ItemsSource = vm.PendingApprovals;
        AuditRecordsListView.ItemsSource = vm.AuditRecords;

        vm.Activate(null);
    }

    private void OnSectionNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            TasksSection.Visibility = tag == "tasks" ? Visibility.Visible : Visibility.Collapsed;
            MemorySection.Visibility = tag == "memory" ? Visibility.Visible : Visibility.Collapsed;
            PersonaSection.Visibility = tag == "persona" ? Visibility.Visible : Visibility.Collapsed;
            ChannelsSection.Visibility = tag == "channels" ? Visibility.Visible : Visibility.Collapsed;
            ToolsSection.Visibility = tag == "tools" ? Visibility.Visible : Visibility.Collapsed;
            ModelsSection.Visibility = tag == "models" ? Visibility.Visible : Visibility.Collapsed;
            ApprovalsSection.Visibility = tag == "approvals" ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private async void OnRefreshAllClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            await _viewModel.LoadAllDataAsync();
        }
    }

    private async void OnAddMemoryClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            var catItem = MemoryCategoryCombo.SelectedItem as ComboBoxItem;
            _viewModel.NewMemoryCategory = catItem?.Content?.ToString() ?? "GeneralFact";
            _viewModel.NewMemoryContent = MemoryContentInput.Text;
            await _viewModel.AddMemoryAsync();
            MemoryContentInput.Text = string.Empty;
        }
    }

    private async void OnForgetMemoryClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null && sender is Button btn && btn.Tag is string memoryId)
        {
            await _viewModel.ForgetMemoryAsync(memoryId);
        }
    }

    private void OnMemorySearchChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (_viewModel != null)
        {
            _viewModel.MemorySearchQuery = sender.Text;
        }
    }

    private void OnMemorySearchSubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (_viewModel != null)
        {
            _viewModel.MemorySearchQuery = args.QueryText;
        }
    }

    private async void OnSavePersonaClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.AgentName = AgentNameInput.Text;
            _viewModel.SelectedTone = (AgentToneCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Professional";
            _viewModel.SelectedLanguage = (AgentLanguageCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "English";
            _viewModel.CustomInstructions = CustomInstructionsInput.Text;
            _viewModel.BehavioralRules = BehavioralRulesInput.Text;
            _viewModel.ProhibitedActions = ProhibitedActionsInput.Text;

            await _viewModel.SavePersonaAsync();
        }
    }

    private async void OnSaveTelegramTokenClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null && !string.IsNullOrWhiteSpace(TelegramTokenBox.Password))
        {
            _viewModel.SelectedChannelId = "tg_main";
            _viewModel.ChannelSecretInput = TelegramTokenBox.Password;
            await _viewModel.SaveChannelSecretAsync();
            TelegramTokenBox.Password = string.Empty;
        }
    }

    private async void OnSaveWhatsAppTokenClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null && !string.IsNullOrWhiteSpace(WhatsAppTokenBox.Password))
        {
            _viewModel.SelectedChannelId = "wa_main";
            _viewModel.ChannelSecretInput = WhatsAppTokenBox.Password;
            await _viewModel.SaveChannelSecretAsync();
            WhatsAppTokenBox.Password = string.Empty;
        }
    }

    private void OnToolSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ToolListView.SelectedItem is ToolItemViewModel tool && _viewModel != null)
        {
            _viewModel.SelectedToolId = tool.Id;
        }
    }

    private async void OnExecuteToolDryRunClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.ToolDryRunArgs = ToolArgsInput.Text;
            await _viewModel.ExecuteToolDryRunAsync();
        }
    }

    private void OnSimulateRoutingClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.SelectedPrivacyMode = (PrivacyModeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()?.Split(' ')[0] ?? "Balanced";
            _viewModel.RoutingTestCategory = (RoutingCategoryCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ComplexReasoning";
            _viewModel.TestRouting();
        }
    }

    private async void OnApproveOperationClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null && sender is Button btn && btn.Tag is string reqId)
        {
            await _viewModel.ApproveOperationAsync(reqId);
        }
    }

    private async void OnRejectOperationClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null && sender is Button btn && btn.Tag is string reqId)
        {
            await _viewModel.RejectOperationAsync(reqId);
        }
    }

    private async void OnRunWorkerCycleClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            await _viewModel.TriggerBackgroundCycleAsync();
        }
    }

    private async void OnSubmitTaskClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null && !string.IsNullOrWhiteSpace(TaskInstructionInput.Text))
        {
            _viewModel.NewTaskInstruction = TaskInstructionInput.Text;
            _viewModel.NewTaskPriority = (TaskPriorityCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Normal";
            await _viewModel.SubmitNewTaskAsync();
            TaskInstructionInput.Text = string.Empty;
        }
    }

    private void OnTaskFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel != null && TaskFilterCombo.SelectedItem is ComboBoxItem item && item.Content is string filter)
        {
            _viewModel.TaskFilter = filter;
        }
    }

    private void OnTaskSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel != null && TaskListView.SelectedItem is TaskItemViewModel task)
        {
            _viewModel.SelectedTask = task;
        }
    }

    private async void OnPauseTaskClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            await _viewModel.PauseCurrentTaskAsync();
        }
    }

    private async void OnResumeTaskClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            await _viewModel.ResumeCurrentTaskAsync();
        }
    }

    private async void OnCancelTaskClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            await _viewModel.CancelCurrentTaskAsync();
        }
    }

    private async void OnToggleTakeoverClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            await _viewModel.ToggleTakeoverAsync();
        }
    }

    private async void OnModifyInstructionClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null && !string.IsNullOrWhiteSpace(EditInstructionBox.Text))
        {
            _viewModel.EditInstructionInput = EditInstructionBox.Text;
            await _viewModel.ModifyCurrentTaskInstructionAsync();
        }
    }
}
