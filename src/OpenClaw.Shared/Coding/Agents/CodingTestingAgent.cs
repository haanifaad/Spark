using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Coding.Models;
using OpenClaw.Shared.Coding.Terminal;

namespace OpenClaw.Shared.Coding.Agents;

/// <summary>
/// Result summary from a test execution run.
/// </summary>
public sealed record TestRunSummary
{
    public bool Success => FailedCount == 0 && TotalTests > 0;
    public int TotalTests { get; init; }
    public int PassedCount { get; init; }
    public int FailedCount { get; init; }
    public int SkippedCount { get; init; }
    public double DurationSeconds { get; init; }
    public IReadOnlyList<string> FailedTestNames { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ErrorDetails { get; init; } = Array.Empty<string>();
    public required string RawOutput { get; init; }
}

/// <summary>
/// Autonomous testing agent that detects test runners, executes test suites,
/// and parses structured validation results.
/// </summary>
public sealed class CodingTestingAgent
{
    private readonly CodingTerminalEngine _terminal;

    public CodingTestingAgent(CodingTerminalEngine terminal)
    {
        _terminal = terminal ?? throw new ArgumentNullException(nameof(terminal));
    }

    /// <summary>
    /// Detects suitable test command for the project.
    /// </summary>
    public string DetectTestCommand(RepositoryAnalysis analysis)
    {
        if (analysis.DetectedLanguages.Contains("C#") || analysis.BuildSystem?.Contains(".NET") == true)
            return "dotnet test --no-restore";

        if (analysis.DetectedLanguages.Contains("TypeScript") || analysis.DetectedLanguages.Contains("JavaScript"))
            return "npm test";

        if (analysis.DetectedLanguages.Contains("Python"))
            return "pytest";

        if (analysis.DetectedLanguages.Contains("Rust"))
            return "cargo test";

        if (analysis.DetectedLanguages.Contains("Go"))
            return "go test ./...";

        return "dotnet test";
    }

    /// <summary>
    /// Executes project test suite and parses test results.
    /// </summary>
    public async Task<TestRunSummary> RunTestsAsync(
        string? testCommand = null,
        RepositoryAnalysis? analysis = null,
        int timeoutSeconds = 180,
        CancellationToken ct = default)
    {
        var cmd = testCommand;
        if (string.IsNullOrWhiteSpace(cmd) && analysis != null)
        {
            cmd = DetectTestCommand(analysis);
        }
        cmd ??= "dotnet test";

        var execResult = await _terminal.ExecuteAsync(cmd, timeoutSeconds: timeoutSeconds, ct: ct);
        return ParseTestOutput(execResult.CombinedOutput, execResult.WallTimeSeconds, execResult.ExitCode);
    }

    /// <summary>
    /// Parses test runner output (xUnit, NUnit, Jest, Vitest, pytest, cargo).
    /// </summary>
    public static TestRunSummary ParseTestOutput(string output, double durationSeconds, int exitCode)
    {
        int passed = 0;
        int failed = 0;
        int skipped = 0;
        var failedTests = new List<string>();
        var errorDetails = new List<string>();

        // 1. .NET / VSTest / xUnit: "Passed:  10, Failed:   1, Skipped:   0, Total:  11"
        var netMatch = Regex.Match(output, @"Passed:\s*(\d+),\s*Failed:\s*(\d+),\s*Skipped:\s*(\d+),\s*Total:\s*(\d+)", RegexOptions.IgnoreCase);
        if (netMatch.Success)
        {
            passed = int.Parse(netMatch.Groups[1].Value);
            failed = int.Parse(netMatch.Groups[2].Value);
            skipped = int.Parse(netMatch.Groups[3].Value);
        }
        else
        {
            // Alternative xUnit: "Total tests: 5. Passed: 5. Failed: 0. Skipped: 0"
            var altMatch = Regex.Match(output, @"Total tests:\s*(\d+)\.\s*Passed:\s*(\d+)\.\s*Failed:\s*(\d+)", RegexOptions.IgnoreCase);
            if (altMatch.Success)
            {
                passed = int.Parse(altMatch.Groups[2].Value);
                failed = int.Parse(altMatch.Groups[3].Value);
            }
            else
            {
                // Jest / Vitest: "Tests:       1 failed, 4 passed, 5 total"
                var jestMatch = Regex.Match(output, @"Tests:\s*(?:(\d+)\s+failed,)?\s*(?:(\d+)\s+passed,)?\s*(\d+)\s+total", RegexOptions.IgnoreCase);
                if (jestMatch.Success)
                {
                    if (jestMatch.Groups[1].Success) failed = int.Parse(jestMatch.Groups[1].Value);
                    if (jestMatch.Groups[2].Success) passed = int.Parse(jestMatch.Groups[2].Value);
                }
                else
                {
                    // Pytest: "=== 1 failed, 4 passed in 0.23s ==="
                    var pytestMatch = Regex.Match(output, @"===+\s*(?:(\d+)\s+failed,)?\s*(?:(\d+)\s+passed)?.*===", RegexOptions.IgnoreCase);
                    if (pytestMatch.Success)
                    {
                        if (pytestMatch.Groups[1].Success) failed = int.Parse(pytestMatch.Groups[1].Value);
                        if (pytestMatch.Groups[2].Success) passed = int.Parse(pytestMatch.Groups[2].Value);
                    }
                }
            }
        }

        // If exit code is non-zero and failed count wasn't parsed, at least mark failed
        if (exitCode != 0 && failed == 0)
        {
            failed = 1;
        }

        // Extract failure lines
        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.Contains("[FAIL]", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("FAILED ", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Catastrophic failure", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Assert.", StringComparison.OrdinalIgnoreCase))
            {
                var clean = line.Trim();
                if (!errorDetails.Contains(clean)) errorDetails.Add(clean);
                if (clean.Length < 100 && !failedTests.Contains(clean)) failedTests.Add(clean);
            }
        }

        int total = passed + failed + skipped;

        return new TestRunSummary
        {
            TotalTests = total,
            PassedCount = passed,
            FailedCount = failed,
            SkippedCount = skipped,
            DurationSeconds = durationSeconds,
            FailedTestNames = failedTests,
            ErrorDetails = errorDetails,
            RawOutput = output
        };
    }
}
