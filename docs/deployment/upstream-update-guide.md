# Upstream Synchronization & Maintenance Guide

## 1. Upstream Repositories and Architecture Boundaries

Project Spark maintains a clean separation between upstream OpenClaw Core and the Windows Hub application:

| Repository Component | Upstream Remote URL | Local Path | Integration Boundary |
| :--- | :--- | :--- | :--- |
| **OpenClaw Core** | `https://github.com/openclaw/openclaw` | `C:\Intel\Spark\openclaw-main` | Subsystem runtime dependency. Spark executes `openclaw.mjs` directly without patching core files. |
| **Windows Hub (Spark)** | `https://github.com/openclaw/openclaw-windows-node` | `C:\Intel\Spark\openclaw-windows-node-main` | Application host repository containing Spark desktop enhancements and lifecycle supervisor. |

---

## 2. Protected Patch Surface in Windows Hub

When updating the Windows Hub repository from upstream, the following Spark integration files must be preserved:

1. **`src/OpenClaw.Connection/NativeGateway/SparkManagedGatewayRuntime.cs`**:
   - New file implementing `INativeGatewayRuntime` for supervised Node.js gateway hosting.
2. **`src/OpenClaw.Connection/NativeGateway/IsolatedGatewayRuntime.cs`**:
   - Routing hook in `NativeGatewayRuntimeRouter` directing local unmanaged/spark records to `SparkManagedGatewayRuntime`.
3. **`src/OpenClaw.Connection/NativeGateway/NativeGatewayRuntime.cs`**:
   - Addition of `string? Arguments = null` to `NativeGatewayStartSpec`.
4. **`src/OpenClaw.Connection/NativeGateway/WindowsNativeGatewayProcessHost.cs`**:
   - Forwarding of `spec.Arguments` to `ProcessStartInfo.Arguments`.
5. **`src/OpenClaw.Tray.WinUI/AppIdentity.cs`**:
   - Spark branding constants (`DisplayName = "Spark"`).
6. **`tests/OpenClaw.Connection.Tests/SparkManagedGatewayRuntimeTests.cs`**:
   - Automated unit test suite for lifecycle management.
7. **`tests/OpenClaw.Connection.Tests/SparkManagedGatewayIntegrationTests.cs`**:
   - Automated integration test suite running against the real Gateway.

---

## 3. Step-by-Step Upstream Update Procedure

### Phase A: Updating OpenClaw Core
```powershell
cd C:\Intel\Spark\openclaw-main

# 1. Fetch latest changes from upstream
git fetch origin main

# 2. Merge or fast-forward to target tag or commit
git merge origin/main

# 3. Update dependencies and recompile
pnpm install
pnpm build

# 4. Verify version and gateway health probe
node openclaw.mjs --version
```

### Phase B: Updating Windows Hub (Spark)
```powershell
cd C:\Intel\Spark\openclaw-windows-node-main

# 1. Fetch latest changes from upstream
git fetch origin main

# 2. Rebase or merge while preserving Spark commits
git merge origin/main

# 3. Compile full solution with .NET 10 SDK
$env:DOTNET_ROOT = "C:\Users\PC\AppData\Local\Microsoft\dotnet"
$env:DOTNET_ROOT_X64 = "C:\Users\PC\AppData\Local\Microsoft\dotnet"
$env:PATH = "C:\Users\PC\AppData\Local\Microsoft\dotnet;C:\Users\PC\AppData\Local\Programs\Git\cmd;C:\Users\PC\AppData\Roaming\npm;" + $env:PATH

dotnet build src\OpenClaw.Tray.WinUI\OpenClaw.Tray.WinUI.csproj -r win-x64 -p:ReactorSourceMap=false -p:ReactorDiagnostics=false

# 4. Run Spark regression test suite
dotnet test tests\OpenClaw.Connection.Tests\OpenClaw.Connection.Tests.csproj --filter "FullyQualifiedName~SparkManaged" -p:ReactorSourceMap=false -p:ReactorDiagnostics=false
```

---

## 4. Upstream Breaking Change Checklist
If upstream OpenClaw Core modifies protocol schemas or CLI flags:
- **CLI Flag Changes**: Review `openclaw gateway run --help`. If flags for port, bind, or auth mode change, update `SparkManagedGatewayRuntime.cs` `Arguments` string format.
- **Protocol Schema Changes**: Review `packages/gateway-protocol`. Verify `ConnectEnvelopeBuilder` and `GatewayProtocolContract.cs` continue matching request schemas.
- **Health Check Format**: Verify `http://127.0.0.1:{port}/health` continues returning `{ "ok": true }`.
