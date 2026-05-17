using System.Collections.Generic;
using System.Threading.Tasks;
using Skock.Meta;

namespace Skock.Tests.Fakes;

internal sealed class NullAdmiralStore : IAdmiralStore
{
    public Task Load() => Task.CompletedTask;
    public IReadOnlyList<Admiral> GetAdmirals() => [];
    public IReadOnlyList<Faction> GetFactions() => [];
    public Admiral? FindAdmiral(string id) => null;
    public Faction? FindFaction(string id) => null;
    public string NameFor(string admiralId) => admiralId;
}
