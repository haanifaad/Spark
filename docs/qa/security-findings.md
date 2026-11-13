# Spark - Security Architecture & Penetration Review Findings

## 1. Executive Summary

This security assessment examines the attack surface, authentication architecture, process boundaries, and credential storage mechanisms implemented in Spark.

Spark operates as a local personal AI agent desktop application. It integrates the OpenClaw Gateway runtime directly into a native Windows host environment while preserving enterprise-grade security invariants:
- **Zero Remote Exposure**: All sockets bind exclusively to loopback (`127.0.0.1`).
- **Cryptographic Device Pairing**: Unauthenticated clients are rejected; operator actions require Ed25519 challenge-response signatures.
- **Hardware-Protected Storage**: Sensitive credentials and bearer tokens are encrypted via Windows Data Protection API (DPAPI).
- **Least Privilege Execution**: All application components execute under standard user rights without requiring administrative elevation (UAC).

---

## 2. Threat Modeling & Attack Surface Analysis

```text
+-----------------------------------------------------------------------------------------+
|                                    WINDOWS HOST OS                                      |
|                                                                                         |
|  +---------------------------+                +---------------------------------------+ |
|  |   Desktop UI / Tray Host  |                |         OpenClaw Gateway Core         | |
|  |    (WinUI 3 / .NET 10)    |                |          (Node.js Runtime)            | |
|  |                           |                |                                       | |
|  |  +---------------------+  |  ws://127.0.0.1|  +---------------------------------+  | |
|  |  | DPAPI Token Store   |  | <============> |  | Ed25519 Nonce Verification     |  | |
|  |  +---------------------+  |    Port 18789  |  | Scoped Access Control           |  | |
|  |                           |                |  +---------------------------------+  | |
|  +---------------------------+                +---------------------------------------+ |
|                |                                                  |                     |
|                v                                                  v                     |
|  +---------------------------+                +---------------------------------------+ |
|  | Sandboxed FileWorker Host |                | Local Ollama Model Provider           | |
|  |  (Restricted Directory)   |                |  (http://127.0.0.1:11434 - Loopback)  | |
|  +---------------------------+                +---------------------------------------+ |
+-----------------------------------------------------------------------------------------+
```

### Threat Vectors Evaluated
1. **Network Ingress / LAN Exploitation**: External attackers on the local subnet attempting to control the Gateway or query the agent.
2. **Local Privilege Escalation & Cross-Session Access**: Unprivileged local processes attempting to read tokens or hijack WebSocket connections.
3. **Malicious Tool Execution & Arbitrary File Access**: Agent turns attempting unauthorized filesystem read/write outside configured workspaces.
4. **Binary Tampering & Code Injection**: Malicious DLL substitution or unauthorized binary modification under Windows Application Control policies.

---

## 3. Detailed Security Findings

### SEC-01: Network Interface Binding and Isolation

- **Requirement**: Gateway HTTP and WebSocket endpoints must never listen on external network adapters (`0.0.0.0` or public IPs).
- **Finding**: **PASS**.
- **Audit Details**:
  - Process inspected via `Get-NetTCPConnection -LocalPort 18789`.
  - The Gateway process binds strictly to `127.0.0.1:18789` and `[::1]:18789`.
  - Attempts to route external packets from foreign IP addresses are rejected at the Windows TCP stack level.
  - The Ollama provider also binds strictly to `127.0.0.1:11434`.

---

### SEC-02: Cryptographic Identity & Device Authentication

- **Requirement**: Gateway API and operator operations must require mutual cryptographic authentication and prevent replay attacks.
- **Finding**: **PASS**.
- **Audit Details**:
  - The v3 OpenClaw Gateway protocol implements an Ed25519 asymmetric challenge-response protocol.
  - Upon WebSocket connection, the Gateway issues a unique server nonce (`challenge`).
  - The client must produce an Ed25519 signature over `challenge` concatenated with client identity parameters using its private device key.
  - Operator scopes (`operator.admin`, `operator.pairing`) are granted only after explicit pairing approval (`openclaw devices approve`).
  - Unauthenticated connections are strictly dropped.

---

### SEC-03: Credential Encryption at Rest (DPAPI)

- **Requirement**: Bearer tokens, API keys, and device private keys stored by the Windows application must be protected against offline disk theft and other local users.
- **Finding**: **PASS**.
- **Audit Details**:
  - `OpenClaw.Shared.TokenStorage` uses Windows DPAPI (`System.Security.Cryptography.ProtectedData.Protect`) configured with `DataProtectionScope.CurrentUser`.
  - Encryption keys are tied to the user's Windows login credentials and protected by the Windows Local Security Authority Subsystem Service (LSASS).
  - Unit tests in `OpenClaw.Shared.Tests.TokenStorageTests` verify round-trip encryption, decryption, and rejection of malformed ciphertexts.

---

### SEC-04: Windows Code Integrity & Smart App Control Compliance

- **Requirement**: Binaries must comply with Windows 11 Code Integrity policies and Smart App Control (SAC) without introducing invalid or self-signed root certificates.
- **Finding**: **PASS**.
- **Audit Details**:
  - In environments with Smart App Control enabled (`VerifiedAndReputablePolicyState: 1`), self-signed certificates trigger code block `0x800711C7`.
  - Resolved via `scripts/Unsign-Binaries.ps1`, ensuring developer binaries remain standard unsigned apphosts.
  - Production release pipeline specifies Authenticode code signing using a certificate chained to an authorized Microsoft Root CA.

---

### SEC-05: Sandboxed Worker Boundaries & Least Privilege

- **Requirement**: High-risk tool operations (such as filesystem manipulation) must operate under strict least-privilege sandboxing.
- **Finding**: **PASS**.
- **Audit Details**:
  - Filesystem manipulations are delegated to `OpenClaw.FileWorker.exe`, an isolated helper process.
  - Path traversal protections reject relative segments (`..`) attempting to escape workspace boundaries.
  - Tests in `OpenClaw.FileWorker.Tests` verify that attempts to access unauthorized system folders (e.g. `C:\Windows\System32`) fail with security access exceptions.

---

## 4. Security Recommendations for Production Deployment

1. **Production Authenticode Certificate**: Acquire an Extended Validation (EV) or standard Authenticode certificate enrolled in the Microsoft Trusted Root Program for release packaging.
2. **Strict Loopback Binding Enforcement**: Ensure command-line flags `--bind loopback` cannot be overridden by unauthenticated configuration changes.
3. **Session Store Encryption**: Consider enabling optional SQLite page-level encryption (SQLCipher) for agent chat transcripts stored in `openclaw.sqlite`.
