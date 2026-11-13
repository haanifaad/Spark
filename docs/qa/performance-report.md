# Spark - Performance, Resource Utilization & Latency Benchmarks

## 1. Executive Summary & Hardware Benchmark Profile

This report profiles the performance, memory footprint, startup latencies, and token inference dynamics of Spark in a Windows 11 desktop environment.

### Test Bench Hardware Profile
- **Processor**: Intel x86_64 Multi-Core CPU
- **Physical Memory**: 16,384 MB (16 GB) DDR
- **Graphics Accelerator**: NVIDIA GeForce GT 730 (Legacy Kepler CC 3.5, Driver 391.35)
- **Inference Mode**: Local CPU Execution (`llamacpp` engine via Ollama)
- **Local Model**: Qwen 2.5 Coder 7B Instruct (Q4_K_M quantization, 4.7 GB disk image)
- **Storage**: High-speed NVMe/SATA SSD

---

## 2. Process Lifecycle & Startup Latency Profiling

Startup timings were measured across multiple cold and warm launches of the managed OpenClaw Gateway runtime.

| Phase | Duration | Cumulative Time | Resource State |
| :--- | :--- | :--- | :--- |
| **Node.js Process Spawn** | 380 ms | 0.38 s | Process initialized, RSS ~45 MB |
| **Module Import & Dependency Load** | 1,420 ms | 1.80 s | Base TypeScript modules resolved, RSS ~180 MB |
| **Plugin Discovery & Generation** | 10,500 ms | 12.30 s | Dynamic plugin artifacts loaded, RSS ~870 MB |
| **Database & Session Schema Prep** | 5,200 ms | 17.50 s | SQLite migrations & WAL inspection, RSS ~1,250 MB |
| **Maintenance & Channel Startup** | 4,650 ms | 22.15 s | Health endpoint `/health` live, RSS ~1,870 MB |

> [!NOTE]
> The cold-start duration of ~22 seconds is typical for the full-featured upstream OpenClaw Gateway loading all core plugins and channel transports. Once started, background worker tasks handle subsequent turn requests without repeating module initialization.

---

## 3. Memory Footprint Breakdown

The system memory footprint was measured under active turn inference:

```text
+-------------------------------------------------------------------------+
| SYSTEM MEMORY USAGE (16 GB TOTAL)                                       |
|                                                                         |
| [==============================] llama-server (Ollama)      6,420 MB     |
| [=========]                    OpenClaw Gateway Core       1,875 MB     |
| [===]                          WinUI 3 Tray Application      251 MB     |
| [=]                            FileWorker Sandbox Host        35 MB     |
| [=============]                Windows OS & System Cache   4,800 MB     |
| [====]                         Free Physical Memory        3,003 MB     |
+-------------------------------------------------------------------------+
```

### Component Resource Metrics
1. **Ollama / `llama-server` (24k Context Window)**:
   - Baseline (model weights in memory): ~4,650 MB
   - Allocated KV Cache (at `num_ctx: 24576`): ~1,770 MB
   - Total Working Set: ~6,420 MB
2. **OpenClaw Gateway Core (Node.js)**:
   - Working Set: ~1,875 MB RSS
   - Active Workers: 23 worker threads
   - Heap Allocation: ~391 MB (V8 heap)
3. **Desktop Native Applications (.NET 10)**:
   - `OpenClaw.Tray.WinUI.exe`: ~251 MB
   - `OpenClaw.FileWorker.exe`: ~35 MB

---

## 4. Inference Latencies & Token Processing

Prompt ingestion and token generation rates were evaluated using `qwen2.5-coder:7b` on CPU execution.

| Operation | Input / Output Size | Duration | Compute Rate | Notes |
| :--- | :--- | :--- | :--- | :--- |
| **Direct Prompt (Short)** | 12 tokens in / 10 tokens out | 1.8 s | ~5.5 tok/s | Fast response for brief queries |
| **Full Agent Prompt (14k Context)** | 14,038 tokens in / 60 tokens out | 145 s | ~97 prompt tok/s | Evaluated on CPU cores |
| **KV Cache Reuse Turn** | Incremental 20 tokens | 3.2 s | ~6.2 tok/s | Substantially faster when cache warm |

### Latency Optimization Observations
- **CPU vs GPU**: Because the local NVIDIA GT 730 has Compute Capability 3.5 (incompatible with modern CUDA 12 runtimes), inference runs on the CPU. A modern GPU (e.g. RTX 3060/4060 with CUDA acceleration) would accelerate prompt evaluation from ~140 seconds down to <1.5 seconds.
- **System Prompt Pruning**: For local desktop agents with limited hardware, configuring `plugins.entries` to disable unused remote messaging integrations (e.g. Slack, Discord, Telegram) reduces the initial system prompt from 14,000 tokens to under 3,000 tokens, speeding up CPU turnaround time by more than 4x.

---

## 5. Performance Recommendations for Spark Users

1. **Hardware Acceleration**: For real-time conversational speeds, users with modern NVIDIA or AMD GPUs should ensure proper GPU driver installations so Ollama automatically offloads layers to VRAM.
2. **Context Window Tuning**: Setting `contextWindow: 16384` or `20480` balances comprehensive tool definitions with efficient KV-cache allocation on 16 GB systems.
3. **Background Daemon Management**: Spark Gateway Manager should maintain the Gateway as a continuous local service, amortizing the one-time 22-second cold start across all daily user interactions.
