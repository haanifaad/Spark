using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Tools.Filesystem;

/// <summary>
/// Enforces filesystem boundaries and prevents path traversal escapes.
/// </summary>
public class SparkFilesystemBoundary
{
    private readonly List<string> _allowedRoots = new();

    public SparkFilesystemBoundary(IEnumerable<string>? allowedRoots = null)
    {
        if (allowedRoots != null)
        {
            foreach (var r in allowedRoots)
            {
                if (!string.IsNullOrWhiteSpace(r))
                {
                    _allowedRoots.Add(Path.GetFullPath(r));
                }
            }
        }

        // If no explicit roots provided, default to current working directory
        if (_allowedRoots.Count == 0)
        {
            _allowedRoots.Add(Path.GetFullPath(Directory.GetCurrentDirectory()));
        }
    }

    public void AddAllowedRoot(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            _allowedRoots.Add(Path.GetFullPath(path));
        }
    }

    public string ValidateAndNormalize(string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            throw new ArgumentException("File or directory path cannot be empty.", nameof(targetPath));
        }

        var fullPath = Path.GetFullPath(targetPath);

        // Disallow dangerous Windows system roots
        var sysRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrEmpty(sysRoot) && fullPath.StartsWith(sysRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"Access to Windows system path '{fullPath}' is strictly forbidden.");
        }

        bool withinAllowedRoot = _allowedRoots.Any(r =>
            fullPath.StartsWith(r.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fullPath, r, StringComparison.OrdinalIgnoreCase));

        if (!withinAllowedRoot)
        {
            throw new UnauthorizedAccessException($"Path '{fullPath}' escapes authorized workspace boundaries.");
        }

        return fullPath;
    }
}

/// <summary>
/// Tool for safe file reading within boundaries.
/// </summary>
public class SparkReadFileTool : ISparkTool
{
    private readonly SparkFilesystemBoundary _boundary;
    private const int MaxAllowedBytes = 10 * 1024 * 1024; // 10MB limit

    public SparkReadFileTool(SparkFilesystemBoundary boundary)
    {
        _boundary = boundary;
        Metadata = new SparkToolMetadata
        {
            Id = "fs_read_file",
            Name = "Read File",
            Description = "Reads the content of a file within authorized filesystem boundaries.",
            Category = SparkToolCategory.Filesystem,
            RiskLevel = SparkToolRiskLevel.Low,
            RequiredPermissions = new List<string> { "filesystem:read" },
            DefaultTimeout = TimeSpan.FromSeconds(15),
            InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"startLine\":{\"type\":\"integer\"},\"endLine\":{\"type\":\"integer\"}},\"required\":[\"path\"]}"
        };
    }

    public SparkToolMetadata Metadata { get; }

    public async Task<SparkToolExecutionResponse> ExecuteAsync(
        SparkToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var rawPath = request.GetArgument<string>("path");
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return SparkToolExecutionResponse.Fail(Metadata.Id, "Path argument is required.", Metadata.RiskLevel, TimeSpan.Zero);
        }

        string fullPath;
        try
        {
            fullPath = _boundary.ValidateAndNormalize(rawPath);
        }
        catch (Exception ex)
        {
            return SparkToolExecutionResponse.Fail(Metadata.Id, ex.Message, Metadata.RiskLevel, TimeSpan.Zero);
        }

        if (!File.Exists(fullPath))
        {
            return SparkToolExecutionResponse.Fail(Metadata.Id, $"File does not exist: {fullPath}", Metadata.RiskLevel, TimeSpan.Zero);
        }

        var fileInfo = new FileInfo(fullPath);
        if (fileInfo.Length > MaxAllowedBytes)
        {
            return SparkToolExecutionResponse.Fail(Metadata.Id, $"File exceeds maximum allowed size limit ({fileInfo.Length} > {MaxAllowedBytes} bytes).", Metadata.RiskLevel, TimeSpan.Zero);
        }

        var startLine = request.GetArgument<int?>("startLine");
        var endLine = request.GetArgument<int?>("endLine");

        var lines = await File.ReadAllLinesAsync(fullPath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);

        if (startLine.HasValue || endLine.HasValue)
        {
            int s = Math.Max(1, startLine ?? 1);
            int e = Math.Min(lines.Length, endLine ?? lines.Length);
            if (s > lines.Length)
            {
                return SparkToolExecutionResponse.Ok(Metadata.Id, new { content = string.Empty, totalLines = lines.Length }, Metadata.RiskLevel, TimeSpan.Zero);
            }

            var slice = lines.Skip(s - 1).Take(e - s + 1);
            return SparkToolExecutionResponse.Ok(Metadata.Id, new
            {
                path = fullPath,
                startLine = s,
                endLine = e,
                totalLines = lines.Length,
                content = string.Join(Environment.NewLine, slice)
            }, Metadata.RiskLevel, TimeSpan.Zero);
        }

