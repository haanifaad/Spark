# Project Spark - Troubleshooting & Diagnostic Guide

## 1. Quick Diagnostic Checklist

| Symptom | Probable Cause | Recommended Resolution |
| :--- | :--- | :--- |
| **"App host version: 10.0.15; .NET location: Not found"** | Missing or unregistered `DOTNET_ROOT` environment variable | Set `DOTNET_ROOT` to your .NET 10 installation directory |
| **Roslyn build error CS8034 (0x800711C7)** | Smart App Control (SAC) blocked unsigned analyzer DLL | Build with `-p:ReactorSourceMap=false -p:ReactorDiagnostics=false` |
| **"LocalPortConflict: Port 18789 already in use"** | Competing process or stale development server on port 18789 | Stop conflicting listener via `openclaw gateway stop` or taskkill |
| **Gateway readiness timeout after 30s** | First-time Node compile cache cold startup or missing build | Run `pnpm build` in `openclaw-main` before first launch |
| **PowerShell script execution disabled** | Windows default ExecutionPolicy (`PSSecurityException`) | Execute commands with `-ExecutionPolicy Bypass` |

---

## 2. In-Depth Diagnostics

### 2.1. Resolving .NET Runtime Discovery on Windows 11
If `OpenClaw.Tray.WinUI.exe` displays a message box stating `.NET is required`:
```powershell
# Set permanently for the current user:
[Environment]::SetEnvironmentVariable("DOTNET_ROOT", "$env:LOCALAPPDATA\Microsoft\dotnet", "User")
[Environment]::SetEnvironmentVariable("DOTNET_ROOT_X64", "$env:LOCALAPPDATA\Microsoft\dotnet", "User")

# Or set in the active terminal session:
$env:DOTNET_ROOT = "$env:LOCALAPPDATA\Microsoft\dotnet"
$env:DOTNET_ROOT_X64 = "$env:LOCALAPPDATA\Microsoft\dotnet"
```

### 2.2. Checking Managed Gateway Health Manually
If the desktop tray icon shows "Disconnected", verify the embedded Gateway manually:

1. **Check process status**:
   ```powershell
   Get-Process -Name node -ErrorAction SilentlyContinue | Select-Object Id, ProcessName, StartTime, WorkingSet64
   ```
2. **Probe HTTP health endpoint**:
   ```powershell
   Invoke-RestMethod -Uri "http://127.0.0.1:18789/health" -Method Get
   # Expected response: { "ok": true, "status": "live" }
   ```
3. **Inspect WebSocket RPC via CLI**:
   ```powershell
   cd C:\Intel\Spark\openclaw-main
   node openclaw.mjs gateway call health
   ```

### 2.3. Inspecting Win32 Job Object and Orphan Processes
Spark binds the Gateway lifecycle to a Win32 Job Object with `KILL_ON_JOB_CLOSE`.
- To confirm Job Object membership:
  - Open **Process Explorer** (Sysinternals).
  - Locate `OpenClaw.Tray.WinUI.exe`.
  - In properties, select the **Job** tab. `node.exe` must appear in the job member list.
- If Spark is killed via Task Manager or terminates unexpectedly, Windows guarantees that all child `node.exe` processes terminate immediately.

### 2.4. Diagnosing Endpoint Security Conflicts
If Spark reports `GatewayErrorKind.LocalPortConflict`:
- Spark protects user security by refusing to transmit operator or node device tokens to an untrusted listener.
- Identify the process holding the port:
  ```powershell
  Get-NetTCPConnection -LocalPort 18789 -ErrorAction SilentlyContinue | Select-Object LocalAddress, LocalPort, OwningProcess, State
  ```
- If the listener is an older orphaned Node process from manual CLI experimentation:
  ```powershell
  Stop-Process -Id <OwningProcessId> -Force
  ```
