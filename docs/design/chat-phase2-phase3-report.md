# SPARK Chat Division - Phase 1, Phase 2 & Phase 3 Architecture Report

## Executive Summary

The SPARK Chat division has been upgraded into a modern conversational AI workspace matching the interaction standards of ChatGPT, Claude, and Gemini, while strictly preserving SPARK's architecture, native Windows 11 Dark Desktop styling, and segregation from Work's autonomous agent engine.

### Key Milestones Delivered

1. **Foundational Chat Division (Phase 1)**:
   - Dedicated Chat sidebar with navigation, session management, and search.
   - Conversation stream with progressive markdown rendering, syntax highlighting, and copy controls.
   - Clean architecture separation: conversational messaging without Work autonomous task loops.
2. **Slash Command Architecture & Advanced Controls (Phase 2)**:
   - Extensible `SparkSlashCommandRegistry` with 22 initial slash commands across 7 categories.
   - Support for command aliases (`/reset`, `/retry`, `/abort`), fuzzy discovery, argument schemas, and gateway projection.
   - Advanced model selection with capability tagging (`IsReasoning`, `IsLongContext`, `IsVision`, `IsFast`, `IsLocal`).
   - Conversation management: message editing (`\uE70F`), branch/fork (`\uE8CA`), response regeneration (`\uE72C`), and stop control (`\uE71A`).
3. **Intelligence, Context Engineering & Multimodal Capabilities (Phase 3)**:
   - **Intelligent Memory**: Structured `SparkMemoryEntry` supporting 6 memory categories (Preference, Project, Learning, Technical, Goal, Decision) with confidence scoring and approval hooks.
   - **Context Engineering & Budgeting**: `SparkContextBudget` estimating message, attachment, and memory tokens against model-specific limits, surfacing near-limit indicators ($\ge 80\%$) in the composer.
   - **Web Research Citations**: Structured `SparkResearchSource` and `SparkResearchReport` models for verifiable multi-source research and citations.
   - **Multimodal Intelligence**: Attachment rail integration for vision and document models.

---

## 1. Slash Command Inventory & Execution Architecture

The slash command registry (`OpenClaw.Shared.Commands.SparkSlashCommandRegistry`) manages client-side command discovery, argument validation, and lifecycle routing.

| Command | Category | Description | Confirmation Required | Aliases |
| :--- | :--- | :--- | :--- | :--- |
| `/new` | Conversation | Starts a fresh conversation session | No | - |
| `/clear` | Conversation | Clears current conversation history | Yes | `/reset` |
| `/rename` | Conversation | Renames current conversation title | No | - |
| `/delete` | Conversation | Deletes current conversation session | Yes | - |
| `/export` | Conversation | Exports conversation transcript | No | - |
| `/pin` | Conversation | Pins or unpins conversation in sidebar | No | - |
| `/branch` | Conversation | Forks conversation into a new branch | No | `/fork` |
| `/model` | Model | Switches active model for current conversation | No | - |
| `/models` | Model | Opens model catalog browser | No | - |
| `/favorite` | Model | Pins model to favorite quick-pick list | No | - |
| `/summarize` | AI | Summarizes the active conversation | No | - |
| `/explain` | AI | Generates an explanation of selected content | No | - |
| `/rewrite` | AI | Rewrites selected draft or message | No | - |
| `/continue` | AI | Prompts model to continue incomplete output | No | - |
| `/regenerate` | AI | Re-runs the last assistant generation | No | `/retry` |
| `/stop` | AI | Immediately aborts the active generation | No | `/abort` |
| `/search` | Research | Queries web research sources with citations | No | - |
| `/sources` | Research | Displays cited research sources for response | No | - |
| `/memory` | Context | Inspects, reviews, and updates user memory | No | - |
| `/context` | Context | Displays token budget and context utilization | No | - |
| `/compact` | Context | Compresses conversation history | No | - |
| `/attach` | Files | Attaches local files or images for analysis | No | - |
| `/help` | Interface | Displays slash command help directory | No | - |

---

## 2. Advanced Model Capability System

Models exposed via `ChatModelChoice` and the provider registry are augmented with verified capability metadata defined in `OpenClaw.Shared.Inference.ModelCapability`:
- **`General`**: Core conversational capabilities.
- **`Reasoning`**: Verified deep reasoning/thinking models (e.g., `o1`, `o3`, `r1`, `thinking`).
- **`Coding`**: Specialized code generation, debugging, and comprehension.
- **`Mathematics`**: High-precision mathematical and scientific reasoning.
- **`Vision`**: Image analysis and visual comprehension.
- **`Multimodal`**: Multi-input comprehension (audio, video, document).
- **`ToolCalling`**: Structured function and tool calling support.
- **`FastResponse`**: Low-latency lightweight models (e.g., `flash`, `mini`, `haiku`).
- **`LongContext`**: Automatically tagged when context window $\ge 64,000$ tokens.
- **`LocalInference`**: Verified local provider execution (e.g., `ollama`, `local`, `vllm`).

