using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Orchestration;

/// <summary>
/// Scaffolding engine generating pedagogical prompts, instructions, and adaptive templates
/// across 8 teaching modes, universal education levels, and multilingual contexts (English & Malayalam).
/// </summary>
public static class StudyTeachingPrompts
{
    /// <summary>
    /// Generates the complete pedagogical system prompt for the AI Tutor.
    /// </summary>
    public static string BuildSystemPrompt(
        TeachingMode mode,
        LearningLanguage language,
        EducationLevel level,
        string? activeSubject = null,
        string? activeTopic = null,
        IReadOnlyList<LearningObjective>? objectives = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are the SPARK Universal AI Tutor — an expert, adaptive, and pedagogical educator.");
        sb.AppendLine($"Target Learner Level: {FormatLevelName(level)}");
        sb.AppendLine($"Pedagogical Teaching Mode: {mode}");
        sb.AppendLine($"Language of Instruction: {language}");

        if (!string.IsNullOrWhiteSpace(activeSubject))
        {
            sb.AppendLine($"Active Subject: {activeSubject}");
        }
        if (!string.IsNullOrWhiteSpace(activeTopic))
        {
            sb.AppendLine($"Current Focus Topic: {activeTopic}");
        }

        if (objectives != null && objectives.Count > 0)
        {
            sb.AppendLine("Target Learning Objectives for this unit:");
            foreach (var obj in objectives)
            {
                sb.AppendLine($"- [{obj.TaxonomyLevel}] {obj.Description} (Mastered: {obj.IsMastered})");
            }
        }

        sb.AppendLine();
        sb.AppendLine("### PEDAGOGICAL INSTRUCTIONS");
        sb.AppendLine(GetModeGuidelines(mode));

        sb.AppendLine();
        sb.AppendLine("### LANGUAGE CONVENTIONS");
        sb.AppendLine(GetLanguageGuidelines(language));

        sb.AppendLine();
        sb.AppendLine("### AUDIENCE COGNITIVE LEVEL GUIDANCE");
        sb.AppendLine(GetLevelGuidelines(level));

        return sb.ToString();
    }

    private static string FormatLevelName(EducationLevel level) => level switch
    {
        EducationLevel.School => "School Education (Grades 6-10)",
        EducationLevel.HigherSecondary => "Higher Secondary / Pre-University (+1, +2)",
        EducationLevel.College => "College / Undergraduate Level",
        EducationLevel.University => "University / Graduate Degree Level",
        EducationLevel.Professional => "Professional & Industry Engineering",
        EducationLevel.Technical => "Technical & Vocational Applied Systems",
        EducationLevel.Research => "Scientific Research & Doctoral Inquiry",
        _ => level.ToString()
    };

