using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;
using OpenClaw.Shared.Study.Infrastructure;

namespace OpenClaw.Shared.Study.Commands;

/// <summary>
/// Registry for Study slash commands, aliases, validation, autocomplete, and audit tracking.
/// </summary>
public sealed class StudyCommandRegistry : IStudyCommandRegistry
{
    private readonly ConcurrentDictionary<string, StudyCommandDefinition> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _aliasMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly IStudyCommandParser _parser;
    private readonly IStudyDataStore _dataStore;

    public StudyCommandRegistry(IStudyDataStore dataStore)
        : this(null, dataStore)
    {
    }

    public StudyCommandRegistry(IStudyCommandParser? parser = null, IStudyDataStore? dataStore = null)
    {
        _parser = parser ?? new StudyCommandParser();
        _dataStore = dataStore ?? new StudyDataStore();

        RegisterDefaultCommands();
    }

    private void RegisterDefaultCommands()
    {
        // ─── 1. Learning Commands ───
        RegisterCommand(new StudyCommandDefinition("learn", "Initiate guided study on a specified topic.", CommandCategory.Learning,
            requiredSkillId: "concept-explainer",
            aliases: ["study"],
            arguments: [
                new("topic", "Topic or concept to learn", "string", isRequired: true),
                new("level", "Educational level", "choice", isRequired: false, defaultValue: "University", allowedValues: ["School", "HigherSecondary", "College", "University", "Professional", "Technical", "Research"]),
                new("mode", "Pedagogical teaching mode", "choice", isRequired: false, defaultValue: "Standard", allowedValues: ["Beginner", "Standard", "Detailed", "Advanced", "StepByStep", "Socratic", "Practical", "Revision"])
            ],
            usageExample: "/learn topic=\"Binary Search\" level=\"University\""));

        RegisterCommand(new StudyCommandDefinition("teach", "Receive step-by-step instructional explanation on a concept.", CommandCategory.Learning,
            requiredSkillId: "step-by-step-teacher",
            arguments: [
                new("topic", "Topic to teach", "string", isRequired: true),
                new("level", "Educational level", "string", isRequired: false, defaultValue: "University")
            ],
            usageExample: "/teach topic=\"Dynamic Programming\""));

        RegisterCommand(new StudyCommandDefinition("explain", "Get an in-depth pedagogical explanation of a concept.", CommandCategory.Learning,
            requiredSkillId: "concept-explainer",
            arguments: [
                new("topic", "Target topic or concept", "string", isRequired: true),
                new("level", "Depth or audience level", "choice", isRequired: false, defaultValue: "Standard", allowedValues: ["Beginner", "Standard", "Detailed", "Advanced"])
            ],
            usageExample: "/explain topic=\"Neural Networks\" level=\"Beginner\""));

        RegisterCommand(new StudyCommandDefinition("solve", "Solve an exercise or mathematical problem with steps.", CommandCategory.Learning,
            requiredSkillId: "practical-learning-coach",
            requiredTools: ["calculator", "equation-evaluator"],
            aliases: ["calc", "solution"],
            arguments: [
                new("problem", "Problem statement or equation to solve", "string", isRequired: true),
                new("stepByStep", "Include step by step explanations", "bool", isRequired: false, defaultValue: "true")
            ],
            usageExample: "/solve problem=\"2x^2+5x-3=0\""));

        RegisterCommand(new StudyCommandDefinition("revise", "Review and consolidate core ideas on a topic.", CommandCategory.Learning,
            requiredSkillId: "revision-coach",
            arguments: [
                new("topic", "Topic to revise", "string", isRequired: true)
            ],
            usageExample: "/revise topic=\"Database Normalization\""));

        RegisterCommand(new StudyCommandDefinition("translate", "Translate or adapt educational content bilingually.", CommandCategory.Learning,
            requiredSkillId: "language-tutor",
            arguments: [
                new("text", "Text or concept to translate", "string", isRequired: true),
                new("targetLanguage", "Target language (e.g. Malayalam)", "string", isRequired: false, defaultValue: "Malayalam")
            ],
            usageExample: "/translate text=\"Recursion\" targetLanguage=\"Malayalam\""));

        // ─── 2. Academic Commands ───
        RegisterCommand(new StudyCommandDefinition("notes", "Generate structured study notes and summaries.", CommandCategory.Academic,
            requiredSkillId: "note-creator",
            arguments: [
                new("chapter", "Chapter or topic name", "string", isRequired: true),
                new("format", "Output format (e.g. Outline, Cornell, Markdown)", "string", isRequired: false, defaultValue: "Cornell")
            ],
            usageExample: "/notes chapter=\"Cell Biology\" format=\"Cornell\""));

        RegisterCommand(new StudyCommandDefinition("quiz", "Generate a diagnostic quiz on a topic.", CommandCategory.Academic,
            requiredSkillId: "question-generator",
            requiredTools: ["quiz-generator"],
            aliases: ["q", "test"],
            arguments: [
                new("subject", "Subject or topic", "string", isRequired: true),
                new("questions", "Number of questions", "int", isRequired: false, defaultValue: "5")
            ],
            usageExample: "/quiz subject=\"Physics\" questions=\"5\""));

        RegisterCommand(new StudyCommandDefinition("exam", "Prepare a comprehensive practice exam.", CommandCategory.Academic,
            requiredSkillId: "exam-preparation-coach",
            arguments: [
                new("subject", "Subject or domain", "string", isRequired: true),
                new("duration", "Duration in minutes", "int", isRequired: false, defaultValue: "60")
            ],
            usageExample: "/exam subject=\"Operating Systems\" duration=\"60\""));

        RegisterCommand(new StudyCommandDefinition("evaluate", "Evaluate and score an essay or open response.", CommandCategory.Academic,
            requiredSkillId: "answer-evaluator",
            arguments: [
                new("answer", "Submitted answer or text", "string", isRequired: true),
                new("rubric", "Grading rubric or question criteria", "string", isRequired: false)
            ],
            usageExample: "/evaluate answer=\"Asymptotic notation describes upper and lower bounds.\""));

        RegisterCommand(new StudyCommandDefinition("flashcards", "Create revision flashcards for active recall.", CommandCategory.Academic,
            requiredSkillId: "flashcard-creator",
            requiredTools: ["flashcard-generator"],
            aliases: ["cards"],
            arguments: [
                new("topic", "Topic to generate flashcards for", "string", isRequired: true),
                new("count", "Number of flashcards", "int", isRequired: false, defaultValue: "5")
            ],
            usageExample: "/flashcards topic=\"Data Structures\" count=\"5\""));

        RegisterCommand(new StudyCommandDefinition("mindmap", "Generate a hierarchical conceptual mind map.", CommandCategory.Academic,
            requiredSkillId: "mind-map-creator",
            arguments: [
                new("concept", "Central concept", "string", isRequired: true),
                new("depth", "Hierarchy depth", "int", isRequired: false, defaultValue: "3")
            ],
            usageExample: "/mindmap concept=\"Computer Networks\" depth=\"3\""));

        RegisterCommand(new StudyCommandDefinition("diagram", "Generate text-based or Mermaid architectural diagrams.", CommandCategory.Academic,
            requiredSkillId: "software-architecture-mentor",
            arguments: [
                new("system", "System or architecture to diagram", "string", isRequired: true),
                new("type", "Diagram type (e.g. sequence, flowchart, class)", "string", isRequired: false, defaultValue: "flowchart")
            ],
            usageExample: "/diagram system=\"OAuth2 Flow\" type=\"sequence\""));

        // ─── 3. Research Commands ───
        RegisterCommand(new StudyCommandDefinition("research", "Conduct a focused literature or concept investigation.", CommandCategory.Research,
            requiredSkillId: "research-assistant",
            requiredTools: ["text-search", "source-metadata-reader"],
            arguments: [
                new("topic", "Research query or topic", "string", isRequired: true)
            ],
            usageExample: "/research topic=\"Renewable Energy Storage\""));

        RegisterCommand(new StudyCommandDefinition("deep-research", "Conduct an in-depth academic inquiry with citations.", CommandCategory.Research,
            requiredSkillId: "academic-research-assistant",
            arguments: [
                new("topic", "In-depth academic query", "string", isRequired: true)
            ],
            usageExample: "/deep-research topic=\"Post-Quantum Cryptography\""));

        RegisterCommand(new StudyCommandDefinition("verify", "Verify a factual claim against academic criteria.", CommandCategory.Research,
            requiredSkillId: "fact-checker",
            requiredTools: ["source-metadata-reader"],
            arguments: [
                new("claim", "Factual statement or claim", "string", isRequired: true)
            ],
            usageExample: "/verify claim=\"QuickSort has worst-case O(N^2) complexity.\""));

        RegisterCommand(new StudyCommandDefinition("cite", "Format a bibliographic citation.", CommandCategory.Research,
            requiredSkillId: "citation-assistant",
            requiredTools: ["citation-formatter", "doi-metadata-lookup"],
            arguments: [
                new("source", "Book title, article, or DOI", "string", isRequired: true),
                new("style", "Citation style (APA, MLA, Chicago, IEEE, Harvard)", "choice", isRequired: false, defaultValue: "APA", allowedValues: ["APA", "MLA", "Chicago", "IEEE", "Harvard"])
            ],
            usageExample: "/cite source=\"Clean Code by Robert C. Martin\" style=\"APA\""));

        RegisterCommand(new StudyCommandDefinition("compare", "Compare two paradigms, algorithms, or theories.", CommandCategory.Research,
            requiredSkillId: "data-analyst",
            arguments: [
                new("itemA", "First item", "string", isRequired: true),
                new("itemB", "Second item", "string", isRequired: true)
            ],
            usageExample: "/compare itemA=\"TCP\" itemB=\"UDP\""));

        // ─── 4. Productivity Commands ───
        RegisterCommand(new StudyCommandDefinition("plan", "Construct a structured revision or study timetable.", CommandCategory.Productivity,
            requiredSkillId: "study-planner",
            arguments: [
                new("goal", "Learning objective or exam goal", "string", isRequired: true),
                new("days", "Available days", "int", isRequired: false, defaultValue: "7")
            ],
            usageExample: "/plan goal=\"Pass Data Structures Exam\" days=\"14\""));

        RegisterCommand(new StudyCommandDefinition("progress", "Inspect current learning streaks and objectives.", CommandCategory.Productivity,
            requiredSkillId: "knowledge-organizer",
            requiredTools: ["study-progress-reader", "learning-objective-reader"],
            arguments: [
                new("courseId", "Course ID to inspect", "string", isRequired: false, defaultValue: "course-foundation-cs")
            ],
            usageExample: "/progress courseId=\"course-foundation-cs\""));

        RegisterCommand(new StudyCommandDefinition("project", "Scaffold a capstone study or engineering project.", CommandCategory.Productivity,
            requiredSkillId: "project-mentor",
            arguments: [
                new("title", "Project topic or goal", "string", isRequired: true)
            ],
            usageExample: "/project title=\"Distributed Key-Value Store\""));

        RegisterCommand(new StudyCommandDefinition("library", "Explore curriculum modules and resources.", CommandCategory.Productivity,
            requiredSkillId: "knowledge-organizer",
            requiredTools: ["document-text-extractor"],
            arguments: [
                new("query", "Search term", "string", isRequired: false)
            ],
            usageExample: "/library query=\"Algorithms\""));

        // ─── 5. System Commands ───
        RegisterCommand(new StudyCommandDefinition("skill", "Query or toggle Study Division skills.", CommandCategory.System,
            requiredSkillId: null,
            arguments: [
                new("action", "list, get, or toggle", "string", isRequired: true),
                new("skillId", "Target skill ID", "string", isRequired: false)
            ],
            usageExample: "/skill action=\"list\""));

        RegisterCommand(new StudyCommandDefinition("tool", "Inspect or manually run available study tools.", CommandCategory.System,
            requiredSkillId: null,
            arguments: [
                new("action", "list or execute", "string", isRequired: true),
                new("toolId", "Target tool ID", "string", isRequired: false)
            ],
            usageExample: "/tool action=\"list\""));

        RegisterCommand(new StudyCommandDefinition("help", "Display documentation on available slash commands.", CommandCategory.System,
            requiredSkillId: null,
            aliases: ["h", "?"],
            arguments: [
                new("command", "Specific command name to get help for", "string", isRequired: false)
            ],
            usageExample: "/help command=\"learn\""));
    }

