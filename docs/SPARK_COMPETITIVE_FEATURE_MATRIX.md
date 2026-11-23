# SPARK Competitive Feature Matrix

This matrix compares SPARK across all 20 capability divisions against the four primary commercial reference targets (OpenAI Dots, xAI Grok Bot, Meta Muse, Anthropic Claude Duet) and verified open-source implementations.

| Capability Domain | Target Product Reference | Open-Source Exemplar | SPARK Status | Implementation Evidence in SPARK | Remaining Work |
|---|---|---|---|---|---|
| **Division 1: Core Intelligence** | All targets | OpenFuse / Open Duet | Complete | `OpenClaw.Shared/Capabilities/OllamaCapability.cs`, `OpenClaw.Chat/ChatModels.cs` | Expand task-based model router |
| **Division 2: Personal Identity & Memory** | Dots / Duet | Open Duet / Rakazo | Existing | `VoiceMode/runtime/memory/memory_manager.py` | Add SQLite Memory Journal and Heuristic Proposer |
| **Division 3: Agent Runtime & Lifecycle** | Grok Bot / Dots | OpenMausBot / Open Duet | Existing | `OpenClaw.Connection/NativeGateway/SparkManagedGatewayRuntime.cs` | Add persistent task queue and Turn Watchdog |
| **Division 4: Specialist Agents & Teams** | Grok Bot | Open Duet / Rakazo | Planned | `src/OpenClaw.Shared/ChatToolHandoff.cs` (`sessions_yield`) | Implement Team Rooms and Specialist Coordinator |
| **Division 5: Persistent Computer Environment** | Muse / Dots | OpenFuse / nanoMuse | Existing | `OpenClaw.Shared/Mxc/NodeSandboxExecutor.cs` (MXC Sandboxing) | Add workspace snapshotting |
| **Division 6: Browser Automation** | Muse / Dots | Rakazo / nanoMuse | Planned | `OpenClaw.Shared/Capabilities/BrowserProxyCapability.cs` | Add native isolated Edge CDP capability |
| **Division 7: Computer Use** | Dots / Muse | nanoMuse | Existing | `OpenClaw.Shared/Capabilities/ScreenCapability.cs`, `CameraCapability.cs` | Add synthetic input with confirmation guards |
| **Division 8: Skills & Learning** | Grok Bot / Duet | nanoMuse / Rakazo | Existing | `src/skills/` skill manifests and runtime | Add demonstration skill recorder |
| **Division 9: Tools & Integrations** | Dots / Duet | Open Duet / nanoMuse | Complete | `OpenClaw.Shared/Mcp/McpHttpServer.cs`, `McpToolBridge.cs` | Add OAuth connector framework |
| **Division 10: Proactive Intelligence** | Duet / Dots | Open Duet / Rakazo | Planned | Gateway cron triggers | Add Windows Autonomous Routine Scheduler |
| **Division 11: Goal & Project Management** | Dots / Muse | Open Duet | Planned | Session worktrees in Sessions Hub | Add multi-project goal planner |
| **Division 12: Voice Intelligence** | Dots / Grok Bot | SPARK | Complete | `SttCapability.cs` (Whisper.net), `TtsCapability.cs` (Kokoro), VoiceMode Live | Add voice activity interruption handling |
| **Division 13: Multimodal Intelligence** | All targets | OpenFuse / SPARK | Existing | Canvas WebView2, Screen GDI+, Camera MediaCapture | Add cross-modal image reasoning pipeline |
| **Division 14: File & Knowledge Management** | Dots / Duet | Open Duet / Rakazo | Existing | `OpenClaw.FileWorker/`, `FileTransferClient.cs` | Add semantic SQLite document retrieval |
| **Division 15: Research & Information** | Dots / Duet | Rakazo / OpenMausBot | Planned | Web search adapters | Add multi-query research agent |
| **Division 16: Coding & Development** | Dots / Duet | Open Duet / OpenMausBot | Existing | `OpenClaw.Tray.WinUI/Sessions/SessionsHubWorktreesView.cs` | Add Git worktree isolation agent |
| **Division 17: Communication & Notifications** | All targets | SPARK / Open Duet | Complete | WinUI 3 Toast Notifications, Reactor timeline, InfoBars | Add multi-channel messaging bridges |
| **Division 18: Governance & Security** | Muse / Duet | Open Duet / nanoMuse | Existing | `ExecApprovalsStore.cs`, DPAPI storage, Ed25519 handshakes | Add Windows Safety Floor & HMAC Guard |
| **Division 19: Observability & Reliability** | Grok Bot | OpenMausBot | Existing | `ConnectionDiagnostics.cs`, `OpenClawTelemetry.cs` | Add Turn Watchdog stall monitor |
| **Division 20: User Experience** | Dots / Grok Bot | SPARK | Complete | WinUI 3 Fluent Mica UI, Command Palette, Sessions Hub | Integrate memory and team room views |
