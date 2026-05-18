using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Skock.Sim;

namespace Skock.Meta;

public enum PostBattleTransition
{
    NextJump,
    Defeat,
    Victory,
}

public static class BattleOutcomeResolver
{
    public static async Task<PostBattleTransition> Resolve(
        IRunData run,
        BattleResult result,
        BattleInputs inputs,
        FleetJsonData playerSnapshot,
        FleetJsonData opponentSnapshot
    )
    {
        var playerWon = result.Winner == "fleet_a";

        await run.Stats.RecordBattle(
            new JumpRecord
            {
                JumpNumber = run.JumpNumber,
                Won = playerWon,
                DurationTicks = result.Ticks,
                EnemiesKilledByHullClass = CountByHullClass(result.FleetBKilled),
                OwnShipsLostByHullClass = CountByHullClass(result.FleetAKilled),
                DamageDealt = result.FleetADamageDealt,
                DamageTaken = result.FleetBDamageDealt,
                PlayerFleetSnapshot = playerSnapshot,
                OpponentFleetSnapshot = opponentSnapshot,
                PlayerUpgrades = new Dictionary<string, int>(run.UpgradePurchases),
                PlayerAdmiralId = run.AdmiralId,
            },
            inputs
        );

        if (!playerWon)
            run.LossCount++;

        // Salvage: flat base every battle + flat win bonus, both scaling with JumpNumber.
        // TODO (playtesting): tune multipliers.
        run.Salvage += run.JumpNumber * 10;
        if (playerWon)
            run.Salvage += run.JumpNumber * 15;

        // Tech on victory scales with JumpNumber: 1 (jumps 1–3), 2 (4–6), 3 (7–8).
        // TODO (playtesting): tune Tech scaling.
        if (playerWon)
            run.Tech += (run.JumpNumber + 2) / 3;

        ApplySurvivorHp(run, result.FleetASurvivors);
        HealAllShips(run);

        if (run.LossCount >= 3)
        {
            run.HasActiveRun = false;
            run.IsRunComplete = true;
            return PostBattleTransition.Defeat;
        }

        if (run.JumpNumber >= 8)
        {
            // TODO: check flawless run + top-10% score for hidden final encounter.
            run.Stats.RecordRunVictory();
            run.HasActiveRun = false;
            run.IsRunComplete = true;
            return PostBattleTransition.Victory;
        }

        Array.Fill(run.TierRerolls, 0);
        run.JumpNumber++;
        return PostBattleTransition.NextJump;
    }

    private static void ApplySurvivorHp(IRunData run, IReadOnlyList<ShipSurvivor> survivors)
    {
        foreach (var ship in run.Fleet.Ships)
            ship.Hp = 1.0;

        foreach (var survivor in survivors)
        {
            if (survivor.IsMothership)
            {
                run.Fleet.Mothership.Hp = survivor.Hp;
                continue;
            }
            if (survivor.FleetIndex is { } idx && idx >= 0 && idx < run.Fleet.Ships.Count)
                run.Fleet.Ships[idx].Hp = survivor.Hp;
        }
    }

    private static void HealAllShips(IRunData run)
    {
        run.Fleet.Mothership.Hp = run.Fleet.Mothership.MaxHp;
        foreach (var ship in run.Fleet.Ships)
            ship.Hp = ship.MaxHp;
    }

    private static Dictionary<string, int> CountByHullClass(IReadOnlyList<KilledShip> killed)
    {
        var counts = new Dictionary<string, int>();
        foreach (var ship in killed)
        {
            if (ship.IsMothership)
                continue;
            counts.TryGetValue(ship.HullClass, out var n);
            counts[ship.HullClass] = n + 1;
        }
        return counts;
    }
}
