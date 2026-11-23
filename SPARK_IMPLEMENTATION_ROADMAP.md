# SPARK — Implementation Roadmap
**Document Version:** 1.0.0  
**Target:** Phased Engineering Execution Plan for Production SPARK System  

---

## Roadmap Phases & Execution Milestones

### Phase 0: Preservation, Security Remediation & Directory Verification (Completed)
- [x] Authoritative directory enforcement strictly targeting `C:\Users\PC\Documents\Spark\Spark-main`.
- [x] Full Git status checkpoint and preservation of working tree.
- [x] Security audit remediation: purged plaintext API keys from `api_keys.json`.
- [x] Restored `pyautogui.FAILSAFE = True` in `actions/computer_control.py`.

### Phase 1: Reference Library Recovery & Multi-Agent Catalogue (Completed)
- [x] Cloned and verified ByteDance UI-TARS (`AI-Computer-Use\UI-TARS`).
- [x] Cloned and verified Keygraph Shannon (`AI-Security\Shannon`).
- [x] Cloned and verified all 17 Storytold ArtCraft applications (`AI-Creative\ArtCraft-Applications`).
- [x] Cloned reference agents: `rakazo`, `OpenMausBot`, `openfuse`, `nanoMuse`, `open-duet`, `open-dots`, `grok-bot-reconstructed`, `agent-zero`, `hermes-agent`, `ironclaw`, `nanoclaw`, `OpenHands`.
- [x] Populated reference inventory: `repositories.json`, `repositories.md`, `source-status.md`, `download-errors.md`, `reference-comparison.md`, `licenses-and-attribution.md`, `reference-integration-plan.md`.

### Phase 2: Core Execution & AI Routing Reliability (Completed)
- [x] Removed synthetic 15 ms delay from `StudyTutorService.cs`; wired to live Socratic LLM (`llama3.2:3b`).
- [x] Reconnected `CodingPage.xaml.cs` to active LLM code generator (`qwen2.5-coder:7b`).
- [x] Reconnected Creative Suite dynamic resolution to `SPARK-Reference-Library\AI-Creative\ArtCraft-Applications`.
- [x] Verified 100% test pass rate (215 of 215 tests passed).

### Phase 3: Autonomous Agent Runtime & Scheduler Hardening (Active Implementation)
- [x] Persistent JSON task queue with atomic replacement (`.tmp`, `.bak`).
- [x] Heartbeat engine with stalled task recovery and consecutive failure tracking.
- [ ] Connect `SparkAutonomousScheduler` default execution loop to `ISparkAgentOrchestrator` for autonomous multi-step reasoning turns.
- [ ] Implement active goal tracking and background notification delivery in WinUI 3.

### Phase 4: Shared WinUI 3 Design System & Advanced UX (Active Implementation)
- [ ] Create `SparkDesignSystem.xaml` ResourceDictionary with semantic color tokens, typography scales, spacing tokens, and control styles.
- [ ] Standardize light and dark themes with Mica backdrop in `App.xaml`.
- [ ] Implement Global Command Palette (`Ctrl + K`) wired to division navigation, model switching, and quick task launching.
- [ ] Add empty states, loading progress rings, and error info bars across all 5 divisions.

### Phase 5: Voice & Computer-Use Hardening (Completed & Polish)
- [x] Scoped Mark-LVII Blue HUD (`#00060A`, `#00D4FF`) strictly to `VoiceModePage.xaml`.
- [x] Selector for Arc Reactor vs 3D Face Avatar.
- [x] UI-TARS 1000-normalized coordinate grounding in `computer_control.py` and `IVoiceModeDivisionService`.
- [ ] Add real-time visual coordinate indicator overlay during computer-control actions.

### Phase 6: Final Verification, Build & Evidence Audit
- [ ] Build complete solution (`OpenClaw.Tray.WinUI.csproj`, `OpenClaw.Shared.csproj`).
- [ ] Execute complete test suite and verify 0 failures.
- [ ] Compile final `SPARK_IMPLEMENTATION_REPORT.md` with complete evidence ledger.
