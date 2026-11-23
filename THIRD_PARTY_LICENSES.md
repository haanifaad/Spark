# Third-Party Licenses and Attribution Notices

SPARK integrates and adapts certain open-source GUI automation concepts and specifications. This document details the third-party components, their respective licenses, and attributions.

---

## 1. ByteDance UI-TARS Desktop Components

- **Upstream Project**: UI-TARS Desktop (`bytedance/UI-TARS-desktop`)
- **Upstream Repository**: https://github.com/bytedance/UI-TARS-desktop
- **Copyright**: Copyright (c) 2025 Bytedance, Inc. and its affiliates.
- **License**: Apache License, Version 2.0 (SPDX-License-Identifier: Apache-2.0)

### Adapted Concepts and Components:
1. **Action Language & Syntax**:
   - Action grammar format: `click(start_box='[x1, y1, x2, y2]')`, `left_double(...)`, `right_single(...)`, `drag(...)`, `type(content='...')`, `hotkey(key='...')`, `scroll(start_box='...', direction='...')`, `wait()`, `finished()`, `call_user()`, `press(...)`, `release(...)`.
   - Adapted from `@ui-tars/action-parser` and `@ui-tars/operator-nut-js`.
2. **Coordinate Normalization and De-quantization**:
   - 1000x1000 normalized grid coordinate de-quantization and scaling to logical and physical display dimensions.
   - Adapted from `@ui-tars/action-parser/src/actionParser.ts`.
3. **GUI Agent System Prompts & Instruction Templates**:
   - VLM prompt structuring for GUI perception and structured `Thought:` / `Action:` reasoning.
   - Adapted from `@ui-tars/sdk/src/constants.ts`.

### Modifications and Native SPARK Implementation:
- Implemented natively in C# (.NET 10) inside `OpenClaw.Shared.ComputerUse` for high performance, zero external Node.js overhead, and direct Windows API integration.
- Desktop actuation implemented directly via native Win32 `SendInput`, `SetCursorPos`, `mouse_event`, and Unicode input rather than Node.js `nut.js`.
- Dual perception engine added: combines visual coordinate grounding with native Windows UI Automation (UIA) accessibility tree inspection.
- Full integration into SPARK's `ISparkModelRouter`, `ISparkToolRegistry`, `ISentinelReviewer`, and WinUI 3 interface.

---

### Apache License 2.0 Text

```text
                              Apache License
                        Version 2.0, January 2004
                     http://www.apache.org/licenses/

TERMS AND CONDITIONS FOR USE, REPRODUCTION, AND DISTRIBUTION

1. Definitions.

   "License" shall mean the terms and conditions for use, reproduction,
   and distribution as defined by Sections 1 through 9 of this document.

   "Licensor" shall mean the copyright owner or entity authorized by
   the copyright owner that is granting the License.

   "Legal Entity" shall mean the union of the acting entity and all
   other entities that control, are controlled by, or are under common
   control with that entity. For the purposes of this definition,
   "control" means (i) the power, direct or indirect, to cause the
   direction or management of such entity, whether by contract or
   otherwise, or (ii) ownership of fifty percent (50%) or more of the
   outstanding shares, or (iii) beneficial ownership of such entity.

   "You" (or "Your") shall mean an individual or Legal Entity
   exercising permissions granted by this License.

   "Source" form shall mean the preferred form for making modifications,
   including but not limited to software source code, documentation
   source, and configuration files.

   "Object" form shall mean any form resulting from mechanical
   transformation or translation of a Source form, including but
   not limited to compiled object code, generated documentation,
   and conversions to other media types.

   "Work" shall mean the work of authorship, whether in Source or
   Object form, made available under the License, as indicated by a
   copyright notice that is included in or attached to the work.

   "Derivative Works" shall mean any work, whether in Source or Object
   form, that is based on (or derived from) the Work and for which the
   editorial revisions, annotations, elaborations, or other modifications
   represent, as a whole, an original work of authorship. For the purposes
   of this License, Derivative Works shall not include works that remain
   separable from, or merely link (or bind by name) to the interfaces of,
   the Work and Derivative Works thereof.

   "Contribution" shall mean any work of authorship, including
   the original version of the Work and any modifications or additions
   to that Work or Derivative Works thereof, that is intentionally
   submitted to Licensor for inclusion in the Work by the copyright owner
   or by an individual or Legal Entity authorized to submit on behalf of
   the copyright owner.

   "Contributor" shall mean Licensor and any individual or Legal Entity
   on behalf of whom a Contribution has been received by Licensor and
   subsequently incorporated within the Work.

2. Grant of Copyright License. Subject to the terms and conditions of
   this License, each Contributor hereby grants to You a perpetual,
   worldwide, non-exclusive, no-charge, royalty-free, irrevocable
   copyright license to reproduce, prepare Derivative Works of,
   publicly display, publicly perform, sublicense, and distribute the
   Work and such Derivative Works in Source or Object form.

3. Grant of Patent License. Subject to the terms and conditions of
   this License, each Contributor hereby grants to You a perpetual,
   worldwide, non-exclusive, no-charge, royalty-free, irrevocable
   (except as stated in this section) patent license to make, have made,
   use, offer to sell, sell, import, and otherwise transfer the Work.

4. Redistribution. You may reproduce and distribute copies of the
   Work or Derivative Works thereof in any medium, with or without
   modifications, and in Source or Object form, provided that You
   meet the following conditions:

   (a) You must give any other recipients of the Work or
       Derivative Works a copy of this License; and

   (b) You must cause any modified files to carry prominent notices
       stating that You changed the files; and

   (c) You must retain, in the Source form of any Derivative Works
       that You distribute, all copyright, patent, trademark, and
       attribution notices from the Source form of the Work; and

   (d) If the Work includes a "NOTICE" text file as part of its
       distribution, then any Derivative Works that You distribute must
       include a readable copy of the attribution notices contained
       within such NOTICE file.

5. Submission of Contributions. Unless You explicitly state otherwise,
   any Contribution intentionally submitted for inclusion in the Work
   by You to the Licensor shall be under the terms and conditions of
   this License, without any additional terms or conditions.

6. Trademarks. This License does not grant permission to use the trade
   names, trademarks, service marks, or product names of the Licensor,
   except as required for reasonable and customary use in describing the
   origin of the Work and reproducing the content of the NOTICE file.

7. Disclaimer of Warranty. Unless required by applicable law or
   agreed to in writing, Licensor provides the Work (and each
   Contributor provides its Contributions) on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
   implied, including, without limitation, any warranties or conditions
   of TITLE, NON-INFRINGEMENT, MERCHANTABILITY, or FITNESS FOR A
   PARTICULAR PURPOSE.

8. Limitation of Liability. In no event and under no legal theory,
   whether in tort (including negligence), contract, or otherwise,
   unless required by applicable law or agreed to in writing, shall
   any Contributor be liable to You for damages, including any direct,
   indirect, special, incidental, or consequential damages of any character
   arising as a result of this License or out of the use or inability to
   use the Work.

9. Accepting Warranty or Additional Liability. While redistributing
   the Work or Derivative Works thereof, You may choose to offer,
   and charge a fee for, acceptance of support, warranty, indemnity,
   or other liability obligations and/or rights consistent with this
   License.
```

