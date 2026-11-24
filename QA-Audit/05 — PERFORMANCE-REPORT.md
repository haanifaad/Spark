# Performance report

No performance measurement is available. The only attempted controlled startup failed during compilation, before a SPARK process could be captured. Startup latency, render latency, navigation responsiveness, CPU, GPU, memory, API latency, and long-running-session stability are therefore **untested**.

Measurement method proposed after the build is repaired: use the repository fixture with a current owned process, record bounded startup/navigation scenarios, preserve capture coverage, and inspect GC/resource counts. Do not report GPU attribution or end-to-end provider latency from a UI-only trace.
