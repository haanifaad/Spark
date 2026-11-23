# SPARK — Architecture Gap Analysis
**Document Version:** 1.0.0  
**Baseline:** Historical November 23, 2026 Audit vs. Current Codebase vs. Target Paradigms  
**Scope:** Identification of Architectural Deficiencies, Remediation Status, and Remaining Work  

---

## 1. Architectural Gap Assessment

| Area | Historical Defect / Gap | Current Codebase Status | Remediation & Production Solution | Remaining Target Work |
|---|---|---|---|---|
| **Coding Division** | `CodingPage.xaml.cs` had `SendPromptAsync` called without model generator; marked work complete without real LLM edits. | **Remediated** | Wired `CodingPage.xaml.cs` to `ISparkModelRouter` with `qwen2.5-coder:7b` for real code synthesis and diff generation. | Add inline side-by-side Monaco diff editor in WinUI. |
| **Study Division** | `StudyTutorService.cs` had static response generation and artificial 15 ms delay; zero LLM inference. | **Remediated** | Replaced synthetic delay with live Socratic inference routing via `llama3.2:3b`. | Persist student flashcard decks to SQLite. |
| **Autonomous Scheduler** | `SparkAutonomousScheduler.cs` had artificial 50 ms delay and hardcoded task result strings. | **Partially Remediated** | Replaced 50ms delay with active task execution, atomic disk persistence (`.tmp`, `.bak`), and template factories. | Connect custom task executor directly to `ISparkAgentOrchestrator` for autonomous multi-step reasoning. |
| **Voice Division** | Voice stack split between disconnected C# pipeline and detached PyQt6 JARVIS window. | **Remediated** | Scoped Mark-LVII Blue HUD (`#00060A`, `#00D4FF`) strictly to `VoiceModePage.xaml`; added Arc Reactor vs 3D Face visual mode selector; enforced Malayalam/English prompt separation. | Complete WebRTC/WebSocket audio streaming pipe. |
| **Computer Use** | PyAutoGUI failsafe was disabled (`FAILSAFE = False`); UI-TARS integration missing. | **Remediated** | Restored `pyautogui.FAILSAFE = True`; implemented UI-TARS 1000-normalized coordinate grounding in `computer_control.py` and C# `IVoiceModeDivisionService`. | Add automated screen element bounding box overlay in WinUI. |
| **Creative Suite** | `CreativePage.xaml.cs` had synthetic 400 ms delay; hardcoded `C:\Intel\Spark\Craft` paths caused tests to fail. | **Remediated** | Repointed `CreativeAppRegistry.cs` and `CreativeLifecycleManager.cs` to dynamically resolve all 17 ArtCraft applications from `SPARK-Reference-Library`; zero test failures. | Implement local Stable Diffusion webui / ComfyUI IPC connector. |
| **Security & Secrets** | Plaintext API keys committed in `api_keys.json`. | **Remediated** | Purged plaintext keys from tracked configuration; updated code to read keys from Windows DPAPI credential store and environment variables. | Rotate compromised keys in upstream provider dashboards. |
| **Branding & Identity** | Legacy "OpenClaw" references across `Resources.resw` and titlebars. | **Remediated** | Purged >100 OpenClaw strings in `Resources.resw`; updated Window title to "SPARK". | Complete optional solution file rename (`spark.slnx`) when all references are safe. |
| **Design System & UX** | Inconsistent control styling, missing global command palette, missing cohesive token system. | **In Progress** | Creating shared WinUI 3 Design System (ColorTokens, Typography, Spacing, CommandPalette Ctrl+K). | Deploy unified ResourceDictionary into `App.xaml`. |

---

## 2. Risk & Impact Matrix

1. **High Impact, Low Risk**: Unifying the WinUI 3 ResourceDictionary with standardized semantic color tokens, typography scales, and spacing scales. Improves UI consistency across all 5 divisions with zero architectural disruption.
2. **High Impact, Medium Risk**: Wiring `SparkAutonomousScheduler` default execution loop to `ISparkAgentOrchestrator`. Converts scheduled tasks from static reports to real multi-turn agent turns.
3. **High Impact, High Risk**: Rewriting solution/project files from `OpenClaw.*` to `Spark.*`. Deferred to Phase 7 to prevent breaking MSBuild target references or active build pipelines.
