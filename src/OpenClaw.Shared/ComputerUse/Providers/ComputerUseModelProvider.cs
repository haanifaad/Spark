using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.ComputerUse.Types;
using OpenClaw.Shared.Inference.Routing;

namespace OpenClaw.Shared.ComputerUse.Providers;

/// <summary>
/// Model integration provider bridging SPARK's intelligence router with Vision-Language Models for Computer Use.
/// Supports OpenAI-compatible endpoints, local Ollama vision models, and custom routing chains.
/// </summary>
public sealed class ComputerUseModelProvider : IComputerUseModelProvider, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ISparkModelRouter? _modelRouter;
    private readonly IOpenClawLogger? _logger;
    private readonly bool _ownsHttpClient;

    public const string DefaultSystemPrompt =
@"You are a GUI agent. You are given a task and your action history, with screenshots. You need to perform the next action to complete the task.

## Output Format
```
Thought: ...
Action: ...
```

## Action Space
click(start_box='[x1, y1, x2, y2]')
left_double(start_box='[x1, y1, x2, y2]')
right_single(start_box='[x1, y1, x2, y2]')
middle_click(start_box='[x1, y1, x2, y2]')
drag(start_box='[x1, y1, x2, y2]', end_box='[x3, y3, x4, y4]')
mouse_move(start_box='[x1, y1, x2, y2]')
hotkey(key='')
type(content='') # If you want to submit your input, use \n at the end of content.
press(key='')
release(key='')
scroll(start_box='[x1, y1, x2, y2]', direction='down or up or right or left')
wait() # Sleep and check for UI changes.
finished() # Declare task completed.
call_user() # Ask user for assistance if unsolvable or requires human input.

