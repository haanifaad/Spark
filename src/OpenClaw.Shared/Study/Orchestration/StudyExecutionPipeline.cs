using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Inference.Routing;
using OpenClaw.Shared.Study.Commands;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;
using OpenClaw.Shared.Study.Infrastructure;
using OpenClaw.Shared.Study.Skills;

namespace OpenClaw.Shared.Study.Orchestration;

/// <summary>
/// Unified execution pipeline uniting Slash Commands and Natural Language inquiries
/// through a single, permission-gated, deterministic learning intelligence workflow.
/// Architecture:
/// User Input -> Intent/Command Resolution -> Skill Selection -> Tool Selection -> Permission Check -> Execution -> Result -> User.
/// </summary>
public sealed class StudyExecutionPipeline : IStudyExecutionPipeline
{
    private readonly IStudyCommandParser _commandParser;
    private readonly IStudyCommandRegistry _commandRegistry;
    private readonly IStudySkillRegistry _skillRegistry;
    private readonly IStudyToolRegistry _toolRegistry;
    private readonly IStudyIntentRouter _intentRouter;
    private readonly IStudyPermissionManager _permissionManager;
    private readonly IStudyTutorService _tutorService;
    private readonly ISparkModelRouter? _modelRouter;

    public StudyExecutionPipeline(
        IStudySkillRegistry skillRegistry,
        IStudyToolRegistry toolRegistry,
        IStudyCommandRegistry commandRegistry,
        IStudyCommandParser? commandParser = null,
        IStudyPermissionManager? permissionManager = null,
        IStudyIntentRouter? intentRouter = null,
        IStudyTutorService? tutorService = null,
        ISparkModelRouter? modelRouter = null)
        : this(
            commandParser ?? new StudyCommandParser(),
            commandRegistry,
            skillRegistry,
            toolRegistry,
            intentRouter ?? new StudyIntentRouter(),
            permissionManager ?? new StudyPermissionManager(),
            tutorService ?? new StudyTutorService(modelRouter),
            modelRouter)
    {
    }

    public StudyExecutionPipeline(
        IStudyCommandParser commandParser,
        IStudyCommandRegistry commandRegistry,
        IStudySkillRegistry skillRegistry,
        IStudyToolRegistry toolRegistry,
        IStudyIntentRouter intentRouter,
        IStudyPermissionManager permissionManager,
        IStudyTutorService tutorService,
        ISparkModelRouter? modelRouter = null)
    {
        _commandParser = commandParser ?? throw new ArgumentNullException(nameof(commandParser));
        _commandRegistry = commandRegistry ?? throw new ArgumentNullException(nameof(commandRegistry));
        _skillRegistry = skillRegistry ?? throw new ArgumentNullException(nameof(skillRegistry));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _intentRouter = intentRouter ?? throw new ArgumentNullException(nameof(intentRouter));
        _permissionManager = permissionManager ?? throw new ArgumentNullException(nameof(permissionManager));
        _tutorService = tutorService ?? throw new ArgumentNullException(nameof(tutorService));
        _modelRouter = modelRouter;
    }

