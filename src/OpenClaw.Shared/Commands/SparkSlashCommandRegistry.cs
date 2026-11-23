using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Commands;

/// <summary>
/// Extensible, central registry for all SPARK slash commands.
/// Provides command discovery, fuzzy search, argument schema validation,
/// autocomplete suggestions, and execution dispatching.
/// </summary>
public sealed class SparkSlashCommandRegistry
{
    private static readonly Lazy<SparkSlashCommandRegistry> s_instance = new(() => new SparkSlashCommandRegistry());
    public static SparkSlashCommandRegistry Instance => s_instance.Value;

    private readonly List<SparkSlashCommand> _commands = new();
    private readonly object _lock = new();

    public SparkSlashCommandRegistry()
    {
        RegisterDefaultCommands();
    }

    /// <summary>
    /// Gets all registered commands.
    /// </summary>
    public IReadOnlyList<SparkSlashCommand> GetAllCommands()
    {
        lock (_lock)
        {
            return _commands.ToList();
        }
    }

    /// <summary>
    /// Registers a custom or plugin command into the registry.
    /// </summary>
    public void Register(SparkSlashCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        lock (_lock)
        {
            _commands.RemoveAll(c => c.Id == command.Id || string.Equals(c.Name, command.Name, StringComparison.OrdinalIgnoreCase));
            _commands.Add(command);
        }
    }

    /// <summary>
    /// Finds a command by its name or alias.
    /// </summary>
    public SparkSlashCommand? FindCommand(string nameOrAlias)
    {
        if (string.IsNullOrWhiteSpace(nameOrAlias)) return null;
        lock (_lock)
        {
            return _commands.FirstOrDefault(c => c.Matches(nameOrAlias));
        }
    }

