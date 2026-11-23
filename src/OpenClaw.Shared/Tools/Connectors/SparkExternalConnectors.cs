using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Tools.Connectors;

/// <summary>
/// Connector for Google Workspace services.
/// Status is explicitly Planned as live production OAuth credentials are required.
/// </summary>
public class GoogleWorkspaceConnector : ISparkExternalConnector
{
    public string ConnectorId => "google_workspace";
    public string DisplayName => "Google Workspace (Docs, Sheets, Drive)";
    public string Description => "Connector for Google Docs, Sheets, and Drive via Google Cloud REST APIs. Requires OAuth2 client configuration.";
    public bool IsImplemented => false; // Explicitly Planned
    public SparkConnectorStatus Status => SparkConnectorStatus.Planned;

    public Task<IReadOnlyList<SparkToolMetadata>> GetProvidedToolsAsync(CancellationToken cancellationToken = default)
    {
        var plannedTools = new List<SparkToolMetadata>
        {
            new()
            {
                Id = "gworkspace_docs_read",
                Name = "Google Docs: Read Document",
                Description = "[Planned] Retrieves the text content of a Google Doc by document ID.",
                Category = SparkToolCategory.ExternalConnector,
                RiskLevel = SparkToolRiskLevel.Low,
                IsEnabled = false
            },
            new()
            {
                Id = "gworkspace_sheets_append",
                Name = "Google Sheets: Append Rows",
                Description = "[Planned] Appends rows to a target Google Sheet spreadsheet.",
                Category = SparkToolCategory.ExternalConnector,
                RiskLevel = SparkToolRiskLevel.High,
                IsEnabled = false
            }
        };

        return Task.FromResult<IReadOnlyList<SparkToolMetadata>>(plannedTools);
    }

    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        // Not configured with live OAuth credentials yet
        return Task.FromResult(false);
    }
}

/// <summary>
/// Connector for Calendar services (Google Calendar / Microsoft 365).
/// Status is explicitly Planned.
/// </summary>
public class CalendarConnector : ISparkExternalConnector
{
    public string ConnectorId => "calendar_integration";
    public string DisplayName => "Calendar Services (Google / M365)";
    public string Description => "Integration with calendar event scheduling and agenda queries. Requires OAuth2 credentials.";
    public bool IsImplemented => false; // Explicitly Planned
    public SparkConnectorStatus Status => SparkConnectorStatus.Planned;

    public Task<IReadOnlyList<SparkToolMetadata>> GetProvidedToolsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<SparkToolMetadata>>(new List<SparkToolMetadata>
        {
            new()
            {
                Id = "calendar_list_events",
                Name = "Calendar: List Events",
                Description = "[Planned] Queries upcoming calendar events within a specified time range.",
                Category = SparkToolCategory.ExternalConnector,
                RiskLevel = SparkToolRiskLevel.Low,
                IsEnabled = false
            }
        });
    }

    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
}

/// <summary>
/// Connector for Email services (IMAP/SMTP or MS Graph).
/// Status is explicitly Planned.
/// </summary>
public class EmailConnector : ISparkExternalConnector
{
    public string ConnectorId => "email_service";
    public string DisplayName => "Email Service (IMAP / Graph)";
    public string Description => "Integration with mailbox query and message drafting. Requires mail server credentials.";
    public bool IsImplemented => false; // Explicitly Planned
    public SparkConnectorStatus Status => SparkConnectorStatus.Planned;

    public Task<IReadOnlyList<SparkToolMetadata>> GetProvidedToolsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<SparkToolMetadata>>(new List<SparkToolMetadata>
        {
            new()
            {
                Id = "email_check_inbox",
                Name = "Email: Check Inbox",
                Description = "[Planned] Queries recent unread email messages.",
                Category = SparkToolCategory.ExternalConnector,
                RiskLevel = SparkToolRiskLevel.Low,
                IsEnabled = false
            }
        });
    }

    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
}

