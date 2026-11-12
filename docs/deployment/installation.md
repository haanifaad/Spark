# Project Spark - Installation & Deployment Guide

## 1. System Requirements

### 1.1. Operating System
- **Windows 11** (64-bit x64 or ARM64, Build 22000 or later recommended).
- **Windows 10** (64-bit Version 2004 / Build 19041 or later).
- Compatible with active **Smart App Control (SAC)** on Windows 11.

### 1.2. Runtimes & Dependencies
- **.NET 10 Runtime**: `Microsoft.WindowsDesktop.App` version 10.0 or higher.
- **Node.js**: Version 22.x LTS or Version 26.x (recommended). Provides `node:sqlite` built-in support.
- **Microsoft Edge WebView2**: Pre-installed on Windows 11 and modern Windows 10. Required for Canvas and interactive Web UI components.

---

## 2. Deployment Models

Spark supports two primary deployment topologies:

### 2.1. Self-Contained / Portable Deployment (Production)
In this layout, Spark bundles the Node.js runtime and compiled OpenClaw Gateway distribution alongside the WinUI 3 binaries:

```text
Spark/
├── OpenClaw.Tray.WinUI.exe      # Main Spark desktop entry point
├── OpenClaw.Shared.dll          # Shared protocol & capability library
├── OpenClaw.Connection.dll      # Connection management & Gateway supervisor
├── OpenClaw.Chat.dll            # Native chat UI rendering
├── OpenClawTray.FunctionalUI.dll# Windows functional presentation
├── WebView2Loader.dll           # WebView2 loader
├── runtimes/
│   ├── node/
│   │   └── node.exe             # Bundled portable Node.js runtime
│   └── gateway/
│       ├── openclaw.mjs         # Upstream OpenClaw entry launcher
│       ├── dist/                # Precompiled OpenClaw core + plugin assets
│       └── package.json
└── Assets/                      # Icons, splash screens, visual assets
```

### 2.2. Development Checkout Deployment
For development and continuous integration environments where OpenClaw Core and Windows Hub reside as sibling checkouts:

1. **Windows Hub**: `C:\Intel\Spark\openclaw-windows-node-main`
2. **OpenClaw Core**: `C:\Intel\Spark\openclaw-main`

Spark automatically discovers the sibling checkout at `C:\Intel\Spark\openclaw-main\openclaw.mjs` without requiring manual path configurations.

---

## 3. Step-by-Step Setup Instructions

### 3.1. Building OpenClaw Core
From the `openclaw-main` directory:
```powershell
cd C:\Intel\Spark\openclaw-main
pnpm install
pnpm build
```
Verify the build:
```powershell
node openclaw.mjs --version
# Expected: OpenClaw 2026.11.3
```

### 3.2. Building Spark Native Desktop App
Set the required environment variables and build with .NET 10 SDK:
```powershell
$env:DOTNET_ROOT = "C:\Users\PC\AppData\Local\Microsoft\dotnet"
$env:DOTNET_ROOT_X64 = "C:\Users\PC\AppData\Local\Microsoft\dotnet"
$env:PATH = "C:\Users\PC\AppData\Local\Microsoft\dotnet;C:\Users\PC\AppData\Local\Programs\Git\cmd;C:\Users\PC\AppData\Roaming\npm;" + $env:PATH

cd C:\Intel\Spark\openclaw-windows-node-main
dotnet build src\OpenClaw.Tray.WinUI\OpenClaw.Tray.WinUI.csproj `
  -r win-x64 `
  -p:ReactorSourceMap=false -p:ReactorDiagnostics=false
```

### 3.3. Launching Spark
Run the compiled executable:
```powershell
$env:DOTNET_ROOT = "C:\Users\PC\AppData\Local\Microsoft\dotnet"
.\src\OpenClaw.Tray.WinUI\bin\Debug\net10.0-windows10.0.22621.0\win-x64\OpenClaw.Tray.WinUI.exe
```

1. Spark initializes and creates the system tray icon in the Windows taskbar.
2. `SparkManagedGatewayRuntime` detects that no local Gateway is running on port 18789.
3. Spark launches `node openclaw.mjs gateway run --port 18789 --bind loopback --dev` in a dedicated Win32 Job Object.
4. The HTTP health check confirms the Gateway is live.
5. The operator and node clients connect over WebSocket and perform the cryptographic handshake.
6. The chat interface is available from the tray menu or by clicking the notification icon.
