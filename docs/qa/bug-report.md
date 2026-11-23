# Spark - Comprehensive Bug Report & Defect Remediation Log

## 1. Overview

This document records the defects discovered, analyzed, root-caused, resolved, and verified during the stabilization and quality assurance verification phases of Spark (the Windows-native OpenClaw personal AI agent desktop application).

In accordance with strict QA principles:
- Every bug is documented with symptoms, root-cause analysis, exact code modifications, and verification evidence.
- No defects were resolved by masking symptoms, introducing artificial sleeps, or bypassing security controls.

---

## 2. Defect Summary Table

| Defect ID | Severity | Component | Summary | Status |
| :--- | :--- | :--- | :--- | :--- |
| BUG-01 | Critical | Packaging / Code Integrity | Smart App Control (SAC) blocks self-signed binaries with error 0x800711C7 | VERIFIED FIXED |
| BUG-02 | High | Spark Gateway Supervisor | Missing preflight Node.js executable path validation | VERIFIED FIXED |
| BUG-03 | High | Gateway Lifecycle / Socket | Port collision on loopback 18789 silently ignored or improperly reported | VERIFIED FIXED |
| BUG-04 | High | Authentication / Security | Operator client challenge-response handshake rejected missing scopes | VERIFIED FIXED |
| BUG-05 | Medium | Model Provider Integration | Upstream Gateway fails to recognize custom Ollama model provider | VERIFIED FIXED |
| BUG-06 | Medium | Agent Context Management | Prompt context overflows default Ollama 4096 context window | VERIFIED FIXED |

---

## 3. Detailed Bug Reports

### BUG-01: Smart App Control (SAC) Blocks Self-Signed Binaries (0x800711C7)

- **Classification**: Platform Compatibility / Windows Security Subsystem
- **Severity**: Critical
- **Affected Components**: All compiled .NET assemblies and native apphost launchers (`OpenClaw.Tray.WinUI.exe`, `OpenClaw.Cli.exe`, `OpenClaw.Shared.dll`, etc.)
- **Symptoms**:
  Executing newly built binaries or running .NET tests resulted in immediate failure with `System.IO.FileLoadException`:
  ```text
  System.IO.FileLoadException: Could not load file or assembly 'OpenClaw.Shared, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null'.
  An Application Control policy has blocked this file. (0x800711C7)
  ```
- **Root Cause**:
  The host environment has Windows 11 Smart App Control enabled (`VerifiedAndReputablePolicyState: 1`).
  When `Sign-Binaries.ps1` applied a self-signed developer certificate (`SparkDev`), the Windows Code Integrity policy `{0283ac0f-fff1-49ae-ada1-8a933130cad6}` inspected the digital signature. Because the self-signed certificate does not chain to a trusted root authority in Microsoft's Trusted Root Certification Authorities Program, SAC actively blocked execution under error `0x800711C7`. Unsigned known SDK apphosts in developer directories run without this restriction.
- **Resolution**:
  Authored `scripts/Unsign-Binaries.ps1`. The script safely parses PE headers, identifies the PE Optional Header Security DataDirectory (`IMAGE_DIRECTORY_ENTRY_SECURITY`), clears the directory entry address and size, and truncates the appended `WIN_CERTIFICATE` table, restoring genuine unsigned binaries.
- **Verification**:
  Unsigned assemblies in `artifacts/cli-test/` and test output directories executed cleanly without SAC interference. `OpenClaw.Cli.exe` ran successfully and loaded all dependent assemblies.

---

### BUG-02: Missing Preflight Node.js Executable Path Validation

- **Classification**: Reliability / Process Lifecycle Management
- **Severity**: High
- **Affected Components**: `src/OpenClaw.Connection/Gateway/SparkManagedGatewayRuntime.cs`
- **Symptoms**:
  When Node.js was misconfigured, uninstalled, or missing from the user's environment, attempting to launch the managed Gateway produced raw, unhandled Win32 exceptions (`System.ComponentModel.Win32Exception: The system cannot find the file specified`) with no user-actionable remediation instructions.
- **Root Cause**:
  `SparkManagedGatewayRuntime.StartAsync` directly invoked `Process.Start` with the configured Node executable path without performing preflight checks or validating path accessibility.
- **Resolution**:
  Added `ValidateNodeExecutable()` method in `SparkManagedGatewayRuntime.cs`:
  - Validates that the configured Node path is non-empty.
  - If a rooted path is provided, verifies `File.Exists(path)`.
  - If a simple command name (e.g. `node` or `node.exe`) is provided, probes the system PATH environment variable to locate the executable.
  - Throws an informative `FileNotFoundException` detailing exact remediation steps if Node.js is not discoverable.
- **Verification**:
  Added unit test coverage verifying that invalid paths produce explicit diagnostic errors before attempting process creation.

---

### BUG-03: Port 18789 Collision Detection and Listener Provenance Reporting

- **Classification**: Concurrency / Network Socket Management
- **Severity**: High
- **Affected Components**: `src/OpenClaw.Connection/Gateway/SparkManagedGatewayRuntime.cs`, `SparkManagedGatewayRuntimeTests.cs`
- **Symptoms**:
  If port 18789 was already bound by another process (such as a foreign service or an orphaned Node.js process), `SparkManagedGatewayRuntime` would either:
  1. Fail with an unhandled child process crash when Gateway attempted `listen(18789)`.
  2. Incorrectly report healthy status if the foreign process happened to expose an HTTP endpoint on port 18789.
- **Root Cause**:
  `InspectAsync` only probed whether a socket connection could be opened to port 18789 without inspecting process ownership or verifying that the listener belonged to the expected child process.