        var fullContent = string.Join(Environment.NewLine, lines);
        return SparkToolExecutionResponse.Ok(Metadata.Id, new
        {
            path = fullPath,
            totalLines = lines.Length,
            content = fullContent
        }, Metadata.RiskLevel, TimeSpan.Zero);
    }
}

/// <summary>
/// Tool for safe file creation or overwrite within boundaries.
/// </summary>
public class SparkWriteFileTool : ISparkTool
{
    private readonly SparkFilesystemBoundary _boundary;

    public SparkWriteFileTool(SparkFilesystemBoundary boundary)
    {
        _boundary = boundary;
        Metadata = new SparkToolMetadata
        {
            Id = "fs_write_file",
            Name = "Write File",
            Description = "Creates or overwrites a text file within authorized filesystem boundaries.",
            Category = SparkToolCategory.Filesystem,
            RiskLevel = SparkToolRiskLevel.High,
            RequiredPermissions = new List<string> { "filesystem:write" },
            DefaultTimeout = TimeSpan.FromSeconds(30),
            InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"content\":{\"type\":\"string\"},\"append\":{\"type\":\"boolean\"}},\"required\":[\"path\",\"content\"]}"
        };
    }

    public SparkToolMetadata Metadata { get; }

    public async Task<SparkToolExecutionResponse> ExecuteAsync(
        SparkToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var rawPath = request.GetArgument<string>("path");
        var content = request.GetArgument<string>("content") ?? string.Empty;
        var append = request.GetArgument<bool>("append", false);

        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return SparkToolExecutionResponse.Fail(Metadata.Id, "Path argument is required.", Metadata.RiskLevel, TimeSpan.Zero);
        }

        string fullPath;
        try
        {
            fullPath = _boundary.ValidateAndNormalize(rawPath);
        }
        catch (Exception ex)
        {
            return SparkToolExecutionResponse.Fail(Metadata.Id, ex.Message, Metadata.RiskLevel, TimeSpan.Zero);
        }

        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (append)
        {
            await File.AppendAllTextAsync(fullPath, content, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await File.WriteAllTextAsync(fullPath, content, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        }

        return SparkToolExecutionResponse.Ok(Metadata.Id, new
        {
            path = fullPath,
            bytesWritten = Encoding.UTF8.GetByteCount(content),
            appended = append
        }, Metadata.RiskLevel, TimeSpan.Zero);
    }
}

/// <summary>
/// Tool for exact string replacement file editing.
/// </summary>
public class SparkEditFileTool : ISparkTool
{
    private readonly SparkFilesystemBoundary _boundary;

    public SparkEditFileTool(SparkFilesystemBoundary boundary)
    {
        _boundary = boundary;
        Metadata = new SparkToolMetadata
        {
            Id = "fs_edit_file",
            Name = "Edit File",
            Description = "Performs an exact target string replacement in an existing file.",
            Category = SparkToolCategory.Filesystem,
            RiskLevel = SparkToolRiskLevel.High,
            RequiredPermissions = new List<string> { "filesystem:write" },
            DefaultTimeout = TimeSpan.FromSeconds(30),
            InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"target\":{\"type\":\"string\"},\"replacement\":{\"type\":\"string\"}},\"required\":[\"path\",\"target\",\"replacement\"]}"
        };
    }

    public SparkToolMetadata Metadata { get; }

    public async Task<SparkToolExecutionResponse> ExecuteAsync(
        SparkToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var rawPath = request.GetArgument<string>("path");
        var target = request.GetArgument<string>("target");
        var replacement = request.GetArgument<string>("replacement") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return SparkToolExecutionResponse.Fail(Metadata.Id, "Path argument is required.", Metadata.RiskLevel, TimeSpan.Zero);
        }

        if (string.IsNullOrEmpty(target))
        {
            return SparkToolExecutionResponse.Fail(Metadata.Id, "Target search string cannot be null or empty.", Metadata.RiskLevel, TimeSpan.Zero);
        }

        string fullPath;
        try
        {
            fullPath = _boundary.ValidateAndNormalize(rawPath);
        }
        catch (Exception ex)
        {
            return SparkToolExecutionResponse.Fail(Metadata.Id, ex.Message, Metadata.RiskLevel, TimeSpan.Zero);
        }

        if (!File.Exists(fullPath))
        {
            return SparkToolExecutionResponse.Fail(Metadata.Id, $"File does not exist: {fullPath}", Metadata.RiskLevel, TimeSpan.Zero);
        }

        var existingText = await File.ReadAllTextAsync(fullPath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        if (!existingText.Contains(target, StringComparison.Ordinal))
        {
            return SparkToolExecutionResponse.Fail(Metadata.Id, "Target string was not found in the specified file.", Metadata.RiskLevel, TimeSpan.Zero);
        }

        var occurrences = (existingText.Length - existingText.Replace(target, string.Empty).Length) / target.Length;
        var updatedText = existingText.Replace(target, replacement);

        await File.WriteAllTextAsync(fullPath, updatedText, Encoding.UTF8, cancellationToken).ConfigureAwait(false);

        return SparkToolExecutionResponse.Ok(Metadata.Id, new
        {
            path = fullPath,
            occurrencesReplaced = occurrences
        }, Metadata.RiskLevel, TimeSpan.Zero);
    }
}

/// <summary>
/// Tool for inspecting directories and listing files.
/// </summary>
public class SparkListDirectoryTool : ISparkTool
{
    private readonly SparkFilesystemBoundary _boundary;

