using System.Collections.Generic;

namespace Skock.Meta;

// Code-defined stat effects per admiral ID.
// TODO (online/data-driven): replace with an interpreter for the shared effects vocabulary
// (CONTEXT.md — same format as doctrines/role_equipment) so effects can live in admirals.json.
public static class AdmiralEffectsRegistry
{
    private static readonly Dictionary<string, IReadOnlyList<AdmiralShipEffect>> _effects = new()
    {
        ["kira"] =
        [
            new AdmiralShipEffect
            {
                Matches = s => s.Role == Role.Fighter,
                Apply = s => s.Speed *= 1.15,
            },
        ],
        ["voss"] =
        [
            new AdmiralShipEffect
            {
                Matches = s => s.Role == Role.Artillery && s.Weapon is not null,
                Apply = s => s.Weapon!.Damage *= 1.10,
            },
        ],
        ["shen"] = [],
    };

    public static IReadOnlyList<AdmiralShipEffect> ForAdmiral(string id) =>
        _effects.TryGetValue(id, out var effects) ? effects : [];
}