/// <summary>
/// Local Productivity Notes Connector.
/// Fully implemented and connected to persistent user notes store.
/// </summary>
public class LocalProductivityNotesConnector : ISparkExternalConnector
{
    private readonly string _notesDirectory;

    public LocalProductivityNotesConnector(string? storageRoot = null)
    {
        _notesDirectory = storageRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenClaw",
            "Notes");

        if (!Directory.Exists(_notesDirectory))
        {
            Directory.CreateDirectory(_notesDirectory);
        }
    }

    public string ConnectorId => "productivity_notes";
    public string DisplayName => "Local Productivity Notes";
    public string Description => "Integrated persistent markdown notes store for project logs, scratchpads, and action items.";
    public bool IsImplemented => true; // Fully Implemented
    public SparkConnectorStatus Status => SparkConnectorStatus.Connected;

    public Task<IReadOnlyList<SparkToolMetadata>> GetProvidedToolsAsync(CancellationToken cancellationToken = default)
    {
        var tools = new List<SparkToolMetadata>
        {
            new()
            {
                Id = "notes_create",
                Name = "Create Note",
                Description = "Creates a persistent productivity note with title, tags, and markdown content.",
                Category = SparkToolCategory.ExternalConnector,
                RiskLevel = SparkToolRiskLevel.High,
                RequiredPermissions = new List<string> { "notes:write" },
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"title\":{\"type\":\"string\"},\"content\":{\"type\":\"string\"},\"tags\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}}},\"required\":[\"title\",\"content\"]}"
            },
            new()
            {
                Id = "notes_list",
                Name = "List Notes",
                Description = "Lists all stored productivity notes with title, tags, and last modified date.",
                Category = SparkToolCategory.ExternalConnector,
                RiskLevel = SparkToolRiskLevel.Low,
                RequiredPermissions = new List<string> { "notes:read" },
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{}}"
            },
            new()
            {
                Id = "notes_search",
                Name = "Search Notes",
                Description = "Searches note titles and content for keywords.",
                Category = SparkToolCategory.ExternalConnector,
                RiskLevel = SparkToolRiskLevel.Low,
                RequiredPermissions = new List<string> { "notes:read" },
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"keyword\":{\"type\":\"string\"}},\"required\":[\"keyword\"]}"
            }
        };

        return Task.FromResult<IReadOnlyList<SparkToolMetadata>>(tools);
    }

    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Directory.Exists(_notesDirectory));
    }

    public async Task<string> CreateNoteAsync(string title, string content, List<string>? tags, CancellationToken ct = default)
    {
        var safeTitle = string.Join("_", title.Split(Path.GetInvalidFileNameChars())).Trim();
        if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = "Untitled_" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var filePath = Path.Combine(_notesDirectory, $"{safeTitle}.json");
        var note = new
        {
            title = title,
            content = content,
            tags = tags ?? new List<string>(),
            createdAt = DateTimeOffset.UtcNow,
            updatedAt = DateTimeOffset.UtcNow
        };

        var json = JsonSerializer.Serialize(note, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json, ct).ConfigureAwait(false);
        return filePath;
    }

    public async Task<IReadOnlyList<object>> ListNotesAsync(CancellationToken ct = default)
    {
        var files = Directory.GetFiles(_notesDirectory, "*.json");
        var list = new List<object>();

        foreach (var file in files)
        {
            try
            {
                var text = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                list.Add(new
                {
                    fileName = Path.GetFileName(file),
                    title = root.GetProperty("title").GetString(),
                    tags = root.TryGetProperty("tags", out var t) ? t.EnumerateArray().Select(x => x.GetString()).ToList() : new List<string?>(),
                    updatedAt = root.GetProperty("updatedAt").GetDateTimeOffset()
                });
            }
            catch
            {
                // Ignore corrupted note files
            }
        }

        return list;
    }

    public async Task<IReadOnlyList<object>> SearchNotesAsync(string keyword, CancellationToken ct = default)
    {
        var allNotes = Directory.GetFiles(_notesDirectory, "*.json");
        var matches = new List<object>();

        foreach (var file in allNotes)
        {
            try
            {
                var text = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    using var doc = JsonDocument.Parse(text);
                    var root = doc.RootElement;
                    matches.Add(new
                    {
                        fileName = Path.GetFileName(file),
                        title = root.GetProperty("title").GetString(),
                        content = root.GetProperty("content").GetString(),
                        updatedAt = root.GetProperty("updatedAt").GetDateTimeOffset()
                    });
                }
            }
            catch
            {
                // Ignore read errors
            }
        }

        return matches;
    }
}

