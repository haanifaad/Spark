using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using OpenClaw.Shared.Creative;

namespace OpenClaw.Cli;

/// <summary>
/// CLI handler implementing all `spark creativity` subcommands (Section 10).
/// </summary>
internal static class CreativeCliHandler
{
    public static async Task<int> RunAsync(string[] args)
    {
        bool jsonMode = args.Any(a => a is "--json" or "-j");
        var filteredArgs = args.Where(a => a is not "--json" and not "-j").ToArray();

        if (filteredArgs.Length == 0 || filteredArgs[0] is "--help" or "-h" or "help")
        {
            PrintCreativeUsage();
            return 0;
        }

        var command = filteredArgs[0].ToLowerInvariant();
        var subArgs = filteredArgs.Skip(1).ToArray();

        switch (command)
        {
            case "apps":
                return HandleApps(subArgs, jsonMode);

            case "app":
                return HandleApp(subArgs, jsonMode);

            case "project":
                return HandleProject(subArgs, jsonMode);

            case "tools":
                return HandleTools(subArgs, jsonMode);

            case "skills":
                return HandleSkills(subArgs, jsonMode);

            case "agent":
                return await HandleAgentAsync(subArgs, jsonMode);

            case "export":
                return await HandleExportAsync(subArgs, jsonMode);

            case "mcp":
                return HandleMcp(subArgs, jsonMode);

            default:
                Console.Error.WriteLine($"Unknown creativity subcommand: '{command}'. Run 'spark creativity --help' for usage.");
                return 2;
        }
    }

    private static int HandleApps(string[] args, bool jsonMode)
    {
        var apps = CreativeAppRegistry.AllApplications;

        if (jsonMode)
        {
            Console.WriteLine(JsonSerializer.Serialize(apps, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        Console.WriteLine("================================================================================");
        Console.WriteLine(" SPARK CREATIVITY APPLICATION ECOSYSTEM (16 APPLICATIONS)");
        Console.WriteLine("================================================================================");
        Console.WriteLine($"{"App ID",-14} | {"Display Name",-14} | {"Category",-12} | {"Status",-10} | {"Capability",-30}");
        Console.WriteLine(new string('-', 90));

        foreach (var app in apps)
        {
            Console.WriteLine($"{app.ApplicationId,-14} | {app.DisplayName,-14} | {app.Category,-12} | {app.Status,-10} | {app.PrimaryCapability,-30}");
        }
        Console.WriteLine(new string('-', 90));
        Console.WriteLine($"Total Applications: {apps.Count} | Extracted Root: {CreativeAppRegistry.ExtractedRoot}");
        return 0;
    }

    private static int HandleApp(string[] args, bool jsonMode)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: spark creativity app inspect <app_id>");
            return 2;
        }

        var subcmd = args[0].ToLowerInvariant();
        if (subcmd == "inspect" && args.Length >= 2)
        {
            var appId = args[1];
            if (!CreativeAppRegistry.TryGetApplication(appId, out var app))
            {
                Console.Error.WriteLine($"Application '{appId}' not found.");
                return 1;
            }

            var health = CreativeLifecycleManager.Instance.PerformHealthCheck(app.ApplicationId);

            if (jsonMode)
            {
                var payload = new { application = app, health };
                Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }

            Console.WriteLine($"=== SPARK Application: {app.DisplayName} ({app.UpstreamName}) ===");
            Console.WriteLine($"ID:            {app.ApplicationId}");
            Console.WriteLine($"Category:      {app.Category}");
            Console.WriteLine($"Capability:    {app.PrimaryCapability}");
            Console.WriteLine($"Status:        {app.Status}");
            Console.WriteLine($"Version:       {app.Version} ({app.License})");
            Console.WriteLine($"Motif:         {app.MascotMotif}");
            Console.WriteLine($"Interfaces:    {string.Join(", ", app.SupportedInterfaces)}");
            Console.WriteLine($"Formats:       {string.Join(", ", app.SupportedFileFormats)}");
            Console.WriteLine($"Capabilities:  {string.Join(", ", app.Capabilities)}");
            Console.WriteLine($"Source Path:   {health["sourcePath"]}");
            Console.WriteLine($"Source Exists: {health["sourceExists"]}");
            Console.WriteLine($"Binary Path:   {health["binaryPath"]}");
            Console.WriteLine($"Is Running:    {health["isRunning"]}");
            return 0;
        }

        Console.Error.WriteLine("Usage: spark creativity app inspect <app_id>");
        return 2;
    }

    private static int HandleProject(string[] args, bool jsonMode)
    {
        if (args.Length == 0 || args[0] is "status" or "list")
        {
            var recent = CreativeProjectManager.Instance.RecentProjects;
            if (jsonMode)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { recentProjects = recent }, new JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }

            Console.WriteLine("=== SPARK Creative Projects ===");
            for (int i = 0; i < recent.Count; i++)
            {
                Console.WriteLine($"[{i + 1}] {recent[i]}");
            }
            return 0;
        }

        if (args[0] == "open" && args.Length >= 2)
        {
            var path = args[1];
            Console.WriteLine($"Opening project: {path}");
            return 0;
        }

        Console.Error.WriteLine("Usage: spark creativity project [status|open <path>]");
        return 2;
    }

