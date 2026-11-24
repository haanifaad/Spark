using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Tools;

/// <summary>
/// Thread-safe registry and execution lifecycle engine for typed Study Division tools.
/// Enforces: Requested -> Validating -> PermissionCheck -> AwaitingApproval -> Executing -> Completed / Failed / Cancelled.
/// </summary>
public sealed class StudyToolRegistry : IStudyToolRegistry
{
    private readonly ConcurrentDictionary<string, (StudyToolDefinition Definition, IStudyToolHandler Handler)> _tools = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, StudyToolExecutionRequest> _pendingApprovals = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _inFlightCancellations = new(StringComparer.OrdinalIgnoreCase);

    private readonly IStudyPermissionManager _permissionManager;
    private readonly IStudyDataStore _dataStore;

    public StudyToolRegistry(IStudyPermissionManager permissionManager, IStudyDataStore dataStore)
    {
        _permissionManager = permissionManager ?? throw new ArgumentNullException(nameof(permissionManager));
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));

        RegisterDefaultTools();
    }

    public StudyToolRegistry(IStudyDataStore dataStore, IStudyPermissionManager permissionManager)
        : this(permissionManager, dataStore)
    {
    }

    private void RegisterDefaultTools()
    {
        // 1. Mathematics
        RegisterTool(
            new StudyToolDefinition("calculator", "Calculator", "Evaluates basic arithmetic expressions.", ToolCategory.Mathematics, "1.0.0",
                "{\"expression\":\"string\"}", "{\"result\":\"number\"}", StudyPermissionKind.ReadOnly, 5000, true, null, ["math", "arithmetic"]),
            new CalculatorTool());

        RegisterTool(
            new StudyToolDefinition("scientific-calculator", "Scientific Calculator", "Evaluates trigonometric, logarithmic, and exponential operations.", ToolCategory.Mathematics, "1.0.0",
                "{\"operation\":\"string\",\"value\":\"number\",\"value2\":\"number?\",\"useDegrees\":\"bool?\"}", "{\"result\":\"number\"}", StudyPermissionKind.ReadOnly, 5000, true, null, ["math", "scientific", "trigonometry"]),
            new ScientificCalculatorTool());

        RegisterTool(
            new StudyToolDefinition("unit-converter", "Unit Converter", "Converts values between length, mass, temperature, time, and digital units.", ToolCategory.Mathematics, "1.0.0",
                "{\"value\":\"number\",\"from\":\"string\",\"to\":\"string\"}", "{\"convertedValue\":\"number\"}", StudyPermissionKind.ReadOnly, 5000, true, null, ["units", "conversion"]),
            new UnitConverterTool());

        RegisterTool(
            new StudyToolDefinition("equation-evaluator", "Equation Evaluator", "Solves linear and quadratic algebraic equations.", ToolCategory.Mathematics, "1.0.0",
                "{\"equation\":\"string\"}", "{\"solution\":\"string\"}", StudyPermissionKind.ReadOnly, 5000, true, null, ["algebra", "equations"]),
            new EquationEvaluatorTool());

        RegisterTool(
            new StudyToolDefinition("statistics-calculator", "Statistics Calculator", "Calculates mean, median, mode, variance, and standard deviation.", ToolCategory.Mathematics, "1.0.0",
                "{\"data\":\"number[]|string\"}", "{\"mean\":\"number\",\"median\":\"number\"}", StudyPermissionKind.ReadOnly, 5000, true, null, ["statistics", "data"]),
            new StatisticsCalculatorTool());

        RegisterTool(
            new StudyToolDefinition("matrix-operations", "Matrix Operations", "Performs matrix addition, multiplication, transposition, and determinants.", ToolCategory.Mathematics, "1.0.0",
                "{\"operation\":\"string\",\"matrixA\":\"number[][]\",\"matrixB\":\"number[][]?\"}", "{\"result\":\"any\"}", StudyPermissionKind.ReadOnly, 5000, true, null, ["matrix", "linear-algebra"]),
            new MatrixOperationsTool());

        // 2. Documents
        RegisterTool(
            new StudyToolDefinition("pdf-reader", "PDF Reader", "Reads text safely from PDF and educational document representations.", ToolCategory.Documents, "1.0.0",
                "{\"filePath\":\"string?\",\"textContent\":\"string?\",\"maxChars\":\"int?\"}", "{\"extractedText\":\"string\"}", StudyPermissionKind.FileAccess, 10000, true, null, ["pdf", "documents"]),
            new PdfReaderTool());

        RegisterTool(
            new StudyToolDefinition("document-text-extractor", "Document Text Extractor", "Extracts headers, sections, bullet items, and readability statistics.", ToolCategory.Documents, "1.0.0",
                "{\"text\":\"string\"}", "{\"headingsFound\":\"string[]\"}", StudyPermissionKind.ReadOnly, 5000, true, null, ["text", "extraction"]),
            new DocumentTextExtractorTool());

        RegisterTool(
            new StudyToolDefinition("text-search", "Text Search", "Performs context-aware line and keyword matching across document text.", ToolCategory.Documents, "1.0.0",
                "{\"query\":\"string\",\"corpus\":\"string\",\"useRegex\":\"bool?\"}", "{\"matches\":\"any[]\"}", StudyPermissionKind.ReadOnly, 5000, true, null, ["search", "regex"]),
            new TextSearchTool());

        RegisterTool(
            new StudyToolDefinition("document-metadata-reader", "Document Metadata Reader", "Calculates character count, word count, hashes, and encoding details.", ToolCategory.Documents, "1.0.0",
                "{\"text\":\"string\",\"fileName\":\"string?\"}", "{\"sha256Hash\":\"string\"}", StudyPermissionKind.ReadOnly, 5000, true, null, ["metadata", "hash"]),
            new DocumentMetadataReaderTool());

        // 3. Programming
        RegisterTool(
            new StudyToolDefinition("code-formatter", "Code Formatter", "Formats code indentation and style across C#, Python, JS, and JSON.", ToolCategory.Programming, "1.0.0",
                "{\"code\":\"string\",\"language\":\"string?\",\"indentSize\":\"int?\"}", "{\"formattedCode\":\"string\"}", StudyPermissionKind.ReadOnly, 5000, true, null, ["code", "format"]),
            new CodeFormatterTool());

        RegisterTool(
            new StudyToolDefinition("code-analyzer", "Code Analyzer", "Analyzes syntax balance, cyclomatic complexity, and code metrics.", ToolCategory.Programming, "1.0.0",
                "{\"code\":\"string\",\"language\":\"string?\"}", "{\"estimatedComplexity\":\"int\"}", StudyPermissionKind.ReadOnly, 5000, true, null, ["code", "analyzer", "lint"]),
            new CodeAnalyzerTool());

        RegisterTool(
            new StudyToolDefinition("json-validator", "JSON Validator", "Validates strict JSON syntax with detailed line and column diagnostics.", ToolCategory.Programming, "1.0.0",
                "{\"json\":\"string\"}", "{\"isValid\":\"bool\"}", StudyPermissionKind.ReadOnly, 5000, true, null, ["json", "validation"]),
            new JsonValidatorTool());

        RegisterTool(
            new StudyToolDefinition("html-preview", "HTML Preview", "Sanitizes and produces safe sandbox HTML previews.", ToolCategory.Programming, "1.0.0",
                "{\"html\":\"string\"}", "{\"previewHtml\":\"string\"}", StudyPermissionKind.ReadOnly, 5000, true, null, ["html", "preview"]),
            new HtmlPreviewTool());

        RegisterTool(
            new StudyToolDefinition("sql-query-validator", "SQL Query Validator", "Analyzes SQL query statements, identifies tables, and flags risky operations.", ToolCategory.Programming, "1.0.0",
                "{\"sql\":\"string\"}", "{\"statementType\":\"string\"}", StudyPermissionKind.ReadOnly, 5000, true, null, ["sql", "database"]),
            new SqlQueryValidatorTool());

        // 4. Learning
        RegisterTool(
            new StudyToolDefinition("flashcard-generator", "Flashcard Generator", "Generates structured pedagogical flashcards with difficulty ratings.", ToolCategory.Learning, "1.0.0",
                "{\"topic\":\"string\",\"content\":\"string?\",\"count\":\"int?\"}", "{\"flashcards\":\"any[]\"}", StudyPermissionKind.ReadOnly, 5000, true, null, ["flashcards", "study"]),
            new FlashcardGeneratorTool());

        RegisterTool(
            new StudyToolDefinition("quiz-generator", "Quiz Generator", "Generates multiple-choice and conceptual practice questions.", ToolCategory.Learning, "1.0.0",
                "{\"topic\":\"string\",\"questions\":\"int?\"}", "{\"quiz\":\"any[]\"}", StudyPermissionKind.ReadOnly, 5000, true, null, ["quiz", "questions"]),
            new QuizGeneratorTool());

        RegisterTool(
            new StudyToolDefinition("study-progress-reader", "Study Progress Reader", "Reads real learner progress, streaks, and completion rates from storage.", ToolCategory.Learning, "1.0.0",
                "{\"courseId\":\"string?\",\"userId\":\"string?\"}", "{\"completionPercentage\":\"int\"}", StudyPermissionKind.ReadOnly, 5000, true, null, ["progress", "tracking"]),
            new StudyProgressReaderTool(_dataStore));

        RegisterTool(
            new StudyToolDefinition("learning-objective-reader", "Learning Objective Reader", "Reads active Bloom's taxonomy objectives and mastery flags from storage.", ToolCategory.Learning, "1.0.0",
                "{\"lessonId\":\"string?\",\"userId\":\"string?\"}", "{\"masteryPercentage\":\"int\"}", StudyPermissionKind.ReadOnly, 5000, true, null, ["objectives", "blooms"]),
            new LearningObjectiveReaderTool(_dataStore));

        // 5. Research
        RegisterTool(
            new StudyToolDefinition("source-metadata-reader", "Source Metadata Reader", "Extracts academic metadata, URLs, authors, and publication dates.", ToolCategory.Research, "1.0.0",
                "{\"source\":\"string\",\"author\":\"string?\",\"year\":\"int?\"}", "{\"domain\":\"string\"}", StudyPermissionKind.ReadOnly, 5000, true, null, ["research", "metadata"]),
            new SourceMetadataReaderTool());

        RegisterTool(
            new StudyToolDefinition("citation-formatter", "Citation Formatter", "Generates standard academic citations in APA, MLA, Chicago, IEEE, and Harvard.", ToolCategory.Research, "1.0.0",
                "{\"title\":\"string\",\"author\":\"string\",\"year\":\"string?\",\"publisher\":\"string?\",\"style\":\"string?\"}", "{\"citation\":\"string\"}", StudyPermissionKind.ReadOnly, 5000, true, null, ["citations", "bibliography"]),
            new CitationFormatterTool());

        RegisterTool(
            new StudyToolDefinition("doi-metadata-lookup", "DOI Metadata Lookup", "Parses and validates standard DOI strings and resolves registrant publishers.", ToolCategory.Research, "1.0.0",
                "{\"doi\":\"string\"}", "{\"registrant\":\"string\"}", StudyPermissionKind.ReadOnly, 5000, true, null, ["doi", "academic"]),
            new DoiMetadataLookupTool());

        RegisterTool(
            new StudyToolDefinition("web-search", "Live Web Search", "Searches the live web for academic papers, articles, documentation, and sources.", ToolCategory.Research, "1.0.0",
                "{\"query\":\"string\",\"count\":\"int?\"}", "{\"results\":\"array\"}", StudyPermissionKind.ReadOnly, 10000, true, null, ["search", "web", "research", "sources"]),
            new WebSearchResearchTool());
    }

    public void RegisterTool(StudyToolDefinition definition, IStudyToolHandler handler)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Id);

        _tools[definition.Id] = (definition, handler);
    }

    public StudyToolDefinition? GetTool(string toolId)
    {
        if (string.IsNullOrWhiteSpace(toolId)) return null;
        if (_tools.TryGetValue(toolId, out var entry)) return entry.Definition;
        if (toolId.StartsWith("tool-", StringComparison.OrdinalIgnoreCase) && _tools.TryGetValue(toolId[5..], out entry)) return entry.Definition;
        if (!toolId.StartsWith("tool-", StringComparison.OrdinalIgnoreCase) && _tools.TryGetValue("tool-" + toolId, out entry)) return entry.Definition;
        return null;
    }

    public IReadOnlyList<StudyToolDefinition> ListTools(ToolCategory? category = null)
    {
        var list = _tools.Values.Select(v => v.Definition);
        if (category.HasValue)
        {
            list = list.Where(t => t.Category == category.Value);
        }
        return list.OrderBy(t => t.Category).ThenBy(t => t.Name).ToList();
    }

    public IReadOnlyList<StudyToolDefinition> SearchTools(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm)) return ListTools();
        var term = searchTerm.Trim();

        return _tools.Values
            .Select(v => v.Definition)
            .Where(t => t.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        t.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        t.Id.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        t.Tags.Any(tag => tag.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public async Task<StudyToolExecutionResult> ExecuteToolAsync(StudyToolExecutionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sw = Stopwatch.StartNew();

        // 1. Requested
        var result = new StudyToolExecutionResult
        {
            RequestId = request.RequestId,
            ToolId = request.ToolId,
            UserId = request.UserId,
            Status = ToolExecutionStatus.Requested,
            TimestampUtc = DateTime.UtcNow
        };

        // 2. Validating
        result.Status = ToolExecutionStatus.Validating;
        if (!_tools.TryGetValue(request.ToolId, out var toolEntry))
        {
            if (request.ToolId.StartsWith("tool-", StringComparison.OrdinalIgnoreCase))
            {
                _tools.TryGetValue(request.ToolId[5..], out toolEntry);
            }
            else
            {
                _tools.TryGetValue("tool-" + request.ToolId, out toolEntry);
            }
        }

        if (toolEntry == default)
        {
            result.Status = ToolExecutionStatus.Failed;
            result.Success = false;
            result.ErrorMessage = $"Tool '{request.ToolId}' is not registered.";
            result.DurationMs = sw.ElapsedMilliseconds;
            await RecordAndPersistAsync(result, request.UserId, ct);
            return result;
        }

        var (definition, handler) = toolEntry;
        if (!definition.IsAvailable)
        {
            result.Status = ToolExecutionStatus.Failed;
            result.Success = false;
            result.ErrorMessage = $"Tool '{request.ToolId}' is unavailable: {definition.AvailabilityReason ?? "Not Configured"}.";
            result.DurationMs = sw.ElapsedMilliseconds;
            await RecordAndPersistAsync(result, request.UserId, ct);
            return result;
        }

        // 3. Permission Check
        result.Status = ToolExecutionStatus.PermissionCheck;
        var decision = await _permissionManager.AuthorizeAsync(
            definition.RequiredPermission,
            definition.Id,
            definition.Description,
            request.UserId,
            ct);

        if (!decision.IsAllowed)
        {
            if (decision.RequiredUserApproval)
            {
                result.Status = ToolExecutionStatus.AwaitingApproval;
                result.Success = false;
                result.ErrorMessage = decision.Reason ?? "Operation queued for user confirmation.";
                result.ApprovalRecord = new StudyApprovalInfo(
                    request.RequestId,
                    "Pending",
                    DateTime.UtcNow,
                    isApproved: false,
                    decision.Reason ?? "User confirmation required.");
                _pendingApprovals[request.RequestId] = request;
                result.DurationMs = sw.ElapsedMilliseconds;
                await RecordAndPersistAsync(result, request.UserId, ct);
                return result;
            }

            result.Status = ToolExecutionStatus.Failed;
            result.Success = false;
            result.ErrorMessage = $"Execution permission denied: {decision.Reason}";
            result.DurationMs = sw.ElapsedMilliseconds;
            await RecordAndPersistAsync(result, request.UserId, ct);
            return result;
        }

        // 4. Executing
        return await ExecuteApprovedCoreAsync(request, definition, handler, null, sw, ct);
    }

    public async Task<StudyToolExecutionResult> ApproveAndExecutePendingAsync(
        string requestId,
        string approvedBy,
        string userId = "default",
        CancellationToken ct = default)
    {
        if (!_pendingApprovals.TryRemove(requestId, out var request))
        {
            return new StudyToolExecutionResult(
                requestId,
                "unknown",
                userId,
                ToolExecutionStatus.Failed,
                success: false,
                errorMessage: $"No pending execution request found with ID '{requestId}'.");
        }

        if (!_tools.TryGetValue(request.ToolId, out var toolEntry))
        {
            if (request.ToolId.StartsWith("tool-", StringComparison.OrdinalIgnoreCase))
            {
                _tools.TryGetValue(request.ToolId[5..], out toolEntry);
            }
            else
            {
                _tools.TryGetValue("tool-" + request.ToolId, out toolEntry);
            }
        }

        if (toolEntry == default)
        {
            return new StudyToolExecutionResult(
                requestId,
                request.ToolId,
                userId,
                ToolExecutionStatus.Failed,
                success: false,
                errorMessage: $"Tool '{request.ToolId}' was deregistered while awaiting approval.");
        }

        var approvalRecord = new StudyApprovalInfo(
            Guid.NewGuid().ToString("N"),
            approvedBy,
            DateTime.UtcNow,
            isApproved: true,
            "Approved by authorized user.");

        var sw = Stopwatch.StartNew();
        return await ExecuteApprovedCoreAsync(request, toolEntry.Definition, toolEntry.Handler, approvalRecord, sw, ct);
    }

    public async Task<StudyToolExecutionResult> RejectPendingAsync(
        string requestId,
        string reason,
        string rejectedBy,
        string userId = "default",
        CancellationToken ct = default)
    {
        _pendingApprovals.TryRemove(requestId, out var request);

        var approvalRecord = new StudyApprovalInfo(
            Guid.NewGuid().ToString("N"),
            rejectedBy,
            DateTime.UtcNow,
            isApproved: false,
            reason);

        var result = new StudyToolExecutionResult(
            requestId,
            request?.ToolId ?? "rejected",
            userId,
            ToolExecutionStatus.Cancelled,
            success: false,
            errorMessage: $"Execution rejected by {rejectedBy}: {reason}",
            durationMs: 0,
            approvalRecord: approvalRecord);

        await RecordAndPersistAsync(result, userId, ct);
        return result;
    }

    public Task<bool> CancelExecutionAsync(string requestId, string userId = "default", CancellationToken ct = default)
    {
        if (_pendingApprovals.TryRemove(requestId, out _))
        {
            return Task.FromResult(true);
        }

        if (_inFlightCancellations.TryGetValue(requestId, out var cts))
        {
            cts.Cancel();
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public async Task<IReadOnlyList<StudyToolExecutionResult>> GetToolExecutionHistoryAsync(
        string? toolId = null,
        string userId = "default",
        int limit = 50,
        CancellationToken ct = default)
    {
        return await _dataStore.ListToolExecutionsAsync(toolId, userId, limit, ct);
    }

    public async Task<StudyToolExecutionResult?> GetExecutionResultAsync(string requestId, string userId = "default", CancellationToken ct = default)
    {
        return await _dataStore.GetToolExecutionAsync(requestId, userId, ct);
    }

    private async Task<StudyToolExecutionResult> ExecuteApprovedCoreAsync(
        StudyToolExecutionRequest request,
        StudyToolDefinition definition,
        IStudyToolHandler handler,
        StudyApprovalInfo? approvalRecord,
        Stopwatch sw,
        CancellationToken ct)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(definition.TimeoutMs));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        _inFlightCancellations[request.RequestId] = timeoutCts;

        var result = new StudyToolExecutionResult
        {
            RequestId = request.RequestId,
            ToolId = definition.Id,
            UserId = request.UserId,
            Status = ToolExecutionStatus.Executing,
            ApprovalRecord = approvalRecord,
            TimestampUtc = DateTime.UtcNow
        };

        try
        {
            var output = await handler.InvokeAsync(request.Arguments, linkedCts.Token);
            sw.Stop();

            result.Status = ToolExecutionStatus.Completed;
            result.Success = true;
            result.Data = output;
            result.DurationMs = sw.ElapsedMilliseconds;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            sw.Stop();
            result.Status = ToolExecutionStatus.Failed;
            result.Success = false;
            result.ErrorMessage = $"Tool '{definition.Id}' timed out after {definition.TimeoutMs} ms.";
            result.DurationMs = sw.ElapsedMilliseconds;
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            result.Status = ToolExecutionStatus.Cancelled;
            result.Success = false;
            result.ErrorMessage = "Tool execution was cancelled.";
            result.DurationMs = sw.ElapsedMilliseconds;
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.Status = ToolExecutionStatus.Failed;
            result.Success = false;
            result.ErrorMessage = $"Tool execution error: {ex.Message}";
            result.DurationMs = sw.ElapsedMilliseconds;
        }
        finally
        {
            _inFlightCancellations.TryRemove(request.RequestId, out _);
        }

        await RecordAndPersistAsync(result, request.UserId, ct);
        return result;
    }

    private async Task RecordAndPersistAsync(StudyToolExecutionResult result, string userId, CancellationToken ct)
    {
        try
        {
            await _dataStore.SaveToolExecutionAsync(result, userId, ct);
        }
        catch
        {
            // Fail-safe persistence: log or suppress persistence failure so execution outcome isn't lost
        }
    }
}
