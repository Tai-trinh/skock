using System.Collections.Generic;

namespace Skock.Meta;

// Statistics and fleet snapshots for one completed jump. Stored in RunState.JumpHistory.
// TODO: persist JumpHistory in player_state.json so RunEnd breakdown survives mid-run saves.
public sealed class JumpRecord
{
    public int JumpNumber { get; init; }
    public bool Won { get; init; }
    public uint DurationTicks { get; init; }

    // Kills keyed by hull class string ("Corvette", "Destroyer", …).
    public IReadOnlyDictionary<string, int> EnemiesKilledByHullClass { get; init; } =
        new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> OwnShipsLostByHullClass { get; init; } =
        new Dictionary<string, int>();

    public float DamageDealt { get; init; }
    public float DamageTaken { get; init; }

    // Fleet snapshots at battle time (base stats; admiral effects applied separately).
    public FleetJsonData PlayerFleetSnapshot { get; init; } = new();
    public FleetJsonData OpponentFleetSnapshot { get; init; } = new();

    // Upgrades purchased by the player at the time of this jump.
    public IReadOnlyDictionary<string, int> PlayerUpgrades { get; init; } =
        new Dictionary<string, int>();

    public string PlayerAdmiralId { get; init; } = "";
}
