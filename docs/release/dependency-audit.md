# Spark - Technical Dependency Audit & Runtime Requirements

## 1. Overview

This document analyzes the exact dependency footprint of **SPARK**, establishing the boundary between build-time development resources and the minimal set of files required for the production runtime and installer package.

---

## 2. Dependency Audit Matrix

| Component / Folder | Required? | Build-time or Runtime? | Technical Reason | Action |
| :--- | :--- | :--- | :--- | :--- |
| **`openclaw-windows-node-main/src`** | YES | Build & Runtime | Contains the complete C# / WinUI 3 desktop application source, lifecycle supervisor, and native node components. | Retain in Git repository; compile into production binaries. |
| **`openclaw-windows-node-main/tests`** | YES | Build-time only | Automated test suites for cryptography, supervisor lifecycle, DPAPI, and sandbox validation. | Retain in Git repository; exclude from production installer. |
| **`openclaw-windows-node-main/scripts`** | YES | Build-time only | Automation scripts for building, testing, linting documentation, and compiling Inno Setup installer. | Retain in Git repository; exclude from production installer. |
| **`openclaw-windows-node-main/docs`** | YES | Build-time only | Maintained documentation, architectural specifications, security reviews, and QA audit reports. | Retain in Git repository; exclude from production installer. |
| **`openclaw-windows-node-main/artifacts`** | NO | Temporary output | Local scratch builds, test outputs, and diagnostic logs. | Exclude via `.gitignore`; purge prior to commit. |
| **`openclaw-windows-node-main/publish-local-*`** | NO | Temporary output | Intermediate Inno Setup publish staging directories. | Exclude via `.gitignore`; regenerate dynamically during installer build. |
| **`openclaw-windows-node-main/bin`, `obj`** | NO | Temporary output | MSBuild intermediate compilation outputs and assemblies. | Exclude via `.gitignore`; clean prior to commit. |
| **`tools/vswhere.exe`** | YES | Build-time only | Required by MSBuild `Directory.Build.props` to discover Visual C++ redistributables and toolchains. | Retain in development environment; exclude from installer. |
| **`vc_redist/`** | YES | Build & Installer | Microsoft Visual C++ CRT redistributable binaries required for native Node.js addons and WebView2 on fresh Windows installations. | Bundle into Inno Setup installer as optional prerequisite. |
| **`openclaw-main/openclaw.mjs`** | YES | Runtime | Primary Node.js entry point and runtime launcher for the OpenClaw Gateway. | Required at runtime; referenced by `SparkManagedGatewayRuntime`. |
| **`openclaw-main/dist`** | YES | Runtime | Compiled JavaScript artifacts (`entry.js`, `control-ui/`, bundled plugins, and core extensions). | Required at runtime; bundled into Gateway distribution. |
| **`openclaw-main/*.mjs` helpers** | YES | Runtime | Runtime modules (`cli-invocation.mjs`, `node-sqlite.mjs`, `node-compile-cache.mjs`, `node-runtime-recovery.mjs`) loaded by `openclaw.mjs`. | Required at runtime; bundled with Gateway distribution. |
| **`openclaw-main/node_modules`** | YES | Runtime | Node.js production runtime dependencies (pnpm virtual store) required by the Gateway daemon and agent runner. | Retain for Gateway execution; exclude development devDependencies in final bundle. |
| **`openclaw-main/src`** | NO | Build-time only | Raw TypeScript source files for the Gateway. Once compiled into `dist/`, raw TypeScript is not required for execution. | Retain in development workspace for future upstream patches; exclude from installer. |
| **`openclaw-main/test`, `qa`** | NO | Development only | Vitest test suites, fixtures, and internal benchmarking suites for OpenClaw Core. | Exclude from production release and installer. |
| **`openclaw-main/apps`, `crates`, `ui`**| NO | Development only | Upstream monorepo artifacts for macOS/Linux shells, Rust crates, and web demo frontends unused by Windows native SPARK. | Exclude from production release and installer. |

---

## 3. Runtime Architecture & Separation of Concerns

### Development Repository Scope (`haanifaad/Spark`)
The Git repository represents the maintainable Windows desktop solution:
- All C# source code and WinUI 3 XAML assets (`src/`).
- Automated tests (`tests/`).
- Documentation and architectural records (`docs/`).
- Build and installer scripts (`scripts/`, `installer.iss`).
- Central Package Management and solution definitions (`openclaw-windows-node.slnx`, `Directory.Build.props`, `Directory.Packages.props`).

### Production Installer Scope (`SPARK-Setup-x64.exe`)
The installer contains only published, compiled, and production-ready artifacts:
1. `OpenClaw.Tray.WinUI.exe` and self-contained .NET 10 runtime libraries.
2. Windows App SDK runtime assets and WebView2 native loaders.
3. Sandboxed `OpenClaw.FileWorker.exe` and `winnode.dll`.
4. High-resolution application icons (`spark.ico`) and assets.
5. Inno Setup uninstaller and migration helpers.
6. VC++ runtime redistributable installer (`vc_redist.x64.exe`).
7. Bundled Gateway launch configuration pointing to the local Node.js engine and agent runtime.