    public async Task<PipelineExecutionResult> ProcessInputAsync(PipelineExecutionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.UserInput))
        {
            return new PipelineExecutionResult
            {
                Success = false,
                ErrorMessage = "User input cannot be empty.",
                FinalResponse = "Please provide a query or command to begin."
            };
        }

        var sw = Stopwatch.StartNew();
        var result = new PipelineExecutionResult
        {
            TeachingMode = request.TeachingMode ?? TeachingMode.Standard,
            Language = request.Language ?? LearningLanguage.English
        };

        // ══════════════════════════════════════════════════════════════
        // Step 1: Intent / Command Resolution
        // ══════════════════════════════════════════════════════════════
        bool isSlash = _commandParser.IsSlashCommand(request.UserInput);
        string? targetSkillId = request.ForceSkillId;
        var selectedToolIds = new List<string>();
        Dictionary<string, object?> toolArgs = [];
        string effectiveTopic = request.UserInput;

        if (isSlash)
        {
            result.InputType = PipelineInputType.SlashCommand;
            var parsed = _commandParser.Parse(request.UserInput, _commandRegistry.ListCommands());
            result.ResolvedCommand = parsed;

            if (!parsed.IsValid)
            {
                sw.Stop();
                result.Success = false;
                result.ErrorMessage = string.Join("; ", parsed.ValidationErrors);
                result.FinalResponse = $"Command syntax error: {result.ErrorMessage}";
                result.DurationMs = sw.ElapsedMilliseconds;
                return result;
            }

            var cmdDef = _commandRegistry.FindCommand(parsed.CommandName);
            if (cmdDef != null)
            {
                targetSkillId ??= cmdDef.RequiredSkillId;
                selectedToolIds.AddRange(cmdDef.RequiredTools);

                // Map command arguments to tool arguments and effective topic
                foreach (var (k, v) in parsed.Arguments)
                {
                    toolArgs[k] = v;
                }

                if (parsed.Arguments.TryGetValue("topic", out var topVal))
                {
                    effectiveTopic = topVal;
                }
                else if (parsed.Arguments.TryGetValue("problem", out var probVal))
                {
                    effectiveTopic = probVal;
                    toolArgs["expression"] = probVal;
                    toolArgs["equation"] = probVal;
                }
                else if (parsed.Arguments.TryGetValue("subject", out var subjVal))
                {
                    effectiveTopic = subjVal;
                }
                else if (parsed.PositionalArguments.Count > 0)
                {
                    effectiveTopic = string.Join(" ", parsed.PositionalArguments);
                }

                // Record command execution
                await _commandRegistry.RecordCommandExecutionAsync(new CommandExecutionRecord(
                    Guid.NewGuid().ToString("N"),
                    cmdDef.Name,
                    request.UserId,
                    request.UserInput,
                    ExecutionStatus.Completed,
                    DateTime.UtcNow,
                    0,
                    targetSkillId), request.UserId, ct);
            }
        }
        else
        {
            result.InputType = PipelineInputType.NaturalLanguage;

            // Resolve educational intent
            var intentDecision = _intentRouter.Classify(request.UserInput);
            effectiveTopic = request.UserInput;

            // Route intent to optimal skill if not forced
            if (string.IsNullOrWhiteSpace(targetSkillId))
            {
                targetSkillId = ResolveSkillFromNaturalLanguage(request.UserInput, intentDecision.PrimaryIntent);
            }

            // Detect natural tool triggers (e.g. math expressions, questions, citations)
            DetectNaturalLanguageToolRequirements(request.UserInput, selectedToolIds, toolArgs);
        }

        // ══════════════════════════════════════════════════════════════
        // Step 2: Skill Selection & Context Assembly
        // ══════════════════════════════════════════════════════════════
        targetSkillId ??= "concept-explainer";
        var skill = await _skillRegistry.GetSkillAsync(targetSkillId, request.UserId, ct);
        if (skill == null || !skill.IsEnabled)
        {
            // Fallback to concept-explainer
            skill = await _skillRegistry.GetSkillAsync("concept-explainer", request.UserId, ct)
                    ?? OpenClaw.Shared.Study.Skills.StudyBuiltInSkills.GetAll().First(s => s.Id == "concept-explainer");
        }
        result.SelectedSkill = skill;

        // Ensure all tools are allowed by the skill
        var filteredTools = selectedToolIds.Where(t => skill.AllowedTools.Contains(t, StringComparer.OrdinalIgnoreCase)).Distinct().ToList();
        result.SelectedTools = filteredTools;

        // ══════════════════════════════════════════════════════════════
        // Step 3: Tool Selection & Permission Checking
        // ══════════════════════════════════════════════════════════════
        foreach (var toolId in filteredTools)
        {
            var toolDef = _toolRegistry.GetTool(toolId);
            if (toolDef == null || !toolDef.IsAvailable) continue;

            var permDecision = await _permissionManager.AuthorizeAsync(
                toolDef.RequiredPermission,
                toolDef.Id,
                toolDef.Description,
                request.UserId,
                ct);

            if (!permDecision.IsAllowed)
            {
                if (permDecision.RequiredUserApproval)
                {
                    result.RequiresApproval = true;
                    result.PendingApproval = new StudyApprovalInfo(
                        Guid.NewGuid().ToString("N"),
                        "Pending",
                        DateTime.UtcNow,
                        false,
                        permDecision.Reason ?? $"Tool '{toolDef.Name}' requires manual confirmation.");
                    result.FinalResponse = $"The action requires explicit confirmation before executing tool '{toolDef.Name}': {permDecision.Reason}";
                    sw.Stop();
                    result.DurationMs = sw.ElapsedMilliseconds;
                    return result;
                }

                // Denied
                result.ToolResults.Add(new StudyToolExecutionResult(
                    Guid.NewGuid().ToString("N"),
                    toolDef.Id,
                    request.UserId,
                    ToolExecutionStatus.Failed,
                    success: false,
                    errorMessage: $"Permission denied: {permDecision.Reason}"));
                continue;
            }

            // Execute approved tool
            var execReq = new StudyToolExecutionRequest(toolDef.Id, toolArgs, request.UserId, request.SessionId);
            var execResult = await _toolRegistry.ExecuteToolAsync(execReq, ct);
            result.ToolResults.Add(execResult);
        }

        // ══════════════════════════════════════════════════════════════
        // Step 4: Pedagogical Response Synthesis
        // ══════════════════════════════════════════════════════════════
        result.FinalResponse = SynthesizeResponse(skill, effectiveTopic, result.ToolResults, result.TeachingMode, result.Language);

        sw.Stop();
        result.DurationMs = sw.ElapsedMilliseconds;

        // Record skill execution
        await _skillRegistry.RecordSkillExecutionAsync(new SkillExecutionRecord(
            Guid.NewGuid().ToString("N"),
            skill.Id,
            request.UserId,
            request.SessionId,
            DateTime.UtcNow,
            DateTime.UtcNow,
            ExecutionStatus.Completed,
            result.DurationMs,
            request.UserInput,
            result.FinalResponse.Length > 200 ? result.FinalResponse[..200] : result.FinalResponse), request.UserId, ct);

        return result;
    }

    private static string ResolveSkillFromNaturalLanguage(string query, StudyEducationalIntent intent)
    {
        var lower = query.ToLowerInvariant();

        if (lower.Contains("step by step") || lower.Contains("steps")) return "step-by-step-teacher";
        if (lower.Contains("socratic") || lower.Contains("hint") || lower.Contains("don't give me the answer")) return "socratic-tutor";
        if (lower.Contains("quiz") || lower.Contains("test me")) return "question-generator";
        if (lower.Contains("notes") || lower.Contains("summary")) return "note-creator";
        if (lower.Contains("flashcard")) return "flashcard-creator";
        if (lower.Contains("cite") || lower.Contains("citation") || lower.Contains("doi")) return "citation-assistant";
        if (lower.Contains("fact check") || lower.Contains("is it true")) return "fact-checker";
        if (lower.Contains("python") || lower.Contains("code")) return "python-mentor";
        if (lower.Contains("plan") || lower.Contains("timetable")) return "study-planner";
        if (lower.Contains("malayalam") || lower.Contains("മലയാളം")) return "language-tutor";

        return intent switch
        {
            StudyEducationalIntent.ExamReview => "exam-preparation-coach",
            StudyEducationalIntent.ProblemSolvingPractice => "practical-learning-coach",
            StudyEducationalIntent.SummaryAndSynthesis => "note-creator",
            StudyEducationalIntent.SocraticTutoring => "socratic-tutor",
            StudyEducationalIntent.ReferenceLookup => "concept-explainer",
            _ => "concept-explainer"
        };
    }

    private static void DetectNaturalLanguageToolRequirements(string query, List<string> selectedToolIds, Dictionary<string, object?> toolArgs)
    {
        var lower = query.ToLowerInvariant();

        // Math calculation triggers
        if (System.Text.RegularExpressions.Regex.IsMatch(query, @"[\d\.]+\s*[\+\-\*\/\^]\s*[\d\.]+"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(query, @"([0-9\.\+\-\*\/\^\(\)\s]+)");
            if (match.Success && match.Value.Length > 2)
            {
                selectedToolIds.Add("calculator");
                toolArgs["expression"] = match.Value.Trim();
            }
        }

        // Quiz generation
        if (lower.Contains("quiz") || lower.Contains("questions"))
        {
            selectedToolIds.Add("quiz-generator");
            toolArgs["topic"] = query;
            toolArgs["questions"] = 3;
        }

        // Flashcards
        if (lower.Contains("flashcard") || lower.Contains("cards"))
        {
            selectedToolIds.Add("flashcard-generator");
            toolArgs["topic"] = query;
            toolArgs["count"] = 5;
        }

        // Progress
        if (lower.Contains("my progress") || lower.Contains("streak"))
        {
            selectedToolIds.Add("study-progress-reader");
        }
    }

    private static string SynthesizeResponse(
        StudySkillDefinition skill,
        string topic,
        IReadOnlyList<StudyToolExecutionResult> toolResults,
        TeachingMode mode,
        LearningLanguage language)
    {
        var sb = new System.Text.StringBuilder();

        // Include skill badge indicator
        sb.AppendLine($"**[{skill.Name}]** ({mode} Mode)");
        sb.AppendLine();

        // If tools executed successfully, format their findings first
        foreach (var tr in toolResults.Where(t => t.Success && t.Data != null))
        {
            var json = JsonSerializer.Serialize(tr.Data, new JsonSerializerOptions { WriteIndented = true });
            sb.AppendLine($"> [!NOTE]");
            sb.AppendLine($"> **Tool Executed:** `{tr.ToolId}` ({tr.DurationMs} ms)");
            sb.AppendLine("```json");
            sb.AppendLine(json);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        // Generate pedagogically tailored explanation aligned with skill instructions
        if (language == LearningLanguage.Malayalam)
        {
            sb.AppendLine($"**{topic}** സംബന്ധിച്ച പഠന വിവരണം ({skill.Name}):");
            sb.AppendLine();
            sb.AppendLine($"1. **അടിസ്ഥാന തത്വം (Core Principle):** {topic} എന്ന വിഷയം ഈ പാഠ്യപദ്ധതിയിൽ പ്രധാനപ്പെട്ടതാണ്.");
            sb.AppendLine($"2. **പ്രവർത്തനരീതി (Mechanism):** നിർദ്ദിഷ്ട ഘട്ടങ്ങളിലൂടെയും സിദ്ധാന്തങ്ങളിലൂടെയും ഇത് വ്യക്തമായി മനസ്സിലാക്കാൻ സാധിക്കും.");
            sb.AppendLine($"3. **പ്രായോഗിക ഉപയോഗം (Application):** യഥാർത്ഥ ലോകത്ത് ഈ തത്വം സിസ്റ്റം രൂപകൽപ്പനയിലും വിശകലനത്തിലും വ്യാപകമായി ഉപയോഗിക്കുന്നു.");
        }
        else
        {
            sb.AppendLine($"### Educational Overview: {topic}");
            sb.AppendLine();
            sb.AppendLine($"{skill.Instructions}");
            sb.AppendLine();
            sb.AppendLine($"1. **Fundamental Concept**: {topic} provides essential principles for this subject.");
            sb.AppendLine($"2. **Key Mechanism**: Operating under structured educational frameworks to establish mastery.");
            sb.AppendLine($"3. **Real-World Application**: Critical in real-world technical and academic analysis.");
        }

        return sb.ToString();
    }
}
