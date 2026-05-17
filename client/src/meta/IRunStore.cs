using System.Threading.Tasks;

namespace Skock.Meta;

// Seam between in-memory run state (RunState) and its persistence/sync backend.
//
// Offline:  LocalRunStore  — reads/writes local JSON files.
// Online:   ServerRunStore — validates each action with the REST API before applying.
//
// To add online mode: implement this interface, assign it to RunState._store at startup.
public interface IRunStore
{
    // ── Lifecycle ─────────────────────────────────────────────────────────────

    Task Load(); // hydrate RunState from the store on startup
    Task Save(); // persist current RunState to the store
    Task DeleteSave(); // destroy persisted state (run abandoned)

    // ── Run start ─────────────────────────────────────────────────────────────

    Task StartRun(Admiral admiral);

    // ── Battle ────────────────────────────────────────────────────────────────

    Task<ulong> GetBattleSeed(); // Online: server assigns seed so anti-cheat re-sim matches exactly.

    // ── Dockyard actions ──────────────────────────────────────────────────────
    // Each method is the single seam for a server round-trip in online mode.
    // Online: POST action to server, await confirmation, then mutate RunState.
    // Offline: validate locally, mutate RunState, persist.

    Task<bool> CommissionShip(Blueprint bp); // false if precondition fails
    Task<int> SalvageShip(int index); // returns Salvage yield, or -1 on failure
    Task<bool> RerollTier(int tierIndex, int cost); // false if can't afford
    Task<bool> BuyUpgrade(string upgradeId); // false if can't afford or maxed
}
