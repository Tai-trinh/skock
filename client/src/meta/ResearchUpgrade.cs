using System;
using System.Globalization;

namespace Skock.Meta;

public static class ResearchCatalog
{
    // Converts a snake_case upgrade ID to a Title Case label for historical display.
    public static string LabelFor(string id) =>
        CultureInfo.CurrentCulture.TextInfo.ToTitleCase(id.Replace('_', ' '));

    public static void ApplyEffect(IRunData run, UpgradeEffect effect)
    {
        switch (effect)
        {
            case HangarCapEffect e:
                run.HangarCapacity += e.Delta;
                break;
            case MothershipHpEffect e:
                run.Fleet.Mothership.MaxHp += e.Delta;
                run.Fleet.Mothership.Hp += e.Delta;
                break;
            case MothershipWeaponDamageEffect e:
                if (run.Fleet.Mothership.Weapon is not null)
                    run.Fleet.Mothership.Weapon.Damage += e.Delta;
                break;
            case MothershipWeaponRangeEffect e:
                if (run.Fleet.Mothership.Weapon is not null)
                    run.Fleet.Mothership.Weapon.Range += e.Delta;
                break;
            case FleetHpEffect e:
                foreach (var ship in run.Fleet.Ships)
                {
                    ship.MaxHp += e.Delta;
                    ship.Hp += e.Delta;
                }
                break;
            case FleetSpeedEffect e:
                foreach (var ship in run.Fleet.Ships)
                    ship.Speed += e.Delta;
                break;
            case FleetWeaponDamageEffect e:
                foreach (var ship in run.Fleet.Ships)
                    if (ship.Weapon is not null)
                        ship.Weapon.Damage += e.Delta;
                break;
            case FleetWeaponRangeEffect e:
                foreach (var ship in run.Fleet.Ships)
                    if (ship.Weapon is not null)
                        ship.Weapon.Range += e.Delta;
                break;
            case FleetWeaponCooldownEffect e:
                foreach (var ship in run.Fleet.Ships)
                    if (ship.Weapon is not null)
                        ship.Weapon.CooldownTicks = Math.Max(
                            e.Min,
                            ship.Weapon.CooldownTicks + e.Delta
                        );
                break;
            case FleetArmorEffect e:
                foreach (var ship in run.Fleet.Ships)
                    ship.Armor = Math.Min(e.Max, ship.Armor + e.Delta);
                break;
            case SalvageEffect e:
                run.Salvage += e.Delta;
                break;
        }
    }
}
