using System.Collections.Generic;
using System.Linq;
using Godot;
using Skock.Meta;

namespace Skock.UI;

// Builds a self-contained fleet inspection panel as a programmatic Control tree.
// TODO (art): replace placeholder portrait with real admiral portrait texture.
public static class FleetInspector
{
    private static readonly Color FleetAColor = new(0.35f, 0.55f, 1f);
    private static readonly Color FleetBColor = new(1f, 0.35f, 0.35f);
    private static readonly Color PortraitTextColor = Colors.White;

    public static Control Build(
        FleetJsonData fleet,
        IReadOnlyDictionary<string, int>? upgrades,
        bool isPlayerFleet
    )
    {
        var root = new PanelContainer();
        root.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var vbox = new VBoxContainer();
        root.AddChild(vbox);

        // ── Admiral header ─────────────────────────────────────────────────
        var header = new HBoxContainer();
        vbox.AddChild(header);

        var portrait = BuildPortrait(fleet.AdmiralId, isPlayerFleet);
        header.AddChild(portrait);

        var admiralInfo = new VBoxContainer();
        admiralInfo.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(admiralInfo);

        admiralInfo.AddChild(
            new Label { Text = RunState.Instance.Catalog.NameFor(fleet.AdmiralId) }
        );
        admiralInfo.AddChild(
            new Label
            {
                Text = $"Faction: {fleet.Faction}",
                Modulate = new Color(0.75f, 0.75f, 0.75f),
            }
        );

        if (isPlayerFleet)
        {
            var admiral = RunState.Instance.Catalog.FindAdmiral(fleet.AdmiralId);
            if (admiral is not null)
                admiralInfo.AddChild(
                    new Label
                    {
                        Text = admiral.BonusText,
                        Modulate = new Color(0.9f, 0.85f, 0.5f),
                        AutowrapMode = TextServer.AutowrapMode.Word,
                    }
                );
        }

        // ── Upgrades (player side only) ────────────────────────────────────
        if (isPlayerFleet && upgrades is { Count: > 0 })
        {
            vbox.AddChild(new HSeparator());
            vbox.AddChild(new Label { Text = "Upgrades" });
            foreach (var (id, count) in upgrades)
            {
                var upgrade = ResearchCatalog.All.FirstOrDefault(u => u.Id == id);
                var name = upgrade?.DisplayName ?? id;
                vbox.AddChild(new Label { Text = $"  {name} ×{count}" });
            }
        }

        // ── Ship roster ────────────────────────────────────────────────────
        vbox.AddChild(new HSeparator());

        AddShipRow(vbox, fleet.Mothership);
        foreach (var ship in fleet.Ships)
            AddShipRow(vbox, ship);

        return root;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Control BuildPortrait(string admiralId, bool isPlayerFleet)
    {
        var container = new Control();
        container.CustomMinimumSize = new Vector2(48, 48);

        var bg = new ColorRect
        {
            Color = isPlayerFleet ? FleetAColor : FleetBColor,
            Size = new Vector2(48, 48),
        };
        container.AddChild(bg);

        var name = RunState.Instance.Catalog.NameFor(admiralId);
        var initial = name.Length > 0 ? name[0].ToString() : "?";
        var label = new Label
        {
            Text = initial,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Size = new Vector2(48, 48),
            Modulate = PortraitTextColor,
        };
        container.AddChild(label);

        return container;
    }

    private static void AddShipRow(VBoxContainer parent, ShipDefData ship)
    {
        var tag = ship.IsMothership ? "[MS] " : "";
        var hpText = $"HP: {ship.Hp:0}/{ship.MaxHp:0}";
        var weaponText = ship.Weapon is { } w ? $"  {w.Type} {w.Damage:0}dmg {w.Range:0}rng" : "";
        parent.AddChild(
            new Label { Text = $"  {tag}{ship.Role} {ship.HullClass}  {hpText}{weaponText}" }
        );
    }
}
