# Fix-priority roadmap

1. **P1: Restore buildability.** Reconcile `ISparkApprovalManager` and `SparkApprovalManager`; add event transition behavior and tests.
2. **P1: Make approval persistence truthful.** Change persistence failures from swallowed exceptions into a visible, recoverable result; add fault-injection tests.
3. Rebuild the WinUI executable from the repaired working tree.
4. Run the fixture-backed UI smoke and capture owned-window evidence.
5. Test all fixture-supported workflows before using a task-owned real Gateway for provider, MCP, node, and external-integration checks.
6. Perform performance and security tests only after stable controlled launch is available.

Dependency: every later step requires step 1.