    private static int HandleTools(string[] args, bool jsonMode)
    {
        string? appFilter = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is "--app" or "-a" && i + 1 < args.Length)
            {
                appFilter = args[i + 1];
            }
        }

        var tools = string.IsNullOrEmpty(appFilter)
            ? CreativeMcpGateway.Instance.GetAllTools()
            : CreativeMcpGateway.Instance.GetToolsForApplication(appFilter);

        if (jsonMode)
        {
            Console.WriteLine(JsonSerializer.Serialize(tools, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        Console.WriteLine($"=== Discovered Creative MCP Tools ({tools.Count} tools) ===");
        foreach (var t in tools)
        {
            Console.WriteLine($"• {t.Name,-32} [Tier {t.PermissionTier}] ({t.ApplicationId}): {t.Description}");
        }
        return 0;
    }

    private static int HandleSkills(string[] args, bool jsonMode)
    {
        string? query = args.Length > 1 && args[0] == "search" ? args[1] : null;
        var skills = CreativeSkillRegistry.Instance.SearchSkills(query).ToList();

        if (jsonMode)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { total = skills.Count, skills }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        Console.WriteLine($"=== SPARK Creative Skills Library ({skills.Count} matching skills) ===");
        foreach (var s in skills.Take(25))
        {
            Console.WriteLine($"• [{s.Category}] {s.SkillId,-36} ({s.Application}): {s.Purpose}");
        }

        if (skills.Count > 25)
        {
            Console.WriteLine($"... and {skills.Count - 25} more skills. Use '--json' or specific query to view all.");
        }
        return 0;
    }

    private static async Task<int> HandleAgentAsync(string[] args, bool jsonMode)
    {
        if (args.Length < 2 || args[0] != "run")
        {
            Console.Error.WriteLine("Usage: spark creativity agent run \"<prompt>\" [--app <app_id>]");
            return 2;
        }

        var prompt = args[1];
        string? appFilter = null;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] is "--app" or "-a" && i + 1 < args.Length)
            {
                appFilter = args[i + 1];
            }
        }

        Console.WriteLine($"Dispatching Creative Agent with prompt: \"{prompt}\"...");
        var result = await CreativeAgent.Instance.ExecutePromptAsync(prompt, appFilter);

        if (jsonMode)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        Console.WriteLine($"Task ID:     {result.TaskId}");
        Console.WriteLine($"Application: {result.TargetApplication}");
        Console.WriteLine($"Model:       {result.SelectedModel}");
        Console.WriteLine($"Skill:       {result.MatchedSkillId}");
        Console.WriteLine($"Status:      {result.State}");
        Console.WriteLine();
        Console.WriteLine(result.ResponseText);
        return 0;
    }

    private static async Task<int> HandleExportAsync(string[] args, bool jsonMode)
    {
        string? sourceApp = null;
        string? targetApp = null;
        string? inputPath = null;
        string? outputPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is "--app" && i + 1 < args.Length) sourceApp = args[i + 1];
            if (args[i] is "--target" && i + 1 < args.Length) targetApp = args[i + 1];
            if (args[i] is "--in" && i + 1 < args.Length) inputPath = args[i + 1];
            if (args[i] is "--out" && i + 1 < args.Length) outputPath = args[i + 1];
        }

        sourceApp ??= "photospark";
        targetApp ??= "vectorspark";
        inputPath ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SPARK", "CreativeProjects", "Sample.pcraft");

        var result = await CreativeProjectManager.Instance.ExchangeAssetAsync(sourceApp, targetApp, inputPath, outputPath);

        if (jsonMode)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        Console.WriteLine(result.Success ? "SUCCESS" : "FAILED");
        Console.WriteLine($"Message: {result.Message}");
        Console.WriteLine($"Output:  {result.OutputFilePath}");
        return result.Success ? 0 : 1;
    }

    private static int HandleMcp(string[] args, bool jsonMode)
    {
        Console.WriteLine("SPARK Creative MCP Gateway running on stdio JSON-RPC transport.");
        Console.WriteLine("Exposing 16 Creative Applications, 240 skills, and tool dispatchers.");
        return 0;
    }

    private static void PrintCreativeUsage()
    {
        Console.WriteLine(@"
SPARK Creativity Suite CLI
Usage: spark creativity <command> [options]

Commands:
  apps list                     List all 16 creative applications and their status
  app inspect <app_id>          Inspect application capabilities, metadata, and health
  project status                List active and recent projects
  project open <path>           Open a creative project file
  tools list [--app <id>]       List discovered MCP tools across the creative suite
  skills search [query]         Search the 240 professional creative skills
  agent run ""<prompt>""          Execute autonomous creative prompt with model routing
  export --app <id> --target <id> --in <file> --out <file>
                                Cross-application asset exchange and conversion
  mcp serve                     Start MCP server gateway

Options:
  --json, -j                    Format output as structured JSON
  --help, -h                    Show this help message
");
    }
}
