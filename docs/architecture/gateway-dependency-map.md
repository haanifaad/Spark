# Gateway Dependency Map - OpenClaw Core

## 1. Classification Methodology

In accordance with Phase 1 instructions, all modules and dependencies required by the OpenClaw Gateway are categorized into:
- **A. Required Gateway Dependency**: Core networking, server, protocol, database, and configuration modules.
- **B. Required Agent Runtime Dependency**: LLM orchestration, model providers, tool dispatch, context engines.
- **C. Optional Integration**: External chat channels (Slack, Discord, Telegram, WhatsApp) that can be enabled dynamically.
- **D. Windows-Specific Dependency**: Native platform wrappers, Windows spawn helpers, named pipe / process handlers.
- **E. External Service Dependency**: Cloud LLM API endpoints (Anthropic, OpenAI, Google) or local inference servers (Ollama).
- **F. Development-Only Dependency**: Build tools, linters, TypeScript compiler, test frameworks.

---

## 2. Monorepo Package Dependencies (`packages/`)

| Monorepo Package | Category | Role in Gateway |
|---|---|---|
| `@openclaw/gateway-protocol` | **A** | WebSocket wire protocol schemas, request/response message definitions |
| `@openclaw/normalization-core` | **A** | String and input normalization utilities |
| `@openclaw/terminal-core` | **A** | Terminal safe formatting, log streams |
| `@openclaw/openclaw-id-protocol`| **A** | Device and agent cryptographic identifier serialization |
| `@openclaw/net-policy` | **A** | SSRF protection, IP allowlists, private subnet filters |
| `@openclaw/plugin-package-contract`| **A** | Plugin metadata schemas, extension boundaries |
| `@openclaw/agent-core` | **B** | Agent lifecycle, attempt tracking, step execution |
| `@openclaw/ai` | **B** | Core AI primitives, token counting, message formatting |
| `@openclaw/llm-core` | **B** | Provider abstractions, unified stream parsing |
| `@openclaw/model-catalog-core` | **B** | Model catalog, capability metadata, pricing tables |
| `@openclaw/media-core` | **B** | Image, audio, and media mime-type handling |
| `@openclaw/worker-runtime` | **B** | Concurrency bounds, async queue scheduling |
| `@openclaw/plugin-sdk` | **B** | Extension API surface for tools and channels |

---

## 3. Subsystem Dependency Map (`src/`)

```
src/gateway/
  ├── server.ts & server-start.ts (A)
  ├── server-http.ts (A) [node:http, node:https]
  ├── server-http-upgrades.ts (A) [ws]
  ├── ws-compression.ts (A) [node:zlib]
  ├── server-runtime-state.ts (A)
  ├── server-methods.ts (A)
  │    ├── server-methods/core-handlers.ts (A)
  │    ├── server-methods/nodes.invoke.ts (A, B) -> routes to Windows Node
  │    └── server-methods/chat.ts (A, B) -> routes to Agent Runtime
  └── server/ws-connection/
       ├── connect-node-session.ts (A, D) -> handles Windows companion pairing
       └── authenticated-request-dispatch.ts (A)

src/agents/ (B)
  ├── embedded-agent-runner/
  │    ├── run-orchestrator.ts (B)
  │    └── run/attempt.ts (B)
  ├── context-engine/ (B)
  └── tool-execution/ (B)

src/config/ (A)
  ├── zod-schema.ts (A) [zod]
  ├── paths.ts (A)
  └── read-config.ts (A)

src/state/ & node-sqlite.mjs (A)
  ├── openclaw-state-db.ts (A) [node:sqlite]
  └── schema-migrations.ts (A)

src/node-host/ (A, D)
  ├── runner.ts (A, D)
  ├── connection.ts (A, D)
  └── windows-spawn.ts (D)

src/llm/ & src/provider-runtime/ (B, E)
  ├── anthropic-provider.ts (B, E)
  ├── openai-provider.ts (B, E)
  ├── gemini-provider.ts (B, E)
  └── ollama-provider.ts (B, E)

src/channels/ (C)
  ├── discord/ (C)
  ├── slack/ (C)
  ├── telegram/ (C)
  └── whatsapp/ (C)
```

---

## 4. Production Runtime Dependencies (`package.json`)

Key npm packages required in production:
- **`ws`**: WebSocket server and client implementation.
- **`zod`**: Configuration and message schema validation.
- **`commander`**: Command line parsing.
- **`chalk`** / **`picocolors`**: Terminal output formatting.
- **`kysely`**: Type-safe SQL query builder.
- Built-in Node modules: `node:http`, `node:https`, `node:sqlite`, `node:crypto`, `node:fs`, `node:path`, `node:child_process`, `node:os`.

---

## 5. Standalone Runtime Packaging Strategy

To deliver Spark as a self-contained Windows desktop application:
1. **Source & Distribution**: The complete compiled output of OpenClaw Core (`dist/`, `package.json`, `openclaw.mjs`, support scripts) is packaged alongside the Spark application.
2. **Node.js Runtime**: Node.js 26.x (or 24.16+) x64 binary is bundled within Spark's installation directory (`runtimes/node/`).
3. **Execution**: The Spark C# Gateway Manager spawns `node.exe runtimes/gateway/openclaw.mjs gateway run` with appropriate flags and environment variables, binding to loopback (`127.0.0.1:18789`).
4. **No Global Dependencies**: End users do not need Node.js, pnpm, Python, or WSL installed.
