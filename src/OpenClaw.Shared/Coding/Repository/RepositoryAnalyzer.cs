using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Coding.Models;

namespace OpenClaw.Shared.Coding.Repository;

/// <summary>
/// Analyzes a workspace directory to extract languages, frameworks, entrypoints,
/// package configurations, architecture overview, and git metadata.
/// </summary>
public sealed class RepositoryAnalyzer
{
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".vscode", ".idea", "bin", "obj", "node_modules",
        "dist", "build", "out", "target", "venv", ".venv", "env",
        "__pycache__", ".next", ".nuxt", "coverage", ".artifacts",
        "vendor", "packages", "AppPackages"
    };

    private static readonly HashSet<string> SourceCodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".ts", ".tsx", ".js", ".jsx", ".py", ".rs", ".go",
        ".cpp", ".c", ".h", ".hpp", ".java", ".kt", ".dart", ".swift",
        ".rb", ".php", ".sh", ".ps1", ".sql", ".xaml", ".json", ".xml",
        ".yaml", ".yml", ".md", ".toml"
    };

    /// <summary>
    /// Analyzes the specified project directory.
    /// </summary>
    public async Task<RepositoryAnalysis> AnalyzeAsync(string projectRoot, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        if (!Directory.Exists(projectRoot))
        {
            throw new DirectoryNotFoundException($"Project directory not found: {projectRoot}");
        }

        var rootInfo = new DirectoryInfo(projectRoot);
        var projectName = rootInfo.Name;

        var languageCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var detectedFrameworks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entryPoints = new List<string>();
        var packageConfigs = new List<string>();
        var instructionFiles = new List<string>();
        var importantDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int totalSourceFiles = 0;

        // Traverse project structure up to depth 5
        void ScanDirectory(DirectoryInfo dir, int depth)
        {
            if (depth > 5 || ct.IsCancellationRequested) return;

            try
            {
                foreach (var file in dir.EnumerateFiles())
                {
                    if (ct.IsCancellationRequested) return;

                    var name = file.Name;
                    var ext = file.Extension;

                    // Instructions & documentation
                    if (name.Equals("README.md", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("CLAUDE.md", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("AGENTS.md", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("DESIGN.md", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("CONTRIBUTING.md", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals(".editorconfig", StringComparison.OrdinalIgnoreCase))
                    {
                        var rel = Path.GetRelativePath(projectRoot, file.FullName);
                        instructionFiles.Add(rel);
                    }

                    // Package / Build configs
                    if (name.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("package.json", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("tsconfig.json", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("requirements.txt", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("Cargo.toml", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("go.mod", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("pom.xml", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("build.gradle", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("Makefile", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("CMakeLists.txt", StringComparison.OrdinalIgnoreCase))
                    {
                        var rel = Path.GetRelativePath(projectRoot, file.FullName);
                        packageConfigs.Add(rel);
                    }

                    // Detect entry points
                    if (name is "Program.cs" or "App.xaml.cs" or "main.ts" or "index.ts" or "main.js" or "index.js" or
                        "main.py" or "app.py" or "main.go" or "main.rs" or "Program.fs" or "App.tsx")
                    {
                        var rel = Path.GetRelativePath(projectRoot, file.FullName);
                        entryPoints.Add(rel);
                    }

                    // Source code tallying
                    if (SourceCodeExtensions.Contains(ext))
                    {
                        totalSourceFiles++;
                        var lang = GetLanguageName(ext);
                        if (!string.IsNullOrEmpty(lang))
                        {
                            languageCounts[lang] = languageCounts.GetValueOrDefault(lang, 0) + 1;
                        }
                    }
                }

                foreach (var subDir in dir.EnumerateDirectories())
                {
                    if (ct.IsCancellationRequested) return;
                    if (IgnoredDirectories.Contains(subDir.Name) || subDir.Name.StartsWith('.'))
                    {
                        continue;
                    }

                    if (depth == 0)
                    {
                        importantDirectories.Add(subDir.Name);
                    }

                    ScanDirectory(subDir, depth + 1);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories with access restrictions
            }
        }

        await Task.Run(() => ScanDirectory(rootInfo, 0), ct);

        // Analyze package configurations to detect frameworks
        await DetectFrameworksFromConfigsAsync(projectRoot, packageConfigs, detectedFrameworks, ct);

        // Determine build system
        var buildSystem = DetectBuildSystem(packageConfigs);

        // Gather Git status
        var (gitBranch, gitCommit, gitIsDirty) = await GetGitInfoAsync(projectRoot, ct);

        // Sort languages by file count
        var detectedLanguages = languageCounts
            .OrderByDescending(kv => kv.Value)
            .Select(kv => kv.Key)
            .ToList();

        // Build brief architecture overview
        var archOverview = BuildArchitectureOverview(
            projectName, detectedLanguages, detectedFrameworks.ToList(),
            entryPoints, importantDirectories.ToList(), buildSystem);

        return new RepositoryAnalysis
        {
            ProjectRoot = projectRoot,
            ProjectName = projectName,
            DetectedLanguages = detectedLanguages,
            PrimaryFrameworks = detectedFrameworks.OrderBy(f => f).ToList(),
            EntryPoints = entryPoints,
            BuildSystem = buildSystem,
            PackageConfigFiles = packageConfigs,
            ImportantDirectories = importantDirectories.OrderBy(d => d).ToList(),
            TotalSourceFiles = totalSourceFiles,
            GitBranch = gitBranch,
            GitCommit = gitCommit,
            GitIsDirty = gitIsDirty,
            InstructionFiles = instructionFiles,
            ArchitectureOverview = archOverview,
            AnalyzedAt = DateTimeOffset.UtcNow
        };
    }

    private static string GetLanguageName(string extension) => extension.ToLowerInvariant() switch
    {
        ".cs" => "C#",
        ".ts" or ".tsx" => "TypeScript",
        ".js" or ".jsx" or ".mjs" or ".cjs" => "JavaScript",
        ".py" => "Python",
        ".rs" => "Rust",
        ".go" => "Go",
        ".cpp" or ".cxx" or ".cc" => "C++",
        ".c" => "C",
        ".h" or ".hpp" => "C/C++ Header",
        ".java" => "Java",
        ".kt" or ".kts" => "Kotlin",
        ".dart" => "Dart",
        ".swift" => "Swift",
        ".rb" => "Ruby",
        ".php" => "PHP",
        ".sh" or ".bash" => "Shell",
        ".ps1" or ".psm1" => "PowerShell",
        ".sql" => "SQL",
        ".xaml" => "XAML",
        ".html" => "HTML",
        ".css" or ".scss" or ".sass" or ".less" => "CSS",
        _ => string.Empty
    };

    private static string DetectBuildSystem(List<string> packageConfigs)
    {
        if (packageConfigs.Any(c => c.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                                    c.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) ||
                                    c.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)))
            return ".NET / MSBuild";

        if (packageConfigs.Any(c => c.Equals("package.json", StringComparison.OrdinalIgnoreCase)))
            return "Node.js (npm / pnpm / yarn / bun)";

        if (packageConfigs.Any(c => c.Equals("Cargo.toml", StringComparison.OrdinalIgnoreCase)))
            return "Cargo (Rust)";

        if (packageConfigs.Any(c => c.Equals("go.mod", StringComparison.OrdinalIgnoreCase)))
            return "Go Modules";

        if (packageConfigs.Any(c => c.Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase) ||
                                    c.Equals("requirements.txt", StringComparison.OrdinalIgnoreCase)))
            return "Python (pip / poetry / uv)";

        if (packageConfigs.Any(c => c.Equals("Makefile", StringComparison.OrdinalIgnoreCase)))
            return "Make";

        if (packageConfigs.Any(c => c.Equals("CMakeLists.txt", StringComparison.OrdinalIgnoreCase)))
            return "CMake";

        return "Generic";
    }

    private static async Task DetectFrameworksFromConfigsAsync(
        string projectRoot,
        List<string> packageConfigs,
        HashSet<string> frameworks,
        CancellationToken ct)
    {
        foreach (var config in packageConfigs)
        {
            if (ct.IsCancellationRequested) break;
            var fullPath = Path.Combine(projectRoot, config);
            if (!File.Exists(fullPath)) continue;

            var filename = Path.GetFileName(fullPath);

            if (filename.Equals("package.json", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var text = await File.ReadAllTextAsync(fullPath, ct);
                    using var doc = JsonDocument.Parse(text);
                    var root = doc.RootElement;

                    void CheckDeps(string propName)
                    {
                        if (root.TryGetProperty(propName, out var deps) && deps.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var dep in deps.EnumerateObject())
                            {
                                var name = dep.Name.ToLowerInvariant();
                                if (name == "react" || name == "react-dom") frameworks.Add("React");
                                else if (name == "next") frameworks.Add("Next.js");
                                else if (name == "vue") frameworks.Add("Vue");
                                else if (name == "@angular/core") frameworks.Add("Angular");
                                else if (name == "svelte") frameworks.Add("Svelte");
                                else if (name == "express") frameworks.Add("Express");
                                else if (name == "tailwindcss") frameworks.Add("Tailwind CSS");
                                else if (name == "electron") frameworks.Add("Electron");
                                else if (name == "vite") frameworks.Add("Vite");
                                else if (name == "vitest" || name == "jest") frameworks.Add("Jest/Vitest");
                            }
                        }
                    }

                    CheckDeps("dependencies");
                    CheckDeps("devDependencies");
                }
                catch
                {
                    // Ignore parse errors in non-standard JSON
                }
            }
            else if (filename.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var content = await File.ReadAllTextAsync(fullPath, ct);
                    if (content.Contains("Microsoft.WindowsAppSDK", StringComparison.OrdinalIgnoreCase))
                        frameworks.Add("WinUI 3 / Windows App SDK");
                    if (content.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase))
                        frameworks.Add("ASP.NET Core");
                    if (content.Contains("UseWPF", StringComparison.OrdinalIgnoreCase))
                        frameworks.Add("WPF");
                    if (content.Contains("UseWindowsForms", StringComparison.OrdinalIgnoreCase))
                        frameworks.Add("Windows Forms");
                    if (content.Contains("xunit", StringComparison.OrdinalIgnoreCase))
                        frameworks.Add("xUnit");
                    if (content.Contains("nunit", StringComparison.OrdinalIgnoreCase))
                        frameworks.Add("NUnit");
                }
                catch
                {
                    // Ignore read errors
                }
            }
        }
    }

    private static async Task<(string? Branch, string? Commit, bool IsDirty)> GetGitInfoAsync(
        string projectRoot, CancellationToken ct)
    {
        var gitDir = Path.Combine(projectRoot, ".git");
        if (!Directory.Exists(gitDir))
        {
            return (null, null, false);
        }

        try
        {
            var branch = await RunGitCommandAsync(projectRoot, "branch --show-current", ct);
            var commit = await RunGitCommandAsync(projectRoot, "rev-parse --short HEAD", ct);
            var status = await RunGitCommandAsync(projectRoot, "status --porcelain", ct);

            var isDirty = !string.IsNullOrWhiteSpace(status);
            return (branch?.Trim(), commit?.Trim(), isDirty);
        }
        catch
        {
            return (null, null, false);
        }
    }

    private static async Task<string?> RunGitCommandAsync(string workingDir, string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        var outputTask = proc.StandardOutput.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);

        return proc.ExitCode == 0 ? await outputTask : null;
    }

    private static string BuildArchitectureOverview(
        string projectName,
        IReadOnlyList<string> languages,
        IReadOnlyList<string> frameworks,
        IReadOnlyList<string> entryPoints,
        IReadOnlyList<string> importantDirs,
        string buildSystem)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Project: {projectName}");
        sb.AppendLine($"Build System: {buildSystem}");

        if (languages.Count > 0)
            sb.AppendLine($"Languages: {string.Join(", ", languages.Take(4))}");

        if (frameworks.Count > 0)
            sb.AppendLine($"Frameworks: {string.Join(", ", frameworks)}");

        if (entryPoints.Count > 0)
            sb.AppendLine($"Entry Points: {string.Join(", ", entryPoints.Take(3))}");

        if (importantDirs.Count > 0)
            sb.AppendLine($"Key Directories: {string.Join(", ", importantDirs.Take(6))}");

        return sb.ToString().Trim();
    }
}
