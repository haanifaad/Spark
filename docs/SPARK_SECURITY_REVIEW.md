# SPARK Security Review

## 1. Executive Summary

This document reviews the security boundaries, authorization guarantees, and threat mitigations designed into SPARK to protect user data, prevent unauthorized system compromise, and eliminate common agent vulnerabilities.

---

## 2. Threat Modeling & Attack Vectors

### 2.1 Threat: Prompt-Injection Command Execution
- Vector: An untrusted web page, document, or third-party message instructs an agent to execute destructive commands (e.g., `Remove-Item -Recurse C:\` or `format C:`).
- Mitigation in SPARK:
  1. The **Windows Safety Floor** parses commands using PowerShell ASTs before execution. Any command matching catastrophic disk wiping, system root modification, boot configuration changes, or disk formatting is rejected with a permanent security denial that cannot be overridden by user "always allow" rules.
  2. Any command with unresolvable or obfuscated dynamic evaluation (such as `Invoke-Expression` on dynamic variables or hidden subshells) is held for explicit human approval.

### 2.2 Threat: Approval Ticket Replay Attacks
- Vector: An attacker intercepts or tricks a user into approving a tool invocation, then replays the valid HMAC approval token to execute subsequent unauthorized actions.
- Mitigation in SPARK:
  1. The **HMAC Approval Guard** mandates that every ticket contains a cryptographic nonce (`Guid`) and a UTC timestamp.
  2. An in-memory replay cache tracks consumed nonces. Re-submitting an already-consumed ticket fails immediately.
  3. Tickets expire automatically after 60 seconds.
  4. Constant-time equality checks eliminate timing side-channel attacks.

### 2.3 Threat: Browser Profile & Credential Exfiltration
- Vector: An agent automating browser tasks attaches to the user's primary desktop browser profile, gaining access to saved passwords, corporate SSO cookies, and personal sessions.
- Mitigation in SPARK:
  1. **Strict Profile Isolation**: SPARK strictly prohibits attaching to the user's daily Edge profile.
  2. All agent browser tasks launch with a dedicated sandbox profile in `%LOCALAPPDATA%\OpenClaw\BrowserProfiles\AgentProfile\` or in an in-memory ephemeral profile.
  3. **SSRF Filtering**: The `BrowserSsrfGuard` intercepts navigations and blocks requests to loopback addresses (`127.0.0.0/8`, `localhost`, `::1`), link-local metadata services (`169.254.169.254`), and private intranet subnets.

### 2.4 Threat: Memory Tampering & History Rewriting
- Vector: A malicious tool invocation or compromised subagent uses file manipulation tools to rewrite its own instructions or erase logs of unauthorized actions.
- Mitigation in SPARK:
  1. The **SQLite Memory Journal** lives outside the agent's project workspace directory under `%LOCALAPPDATA%\OpenClaw\Memory\journal.db`.
  2. The database is secured with NTFS user permissions and DPAPI encryption for sensitive entries.
  3. Memory tools operating inside the workspace cannot alter or delete the external audit journal.

### 2.5 Threat: Unattended Runaway Loops
- Vector: An autonomous routine enters an infinite retry loop or repeatedly issues failing shell commands overnight, burning API credits or exhausting local CPU/disk.
- Mitigation in SPARK:
  1. **Unattended Execution Ceilings**: Background routines enforce a strict maximum iteration count (default 15) and consecutive error ceiling (default 3).
  2. **Turn Watchdog Monitor**: Detects silent hangs and runaway commands, terminating orphaned child processes via Win32 Job Objects.
