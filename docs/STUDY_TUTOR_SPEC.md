# SPARK Study Division — AI Tutor Specification
**Phase 2: Core Learning & AI Tutor**
*Document Version: 2.0.0 | Date: November 2026*

---

## 1. Overview

The SPARK AI Tutor (`IStudyTutorService` / `StudyTutorService`) is an adaptive, pedagogically grounded instructional intelligence engine. Unlike generic chat interfaces, the AI Tutor actively adapts its cognitive depth, tone, structure, language, and scaffolding based on the learner's tier, session preferences, and real-time dialogue cues.

---

## 2. Pedagogical Teaching Modes

The AI Tutor implements 8 distinct teaching modes:

### 1. Beginner Mode
- **Goal**: Build intuition and confidence without cognitive overload.
- **Scaffolding**: Heavy reliance on physical, everyday metaphors; immediate clarification of terms; encouraging tone; friendly comprehension checks.

### 2. Standard Mode
- **Goal**: Balanced, structured academic explanation.
- **Scaffolding**: Clear conceptual hierarchy, formal definition, explanation of underlying mechanics, typical examples, and a follow-up application question.

### 3. Detailed Mode
- **Goal**: Comprehensive, in-depth academic mastery.
- **Scaffolding**: Historical and theoretical motivation, full operational mechanisms, edge-case constraints, memory/performance implications, and comparison with alternatives.

### 4. Advanced Mode
- **Goal**: Rigorous formal, mathematical, and architectural treatment.
- **Scaffolding**: Asymptotic bounds, mathematical proofs by induction, hardware cache interactions, distributed tradeoffs, and challenge problems.

### 5. Step-by-Step Mode
- **Goal**: Systematic procedural mastery.
- **Scaffolding**: Ordered numbered phases (Phase 1, Phase 2, etc.), single atomic action per step, rationale for why the step is necessary, and validation checkpoints.

### 6. Socratic Mode
- **Goal**: Inquiry-based guided discovery.
- **Scaffolding**: Refuses to provide direct answers upfront. Asks guiding counter-questions, acknowledges learner intuition, identifies logical gaps, and prompts deduction from first principles.

### 7. Practical Mode
- **Goal**: Production-ready, actionable engineering.
- **Scaffolding**: Well-commented, runnable code examples, industry best practices, common production pitfalls, defensive programming tips, and mini coding challenges.

### 8. Revision Mode
- **Goal**: High-density rapid recall and exam preparation.
- **Scaffolding**: Concise bullet points, core formulas, key differences, memory hooks, and rapid-fire recall questions.

---

## 3. Bilingual Instruction (English & Malayalam)

The AI Tutor natively supports both **English** and **Malayalam** (`LearningLanguage.Malayalam`):
- **Pedagogical Rule**: Explanations, analogies, and guidance are rendered in natural, respectful Malayalam (മലയാളം).
- **Bilingual Terminology Retention**: Technical keywords, function names, formulas, and data structures are preserved in standard Latin script or bracketed format (e.g., `Recursion (റിക്കർഷൻ)`, `Time Complexity`, `Binary Search Tree`, `Big-O Notation`).
- **Cultural Resonance**: Incorporates relevant analogies while adhering strictly to global technical standards.

---

## 4. Adaptive Mode & Language Detection

The tutor continuously scans user inquiries for dynamic adaptation requests:

| User Trigger Phrase | Adaptive Action |
| :--- | :--- |
| `"simpler"`, `"explain like i'm 5"`, `"in simple terms"` | Dynamically shifts turn to `TeachingMode.Beginner` |
| `"step by step"`, `"walk me through"` | Dynamically shifts turn to `TeachingMode.StepByStep` |
| `"quiz me"`, `"test me"`, `"socratic"` | Dynamically shifts turn to `TeachingMode.Socratic` |
| `"code example"`, `"hands on"`, `"practical"` | Dynamically shifts turn to `TeachingMode.Practical` |
| `"revision"`, `"summary"`, `"cheat sheet"` | Dynamically shifts turn to `TeachingMode.Revision` |
| `"deep dive"`, `"in detail"`, `"under the hood"` | Dynamically shifts turn to `TeachingMode.Detailed` |
| `"mathematical proof"`, `"formal proof"` | Dynamically shifts turn to `TeachingMode.Advanced` |
| `"in malayalam"`, `"മലയാളത്തിൽ"` | Dynamically shifts turn to `LearningLanguage.Malayalam` |
| `"in english"` | Dynamically shifts turn to `LearningLanguage.English` |

---

## 5. Model Routing & Fallback Architecture

1. **Routing**: Maps active teaching mode to `SparkTaskCategory`:
   - `Practical` $\rightarrow$ `SparkTaskCategory.Coding`
   - `Advanced`, `Detailed`, `Socratic` $\rightarrow$ `SparkTaskCategory.ComplexReasoning`
   - `Revision` $\rightarrow$ `SparkTaskCategory.DocumentAnalysis`
   - `Beginner`, `Standard`, `StepByStep` $\rightarrow$ `SparkTaskCategory.SimpleConversation`
2. **Resilient Local Fallback**: When offline or if the routing model is unavailable, `StudyTeachingPrompts.GenerateLocalPedagogicalResponse` synthesizes a deterministic, structured educational response, setting `IsFallback = true`.
