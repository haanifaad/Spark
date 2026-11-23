# Security Review - Project Spark

## 1. Network Surface & Gateway Exposure
- **Loopback Enforcement**: The managed local OpenClaw Gateway is explicitly launched with `--bind loopback` (binding exclusively to `127.0.0.1`). It never binds to `0.0.0.0` or exposes unauthenticated inbound listeners to the local area network or public internet by default.
- **Port Assignment**: Port 18789 is reserved for loopback IPC between the native Spark desktop application (WinUI 3) and the managed Gateway runtime.
- **TLS & Encryption**: External connections require HTTPS/WSS. Local loopback connections use HTTP/WS with Ed25519 cryptographic token authentication and challenge-response signing.

---

## 2. Authentication & Cryptographic Identity
- **Ed25519 Device Keys**: Every Windows installation generates an immutable Ed25519 keypair (`device-key-ed25519.json`) stored under protected user AppData (`%APPDATA%\OpenClawTray\gateways\<id>\`).
- **Handshake Signing**: All operator and node connection frames are cryptographically signed with V2/V3 signatures verifying device ownership and timestamp freshness.
- **Token Verification**: Tokens are validated during WebSocket upgrade; unauthorized requests fail immediately with strict protocol disconnection.

---

## 3. Tool Permissions & Execution Boundaries
- **MXC Sandbox Isolation**: Command execution requested by the agent (`system.run`) is routed through `NodeSandboxExecutor` with path isolation and directory whitelisting.
- **Hash-Based Approvals**: Sensitive commands require explicit user consent via `exec-approvals.json` before execution.
- **Browser Automation Security**: CDP browser sessions are isolated and cannot access user passwords, protected cookies, or sensitive host sessions without consent.
- **Credential Storage**: API keys and tokens are stored using Windows DPAPI (`System.Security.Cryptography.ProtectedData`) or Windows Credential Locker. Secrets are prefixed with `dpapi:` and are never exposed in plaintext log files.

---

## 4. Process Sandboxing & Containment
- **Win32 Job Object**: The Node.js Gateway process is assigned to a restricted Win32 Job Object with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` (`0x2000`).
- **Zero Orphan Guarantee**: In the event of a crash, unhandled exception, or unexpected termination of Spark, the Windows kernel automatically tears down the entire child process tree.

---

## 5. Smart App Control (SAC) Compliance & Windows 11 Compatibility
- **Host Security Posture**: Evaluated on Windows 11 Enterprise with Smart App Control actively enforced (`SmartAppControlState: On`).
- **Interpreted Script Execution**: The OpenClaw Gateway executes as an interpreted Node.js script tree (`node openclaw.mjs gateway run`), ensuring full compliance with application integrity policies without requiring elevated driver privileges.
- **Analyzer Isolation**: During development and compile phases, Roslyn source code generators (`Reactor.SourceMap.Generator.dll`) that lack WHQL digital signatures are suppressed using `-p:ReactorSourceMap=false -p:ReactorDiagnostics=false`, avoiding CS8034 policy violations while preserving 100% functional binary equivalence.

---

## 6. Endpoint Provenance & Rogue Process Hijack Defense
- **Listener Attribution (`NativeGatewayEndpointSecurity`)**: Before handing credentials to any local listener, Spark inspects the listening TCP socket PID and creation timestamp via `WindowsTcpListenerSnapshot`.
- **Expected Managed Gateway Verification**: Only a process owned by Spark's Job Object returning `GatewayEndpointProvenanceKind.ExpectedManagedGateway` is authorized to receive operator credentials. If a rogue process binds to port 18789, Spark detects the mismatch, refuses credential transmission, and raises a security alert (`GatewayErrorKind.LocalPortConflict`).

---

## 7. Credential Protection & Secret Redaction
- **Log Sanitization (`TokenSanitizer`, `UrlLogSanitizer`)**: All authorization tokens, passwords, bearer credentials, and signed query parameters are stripped before writing to `logs/` or diagnostics views.
- **Zero Secret Ingress**: Handshake frames pass through `RedactAuthPayload()` ensuring no cleartext keys leak into application diagnostics or event logs.

---

## 8. Node-Side Execution Policy and Capability Scoping
- **Scoped Capabilities**: Native node capabilities (`canvas.*`, `camera.*`, `screen.capture`, `system.run`) are registered selectively based on user preferences in `SettingsManager`.
- **User Confirmation Required**: Potentially dangerous capabilities (arbitrary shell commands, file modifications outside designated workspaces) require user interaction and confirmation before dispatch.
