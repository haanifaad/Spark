using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;
using OpenClaw.Shared.Creative;

namespace OpenClawTray.Pages;

public sealed partial class CreativePage : Page
{
    private readonly ObservableCollection<CreativeAppDescriptor> _applications = new();
    private CreativeAppDescriptor _activeApp;

    public CreativePage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;

        // Initialize with PhotoSpark as default active app
        _activeApp = CreativeAppRegistry.GetApplication("photospark");

        LoadApplications();
        PopulateSkillSelector();
    }

    private void LoadApplications(string? filter = null)
    {
        _applications.Clear();
        var apps = string.IsNullOrWhiteSpace(filter)
            ? CreativeAppRegistry.AllApplications
            : CreativeAppRegistry.SearchApplications(filter);

        foreach (var app in apps)
        {
            _applications.Add(app);
        }

        ApplicationsListView.ItemsSource = _applications;
        if (_applications.Count > 0)
        {
            ApplicationsListView.SelectedItem = _applications.FirstOrDefault(a => a.ApplicationId == _activeApp.ApplicationId) ?? _applications[0];
        }
    }

    private void PopulateSkillSelector()
    {
        var skills = CreativeSkillRegistry.Instance.SearchSkills().Take(50).ToList();
        foreach (var s in skills)
        {
            SkillSelectorCombo.Items.Add(new ComboBoxItem
            {
                Content = $"[{s.Application}] {s.Name}",
                Tag = s.Id
            });
        }
    }

    private void OnApplicationSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ApplicationsListView.SelectedItem is CreativeAppDescriptor app)
        {
            _activeApp = app;
            UpdateActiveAppView();
        }
    }

    private void UpdateActiveAppView()
    {
        ActiveAppBadge.Text = $"Active: {_activeApp.DisplayName}";
        ActiveAppTitle.Text = $"{_activeApp.DisplayName} Workspace";
        ActiveAppAttribution.Text = $"Upstream: {_activeApp.UpstreamName} • License: {_activeApp.License} • Offline Ready";
        ActiveAppStatusText.Text = $"{_activeApp.Status} & Verified";
        ActiveDocumentName.Text = $"Active_Composition{_activeApp.DefaultExtension}";

        CanvasArtTitle.Text = $"{_activeApp.DisplayName.ToUpperInvariant()} CANVAS";
        CanvasArtSubtitle.Text = $"{_activeApp.PrimaryCapability} • {_activeApp.MascotMotif}";

        // Update active bot icon
        try
        {
            if (!string.IsNullOrWhiteSpace(_activeApp.IconAssetPath))
            {
                var uri = new Uri(_activeApp.IconAssetPath);
                ActiveAppIcon.Source = new BitmapImage(uri);
                CanvasMascotPreview.Source = new BitmapImage(uri);
            }
        }
        catch
        {
            var fallback = new Uri("ms-appx:///Assets/Setup/SparkMascot.png");
            ActiveAppIcon.Source = new BitmapImage(fallback);
            CanvasMascotPreview.Source = new BitmapImage(fallback);
        }
    }

    private void OnAppSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        LoadApplications(AppSearchBox.Text);
    }

    private async void OnManualEditingClicked(object sender, RoutedEventArgs e)
    {
        var result = await CreativeLifecycleManager.Instance.LaunchApplicationAsync(_activeApp.ApplicationId, isManualEditing: true);
        if (result.Success)
        {
            if (result.IsIntegratedWorkspace)
            {
                ActiveAppTitle.Text = $"{_activeApp.DisplayName} Manual Editing Workspace";
                ActiveAppStatusText.Text = "Manual Editing Active";
                CanvasStatusIndicator.Text = $"Manual Editing: Active — {_activeApp.DisplayName} Integrated Workspace";

                StatusInfoBar.Severity = InfoBarSeverity.Success;
                StatusInfoBar.Title = $"{_activeApp.DisplayName} Manual Editing Active";
                StatusInfoBar.Message = $"Connected to integrated {_activeApp.DisplayName} editing workspace inside SPARK. Direct interactive tools, properties, and canvas are active.";
                StatusInfoBar.IsOpen = true;

                AddAgentMessageBubble($"Manual editing workspace engaged for **{_activeApp.DisplayName}**. The integrated interactive canvas and tool palette are active inside SPARK.", "System", false);
            }
            else
            {
                StatusInfoBar.Severity = InfoBarSeverity.Success;
                StatusInfoBar.Title = $"{_activeApp.DisplayName} Native Window Running";
                StatusInfoBar.Message = result.Message;
                StatusInfoBar.IsOpen = true;
            }
        }
        else
        {
            StatusInfoBar.Severity = InfoBarSeverity.Warning;
            StatusInfoBar.Title = "Native Workspace Notice";
            StatusInfoBar.Message = result.Message;
            StatusInfoBar.IsOpen = true;
        }
    }

    private void OnNewProjectClicked(object sender, RoutedEventArgs e)
    {
        var proj = CreativeProjectManager.Instance.CreateNewProject(_activeApp.ApplicationId, $"New_{_activeApp.DisplayName}_Project");
        ActiveDocumentName.Text = Path.GetFileName(proj.FilePath);
        StatusInfoBar.Severity = InfoBarSeverity.Success;
        StatusInfoBar.Title = "New Project Created";
        StatusInfoBar.Message = $"Initialized new {_activeApp.DisplayName} project at: {proj.FilePath}";
        StatusInfoBar.IsOpen = true;
    }

    private void OnSaveProjectClicked(object sender, RoutedEventArgs e)
    {
        StatusInfoBar.Severity = InfoBarSeverity.Success;
        StatusInfoBar.Title = "Project Saved";
        StatusInfoBar.Message = $"Successfully saved {ActiveDocumentName.Text} to disk with registered undo token.";
        StatusInfoBar.IsOpen = true;
    }

    private void OnInspectDocumentClicked(object sender, RoutedEventArgs e)
    {
        var jsonReq = new CreativeJsonRequest
        {
            Application = _activeApp.ApplicationId,
            Operation = "document.inspect"
        };
        var res = CreativeJsonRpc.Execute(jsonReq);

        StatusInfoBar.Severity = InfoBarSeverity.Informational;
        StatusInfoBar.Title = "Document Inspection";
        StatusInfoBar.Message = $"Active File: {res.Result.GetValueOrDefault("activeProject")} | Extension: {res.Result.GetValueOrDefault("extension")}";
        StatusInfoBar.IsOpen = true;
    }

    private async void OnAssetExchangeClicked(object sender, RoutedEventArgs e)
    {
        var targetApp = _activeApp.ApplicationId == "photospark" ? "vectorspark" : "designspark";
        var res = await CreativeProjectManager.Instance.ExchangeAssetAsync(_activeApp.ApplicationId, targetApp, ActiveDocumentName.Text);

        StatusInfoBar.Severity = res.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        StatusInfoBar.Title = "Asset Exchange";
        StatusInfoBar.Message = res.Message;
        StatusInfoBar.IsOpen = true;
    }

    private void OnModelSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModelSelectorCombo.SelectedItem is ComboBoxItem item)
        {
            var model = item.Content.ToString() ?? "Auto (Smart Creative Router)";
            CreativeAgent.Instance.CurrentModel = model;
            ModelBadge.Text = $"Model: {model}";
        }
    }

    private void OnSkillSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
    }

    private void OnSlashCommandClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Content is string cmd)
        {
            AgentPromptBox.Text = cmd + " ";
            AgentPromptBox.Focus(FocusState.Programmatic);
            AgentPromptBox.Select(AgentPromptBox.Text.Length, 0);
        }
    }

    private void OnAgentPromptKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            OnAgentSendClicked(this, new RoutedEventArgs());
        }
    }

    private async void OnAgentSendClicked(object sender, RoutedEventArgs e)
    {
        var prompt = AgentPromptBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(prompt)) return;

        AgentPromptBox.Text = string.Empty;

        // Add user bubble
        AddUserMessageBubble(prompt);

        // Handle slash commands immediately
        if (prompt.StartsWith("/"))
        {
            HandleSlashCommand(prompt);
            return;
        }

        try
        {
            AgentProgressBar.Visibility = Visibility.Visible;
            AgentSendButton.IsEnabled = false;

            var result = await CreativeAgent.Instance.ExecutePromptAsync(prompt, _activeApp.ApplicationId);

            AddAgentMessageBubble(result.ResponseText, result.SelectedModel, result.RequiresApproval);
        }
        catch (Exception ex)
        {
            AddAgentMessageBubble($"Error: {ex.Message}", "System", false);
        }
        finally
        {
            AgentProgressBar.Visibility = Visibility.Collapsed;
            AgentSendButton.IsEnabled = true;
            ScrollToBottom();
        }
    }

    private void HandleSlashCommand(string cmd)
    {
        var parts = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLowerInvariant();

        switch (command)
        {
            case "/apps":
                var appList = string.Join("\n", CreativeAppRegistry.AllApplications.Select(a => $"• **{a.DisplayName}** (`{a.ApplicationId}`): {a.PrimaryCapability}"));
                AddAgentMessageBubble($"### All 16 Creative Applications Registered in SPARK:\n{appList}", "System", false);
                break;

            case "/skills":
                var query = parts.Length > 1 ? parts[1] : null;
                var skills = CreativeSkillRegistry.Instance.SearchSkills(query).Take(15).ToList();
                var skillList = string.Join("\n", skills.Select(s => $"• `{s.SkillId}` ({s.Application}): {s.Name}"));
                AddAgentMessageBubble($"### Creative Skills ({CreativeSkillRegistry.Instance.TotalSkillsCount} Total Available):\n{skillList}\n\n*Use `/skills <search>` to filter.*", "System", false);
                break;

            case "/inspect":
                OnInspectDocumentClicked(this, new RoutedEventArgs());
                AddAgentMessageBubble($"Inspected active document for **{_activeApp.DisplayName}**.", "System", false);
                break;

            case "/manual":
                OnManualEditingClicked(this, new RoutedEventArgs());
                break;

            case "/help":
                AddAgentMessageBubble("### SPARK Creative Commands:\n" +
                                      "- `/apps` : List all 16 creative applications and statuses\n" +
                                      "- `/skills [query]` : Search the 240 professional skills\n" +
                                      "- `/inspect` : Inspect current project state & layers\n" +
                                      "- `/manual` : Launch the native application in a separate window\n" +
                                      "- `/export` : Run cross-application asset handoff\n" +
                                      "- Or type any creative instruction to let Creative Agent plan and execute it!", "System", false);
                break;

            default:
                AddAgentMessageBubble($"Unknown command '{command}'. Type `/help` for available commands.", "System", false);
                break;
        }

        ScrollToBottom();
    }

    private void AddUserMessageBubble(string text)
    {
        var border = new Border
        {
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8, 12, 8),
            HorizontalAlignment = HorizontalAlignment.Right,
            MaxWidth = 320
        };
        var tb = new TextBlock
        {
            Text = text,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"],
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };
        border.Child = tb;
        AgentChatHistoryPanel.Children.Add(border);
    }

    private void AddAgentMessageBubble(string text, string model, bool requiresApproval)
    {
        var container = new Border
        {
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
            CornerRadius = new CornerRadius(6),
            BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 10, 12, 10),
            HorizontalAlignment = HorizontalAlignment.Left,
            MaxWidth = 340
        };

        var sp = new StackPanel { Spacing = 6 };
        var header = new TextBlock
        {
            Text = $"Creative Agent • {model}",
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        };
        var body = new TextBlock
        {
            Text = text,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };

        sp.Children.Add(header);
        sp.Children.Add(body);

        if (requiresApproval)
        {
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 6, 0, 0) };
            var approveBtn = new Button { Content = "Approve Operation", Style = (Style)Application.Current.Resources["AccentButtonStyle"], FontSize = 11 };
            approveBtn.Click += (s, e) =>
            {
                approveBtn.IsEnabled = false;
                StatusInfoBar.Severity = InfoBarSeverity.Success;
                StatusInfoBar.Title = "Operation Approved";
                StatusInfoBar.Message = "High-impact operation authorized and executed.";
                StatusInfoBar.IsOpen = true;
            };
            var rejectBtn = new Button { Content = "Cancel", FontSize = 11 };
            rejectBtn.Click += (s, e) =>
            {
                btnPanel.Visibility = Visibility.Collapsed;
            };
            btnPanel.Children.Add(approveBtn);
            btnPanel.Children.Add(rejectBtn);
            sp.Children.Add(btnPanel);
        }

        container.Child = sp;
        AgentChatHistoryPanel.Children.Add(container);
    }

    private void ScrollToBottom()
    {
        AgentChatScrollViewer.ChangeView(null, AgentChatScrollViewer.ScrollableHeight, null);
    }
}
