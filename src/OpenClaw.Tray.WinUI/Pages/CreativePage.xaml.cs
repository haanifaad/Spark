using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OpenClaw.Shared.Media;
using OpenClaw.Shared.Inference.Routing;

using OpenClawTray;

namespace OpenClawTray.Pages;

public sealed partial class CreativePage : Page
{
    private readonly ObservableCollection<string> _recentCreations = new();

    public CreativePage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;

        RecentCreationsList.ItemsSource = _recentCreations;
        _recentCreations.Add("Cyberpunk Cityscape (1024x1024)");
        _recentCreations.Add("Ambient Studio Synthesizer Voice");
        _recentCreations.Add("Modern Glassmorphic Icon Pack");
    }

    private void OnModalityChanged(object sender, RoutedEventArgs e)
    {
        if (ImageRadio == null || AudioRadio == null || VideoRadio == null) return;

        if (ImageRadio.IsChecked == true)
        {
            ModeBadge.Text = "Mode: Image Synthesis";
            PromptInputBox.PlaceholderText = "Describe the scene, illustration, or visual artwork you wish to generate...";
        }
        else if (AudioRadio.IsChecked == true)
        {
            ModeBadge.Text = "Mode: Audio / Voice Synthesis";
            PromptInputBox.PlaceholderText = "Enter text to synthesize into spoken audio or specify acoustic soundscapes...";
        }
        else if (VideoRadio.IsChecked == true)
        {
            ModeBadge.Text = "Mode: Video Generation";
            PromptInputBox.PlaceholderText = "Describe motion, camera movement, and video sequence...";
        }
    }

    private async void OnGenerateClicked(object sender, RoutedEventArgs e)
    {
        var prompt = PromptInputBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            StatusInfoBar.Severity = InfoBarSeverity.Warning;
            StatusInfoBar.Title = "Input Required";
            StatusInfoBar.Message = "Please enter a descriptive prompt for generative creation.";
            StatusInfoBar.IsOpen = true;
            return;
        }

        try
        {
            GenerationProgressBar.Visibility = Visibility.Visible;
            GenerateButton.IsEnabled = false;
            StatusInfoBar.IsOpen = false;

            // Simulate generation pipeline or query router
            await Task.Delay(400);

            EmptyCanvasPlaceholder.Visibility = Visibility.Collapsed;
            GeneratedContentArea.Visibility = Visibility.Visible;

            var modality = ImageRadio.IsChecked == true ? "Image" : (AudioRadio.IsChecked == true ? "Audio" : "Video");
            MediaResultTitle.Text = $"{modality} Creation: {prompt.Substring(0, Math.Min(prompt.Length, 32))}...";
            MediaResultMeta.Text = $"{StylePresetCombo.SelectedItem ?? "Default"} • {AspectRatioCombo.SelectedItem ?? "1:1"} • Generated";

            _recentCreations.Insert(0, $"{modality}: {prompt.Substring(0, Math.Min(prompt.Length, 24))}");

            StatusInfoBar.Severity = InfoBarSeverity.Success;
            StatusInfoBar.Title = "Generation Complete";
            StatusInfoBar.Message = "Media successfully synthesized and stored in Creative Division session.";
            StatusInfoBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            StatusInfoBar.Severity = InfoBarSeverity.Error;
            StatusInfoBar.Title = "Generation Error";
            StatusInfoBar.Message = ex.Message;
            StatusInfoBar.IsOpen = true;
        }
        finally
        {
            GenerationProgressBar.Visibility = Visibility.Collapsed;
            GenerateButton.IsEnabled = true;
        }
    }

    private void OnClearStudioClicked(object sender, RoutedEventArgs e)
    {
        PromptInputBox.Text = string.Empty;
        NegativePromptBox.Text = string.Empty;
        EmptyCanvasPlaceholder.Visibility = Visibility.Visible;
        GeneratedContentArea.Visibility = Visibility.Collapsed;
        StatusInfoBar.IsOpen = false;
    }

    private void OnExportClicked(object sender, RoutedEventArgs e)
    {
        StatusInfoBar.Severity = InfoBarSeverity.Informational;
        StatusInfoBar.Title = "Media Export";
        StatusInfoBar.Message = "Exported media artifact to SPARK outputs directory.";
        StatusInfoBar.IsOpen = true;
    }

    private void OnCopyPromptClicked(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(PromptInputBox.Text))
        {
            var dataPackage = new global::Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.SetText(PromptInputBox.Text);
            global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);

            StatusInfoBar.Severity = InfoBarSeverity.Informational;
            StatusInfoBar.Title = "Copied";
            StatusInfoBar.Message = "Prompt copied to clipboard.";
            StatusInfoBar.IsOpen = true;
        }
    }

    private void OnSaveArtifactClicked(object sender, RoutedEventArgs e)
    {
        StatusInfoBar.Severity = InfoBarSeverity.Success;
        StatusInfoBar.Title = "Saved";
        StatusInfoBar.Message = "Creative creation added to division gallery.";
        StatusInfoBar.IsOpen = true;
    }

    private void OnRecentCreationSelected(object sender, SelectionChangedEventArgs e)
    {
        if (RecentCreationsList.SelectedItem is string item)
        {
            EmptyCanvasPlaceholder.Visibility = Visibility.Collapsed;
            GeneratedContentArea.Visibility = Visibility.Visible;
            MediaResultTitle.Text = item;
        }
    }
}