    public void RegisterCommand(StudyCommandDefinition command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Name);

        var clean = command.Name.TrimStart('/');
        _commands[clean] = command;

        foreach (var alias in command.Aliases)
        {
            var cleanAlias = alias.TrimStart('/');
            _aliasMap[cleanAlias] = clean;
        }
    }

    public StudyCommandDefinition? FindCommand(string commandNameOrAlias)
    {
        if (string.IsNullOrWhiteSpace(commandNameOrAlias)) return null;
        var clean = commandNameOrAlias.TrimStart('/');

        if (_commands.TryGetValue(clean, out var cmd))
        {
            return cmd;
        }

        if (_aliasMap.TryGetValue(clean, out var primaryName) && _commands.TryGetValue(primaryName, out var primaryCmd))
        {
            return primaryCmd;
        }

        return null;
    }

    public IReadOnlyList<StudyCommandDefinition> ListCommands(CommandCategory? category = null)
    {
        var list = _commands.Values.AsEnumerable();
        if (category.HasValue)
        {
            list = list.Where(c => c.Category == category.Value);
        }

        return list.OrderBy(c => c.Category).ThenBy(c => c.Name).ToList();
    }

    public IReadOnlyList<CommandAutocompleteItem> GetAutocompleteSuggestions(string partialInput)
    {
        return _parser.GetAutocompleteSuggestions(partialInput, ListCommands());
    }

    public async Task RecordCommandExecutionAsync(CommandExecutionRecord record, string userId = "default", CancellationToken ct = default)
    {
        await _dataStore.SaveCommandExecutionAsync(record, userId, ct);
    }

    public async Task<IReadOnlyList<CommandExecutionRecord>> GetCommandExecutionHistoryAsync(
        string? commandName = null,
        string userId = "default",
        int limit = 50,
        CancellationToken ct = default)
    {
        return await _dataStore.ListCommandExecutionsAsync(commandName, userId, limit, ct);
    }
}
