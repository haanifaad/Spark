# Complete bug register

## SPARK-001: Approval-manager interface change breaks the application build

| Field | Finding |
|---|---|
| Severity | P1 - High |
| Category | Functional, Reliability, Architecture |
| Status | Reproducible |
| Affected module | `OpenClaw.Shared` approval subsystem, transitive `OpenClaw.Tray.WinUI` build |
| Preconditions | Current working tree; .NET SDK configured by `global.json` |
| Reproduction | Run the supported fixture command shown below |
| Expected | Fixture host builds and launches the selected current WinUI executable against its isolated synthetic Gateway |
| Actual | Compilation stops in `OpenClaw.Shared` with CS0535 before UI startup |
| Evidence | `TEST-EVIDENCE/BUILD-FAILURE.txt` |

Reproduction command:

```powershell
.\scripts\run-gateway-fixture.ps1 -AppPath .\src\OpenClaw.Tray.WinUI\bin\Debug\net10.0-windows10.0.22621.0\win-x64\OpenClaw.Tray.WinUI.exe -Scenario multi-session-browse
```

Compiler output identifies `SparkApprovalManager` as missing `ISparkApprovalManager.ApprovalRequested` and `ISparkApprovalManager.ApprovalStatusChanged`.

Relevant source:

- `src/OpenClaw.Shared/Security/Approvals/ISparkApprovalManager.cs:14-15`
- `src/OpenClaw.Shared/Security/Approvals/SparkApprovalManager.cs:16`
- `src/OpenClaw.Tray.WinUI/Presentation/AppServiceRegistration.cs:123`
- `src/OpenClaw.Tray.WinUI/OpenClaw.Tray.WinUI.csproj:105`

Root cause: an interface contract was expanded without updating the only production implementation. No source test covers interface implementation completeness or raises/observes the new events.

Recommended solution: add the two events to `SparkApprovalManager`; invoke request/status notifications at the corresponding successful state transitions; add tests for the event contract, then build the tray graph and run the fixture smoke.

Required regression tests:

1. Compile-time project build of `OpenClaw.Shared` and `OpenClaw.Tray.WinUI`.
2. Unit test that new requests raise `ApprovalRequested` exactly once.
3. Unit tests that approval, rejection, cancellation, and expiration raise `ApprovalStatusChanged` with the final state.
4. Fixture launch against `multi-session-browse`.

## SPARK-002: Approval persistence reports success if disk persistence fails

| Field | Finding |
|---|---|
| Severity | P1 - High candidate |
| Category | Security, Reliability, Database/persistence |
| Status | Unconfirmed |
| Affected module | `OpenClaw.Shared` approval persistence |

Static evidence shows `RequestApprovalAsync`, `ApproveAsync`, `RejectAsync`, and `CancelAsync` call `SaveToDisk` and then return success. `SaveToDisk` catches every exception and discards it (`SparkApprovalManager.cs:295-306`). A disk-full, denied-path, lock, or serialization failure can therefore leave the caller with a successful result whose state does not survive restart. The build blocker prevented safe fault injection, so this is not counted as a confirmed defect.

Recommended validation after SPARK-001: inject a failing filesystem abstraction or a write-denied temporary directory and assert that each operation reports failure and does not claim persistence.
