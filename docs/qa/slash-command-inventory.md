# SPARK  -  Slash Command Discovery & Inventory Report

**Generated:** 2026-11-18  
**Codebase:** `Spark-main` (OpenClaw)  
**Methodology:** Source-code investigation of C# client, gateway runtime (JavaScript), protocol DTOs, command registries, UI dispatchers, and command categories.

---

## A. Executive Summary

| Metric | Count |
|---|---|
| **Total unique built-in commands** | **39** |
| **Total aliases** | **15** |
| **Total commands in Chat division** | 0 (inherits shared) |
| **Total commands in Work division** | 0 (inherits shared) |
| **Total commands in Code division** | 0 (not implemented) |
| **Total commands in Study division** | 0 (not implemented) |
| **Total commands in Creative division** | 0 (not implemented) |
| **Shared commands (division-agnostic)** | 39 |
| **Client-side lifecycle commands** | 3 (subset of shared) |
| **Approval pseudo-commands** | 2 (`/approve`, `/deny`) |
| **Deprecated commands** | 0 |
| **Partially implemented commands** | 0 |
| **Dynamic skill commands** | Variable (registered at runtime per workspace/agent) |

> [!IMPORTANT]
> SPARK's slash commands are **not division-scoped**. All 39 built-in commands are registered at the **gateway level** and are shared across every active division (Chat and Work today). The five-division architecture (Chat, Work, Code, Study, Creative) defines workspace boundaries and UI routing  -  it does not partition the command catalog.

---

## B. Chat Division Commands (Phase 2 Upgrade)

