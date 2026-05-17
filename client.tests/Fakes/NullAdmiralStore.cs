using System.Collections.Generic;
using Skock.Meta;

namespace Skock.Tests.Fakes;

internal sealed class NullAdmiralStore : IAdmiralStore
{
    public void Load() { }
    public IReadOnlyList<Admiral> GetAdmirals() => [];
    public IReadOnlyList<Faction> GetFactions() => [];
    public Admiral? FindAdmiral(string id) => null;
    public Faction? FindFaction(string id) => null;
    public string NameFor(string admiralId) => admiralId;
}
