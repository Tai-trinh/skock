using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Skock.Meta;
using Skock.Tests.Fakes;
using Xunit;

namespace Skock.Tests;

// Bridge tests for the dockyard binary ↔ C# upgrade catalog seam.
// Two layers:
//   Integration test — spawns the binary, verifies every served UpgradeEffect
//     can be deserialized and applied without throwing.
//   Unit tests (one per variant) — verify each ApplyEffect branch actually
//     changes run state, catching silent switch fall-throughs.
public sealed class DockCatalogBridgeTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string? FindDockBinaryPath()
    {
        // Assembly lives at client.tests/bin/Debug/net8.0/; go up 4 levels to repo root.
        var dir = Path.GetDirectoryName(typeof(DockCatalogBridgeTests).Assembly.Location)!;
        for (var i = 0; i < 4; i++)
            dir = Path.GetDirectoryName(dir)!;
        var path = Path.Combine(dir, "target", "release", "skock-dockyard.exe");
        return File.Exists(path) ? path : null;
    }

    private static DockSessionInput MinimalInput() =>
        new()
        {
            PlayerId = "offline:test",
            RunSeed = 0xDEAD_BEEF_CAFE_1234,
            JumpNumber = 1,
            TierRerolls = new int[4],
            ResearchRerolls = new int[4],
            Salvage = 9999,
            Tech = 9999,
            HangarUsed = 0,
            HangarCap = 9999,
            Fleet = [],
            UpgradePurchases = new(),
        };

    private static FakeRunData ArmedRun() =>
        new()
        {
            Salvage = 50,
            HangarCapacity = 10,
            Fleet = new FleetJsonData
            {
                Mothership = new ShipDefData
                {
                    IsMothership = true,
                    MaxHp = 500,
                    Hp = 500,
                    Weapon = new WeaponDefData { Damage = 20, Range = 100, CooldownTicks = 30 },
                },
                Ships =
                [
                    new ShipDefData
                    {
                        MaxHp = 100,
                        Hp = 100,
                        Speed = 5,
                        Armor = 0,
                        Weapon = new WeaponDefData { Damage = 10, Range = 80, CooldownTicks = 30 },
                    },
                ],
            },
        };

    // ── Integration test ──────────────────────────────────────────────────────

    [Fact]
    public async Task AllServedEffects_CanBeDeserializedAndApplied()
    {
        var binaryPath = FindDockBinaryPath();
        if (binaryPath is null)
            return; // skip: release binary not built

        await using var session = new LocalDockyardAdapter(binaryPath);
        var offers = await session.GetOffersAsync(MinimalInput());

        var allEffects = offers.ResearchTracks.SelectMany(t => t.Items).SelectMany(i => i.Effects).ToList();

        // Every effect must deserialize to a known concrete type (not null).
        Assert.NotEmpty(allEffects);
        Assert.All(allEffects, e => Assert.NotNull(e));

        // Every effect must apply without throwing.
        // An unknown Rust variant would have thrown during deserialization above;
        // a missing switch case in ApplyEffect would throw here (or silently no-op,
        // which the unit tests below catch).
        var run = ArmedRun();
        foreach (var effect in allEffects)
            ResearchCatalog.ApplyEffect(run, effect);
    }

    // ── Unit tests: every ApplyEffect branch changes observable state ─────────

    [Fact]
    public void HangarCap_IncreasesHangarCapacity()
    {
        var run = ArmedRun();
        ResearchCatalog.ApplyEffect(run, new HangarCapEffect { Delta = 4 });
        Assert.Equal(14, run.HangarCapacity);
    }

    [Fact]
    public void MothershipHp_IncreasesHpAndMaxHp()
    {
        var run = ArmedRun();
        ResearchCatalog.ApplyEffect(run, new MothershipHpEffect { Delta = 100 });
        Assert.Equal(600, run.Fleet.Mothership.MaxHp);
        Assert.Equal(600, run.Fleet.Mothership.Hp);
    }

    [Fact]
    public void MothershipWeaponDamage_IncreasesMothershipDamage()
    {
        var run = ArmedRun();
        ResearchCatalog.ApplyEffect(run, new MothershipWeaponDamageEffect { Delta = 5 });
        Assert.Equal(25, run.Fleet.Mothership.Weapon!.Damage);
    }

    [Fact]
    public void MothershipWeaponRange_IncreasesMothershipRange()
    {
        var run = ArmedRun();
        ResearchCatalog.ApplyEffect(run, new MothershipWeaponRangeEffect { Delta = 40 });
        Assert.Equal(140, run.Fleet.Mothership.Weapon!.Range);
    }

    [Fact]
    public void FleetHp_IncreasesHpAndMaxHpForAllFleetShips()
    {
        var run = ArmedRun();
        ResearchCatalog.ApplyEffect(run, new FleetHpEffect { Delta = 20 });
        Assert.Equal(120, run.Fleet.Ships[0].MaxHp);
        Assert.Equal(120, run.Fleet.Ships[0].Hp);
    }

    [Fact]
    public void FleetSpeed_IncreasesSpeedForAllFleetShips()
    {
        var run = ArmedRun();
        ResearchCatalog.ApplyEffect(run, new FleetSpeedEffect { Delta = 1 });
        Assert.Equal(6, run.Fleet.Ships[0].Speed);
    }

    [Fact]
    public void FleetWeaponDamage_IncreasesWeaponDamageForAllFleetShips()
    {
        var run = ArmedRun();
        ResearchCatalog.ApplyEffect(run, new FleetWeaponDamageEffect { Delta = 5 });
        Assert.Equal(15, run.Fleet.Ships[0].Weapon!.Damage);
    }

    [Fact]
    public void FleetWeaponRange_IncreasesWeaponRangeForAllFleetShips()
    {
        var run = ArmedRun();
        ResearchCatalog.ApplyEffect(run, new FleetWeaponRangeEffect { Delta = 20 });
        Assert.Equal(100, run.Fleet.Ships[0].Weapon!.Range);
    }

    [Fact]
    public void FleetWeaponCooldown_ReducesCooldownRespectingMin()
    {
        var run = ArmedRun();
        ResearchCatalog.ApplyEffect(run, new FleetWeaponCooldownEffect { Delta = -4, Min = 5 });
        Assert.Equal(26, run.Fleet.Ships[0].Weapon!.CooldownTicks);

        for (var i = 0; i < 10; i++)
            ResearchCatalog.ApplyEffect(run, new FleetWeaponCooldownEffect { Delta = -4, Min = 5 });
        Assert.Equal(5, run.Fleet.Ships[0].Weapon!.CooldownTicks);
    }

    [Fact]
    public void FleetArmor_IncreasesArmorRespectingMax()
    {
        var run = ArmedRun();
        ResearchCatalog.ApplyEffect(run, new FleetArmorEffect { Delta = 0.10, Max = 0.9 });
        Assert.Equal(0.10, run.Fleet.Ships[0].Armor, precision: 6);

        for (var i = 0; i < 10; i++)
            ResearchCatalog.ApplyEffect(run, new FleetArmorEffect { Delta = 0.10, Max = 0.9 });
        Assert.Equal(0.9, run.Fleet.Ships[0].Armor, precision: 6);
    }

    [Fact]
    public void Salvage_IncreasesSalvage()
    {
        var run = ArmedRun();
        ResearchCatalog.ApplyEffect(run, new SalvageEffect { Delta = 30 });
        Assert.Equal(80, run.Salvage);
    }
}
