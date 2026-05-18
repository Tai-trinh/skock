# ADR-0008: C# test project layout — direct reference over client.core extraction

**Status:** Accepted

## Decision

The xUnit test project (`client.tests/`) references `client/skock.csproj` directly rather than extracting a shared `client.core/` library of pure C# classes.

## Alternatives considered

**Option A (chosen): direct `ProjectReference`**

`client.tests/skock.tests.csproj` references `client/skock.csproj`. The Godot SDK is a NuGet package and builds cleanly without the editor installed — CI already proves this. The `ScriptPathAttributeGenerator` warning is suppressed in the test project.

**Option B (deferred): extract `client.core/`**

Move all pure C# classes (`LocalRunStore`, `LocalAdmiralStore`, `LocalStatsStore`, `SimRunner`, data types, interfaces) to a new `client.core/` project with `Microsoft.NET.Sdk`. Both `client/` and `client.tests/` reference it. No Godot SDK in the test build.

## Why Option A now

1. Zero tests existed when this decision was made — the value of the clean split is unproven.
2. The pure C# classes are already isolated by convention (no `using Godot` imports). The separation exists in code; a separate project is the packaging of that separation, not the separation itself.
3. Moving ~15 files before writing a single test is speculative refactoring.

## When to revisit Option B

Extract `client.core/` when any of these becomes true:
- Godot SDK compile overhead measurably slows `dotnet test` to the point of frustration
- A transitive Godot dependency bleeds into a test and causes a build or runtime failure
- The test project references something that cannot compile without Godot types present
- The `client.core/` classes are needed by the server crate via a shared NuGet package

Until then, the `<!-- TODO -->` in `skock.tests.csproj` tracks the deferred work.

## IRunData as the DI seam

`RunState : Node, IRunData` exposes the mutable run data behind a plain interface. `LocalRunStore(IRunData run)` depends on the interface, not the Node. Test code uses `FakeRunData` — a hand-written implementation in `client.tests/Fakes/` with no mocking framework required. `ResearchUpgrade.Apply` is `Action<IRunData>` for the same reason.
