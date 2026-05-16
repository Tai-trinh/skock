using System;
using System.Collections.Generic;

namespace Skock.Meta;

public sealed class AdmiralShipEffect
{
    public required Func<ShipDefData, bool> Matches { get; init; }
    public required Action<ShipDefData> Apply { get; init; }
}

public sealed class Admiral
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string FactionId { get; init; }
    public required string BonusText { get; init; }
    public required int StartingSalvage { get; init; }
    public required int StartingTech { get; init; }
    public required int StartingHangarCapacity { get; init; }
    public required FleetJsonData StartingFleet { get; init; }
    public required IReadOnlyList<AdmiralShipEffect> ShipEffects { get; init; }
}
