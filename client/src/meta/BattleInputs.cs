namespace Skock.Meta;

// The minimal inputs required to deterministically re-simulate a battle.
// Stored by the server for retroactive anti-cheat verification (see ADR-0005).
// Given the same seed and both fleet JSONs, the sim produces byte-identical output on any machine.
public sealed class BattleInputs
{
    public ulong Seed { get; init; }
    public string FleetAJson { get; init; } = "";
    public string FleetBJson { get; init; } = "";
}
