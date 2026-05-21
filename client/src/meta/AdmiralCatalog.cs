namespace Skock.Meta;

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
}