    public SparkListDirectoryTool(SparkFilesystemBoundary boundary)
    {
        _boundary = boundary;
        Metadata = new SparkToolMetadata
        {
            Id = "fs_list_directory",
            Name = "List Directory",
            Description = "Lists directory contents, subdirectories, and file metadata.",
            Category = SparkToolCategory.Filesystem,
            RiskLevel = SparkToolRiskLevel.Low,
            RequiredPermissions = new List<string> { "filesystem:read" },
            DefaultTimeout = TimeSpan.FromSeconds(15),
            InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"recursive\":{\"type\":\"boolean\"}},\"required\":[\"path\"]}"
        };
    }

    public SparkToolMetadata Metadata { get; }

    public Task<SparkToolExecutionResponse> ExecuteAsync(
        SparkToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var rawPath = request.GetArgument<string>("path");
        var recursive = request.GetArgument<bool>("recursive", false);

        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return Task.FromResult(SparkToolExecutionResponse.Fail(Metadata.Id, "Path argument is required.", Metadata.RiskLevel, TimeSpan.Zero));
        }

        string fullPath;
        try
        {
            fullPath = _boundary.ValidateAndNormalize(rawPath);
        }
        catch (Exception ex)
        {
            return Task.FromResult(SparkToolExecutionResponse.Fail(Metadata.Id, ex.Message, Metadata.RiskLevel, TimeSpan.Zero));
        }

        if (!Directory.Exists(fullPath))
        {
            return Task.FromResult(SparkToolExecutionResponse.Fail(Metadata.Id, $"Directory does not exist: {fullPath}", Metadata.RiskLevel, TimeSpan.Zero));
        }

        var opt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var dirInfo = new DirectoryInfo(fullPath);

        var subdirs = dirInfo.GetDirectories("*", opt).Select(d => new
        {
            name = d.Name,
            fullName = d.FullName,
            isDir = true,
            lastModified = d.LastWriteTimeUtc
        }).ToList();

        var files = dirInfo.GetFiles("*", opt).Select(f => new
        {
            name = f.Name,
            fullName = f.FullName,
            isDir = false,
            sizeBytes = f.Length,
            lastModified = f.LastWriteTimeUtc
        }).ToList();

        return Task.FromResult(SparkToolExecutionResponse.Ok(Metadata.Id, new
        {
            directory = fullPath,
            directoryCount = subdirs.Count,
            fileCount = files.Count,
            directories = subdirs,
            files = files
        }, Metadata.RiskLevel, TimeSpan.Zero));
    }
}

/// <summary>
/// Tool for controlled file deletion (Critical Risk).
/// </summary>
public class SparkDeleteFileTool : ISparkTool
{
    private readonly SparkFilesystemBoundary _boundary;

    public SparkDeleteFileTool(SparkFilesystemBoundary boundary)
    {
        _boundary = boundary;
        Metadata = new SparkToolMetadata
        {
            Id = "fs_delete_file",
            Name = "Delete File",
            Description = "Permanently deletes a file within authorized filesystem boundaries.",
            Category = SparkToolCategory.Filesystem,
            RiskLevel = SparkToolRiskLevel.Critical,
            RequiredPermissions = new List<string> { "filesystem:delete" },
            DefaultTimeout = TimeSpan.FromSeconds(15),
            InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"}},\"required\":[\"path\"]}"
        };
    }

    public SparkToolMetadata Metadata { get; }

    public Task<SparkToolExecutionResponse> ExecuteAsync(
        SparkToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var rawPath = request.GetArgument<string>("path");
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return Task.FromResult(SparkToolExecutionResponse.Fail(Metadata.Id, "Path argument is required.", Metadata.RiskLevel, TimeSpan.Zero));
        }

        string fullPath;
        try
        {
            fullPath = _boundary.ValidateAndNormalize(rawPath);
        }
        catch (Exception ex)
        {
            return Task.FromResult(SparkToolExecutionResponse.Fail(Metadata.Id, ex.Message, Metadata.RiskLevel, TimeSpan.Zero));
        }

        if (!File.Exists(fullPath))
        {
            return Task.FromResult(SparkToolExecutionResponse.Fail(Metadata.Id, $"File not found: {fullPath}", Metadata.RiskLevel, TimeSpan.Zero));
        }

        File.Delete(fullPath);
        return Task.FromResult(SparkToolExecutionResponse.Ok(Metadata.Id, new
        {
            path = fullPath,
            deleted = true
        }, Metadata.RiskLevel, TimeSpan.Zero));
    }
}
