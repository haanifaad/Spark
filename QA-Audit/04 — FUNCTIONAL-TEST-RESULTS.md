# Functional test results

## Execution record

| ID | Area | Test | Result | Evidence |
|---|---|---|---|---|
| FT-001 | Startup / controlled UI | Launch the repository fixture against current Debug WinUI binary, synthetic read-only Gateway, isolated profile | Failed | `TEST-EVIDENCE/BUILD-FAILURE.txt` |
| FT-002 | Default local launch | Inspect and attempt repository launcher with isolated profile | Blocked as controlled proof | Default path prepared shared `npm-latest` Gateway; it is not a task-owned isolated Gateway |

## Untested due to FT-001

The following remain **untested**, not passed: dashboard; navigation; settings; theme; notifications; data persistence; workspace/chat; agent selection; task delegation; orchestration; memory; provider switching; streaming; retry; Ollama; external providers; browser/file/email/task tools; task scheduling/cancellation/retry; voice I/O/HUD; image/file input; accessibility; responsive layouts; crash recovery; cross-module workflows; live Gateway pairing; local MCP; background jobs; shutdown; long-session stability.

## Scope boundaries

No credentialed external provider, user file, user configuration, database reset, or destructive action was used. The existing visible SPARK window was not adopted because its process/profile ownership could not be established as this audit's isolated run.