    public static string GetModeGuidelines(TeachingMode mode) => mode switch
    {
        TeachingMode.Beginner =>
            "1. Focus on intuitive, physical metaphors and everyday analogies.\n" +
            "2. Keep technical jargon to an absolute minimum; define every new term immediately.\n" +
            "3. Maintain a warm, encouraging tone that builds confidence.\n" +
            "4. Provide simple, visualizable examples before discussing abstract rules.\n" +
            "5. End with a friendly, low-pressure comprehension check.",

        TeachingMode.Standard =>
            "1. Deliver structured conceptual clarity with formal definitions.\n" +
            "2. Explain the fundamental mechanics: what it is, why it exists, and how it functions.\n" +
            "3. Provide at least one clear, typical example illustrating the concept.\n" +
            "4. Highlight the primary takeaway and its connection to broader topics.\n" +
            "5. Offer a relevant follow-up question to test application.",

        TeachingMode.Detailed =>
            "1. Provide a comprehensive, in-depth academic exploration.\n" +
            "2. Trace historical motivations, theoretical roots, and internal mechanisms.\n" +
            "3. Address edge cases, constraints, exceptions, and memory/performance implications.\n" +
            "4. Compare and contrast with alternative methods or related concepts.\n" +
            "5. Include detailed architectural diagrams or structured breakdowns where helpful.",

        TeachingMode.Advanced =>
            "1. Emphasize rigorous academic, mathematical, or systems-level depth.\n" +
            "2. Include formal proofs, asymptotic bounds, invariant properties, and algorithmic proofs.\n" +
            "3. Analyze architectural tradeoffs, hardware cache interactions, and state-of-the-art literature.\n" +
            "4. Assume the learner possesses foundational fluency; avoid redundant beginner recaps.\n" +
            "5. Challenge the learner with complex optimization problems or edge scenarios.",

        TeachingMode.StepByStep =>
            "1. Break the concept or process down into strictly ordered, numbered phases (Phase 1, Phase 2, etc.).\n" +
            "2. Ensure each step accomplishes a single atomic milestone.\n" +
            "3. Explain what happens at each step and why that step is necessary.\n" +
            "4. Include an actionable checkpoint or verification check between steps.\n" +
            "5. Summarize the workflow so the learner can reproduce it independently.",

        TeachingMode.Socratic =>
            "1. NEVER give the direct answer or solution immediately.\n" +
            "2. Respond with thoughtful, guiding counter-questions that lead the student to discover the answer.\n" +
            "3. Acknowledge the student's intuition, validate correct fragments, and gently highlight logical gaps.\n" +
            "4. Scaffold the reasoning: ask about known axioms, observe patterns, and deduce consequences.\n" +
            "5. Foster autonomous critical thinking through interactive dialogue.",

        TeachingMode.Practical =>
            "1. Ground all explanations in concrete, hands-on application and real-world utility.\n" +
            "2. Provide clean, well-commented, production-quality code snippets or practical exercises.\n" +
            "3. Highlight real-world industry use cases, common pitfalls, and debugging tips.\n" +
            "4. Include a mini challenge or 'try it yourself' exercise.\n" +
            "5. Emphasize maintainability, best practices, and operational robustness.",

        TeachingMode.Revision =>
            "1. Deliver high-density, concise bullet points for rapid review and exam preparation.\n" +
            "2. Highlight essential formulas, definitions, and cheat-sheet rules.\n" +
            "3. Focus on high-yield exam topics and frequently tested distinctions.\n" +
            "4. Use memory hooks, acronyms, or quick comparison tables.\n" +
            "5. Conclude with a rapid-fire quiz question to verify recall.",

        _ => "Deliver clear, pedagogical instruction suited to the learner's inquiry."
    };

    public static string GetLanguageGuidelines(LearningLanguage language) => language switch
    {
        LearningLanguage.English =>
            "Deliver explanations in clear, precise, and articulate English. Maintain academic and technical rigor.",

        LearningLanguage.Malayalam =>
            "1. Deliver all instructional explanations, conceptual analogies, and pedagogical guidance in natural, fluent Malayalam (മലയാളം).\n" +
            "2. CRITICAL BILINGUAL CONVENTION: Retain standard English technical terms, function names, keywords, and equations in Latin script or bracketed format alongside Malayalam (e.g., 'Recursion (റിക്കർഷൻ)', 'Time Complexity', 'Binary Search Tree', 'Big-O notation').\n" +
            "3. Ensure the Malayalam grammar is natural, respectful, and educational (ആദരവോടെയും ലളിതമായും വിശദീകരിക്കുക).\n" +
            "4. Provide clear examples that resonate with Kerala / Indian educational contexts while maintaining universal technical standards.",

        _ => "Deliver in clear English."
    };

    public static string GetLevelGuidelines(EducationLevel level) => level switch
    {
        EducationLevel.School =>
            "Target secondary school students (Grades 6-10). Use relatable everyday life examples, avoid high-level mathematics unless explained from scratch, and maintain an engaging, positive tone.",

        EducationLevel.HigherSecondary =>
            "Target +1 and +2 (pre-university) students. Align with foundational science, mathematics, and board/entrance curricula. Emphasize rigorous principles alongside practical problem solving.",

        EducationLevel.College or EducationLevel.University =>
            "Target undergraduate and graduate students. Emphasize theoretical correctness, algorithmic analysis, design patterns, and formal disciplinary terminology.",

        EducationLevel.Professional or EducationLevel.Technical =>
            "Target working practitioners and engineers. Emphasize real-world architecture, industry best practices, performance, trade-offs, and maintainability.",

        EducationLevel.Research =>
            "Target researchers, scientists, and doctoral candidates. Emphasize empirical evidence, mathematical proofs, asymptotic limits, and open research directions.",

        _ => "Target general academic learners."
    };

    /// <summary>
    /// Generates high-fidelity local pedagogical fallback responses when offline or when model routing fails.
    /// </summary>
    public static string GenerateLocalPedagogicalResponse(
        string query,
        TeachingMode mode,
        LearningLanguage language,
        EducationLevel level,
        string? activeTopic = null)
    {
        var topic = !string.IsNullOrWhiteSpace(activeTopic) ? activeTopic : query;

        if (language == LearningLanguage.Malayalam)
        {
            return GenerateMalayalamLocalResponse(topic, mode, level);
        }

        return GenerateEnglishLocalResponse(topic, mode, level);
    }

