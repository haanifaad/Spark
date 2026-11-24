# SPARK UI SCREEN INVENTORY

**Project:** SPARK Native Windows AI Workstation  
**Audit & Extraction Date:** November 24, 2026  
**Application Architecture:** .NET 10 / Windows App SDK (WinUI 3) Unpackaged Desktop Application  
**Execution Environment:** Isolated Synthetic Gateway Fixture & Native Windows Shell  
**Total Screenshots Captured:** 40  

---

## Screen Inventory Register

| # | Screenshot Filename | Screen Name | Parent Section | Navigation Path | Description & Visible UI Components | Screen Type |
|---|---|---|---|---|---|---|
| 01 | `spark-initial-overview.png` | SPARK Workspace Shell | 00-Overview | Launch Entry / Shell Initial | TitleBar with DivisionSwitcher (Chat, Work, Code, Study, Creative, Voice Mode), Sidebar navigation pane, Chat message host, composer. | Full Page |
| 02 | `spark-workspace-overview.png` | SPARK Workspace Overview | 00-Overview | `workspace:home` | Full application workspace overview showing dual-pane layout: Sidebar Navigation with Sessions and Chat Conversation pane. | Full Page |
| 03 | `dashboards-gallery.png` | Dashboards Gallery | 01-Dashboard | Settings Rail > `dashboards` | Central telemetry & analytics dashboards gallery for gateway health, performance, and session analytics. | Full Page |
| 04 | `work-division-autonomous-agent.png` | Work Division - Autonomous Personal Agent | 01-Dashboard | DivisionSwitcher > `Work` | Autonomous Background Worker deck, Task launcher, Proactive routines scheduler, and Autonomous task cards. | Full Page |
| 05 | `work-division-tasks-tab.png` | Work Division - Tasks & Execution | 01-Dashboard | Work Division > `NavTabTasks` | Task submission form, multi-step plan configuration, execution log stream, Sentinel safety limit status. | Full Page (Sub-tab) |
| 06 | `chat-composer-reasoning-picker-flyout.png` | Reasoning Picker Flyout | 02-AI-Core | Chat Composer > Reasoning Button | Popover flyout offering reasoning effort levels (off, low, medium, high) and chain-of-thought toggles. | Popover / Flyout |
| 07 | `work-division-tools-mcp-registry.png` | Work Division - Tools & MCP Registry | 02-AI-Core | Work Division > `NavTabTools` | Active MCP servers list, registered tool capabilities, tool execution permissions, and invocation counters. | Full Page (Sub-tab) |
| 08 | `chat-empty-state.png` | Chat - Empty Conversation State | 03-Chat | Sidebar > New Conversation | Clean slate chat interface with empty prompt suggestions, composer input box, and model badge. | Empty State |
| 09 | `chat-fixture-long-session.png` | Chat - Multi-Message History Session | 03-Chat | Sidebar > Fixture Long Session | Scrollable message timeline with user speech bubbles, assistant cards, copy/branch actions, and token counters. | Full Page |
| 10 | `chat-main.png` | Chat - Main Division View | 03-Chat | DivisionSwitcher > `Chat` | Default conversational AI division with active session messages, token budget gauge, and media input actions. | Full Page |
| 11 | `voice-settings-audio.png` | Voice & Audio Settings | 04-Voice | Settings Rail > `voice` | Speech-to-text (STT) and text-to-speech (TTS) engine configuration, voice synthesis selection, playback volume. | Full Page |
| 12 | `sessions-hub-roster.png` | Sessions Hub & Agent Roster | 05-Agents | Settings Rail > `sessions` | Active sessions table with session keys, agent assignments, token consumption metrics, and state badges. | Full Page |
| 13 | `work-division-persona-customization.png` | Work Division - Persona Customization | 05-Agents | Work Division > `NavTabPersona` | Agent persona prompt editor, tone adjustments, role definitions, and system instruction constraints. | Full Page (Sub-tab) |
| 14 | `work-division-team-rooms.png` | Work Division - Team Rooms | 05-Agents | Work Division > `NavTabRooms` | Multi-agent collaborative rooms, participant roster, cross-agent coordination protocols, and message dispatch. | Full Page (Sub-tab) |
| 15 | `work-division-memory-store.png` | Work Division - Memory Store | 06-Memory | Work Division > `NavTabMemory` | SQLite persistent vector store, learned user facts, context retention keys, and memory search query bar. | Full Page (Sub-tab) |
| 16 | `chat-composer-model-picker-flyout.png` | Model Selector Flyout | 07-Models | Chat Composer > Model Button | Flyout menu listing configured AI inference models across Ollama, Cloud providers, and custom endpoints. | Popover / Flyout |
| 17 | `work-division-model-router.png` | Work Division - Model Router | 07-Models | Work Division > `NavTabModels` | Intelligent model routing table, latency/cost failover rules, local Ollama vs cloud fallbacks. | Full Page (Sub-tab) |
| 18 | `connection-gateway-manager.png` | Connection & Gateway Manager | 08-Providers | Settings Rail > `connection` | Gateway connection endpoint, active token status, pairing wizard triggers, and reconnect actions. | Full Page |
| 19 | `code-division-main.png` | Code Division - Main IDE Shell | 09-Productivity | DivisionSwitcher > `Code` | Integrated development environment shell, repository file explorer, code tabs, and syntax highlighter. | Full Page |
| 20 | `code-division-terminal-shell.png` | Code Division - Terminal Shell | 09-Productivity | Code Division > Terminal Tab | Embedded interactive developer console, command-line execution, build script runners, and stdout stream. | Full Page (Sub-tab) |
| 21 | `creative-app-vectorspark-selected.png` | Creative Division - VectorSpark Selected | 09-Productivity | Creative Division > VectorSpark App | Detailed app inspector for VectorCraft engine, Bézier curve tools, SVG export capabilities, and mascot motif. | Full Page (Sub-state) |
| 22 | `creative-division-studio.png` | Creative Division - 16 Studio Apps Grid | 09-Productivity | DivisionSwitcher > `Creative` | 16-app creative studio suite (VectorSpark, WordSpark, GridSpark, TextureSpark, SoundSpark, FilmSpark, etc.). | Full Page |
| 23 | `automations-cron-schedules.png` | Automations & Cron Schedules | 10-Automation | Settings Rail > `cron` | Scheduled recurring cron jobs, background task triggers, interval timers, and automation status toggles. | Full Page |
| 24 | `computer-use-automation.png` | Computer Use Automation | 10-Automation | Settings Rail > `computer-use` | UI-TARS desktop computer use automation panel, screenshot perception settings, and PyAutoGUI safety rails. | Full Page |
| 25 | `work-division-channels-integration.png` | Work Division - Channels Integration | 12-Integrations | Work Division > `NavTabChannels` | External messaging bridges (Discord, Slack, Telegram, WhatsApp, Email) and webhook connection status. | Full Page (Sub-tab) |
| 26 | `about-system-info.png` | About & System Information | 13-Settings | Settings Rail > `about` | SPARK version info, open source licenses, copyright attribution, GitHub repository links, and build metadata. | Full Page |
| 27 | `config-gateway-configuration.png` | Configuration & Raw Schema Editor | 13-Settings | Settings Rail > `config` | Dynamic gateway JSON schema editor, configuration trees, environment variable bindings, and save buttons. | Full Page |
| 28 | `settings-general.png` | General Application Settings | 13-Settings | Settings Rail > `settings` | Fluent theme switcher (Light, Dark, System), startup behavior, language selector, and global shortcuts. | Full Page |
| 29 | `code-division-security-audit.png` | Code Division - Security & Vulnerability Audit | 14-Security | Code Division > Security Tab | Shannon cybersecurity audit engine, dependency vulnerability checks, code security review reports. | Full Page (Sub-tab) |
| 30 | `permissions-capabilities.png` | Permissions & Capabilities Matrix | 14-Security | Settings Rail > `permissions` | Granular capability toggles: File system, Screen capture, Camera, Location, Terminal, and Network access. | Full Page |
| 31 | `sandbox-environment.png` | Sandbox Environment Policy | 14-Security | Settings Rail > `sandbox` | Execution sandbox boundaries, isolation levels, command whitelist policies, and path access restrictions. | Full Page |
| 32 | `work-division-approvals-audit.png` | Work Division - Approvals & Forensic Audit | 14-Security | Work Division > `NavTabApprovals` | Human-in-the-loop pending approval requests, high-impact mutation reviews, and tamper-evident audit logs. | Full Page (Sub-tab) |
| 33 | `diagnostics-debug-page.png` | Diagnostics & Debugging Console | 15-System-Management | Settings Rail > `debug` | Real-time diagnostic bundle exporter, internal event traces, error log viewer, and RPC health monitor. | Full Page |
| 34 | `systems-nodes-management.png` | Systems & Distributed Nodes | 15-System-Management | Settings Rail > `systems` | Connected Windows nodes, remote devices, hardware specifications, CPU/GPU utilization, and capability counts. | Full Page |
| 35 | `usage-metrics-costs.png` | Usage & Token Costs Analytics | 15-System-Management | Settings Rail > `usage` | 30-day inference cost tracking, prompt/completion token consumption curves, and model cost breakdowns. | Full Page |
| 36 | `assistant-selector-dropdown.png` | Assistant & Agent Switcher Dropdown | 17-Menus-and-Panels | Sidebar Header > Assistant Dropdown | Dropdown combo listing available AI agent personas, active assistant profiles, and 'New Agent' creation option. | Dropdown Menu |
| 37 | `notifications-inbox-page.png` | Notifications Inbox Center | 17-Menus-and-Panels | Settings Rail > `notifications` | Notification history feed, system alerts, execution completions, and approval request notices. | Full Page |
| 38 | `workspace-inbox-notifications-flyout.png` | Inbox Notifications Flyout | 17-Menus-and-Panels | Sidebar Footer > Bell Icon | Compact quick-access inbox popup flyout showing unread notifications and pending action items. | Popover / Flyout |
| 39 | `workspace-owner-account-menu.png` | User Account & Gateway Menu | 17-Menus-and-Panels | Sidebar Footer > User Profile Button | Context menu showing current operator profile, active gateway connection status, and quick settings links. | Context Menu |
| 40 | `workspace-search-flyout.png` | Universal Command Search Flyout | 17-Menus-and-Panels | Sidebar Header > Search Icon | Search popup offering instant navigation across divisions, model switches, and settings destinations. | Popover / Flyout |

---

## Directory Distribution Summary

- `00-Overview/`: 2 screenshots
- `01-Dashboard/`: 3 screenshots
- `02-AI-Core/`: 2 screenshots
- `03-Chat/`: 3 screenshots
- `04-Voice/`: 1 screenshot
- `05-Agents/`: 3 screenshots
- `06-Memory/`: 1 screenshot
- `07-Models/`: 2 screenshots
- `08-Providers/`: 1 screenshot
- `09-Productivity/`: 4 screenshots
- `10-Automation/`: 2 screenshots
- `12-Integrations/`: 1 screenshot
- `13-Settings/`: 3 screenshots
- `14-Security/`: 4 screenshots
- `15-System-Management/`: 3 screenshots
- `17-Menus-and-Panels/`: 5 screenshots

**Total Verified Screenshots:** 40
