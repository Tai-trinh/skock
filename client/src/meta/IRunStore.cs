using System.Threading.Tasks;

namespace Skock.Meta;

// Seam between in-memory run state (RunState) and its persistence/sync backend.
// Responsible for run lifecycle only. Dockyard-phase actions live in IDockyard.
//
// Offline:  LocalRunStore  — reads/writes local JSON files.
// Online:   ServerRunStore — validates each action with the REST API before applying.
//
// Swap point: RunState._Ready().
public interface IRunStore
{
    // ── Lifecycle ─────────────────────────────────────────────────────────────

    Task Load();
    Task Save();
    Task DeleteSave();

    // ── Run start ─────────────────────────────────────────────────────────────

    Task StartRun(Admiral admiral);

    // ── Battle ────────────────────────────────────────────────────────────────

    // Online: server assigns seed so anti-cheat re-sim matches exactly.
    Task<ulong> GetBattleSeed();
}
