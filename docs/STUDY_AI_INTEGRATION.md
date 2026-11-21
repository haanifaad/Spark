# SPARK STUDY DIVISION — AI INTEGRATION SPECIFICATION
## Phase 1: Orchestration, Intent Routing & Model Router Integration

---

## 1. Principles of AI Integration

1. **Zero Duplicate Infrastructure**: SPARK's existing `ISparkModelRouter` (`OpenClaw.Shared.Inference.Routing`) and `ISparkMemoryStore` (`OpenClaw.Shared.Memory`) serve as the shared intelligence and context engines. The Study Division does not create redundant model routers or storage mechanisms.
2. **Intent-Driven Orchestration**: Different educational queries require fundamentally different prompt patterns, model capabilities, and latency profiles (e.g., Socratic dialogue vs. complex exam derivation).
3. **Calibrated Privacy & Locality**: The student or educational institution can enforce local-only inference (`ModelLocality.LocalOnly`) for sensitive materials.

---

## 2. Intent Routing Architecture

The `IStudyIntentRouter` classifies inbound learner prompts into one of six core educational intents:

| Educational Intent | Description | Recommended Model Capability | Suggested Reasoning Style |
| :--- | :--- | :--- | :--- |
| `ConceptualExplanation` | Breaking down complex theoretical ideas. | High reasoning / Text | Direct, illustrative with analogies |
| `SocraticTutoring` | Guiding the student towards answers via targeted questions. | High conversational / Fast | Non-direct, inquiry-driven |
| `ProblemSolvingPractice` | Step-by-step guidance on math, code, or science exercises. | Reasoning / Math / Code | Phased hints, intermediate checks |
| `SummaryAndSynthesis` | Compacting broad text or lecture transcripts into cheat sheets. | High context window | Structured Markdown, bulleted |
| `ExamReview` | Rapid flashcard and quiz drill questioning. | Fast / Low latency | Q&A with immediate score feedback |
| `ReferenceLookup` | Factual inquiry about definitions or formulas. | Fast / Factual | Concise definition + citation |

---

## 3. Model Router Integration

When `StudyOrchestrator` receives a learning turn:
1. It uses `StudyIntentRouter.Classify(request)` to determine the required `ModelCapability`.
2. It builds a `SparkRoutingRequest`:
   - Task: `"Study." + intent.ToString()`
   - RequiredCapability: High reasoning or Fast conversational
   - Locality: Matches user study settings (Local Ollama vs. Cloud)
3. It calls `ISparkModelRouter.Route(routingRequest)` to obtain optimal model and fallback chain.
4. Executes inference via `ISparkModelRouter.ExecuteWithFallbackAsync(...)`.

---

## 4. Context Aggregator (`IStudyContextProvider`)

The context builder compiles a multi-layer context envelope:
- **Layer 1: Learner Profile**: Grade level, learning pace, preferred explanations (intuitive vs. mathematical).
- **Layer 2: Syllabus Context**: Current subject, chapter outline, and target learning objectives.
- **Layer 3: Session State**: Recent messages, questions asked, and misconceptions encountered.
- **Layer 4: Memory Recall**: Relevant semantic memories fetched from `ISparkMemoryStore.SearchAsync(...)`.

This guarantees high relevance while adhering to context window limits.
