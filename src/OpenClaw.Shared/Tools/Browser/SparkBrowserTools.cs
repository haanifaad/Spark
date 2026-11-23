using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Tools.Filesystem;

namespace OpenClaw.Shared.Tools.Browser;

/// <summary>
/// Tool for navigating to and retrieving web pages.
/// </summary>
public class SparkBrowserNavigateTool : ISparkTool
{
    private readonly HttpClient _httpClient;

    public SparkBrowserNavigateTool(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        Metadata = new SparkToolMetadata
        {
            Id = "browser_navigate",
            Name = "Browser Navigate",
            Description = "Navigates to a web URL, fetches content, and extracts basic page metadata.",
            Category = SparkToolCategory.Browser,
            RiskLevel = SparkToolRiskLevel.Medium,
            RequiredPermissions = new List<string> { "network:outbound" },
            DefaultTimeout = TimeSpan.FromSeconds(30),
            InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}"
        };
    }

    public SparkToolMetadata Metadata { get; }

    public async Task<SparkToolExecutionResponse> ExecuteAsync(
        SparkToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var rawUrl = request.GetArgument<string>("url");
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return SparkToolExecutionResponse.Fail(Metadata.Id, "URL argument is required.", Metadata.RiskLevel, TimeSpan.Zero);
        }

        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return SparkToolExecutionResponse.Fail(Metadata.Id, "Only absolute HTTP and HTTPS URLs are allowed.", Metadata.RiskLevel, TimeSpan.Zero);
        }

        try
        {
            using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            var titleMatch = Regex.Match(content, @"<title>\s*(.*?)\s*</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var title = titleMatch.Success ? titleMatch.Groups[1].Value : string.Empty;

            // Strip HTML tags for clean text preview
            var textOnly = Regex.Replace(content, "<[^>]+>", " ");
            textOnly = Regex.Replace(textOnly, @"\s+", " ").Trim();
            var textSnippet = textOnly.Length > 2000 ? textOnly.Substring(0, 2000) + "..." : textOnly;

            return SparkToolExecutionResponse.Ok(Metadata.Id, new
            {
                url = uri.ToString(),
                statusCode = (int)response.StatusCode,
                isSuccess = response.IsSuccessStatusCode,
                title = title,
                contentLength = content.Length,
                textSnippet = textSnippet
            }, Metadata.RiskLevel, TimeSpan.Zero);
        }
        catch (Exception ex)
        {
            return SparkToolExecutionResponse.Fail(Metadata.Id, $"Navigation failed: {ex.Message}", Metadata.RiskLevel, TimeSpan.Zero);
        }
    }
}

/// <summary>
/// Tool for performing internet search queries.
/// </summary>
public class SparkBrowserSearchTool : ISparkTool
{
    private readonly HttpClient _httpClient;

    public SparkBrowserSearchTool(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        Metadata = new SparkToolMetadata
        {
            Id = "browser_search",
            Name = "Browser Search",
            Description = "Executes a web search query and returns structured result links and snippets.",
            Category = SparkToolCategory.Browser,
            RiskLevel = SparkToolRiskLevel.Medium,
            RequiredPermissions = new List<string> { "network:outbound" },
            DefaultTimeout = TimeSpan.FromSeconds(30),
            InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"},\"limit\":{\"type\":\"integer\"}},\"required\":[\"query\"]}"
        };
    }

    public SparkToolMetadata Metadata { get; }

    public async Task<SparkToolExecutionResponse> ExecuteAsync(
        SparkToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = request.GetArgument<string>("query");
        var limit = request.GetArgument<int?>("limit") ?? 5;

        if (string.IsNullOrWhiteSpace(query))
        {
            return SparkToolExecutionResponse.Fail(Metadata.Id, "Search query is required.", Metadata.RiskLevel, TimeSpan.Zero);
        }

        // Use DuckDuckGo HTML endpoint for search
        var searchUrl = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";

        try
        {
            using var reqMsg = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            reqMsg.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            using var resp = await _httpClient.SendAsync(reqMsg, cancellationToken).ConfigureAwait(false);
            var html = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            var results = new List<object>();
            var matches = Regex.Matches(html, @"<a class=""result__url"" href=""([^""]+)"">.*?</a>.*?<a class=""result__snippet[^""]*""[^>]*>(.*?)</a>", RegexOptions.Singleline);

            foreach (Match m in matches)
            {
                if (results.Count >= limit) break;
                var rawLink = m.Groups[1].Value.Trim();
                var snippet = Regex.Replace(m.Groups[2].Value, "<[^>]+>", " ").Trim();
                results.Add(new
                {
                    url = rawLink,
                    snippet = snippet
                });
            }

            return SparkToolExecutionResponse.Ok(Metadata.Id, new
            {
                query = query,
                totalResults = results.Count,
                results = results
            }, Metadata.RiskLevel, TimeSpan.Zero);
        }
        catch (Exception ex)
        {
            return SparkToolExecutionResponse.Fail(Metadata.Id, $"Search request failed: {ex.Message}", Metadata.RiskLevel, TimeSpan.Zero);
        }
    }
}