/// <summary>
/// Tool exposing LocalProductivityNotesConnector to the tool registry.
/// </summary>
public class SparkNotesTool : ISparkTool
{
    private readonly LocalProductivityNotesConnector _connector;

    public SparkNotesTool(LocalProductivityNotesConnector connector, string subAction = "create")
    {
        _connector = connector;
        SubAction = subAction.ToLowerInvariant();

        Metadata = SubAction switch
        {
            "list" => new SparkToolMetadata
            {
                Id = "notes_list",
                Name = "List Notes",
                Description = "Lists all stored productivity notes.",
                Category = SparkToolCategory.ExternalConnector,
                RiskLevel = SparkToolRiskLevel.Low,
                RequiredPermissions = new List<string> { "notes:read" },
                InputSchemaJson = "{}"
            },
            "search" => new SparkToolMetadata
            {
                Id = "notes_search",
                Name = "Search Notes",
                Description = "Searches notes for a specific keyword.",
                Category = SparkToolCategory.ExternalConnector,
                RiskLevel = SparkToolRiskLevel.Low,
                RequiredPermissions = new List<string> { "notes:read" },
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"keyword\":{\"type\":\"string\"}},\"required\":[\"keyword\"]}"
            },
            _ => new SparkToolMetadata
            {
                Id = "notes_create",
                Name = "Create Note",
                Description = "Creates a persistent productivity note.",
                Category = SparkToolCategory.ExternalConnector,
                RiskLevel = SparkToolRiskLevel.High,
                RequiredPermissions = new List<string> { "notes:write" },
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"title\":{\"type\":\"string\"},\"content\":{\"type\":\"string\"}},\"required\":[\"title\",\"content\"]}"
            }
        };
    }

    public string SubAction { get; }
    public SparkToolMetadata Metadata { get; }

    public async Task<SparkToolExecutionResponse> ExecuteAsync(
        SparkToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        switch (SubAction)
        {
            case "list":
            {
                var notes = await _connector.ListNotesAsync(cancellationToken).ConfigureAwait(false);
                return SparkToolExecutionResponse.Ok(Metadata.Id, notes, Metadata.RiskLevel, TimeSpan.Zero);
            }
            case "search":
            {
                var kw = request.GetArgument<string>("keyword");
                if (string.IsNullOrWhiteSpace(kw))
                {
                    return SparkToolExecutionResponse.Fail(Metadata.Id, "Keyword argument is required.", Metadata.RiskLevel, TimeSpan.Zero);
                }
                var results = await _connector.SearchNotesAsync(kw, cancellationToken).ConfigureAwait(false);
                return SparkToolExecutionResponse.Ok(Metadata.Id, results, Metadata.RiskLevel, TimeSpan.Zero);
            }
            default:
            {
                var title = request.GetArgument<string>("title");
                var content = request.GetArgument<string>("content") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(title))
                {
                    return SparkToolExecutionResponse.Fail(Metadata.Id, "Title argument is required.", Metadata.RiskLevel, TimeSpan.Zero);
                }
                var tags = request.GetArgument<List<string>>("tags");
                var path = await _connector.CreateNoteAsync(title, content, tags, cancellationToken).ConfigureAwait(false);
                return SparkToolExecutionResponse.Ok(Metadata.Id, new { path = path, created = true }, Metadata.RiskLevel, TimeSpan.Zero);
            }
        }
    }
}