## Note
- Coordinates [x1, y1, x2, y2] are normalized on a [0..1000] scale.
- Write a small plan and summarize your next action in one sentence in Thought part.
";

    public ComputerUseModelProvider(
        ISparkModelRouter? modelRouter = null,
        HttpClient? httpClient = null,
        IOpenClawLogger? logger = null)
    {
        _modelRouter = modelRouter;
        _logger = logger;
        if (httpClient != null)
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }
        else
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            _ownsHttpClient = true;
        }
    }

    public async Task<ComputerUseModelResponse> GenerateActionAsync(
        ComputerUseModelRequest request,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // 1. Resolve target model through SPARK Router if available
        SparkModelDescriptor? targetModel = request.ModelDescriptor;
        if (targetModel == null && _modelRouter != null)
        {
            var routeDecision = _modelRouter.Route(new SparkRoutingRequest
            {
                TaskCategory = SparkTaskCategory.ImageUnderstanding,
                PrivacyMode = SparkModelPrivacyMode.PreferLocal,
                UserManualModelOverride = request.PreferredModelId
            });
            targetModel = routeDecision.SelectedModel;
        }

        string modelId = targetModel?.ModelId ?? request.PreferredModelId ?? "qwen2.5-vl";
        var provider = targetModel?.Provider ?? SparkModelProvider.Local;

        try
        {
            // 2. Dispatch based on provider type
            if (provider == SparkModelProvider.Ollama)
            {
                return await InvokeOllamaVisionAsync(request, modelId, sw, ct).ConfigureAwait(false);
            }
            else
            {
                // Default to OpenAI-compatible VLM endpoint
                return await InvokeOpenAiCompatibleAsync(request, modelId, sw, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            return ComputerUseModelResponse.Fail("Model invocation timed out or was cancelled by user.", modelId);
        }
        catch (Exception ex)
        {
            _logger?.Error($"[ComputerUseModelProvider] Invocation failed: {ex.Message}", ex);
            return ComputerUseModelResponse.Fail($"VLM provider error: {ex.Message}", modelId);
        }
    }

    private async Task<ComputerUseModelResponse> InvokeOpenAiCompatibleAsync(
        ComputerUseModelRequest request,
        string modelId,
        Stopwatch sw,
        CancellationToken ct)
    {
        // Check endpoint configuration from environment or defaults
        string endpoint = Environment.GetEnvironmentVariable("SPARK_VLM_ENDPOINT")
            ?? Environment.GetEnvironmentVariable("OPENAI_BASE_URL")
            ?? "https://api.openai.com/v1/chat/completions";

        if (!endpoint.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            endpoint = endpoint.TrimEnd('/') + "/chat/completions";
        }

        string? apiKey = Environment.GetEnvironmentVariable("SPARK_VLM_KEY")
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        if (!string.IsNullOrEmpty(apiKey))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        // Build messages payload
        var messages = new List<object>
        {
            new { role = "system", content = DefaultSystemPrompt }
        };

        // Add history summary
        if (request.HistorySteps.Count > 0)
        {
            var historySb = new StringBuilder();
            historySb.AppendLine("Previous Actions Executed:");
            foreach (var step in request.HistorySteps)
            {
                historySb.AppendLine($"Step {step.StepIndex}: Thought: {step.Thought} -> Action: {step.Action} (Result: {step.ExecutionOutput?.Message})");
            }
            messages.Add(new { role = "user", content = historySb.ToString() });
        }

        // Add current observation with screenshot
        var userContentList = new List<object>
        {
            new { type = "text", text = $"Task: {request.Goal}\nActive Window: {request.ActiveWindowTitle ?? "Unknown"}\nWhat is the next action?" }
        };

        if (!string.IsNullOrEmpty(request.CurrentScreenshotBase64))
        {
            userContentList.Add(new
            {
                type = "image_url",
                image_url = new
                {
                    url = $"data:image/png;base64,{request.CurrentScreenshotBase64}",
                    detail = "high"
                }
            });
        }

        messages.Add(new { role = "user", content = userContentList });

        var body = new
        {
            model = modelId,
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            messages
        };

        string jsonPayload = JsonSerializer.Serialize(body);
        httpRequest.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest, ct).ConfigureAwait(false);
        string responseString = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return ComputerUseModelResponse.Fail($"VLM API returned status {response.StatusCode}: {responseString}", modelId);
        }

        using var doc = JsonDocument.Parse(responseString);
        var root = doc.RootElement;
        string completionText = string.Empty;
        int tokens = 0;

        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var firstChoice = choices[0];
            if (firstChoice.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var cnt))
            {
                completionText = cnt.GetString() ?? string.Empty;
            }
        }

        if (root.TryGetProperty("usage", out var usage) && usage.TryGetProperty("total_tokens", out var tt))
        {
            tokens = tt.GetInt32();
        }

        sw.Stop();
        return ComputerUseModelResponse.Ok(completionText, modelId, tokens, sw.ElapsedMilliseconds);
    }

    private async Task<ComputerUseModelResponse> InvokeOllamaVisionAsync(
        ComputerUseModelRequest request,
        string modelId,
        Stopwatch sw,
        CancellationToken ct)
    {
        string endpoint = Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT") ?? "http://127.0.0.1:11434/api/chat";

        var messages = new List<object>
        {
            new { role = "system", content = DefaultSystemPrompt }
        };

        var images = new List<string>();
        if (!string.IsNullOrEmpty(request.CurrentScreenshotBase64))
        {
            images.Add(request.CurrentScreenshotBase64);
        }

        messages.Add(new
        {
            role = "user",
            content = $"Task: {request.Goal}\nActive Window: {request.ActiveWindowTitle ?? "Unknown"}\nWhat is the next action?",
            images = images.Count > 0 ? images : null
        });

        var body = new
        {
            model = modelId,
            messages,
            stream = false,
            options = new { temperature = request.Temperature }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest, ct).ConfigureAwait(false);
        string responseString = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return ComputerUseModelResponse.Fail($"Ollama returned status {response.StatusCode}: {responseString}", modelId);
        }

        using var doc = JsonDocument.Parse(responseString);
        var root = doc.RootElement;
        string text = string.Empty;

        if (root.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var contentProp))
        {
            text = contentProp.GetString() ?? string.Empty;
        }

        sw.Stop();
        return ComputerUseModelResponse.Ok(text, modelId, 0, sw.ElapsedMilliseconds);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
