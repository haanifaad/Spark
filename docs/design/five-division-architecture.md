# SPARK - Five-Division Architecture Specification & Migration Plan

## 1. Architectural Vision

SPARK is restructured into **five primary divisions**, maintaining a single unified desktop and web application while establishing clean, decoupled architectural boundaries:

```
+--------------------------------------------------------------------------------------------------+
|                                      SPARK APPLICATION SHELL                                     |
|           (Windows 11 Native Desktop WinUI 3 Host  /  SolidJS Modern Web Control UI)            |
+--------------------------------------------------------------------------------------------------+
|                                    PRIMARY DIVISION SELECTOR                                     |
|     [ 1. Chat ]     [ 2. Work ]     [ 3. Code ]     [ 4. Study ]     [ 5. Creative ]            |
+--------------------------------------------------------------------------------------------------+
|                                                                                                  |
|  1. CHAT DIVISION          2. WORK DIVISION         3. CODE DIVISION        4. STUDY / 5. CREATIVE   |
|  - Conversational AI       - Workboard              - Dedicated Dev         - Research & Learning    |
|  - Session Context         - Worktrees                Workspace             - Creative Studio        |
|  - Multi-Turn Reasoning    - Tasks & Automations    - OpenCode Seam         - Dedicated Architectural|
|  - New implementation        (Cron)                 - Hermes Agent Seam       Boundaries             |
|    isolated for separate   - Dashboards             - Zero fake features    - Separate feature       |
|    development             - Sessions & Execution   - Preserved contracts     development            |
|                                                                                                  |
+--------------------------------------------------------------------------------------------------+
|                                      SHARED INFRASTRUCTURE                                       |
|  - Authentication, Approvals & Secrets              - Model Setup & Providers (Local / Cloud)    |
|  - Gateway Client & Network Protocol                - Device Identity & Node Management          |
|  - Application Context, Memory & Preferences       - Windows 11 Design System Tokens & Styling  |
+--------------------------------------------------------------------------------------------------+
```

---

## 2. Division Definitions & Scope

### Division 1: Chat (`chat`)
- **Role**: Primary conversational AI workspace.
- **Current State**: Backed by the existing chat workspace while establishing a clear division boundary so the new conversational implementation can be developed independently without affecting other divisions.
- **Route Boundary**: `/chat`, `/new`, `/chat/:sessionKey`.
- **Isolation Rule**: Chat interacts only with shared runtime services (Gateway, Models, Auth). It does not import or couple to Workboard, Worktrees, or specific productivity tools.

### Division 2: Work (`work`)
- **Role**: Productivity and task execution division.
- **Current State**: Existing implementation in the repository. Preserves all existing functionality, tools, agents, workboards, worktrees, dashboards, cron/automations, and session management.
- **Route Boundary**: `/work` (primary hub), with sub-routes `/workboard`, `/worktrees`, `/automations` (alias `/cron`), `/dashboards`, `/sessions`, `/terminal`.
- **Integrations**: Workboard contract, worktrees model, cron task runner, dashboard projection.
- **Non-Negotiable**: No rebuilding or replacement of Work. Source code and existing workflows are preserved intact.

### Division 3: Code (`code`)
- **Role**: Dedicated development and programming workspace.
- **Target Integrations**: Architected for future integration of **OpenCode** and **Hermes Agent**.
- **Current State**: Architectural boundary and route definition established. Integrations are NOT implemented yet, avoiding mockups or fake implementations.
- **Route Boundary**: `/code`.
- **Isolation Rule**: Independent division module ready for coding agent engines.

### Division 4: Study (`study`)
- **Role**: Dedicated learning, reading, knowledge synthesis, and research workspace.
- **Current State**: Completely new division. Architectural boundary and route definition established. Features will be developed separately.
- **Route Boundary**: `/study`.
- **Isolation Rule**: Zero dependencies on division-specific logic of Chat, Work, or Code.

### Division 5: Creative (`creative`)
- **Role**: Dedicated generative and creative application workspace (media generation, design prompts, canvas workflows).
- **Current State**: Dedicated workspace division. Architectural boundary established without building placeholder features.
- **Route Boundary**: `/creative`.
- **Isolation Rule**: Standalone division boundary interacting with shared media and inference infrastructure.

---

## 3. Shared Infrastructure Boundary

The shared infrastructure serves all five divisions without belonging to any single division:

1. **Authentication & Security** (`shared/auth` / `pages/approvals`, `pages/secrets`):
   - Executive approvals, security policies, token credentials, and cryptographic signing.
2. **Gateway & Transport** (`packages/gateway-client`, `packages/gateway-protocol`):
   - Real-time RPC, WebSocket multiplexing, connection recovery, subscription feeds.
3. **Model Management & Inference** (`pages/model-setup`, `pages/model-providers`, `OpenClaw.Shared/Inference`):
   - Local AI models, cloud providers, fallback hierarchies, temperature and parameter configurations.
4. **Settings & System Preferences** (`pages/config`, `pages/profile`, `pages/appearance`, `pages/systems`, `pages/usage`):
   - Application configuration, node telemetry, resource meters, theme tokens.
5. **UI Shell & Foundations** (`components/`, `styles/`, WinUI 3 Controls):
   - Windows 11 Dark Desktop styling tokens, title bar chrome, navigation rail, modal dialogs, icon system.

---

## 4. Cross-Division Communication Rules

1. **Strict Decoupling**: Direct cross-division imports (e.g. `divisions/chat` importing from `divisions/work`) are forbidden.
2. **Shared Contracts**: Divisions communicate exclusively via:
   - Shared route locations (`app-route-paths.ts`).
   - Shared Gateway protocol messages (`@openclaw/gateway-protocol`).
   - Shared application context (`ApplicationContext`).
3. **No Duplicate Services**: Gateway connection, auth tokens, model providers, and notification bus exist as singletons in shared infrastructure.
4. **Backward Compatibility**: All pre-restructure URLs, bookmarks, deep links, and aliases continue to resolve seamlessly.