    private static string GenerateEnglishLocalResponse(string topic, TeachingMode mode, EducationLevel level) => mode switch
    {
        TeachingMode.Beginner =>
            $"### Getting Started with {topic}\n\n" +
            $"Think of **{topic}** like a familiar everyday system. Imagine a well-organized library where every book has its exact shelf location.\n\n" +
            $"1. **The Big Idea**: At its heart, {topic} is designed to make problem-solving simple and predictable.\n" +
            $"2. **Why It Matters**: Without it, we would have to redo work from scratch every single time.\n" +
            $"3. **A Simple Example**: When you follow a recipe step by step, you are using the core philosophy behind {topic}.\n\n" +
            $"**Quick Check**: Does this analogy make sense, or would you like to explore another everyday comparison?",

        TeachingMode.Standard =>
            $"### Core Concept: {topic}\n\n" +
            $"**Definition**: {topic} represents a foundational construct in modern computational and scientific systems.\n\n" +
            $"#### Key Pillars\n" +
            $"- **Structure**: How the underlying elements are logically organized.\n" +
            $"- **Operation**: The operational rules that govern its state transitions.\n" +
            $"- **Efficiency**: The computational and resources guarantees it delivers.\n\n" +
            $"#### Practical Illustration\n" +
            $"Consider a standard processing workflow: when an input is provided, {topic} ensures deterministic, validated progression toward the target output.\n\n" +
            $"**Key Takeaway**: Understanding {topic} provides the building blocks for constructing scalable, robust architectures.",

        TeachingMode.Detailed =>
            $"### Deep-Dive Analysis: {topic}\n\n" +
            $"#### 1. Architectural Foundations & Motivation\n" +
            $"{topic} addresses the challenge of managing complexity under scale. Historically, simpler naive approaches suffered from quadratic degradation or state corruption.\n\n" +
            $"#### 2. Internal Mechanics & Invariants\n" +
            $"- **Invariant Enforcement**: Guarantees that internal invariants remain valid before and after every state mutation.\n" +
            $"- **Boundary Behavior**: Handles edge cases such as empty inputs, maximal bounds, and concurrent access gracefully.\n\n" +
            $"#### 3. Complexity & Trade-Offs\n" +
            $"- **Time vs. Space**: Balancing logarithmic execution time against supplementary working memory requirements.\n" +
            $"- **Extensibility**: How this pattern integrates into distributed or modular topologies.",

        TeachingMode.Advanced =>
            $"### Advanced Treatise: {topic}\n\n" +
            $"**Rigorous Formulation**: Let S represent the system state space governed by {topic}.\n\n" +
            $"#### Theoretical Invariants & Complexity Bounds\n" +
            $"- **Asymptotic Bounds**: The worst-case upper bound is strictly bounded by O(log n) under balanced conditions.\n" +
            $"- **Formal Correctness**: Invariant maintenance can be proven by structural induction across all mutation transitions.\n" +
            $"- **Systems Nuance**: Cache line locality, branch prediction hints, and memory barrier synchronization play decisive roles in real hardware throughput.\n\n" +
            $"*Challenge Problem*: Under what specific boundary conditions would the amortized guarantees of {topic} degrade into linear behavior?",

        TeachingMode.StepByStep =>
            $"### Step-by-Step Walkthrough: {topic}\n\n" +
            $"Follow these structured sequential phases to understand and apply {topic}:\n\n" +
            $"1. **Phase 1: Input Identification & Initialization**\n" +
            $"   - Define your baseline state and verify prerequisite invariants.\n" +
            $"2. **Phase 2: Core Transformation Loop**\n" +
            $"   - Execute the primary iterative or recursive rule on the working dataset.\n" +
            $"3. **Phase 3: Boundary & Terminating Evaluation**\n" +
            $"   - Evaluate whether the convergence criterion or base case has been reached.\n" +
            $"4. **Phase 4: Output Synthesis & Verification**\n" +
            $"   - Extract the synthesized result and run a sanity verification check.\n\n" +
            $"*Next Action*: Would you like to execute Phase 1 together with a sample input?",

        TeachingMode.Socratic =>
            $"### Exploring {topic}\n\n" +
            $"That's a profound area to investigate regarding **{topic}**.\n\n" +
            $"Before I provide an explanation, let's reflect together:\n\n" +
            $"1. If you had to design a solution for this problem from first principles, what would be your very first intuition?\n" +
            $"2. What constraints or challenges do you think would arise if we simply handled it naively?\n" +
            $"3. How does this connect to what we already understand about baseline structural ordering?\n\n" +
            $"Take a moment to share your thought process on any of these points!",

        TeachingMode.Practical =>
            $"### Practical Hands-On Guide: {topic}\n\n" +
            $"Here is how **{topic}** is engineered in real-world production environments:\n\n" +
            "```csharp\n" +
            $"// Production pattern for {topic}\n" +
            $"public class {topic.Replace(" ", "")}Processor\n" +
            "{\n" +
            "    public void Execute()\n" +
            "    {\n" +
            "        // 1. Validate preconditions\n" +
            "        // 2. Perform core execution\n" +
            "        // 3. Return verified outcome\n" +
            "    }\n" +
            "}\n" +
            "```\n\n" +
            $"#### Industry Best Practices\n" +
            $"- **Defensive Verification**: Always sanitize edge cases at API boundaries.\n" +
            $"- **Instrumentation**: Monitor latency and error rates in telemetry.\n" +
            $"- **Lab Exercise**: Try modifying the snippet above to handle concurrent invocations safely.",

        TeachingMode.Revision =>
            $"### Rapid Revision Cheat-Sheet: {topic}\n\n" +
            $"- **Core Definition**: Fundamental mechanism governing {topic}.\n" +
            $"- **Key Formula / Rule**: Invariant preservation across state transitions.\n" +
            $"- **Common Pitfall**: Overlooking edge cases or boundary conditions.\n" +
            $"- **High-Yield Exam Point**: Be prepared to contrast this directly with its closest alternative.\n\n" +
            $"**Quick Recall Drill**: Can you state the primary advantage of {topic} in a single sentence?",

        _ => $"Structured overview of {topic} for {level} level."
    };

