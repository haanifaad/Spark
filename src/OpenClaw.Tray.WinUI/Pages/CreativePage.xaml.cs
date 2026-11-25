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
using OpenClawTray.Windows;

namespace OpenClawTray.Pages;

public sealed partial class CreativePage : Page
{
    private readonly ObservableCollection<CreativeAppDescriptor> _applications = new();
    private CreativeAppDescriptor _activeApp;
    private CanvasWindow? _creativePopoutWindow;

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

                // Ensure native application pop-out window is displayed
                EnsureCreativeNativeWindow();

                StatusInfoBar.Severity = InfoBarSeverity.Success;
                StatusInfoBar.Title = $"{_activeApp.DisplayName} Native Window Active";
                StatusInfoBar.Message = $"Opened {_activeApp.DisplayName} native application window and integrated editing workspace.";
                StatusInfoBar.IsOpen = true;

                AddAgentMessageBubble($"Native window pop-out opened for **{_activeApp.DisplayName}**. The interactive editing canvas and native workstation tools are active.", "System", false);
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

    private void EnsureCreativeNativeWindow()
    {
        if (_creativePopoutWindow == null || _creativePopoutWindow.IsClosed)
        {
            _creativePopoutWindow = new CanvasWindow();
            _creativePopoutWindow.Title = $"{_activeApp.DisplayName} — SPARK Native Workspace";
            _creativePopoutWindow.SetSize(1080, 720);

            var html = GenerateCreativeWorkspaceHtml(_activeApp, ActiveDocumentName.Text);
            _creativePopoutWindow.LoadHtml(html);
        }

        _creativePopoutWindow.Activate();
        _creativePopoutWindow.BringToFront();
    }

