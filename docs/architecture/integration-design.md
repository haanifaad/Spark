# Integration Design - Project Spark

## 1. Upstream Integration Strategy

### 1.1 Strategy Selection
- **Selected Approach**: Pinned upstream source snapshot with dedicated runtime packaging (`runtimes/gateway`).
- **Rationale**:
  - The OpenClaw Core repository (`openclaw-main`) is a TypeScript/JavaScript monorepo that compiles down to clean ESM bundles in `dist/`.
  - The Windows Hub repository (`openclaw-windows-node-main`) is a C# .NET 10 solution that compiles to native WinUI 3 desktop binaries.
  - Rather than attempting an unsupported language port of either codebase, Spark bridges both runtimes cleanly:
    1. The OpenClaw Core distribution (`dist/`, `package.json`, `node_modules`, `openclaw.mjs`) is bundled into the desktop application payload under `runtimes/gateway/`.
    2. A compatible Node.js binary (v26.x / v24.x) is resolved or bundled under `runtimes/node/`.
    3. The C# Windows Hub manages the Node.js Gateway process lifecycle via Win32 Job Objects.
    4. Communication happens over loopback WebSocket (`ws://127.0.0.1:18789`), using the exact existing protocol handlers.

---

## 2. Component Mapping & Reusability

### 2.1 Reused Components (No Changes Needed)
- **`OpenClawGatewayClient`**: Reused 100% as the Operator client.
- **`WindowsNodeClient`**: Reused 100% as the Node capability client.
- **`DeviceIdentity`**: Reused for Ed25519 handshake signing and pairing.
- **Node Capabilities**: Reused for camera, screen, canvas, browser, stt, tts, and file execution.
- **WinUI 3 Reactor Chat**: Reused for the user chat surface.

### 2.2 Extended Components
- **`NativeGatewayRuntimeRouter`**: Extended to automatically select `SparkManagedGatewayRuntime` for local gateway configurations.
- **`GatewayRegistry`**: Pre-populates a "Spark Local Gateway" record (`ws://127.0.0.1:18789`) on initial launch if no gateway is configured.
- **Settings & Wizard UI**: Surfaces Spark Local Gateway status, restart button, and port configuration.

### 2.3 Created Components
- **`SparkManagedGatewayRuntime`**: Supervised Node.js child process lifecycle manager implementing `INativeGatewayRuntime`.
- **`WindowsNativeGatewayProcessHost`**: Win32 Job Object supervisor ensuring leak-free child process management.

---

## 3. Security Architecture
- The Gateway is configured to bind strictly to loopback (`127.0.0.1`) by default (`--bind loopback`), preventing external network access.
- Authentication tokens are generated and stored in the user profile (`gateways.json` and `%APPDATA%\OpenClawTray`).
- Windows Node tool execution retains MXC container sandboxing and hash-based command approval gates (`exec-approvals.json`).

---

## 4. Packaging and Distribution Architecture
- Desktop distribution produces an Inno Setup installer that copies:
  - Spark WinUI 3 binaries (`OpenClaw.Tray.WinUI.exe`, DLLs, assets).
  - OpenClaw Core distribution (`runtimes/gateway/`).
  - Node.js runtime (`runtimes/node/`).
- The application runs out of the box with zero prerequisite installation steps.
