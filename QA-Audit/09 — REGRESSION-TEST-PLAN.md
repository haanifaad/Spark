# Regression test plan

## Build gate

```powershell
dotnet build .\src\OpenClaw.Shared\OpenClaw.Shared.csproj
dotnet build .\src\OpenClaw.Tray.WinUI\OpenClaw.Tray.WinUI.csproj -r win-x64
```

## Approval contract tests

- Request raises `ApprovalRequested` once after durable persistence succeeds.
- Approve, reject, cancel, and expiry raise `ApprovalStatusChanged` once with correct final state.
- A failing persistence implementation returns an error and does not advertise completion.
- Restart after every successful state transition reloads the expected state.

## UI recovery gate

```powershell
.\scripts\run-gateway-fixture.ps1 -AppPath .\src\OpenClaw.Tray.WinUI\bin\Debug\net10.0-windows10.0.22621.0\win-x64\OpenClaw.Tray.WinUI.exe -Scenario multi-session-browse
```

Pass criteria: the fixture reports ready, starts an owned SPARK process, exposes the workspace, sessions, settings, and synthetic Gateway data, and exits cleanly.
