using System.Collections.Generic;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Skills;

/// <summary>
/// Catalogs all 35+ verified built-in pedagogical capabilities for the SPARK Study Division.
/// </summary>
public static class StudyBuiltInSkills
{
    public static IReadOnlyList<StudySkillDefinition> GetAll() =>
    [
        // ══════════════════════════════════════════════════════════════
        // 1. Learning Skills (10)
        // ══════════════════════════════════════════════════════════════
        new(
            id: "personal-tutor",
            name: "Personal Tutor",
            description: "Empathetic, holistic one-on-one guide adapting to learner pace and goals.",
            category: SkillCategory.Learning,
            version: "1.0.0",
            instructions: "Act as an adaptive, encouraging personal tutor. Assess student understanding, provide immediate supportive feedback, and tailor analogies to the student's background.",
            triggerConditions: ["tutor me", "help me learn", "personal guide", "study with me"],
            requiredContext: ["activeSession", "learningPath"],
            allowedTools: ["study-progress-reader", "flashcard-generator"],
            modelPreference: "balanced",
            tags: ["tutor", "pedagogy", "adaptive"]
        ),
        new(
            id: "subject-teacher",
            name: "Subject Teacher",
            description: "Structured curriculum-aligned subject lecturer following formal syllabus guidelines.",
            category: SkillCategory.Learning,
            version: "1.0.0",
            instructions: "Deliver formal subject lectures adhering strictly to defined syllabus topics, educational milestones, and learning objectives.",
            triggerConditions: ["teach me", "syllabus", "subject lecture", "course overview"],
            requiredContext: ["activeCourse", "learningObjectives"],
            allowedTools: ["learning-objective-reader", "document-text-extractor"],
            modelPreference: "balanced",
            tags: ["lecture", "formal", "syllabus"]
        ),
        new(
            id: "concept-explainer",
            name: "Concept Explainer",
            description: "Deep conceptual unbundling utilizing the Feynman technique and progressive abstraction.",
            category: SkillCategory.Learning,
            version: "1.0.0",
            instructions: "Deconstruct complex topics into intuitive principles. Start with an accessible intuitive analogy, explain the underlying mechanism, and formalize with rigor.",
            triggerConditions: ["explain", "what is", "how does it work", "concept breakdown"],
            requiredContext: ["currentTopic"],
            allowedTools: ["calculator", "text-search"],
            modelPreference: "balanced",
            tags: ["explanation", "feynman", "intuition"]
        ),
        new(
            id: "step-by-step-teacher",
            name: "Step-by-Step Teacher",
            description: "Methodical procedural breakdown with intermediate checkpoints and validation questions.",
            category: SkillCategory.Learning,
            version: "1.0.0",
            instructions: "Break every process or problem down into numbered sequential stages. Never proceed past a step without explaining why that specific step is necessary.",
            triggerConditions: ["step by step", "walk through", "one step at a time", "procedural"],
            requiredContext: ["currentTopic"],
            allowedTools: ["calculator", "code-formatter"],
            modelPreference: "balanced",
            tags: ["procedural", "step-by-step", "clear"]
        ),
        new(
            id: "socratic-tutor",
            name: "Socratic Tutor",
            description: "Guided discovery through inquiry, leading questions, and critical hypothesis testing.",
            category: SkillCategory.Learning,
            version: "1.0.0",
            instructions: "Never reveal direct solutions immediately. Respond with targeted, probing questions that illuminate contradictions and guide the learner toward finding the insight themselves.",
            triggerConditions: ["guide me", "don't tell me", "socratic", "why does", "lead me to answer"],
            requiredContext: ["currentTopic", "recentErrors"],
            allowedTools: ["quiz-generator"],
            modelPreference: "complex-reasoning",
            tags: ["socratic", "inquiry", "critical-thinking"]
        ),
        new(
            id: "beginner-teacher",
            name: "Beginner Teacher",
            description: "Zero-assumption onboarding eliminating jargon and establishing baseline foundations.",
            category: SkillCategory.Learning,
            version: "1.0.0",
            instructions: "Assume zero prior knowledge. Define every technical term immediately upon introduction. Use familiar real-world comparisons and encourage questions.",
            triggerConditions: ["im a beginner", "simple explanation", "from scratch", "for dummies", "eli5"],
            requiredContext: ["currentTopic"],
            allowedTools: ["flashcard-generator"],
            modelPreference: "fast",
            tags: ["beginner", "foundations", "accessible"]
        ),
        new(
            id: "advanced-teacher",
            name: "Advanced Teacher",
            description: "Rigorous academic and professional mastery analyzing edge cases, invariants, and trade-offs.",
            category: SkillCategory.Learning,
            version: "1.0.0",
            instructions: "Speak as a senior researcher or principal engineer. Focus on formal proofs, time/space trade-offs, internal implementation details, and failure modes.",
            triggerConditions: ["advanced", "deep dive", "under the hood", "rigorous", "internals"],
            requiredContext: ["currentTopic"],
            allowedTools: ["code-analyzer", "matrix-operations"],
            modelPreference: "complex-reasoning",
            tags: ["advanced", "rigorous", "deep-dive"]
        ),
        new(
            id: "revision-coach",
            name: "Revision Coach",
            description: "Active recall, spaced repetition prompts, and high-yield summary synthesis.",
            category: SkillCategory.Learning,
            version: "1.0.0",
            instructions: "Target high-yield concepts. Synthesize key summary cheat-sheets, test recall with quick drill prompts, and identify weak spots.",
            triggerConditions: ["revise", "revision", "summary review", "quick recap", "cram"],
            requiredContext: ["courseId", "masteredObjectives"],
            allowedTools: ["flashcard-generator", "learning-objective-reader"],
            modelPreference: "balanced",
            tags: ["revision", "recall", "spaced-repetition"]
        ),
        new(
            id: "language-tutor",
            name: "Language Tutor",
            description: "Bilingual English and Malayalam instruction preserving technical terms with vernacular clarity.",
            category: SkillCategory.Learning,
            version: "1.0.0",
            instructions: "Facilitate bilingual learning between English and Malayalam. Explain core ideas naturally in Malayalam while keeping standard technical terms in English.",
            triggerConditions: ["malayalam", "മലയാളം", "translate", "language switch", "bilingual"],
            requiredContext: ["activeLanguage"],
            allowedTools: ["text-search"],
            modelPreference: "balanced",
            tags: ["language", "malayalam", "bilingual"]
        ),
        new(
            id: "practical-learning-coach",
            name: "Practical Learning Coach",
            description: "Hands-on problem solving, labs, and interactive exercise execution.",
            category: SkillCategory.Learning,
            version: "1.0.0",
            instructions: "Emphasize actionable practice. Give concrete exercises, inspect user attempts, explain how code or formulas run in production, and foster builder intuition.",
            triggerConditions: ["practice", "exercise", "lab", "hands-on", "build something"],
            requiredContext: ["currentTopic"],
            allowedTools: ["calculator", "equation-evaluator", "scientific-calculator", "statistics-calculator", "code-formatter", "code-analyzer"],
            modelPreference: "coding",
            tags: ["hands-on", "practical", "lab"]
        ),

        // ══════════════════════════════════════════════════════════════
        // 2. Academic Skills (9)
        // ══════════════════════════════════════════════════════════════
        new(
            id: "note-creator",
            name: "Note Creator",
            description: "Transforms text into Cornell notes, summary outlines, and cheat sheets.",
            category: SkillCategory.Academic,
            version: "1.0.0",
            instructions: "Convert provided material into high-retention notes: cue column, structured concise notes, key equations, and a 2-sentence bottom summary.",
            triggerConditions: ["make notes", "cornell notes", "cheat sheet", "bullet notes"],
            requiredContext: ["lessonText"],
            allowedTools: ["document-text-extractor", "text-search"],
            modelPreference: "balanced",
            tags: ["notes", "cornell", "study-aid"]
        ),
        new(
            id: "chapter-explainer",
            name: "Chapter Explainer",
            description: "Comprehensive chapter synthesis mapping themes, key takeaways, and discussion prompts.",
            category: SkillCategory.Academic,
            version: "1.0.0",
            instructions: "Break down an entire book or syllabus chapter into central themes, key arguments, formulas, and reflective discussion points.",
            triggerConditions: ["explain chapter", "chapter breakdown", "textbook summary"],
            requiredContext: ["chapterOutline"],
            allowedTools: ["document-text-extractor", "pdf-reader"],
            modelPreference: "balanced",
            tags: ["textbook", "chapter", "academic"]
        ),
        new(
            id: "question-generator",
            name: "Question Generator",
            description: "Generates Bloom's taxonomy questions across all 6 cognitive difficulty levels.",
            category: SkillCategory.Academic,
            version: "1.0.0",
            instructions: "Formulate balanced assessment questions across Bloom's levels: Remembering, Understanding, Applying, Analyzing, Evaluating, and Creating.",
            triggerConditions: ["generate questions", "quiz me", "create test", "assessment"],
            requiredContext: ["learningObjectives"],
            allowedTools: ["quiz-generator"],
            modelPreference: "balanced",
            tags: ["assessment", "questions", "blooms"]
        ),
        new(
            id: "answer-evaluator",
            name: "Answer Evaluator",
            description: "Grades student answers with rubric criteria, highlighting strengths and corrections.",
            category: SkillCategory.Academic,
            version: "1.0.0",
            instructions: "Evaluate user responses against objective rubrics. Highlight what was accurate, point out specific omissions, and provide a model answer.",
            triggerConditions: ["check my answer", "grade this", "evaluate essay", "score response"],
            requiredContext: ["activeQuestion", "rubric"],
            allowedTools: ["document-text-extractor"],
            modelPreference: "complex-reasoning",
            tags: ["grading", "rubric", "evaluation"]
        ),
        new(
            id: "grammar-coach",
            name: "Grammar Coach",
            description: "Analyzes sentence mechanics, clarity, voice, and academic style.",
            category: SkillCategory.Academic,
            version: "1.0.0",
            instructions: "Diagnose grammatical errors, awkward phrasing, and passive voice. Suggest concise academic revisions while explaining the underlying grammar rule.",
            triggerConditions: ["check grammar", "fix writing", "proofread", "syntax check"],
            requiredContext: ["userSubmission"],
            allowedTools: ["document-metadata-reader"],
            modelPreference: "fast",
            tags: ["grammar", "writing", "style"]
        ),
        new(
            id: "essay-assistant",
            name: "Essay Assistant",
            description: "Thesis formulation, evidence structuring, counterargument planning, and outlines.",
            category: SkillCategory.Academic,
            version: "1.0.0",
            instructions: "Guide students through academic essay development: sharpen thesis statements, structure paragraphs around claims and evidence, and preempt counterarguments.",
            triggerConditions: ["essay help", "thesis statement", "essay outline", "term paper"],
            requiredContext: ["essayTopic"],
            allowedTools: ["citation-formatter"],
            modelPreference: "complex-reasoning",
            tags: ["essay", "thesis", "academic-writing"]
        ),
        new(
            id: "literature-analyst",
            name: "Literature Analyst",
            description: "Analyzes textual motifs, character arcs, historical context, and thematic tension.",
            category: SkillCategory.Academic,
            version: "1.0.0",
            instructions: "Analyze literary works exploring recurring motifs, symbolism, character psychology, and socio-historical context.",
            triggerConditions: ["analyze poem", "literary devices", "theme analysis", "book review"],
            requiredContext: ["literaryWork"],
            allowedTools: ["text-search"],
            modelPreference: "complex-reasoning",
            tags: ["literature", "criticism", "humanities"]
        ),
        new(
            id: "homework-assistant",
            name: "Homework Assistant",
            description: "Clarifies ambiguous problem statements and guides assignments without violating honor codes.",
            category: SkillCategory.Academic,
            version: "1.0.0",
            instructions: "Help student dissect problem requirements, identify applicable laws or formulas, and review their work without simply doing their homework for them.",
            triggerConditions: ["homework", "assignment help", "stuck on problem"],
            requiredContext: ["problemStatement"],
            allowedTools: ["calculator", "equation-evaluator"],
            modelPreference: "balanced",
            tags: ["homework", "academic-integrity", "guidance"]
        ),
        new(
            id: "exam-preparation-coach",
            name: "Exam Preparation Coach",
            description: "High-stakes exam strategy, time management, mock tests, and stress reduction.",
            category: SkillCategory.Academic,
            version: "1.0.0",
            instructions: "Structure high-stakes test prep schedules: simulate exam pacing, recommend question-triage strategies, and identify critical score-maximizing topics.",
            triggerConditions: ["exam prep", "finals", "test strategy", "mock exam"],
            requiredContext: ["examDate", "syllabus"],
            allowedTools: ["quiz-generator", "study-progress-reader"],
            modelPreference: "balanced",
            tags: ["exam", "strategy", "mock-test"]
        ),

        // ══════════════════════════════════════════════════════════════
        // 3. Research Skills (8)
        // ══════════════════════════════════════════════════════════════
        new(
            id: "research-assistant",
            name: "Research Assistant",
            description: "Synthesizes literature, drafts research questions, and organizes findings.",
            category: SkillCategory.Research,
            version: "1.0.0",
            instructions: "Assist in literature review synthesis, formulation of testable hypotheses, and structuring research methodologies.",
            triggerConditions: ["research topic", "literature scan", "research question"],
            requiredContext: ["researchDomain"],
            allowedTools: ["source-metadata-reader", "text-search"],
            modelPreference: "complex-reasoning",
            tags: ["research", "methodology", "literature"]
        ),
        new(
            id: "academic-research-assistant",
            name: "Academic Research Assistant",
            description: "Peer-review rigor, methodology evaluation, and formal scholarly synthesis.",
            category: SkillCategory.Research,
            version: "1.0.0",
            instructions: "Conduct rigorous academic critiques of methodology, statistical validity, confounding variables, and ethical considerations.",
            triggerConditions: ["academic paper", "peer review", "methodology critique"],
            requiredContext: ["paperDetails"],
            allowedTools: ["doi-metadata-lookup", "citation-formatter"],
            modelPreference: "complex-reasoning",
            tags: ["scholarly", "peer-review", "rigor"]
        ),
        new(
            id: "paper-analyzer",
            name: "Paper Analyzer",
            description: "Summarizes paper contributions, methodology, datasets, and benchmark results.",
            category: SkillCategory.Research,
            version: "1.0.0",
            instructions: "Dissect research papers into key contribution, baseline comparisons, experimental setup, and remaining open challenges.",
            triggerConditions: ["read paper", "paper summary", "analyze paper"],
            requiredContext: ["paperAbstract"],
            allowedTools: ["pdf-reader", "document-text-extractor"],
            modelPreference: "complex-reasoning",
            tags: ["paper", "contributions", "summary"]
        ),
        new(
            id: "literature-reviewer",
            name: "Literature Reviewer",
            description: "Maps research clusters, identifies research gaps, and contrasts conflicting studies.",
            category: SkillCategory.Research,
            version: "1.0.0",
            instructions: "Map the landscape of existing scholarship on a topic, group studies by methodological paradigm, and spotlight unaddressed research gaps.",
            triggerConditions: ["literature review", "state of the art", "research gap"],
            requiredContext: ["topic"],
            allowedTools: ["source-metadata-reader", "citation-formatter"],
            modelPreference: "complex-reasoning",
            tags: ["state-of-the-art", "clusters", "gaps"]
        ),
        new(
            id: "fact-checker",
            name: "Fact Checker",
            description: "Verifies claims against scholarly consensus, empirical evidence, and sources.",
            category: SkillCategory.Research,
            version: "1.0.0",
            instructions: "Audit claims against verifiable scientific consensus. Rate claims as Verified, Disputed, Misleading, or Unsubstantiated with citations.",
            triggerConditions: ["is it true", "fact check", "verify claim", "evidence for"],
            requiredContext: ["claimText"],
            allowedTools: ["source-metadata-reader"],
            modelPreference: "complex-reasoning",
            tags: ["fact-check", "evidence", "verification"]
        ),
        new(
            id: "source-verifier",
            name: "Source Verifier",
            description: "Evaluates publisher reputation, peer-review status, and author credentials.",
            category: SkillCategory.Research,
            version: "1.0.0",
            instructions: "Assess credibility of sources using the CRAAP test: Currency, Relevance, Authority, Accuracy, and Purpose.",
            triggerConditions: ["reliable source", "check source", "peer reviewed"],
            requiredContext: ["sourceMetadata"],
            allowedTools: ["doi-metadata-lookup", "source-metadata-reader"],
            modelPreference: "balanced",
            tags: ["credibility", "sources", "evaluation"]
        ),
        new(
            id: "citation-assistant",
            name: "Citation Assistant",
            description: "Generates correct citations and bibliographies in APA, MLA, Chicago, and IEEE.",
            category: SkillCategory.Research,
            version: "1.0.0",
            instructions: "Generate perfectly formatted academic references and in-text citations. Validate DOI and author metadata.",
            triggerConditions: ["cite", "citation", "bibliography", "apa citation"],
            requiredContext: ["sourceDetails"],
            allowedTools: ["citation-formatter", "doi-metadata-lookup"],
            modelPreference: "fast",
            tags: ["citations", "referencing", "apa"]
        ),
        new(
            id: "data-analyst",
            name: "Data Analyst",
            description: "Interprets statistical datasets, distributions, regressions, and charts.",
            category: SkillCategory.Research,
            version: "1.0.0",
            instructions: "Analyze numerical datasets. Explain distributions, correlation vs causation, central tendency, confidence intervals, and hypothesis tests.",
            triggerConditions: ["analyze data", "statistics of", "dataset review", "correlation"],
            requiredContext: ["dataset"],
            allowedTools: ["statistics-calculator", "calculator"],
            modelPreference: "balanced",
            tags: ["statistics", "data", "analysis"]
        ),

        // ══════════════════════════════════════════════════════════════
        // 4. Technical Skills (10)
        // ══════════════════════════════════════════════════════════════
        new(
            id: "python-mentor",
            name: "Python Mentor",
            description: "Guides idiomatic Python, PEP 8 standards, async programming, and data science.",
            category: SkillCategory.Technical,
            version: "1.0.0",
            instructions: "Teach idiomatic, pythonic code (list comprehensions, generators, context managers, type hints). Guide debugging and memory efficiency.",
            triggerConditions: ["python", "pythonic", "pep8", "async python"],
            requiredContext: ["codeSnippet"],
            allowedTools: ["code-formatter", "code-analyzer"],
            modelPreference: "coding",
            tags: ["python", "programming", "mentor"]
        ),
        new(
            id: "programming-tutor",
            name: "Programming Tutor",
            description: "Teaches core programming fundamentals: variables, control flow, functions, and OOP.",
            category: SkillCategory.Technical,
            version: "1.0.0",
            instructions: "Teach foundational programming constructs language-agnostically: control flow, memory allocation, data types, and functions.",
            triggerConditions: ["learn coding", "coding basics", "how to program", "loops"],
            requiredContext: ["programmingLanguage"],
            allowedTools: ["code-formatter", "code-analyzer"],
            modelPreference: "coding",
            tags: ["coding", "fundamentals", "tutor"]
        ),
        new(
            id: "web-development-mentor",
            name: "Web Development Mentor",
            description: "Coaches HTML5, CSS3, modern JavaScript/TypeScript, and frontend/backend architecture.",
            category: SkillCategory.Technical,
            version: "1.0.0",
            instructions: "Coach modern web development across the full stack: responsive design, DOM manipulation, asynchronous state, and secure API design.",
            triggerConditions: ["web dev", "html css", "frontend", "backend", "javascript"],
            requiredContext: ["webProject"],
            allowedTools: ["html-preview", "code-formatter", "json-validator"],
            modelPreference: "coding",
            tags: ["web", "fullstack", "frontend"]
        ),
        new(
            id: "ai-ml-mentor",
            name: "AI/ML Mentor",
            description: "Explains machine learning theory, neural networks, loss functions, and inference.",
            category: SkillCategory.Technical,
            version: "1.0.0",
            instructions: "Explain ML and deep learning math: gradient descent, backpropagation, attention mechanisms, loss functions, and regularization.",
            triggerConditions: ["machine learning", "neural network", "deep learning", "ai models"],
            requiredContext: ["mlConcept"],
            allowedTools: ["calculator", "matrix-operations", "statistics-calculator"],
            modelPreference: "complex-reasoning",
            tags: ["ai", "machine-learning", "deep-learning"]
        ),
        new(
            id: "robotics-mentor",
            name: "Robotics Mentor",
            description: "Kinematics, sensor fusion, control loops (PID), and embedded actuators.",
            category: SkillCategory.Technical,
            version: "1.0.0",
            instructions: "Teach robotics theory: forward and inverse kinematics, PID controllers, Kalman filters, sensor interfaces, and motor controls.",
            triggerConditions: ["robotics", "pid controller", "kinematics", "actuators"],
            requiredContext: ["roboticsSystem"],
            allowedTools: ["calculator", "matrix-operations"],
            modelPreference: "balanced",
            tags: ["robotics", "control-systems", "kinematics"]
        ),
        new(
            id: "electronics-mentor",
            name: "Electronics Mentor",
            description: "Ohm's law, circuit analysis, semiconductors, microcontrollers, and PCB design.",
            category: SkillCategory.Technical,
            version: "1.0.0",
            instructions: "Analyze analog and digital circuits: Kirchhoff's laws, RC filters, transistors, operational amplifiers, and digital logic gates.",
            triggerConditions: ["circuits", "electronics", "ohms law", "resistors", "microcontroller"],
            requiredContext: ["circuitDescription"],
            allowedTools: ["calculator", "unit-converter"],
            modelPreference: "balanced",
            tags: ["electronics", "circuits", "hardware"]
        ),
        new(
            id: "mathematics-tutor",
            name: "Mathematics Tutor",
            description: "Calculus, linear algebra, discrete math, and probability with rigorous proofs.",
            category: SkillCategory.Technical,
            version: "1.0.0",
            instructions: "Teach higher mathematics with conceptual intuition followed by formal proofs: derivatives, integrals, eigenvalues, and combinatorics.",
            triggerConditions: ["calculus", "linear algebra", "discrete math", "probability", "derivative"],
            requiredContext: ["mathProblem"],
            allowedTools: ["calculator", "scientific-calculator", "equation-evaluator", "matrix-operations"],
            modelPreference: "complex-reasoning",
            tags: ["mathematics", "calculus", "linear-algebra"]
        ),
        new(
            id: "science-tutor",
            name: "Science Tutor",
            description: "Physics, Chemistry, and Biology explaining laws of nature and experiments.",
            category: SkillCategory.Technical,
            version: "1.0.0",
            instructions: "Explain natural sciences through empirical observation and first principles: Newton's laws, thermodynamics, chemical kinetics, and genetics.",
            triggerConditions: ["physics", "chemistry", "biology", "thermodynamics", "science question"],
            requiredContext: ["scienceTopic"],
            allowedTools: ["unit-converter", "scientific-calculator"],
            modelPreference: "balanced",
            tags: ["physics", "chemistry", "biology", "science"]
        ),
        new(
            id: "algorithm-explainer",
            name: "Algorithm Explainer",
            description: "Dissects algorithms, asymptotic complexity (Big-O), data structures, and invariants.",
            category: SkillCategory.Technical,
            version: "1.0.0",
            instructions: "Deconstruct algorithmic concepts: analyze time/space complexity, illustrate execution traces, identify loop invariants, and compare alternatives.",
            triggerConditions: ["big o", "algorithm", "data structure", "complexity", "quicksort"],
            requiredContext: ["algorithmName"],
            allowedTools: ["code-analyzer", "calculator"],
            modelPreference: "complex-reasoning",
            tags: ["algorithms", "big-o", "data-structures"]
        ),
        new(
            id: "software-architecture-mentor",
            name: "Software Architecture Mentor",
            description: "System design, microservices, decoupling, SOLID principles, and design patterns.",
            category: SkillCategory.Technical,
            version: "1.0.0",
            instructions: "Teach distributed systems and software architecture: bounded contexts, eventual consistency, CAP theorem, resilience patterns, and clean architecture.",
            triggerConditions: ["system design", "software architecture", "design patterns", "microservices"],
            requiredContext: ["architectureRequirements"],
            allowedTools: ["sql-query-validator", "code-analyzer"],
            modelPreference: "complex-reasoning",
            tags: ["architecture", "system-design", "patterns"]
        ),

        // ══════════════════════════════════════════════════════════════
        // 5. Productivity Skills (6)
        // ══════════════════════════════════════════════════════════════
        new(
            id: "study-planner",
            name: "Study Planner",
            description: "Builds realistic, milestone-driven study timetables optimized for retention.",
            category: SkillCategory.Productivity,
            version: "1.0.0",
            instructions: "Create balanced, achievable study timetables using Pomodoro blocks, spaced intervals, buffer days, and periodic diagnostic reviews.",
            triggerConditions: ["study plan", "schedule revision", "timetable", "study routine"],
            requiredContext: ["targetExamDate", "availableHours"],
            allowedTools: ["study-progress-reader"],
            modelPreference: "balanced",
            tags: ["planner", "schedule", "productivity"]
        ),
        new(
            id: "revision-planner",
            name: "Revision Planner",
            description: "Calculates optimal spaced repetition intervals based on forgetting curves.",
            category: SkillCategory.Productivity,
            version: "1.0.0",
            instructions: "Schedule prioritized review sessions targeting low-mastery competencies right before predicted memory decay milestones.",
            triggerConditions: ["revision plan", "spaced repetition schedule", "exam countdown"],
            requiredContext: ["masteredObjectives"],
            allowedTools: ["learning-objective-reader", "study-progress-reader"],
            modelPreference: "balanced",
            tags: ["revision", "spaced-repetition", "planning"]
        ),
        new(
            id: "knowledge-organizer",
            name: "Knowledge Organizer",
            description: "Catalogs notes, highlights, and resources into indexed semantic knowledge bases.",
            category: SkillCategory.Productivity,
            version: "1.0.0",
            instructions: "Organize fragmented notes into cohesive knowledge hierarchies with cross-references, index tags, and glossary definitions.",
            triggerConditions: ["organize notes", "knowledge base", "index materials", "glossary"],
            requiredContext: ["rawNotes"],
            allowedTools: ["document-text-extractor", "text-search"],
            modelPreference: "balanced",
            tags: ["organization", "knowledge", "indexing"]
        ),
        new(
            id: "flashcard-creator",
            name: "Flashcard Creator",
            description: "Produces high-quality question-and-answer cards optimized for active recall.",
            category: SkillCategory.Productivity,
            version: "1.0.0",
            instructions: "Formulate atomic, unambiguous flashcards targeting one specific fact or concept per card. Include concise explanations on card backs.",
            triggerConditions: ["make flashcards", "create cards", "anki deck", "active recall cards"],
            requiredContext: ["lessonText"],
            allowedTools: ["flashcard-generator"],
            modelPreference: "balanced",
            tags: ["flashcards", "anki", "recall"]
        ),
        new(
            id: "mind-map-creator",
            name: "Mind Map Creator",
            description: "Visualizes relationships and branches between core concepts and sub-disciplines.",
            category: SkillCategory.Productivity,
            version: "1.0.0",
            instructions: "Construct structured text or Mermaid mind maps radiating outward from core concepts to sub-branches, mechanisms, and applications.",
            triggerConditions: ["mind map", "concept map", "visual hierarchy"],
            requiredContext: ["coreConcept"],
            allowedTools: ["document-text-extractor"],
            modelPreference: "balanced",
            tags: ["mindmap", "visualization", "hierarchy"]
        ),
        new(
            id: "project-mentor",
            name: "Project Mentor",
            description: "Scaffolds student capstone and research projects from ideation to delivery.",
            category: SkillCategory.Productivity,
            version: "1.0.0",
            instructions: "Guide students through project milestones: scoping requirements, technical stack selection, risk registers, and weekly deliverables.",
            triggerConditions: ["project idea", "capstone", "portfolio project", "scaffold project"],
            requiredContext: ["projectDomain"],
            allowedTools: ["study-progress-reader"],
            modelPreference: "balanced",
            tags: ["project", "capstone", "mentorship"]
        )
    ];
}