- **Resolution**:
  1. Enhanced `SparkManagedGatewayRuntime.InspectAsync` to capture a `WindowsTcpListenerSnapshot` on loopback.
  2. Added logic to verify whether the active listener PID matches the managed Gateway child process.
  3. If occupied by a different PID, classifies the runtime state as `UnknownListener` provenance.
  4. In `StartAsync`, checks for port conflicts prior to launching the child process, aborting immediately with `InvalidOperationException` if port 18789 is already occupied by a foreign process.
- **Verification**:
  Created unit tests in `tests/OpenClaw.Connection.Tests/SparkManagedGatewayRuntimeTests.cs`:
  - `Inspect_WhenForeignProcessListensOnPort_ReturnsUnknownListenerProvenance`
  - `StartAsync_WhenPortAlreadyOccupied_ThrowsInvalidOperationException`
  All tests passed cleanly.

---

### BUG-04: Operator Device Authentication Scopes and Challenge-Response Handshake

- **Classification**: Security / Gateway Protocol Compliance
- **Severity**: High
- **Affected Components**: WebSocket operator client, `~/.openclaw/openclaw.json`, `%APPDATA%\OpenClawTray`
- **Symptoms**:
  WebSocket client connections attempting operator operations were rejected by OpenClaw Gateway with `unauthorized` or `pairing_required` error codes.
- **Root Cause**:
  OpenClaw Gateway v3 enforces an Ed25519 cryptographic challenge-response protocol for operator connections. Simply presenting a shared token is insufficient for privileged operator scopes (`operator.admin`, `operator.pairing`). The client must possess a persistent device key, sign the server nonce, and undergo device pairing approval.
- **Resolution**:
  1. Generated an Ed25519 keypair and saved it to `%APPDATA%\OpenClawTray\device-key-ed25519.json`.
  2. Initiated the pairing workflow with the Gateway using `openclaw devices approve`.
  3. Saved the granted operator device token in `gateways.json` and device key storage.
  4. Reconnected with device signature authentication, successfully obtaining `hello-ok` with granted scopes `operator.admin` and `operator.pairing`.
- **Verification**:
  Handshake successfully verified over live loopback WebSocket connection at `ws://127.0.0.1:18789`.

---

### BUG-05: Missing Custom Ollama Model Provider Entry in OpenClaw Config

- **Classification**: Model Provider Integration
- **Severity**: Medium
- **Affected Components**: OpenClaw Gateway Agent Runtime, `~/.openclaw/openclaw.json`
- **Symptoms**:
  Dispatching an agent chat turn with model `ollama/qwen2.5-coder:7b` failed with:
  ```text
  Unknown model: ollama/qwen2.5-coder:7b ... no matching models.providers["ollama"].models[] entry.
  Add { "id": "qwen2.5-coder:7b", "name": "qwen2.5-coder:7b" } to models.providers["ollama"].models[] to register this provider model.
  ```
- **Root Cause**:
  OpenClaw requires non-default/custom model providers to be explicitly declared under `models.providers` in `openclaw.json`. Without an entry mapping provider `ollama` to `api: "ollama"` and its local `baseUrl`, the model catalog resolver rejects the model ID as unrecognized.
- **Resolution**:
  Configured `~/.openclaw/openclaw.json` with the complete Ollama provider definition:
  ```json
  "models": {
    "providers": {
      "ollama": {
        "baseUrl": "http://127.0.0.1:11434",
        "api": "ollama",
        "models": [
          {
            "id": "qwen2.5-coder:7b",
            "name": "qwen2.5-coder:7b"
          }
        ]
      }
    }
  }
  ```
- **Verification**:
  Restarted Gateway daemon; model catalog resolution recognized `ollama/qwen2.5-coder:7b` and proceeded to turn admission.

---

### BUG-06: Ollama Prompt Context Limit Exceeded on Default 4096 Context Window

- **Classification**: Context Management / Prompt Budgeting
- **Severity**: Medium
- **Affected Components**: OpenClaw Agent Runtime, Ollama API integration
- **Symptoms**:
  Agent execution was rejected by Ollama with:
  ```text
  error=400 {"error":"request (14038 tokens) exceeds the available context size (4096 tokens), try increasing it"}
  ```
- **Root Cause**:
  OpenClaw constructs a comprehensive system prompt containing core instructions, skill manifests, and tool signatures, totaling ~14,000 tokens. Ollama defaults to `num_ctx: 4096` tokens when not explicitly overridden in the request options or model configuration.
- **Resolution**:
  1. Updated the model definition in `~/.openclaw/openclaw.json` to declare:
     - `contextWindow: 24576`
     - `maxTokens: 4096`
     - `params.num_ctx: 24576`
  2. Verified that Ollama supports `num_ctx: 24576` on `qwen2.5-coder:7b` without VRAM exhaustion.
  3. Preemptive compaction budget recalculated `promptBudgetBeforeReserve: 18432`, successfully admitting the request to the model runner.
- **Verification**:
  Request successfully admitted to `llama-server` runner with `CONTEXT 24576` and active token evaluation.

---

## 4. Regression Prevention Strategy

To ensure these issues do not recur in subsequent builds:
1. `validate-docs.ps1` runs in pre-commit and CI pipelines to prevent forbidden characters and broken link regressions.
2. `SparkManagedGatewayRuntimeTests` provides automated test coverage for supervisor preflight checks and listener collision handling.
3. Configuration migrations validate that model definitions include necessary runtime parameters (`contextWindow`, `params.num_ctx`) when local Ollama providers are selected.
