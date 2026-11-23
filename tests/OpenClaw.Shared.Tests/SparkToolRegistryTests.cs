using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Tools;
using OpenClaw.Shared.Tools.Browser;
using OpenClaw.Shared.Tools.Connectors;
using OpenClaw.Shared.Tools.Development;
using OpenClaw.Shared.Tools.Filesystem;
using OpenClaw.Shared.Tools.Mcp;
using Xunit;

namespace OpenClaw.Shared.Tests;

public class SparkToolRegistryTests : IDisposable
{
    private readonly string _testWorkspace;

    public SparkToolRegistryTests()
    {
        _testWorkspace = Path.Combine(Path.GetTempPath(), "SparkToolsTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testWorkspace);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testWorkspace))
            {
                Directory.Delete(_testWorkspace, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public void Registry_RegisterAndListTools_FiltersByCategoryAndRisk()
    {
        var registry = new SparkToolRegistry();
        var boundary = new SparkFilesystemBoundary(new[] { _testWorkspace });

        registry.RegisterTool(new SparkReadFileTool(boundary));
        registry.RegisterTool(new SparkWriteFileTool(boundary));
        registry.RegisterTool(new SparkDeleteFileTool(boundary));
        registry.RegisterTool(new SparkGitStatusTool());

        var allTools = registry.ListTools();
        Assert.Equal(4, allTools.Count);

        var fsTools = registry.ListTools(category: SparkToolCategory.Filesystem);
        Assert.Equal(3, fsTools.Count);

        var devTools = registry.ListTools(category: SparkToolCategory.Development);
        Assert.Single(devTools);

        var lowRiskTools = registry.ListTools(maxRiskLevel: SparkToolRiskLevel.Low);
        Assert.Equal(2, lowRiskTools.Count); // fs_read_file and dev_git_status
    }

    [Fact]
    public async Task Registry_EnforcesRequiredPermissions()
    {
        var registry = new SparkToolRegistry();
        var boundary = new SparkFilesystemBoundary(new[] { _testWorkspace });
        registry.RegisterTool(new SparkWriteFileTool(boundary));

        var testFile = Path.Combine(_testWorkspace, "test.txt");

        // Request WITHOUT required permission
        var reqDenied = new SparkToolExecutionRequest
        {
            ToolId = "fs_write_file",
            Arguments = new Dictionary<string, object?> { ["path"] = testFile, ["content"] = "hello" }
        };

        var respDenied = await registry.ExecuteToolAsync(reqDenied);
        Assert.False(respDenied.Success);
        Assert.Contains("Execution denied", respDenied.Error);

        // Request WITH required permission
        var reqAllowed = new SparkToolExecutionRequest
        {
            ToolId = "fs_write_file",
            Arguments = new Dictionary<string, object?> { ["path"] = testFile, ["content"] = "hello" },
            GrantedPermissions = new HashSet<string> { "filesystem:write" }
        };

        var respAllowed = await registry.ExecuteToolAsync(reqAllowed);
        Assert.True(respAllowed.Success);
        Assert.True(File.Exists(testFile));
        Assert.Equal("hello", await File.ReadAllTextAsync(testFile));
    }

    [Fact]
    public async Task Registry_DryRun_ReturnsPreviewWithoutStateAlteration()
    {
        var registry = new SparkToolRegistry();
        var boundary = new SparkFilesystemBoundary(new[] { _testWorkspace });
        registry.RegisterTool(new SparkWriteFileTool(boundary));

        var testFile = Path.Combine(_testWorkspace, "dryrun.txt");

        var req = new SparkToolExecutionRequest
        {
            ToolId = "fs_write_file",
            Arguments = new Dictionary<string, object?> { ["path"] = testFile, ["content"] = "dry run content" },
            GrantedPermissions = new HashSet<string> { "filesystem:write" },
            DryRun = true
        };

        var resp = await registry.ExecuteToolAsync(req);
        Assert.True(resp.Success);
        Assert.False(File.Exists(testFile)); // File was NOT created
    }

    [Fact]
    public async Task FilesystemTools_ReadWriteEditListDelete_WorkWithinBoundary()
    {
        var registry = new SparkToolRegistry();
        var boundary = new SparkFilesystemBoundary(new[] { _testWorkspace });

        var readTool = new SparkReadFileTool(boundary);
        var writeTool = new SparkWriteFileTool(boundary);
        var editTool = new SparkEditFileTool(boundary);
        var listTool = new SparkListDirectoryTool(boundary);
        var deleteTool = new SparkDeleteFileTool(boundary);

        registry.RegisterTool(readTool);
        registry.RegisterTool(writeTool);
        registry.RegisterTool(editTool);
        registry.RegisterTool(listTool);
        registry.RegisterTool(deleteTool);

        var filePath = Path.Combine(_testWorkspace, "doc.txt");

        // 1. Write file
        var writeReq = new SparkToolExecutionRequest
        {
            ToolId = "fs_write_file",
            Arguments = new Dictionary<string, object?> { ["path"] = filePath, ["content"] = "Line 1\nHello World\nLine 3" },
            GrantedPermissions = new HashSet<string> { "filesystem:write" }
        };
        var writeRes = await registry.ExecuteToolAsync(writeReq);
        Assert.True(writeRes.Success);

        // 2. Read file with line range
        var readReq = new SparkToolExecutionRequest
        {
            ToolId = "fs_read_file",
            Arguments = new Dictionary<string, object?> { ["path"] = filePath, ["startLine"] = 2, ["endLine"] = 2 },
            GrantedPermissions = new HashSet<string> { "filesystem:read" }
        };
        var readRes = await registry.ExecuteToolAsync(readReq);
        Assert.True(readRes.Success);

        // 3. Edit file (replace Hello World with Hello SPARK)
        var editReq = new SparkToolExecutionRequest
        {
            ToolId = "fs_edit_file",
            Arguments = new Dictionary<string, object?> { ["path"] = filePath, ["target"] = "Hello World", ["replacement"] = "Hello SPARK" },
            GrantedPermissions = new HashSet<string> { "filesystem:write" }
        };
        var editRes = await registry.ExecuteToolAsync(editReq);
        Assert.True(editRes.Success);

        var contentAfterEdit = await File.ReadAllTextAsync(filePath);
        Assert.Contains("Hello SPARK", contentAfterEdit);

        // 4. List directory
        var listReq = new SparkToolExecutionRequest
        {
            ToolId = "fs_list_directory",
            Arguments = new Dictionary<string, object?> { ["path"] = _testWorkspace },
            GrantedPermissions = new HashSet<string> { "filesystem:read" }
        };
        var listRes = await registry.ExecuteToolAsync(listReq);
        Assert.True(listRes.Success);

        // 5. Delete file
        var deleteReq = new SparkToolExecutionRequest
        {
            ToolId = "fs_delete_file",
            Arguments = new Dictionary<string, object?> { ["path"] = filePath },
            GrantedPermissions = new HashSet<string> { "filesystem:delete" }
        };
        var deleteRes = await registry.ExecuteToolAsync(deleteReq);
        Assert.True(deleteRes.Success);
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public void FilesystemBoundary_RejectsPathTraversalAndEscape()
    {
        var boundary = new SparkFilesystemBoundary(new[] { _testWorkspace });

        // Escape via parent directory
        var escapePath = Path.Combine(_testWorkspace, "..", "outside.txt");
        Assert.Throws<UnauthorizedAccessException>(() => boundary.ValidateAndNormalize(escapePath));

        // Escape into Windows directory
        var winPath = @"C:\Windows\System32\cmd.exe";
        Assert.Throws<UnauthorizedAccessException>(() => boundary.ValidateAndNormalize(winPath));
    }

    [Fact]
    public async Task McpManager_ServerRegistrationAndToolDiscoveryMapping()
    {
        var registry = new SparkToolRegistry();
        var mcpManager = new SparkMcpManager(registry);

        var config = new SparkMcpServerConfig
        {
            ServerId = "server_test",
            ServerName = "Test Server",
            EndpointUrl = "http://localhost:9999/mcp",
            IsEnabled = false // disabled so it doesn't attempt network call in unit test
        };

        var added = await mcpManager.AddServerAsync(config);
        Assert.True(added);

        var servers = await mcpManager.GetServersAsync();
        Assert.Single(servers);
        Assert.Equal("server_test", servers[0].ServerId);

        // Manual registration of MCP proxy tool
        var toolMeta = new SparkToolMetadata
        {
            Id = "mcp_server_test_sample",
            Name = "sample",
            Category = SparkToolCategory.Mcp,
            McpServerId = "server_test"
        };
        registry.RegisterTool(new SparkMcpToolProxy(config, toolMeta));

        Assert.Single(registry.ListTools(SparkToolCategory.Mcp));

        // Removing server unregisters its tools
        await mcpManager.RemoveServerAsync("server_test");
        Assert.Empty(registry.ListTools(SparkToolCategory.Mcp));
    }

    [Fact]
    public async Task ExternalConnectors_ClearlyDistinguishesImplementedFromPlanned()
    {
        var gWorkspace = new GoogleWorkspaceConnector();
        Assert.False(gWorkspace.IsImplemented);
        Assert.Equal(SparkConnectorStatus.Planned, gWorkspace.Status);

        var email = new EmailConnector();
        Assert.False(email.IsImplemented);
        Assert.Equal(SparkConnectorStatus.Planned, email.Status);

        var notesDir = Path.Combine(_testWorkspace, "Notes");
        var notesConnector = new LocalProductivityNotesConnector(notesDir);
        Assert.True(notesConnector.IsImplemented);
        Assert.Equal(SparkConnectorStatus.Connected, notesConnector.Status);

        // Test notes operations
        var notePath = await notesConnector.CreateNoteAsync("Meeting Notes", "Discussed SPARK architecture.", new List<string> { "spark", "architecture" });
        Assert.True(File.Exists(notePath));

        var notesList = await notesConnector.ListNotesAsync();
        Assert.Single(notesList);

        var searchResults = await notesConnector.SearchNotesAsync("architecture");
        Assert.Single(searchResults);
    }
}