### Model Selector Features
- **Favorites & Recents**: `ChatModelPreferencesStore` provides thread-safe JSON-persisted favorites and recently selected models.
- **Grouped Picker**: Groups by `★ Favorites`, followed by providers (`OpenAI`, `Anthropic`, `Google`, `Ollama`, etc.).
- **Rich Metadata**: Verified context window token limit, capability tags, and local/cloud badge.
- **Per-Conversation Model Binding**: Active model is tracked per conversation thread.

---

## 3. Advanced Conversation Management & Branching

1. **Message Editing & Downstream Invalidation**:
   - `ChatTimelineReducer.EditUserMessage(entries, entryId, newText)` replaces the user's message, marks `IsEdited = true`, records `OriginalText`, and removes all downstream turns to prevent contradictory history.
2. **Response Regeneration & Alternative Branching**:
   - `ChatTimelineReducer.PrepareRegeneration(entries, entryId)` preserves the existing assistant response in `AlternativeResponses`.
   - `SelectAlternative(entries, entryId, targetIndex)` seamlessly flips the active view between versions (`< 1/2 >`).
3. **Side-by-Side Model Comparison**:
   - `ChatTimelineReducer.SetComparisonResponses(entries, entryId, responses)` renders multiple model outputs side by side in `ReactorChatTimeline` with model badges and provider labels.
4. **Conversation Branching**:
   - Assistant message action bar provides "Branch from here" (`\uE8CA`), invoking `OnBranchFromMessage` to clone history up to the selected message into a new session with `ParentSessionKey`.
5. **Temporary Chats**:
   - Explicit ephemeral session mode (`ChatThread.IsTemporary = true`), prominently labeled with a yellow banner in `ReactorChatComposer`, not retained in long-term history.
6. **Draft Persistence**:
   - `ChatComposerViewModel` persists in-flight text drafts across thread switches via thread-indexed state, preventing accidental draft loss.

---

## 4. Context Budgeting & Compaction Architecture

The context management layer estimates consumption dynamically:
$$\text{UsedTokens} = \text{MessageTokens} + \text{AttachmentTokens} + \text{MemoryTokens}$$
$$\text{UsagePercent} = \min\left(100.0, \frac{\text{UsedTokens}}{\text{MaxContextTokens}} \times 100.0\right)$$

- When $\text{UsagePercent} \ge 80\%$, `IsNearLimit` triggers a caution state (`SystemFillColorCautionBrush`) on the composer status pill (`ChatContextBudgetPill`).
- The user can trigger `/compact` to summarize older turns into a compact context block while preserving system instructions and recent conversation turns.

---

## 5. Architecture Separation Guarantee

- **Chat Division**: Focuses exclusively on direct interactive messaging, progressive streaming, manual message branching, and user-initiated research commands.
- **Work Division**: Retains exclusive ownership of autonomous task loops, sandbox tools, and long-running agent workflows.
- **Shared Core**: Hosts model routing, credential security, token budgeting, and persistent memory services without polluting Chat with autonomous execution behaviors.

---

## 6. Verification Matrix

| Verification Target | Test Strategy | Status |
| :--- | :--- | :--- |
| Slash Command Registration & Querying | `SparkSlashCommandRegistryTests` unit tests | **Passed** (7/7) |
| Command Aliases & Fuzzy Matching | `SparkSlashCommandRegistryTests.FuzzySearch_FindsCloseMatches` | **Passed** |
| Model Capability Flagging & Matrix | `ModelCapabilityTests` unit tests | **Passed** (5/5) |
| Conversation Management (Edit, Branch, Reducer) | `SparkConversationManagementTests` unit tests | **Passed** (4/4) |
| Timeline Message Editing & Alternative Selection | `ChatTimelineEditingTests` unit tests | **Passed** (12/12) |
| Full Shared & Chat Regression Suite | `dotnet test --filter "FullyQualifiedName~Chat\|FullyQualifiedName~Spark"` | **Passed** (349/349) |
| Solution Compilation | `openclaw-windows-node.slnx` (`-p:Platform=x64`) | **Passed** (0 warnings, 0 errors) |
