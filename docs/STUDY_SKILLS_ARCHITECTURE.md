# SPARK Study Division — Skills Architecture

## 1. Overview & Pedagogical Purpose
The **SPARK Study Division Skills Architecture** provides an extensible, strongly typed capability framework for the AI Tutor. A **Skill** is a structured behavioral contract that defines how the tutor acts, scopes domain context, enforces pedagogical constraints, and specifies permitted tools and model routing preferences.

Skills isolate specialized tutoring strategies (such as Socratic dialogue, Feynman simplification, or Bloom's taxonomy assessments) preventing instruction leakage across tasks while ensuring consistent educational quality.

---

## 2. Structured Skill Metadata Schema
Every skill registered in the SPARK platform adheres to the `StudySkillDefinition` contract:

```csharp
public sealed record StudySkillDefinition(
    string Id,
    string Name,
    string Description,
    SkillCategory Category,
    string Version = "1.0.0",
    string Instructions = "",
    IReadOnlyList<string>? TriggerConditions = null,
    IReadOnlyList<string>? RequiredContext = null,
    string InputSchema = "{}",
    string OutputSchema = "{}",
    IReadOnlyList<string>? AllowedTools = null,
    string ModelPreference = "balanced",
    StudyPermissionKind RequiredPermission = StudyPermissionKind.ReadOnly,
    SkillStatus Status = SkillStatus.Active,
    SkillErrorHandlingConfig? ErrorHandling = null,
    bool IsEnabled = true,
    IReadOnlyList<string>? Tags = null,
    DateTime CreatedAtUtc = default,
    DateTime UpdatedAtUtc = default
);
```

### Core Properties
- **Skill ID**: Canonical lowercase alphanumeric identifier (e.g. `concept-explainer`, `socratic-tutor`).
- **Category**: Functional group (`Learning`, `Academic`, `Research`, `Technical`, `Productivity`).
- **Instructions**: Immutable system prompt preamble defining pedagogy, tone, and cognitive boundaries.
- **Trigger Conditions**: Natural language intent keywords and semantic phrases mapped to the skill.
- **Allowed Tools**: Strict whitelist of tool IDs that the skill is authorized to invoke.
- **Model Preference**: Cognitive tier request (`balanced`, `coding`, `deep_reasoning`, `fast`).
- **Error Handling**: Max retry count, fallback skill ID, and graceful degradation messages.

---

## 3. Skill Categories & Complete Built-In Inventory (37 Skills)

### 3.1 Learning Skills (10)
1. **Personal Tutor (`personal-tutor`)**: Adaptive comprehensive mentoring attuned to learner mastery level.
2. **Subject Teacher (`subject-teacher`)**: Structured curricular instruction aligned with formal syllabi.
3. **Concept Explainer (`concept-explainer`)**: Deep conceptual unbundling utilizing the Feynman technique and progressive abstraction.
4. **Step-by-Step Teacher (`step-by-step-teacher`)**: Methodical procedural breakdown with intermediate checkpoints and validation questions.
5. **Socratic Tutor (`socratic-tutor`)**: Guided discovery through progressive questioning without direct solution reveal.
6. **Beginner Teacher (`beginner-teacher`)**: Zero-jargon foundational instruction using everyday analogies.
7. **Advanced Teacher (`advanced-teacher`)**: Rigorous academic deep-dives with formal proofs and edge cases.
8. **Revision Coach (`revision-coach`)**: Spaced retrieval practice and active recall reinforcement.
9. **Language Tutor (`language-tutor`)**: Multilingual translation and vocabulary acquisition (specialized for Malayalam and English).
10. **Practical Learning Coach (`practical-learning-coach`)**: Hands-on problem solving, labs, and interactive exercise execution.

### 3.2 Academic Skills (9)
11. **Note Creator (`note-creator`)**: Cornell-style and structured note synthesis from study material.
12. **Chapter Explainer (`chapter-explainer`)**: Section-by-section breakdown of textbook chapters and syllabi.
13. **Question Generator (`question-generator`)**: Bloom's taxonomy assessment questions across all 6 cognitive difficulty levels.
14. **Answer Evaluator (`answer-evaluator`)**: Rubric-based grading with feedback on strengths, misconceptions, and score.
15. **Grammar Coach (`grammar-coach`)**: Syntactic precision and stylistic feedback for academic writing.
16. **Essay Assistant (`essay-assistant`)**: Argumentation scaffolding, thesis development, and rhetorical structuring.
17. **Literature Analyst (`literature-analyst`)**: Thematic and structural analysis of literary and philosophical texts.
18. **Homework Assistant (`homework-assistant`)**: Guided problem verification with intermediate hints.
19. **Exam Preparation Coach (`exam-preparation-coach`)**: Timed practice, mock tests, and high-yield topic prioritization.

### 3.3 Research Skills (8)
20. **Research Assistant (`research-assistant`)**: Academic inquiry scoping, methodology outlining, and literature review.
21. **Academic Research Assistant (`academic-research-assistant`)**: Formal peer-reviewed journal analysis and hypothesis formulation.
22. **Paper Analyzer (`paper-analyzer`)**: Deep deconstruction of research papers (abstract, methodology, results, critiques).
23. **Literature Reviewer (`literature-reviewer`)**: Comparative synthesis across multiple academic papers and trends.
24. **Fact Checker (`fact-checker`)**: Claim verification against authoritative sources.
25. **Source Verifier (`source-verifier`)**: Credibility assessment of journal publishers, DOIs, and institutional domains.
26. **Citation Assistant (`citation-assistant`)**: Multi-style citation formatting (APA 7, MLA 9, Chicago, IEEE, Harvard).
27. **Data Analyst (`data-analyst`)**: Statistical synthesis, data distribution interpretation, and trend analysis.

### 3.4 Technical Skills (10)
28. **Python Mentor (`python-mentor`)**: Idiomatic Python programming instruction and debugging.
29. **Programming Tutor (`programming-tutor`)**: Polyglot programming concepts (C#, Java, C++, JS, Rust).
30. **Web Development Mentor (`web-development-mentor`)**: Modern full-stack web engineering (HTML5, CSS3, JS, React, WinUI).
31. **AI & ML Mentor (`ai-ml-mentor`)**: Machine learning fundamentals, neural networks, transformers, and model evaluation.
32. **Robotics Mentor (`robotics-mentor`)**: Embedded hardware control, kinematics, actuators, and ROS.
33. **Electronics Mentor (`electronics-mentor`)**: Circuit design, Ohm's law, digital logic gates, and microcontroller I/O.
34. **Mathematics Tutor (`mathematics-tutor`)**: Pure and applied mathematics, calculus, linear algebra, and discrete math.
35. **Science Tutor (`science-tutor`)**: Integrated STEM instruction spanning physics, chemistry, and biology.
36. **Algorithm Explainer (`algorithm-explainer`)**: Algorithmic complexity, Big O analysis, and dynamic programming decomposition.
37. **Software Architecture Mentor (`software-architecture-mentor`)**: Distributed systems, microservices, domain-driven design, and clean architecture.

---

## 4. Skills Registry Engine (`StudySkillRegistry`)
The `IStudySkillRegistry` contract manages skill lifecycle and persistence:

- **Seeding**: Automatically seeds all built-in skills upon first launch or when the database is empty.
- **Lookup Resolution**: Resolves both prefixed (`skill-concept-explainer`) and unprefixed (`concept-explainer`) IDs.
- **Search Engine**: Multi-field search over Name, Description, Tags, and Trigger Conditions.
- **Status Management**: Supports enabling/disabling skills per user with instant persistence.
- **Validation**: Enforces non-empty IDs, names, instructions, and schema syntax before saving custom skills.
- **Audit History**: Records skill activations with duration, prompt, and response snapshots in `StudyDataStore`.

---

## 5. Intent-to-Skill Routing & Context Assembly
When a learner submits input without an explicit `/slash` command or force-skill override:
1. `StudyEducationalIntentRouter` classifies the pedagogical intent (e.g., `SocraticTutoring`, `ExamReview`, `SummaryAndSynthesis`).
2. `StudyExecutionPipeline` matches natural language trigger phrases (`"quiz"`, `"step by step"`, `"fact check"`).
3. The resolved skill injects its instructions into the execution context along with the active course topic, learner level, and permitted tools.