/// <summary>
/// Tool for downloading remote web assets into authorized workspace boundaries.
/// </summary>
public class SparkBrowserDownloadTool : ISparkTool
{
    private readonly HttpClient _httpClient;
    private readonly SparkFilesystemBoundary _boundary;
    private const long MaxDownloadBytes = 50 * 1024 * 1024; // 50MB max

    public SparkBrowserDownloadTool(SparkFilesystemBoundary boundary, HttpClient? httpClient = null)
    {
        _boundary = boundary;
        _httpClient = httpClient ?? new HttpClient();
        Metadata = new SparkToolMetadata
        {
            Id = "browser_download",
            Name = "Browser Download",
            Description = "Downloads a file from a URL to a local destination within authorized boundaries.",
            Category = SparkToolCategory.Browser,
            RiskLevel = SparkToolRiskLevel.High,
            RequiredPermissions = new List<string> { "network:outbound", "filesystem:write" },
            DefaultTimeout = TimeSpan.FromSeconds(60),
            InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"},\"destinationPath\":{\"type\":\"string\"}},\"required\":[\"url\",\"destinationPath\"]}"
        };
    }

    public SparkToolMetadata Metadata { get; }

    public async Task<SparkToolExecutionResponse> ExecuteAsync(
        SparkToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var rawUrl = request.GetArgument<string>("url");
        var destPath = request.GetArgument<string>("destinationPath");

        if (string.IsNullOrWhiteSpace(rawUrl) || string.IsNullOrWhiteSpace(destPath))
        {
            return SparkToolExecutionResponse.Fail(Metadata.Id, "Both URL and destinationPath arguments are required.", Metadata.RiskLevel, TimeSpan.Zero);
        }

        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return SparkToolExecutionResponse.Fail(Metadata.Id, "Only absolute HTTP and HTTPS URLs are allowed.", Metadata.RiskLevel, TimeSpan.Zero);
        }

        string fullDest;
        try
        {
            fullDest = _boundary.ValidateAndNormalize(destPath);
        }
        catch (Exception ex)
        {
            return SparkToolExecutionResponse.Fail(Metadata.Id, ex.Message, Metadata.RiskLevel, TimeSpan.Zero);
        }

        try
        {
            using var resp = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                return SparkToolExecutionResponse.Fail(Metadata.Id, $"Download failed with HTTP status {(int)resp.StatusCode} {resp.ReasonPhrase}.", Metadata.RiskLevel, TimeSpan.Zero);
            }

            if (resp.Content.Headers.ContentLength.HasValue && resp.Content.Headers.ContentLength.Value > MaxDownloadBytes)
            {
                return SparkToolExecutionResponse.Fail(Metadata.Id, $"File size exceeds limit ({resp.Content.Headers.ContentLength.Value} > {MaxDownloadBytes} bytes).", Metadata.RiskLevel, TimeSpan.Zero);
            }

            var dir = Path.GetDirectoryName(fullDest);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await using (var remoteStream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var localFile = File.Create(fullDest))
            {
                await remoteStream.CopyToAsync(localFile, cancellationToken).ConfigureAwait(false);
            }

            var fileInfo = new FileInfo(fullDest);
            return SparkToolExecutionResponse.Ok(Metadata.Id, new
            {
                url = uri.ToString(),
                destinationPath = fullDest,
                bytesDownloaded = fileInfo.Length,
                contentType = resp.Content.Headers.ContentType?.ToString()
            }, Metadata.RiskLevel, TimeSpan.Zero);
        }
        catch (Exception ex)
        {
            return SparkToolExecutionResponse.Fail(Metadata.Id, $"Download error: {ex.Message}", Metadata.RiskLevel, TimeSpan.Zero);
        }
    }
}
