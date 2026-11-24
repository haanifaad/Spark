# Root-cause analysis

## SPARK-001

Execution path:

`run-gateway-fixture.ps1` -> `dotnet run` of `OpenClaw.GatewayFixtureHost` -> build of test/production dependency graph -> `OpenClaw.Shared` compilation -> error CS0535 -> fixture host never starts -> no isolated SPARK window exists to operate.

The direct source evidence is decisive:

1. `ISparkApprovalManager` adds two events at lines 14-15.
2. `SparkApprovalManager` declares it implements that interface at line 16, but declares neither event.
3. Dependency injection registers that concrete type for the interface at `AppServiceRegistration.cs:123`.
4. The WinUI project references the shared project at `OpenClaw.Tray.WinUI.csproj:105`.

This is a compile-time contract break, not a Gateway, credentials, or UI automation failure. The correction must keep the interface and all implementations synchronized, and its behavior must be tested, not merely made to compile.

## SPARK-002 (unconfirmed)

State-mutating approval methods update the in-memory dictionary and call `SaveToDisk`, which catches all exceptions. They subsequently return success. On a subsequent process start, `LoadFromDisk` reads the stale previous file or no file. The likely underlying cause is an error-handling design that treats durable state write failure as non-observable. Evidence is insufficient to mark it reproducible until a safe filesystem-failure test runs.