    /// <summary>
    /// Searches commands by query with ranking:
    /// 1. Exact name match
    /// 2. StartsWith name
    /// 3. Substring name or alias
    /// 4. Substring description or category
    /// 5. Fuzzy match (Levenshtein distance <= 2)
    /// </summary>
    public IReadOnlyList<SparkSlashCommand> SearchCommands(string? query, SparkCommandExecutionContext? context = null)
    {
        lock (_lock)
        {
            var candidates = context != null
                ? _commands.Where(c => c.IsAvailable(context)).ToList()
                : _commands.ToList();

            if (string.IsNullOrWhiteSpace(query))
            {
                return candidates.OrderBy(c => c.Category).ThenBy(c => c.Name).ToList();
            }

            var cleanQuery = query.Trim().TrimStart('/');
            if (cleanQuery.Length == 0)
            {
                return candidates.OrderBy(c => c.Category).ThenBy(c => c.Name).ToList();
            }

            var scored = new List<(SparkSlashCommand Command, int Score)>();

            foreach (var cmd in candidates)
            {
                var cleanName = cmd.Name.TrimStart('/');
                var score = -1;

                if (string.Equals(cleanName, cleanQuery, StringComparison.OrdinalIgnoreCase))
                {
                    score = 1000;
                }
                else if (cleanName.StartsWith(cleanQuery, StringComparison.OrdinalIgnoreCase))
                {
                    score = 800 - (cleanName.Length - cleanQuery.Length);
                }
                else if (cmd.Aliases.Any(a => a.TrimStart('/').StartsWith(cleanQuery, StringComparison.OrdinalIgnoreCase)))
                {
                    score = 700;
                }
                else if (cleanName.IndexOf(cleanQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score = 500;
                }
                else if (cmd.Description.IndexOf(cleanQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score = 300;
                }
                else if (cmd.Category.ToString().IndexOf(cleanQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score = 200;
                }
                else
                {
                    var dist = ComputeLevenshteinDistance(cleanName.ToLowerInvariant(), cleanQuery.ToLowerInvariant());
                    if (dist <= 2 && cleanQuery.Length >= 3)
                    {
                        score = 100 - dist * 20;
                    }
                }

                if (score > 0)
                {
                    scored.Add((cmd, score));
                }
            }

            return scored.OrderByDescending(s => s.Score).ThenBy(s => s.Command.Name).Select(s => s.Command).ToList();
        }
    }

    /// <summary>
    /// Suggests closest commands when an unknown command is typed.
    /// </summary>
    public IReadOnlyList<SparkSlashCommand> SuggestAlternatives(string unknownCommandToken, int maxCount = 3)
    {
        var searchResults = SearchCommands(unknownCommandToken);
        return searchResults.Take(maxCount).ToList();
    }

    /// <summary>
    /// Executes a slash command input line (e.g. "/rename New Title" or "/model gpt-4o").
    /// </summary>
    public Task<SparkCommandResult> ExecuteCommandAsync(
        string commandLine,
        SparkCommandExecutionContext context,
        CancellationToken cancellationToken = default) =>
        ExecuteLineAsync(commandLine, context, cancellationToken);

    public async Task<SparkCommandResult> ExecuteLineAsync(
        string commandLine,
        SparkCommandExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return SparkCommandResult.Failure("Empty command line.");
        }

        var trimmed = commandLine.Trim();
        if (!trimmed.StartsWith('/'))
        {
            return SparkCommandResult.Failure("Command must start with a slash (/).");
        }

        var parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var commandName = parts[0];
        var args = parts.Length > 1 ? parts[1].Trim() : null;

        var command = FindCommand(commandName);
        if (command == null)
        {
            var suggestions = SuggestAlternatives(commandName);
            var suggestionText = suggestions.Count > 0
                ? " Did you mean: " + string.Join(", ", suggestions.Select(s => s.Name)) + "?"
                : " Type /help to see all available commands.";

            return SparkCommandResult.Failure($"Unknown command '{commandName}'.{suggestionText}");
        }

        if (!command.IsAvailable(context))
        {
            return SparkCommandResult.Failure($"Command '{command.Name}' is not available in the current context.");
        }

        // Validate arguments if schema exists
        if (command.Arguments.Count > 0)
        {
            var primaryArg = command.Arguments[0];
            var (isValid, errorMessage) = primaryArg.Validate(args);
            if (!isValid)
            {
                return SparkCommandResult.Failure(errorMessage ?? $"Invalid arguments for '{command.Name}'. Usage: {command.Name} {command.BuildArgTemplate()}");
            }
        }

        // Handle destructive confirmation if requested
        if (command.RequiresConfirmation)
        {
            var prompt = command.ConfirmationPrompt ?? $"Are you sure you want to execute {command.Name}?";
            return SparkCommandResult.NeedsConfirmation(prompt, ct => command.Handler(context, args, ct));
        }

        return await command.Handler(context, args, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Projects registered SPARK slash commands into GatewayCommand models for palette and auto-complete rendering.
    /// </summary>
    public IReadOnlyList<GatewayCommand> ToGatewayCommands()
    {
        return _commands.Select(cmd => new GatewayCommand
        {
            Name = cmd.Name.TrimStart('/'),
            NativeName = cmd.Name,
            TextAliases = cmd.Aliases,
            Description = cmd.Description,
            Category = cmd.Category.ToString(),
            Source = "spark",
            Scope = "both",
            AcceptsArgs = cmd.Arguments.Count > 0,
            Args = cmd.Arguments.Select(arg => new GatewayCommandArg
            {
                Name = arg.Name,
                Description = arg.Description
            }).ToArray()
        }).ToArray();
    }

    private void RegisterDefaultCommands()
    {
        // ── Conversation Commands ──
        Register(new SparkSlashCommand
        {
            Id = "conv.new",
            Name = "/new",
            Description = "Start a new conversation session",
            Category = SparkCommandCategory.Conversation,
            KeyboardShortcut = "Ctrl+N",
            Handler = (ctx, _, _) => Task.FromResult(SparkCommandResult.Success(
                "Starting a new conversation...",
                clientAction: "CreateNewSession"))
        });

        Register(new SparkSlashCommand
        {
            Id = "conv.clear",
            Name = "/clear",
            Aliases = new[] { "/reset" },
            Description = "Clear all messages in the current conversation",
            Category = SparkCommandCategory.Conversation,
            KeyboardShortcut = "Ctrl+L",
            RequiresConfirmation = true,
            ConfirmationPrompt = "Are you sure you want to clear this conversation? All messages will be reset.",
            Handler = (ctx, _, _) => Task.FromResult(SparkCommandResult.Success(
                "Conversation cleared.",
                clientAction: "ClearSession",
                actionPayload: ctx.SessionKey))
        });

        Register(new SparkSlashCommand
        {
            Id = "conv.rename",
            Name = "/rename",
            Aliases = new[] { "/name" },
            Description = "Rename the current conversation",
            Category = SparkCommandCategory.Conversation,
            Arguments = new[]
            {
                new SparkCommandArgumentSchema
                {
                    Name = "title",
                    Description = "New title for this conversation",
                    IsRequired = true
                }
            },
            Handler = (ctx, args, _) =>
            {
                if (string.IsNullOrWhiteSpace(args))
                {
                    return Task.FromResult(SparkCommandResult.Failure("Please provide a new title. Usage: /rename <title>"));
                }
                return Task.FromResult(SparkCommandResult.Success(
                    $"Conversation renamed to '{args}'.",
                    clientAction: "RenameSession",
                    actionPayload: args));
            }
        });

        Register(new SparkSlashCommand
        {
            Id = "conv.delete",
            Name = "/delete",
            Description = "Permanently delete the current conversation",
            Category = SparkCommandCategory.Conversation,
            RequiresConfirmation = true,
            ConfirmationPrompt = "Are you sure you want to delete this conversation permanently?",
            Handler = (ctx, _, _) => Task.FromResult(SparkCommandResult.Success(
                "Conversation deleted.",
                clientAction: "DeleteSession",
                actionPayload: ctx.SessionKey))
        });

        Register(new SparkSlashCommand
        {
            Id = "conv.export",
            Name = "/export",
            Description = "Export the conversation transcript (markdown, json, or html)",
            Category = SparkCommandCategory.Conversation,
            Arguments = new[]
            {
                new SparkCommandArgumentSchema
                {
                    Name = "format",
                    Description = "Export format",
                    IsRequired = false,
                    Choices = new[]
                    {
                        new SparkCommandArgChoice("markdown", "Markdown", "Standard .md format"),
                        new SparkCommandArgChoice("json", "JSON", "Structured message array"),
                        new SparkCommandArgChoice("html", "HTML", "Styled HTML transcript")
                    }
                }
            },
            Handler = (ctx, args, _) => Task.FromResult(SparkCommandResult.Success(
                $"Exporting transcript in {args ?? "markdown"} format...",
                clientAction: "ExportSession",
                actionPayload: args ?? "markdown"))
        });

        Register(new SparkSlashCommand
        {
            Id = "conv.pin",
            Name = "/pin",
            Description = "Pin or unpin the current conversation in your history",
            Category = SparkCommandCategory.Conversation,
            Handler = (ctx, _, _) => Task.FromResult(SparkCommandResult.Success(
                "Toggling pinned state...",
                clientAction: "TogglePinSession",
                actionPayload: ctx.SessionKey))
        });

        // ── Model Commands ──
        Register(new SparkSlashCommand
        {
            Id = "model.set",
            Name = "/model",
            Description = "Show or switch the active AI model",
            Category = SparkCommandCategory.Model,
            Arguments = new[]
            {
                new SparkCommandArgumentSchema
                {
                    Name = "modelId",
                    Description = "Model ID or name to switch to",
                    IsRequired = false
                }
            },
            Handler = (ctx, args, _) =>
            {
                if (string.IsNullOrWhiteSpace(args))
                {
                    return Task.FromResult(SparkCommandResult.Success(
                        $"Current active model: {ctx.CurrentModel ?? "Default"}. Type '/model <name>' or '/models' to change.",
                        clientAction: "OpenModelPicker"));
                }
                return Task.FromResult(SparkCommandResult.Success(
                    $"Switched model to '{args}'.",
                    clientAction: "SetSessionModel",
                    actionPayload: args));
            }
        });

        Register(new SparkSlashCommand
        {
            Id = "model.list",
            Name = "/models",
            Description = "List all available AI model providers and models",
            Category = SparkCommandCategory.Model,
            Handler = (ctx, _, _) => Task.FromResult(SparkCommandResult.Success(
                "Opening model catalog...",
                clientAction: "OpenModelPicker"))
        });

        Register(new SparkSlashCommand
        {
            Id = "model.favorite",
            Name = "/favorite",
            Description = "Toggle favorite status for a model",
            Category = SparkCommandCategory.Model,
            Arguments = new[]
            {
                new SparkCommandArgumentSchema
                {
                    Name = "modelId",
                    Description = "Model to toggle as favorite",
                    IsRequired = false
                }
            },
            Handler = (ctx, args, _) =>
            {
                var targetModel = string.IsNullOrWhiteSpace(args) ? ctx.CurrentModel : args;
                if (string.IsNullOrWhiteSpace(targetModel))
                {
                    return Task.FromResult(SparkCommandResult.Failure("No model specified and no current model active."));
                }
                return Task.FromResult(SparkCommandResult.Success(
                    $"Toggled favorite for {targetModel}.",
                    clientAction: "ToggleFavoriteModel",
                    actionPayload: targetModel));
            }
        });

        // ── AI Prompts & Lifecycle Commands ──
        Register(new SparkSlashCommand
        {
            Id = "ai.summarize",
            Name = "/summarize",
            Description = "Instruct the assistant to summarize recent discussion or provided topic",
            Category = SparkCommandCategory.AI,
            Arguments = new[]
            {
                new SparkCommandArgumentSchema { Name = "topic", Description = "Optional specific topic to summarize", IsRequired = false }
            },
            Handler = (ctx, args, _) => Task.FromResult(SparkCommandResult.Success(
                clientAction: "InjectPrompt",
                actionPayload: string.IsNullOrWhiteSpace(args)
                    ? "Please provide a concise summary of our conversation so far, highlighting the key decisions, points, and action items."
                    : $"Please provide a concise summary focused on: {args}"))
        });

        Register(new SparkSlashCommand
        {
            Id = "ai.explain",
            Name = "/explain",
            Description = "Instruct the assistant to explain a concept or code snippet in detail",
            Category = SparkCommandCategory.AI,
            Arguments = new[]
            {
                new SparkCommandArgumentSchema { Name = "query", Description = "Concept, error message, or code to explain", IsRequired = true }
            },
            Handler = (ctx, args, _) => Task.FromResult(SparkCommandResult.Success(
                clientAction: "InjectPrompt",
                actionPayload: string.IsNullOrWhiteSpace(args)
                    ? "Please explain the previous message in step-by-step detail with examples."
                    : $"Please explain the following in detail with clear examples: {args}"))
        });

        Register(new SparkSlashCommand
        {
            Id = "ai.rewrite",
            Name = "/rewrite",
            Description = "Rewrite text in a specified style or tone",
            Category = SparkCommandCategory.AI,
            Arguments = new[]
            {
                new SparkCommandArgumentSchema
                {
                    Name = "style",
                    Description = "Target tone or style",
                    IsRequired = false,
                    Choices = new[]
                    {
                        new SparkCommandArgChoice("concise", "Concise", "Shorter and direct"),
                        new SparkCommandArgChoice("professional", "Professional", "Formal tone"),
                        new SparkCommandArgChoice("technical", "Technical", "Deep technical detail"),
                        new SparkCommandArgChoice("casual", "Casual", "Friendly conversational tone")
                    }
                }
            },
            Handler = (ctx, args, _) => Task.FromResult(SparkCommandResult.Success(
                clientAction: "InjectPrompt",
                actionPayload: $"Please rewrite the last response in a {args ?? "concise"} style."))
        });

        Register(new SparkSlashCommand
        {
            Id = "ai.continue",
            Name = "/continue",
            Description = "Continue generation from where the assistant stopped",
            Category = SparkCommandCategory.AI,
            Handler = (ctx, _, _) => Task.FromResult(SparkCommandResult.Success(
                clientAction: "InjectPrompt",
                actionPayload: "Please continue where you left off."))
        });

        Register(new SparkSlashCommand
        {
            Id = "ai.regenerate",
            Name = "/regenerate",
            Description = "Regenerate the latest assistant response with alternative branching",
            Category = SparkCommandCategory.AI,
            Handler = (ctx, _, _) => Task.FromResult(SparkCommandResult.Success(
                "Regenerating assistant response...",
                clientAction: "RegenerateResponse"))
        });

        Register(new SparkSlashCommand
        {
            Id = "ai.retry",
            Name = "/retry",
            Description = "Retry the last failed turn",
            Category = SparkCommandCategory.AI,
            Handler = (ctx, _, _) => Task.FromResult(SparkCommandResult.Success(
                "Retrying last turn...",
                clientAction: "RetryLastTurn"))
        });

        Register(new SparkSlashCommand
        {
            Id = "ai.stop",
            Name = "/stop",
            Description = "Stop currently active assistant generation",
            Category = SparkCommandCategory.AI,
            KeyboardShortcut = "Esc",
            Handler = (ctx, _, _) => Task.FromResult(SparkCommandResult.Success(
                "Stopping generation...",
                clientAction: "StopGeneration"))
        });

        // ── Research Commands ──
        Register(new SparkSlashCommand
        {
            Id = "research.search",
            Name = "/search",
            Description = "Search across conversation transcripts and history",
            Category = SparkCommandCategory.Research,
            Arguments = new[]
            {
                new SparkCommandArgumentSchema { Name = "query", Description = "Search terms", IsRequired = true }
            },
            Handler = (ctx, args, _) =>
            {
                if (string.IsNullOrWhiteSpace(args))
                {
                    return Task.FromResult(SparkCommandResult.Failure("Please provide a search term. Usage: /search <query>"));
                }
                return Task.FromResult(SparkCommandResult.Success(
                    $"Searching for '{args}'...",
                    clientAction: "SearchConversations",
                    actionPayload: args));
            }
        });

        Register(new SparkSlashCommand
        {
            Id = "research.sources",
            Name = "/sources",
            Description = "View citations, source references, and attachments for this conversation",
            Category = SparkCommandCategory.Research,
            Handler = (ctx, _, _) => Task.FromResult(SparkCommandResult.Success(
                "Loading reference sources...",
                clientAction: "ViewSources"))
        });

        // ── Context Commands ──
        Register(new SparkSlashCommand
        {
            Id = "context.memory",
            Name = "/memory",
            Description = "Inspect or manage memory and learned knowledge",
            Category = SparkCommandCategory.Context,
            Handler = (ctx, _, _) => Task.FromResult(SparkCommandResult.Success(
                "Viewing long-term memory context...",
                clientAction: "OpenMemoryViewer"))
        });

        Register(new SparkSlashCommand
        {
            Id = "context.info",
            Name = "/context",
            Description = "Show context window usage, token budget, and history contribution",
            Category = SparkCommandCategory.Context,
            Handler = (ctx, _, _) => Task.FromResult(SparkCommandResult.Success(
                "Displaying context window breakdown...",
                clientAction: "ShowContextBreakdown"))
        });

        Register(new SparkSlashCommand
        {
            Id = "context.compact",
            Name = "/compact",
            Description = "Safely compact conversation context to free token capacity",
            Category = SparkCommandCategory.Context,
            Arguments = new[]
            {
                new SparkCommandArgumentSchema { Name = "instructions", Description = "Optional specific compaction instructions", IsRequired = false }
            },
            Handler = (ctx, args, _) => Task.FromResult(SparkCommandResult.Success(
                "Compacting conversation context...",
                clientAction: "CompactSession",
                actionPayload: args))
        });

        // ── Files Commands ──
        Register(new SparkSlashCommand
        {
            Id = "files.attach",
            Name = "/attach",
            Description = "Attach a file, document, or image to the conversation",
            Category = SparkCommandCategory.Files,
            Handler = (ctx, _, _) => Task.FromResult(SparkCommandResult.Success(
                "Opening file attachment picker...",
                clientAction: "OpenAttachPicker"))
        });

        Register(new SparkSlashCommand
        {
            Id = "files.list",
            Name = "/files",
            Description = "List all files and attachments present in this conversation",
            Category = SparkCommandCategory.Files,
            Handler = (ctx, _, _) => Task.FromResult(SparkCommandResult.Success(
                "Listing conversation files...",
                clientAction: "ListSessionFiles"))
        });

        // ── Interface Commands ──
        Register(new SparkSlashCommand
        {
            Id = "ui.settings",
            Name = "/settings",
            Description = "Open SPARK settings",
            Category = SparkCommandCategory.Interface,
            Arguments = new[]
            {
                new SparkCommandArgumentSchema
                {
                    Name = "section",
                    Description = "Settings section",
                    IsRequired = false,
                    Choices = new[]
                    {
                        new SparkCommandArgChoice("general", "General"),
                        new SparkCommandArgChoice("models", "Models"),
                        new SparkCommandArgChoice("voice", "Voice"),
                        new SparkCommandArgChoice("capabilities", "Capabilities")
                    }
                }
            },
            Handler = (ctx, args, _) => Task.FromResult(SparkCommandResult.Success(
                "Opening settings...",
                clientAction: "OpenSettings",
                actionPayload: args ?? "general"))
        });

        Register(new SparkSlashCommand
        {
            Id = "ui.theme",
            Name = "/theme",
            Description = "Toggle or configure application theme (dark or light)",
            Category = SparkCommandCategory.Interface,
            Arguments = new[]
            {
                new SparkCommandArgumentSchema
                {
                    Name = "mode",
                    Description = "Theme mode",
                    IsRequired = false,
                    Choices = new[]
                    {
                        new SparkCommandArgChoice("dark", "Dark", "Windows 11 Dark Desktop palette"),
                        new SparkCommandArgChoice("light", "Light", "Standard light mode"),
                        new SparkCommandArgChoice("system", "System", "Match Windows system preference")
                    }
                }
            },
            Handler = (ctx, args, _) => Task.FromResult(SparkCommandResult.Success(
                $"Setting theme mode to {args ?? "dark"}...",
                clientAction: "SetTheme",
                actionPayload: args ?? "dark"))
        });

        Register(new SparkSlashCommand
        {
            Id = "ui.help",
            Name = "/help",
            Aliases = new[] { "/commands" },
            Description = "Display help and command directory",
            Category = SparkCommandCategory.Interface,
            Arguments = new[]
            {
                new SparkCommandArgumentSchema { Name = "command", Description = "Command name to inspect", IsRequired = false }
            },
            Handler = (ctx, args, _) =>
            {
                if (!string.IsNullOrWhiteSpace(args))
                {
                    var target = FindCommand(args);
                    if (target != null)
                    {
                        var argInfo = target.Arguments.Count > 0 ? " " + target.BuildArgTemplate() : "";
                        var shortcutInfo = !string.IsNullOrWhiteSpace(target.KeyboardShortcut) ? $" [Shortcut: {target.KeyboardShortcut}]" : "";
                        return Task.FromResult(SparkCommandResult.Success(
                            $"**{target.Name}{argInfo}** ({target.Category}){shortcutInfo}\n{target.Description}"));
                    }
                    return Task.FromResult(SparkCommandResult.Failure($"Unknown command '{args}'. Type /help for all commands."));
                }

                return Task.FromResult(SparkCommandResult.Success(
                    "Displaying command palette...",
                    clientAction: "OpenHelpPalette"));
            }
        });

        // ── Cybersecurity Audit Commands (Shannon Integration) ──
        Register(new SparkSlashCommand
        {
            Id = "security.audit",
            Name = "/cyber-security-audit",
            Aliases = new[] { "/audit", "/security-audit", "/pentest" },
            Description = "Launch autonomous cybersecurity audit and vulnerability assessment pipeline (powered by Shannon engine)",
            Category = SparkCommandCategory.Security,
            Arguments = new[]
            {
                new SparkCommandArgumentSchema
                {
                    Name = "target",
                    Description = "Optional target URL, endpoint, or local workspace to audit",
                    IsRequired = false
                }
            },
            Handler = (ctx, args, _) =>
            {
                var target = string.IsNullOrWhiteSpace(args) ? (ctx.WorkspacePath ?? "active project") : args.Trim();
                return Task.FromResult(SparkCommandResult.Success(
                    $"Initializing SPARK Cybersecurity Audit for target: **{target}**\nStarting pre-reconnaissance and vulnerability assessment agents...",
                    clientAction: "StartCyberSecurityAudit",
                    actionPayload: target));
            }
        });

        // ── Build Anything Commands (Craft Integration) ──
        Register(new SparkSlashCommand
        {
            Id = "coding.build",
            Name = "/build",
            Aliases = new[] { "/craft", "/create-app" },
            Description = "Create new application, site, or microservice using natural-language instructions",
            Category = SparkCommandCategory.Coding,
            Arguments = new[]
            {
                new SparkCommandArgumentSchema
                {
                    Name = "specification",
                    Description = "Natural-language description of the application to build",
                    IsRequired = false
                }
            },
            Handler = (ctx, args, _) => Task.FromResult(SparkCommandResult.Success(
                string.IsNullOrWhiteSpace(args)
                    ? "Opening Build Anything creation workspace..."
                    : $"Initializing Build Anything project generation for: {args}",
                clientAction: "StartBuildAnything",
                actionPayload: args))
        });

        // ── CLI & Terminal Intelligence Commands (CLI Anything Integration) ──
        Register(new SparkSlashCommand
        {
            Id = "coding.cli",
            Name = "/cli",
            Aliases = new[] { "/terminal", "/harness" },
            Description = "Discover, inspect, or execute CLI workflows across 100+ application harnesses",
            Category = SparkCommandCategory.Coding,
            Arguments = new[]
            {
                new SparkCommandArgumentSchema
                {
                    Name = "queryOrCommand",
                    Description = "Software harness name, natural-language request, or terminal command",
                    IsRequired = false
                }
            },
            Handler = (ctx, args, _) =>
            {
                if (string.IsNullOrWhiteSpace(args))
                {
                    return Task.FromResult(SparkCommandResult.Success(
                        $"Opening SPARK CLI Hub ({OpenClaw.Shared.Coding.CliIntelligence.SparkCliHubCatalog.Harnesses.Count} Built-in Application Harnesses & {OpenClaw.Shared.Coding.CliIntelligence.SparkCliHubCatalog.Matrices.Count} Capability Matrices)...",
                        clientAction: "OpenCliIntelligence",
                        actionPayload: null));
                }

                var clean = args.Trim();
                if (clean.Equals("list", StringComparison.OrdinalIgnoreCase) || 
                    clean.Equals("harnesses", StringComparison.OrdinalIgnoreCase) ||
                    clean.Equals("tools", StringComparison.OrdinalIgnoreCase))
                {
                    var catSummary = string.Join(", ", OpenClaw.Shared.Coding.CliIntelligence.SparkCliHubCatalog.GetCategories());
                    var matricesSummary = string.Join(", ", OpenClaw.Shared.Coding.CliIntelligence.SparkCliHubCatalog.Matrices.Select(m => m.Id));
                    return Task.FromResult(SparkCommandResult.Success(
                        $"### SPARK Built-In CLI Hub Catalog ({OpenClaw.Shared.Coding.CliIntelligence.SparkCliHubCatalog.Harnesses.Count} Harnesses)\n" +
                        $"- **Capability Matrices:** {matricesSummary}\n" +
                        $"- **Categories ({OpenClaw.Shared.Coding.CliIntelligence.SparkCliHubCatalog.GetCategories().Count}):** {catSummary}\n\n" +
                        $"*Harnesses include:* Blender, GIMP, LibreOffice, Obsidian, FreeCAD, Audacity, Godot, Suno, ElevenLabs, Kdenlive, ComfyUI, Exa, JumpServer, OpenRefine, QGIS, etc.\n" +
                        $"Type `/cli <tool-name>` to inspect a harness or use the CLI tab.",
                        clientAction: "OpenCliIntelligence",
                        actionPayload: clean));
                }

                var found = OpenClaw.Shared.Coding.CliIntelligence.SparkCliHubCatalog.FindByName(clean);
                if (found != null)
                {
                    return Task.FromResult(SparkCommandResult.Success(
                        $"### CLI Harness: **{found.DisplayName}** (`{found.EntryPoint}`)\n" +
                        $"- **Category:** {found.Category}\n" +
                        $"- **Description:** {found.Description}\n" +
                        $"- **Prerequisites:** {found.Requires}\n" +
                        (!string.IsNullOrEmpty(found.InstallCmd) ? $"- **Install:** `{found.InstallCmd}`\n" : "") +
                        (!string.IsNullOrEmpty(found.Homepage) ? $"- **Homepage:** {found.Homepage}\n" : ""),
                        clientAction: "OpenCliIntelligence",
                        actionPayload: $"{found.EntryPoint} --help"));
                }

                return Task.FromResult(SparkCommandResult.Success(
                    $"Routing CLI workflow: {args}",
                    clientAction: "OpenCliIntelligence",
                    actionPayload: args));
            }
        });

        // ── Reverse Engineering Commands (Rea Integration) ──
        Register(new SparkSlashCommand
        {
            Id = "coding.reverse_engineer",
            Name = "/reverse-engineer",
            Aliases = new[] { "/rea", "/decompile", "/inspect-binary" },
            Description = "Inspect binary, .NET assembly, web runtime, ASAR, or APK to extract architecture and evidence",
            Category = SparkCommandCategory.Coding,
            Arguments = new[]
            {
                new SparkCommandArgumentSchema
                {
                    Name = "targetPath",
                    Description = "Path to binary, package, or application to inspect",
                    IsRequired = false
                }
            },
            Handler = (ctx, args, _) => Task.FromResult(SparkCommandResult.Success(
                string.IsNullOrWhiteSpace(args)
                    ? "Opening Reverse Engineering & Evidence workspace..."
                    : $"Inspecting reverse engineering target: {args}",
                clientAction: "StartReverseEngineering",
                actionPayload: args))
        });
    }

    private static int ComputeLevenshteinDistance(string s, string t)
    {
        var n = s.Length;
        var m = t.Length;
        var d = new int[n + 1, m + 1];

        if (n == 0) return m;
        if (m == 0) return n;

        for (var i = 0; i <= n; d[i, 0] = i++) { }
        for (var j = 0; j <= m; d[0, j] = j++) { }

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }
}
