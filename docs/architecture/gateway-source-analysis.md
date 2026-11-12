# Gateway Source Analysis — OpenClaw Core

## 1. Overview

This document provides an exhaustive structural analysis of the OpenClaw Core Gateway implementation located in `C:\Intel\Spark\openclaw-main`. The analysis traces the complete execution lifecycle from the process entry point down to network sockets, RPC handlers, database persistence, and agent dispatch.

---

## 2. Gateway Entry Point & Execution Chain

The execution path for starting the OpenClaw Gateway (`openclaw gateway run`) traces through these key modules:

```
[1] openclaw.mjs
    ├── Runtime compatibility checks (node >=24.16 <25 || >=26.1)
    ├── V8 compile-cache initialization (node-compile-cache.mjs)
    └── Imports dist/entry.js (Source: src/entry.ts)

[2] src/entry.ts
    ├── Normalizes CLI args (cli/windows-argv.ts)
    ├── Sets environment & process role ("gateway")
    └── Calls runCli() in src/cli/run-main.ts

[3] src/cli/run-main.ts
    ├── Registers CLI command groups via Commander
    └── Calls registerGatewayCli() in src/cli/gateway-cli/register.ts

[4] src/cli/gateway-cli/run-command.ts
    ├── Defines flags: --port, --bind, --token, --auth, --password, --tailscale, --dev, --force
    └── Invokes runGatewayCommand() in src/cli/gateway-cli/run.ts

[5] src/cli/gateway-cli/run.ts
    ├── Loads configuration: readConfigFileSnapshotWithPluginMetadata()
    ├── Resolves authorization mode: resolveGatewayAuth()
    ├── Resolves bind network address: resolveGatewayBindHost()
    ├── Acquires gateway process lock (infra/proc-lock.ts)
    └── Calls startGatewayServer() in src/gateway/server.ts

[6] src/gateway/server.ts & src/gateway/server-start.ts
    ├── Initializes database engine (SQLite via node:sqlite / Postgres)
    ├── Mounts HTTP server via node:http (or node:https)
    ├── Mounts WebSocket server via ws attached to HTTP upgrade
    ├── Registers RPC handlers in src/gateway/server-methods.ts
    ├── Starts background services (Heartbeat, Crons, Watchers, Nodes)
    └── Listens on target port (default 18789)
```

---

## 3. Server Architecture & Network Transports

### 3.1 HTTP Server Subsystem (`src/gateway/server-http.ts`)
- Implemented with Node.js standard library `node:http.createServer()`.
- Routes:
  - `GET /health` or `GET /api/health`: Health probe returning gateway state, version, memory, uptime, connected clients.
  - `GET /` & `/dashboard/*`: Serves the embedded Control UI (Vite/React bundle in `dist/control-ui`).
  - `POST /api/webhook/*`: Ingress endpoints for external channel integrations.
  - `POST /api/upload`: Binary/file asset upload handler.
  - `GET /api/canvas/*`: Serves dynamic visual assets for Canvas/A2UI rendering.

### 3.2 WebSocket Server Subsystem (`src/gateway/server-http-upgrades.ts`)
- Uses `ws.WebSocketServer({ noServer: true })`.
- Attached to HTTP server's `upgrade` event:
  - Validates HTTP headers, path, protocol version, and origin.
  - Negotiates per-message deflate compression (`src/gateway/ws-compression.ts`).
  - Upgrades socket and creates client session instance (`src/gateway/server/ws-connection/`).

### 3.3 Framing & Wire Protocol
- Protocol is JSON-RPC style bidirectional messaging:
  - **Requests**: `{ type: "req", id: "<uuid>", method: "<name>", params: { ... } }`
  - **Responses**: `{ type: "res", id: "<uuid>", ok: true|false, result: { ... }, error: { code, message } }`
  - **Events**: `{ type: "event", event: "<name>", payload: { ... }, seq: <int> }`
- Roles supported during handshake:
  - `operator`: Control clients (Windows Hub chat, web control UI, TUI, CLI).
  - `node`: Hardware capability providers (Windows Companion Node, macOS Node, Android Node).

---

## 4. Authentication, Handshake & Pairing

### 4.1 Operator Handshake Flow (`src/gateway/server/ws-connection/authenticated-request-dispatch.ts`)
1. Client connects via WS.
2. Client sends `connect` method:
   ```json
   {
     "type": "req",
     "id": "init-1",
     "method": "connect",
     "params": {
       "role": "operator",
       "client": { "id": "tray-ui", "version": "1.0.0", "platform": "win32" },
       "device": { "id": "<device-hash>", "publicKey": "<base64-ed25519-pk>", "signature": "<v3-sig>" },
       "auth": { "token": "<gateway-token>" }
     }
   }
   ```
3. Gateway validates signature against public key, verifies token against `openclaw.json` or state DB.
4. Gateway responds with session info, permissions, and available agents.

### 4.2 Node Handshake & Remote Tool Dispatch (`src/gateway/server/ws-connection/connect-node-session.ts`)
1. Node connects with `role: "node"` and its device public key.
2. Gateway verifies device pairing via `src/infra/device-pairing-node.ts`.
3. If paired, node registers its capabilities: `canvas`, `camera`, `screen`, `system`, `device`, `location`, `stt`, `tts`, `browser`, `file`.
4. When an agent executes a node-delegated tool (e.g. `system.run` or `screen.snapshot`):
   - Gateway sends `node.invoke.request` event to the node socket.
   - Node streams intermediate chunks via `node.invoke.progress`.
   - Node finishes with `node.invoke.result`.
   - Gateway receives the result and feeds it back into the Agent Runtime reasoning loop.

---

## 5. Agent Runtime & Orchestration

The core agent intelligence engine lives in `src/agents/`:
- **`run-orchestrator.ts`**: Coordinates the multi-step agent reasoning loop.
  1. Accepts user prompt from chat session.
  2. Constructs prompt context (system instructions, memory entries, session history, available tools schema).
  3. Sends request to selected model provider (Claude, OpenAI, Gemini, local Ollama, etc.).
  4. Parses streaming LLM output (text tokens, reasoning tokens, tool call intentions).
  5. If model calls a tool:
     - Dispatches tool locally (e.g. file read, web search) or remotely to Windows Node via `node.invoke.request`.
     - Collects tool result.
     - Appends tool result to context and recursively prompts LLM for subsequent thought/action.
  6. Emits incremental streaming events back to the Windows Hub operator client (`chat.chunk`, `chat.tool_call`, `chat.complete`).
- **Session Management (`src/sessions/`)**:
  - Manages active conversation sessions.
  - Persists messages and history to JSONL files and SQLite.

---

## 6. Persistence & Storage Engines

- **SQLite Engine (`node:sqlite`)**:
  - Requires Node.js standard library `node:sqlite` (Node 24/26).
  - Uses `DatabaseSync` in WAL mode for crash-resilient transactional storage.
  - Schema migrations are automated on startup (`src/state/openclaw-state-db.ts`).
- **File Storage**:
  - Session transcripts stored under `~/.openclaw/agents/<agentId>/sessions/`.
  - Workspace files stored under `~/.openclaw/workspace/`.

---

## 7. Implications for Spark Integration

1. The Gateway does **not** need to be rewritten in Rust or C# — it is designed to run on Node.js using standard `node:http` and `ws`.
2. The Windows Hub already implements the exact client protocol needed to talk to this Gateway.
3. Spark will bundle the Node.js runtime and OpenClaw Gateway as a managed local child process controlled via Windows Job Objects.
