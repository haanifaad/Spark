# Project Spark — Progress & Milestone Tracker

## Current Milestone Status

- [x] **M0: Repository and Environment Audit**
  - Inspected Windows Hub (`openclaw-windows-node-main`) and Core Gateway (`openclaw-main`).
  - Audited all 11 C# projects, .NET requirements, Node.js (`v26.13.0`), pnpm (`12.18.0`).
  - Completed `docs/audit/repository-audit.md`.

- [x] **M1: Gateway Source and Dependency Analysis**
  - Traced Gateway entry point (`openclaw.mjs` -> `src/entry.ts` -> `src/gateway/server.ts`).
  - Identified HTTP/WS networking (`node:http`, `ws`), embedded SQLite (`node:sqlite`).
  - Traced Node-Host protocol (`node.invoke.request` / `node.invoke.result`).
  - Completed `docs/architecture/gateway-source-analysis.md` and `docs/architecture/gateway-dependency-map.md`.

- [x] **M2: Integration Architecture**
  - Defined hybrid desktop architecture (WinUI 3 + in-process managed Node.js Gateway supervisor).
  - Selected Windows Job Objects for leak-free child process management.
  - Completed `docs/architecture/integration-architecture.md` and `docs/architecture/feature-preservation-matrix.md`.

- [/] **M3: Gateway Source Integration & Build**
  - Pnpm dependencies installed in `openclaw-main` (1,479 packages linked, native drivers compiled).
  - Compiling Gateway distribution (`pnpm build`).

- [ ] **M4: Windows Gateway Lifecycle Manager**
  - Implement `SparkGatewayProcessManager` in `OpenClaw.Connection` / `OpenClaw.Shared`.
  - Job Object attachment, loopback discovery, startup health probing, graceful shutdown.

- [ ] **M5: Gateway Client Integration**
  - Connect `OpenClawGatewayClient` and `WindowsNodeClient` to managed local Gateway.
  - Verify handshake, pairing, and session creation.

- [ ] **M6: Agent Runtime Verification**
  - End-to-end user message -> Gateway -> Agent Runner -> tool execution -> response stream.

- [ ] **M7: Feature Preservation**
  - Verify tray, chat, canvas, screen, camera, system capabilities.

- [ ] **M8: Security & Configuration**
  - Produce `docs/security/security-review.md`.

- [ ] **M9: Spark Branding**
  - Apply Spark branding across application titles and packaging metadata.

- [ ] **M10: Testing & Regression**
  - Execute automated tests, produce `docs/testing/test-results.md` and `docs/testing/compatibility-report.md`.

- [ ] **M11: Packaging & Documentation**
  - Installation guides, troubleshooting docs, upstream update strategy.
