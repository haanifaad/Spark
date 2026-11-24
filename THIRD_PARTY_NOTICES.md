# Third-Party Software Notices and Licenses

This project incorporates third-party open-source components and architecture patterns. Below are the notices, attributions, and license texts applicable to these components:

---

## 1. Mark-LVII (Human Head 3D Avatar & Voice Architecture)

- **Original Project:** Mark-LVII / MARK Personal AI Assistant
- **Copyright:** (c) 2024-2026 Mark-LVII Authors & Contributors
- **License:** Creative Commons Attribution-NonCommercial 4.0 International (CC BY-NC 4.0)
- **Local License File:** [LICENSE_MARK_LVII](file:///C:/Intel/Spark/Spark-main/src/OpenClaw.Shared/VoiceMode/runtime/LICENSE_MARK_LVII)

### Attribution Notice:
The 3D facial geometry (`core/face_model.obj`), avatar mesh calculation (`core/avatar_mesh.py`), viseme mapping (`core/viseme.py`), avatar animator (`core/avatar.py`), and modular voice agent patterns used in SPARK's Voice Mode are derived from the Mark-LVII project under the terms of the Creative Commons Attribution-NonCommercial 4.0 International (CC BY-NC 4.0) License. For commercial use inquiries regarding Mark-LVII components, please consult the original authors.

### Commercial Deployment Isolation (Safe Harbor):
SPARK provides a fully modular runtime architecture. The core SPARK desktop environment, orchestration engine, speech synthesis/recognition pipelines, and tools are 100% open-source under the MIT License. The Mark-LVII 3D Face Avatar is maintained as an isolated visual plugin (`VisualMode: "3DFace"`). For commercial deployments, SPARK defaults to the native MIT-licensed "Arc Reactor" or "Minimal" visual modes, ensuring zero commercial IP encumbrance.

---

## 2. Rea (Reverse Engineering & Clean-Room Architecture Specification)

- **Original Project:** Rea
- **Copyright:** Copyright (c) 2025-2026 Rea Contributors
- **License:** MIT License
- **Adapted Capabilities:** Binary inspection, .NET assembly reflection, Electron ASAR inspection, Android APK analysis, evidence collection with confidence scoring, and clean-room architecture specification synthesis. Re-implemented natively in C# inside `OpenClaw.Shared.Coding.ReverseEngineering`.

---

## 3. Craft Anything (Autonomous Project Scaffolding & Output Detection)

- **Original Project:** Craft Anything
- **Copyright:** Copyright (c) 2025-2026 Craft Anything Contributors
- **License:** MIT License / Apache License 2.0 (Dual Licensed)
- **Adapted Capabilities:** Natural-language to full working project generation, clean-room scaffolding, output discovery engine (`site_dir` index detection and executable detection), and live preview resolution. Re-implemented natively in C# inside `OpenClaw.Shared.Coding.BuildAnything`.

---

## 4. CLI Anything (Agent-Native CLI Harness Registry & Safety Tiering)

- **Original Project:** CLI Anything
- **Copyright:** Copyright (c) 2025-2026 CLI Anything Contributors
- **License:** Apache License 2.0
- **Adapted Capabilities:** Software CLI harness hub (100+ creative and developer tools), natural language to CLI command translation, 3-tier safety execution guardrails (Tier 1 Read-Only, Tier 2 Safe Modification, Tier 3 Destructive Operation with explicit confirmation), and auditable execution logging. Re-implemented natively in C# inside `OpenClaw.Shared.Coding.CliIntelligence`.

---

## 5. Shannon (Autonomous Cybersecurity Audit & Vulnerability Assessment Pipeline)

- **Original Project:** Shannon
- **Copyright:** Copyright (c) 2025-2026 Keygraph, Inc. and Shannon Contributors
- **License:** GNU Affero General Public License v3.0 (AGPL-3.0)
- **Adapted Capabilities:** 5-phase security auditing pipeline (`pre-recon`, `recon`, `vuln-identification`, `exploit-verification`, `reporting`), specialized agent orchestration, "no exploit, no report" proof-of-concept verification doctrine, CWE taxonomy mapping, and Markdown/SARIF 2.1.0 report generation. Re-implemented natively in C# inside `OpenClaw.Shared.Coding.CyberSecurity` and invoked via `/cyber-security-audit`.