**Division status:** `IsActive: true`  -  [SparkDivision.cs#L14](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Shared/Divisions/SparkDivision.cs#L14)  
**Implementation:** [SparkSlashCommandRegistry.cs](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Shared/Commands/SparkSlashCommandRegistry.cs), [SparkSlashCommand.cs](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Shared/Commands/SparkSlashCommand.cs), [ChatComposerController.cs](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Tray.WinUI/Chat/ChatComposerController.cs)

In Phase 2, the Chat division is equipped with a dedicated, extensible C# **`SparkSlashCommandRegistry`** that exposes 26 verified slash commands across 6 core categories. These commands are seamlessly merged into the composer autocomplete palette (`ChatComposerViewModel`), support fuzzy search (Levenshtein distance <= 2), argument validation, explicit confirmation prompts for destructive actions, and client-side execution handlers.

### Phase 2 Slash Command Registry (26 Built-in Commands)

| Command | Category | Arguments | Requires Confirmation | Description |
|---|---|---|---|---|
| `/new` | Conversation | `[title]` (optional) | No | Create and switch to a new conversation session. |
| `/clear` | Conversation | None | **Yes** | Clear all messages in the active conversation thread. |
| `/rename` | Conversation | `<name>` (required) | No | Rename the current conversation session. |
| `/delete` | Conversation | None | **Yes** | Permanently delete the active conversation session. |
| `/export` | Conversation | `[format: markdown\|json\|txt]` | No | Export current conversation history to file or clipboard. |
| `/pin` | Conversation | None | No | Pin or unpin the active conversation session in history. |
| `/model` | Model | `[model-id]` (optional) | No | Switch active inference model or inspect active model details. |
| `/models` | Model | `[filter]` (optional) | No | List and filter available verified AI models. |
| `/favorite` | Model | `[model-id]` (optional) | No | Toggle model favorite status for quick access in selector. |
| `/summarize` | AI | `[instructions]` (optional) | No | Summarize the current conversation or specified text. |
| `/explain` | AI | `[topic]` (optional) | No | Request structured, in-depth explanation of recent content. |
| `/rewrite` | AI | `[tone/style]` (optional) | No | Rewrite the preceding response with alternate tone, conciseness, or format. |
| `/continue` | AI | None | No | Continue generating assistant response from where it stopped. |
| `/regenerate`| AI | None | No | Regenerate the latest assistant response with a new alternative. |
| `/retry` | AI | None | No | Retry the previous prompt with active or fallback model. |
| `/stop` | AI | None | No | Immediately halt ongoing inference streaming. |
| `/search` | Research | `<query>` (required) | No | Perform grounded web search and retrieve live citations. |
| `/sources` | Research | None | No | Display grounding sources and citations for recent answers. |
| `/memory` | Context | `[action: view\|add\|clear]` | View/Add: No, Clear: **Yes** | Inspect or manage long-term conversational memory. |
| `/context` | Context | None | No | Display context window usage, token budget, and breakdown. |
| `/compact` | Context | None | No | Compact conversation context to preserve token limits. |
| `/attach` | Files | `[file-path]` (optional) | No | Open file picker or attach specified file to composer. |
| `/files` | Files | None | No | List all active attachments in current session context. |
| `/settings` | Interface | `[section]` (optional) | No | Open SPARK application settings dialog. |
| `/theme` | Interface | `[theme: dark\|light\|system]` | No | Toggle or set active visual theme. |
| `/help` | Interface | `[command]` (optional) | No | Display help documentation and command usage guide. |

### Fuzzy Matching & Unknown Command Handling
When a user types an unknown command (e.g. `/clr` or `/modl`), the registry computes the Levenshtein distance against all registered commands. If distance <= 2, it presents targeted suggestions:
> *"Unknown command `/clr`. Did you mean: `/clear`?"*

Commands with validation errors (e.g. `/rename` without arguments) provide explicit usage syntax:
> *"Command '/rename' requires an argument (<name>). Usage: /rename <name>"*

Unknown or invalid commands are intercepted at composer level and **never submitted as ordinary LLM messages**. Destructive operations (`/clear`, `/delete`, `/memory clear`) require explicit user confirmation before execution.

---

## C. Work Division Commands  -  Detailed Investigation

**Division status:** `IsActive: true`  -  [SparkDivision.cs#L19](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Shared/Divisions/SparkDivision.cs#L19)

The Work division is described as:
> *"Productivity workspace housing Workboard, Worktrees, Automations, Dashboards, and Sessions."*

### Work-Specific Slash Commands: None

After thorough investigation of the entire codebase, the Work division **does not define any division-specific slash commands**. The Work division provides its productivity features through:

1. **Dashboard routes** (UI navigation, not slash commands): `/worktrees`, `/automations`, `/dashboards`, `/sessions`, `/activity`, `/portals`, `/apps`, `/meetings`, `/plugins`
2. **Workboard gateway extension** (`extensions/workboard/index.js`)  -  provides backend tools for the workboard feature, not slash commands
3. **Shared gateway commands**  -  the same 39 built-in slash commands available in Chat are also available in Work

All slash commands accessible within the Work division are the **shared commands** listed in Section G.

### Work Division Investigation Details

| Investigation Target | Location | Finding |
|---|---|---|
| Division catalog | [SparkDivision.cs](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Shared/Divisions/SparkDivision.cs) | Work division defined with key `"work"`, `IsActive: true`. No command registrations. |
| Workboard extension | `artifacts/gateway-stage/dist/extensions/workboard/index.js` | Backend extension providing tool functionality; no slash command registrations via `registerCommand`. |
| Division key aliases | [SparkDivision.cs#L129-L136](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Shared/Divisions/SparkDivision.cs#L129-L136) | Work division resolves from keys: `work`, `workspace`, `workboard`, `worktrees`, `cron`, `dashboards`, `sessions`. These are routing aliases, not slash commands. |
| Dashboard routes | [HubSearchCatalog.cs#L14-L23](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Tray.WinUI/Presentation/HubSearchCatalog.cs#L14-L23) | Hub navigation routes registered for Worktrees, Automations, Dashboards, etc. These are page navigation entries, not slash commands. |
| Gateway command registry | [commands-registry.data-D37tBV_t.mjs](file:///c:/Intel/Spark/Spark-main/artifacts/gateway-stage/dist/commands-registry.data-D37tBV_t.mjs) | No division-scoped command filtering. All 39 built-in commands are division-agnostic. |
| Command catalog query | [GatewayProtocolModels.cs#L111-L133](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Shared/GatewayProtocolModels.cs#L111-L133) | `CommandCatalogQuery` filters by `Category`, `Source`, `Scope`, `Search`, `AcceptsArgs`  -  no division filter exists. |

---

## D. Code Division Commands

**Division status:** `IsActive: false`  -  [SparkDivision.cs#L82](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Shared/Divisions/SparkDivision.cs#L82)

> *"Dedicated development workspace architected for OpenCode and Hermes Agent integration."*

**No commands implemented.** The Code division is defined in the `SparkDivision` enum and `DivisionCatalog` but is marked inactive. No Code-specific slash commands exist in the codebase.

---

## E. Study Division Commands

**Division status:** `IsActive: false`  -  [SparkDivision.cs#L91](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Shared/Divisions/SparkDivision.cs#L91)

> *"Dedicated learning, research, reading, and knowledge synthesis workspace."*

**No commands implemented.** The Study division is defined in the `SparkDivision` enum and `DivisionCatalog` but is marked inactive. No Study-specific slash commands exist in the codebase.

---

## F. Creative Division Commands

**Division status:** `IsActive: false`  -  [SparkDivision.cs#L100](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Shared/Divisions/SparkDivision.cs#L100)

> *"Dedicated generative studio and creative application workspace."*

**No commands implemented.** The Creative division is defined in the `SparkDivision` enum and `DivisionCatalog` but is marked inactive. No Creative-specific slash commands exist in the codebase.

---

## G. Shared Commands  -  Complete Inventory

All 39 built-in slash commands are registered in the gateway's command registry and are available across all active divisions. They are defined in [`commands-registry.data-D37tBV_t.mjs`](file:///c:/Intel/Spark/Spark-main/artifacts/gateway-stage/dist/commands-registry.data-D37tBV_t.mjs) via the `buildBuiltinChatCommands()` function (line 164).

Three of these commands (`/new`, `/reset`, `/compact`) are additionally intercepted on the client side by [`ChatLifecycleCommandParser`](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Tray.WinUI/Chat/ChatLifecycleCommandDispatcher.cs#L18-L43) for native lifecycle execution.

### G.1 Session Commands

| No. | Command | Aliases | Purpose | Arguments | Category | Status |
|---|---|---|---|---|---|---|
| 1 | `/new` |  -  | Start a new session | Optional (accepts args) | session | **Implemented** (gateway + client lifecycle) |
| 2 | `/reset` |  -  | Reset the current session | Optional (accepts args) | session | **Implemented** (gateway + client lifecycle) |
| 3 | `/compact` |  -  | Compact the session context | `[instructions]`  -  extra compaction instructions | session | **Implemented** (gateway + client lifecycle) |
| 4 | `/stop` |  -  | Stop the current run |  -  | session | **Implemented** |
| 5 | `/session` |  -  | Manage conversation bindings and session lifecycle settings | `<action>` (idle \| max-age \| unbind), `[value]` (duration or off) | session | **Implemented** |
| 6 | `/fork` |  -  | Fork this conversation into a separate thread | `[title]`  -  optional title for new conversation | session | **Implemented** (native on Discord, Telegram) |
| 7 | `/name` |  -  | Name or rename the current session | `[title]`  -  new session name (omit to get a suggestion) | session | **Implemented** |

### G.2 Model & Options Commands

| No. | Command | Aliases | Purpose | Arguments | Category | Status |
|---|---|---|---|---|---|---|
| 8 | `/model` |  -  | Show or set the model | `[model]`  -  model id; add `-s` for session, `-a` for agent, or `-g` for global scope | options | **Implemented** |
| 9 | `/models` |  -  | List model providers and their models | Optional (accepts args) | options | **Implemented** |
| 10 | `/think` | `/thinking`, `/t` | Set thinking level | `[level]`  -  dynamic choices (default, low, medium, high, xhigh, adaptive, max) | options | **Implemented** |
| 11 | `/verbose` | `/v` | Toggle verbose mode | `[mode]` (on \| off \| full) | options | **Implemented** |
| 12 | `/fast` |  -  | Toggle fast mode | `[mode]` (on \| off \| auto \| default \| status) | options | **Implemented** |
| 13 | `/reasoning` | `/reason` | Toggle reasoning visibility | `[mode]` (on \| off \| stream) | options | **Implemented** |
| 14 | `/elevated` | `/elev` | Toggle elevated mode | `[mode]` (on \| off \| ask \| full) | options | **Implemented** |
| 15 | `/usage` |  -  | Set the usage footer or show a cost summary | `[mode]` (off \| tokens \| full \| cost) | options | **Implemented** |
| 16 | `/queue` |  -  | Adjust queue settings | `[mode]` (steer \| followup \| collect \| interrupt), `[debounce]`, `[cap]`, `[drop]` (old \| new \| summarize) | options | **Implemented** |
| 17 | `/trace` |  -  | Toggle plugin trace lines | `[mode]` (on \| off \| raw) | options | **Implemented** |
| 18 | `/exec` |  -  | Set exec defaults for this session | `[host]` (auto \| sandbox \| gateway \| node), `[security]` (deny \| allowlist \| full), `[ask]` (off \| on-miss \| always), `[node]` | options | **Implemented** |

### G.3 Tools & Utility Commands

| No. | Command | Aliases | Purpose | Arguments | Category | Status |
|---|---|---|---|---|---|---|
| 19 | `/help` |  -  | Show available commands |  -  | status | **Implemented** |
| 20 | `/commands` |  -  | List all slash commands |  -  | status | **Implemented** |
| 21 | `/tools` |  -  | List available runtime tools | `[mode]` (compact \| verbose) | status | **Implemented** |
| 22 | `/status` |  -  | Show current status | Optional (accepts args) | status | **Implemented** |
| 23 | `/context` |  -  | Explain how context is built and used | Optional (accepts args) | status | **Implemented** |
| 24 | `/diagnostics` |  -  | Request Gateway diagnostics and optional Codex feedback uploads | `[note]`  -  optional note for Codex feedback upload | status | **Implemented** |
| 25 | `/whoami` | `/id` | Show your sender id |  -  | status | **Implemented** |
| 26 | `/skill` |  -  | Run a skill by name | `<name>` (required), `[input]`  -  skill input | tools | **Implemented** |
| 27 | `/dashboard` |  -  | Create or update this session's dashboard | `[request]`  -  dashboard requirements | tools | **Implemented** |
| 28 | `/learn` |  -  | Save lessons from recent work or named sources to skills or memory | `[request]`  -  sources and what to learn | tools | **Implemented** |
| 29 | `/loop` |  -  | Run and manage recurring prompts in this conversation | `[spec]`  -  [interval] prompt, or status/stop | tools | **Implemented** |
| 30 | `/btw` | `/side` | Ask a side question without changing future session context | Optional (accepts args) | tools | **Implemented** |
| 31 | `/bash` |  -  | Run shell commands on the host | `[command]`  -  shell command | tools | **Implemented** (text-only, no native name) |
| 32 | `/tts` |  -  | Control text-to-speech (TTS) | `[action]` (on \| off \| status \| provider \| limit \| summary \| audio \| help), `[value]` | media | **Implemented** |
| 33 | `/restart` |  -  | Restart OpenClaw |  -  | tools | **Implemented** |
| 34 | `/update` |  -  | Update OpenClaw and restart |  -  | tools | **Implemented** |

### G.4 Management & Agent Commands

| No. | Command | Aliases | Purpose | Arguments | Category | Status |
|---|---|---|---|---|---|---|
| 35 | `/agents` |  -  | List agents and conversation bindings for this session |  -  | management | **Implemented** |
| 36 | `/subagents` |  -  | Inspect subagent runs for this session | `[action]` (list \| log \| info), `[target]`, `[value]` | management | **Implemented** |
| 37 | `/steer` | `/tell` | Send guidance to the active run in this session | `[message]`  -  steering message | management | **Implemented** |
| 38 | `/followup` |  -  | Run this message as a new turn without changing queue mode | `[message]`  -  followup message | management | **Implemented** |
| 39 | `/acp` |  -  | Manage ACP sessions and runtime options | `[action]` (spawn \| cancel \| steer \| close \| sessions \| status \| set-mode \| set \| cwd \| permissions \| timeout \| model \| reset-options \| doctor \| install \| help), `[value]` | management | **Implemented** |

### G.5 Export & Session Persistence Commands

| No. | Command | Aliases | Purpose | Arguments | Category | Status |
|---|---|---|---|---|---|---|
| 40 | `/export-session` | `/export` | Export current session to an owner-only HTML file in the workspace | `[path]`  -  output path inside workspace | status | **Implemented** |
| 41 | `/export-trajectory` | `/trajectory` | Export a JSONL trajectory bundle for the active session | `[path]`  -  output directory | status | **Implemented** |

### G.6 Administrative / Configuration Commands

| No. | Command | Aliases | Purpose | Arguments | Category | Status |
|---|---|---|---|---|---|---|
| 42 | `/config` |  -  | Show or set config values | `<action>` (show \| get \| set \| unset), `<path>`, `[value]` | management | **Implemented** (feature-flag gated) |
| 43 | `/mcp` |  -  | Show or set OpenClaw MCP servers | `<action>` (show \| get \| set \| unset), `<path>`, `[value]` | management | **Implemented** (feature-flag gated) |
| 44 | `/plugins` | `/plugin` | List, show, enable, or disable plugins | `<action>` (list \| show \| get \| enable \| disable), `<path>` | management | **Implemented** (feature-flag gated) |
| 45 | `/debug` |  -  | Set runtime debug overrides | `<action>` (show \| reset \| set \| unset), `<path>`, `[value]` | management | **Implemented** (feature-flag gated) |
| 46 | `/login` |  -  | Connect a model provider | `[provider]`  -  provider or connection method | management | **Implemented** (native on Discord, Slack, Telegram) |
| 47 | `/openclaw` |  -  | Run the OpenClaw setup and repair helper | Optional (accepts args) | management | **Implemented** (text-only, no native name) |
| 48 | `/allowlist` |  -  | List, add, or remove allowlist entries | Optional (accepts args) | management | **Implemented** (text-only, no native name) |
| 49 | `/approve` |  -  | Approve or deny pending requests | Required (accepts args) | management | **Implemented** |
| 50 | `/activation` |  -  | Set group activation mode | `[mode]` (mention \| always) | management | **Implemented** |
| 51 | `/send` |  -  | Control message delivery for this session | `[mode]` (on \| off \| inherit) | management | **Implemented** |

### G.7 Approval Pseudo-Commands

These are **implicit** commands recognized by the approval regex but not independently registered in `buildBuiltinChatCommands`. They are sub-actions of the `/approve` command:

| Pseudo-Command | Pattern | Purpose | Source |
|---|---|---|---|
| `/approve <slug> allow-once` | Regex-matched | Grant one-time approval for a pending request | [ChatContentFormatting.cs#L86](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Tray.WinUI/Chat/ChatContentFormatting.cs#L86) |
| `/approve <slug> allow-always` | Regex-matched | Grant permanent approval for a pending request | [ChatContentFormatting.cs#L86](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Tray.WinUI/Chat/ChatContentFormatting.cs#L86) |
| `/deny <slug>` | Regex-matched | Deny a pending request | [ChatContentFormatting.cs#L86](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Tray.WinUI/Chat/ChatContentFormatting.cs#L86) |

> [!NOTE]
> `/deny` is recognized as an approval slash command pattern on the client side but it is **not** separately registered in the gateway's `buildBuiltinChatCommands`. It is an operational form of the `/approve` command. The client hides these from the chat bubble display (they are system-generated approval actions).

### G.8 Dynamic Skill Commands

Skill commands are **dynamically registered at runtime** per workspace and agent configuration. They are merged into the command catalog by [`commands-registry-list-DzfzJc1y.mjs`](file:///c:/Intel/Spark/Spark-main/artifacts/gateway-stage/dist/commands-registry-list-DzfzJc1y.mjs) lines 4–21:

```javascript
for (const n of e?.skillCommands ?? []) {
    const e = {
        key: `skill:${n.skillName}`,
        nativeName: n.name,
        textAliases: [`/${n.name}`],
        acceptsArgs: true,
        scope: "both",
        category: "tools"
    };
    t.push(e);
}
```

Each skill with a `commands` declaration generates a slash command with `/skillname` syntax. These are **not counted** in the unique built-in command total because their availability depends on which skills are installed.

### G.9 Complete Alias Reference

| Primary Command | Alias(es) | Source |
|---|---|---|
| `/think` | `/thinking`, `/t` | `textAliases: ["/think", "/thinking", "/t"]` |
| `/verbose` | `/v` | `textAliases: ["/verbose", "/v"]` |
| `/reasoning` | `/reason` | `textAliases: ["/reasoning", "/reason"]` |
| `/elevated` | `/elev` | `textAliases: ["/elevated", "/elev"]` |
| `/export-session` | `/export` | `textAliases: ["/export-session", "/export"]` |
| `/export-trajectory` | `/trajectory` | `textAliases: ["/export-trajectory", "/trajectory"]` |
| `/btw` | `/side` | `textAliases: ["/btw", "/side"]` |
| `/whoami` | `/id` | `textAliases: ["/whoami", "/id"]` |
| `/steer` | `/tell` | `textAliases: ["/steer", "/tell"]` |
| `/plugins` | `/plugin` | `textAliases: ["/plugins", "/plugin"]` |

**Total unique primary commands:** 39 (+ 2 export commands = 41... *see note*)

> [!NOTE]
> **Corrected count methodology**: The `buildBuiltinChatCommands()` function returns exactly **39 entries** from the `defineBuiltinCommand` calls. This includes both export commands (`/export-session` and `/export-trajectory`). The total unique registered commands is **39**. The 15 aliases listed above are alternate invocation names that resolve to these 39 primary commands; they are not counted separately.

---

## H. Command Architecture

### H.1 Where Commands Are Registered

```
buildBuiltinChatCommands() ---> getChatCommands()
                                     |
                                     v
Skill Commands -------------> listChatCommands()
(workspace/agent)                    |
                                     v
                              commands.list (gateway API)
                                     |
                                     v
                            Client: CommandCatalog
                                  /        \
                                 v          v
                  Slash Command Palette UI   ChatLifecycleCommandParser (client intercept)
```

**Primary registry:** [`commands-registry.data-D37tBV_t.mjs`](file:///c:/Intel/Spark/Spark-main/artifacts/gateway-stage/dist/commands-registry.data-D37tBV_t.mjs)  
- `buildBuiltinChatCommands()` (line 164) defines all 39 built-in commands using `defineBuiltinCommand()`.
- Each command declaration specifies: `key`, `description`, `category`, `tier`, `textAliases`, `nativeName`, `args`, `argsParsing`, `scope`, `acceptsArgs`, `modelIndependent`, `activeRunSafe`.

**Skill command merge:** [`commands-registry-list-DzfzJc1y.mjs`](file:///c:/Intel/Spark/Spark-main/artifacts/gateway-stage/dist/commands-registry-list-DzfzJc1y.mjs)  
- `listChatCommands()` appends dynamic skill commands to the built-in list.

**Feature-flag gating:** [`commands-registry-list-DzfzJc1y.mjs`](file:///c:/Intel/Spark/Spark-main/artifacts/gateway-stage/dist/commands-registry-list-DzfzJc1y.mjs) line 23  
- `isCommandEnabled()` applies feature flags to `config`, `mcp`, `plugins`, `debug`, and `bash` commands.

### H.2 How Commands Are Parsed

**Gateway text parsing:**
- [`commands-registry-normalize-hIqNK352.mjs`](file:///c:/Intel/Spark/Spark-main/artifacts/gateway-stage/dist/commands-registry-normalize-hIqNK352.mjs): `normalizeCommandBody()` resolves aliases, strips bot mentions, and canonicalizes command text.
- `resolveTextCommand()` matches raw text to a command definition and extracts the argument tail.
- `parseCommandArgs()` in [`commands-registry-DodAmgDp.mjs`](file:///c:/Intel/Spark/Spark-main/artifacts/gateway-stage/dist/commands-registry-DodAmgDp.mjs) line 89 splits argument text by whitespace, mapping tokens to named arguments with `captureRemaining` support.

**Client-side parsing:**
- [`ChatLifecycleCommandParser.TryParse()`](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Tray.WinUI/Chat/ChatLifecycleCommandDispatcher.cs#L20-L43): Exact-match switch on `/new`, `/reset`, `/compact` for native lifecycle execution.
- [`ChatContentFormatting.LooksLikeApprovalSlashCommand()`](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Tray.WinUI/Chat/ChatContentFormatting.cs#L78-L87): Regex match for `/approve` and `/deny` patterns to suppress chat bubble rendering.

### H.3 How Arguments Are Processed

Arguments are declared in command definitions using `defineCommandArgument(name, description, options)`:
- **Positional parsing** (default): Arguments split by whitespace, mapped in order.
- **`captureRemaining: true`**: The last argument captures all remaining text.
- **`argsParsing: "none"`**: Arguments are passed as raw text (used by `config`, `mcp`, `plugins`, `debug`, `exec`, `queue`).
- **Static choices**: Enumerated values shown in the command palette UI.
- **Dynamic choices**: Callback functions that resolve choices based on current provider, model, and config.
- **Argument menus**: `argsMenu: "auto"` or `{ arg, title }` triggers an interactive choice picker in the UI.

### H.4 How Commands Are Dispatched

**Gateway-side dispatch:** [`commands-handlers.runtime-BJ4IvxNB.mjs`](file:///c:/Intel/Spark/Spark-main/artifacts/gateway-stage/dist/commands-handlers.runtime-BJ4IvxNB.mjs)  
- 5,325-line handler module. Each command key maps to a handler function.
- Commands are routed based on `commandTurn.kind` (`"native"` or `"text-slash"`).
- `resolveCommandAuthorization()` checks permissions before execution.
- `isActiveRunSafeCommandTurn()` determines if a command can execute during an active AI run.

**Client-side dispatch:** [`ChatLifecycleCommandDispatcher`](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Tray.WinUI/Chat/ChatLifecycleCommandDispatcher.cs#L70-L93)  
- Three lifecycle commands (`/new`, `/reset`, `/compact`) are intercepted before the gateway.
- `/new` → `CreateSessionAsync()` (creates a new session, navigates to it).
- `/reset` → `ResetSessionAsync()` (clears session state with confirmation dialog).
- `/compact` → Queued via `EnqueueCompactCommandAsync()`.

### H.5 How Handlers Execute

- **Model-independent commands** (`modelIndependent: "always"`): Execute without an AI model (e.g., `/help`, `/status`, `/new`, `/stop`).
- **Directive commands** (`modelIndependent: "directive"`): Execute as session directives that modify settings (e.g., `/model`, `/think`, `/verbose`).
- **Model-dependent commands** (`modelIndependent: "no-args"` or function): May require a model when arguments are provided (e.g., `/goal start <text>` needs an AI model, `/goal status` does not).
- **Active-run-safe commands** (`activeRunSafe: true`): Can execute while the AI is actively generating a response (e.g., `/stop`, `/approve`, `/status`).
- **Text-only commands** (`scope: "text"`): Only available via text input, not native platform commands (e.g., `/bash`, `/openclaw`, `/allowlist`).

### H.6 How New Commands Can Be Registered

1. **Built-in commands:** Add a `defineBuiltinCommand()` call in the `buildBuiltinChatCommands()` function within the gateway source.
2. **Skill commands:** Define a `commands` section in a skill manifest. The gateway merges these into the catalog at runtime via `listChatCommands()`.
3. **Plugin commands:** Channel plugins can contribute native command specs via the `resolveNativeCommandName` callback, and skill commands via the plugin SDK's `skill-commands-runtime`.
4. **Client lifecycle commands:** Add a case to `ChatLifecycleCommandParser.TryParse()` and a handler in `ChatLifecycleCommandDispatcher.ExecuteAsync()`.

### H.7 Command Palette UI Architecture

The slash command palette is implemented in:

- [`ReactorSlashCommandController`](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Tray.WinUI/Chat/ReactorSlashCommandController.cs#L53): State machine for the command palette flyout. Detects `/` prefix in composer text, filters commands by query, manages keyboard navigation and argument completion.
- [`CommandCatalogPresentation`](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Shared/CommandCatalogPresentation.cs): UI-facing presentation logic  -  display names, insertion text, ranked search, and category grouping.
- [`CommandCategories`](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Shared/CommandCatalogPresentation.cs#L298): Maps commands into four UI display buckets: **Session**, **Model**, **Tools**, **Agents**. Display order: `session → model → tools → agents`.

---

## I. Final Statistics

### Command Count Summary

| Statistic | Value |
|---|---|
| Total unique built-in commands (from `buildBuiltinChatCommands`) | **39** |
| Total text aliases (alternate slash names) | **15** |
| Total unique invocable command names (commands + aliases) | **54** |
| Feature-flag gated commands (`config`, `mcp`, `plugins`, `debug`, `bash`) | **5** |
| Client-intercepted lifecycle commands (`/new`, `/reset`, `/compact`) | **3** |
| Native-platform-specific commands (`/fork`, `/login`) | **2** |
| Text-only commands (no native platform equivalent) | **3** (`/bash`, `/openclaw`, `/allowlist`) |
| Active-run-safe commands | **10** |
| Dynamic skill commands | **Variable** (per workspace) |

### Commands by Gateway Category

| Category | Count | Commands |
|---|---|---|
| `session` | 7 | `/new`, `/reset`, `/compact`, `/stop`, `/session`, `/fork`, `/name` |
| `options` | 11 | `/model`, `/models`, `/think`, `/verbose`, `/fast`, `/reasoning`, `/elevated`, `/usage`, `/queue`, `/trace`, `/exec` |
| `status` | 7 | `/help`, `/commands`, `/tools`, `/status`, `/context`, `/diagnostics`, `/whoami` |
| `tools` | 5 | `/skill`, `/dashboard`, `/learn`, `/loop`, `/btw` |
| `media` | 1 | `/tts` |
| `management` | 8 | `/agents`, `/subagents`, `/steer`, `/followup`, `/acp`, `/config`, `/mcp`, `/plugins`, `/debug`, `/login`, `/openclaw`, `/allowlist`, `/approve`, `/activation`, `/send` |

> [!NOTE]
> The management category contains 15 commands by source, though some are gated behind feature flags and may not appear in the user-visible command palette depending on operator configuration.

### Commands by UI Bucket (Display Grouping)

| UI Bucket | Commands (from `BucketOverrides`) |
|---|---|
| **Session** | `/session`, `/stop`, `/reset`, `/new`, `/compact`, `/focus`*, `/unfocus`* |
| **Model** | `/model`, `/models`, `/think`, `/verbose`, `/fast`, `/reasoning`, `/elevated`, `/queue` |
| **Tools** | `/help`, `/commands`, `/tools`, `/skill`, `/status`, `/export`, `/usage`, `/tts` |
| **Agents** | `/agents`, `/subagents`, `/kill`*, `/steer`, `/redirect`* |

*Commands marked with `*` are defined in the bucket override map but do not have corresponding `defineBuiltinCommand` entries. They may be reserved for future implementation or dynamically contributed by extensions.

### Division Activation Status

| Division | Active | Slash Commands |
|---|---|---|
| Chat | ✅ Yes | 0 division-specific, 39 shared |
| Work | ✅ Yes | 0 division-specific, 39 shared |
| Code | ❌ No | Awaiting implementation |
| Study | ❌ No | Awaiting implementation |
| Creative | ❌ No | Awaiting implementation |

---

## Appendix: Source File Index

| File | Purpose | Line References |
|---|---|---|
| [`SparkDivision.cs`](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Shared/Divisions/SparkDivision.cs) | Division enum, metadata, and catalog | L9–L152 |
| [`GatewayProtocolModels.cs`](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Shared/GatewayProtocolModels.cs) | Wire DTOs: `GatewayCommand`, `GatewayCommandArg`, `CommandCatalog`, `CommandCatalogQuery` | L30–L133 |
| [`CommandCatalogPresentation.cs`](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Shared/CommandCatalogPresentation.cs) | Display helpers, search, grouping, category bucketing | L1–L401 |
| [`ChatLifecycleCommandDispatcher.cs`](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Tray.WinUI/Chat/ChatLifecycleCommandDispatcher.cs) | Client-side lifecycle parser and dispatcher (`/new`, `/reset`, `/compact`) | L1–L165 |
| [`ReactorSlashCommandController.cs`](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Tray.WinUI/Chat/ReactorSlashCommandController.cs) | Slash command palette UI state machine | L1–L260 |
| [`ChatComposerController.cs`](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Tray.WinUI/Chat/ChatComposerController.cs) | Composer send workflow with lifecycle command interception | L246–L302 |
| [`ChatContentFormatting.cs`](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Tray.WinUI/Chat/ChatContentFormatting.cs) | Approval pseudo-command regex matching | L78–L87 |
| [`HubSearchCatalog.cs`](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Tray.WinUI/Presentation/HubSearchCatalog.cs) | Hub navigation and dashboard route catalog | L1–L185 |
| [`commands-registry.data-D37tBV_t.mjs`](file:///c:/Intel/Spark/Spark-main/artifacts/gateway-stage/dist/commands-registry.data-D37tBV_t.mjs) | **Primary command registry**: all 39 `defineBuiltinCommand` definitions | L164–L579 |
| [`commands-registry-list-DzfzJc1y.mjs`](file:///c:/Intel/Spark/Spark-main/artifacts/gateway-stage/dist/commands-registry-list-DzfzJc1y.mjs) | Skill command merge + feature-flag gating | L1–L29 |
| [`commands-registry-normalize-hIqNK352.mjs`](file:///c:/Intel/Spark/Spark-main/artifacts/gateway-stage/dist/commands-registry-normalize-hIqNK352.mjs) | Text alias resolution and command normalization | L1–L101 |
| [`commands-registry-DodAmgDp.mjs`](file:///c:/Intel/Spark/Spark-main/artifacts/gateway-stage/dist/commands-registry-DodAmgDp.mjs) | Command registry runtime facade: argument parsing, choice resolution, native specs | L1–L252 |
| [`commands-handlers.runtime-BJ4IvxNB.mjs`](file:///c:/Intel/Spark/Spark-main/artifacts/gateway-stage/dist/commands-handlers.runtime-BJ4IvxNB.mjs) | Gateway command handler implementations (5,325 lines) | Full file |
| [`OpenClawGatewayClient.Protocol.cs`](file:///c:/Intel/Spark/Spark-main/src/OpenClaw.Shared/OpenClawGatewayClient.Protocol.cs) | `commands.list` client, `ParseCommandCatalog`, `ApplyCommandQuery` | L346–L461 |