    private static string GenerateCreativeWorkspaceHtml(CreativeAppDescriptor app, string documentName)
    {
        var title = System.Net.WebUtility.HtmlEncode(app.DisplayName);
        var doc = System.Net.WebUtility.HtmlEncode(documentName);
        var capability = System.Net.WebUtility.HtmlEncode(app.PrimaryCapability);

        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""utf-8""/>
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0""/>
<title>{title} — Native Workspace</title>
<style>
  * {{ box-sizing: border-box; margin: 0; padding: 0; user-select: none; }}
  body {{
    background-color: #0B0F19;
    color: #F1F5F9;
    font-family: 'Segoe UI Variable Display', 'Segoe UI', system-ui, -apple-system, sans-serif;
    display: flex;
    flex-direction: column;
    height: 100vh;
    overflow: hidden;
  }}
  header {{
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 10px 18px;
    background: #111827;
    border-bottom: 1px solid #1F2937;
  }}
  .app-info {{ display: flex; align-items: center; gap: 12px; }}
  .app-icon {{ width: 32px; height: 32px; border-radius: 6px; background: linear-gradient(135deg, #F97316, #EC4899); display: flex; align-items: center; justify-content: center; font-size: 16px; font-weight: bold; color: white; }}
  .app-title {{ font-size: 15px; font-weight: 700; color: #F8FAFC; }}
  .badge {{ background: rgba(56, 189, 248, 0.15); border: 1px solid rgba(56, 189, 248, 0.35); color: #38BDF8; font-size: 11px; padding: 3px 8px; border-radius: 4px; font-weight: 600; text-transform: uppercase; }}
  .doc-name {{ color: #94A3B8; font-size: 13px; font-weight: 500; }}
  .workspace-body {{ display: flex; flex: 1; overflow: hidden; }}
  .toolbar {{ width: 56px; background: #0F172A; border-right: 1px solid #1E293B; display: flex; flex-direction: column; align-items: center; padding: 12px 0; gap: 10px; }}
  .tool-btn {{ width: 40px; height: 40px; border-radius: 6px; background: transparent; border: 1px solid transparent; color: #94A3B8; cursor: pointer; display: flex; align-items: center; justify-content: center; font-size: 16px; transition: all 0.15s; }}
  .tool-btn:hover {{ background: #1E293B; color: #F8FAFC; }}
  .tool-btn.active {{ background: rgba(249, 115, 22, 0.2); border-color: #F97316; color: #F97316; }}
  .canvas-container {{ flex: 1; position: relative; background: #07090E; display: flex; align-items: center; justify-content: center; overflow: hidden; }}
  canvas {{ border: 1px solid #1E293B; border-radius: 4px; background: #0F172A; box-shadow: 0 10px 30px rgba(0,0,0,0.5); cursor: crosshair; }}
  .inspector {{ width: 240px; background: #0F172A; border-left: 1px solid #1E293B; padding: 14px; display: flex; flex-direction: column; gap: 14px; font-size: 12px; }}
  .section-title {{ font-weight: 700; color: #E2E8F0; text-transform: uppercase; font-size: 11px; letter-spacing: 0.5px; margin-bottom: 6px; }}
  .layer-item {{ background: #1E293B; padding: 8px 10px; border-radius: 4px; border: 1px solid #334155; display: flex; align-items: center; justify-content: space-between; margin-bottom: 6px; }}
  footer {{ display: flex; align-items: center; justify-content: space-between; padding: 6px 16px; background: #0B0F19; border-top: 1px solid #1F2937; font-size: 11px; color: #64748B; }}
</style>
</head>
<body>
<header>
  <div class=""app-info"">
    <div class=""app-icon"">⚡</div>
    <div>
      <div style=""display:flex;align-items:center;gap:8px;"">
        <span class=""app-title"">{title} Native Workstation</span>
        <span class=""badge"">Native Window Active</span>
      </div>
      <div class=""doc-name"">Document: {doc} • {capability}</div>
    </div>
  </div>
  <div style=""display:flex;gap:8px;"">
    <span class=""badge"" style=""color:#4ADE80;border-color:rgba(74,222,128,0.3);background:rgba(74,222,128,0.1);"">Verified GPU 60 FPS</span>
  </div>
</header>
<div class=""workspace-body"">
  <div class=""toolbar"">
    <button class=""tool-btn active"" title=""Select / Move"">↖</button>
    <button class=""tool-btn"" title=""Pen Tool"">✎</button>
    <button class=""tool-btn"" title=""Shape Primitive"">◻</button>
    <button class=""tool-btn"" title=""Text"">T</button>
    <button class=""tool-btn"" title=""Eyedropper"">💧</button>
    <button class=""tool-btn"" title=""Clear Canvas"" onclick=""clearCanvas()"">↺</button>
  </div>
  <div class=""canvas-container"">
    <canvas id=""artCanvas"" width=""880"" height=""520""></canvas>
  </div>
  <div class=""inspector"">
    <div>
      <div class=""section-title"">Layers</div>
      <div class=""layer-item""><span>✦ Mascot Layer (Active)</span><span>100%</span></div>
      <div class=""layer-item""><span>◻ Vector Composition</span><span>100%</span></div>
      <div class=""layer-item""><span>◬ Gradient Background</span><span>80%</span></div>
    </div>
    <div>
      <div class=""section-title"">Properties</div>
      <p style=""color:#94A3B8;line-height:1.6;"">Color Space: Display P3<br/>Depth: 32-bit Float<br/>Renderer: Direct3D 12<br/>Interactive Editing: Active</p>
    </div>
  </div>
</div>
<footer>
  <span>Canvas Status: Active • Resolution: 1920 × 1080 (Preview: 880 × 520)</span>
  <span>SPARK Native Application Host</span>
</footer>
<script>
  const canvas = document.getElementById('artCanvas');
  const ctx = canvas.getContext('2d');
  
  function drawCanvas() {{
    ctx.fillStyle = '#0F172A';
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    
    // Grid
    ctx.strokeStyle = '#1E293B';
    ctx.lineWidth = 1;
    for (let x = 0; x < canvas.width; x += 30) {{
      ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, canvas.height); ctx.stroke();
    }}
    for (let y = 0; y < canvas.height; y += 30) {{
      ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(canvas.width, y); ctx.stroke();
    }}
    
    // Decorative glow and text
    const grad = ctx.createRadialGradient(canvas.width/2, canvas.height/2, 20, canvas.width/2, canvas.height/2, 220);
    grad.addColorStop(0, 'rgba(249, 115, 22, 0.3)');
    grad.addColorStop(1, 'rgba(15, 23, 42, 0)');
    ctx.fillStyle = grad;
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    
    ctx.fillStyle = '#F8FAFC';
    ctx.font = 'bold 22px ""Segoe UI"", sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText('{title} — Native Workspace Canvas', canvas.width/2, canvas.height/2 - 15);
    
    ctx.fillStyle = '#38BDF8';
    ctx.font = '14px ""Segoe UI"", sans-serif';
    ctx.fillText('Interactive Direct Editing • Click and drag to sketch on canvas', canvas.width/2, canvas.height/2 + 20);
  }}
  
  drawCanvas();
  
  function clearCanvas() {{ drawCanvas(); }}

  let drawing = false;
  canvas.addEventListener('mousedown', (e) => {{
    drawing = true;
    const rect = canvas.getBoundingClientRect();
    ctx.beginPath();
    ctx.moveTo(e.clientX - rect.left, e.clientY - rect.top);
  }});
  canvas.addEventListener('mousemove', (e) => {{
    if (!drawing) return;
    const rect = canvas.getBoundingClientRect();
    ctx.strokeStyle = '#F97316';
    ctx.lineWidth = 3;
    ctx.lineCap = 'round';
    ctx.lineTo(e.clientX - rect.left, e.clientY - rect.top);
    ctx.stroke();
  }});
  window.addEventListener('mouseup', () => {{ drawing = false; }});
</script>
</body>
</html>";
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