---

## 2. Mark-LVII 3D Facial Avatar & Voice Architecture

- **Upstream Project**: Mark-LVII / MARK Personal AI Assistant
- **Copyright**: Copyright (c) 2024-2026 Mark-LVII Authors & Contributors
- **License**: Creative Commons Attribution-NonCommercial 4.0 International (CC BY-NC 4.0)
- **Local License File**: [src/OpenClaw.Shared/VoiceMode/runtime/LICENSE_MARK_LVII](file:///C:/Intel/Spark/Spark-main/src/OpenClaw.Shared/VoiceMode/runtime/LICENSE_MARK_LVII)

### Adapted Components:
1. **3D Facial Avatar Geometry and Mesh**:
   - `core/avatar_mesh.py`: 3D vertex computation, facial mesh deformation, morph targets.
   - `core/viseme.py`: Speech acoustic viseme phoneme-to-morph-target mapping.
   - `core/avatar.py`: Real-time avatar animation loop, blinking, natural head movements, gaze tracking.
2. **Modular Voice Agent Engine**:
   - `agent/`: Planner, task execution queue, and reasoning loop.
   - `memory/`: Long-term persistent memory management.
   - `actions/`: Modular action executors (computer control, settings, screen analysis, file processing).

### Attribution Notice:
The 3D facial avatar geometry, mesh calculations, viseme generators, and voice modules are derived from the Mark-LVII project under the terms of the Creative Commons Attribution-NonCommercial 4.0 International (CC BY-NC 4.0) License. Commercial use of these components requires separate licensing from the original authors.


---

## 3. Rea (Reverse Engineering & Clean-Room Architecture Specification)

- **Upstream Project**: Rea
- **Copyright**: Copyright (c) 2025-2026 Rea Contributors
- **License**: MIT License (SPDX-License-Identifier: MIT)

### Adapted Concepts and Components:
1. **Target Inspection Engine**:
   - Categorization of binaries, .NET assemblies, Web runtimes, Electron ASAR packages, and Android APKs.
   - Adapted from `src/application/binary/`, `src/electron/`, `src/dotnet/`, and `src/android/`.
2. **Evidence Collection Matrix & Confidence Scoring**:
   - Structured evidence records tracking decompilation, API surface, state machines, xrefs, confidence, and analysis limitations.
   - Adapted from `src/application/WorkflowEvidence.ts`.
3. **Clean-Room Specification Synthesis**:
   - Transformation of reverse engineering findings into copyright-clean functional specifications and implementation plans.
   - Adapted from `src/application/specification/`.

### Modifications and Native SPARK Implementation:
- Implemented natively in C# (.NET 10) inside `OpenClaw.Shared.Coding.ReverseEngineering` (`ReaEngine.cs`, `ReaModels.cs`).
- Integrated into SPARK's `CodingDivisionWorkspaceContext` and `CodingPage.xaml` Reverse Engineering tab.
- Added slash command integration: `/reverse-engineer <targetPath>`.

### MIT License Text:
```text
MIT License

Copyright (c) 2025-2026 Rea Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## 4. Craft Anything (Clean-Room Project Generation & Output Detection)

- **Upstream Project**: Craft Anything
- **Copyright**: Copyright (c) 2025-2026 Craft Anything Contributors
- **License**: MIT License / Apache License 2.0 (SPDX-License-Identifier: MIT OR Apache-2.0)

### Adapted Concepts and Components:
1. **Clean-Room Project Scaffolding**:
   - Generation of real runnable software artifacts across multiple archetypes (Web SPAs, Full-Stack apps, CLI tools, documentation sites).
   - Adapted from `crates/engine/src/clean_room.rs` and `crates/engine/src/toolchains.rs`.
2. **Output Discovery Engine**:
   - Automatic heuristic detection of build outputs, identifying `site_dir` folders containing index.html (`dist`, `build`, `out`, root) and compiled native executables.
   - Adapted from `crates/engine/src/outputs.rs`.
3. **Live Preview Resolution**:
   - Seamless conversion of build output folders into local preview endpoints.

### Modifications and Native SPARK Implementation:
- Implemented natively in C# (.NET 10) inside `OpenClaw.Shared.Coding.BuildAnything` (`CraftEngine.cs`, `CraftModels.cs`).
- Integrated into SPARK WinUI 3 interface with WebView2 live preview and project file tree inspection.
- Added slash command integration: `/build <specification>`.

---

## 5. CLI Anything (Agent-Native CLI Harness Registry & Safety Tiering)

- **Upstream Project**: CLI Anything
- **Copyright**: Copyright (c) 2025-2026 CLI Anything Contributors
- **License**: Apache License, Version 2.0 (SPDX-License-Identifier: Apache-2.0)

### Adapted Concepts and Components:
1. **Software Harness Registry**:
   - Registry architecture cataloging 100+ creative and developer tools (Blender, GIMP, LibreOffice, Obsidian, OpenRefine, FreeCAD, Audacity, etc.).
   - Adapted from `registry.json` and `cli-hub/`.
2. **Natural-Language to CLI Command Translation**:
   - Translation of intent into harness invocations and CLI commands.
3. **3-Tier Command Safety Execution Guardrails**:
   - Pre-execution regex classification into Tier 1 (Read-Only), Tier 2 (Safe Modification), and Tier 3 (Destructive Operations requiring user confirmation).
   - Auditable command execution history with stdout, stderr, exit code, and execution duration.

### Modifications and Native SPARK Implementation:
- Implemented natively in C# (.NET 10) inside `OpenClaw.Shared.Coding.CliIntelligence` (`CliAnythingEngine.cs`, `CliModels.cs`).
- Integrated into SPARK's `CodingTerminalEngine`, `CodingPermissionManager`, and `CodingPage.xaml` CLI Intelligence console.
- Added slash command integration: `/cli <queryOrCommand>`.

---

## 6. Shannon (Autonomous Cybersecurity Audit & Vulnerability Assessment Pipeline)

- **Upstream Project**: Shannon
- **Copyright**: Copyright (c) 2025-2026 Keygraph, Inc. and Shannon Contributors
- **License**: GNU Affero General Public License v3.0 (SPDX-License-Identifier: AGPL-3.0)

### Adapted Concepts and Components:
1. **5-Phase Security Assessment Pipeline**:
   - Structured autonomous pipeline: `pre-recon` -> `recon` -> `vuln-identification` -> `exploit-verification` -> `report-authoring`.
   - Adapted from `apps/worker/src/temporal/workflows.ts`.
2. **Specialized Security Agent Pool**:
   - Modular agents evaluating distinct vulnerability classes: Injection, XSS, Authentication, Authorization, SSRF, and Miscellaneous.
   - Adapted from `apps/worker/src/types/agents.ts`.
3. **"No Exploit, No Report" Proof-of-Concept Verification Doctrine**:
   - Strict requirement that discovered vulnerabilities must be validated with reproducible exploit steps, payloads, responses, and concrete impact evidence before inclusion in the final report.
4. **Defensible Reporting & SARIF 2.1.0 Export**:
   - Standardized executive summaries, vulnerability breakdown tables, and SARIF 2.1.0 schema export for CI/CD and security dashboard integration.
   - Adapted from `apps/worker/src/ai/sast/sarif-profile.ts` and `sample-reports/`.

### Modifications and Native SPARK Implementation:
- Implemented natively in C# (.NET 10) inside `OpenClaw.Shared.Coding.CyberSecurity` (`ShannonAuditEngine.cs`, `ShannonAuditModels.cs`, `SecurityReportGenerator.cs`).
- Strict authorization validation restricting audits to explicitly authorized targets and local environments.
- First-class slash command integration: mandatory invocation via `/cyber-security-audit <target>`.
- Full UI integration inside SPARK WinUI 3 `CodingPage.xaml` Audit tab with real-time pipeline visualization, agent matrix, finding details, and SARIF/Markdown export.