    private static string GenerateMalayalamLocalResponse(string topic, TeachingMode mode, EducationLevel level) => mode switch
    {
        TeachingMode.Beginner =>
            $"### {topic} - ലളിതമായ തുടക്കം\n\n" +
            $"**{topic}** എന്ന ആശയത്തെ നമുക്ക് നിത്യജീവിതത്തിലെ ഒരു ലളിതമായ ഉദാഹരണത്തിലൂടെ മനസ്സിലാക്കാം.\n\n" +
            $"1. **പ്രധാന ആശയം**: ഒരു വലിയ ജോലിയെ ചെറിയ ഭാഗങ്ങളാക്കി എളുപ്പത്തിൽ പരിഹരിക്കാൻ {topic} സഹായിക്കുന്നു.\n" +
            $"2. **പ്രാധാന്യം**: ഇത് ഉപയോഗിക്കുന്നതിലൂടെ നമ്മുടെ സമയവും അദ്ധ്വാനവും ഗണ്യമായി ലാഭിക്കാം.\n" +
            $"3. **ഉദാഹരണം**: പുസ്തകങ്ങൾ അക്ഷരമാലാ ക്രമത്തിൽ ക്രമീകരിക്കുമ്പോൾ ആവശ്യമുള്ള പുസ്തകം വേഗത്തിൽ കണ്ടെത്താൻ സാധിക്കുന്നത് പോലെയാണിത്.\n\n" +
            $"ഈ വിശദീകരണം വ്യക്തമാണോ? സംശയമുണ്ടെങ്കിൽ ദയവായി ചോദിക്കൂ!",

        TeachingMode.Standard =>
            $"### അടിസ്ഥാന പാഠം: {topic}\n\n" +
            $"**നിർവ്വചനം (Definition)**: കമ്പ്യൂട്ടർ സയൻസിലും സാങ്കേതിക വിദ്യയിലും അതിപ്രധാനമായ ഒരു അടിസ്ഥാന ആശയമാണ് **{topic}**.\n\n" +
            $"#### പ്രധാന ഘടകങ്ങൾ\n" +
            $"- **ഘടന (Structure)**: ഡാറ്റയും നിർദ്ദേശങ്ങളും ക്രമീകരിക്കുന്ന രീതി.\n" +
            $"- **പ്രവർത്തന രീതി (Mechanism)**: ഇൻപുട്ട് സ്വീകരിച്ച് ഔട്ട്പുട്ടിലേക്ക് എത്തുന്ന പ്രക്രിയ.\n" +
            $"- **കാര്യക്ഷമത (Efficiency)**: സിസ്റ്റത്തിന്റെ വേഗതയും റിസോഴ്സ് ഉപയോഗവും.\n\n" +
            $"**പ്രധാന നിഗമനം (Key Takeaway)**: {topic} കൃത്യമായി മനസ്സിലാക്കുന്നത് ഉയർന്ന നിലവാരമുള്ള സോഫ്റ്റ്‌വെയറുകൾ നിർമ്മിക്കാൻ നിങ്ങളെ പ്രാപ്തമാക്കും.",

        TeachingMode.Detailed =>
            $"### ആഴത്തിലുള്ള പഠനം: {topic}\n\n" +
            $"#### 1. സൈദ്ധാന്തിക പശ്ചാത്തലം (Theoretical Background)\n" +
            $"{topic} വികസിപ്പിച്ചെടുത്തത് സിസ്റ്റങ്ങളുടെ സങ്കീർണ്ണതയും പരിമിതികളും ലഘൂകരിക്കാനാണ്.\n\n" +
            $"#### 2. ആന്തരിക പ്രവർത്തനങ്ങൾ (Internal Mechanics)\n" +
            $"- **ഇൻവേരിയന്റുകൾ (Invariants)**: ഓരോ ഘട്ടത്തിലും വ്യവസ്ഥകൾ പാലിക്കപ്പെടുന്നുവെന്ന് ഉറപ്പുവരുത്തുന്നു.\n" +
            $"- **എഡ്ജ് കേസുകൾ (Edge Cases)**: ശൂന്യമായ ഡാറ്റയും പരമാവധി പരിധികളും കാര്യക്ഷമമായി കൈകാര്യം ചെയ്യുന്നു.\n\n" +
            $"#### 3. സമയവും മെമ്മറിയും (Time & Space Complexity)\n" +
            $"മെമ്മറി ഉപയോഗവും പ്രവർത്തന സമയവും തമ്മിലുള്ള സന്തുലിതാവസ്ഥ {topic} നിലനിർത്തുന്നു.",

        TeachingMode.Advanced =>
            $"### അഡ്വാൻസ്ഡ് ലെവൽ വിശകലനം: {topic}\n\n" +
            $"**സിദ്ധാന്തവും ഗണിതശാസ്ത്രപരമായ വിശകലനവും**:\n\n" +
            $"- **അസിംപ്റ്റോട്ടിക് ബൗണ്ട്സ് (Asymptotic Bounds)**: ഏറ്റവും മോശം സാഹചര്യത്തിലും (Worst-case) സമയം കർശനമായി നിയന്ത്രിക്കപ്പെടുന്നു.\n" +
            $"- **സിസ്റ്റം ആർക്കിടെക്ചർ (System Architecture)**: സിപിയു കാഷെ (CPU Cache Locality), മെമ്മറി അലൈൻമെന്റ് എന്നിവയിലെ സ്വാധീനം.\n" +
            $"- **മാത്തമാറ്റിക്കൽ പ്രൂഫ് (Mathematical Proof)**: ഇൻഡക്ഷൻ (Induction) രീതിയിലൂടെ ഇതിന്റെ കൃത്യത തെളിയിക്കാൻ സാധിക്കും.\n\n" +
            $"*ചോദ്യം*: ഏത് പ്രത്യേക സാഹചര്യത്തിലാണ് {topic}-ന്റെ കാര്യക്ഷമതയിൽ വ്യതിയാനം സംഭവിക്കുന്നത്?",

        TeachingMode.StepByStep =>
            $"### ഘട്ടം ഘട്ടമായുള്ള ഗൈഡ് (Step-by-Step): {topic}\n\n" +
            $"ഇനിപ്പറയുന്ന ഘട്ടങ്ങളിലൂടെ {topic} പ്രാവർത്തികമാക്കാം:\n\n" +
            $"1. **ഘട്ടം 1: പ്രാരംഭ അവസ്ഥ പരിശോധിക്കുക (Initialization)**\n" +
            $"   - ഇൻപുട്ട് ഡാറ്റ കൃത്യമാണെന്ന് പരിശോധിക്കുക.\n" +
            $"2. **ഘട്ടം 2: പ്രധാന പ്രക്രിയ നടപ്പിലാക്കുക (Core Processing)**\n" +
            $"   - നിശ്ചിത അൽഗോരിതം അല്ലെങ്കിൽ ലോജിക് പ്രവർത്തിപ്പിക്കുക.\n" +
            $"3. **ഘട്ടം 3: ഫലം സ്ഥിരീകരിക്കുക (Verification)**\n" +
            $"   - ലഭിച്ച ഔട്ട്പുട്ട് നിശ്ചിത മാനദണ്ഡങ്ങൾ പാലിക്കുന്നുണ്ടോ എന്ന് പരിശോധിക്കുക.\n\n" +
            $"ആദ്യ ഘട്ടം ഒരുമിച്ച് പരീക്ഷിക്കാൻ നിങ്ങൾ തയ്യാറാണോ?",

        TeachingMode.Socratic =>
            $"### ചിന്തിച്ചു കണ്ടെത്താം (Socratic Dialogue): {topic}\n\n" +
            $"**{topic}** എന്ന വിഷയത്തെക്കുറിച്ച് താങ്കൾ ചോദിച്ച ചോദ്യം വളരെ ചിന്തോദ്ദീപകമാണ്.\n\n" +
            $"നേരിട്ടുള്ള ഉത്തരം പറയുന്നതിന് മുൻപ്, നമുക്ക് ഒന്നിച്ച് ആലോചിക്കാം:\n\n" +
            $"1. ഈ പ്രശ്നം പരിഹരിക്കാൻ ആദ്യമായി താങ്കളുടെ മനസ്സിൽ വരുന്ന മാർഗ്ഗം എന്താണ്?\n" +
            $"2. സാധാരണ രീതിയിൽ ഇത് ചെയ്യുമ്പോൾ എന്തെല്ലാം തടസ്സങ്ങൾ ഉണ്ടാകാൻ സാധ്യതയുണ്ട്?\n" +
            $"3. ഇതിന് മുൻപ് നാം പഠിച്ച ഏതെങ്കിലും തത്വവുമായി ഇതിന് സാമ്യമുണ്ടോ?\n\n" +
            $"താങ്കളുടെ അഭിപ്രായം ദയവായി പങ്കുവെക്കൂ!",

        TeachingMode.Practical =>
            $"### പ്രായോഗിക പരിശീലനം (Practical Guide): {topic}\n\n" +
            $"യഥാർത്ഥ സോഫ്റ്റ്‌വെയർ വികസനത്തിൽ **{topic}** എങ്ങനെ ഉപയോഗിക്കുന്നുവെന്ന് നോക്കാം:\n\n" +
            "```csharp\n" +
            $"// {topic} പ്രായോഗിക ഉദാഹരണം\n" +
            $"public class {topic.Replace(" ", "")}Demo\n" +
            "{\n" +
            "    public void Process()\n" +
            "    {\n" +
            "        // പ്രായോഗിക കോഡ് ഇവിടെ ചേർക്കുന്നു\n" +
            "    }\n" +
            "}\n" +
            "```\n\n" +
            $"**ശ്രദ്ധിക്കേണ്ട കാര്യങ്ങൾ**:\n" +
            $"- കോഡിന്റെ ക്ലീൻ ആർക്കിടെക്ചർ നിലനിർത്തുക.\n" +
            $"- എറർ ഹാൻഡിലിംഗ് കൃത്യമായി ഉൾപ്പെടുത്തുക.",

        TeachingMode.Revision =>
            $"### ദ്രുത റിവിഷൻ (Quick Revision): {topic}\n\n" +
            $"- **നിർവ്വചനം**: {topic}-ന്റെ പ്രധാന നിർവ്വചനവും ലക്ഷ്യവും.\n" +
            $"- **പ്രധാന ഫോർമുല / സൂത്രവാക്യം**: പരീക്ഷയ്ക്ക് ഓർത്തിരിക്കേണ്ട പ്രധാന സമവാക്യങ്ങൾ.\n" +
            $"- **സാധാരണയായി സംഭവിക്കുന്ന തെറ്റുകൾ**: ശ്രദ്ധിക്കേണ്ട പ്രധാന ഘടകങ്ങൾ.\n\n" +
            $"**പരീക്ഷണ ചോദ്യം**: {topic}-ന്റെ ഏറ്റവും വലിയ ഗുണം ഒരു വാക്യത്തിൽ പറയാമോ?",

        _ => $"{level} തലത്തിലുള്ള {topic} പഠന സഹായം."
    };
}
