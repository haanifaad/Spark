# Security Policy

If you believe you have found a security vulnerability in SPARK, report it privately first.

Do not open a public issue or pull request that discloses an unpatched vulnerability, exploit path, secret, or security-sensitive proof of concept.

## Reporting

For vulnerabilities in this repository, submit a private GitHub Security Advisory for [haanifaad/Spark](https://github.com/haanifaad/Spark/security/advisories/new).

Useful reports include:

- affected version or commit SHA,
- impacted division, subsystem, or file path,
- reproduction steps or a proof of concept against latest `main`,
- actual impact and the SPARK security boundary crossed (Windows Safety Floor, HMAC Guard, DPAPI Store),
- Windows version and architecture,
- suggested remediation when practical.

Reports without reproduction steps, demonstrated impact, and remediation advice may be deprioritized.

## Scope

Security-relevant surfaces in this repository include:

- Windows tray pairing, credential storage, and gateway connection behavior,
- autonomous agent execution, TurnWatchdogMonitor, and HMAC-SHA256 approval guardrails,
- Windows Safety Floor (PowerShell AST decoding, disk wiping, root deletion prevention),
- Computer Use actuator (Win32 P/Invoke, coordinate clamping, UI-TARS translation),
- local setup, installer, update, uninstall, and PowerToys Command Palette flows,
- deep links, IPC (Voice Mode JSON-RPC pipe, MCP gateway), WebView2, and local file handling,
- GitHub Actions, dependency automation, and release packaging.

## Out of Scope

The following are usually out of scope:

- vulnerabilities in upstream Windows, .NET, WinUI, or third-party packages without reachable impact through SPARK,
- prompt injection by itself, unless it demonstrates a concrete auth, approval, sandbox, command-policy, or local-boundary bypass,
- reports that require prior administrative write access to trusted local state such as `%APPDATA%\OpenClawTray`, `%LOCALAPPDATA%\OpenClaw`, project files, shell profile files, or installed binaries,
- insecure local machine administration or multi-user host setups where the OS trust boundary is already lost,
- scanner-only findings without a working reproduction and demonstrated SPARK impact.

## Trust Boundaries & Security Floors

SPARK enforces strict local host security boundaries:

- **Windows Safety Floor**: Recursively decodes PowerShell `-EncodedCommand` arguments and denies destructive commands (`Format-Volume`, `diskpart`, `rmdir /s /q C:\Windows`, `reg delete HKLM`).
- **Cryptographic Approval Guard**: One-time approval tickets use HMAC-SHA256 nonces with a 60-second TTL and constant-time signature comparison to prevent replay attacks.
- **DPAPI Vault**: All AI model API keys and secrets are protected using Windows Data Protection API (`ProtectedData`).
- **Browser SSRF Guard**: Sandboxed browser operations reject loopback (`127.0.0.1`, `localhost`) and private RFC-1918 subnets (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`).
- **Commercial Safe Harbor**: The core SPARK platform is licensed under the MIT License; the non-commercial Mark-LVII 3D Face Avatar plugin is decoupled, defaulting to native MIT Arc Reactor and Fluent HUD modes.

Reports should demonstrate how an untrusted input crosses one of these verified boundaries.
