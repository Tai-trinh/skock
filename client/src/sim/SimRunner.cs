using System;
using System.Collections.Generic;

namespace Skock.Sim;

public sealed class ShipSurvivor
{
    public string BlueprintDrawingId { get; init; } = "";
    public float Hp { get; init; }
    public bool IsMothership { get; init; }
    public int? FleetIndex { get; init; }
}

public sealed class KilledShip
{
    public string HullClass { get; init; } = "";
    public bool IsMothership { get; init; }
}

public sealed class BattleResult
{
    public string Winner { get; init; } = "";
    public uint Ticks { get; init; }
    public string Reason { get; init; } = "";
    public IReadOnlyList<ShipSurvivor> FleetASurvivors { get; init; } = [];
    public IReadOnlyList<ShipSurvivor> FleetBSurvivors { get; init; } = [];
    public IReadOnlyList<KilledShip> FleetAKilled { get; init; } = [];
    public IReadOnlyList<KilledShip> FleetBKilled { get; init; } = [];
    public float FleetADamageDealt { get; init; }
    public float FleetBDamageDealt { get; init; }
}

public sealed class SimRunException(string message) : Exception(message);
