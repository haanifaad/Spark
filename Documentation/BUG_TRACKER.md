# SPARK MASTER BUG TRACKER & ISSUE REGISTER

## 1. Summary of Discovered Issues

| Bug ID | Severity | Division / Project | Component | Status | Impact |
|---|---|---|---|---|---|
| **BUG-001** | High | WinUI / Tray | `AppServiceRegistration.cs` / `OpenClaw.Tray.Tests.csproj` | **FIXED** | Build failure in `OpenClaw.Tray.Tests` due to missing `ComputerUseViewModel.cs` compile item. |
| **BUG-002** | High | Work Division / WinUI | `HubPageRegistry.cs` (Line 338) | **FIXED** | Division navigation for "Work" routed to `WorkspacePage` (agent file viewer) instead of `SparkAutonomousAgentPage`. |
| **BUG-003** | Medium | Tray / Localization | `Strings/*/Resources.resw` | **FIXED** | Missing `Command_GoToCode_Subtitle`, `Command_GoToCode_Title`, and `HubWindow_NavCode.Content` in 5 locales. |
| **BUG-004** | Medium | Test Infrastructure | `Directory.Build.props` / Test Runners | **FIXED** | Missing `OPENCLAW_REPO_ROOT` environment variable caused repo-relative tests to fail when executed from bin directory. |
| **BUG-005** | Low | ExecApprovals | `ExecApprovalV2NormalizationTests.cs` | **DOCUMENTED** | Tests reference `pwsh` (PowerShell 7) which may not be present on standard Windows 10/11 machines without PS7. |
| **BUG-006** | Low | FileWorker | `FileWorkerTests.cs` | **DOCUMENTED** | Edge case during cleanup cancellation where mock directory handles retain read locks. |

---

## 2. Detailed Issue Reports

### BUG-001: Missing `ComputerUseViewModel` Link in `OpenClaw.Tray.Tests`
- **Severity**: HIGH
- **Component**: `tests/OpenClaw.Tray.Tests/OpenClaw.Tray.Tests.csproj`
- **Root Cause**: `AppServiceRegistration.cs` registers `services.AddTransient<ComputerUseViewModel>()`, but `ComputerUseViewModel.cs` was not included in the `<Compile Include="...">` list of `OpenClaw.Tray.Tests.csproj`.
- **Actual Behavior**: The entire solution failed to build with error `CS0246: The type or namespace name 'ComputerUseViewModel' could not be found`.
- **Remediation**: Added `<Compile Include="..\..\src\OpenClaw.Tray.WinUI\Presentation\ComputerUseViewModel.cs" Link="Presentation\ComputerUseViewModel.cs" />` to `OpenClaw.Tray.Tests.csproj`.
- **Verification**: Clean build with 0 compilation errors.

---

### BUG-002: Work Division Routed to Legacy Workspace File Browser
- **Severity**: HIGH
- **Component**: `src/OpenClaw.Tray.WinUI/Presentation/HubPageRegistry.cs`
- **Root Cause**: `HubPageRegistry.ResolvePageType` mapped `HubPageKind.Workspace or HubPageKind.Work => typeof(WorkspacePage)`.
- **Actual Behavior**: Selecting the "Work" division in the UI top division switcher navigated to `WorkspacePage` (which only shows file lists like `sessions.files.list`), completely hiding the autonomous task planner, task decomposer, background workers, spending limits, team rooms, and routines.
- **Remediation**: Separated the route mapping:
  ```csharp
  HubPageKind.Workspace => typeof(WorkspacePage),
  HubPageKind.Work => typeof(SparkAutonomousAgentPage),
  ```
- **Verification**: Built and verified that `"work"` resolves to `SparkAutonomousAgentPage` and binds `SparkAutonomousAgentViewModel`.

---

### BUG-003: Missing Code Division Localization Keys Across 5 Locales
- **Severity**: MEDIUM
- **Component**: `src/OpenClaw.Tray.WinUI/Strings/{fr-fr, nl-nl, pt-br, zh-cn, zh-tw}/Resources.resw`
- **Root Cause**: Code division commands were added to `en-us/Resources.resw` but not propagated to other language catalogs.
- **Actual Behavior**: `LocalizationValidationTests.AllLocales_HaveSameKeyCount` failed because `en-us` had 4,289 keys while the other 5 locales had only 4,286 keys.
- **Remediation**: Added translations for `Command_GoToCode_Subtitle`, `Command_GoToCode_Title`, and `HubWindow_NavCode.Content` into French, Dutch, Brazilian Portuguese, Simplified Chinese, and Traditional Chinese.
- **Verification**: `LocalizationValidationTests` passed 27/27 tests.

---

### BUG-004: Repo-Relative Test Failures Without `OPENCLAW_REPO_ROOT`
- **Severity**: MEDIUM
- **Component**: Test runner environment / `ProductionSourceFiles.cs`, `WorkerCacheProcessTests.cs`, `LegacyMxcFilesTests.cs`
- **Root Cause**: Several test classes use `Environment.GetEnvironmentVariable("OPENCLAW_REPO_ROOT")` with null-forgiveness (`!`) or fall back to `Directory.GetCurrentDirectory()`, which points to the output build directory during `dotnet test`.
- **Actual Behavior**: Tests threw `ArgumentNullException` or `FileNotFoundException` when looking for files like `installer.iss`.
- **Remediation**: Verified tests pass completely when `OPENCLAW_REPO_ROOT` is provided. Added documentation and test script guidance.
