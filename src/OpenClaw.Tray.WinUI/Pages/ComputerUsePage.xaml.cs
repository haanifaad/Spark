using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using OpenClawTray.Presentation;
using Windows.Storage.Streams;

namespace OpenClawTray.Pages;

public sealed partial class ComputerUsePage : Page
{
    private ComputerUseViewModel? _viewModel;

    public ComputerUsePage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (DataContext is ComputerUseViewModel vm)
        {
            _viewModel = vm;
            AttachViewModel(vm);
        }
    }

    internal void Initialize(ComputerUseViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        AttachViewModel(viewModel);
    }

    private void AttachViewModel(ComputerUseViewModel vm)
    {
        StepsListView.ItemsSource = vm.Steps;
        LogsListView.ItemsSource = vm.Logs;
        SessionsListView.ItemsSource = vm.Sessions;

        DesktopControlToggle.IsOn = vm.IsDesktopControlPermitted;
        RequireConfirmationToggle.IsOn = vm.RequireConfirmationForSensitive;
        ActionDelaySlider.Value = vm.ActionDelayMs;
        ScreenshotQualitySlider.Value = vm.ScreenshotQuality;

        vm.PropertyChanged += (s, args) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                switch (args.PropertyName)
                {
                    case nameof(ComputerUseViewModel.StatusText):
                        StatusBadge.Text = vm.StatusText;
                        break;

                    case nameof(ComputerUseViewModel.CurrentThought):
                        CurrentThoughtText.Text = vm.CurrentThought;
                        break;

                    case nameof(ComputerUseViewModel.CurrentActionText):
                        CurrentActionText.Text = vm.CurrentActionText;
                        break;

                    case nameof(ComputerUseViewModel.CurrentActionType):
                        ActionTypeBadge.Text = vm.CurrentActionType;
                        break;

                    case nameof(ComputerUseViewModel.CurrentVerificationText):
                        VerificationResultText.Text = vm.CurrentVerificationText;
                        break;

                    case nameof(ComputerUseViewModel.CurrentWindowText):
                        ActiveWindowText.Text = $"Active: {vm.CurrentWindowText}";
                        break;

                    case nameof(ComputerUseViewModel.DurationText):
                        TotalDurationText.Text = vm.DurationText;
                        break;

                    case nameof(ComputerUseViewModel.StepCountText):
                        TotalStepsText.Text = vm.StepCountText;
                        StepCountBadge.Text = vm.StepCountText;
                        break;

                    case nameof(ComputerUseViewModel.TokensText):
                        TotalTokensText.Text = vm.TokensText;
                        break;

                    case nameof(ComputerUseViewModel.IsEmergencyStopped):
                        EmergencyStopAlertBar.IsOpen = vm.IsEmergencyStopped;
                        break;

                    case nameof(ComputerUseViewModel.IsConfirmationPromptVisible):
                        ConfirmationInfoBar.IsOpen = vm.IsConfirmationPromptVisible;
                        ConfirmationInfoBar.Message = vm.ConfirmationPromptMessage;
                        break;

                    case nameof(ComputerUseViewModel.IsPaused):
                        PauseResumeButton.Content = vm.IsPaused ? "Resume" : "Pause";
                        break;

                    case nameof(ComputerUseViewModel.CurrentScreenshotBase64):
                        UpdateScreenPreview(vm.CurrentScreenshotBase64);
                        break;
                }
            });
        };
    }

    private async void UpdateScreenPreview(string? base64)
    {
        if (string.IsNullOrEmpty(base64))
        {
            ScreenPreviewImage.Source = null;
            NoPreviewPlaceholder.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            byte[] bytes = Convert.FromBase64String(base64);
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);

            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            ScreenPreviewImage.Source = bitmap;
            NoPreviewPlaceholder.Visibility = Visibility.Collapsed;
        }
        catch
        {
            NoPreviewPlaceholder.Visibility = Visibility.Visible;
        }
    }

    private void OnSectionNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            string tag = item.Tag?.ToString() ?? "workspace";
            WorkspaceSection.Visibility = tag == "workspace" ? Visibility.Visible : Visibility.Collapsed;
            SessionsSection.Visibility = tag == "sessions" ? Visibility.Visible : Visibility.Collapsed;
            SettingsSection.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;

            if (tag == "sessions" && _viewModel != null)
            {
                _viewModel.RefreshSessions();
            }
        }
    }

    private async void OnStartRunClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        _viewModel.GoalInput = GoalTextBox.Text;
        if (ModelSelectorCombo.SelectedItem is ComboBoxItem cbi && cbi.Content is string modelName)
        {
            _viewModel.SelectedModel = modelName;
        }
        _viewModel.StepByStepMode = StepByStepCheck.IsChecked == true;
        await _viewModel.StartTaskAsync();
    }

    private async void OnSingleStepClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        _viewModel.GoalInput = GoalTextBox.Text;
        await _viewModel.ExecuteStepAsync();
    }

    private void OnPauseResumeClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        if (_viewModel.IsPaused)
            _viewModel.ResumeTask();
        else
            _viewModel.PauseTask();
    }

    private void OnStopClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.StopTask();
    }

    private void OnEmergencyStopClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.TriggerEmergencyStop();
    }

    private void OnResetEmergencyStopClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.ResetEmergencyStop();
    }

    private void OnApproveActionClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.ApproveSensitiveAction(true);
    }

    private void OnDeclineActionClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.ApproveSensitiveAction(false);
    }

    private void OnRefreshSessionsClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.RefreshSessions();
    }

    private void OnDesktopControlToggled(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.IsDesktopControlPermitted = DesktopControlToggle.IsOn;
        }
    }

    private void OnRequireConfirmationToggled(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.RequireConfirmationForSensitive = RequireConfirmationToggle.IsOn;
        }
    }

    private void OnActionDelayChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.ActionDelayMs = (int)e.NewValue;
        }
    }

    private void OnQualityChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.ScreenshotQuality = (int)e.NewValue;
        }
    }
}
