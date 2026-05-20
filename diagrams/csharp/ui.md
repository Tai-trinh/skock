# ui

```mermaid
classDiagram
    class DebugOverlay {
        +_Ready() void
        +_Input(@event) void
        +UpdateFromSnapshot(ships, selectedId) void
    }
    class FleetInspector {
        +Build(fleet, IReadOnlyDictionary<string, upgrades, isPlayerFleet) Control
    }
    DebugOverlay --|> CanvasLayer
```
