using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Skock.Meta;

public partial class DockUi : Control
{
    private Label _titleLabel = null!;
    private Label _resourcesLabel = null!;
    private VBoxContainer _fleetContainer = null!;
    private VBoxContainer _dockContainer = null!;
    private Label _statusLabel = null!;

    private const int RerollCost = 5;

    // TODO: tweak slot counts, tonnage tiers, and costs once the loop is playtested.
    private static readonly (string Label, HullClass[] Classes, int Slots)[] Tiers =
    [
        ("Corvette / Fighter",  [HullClass.Corvette],                            5),
        ("Frigate",             [HullClass.Frigate],                             3),
        ("Destroyer / Cruiser", [HullClass.Destroyer, HullClass.Cruiser],        2),
        ("Capital",             [HullClass.Battlecruiser, HullClass.Dreadnought],1),
    ];

    public override void _Ready()
    {
        _titleLabel     = GetNode<Label>("MarginContainer/VBox/Header/TitleLabel");
        _resourcesLabel = GetNode<Label>("MarginContainer/VBox/Header/ResourcesLabel");
        _fleetContainer = GetNode<VBoxContainer>("MarginContainer/VBox/Main/FleetPanel/FleetScroll/FleetContainer");
        _dockContainer  = GetNode<VBoxContainer>("MarginContainer/VBox/Main/DockPanel/DockScroll/DockContainer");
        _statusLabel    = GetNode<Label>("MarginContainer/VBox/Footer/StatusLabel");
        GetNode<Button>("MarginContainer/VBox/Footer/BattleButton").Pressed += OnBattlePressed;
        Refresh();
    }

    // ── Full rebuild ──────────────────────────────────────────────────────────

    private void Refresh()
    {
        var run = RunState.Instance;
        _titleLabel.Text = $"DOCKYARD — Jump {run.JumpNumber} / 8";
        _resourcesLabel.Text =
            $"Salvage: {run.Salvage}   Tech: {run.Tech}   " +
            $"Tonnage: {run.UsedTonnage} / {run.HangarCapacity}   " +
            $"Losses: {run.LossCount} / 3";
        RebuildFleet();
        RebuildDock();
    }

    // ── Fleet panel ───────────────────────────────────────────────────────────

    private void RebuildFleet()
    {
        var run = RunState.Instance;
        foreach (var child in _fleetContainer.GetChildren())
            child.QueueFree();

        for (var i = 0; i < run.Fleet.Ships.Count; i++)
        {
            var ship  = run.Fleet.Ships[i];
            var index = i;
            var yield = ship.HullClass.Tonnage() * 3;
            var btn = new Button { Text = $"{ShipDisplay.NameFor(ship)}  [+{yield} salvage]" };
            btn.Pressed += () => OnSalvageShip(index);
            _fleetContainer.AddChild(btn);
        }
    }

    private void OnSalvageShip(int index)
    {
        var run      = RunState.Instance;
        var shipName = index < run.Fleet.Ships.Count ? ShipDisplay.NameFor(run.Fleet.Ships[index]) : "ship";
        var yield    = run.SalvageShip(index);
        if (yield < 0) return;
        _statusLabel.Text = $"Salvaged {shipName} for {yield} salvage.";
        Refresh();
    }

    // ── Dock panel ────────────────────────────────────────────────────────────

    private void RebuildDock()
    {
        foreach (var child in _dockContainer.GetChildren())
            child.QueueFree();

        for (var tierIndex = 0; tierIndex < Tiers.Length; tierIndex++)
        {
            var (label, classes, slots) = Tiers[tierIndex];
            var pool = BlueprintCatalog.All.Where(bp => classes.Contains(bp.Template.HullClass)).ToList();
            if (pool.Count == 0)
                continue;

            var offers = GenerateOffers(pool, slots, tierIndex);
            var run    = RunState.Instance;

            var header    = new HBoxContainer();
            var tierLabel = new Label
            {
                Text = $"── {label} ──",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            var rerollBtn    = new Button { Text = $"Reroll [{RerollCost} salvage]" };
            var capturedTier = tierIndex;
            rerollBtn.Pressed += () => OnRerollTier(capturedTier);
            header.AddChild(tierLabel);
            header.AddChild(rerollBtn);
            _dockContainer.AddChild(header);

            foreach (var bp in offers)
            {
                var btn = new Button
                {
                    Text     = $"{bp.DisplayName}  [{bp.Tonnage}T  {bp.SalvageCost} salvage]",
                    Disabled = run.Salvage < bp.SalvageCost || run.FreeTonnage < bp.Tonnage,
                };
                var captured = bp;
                btn.Pressed += () => OnCommissionShip(captured);
                _dockContainer.AddChild(btn);
            }
        }
    }

    private static List<Blueprint> GenerateOffers(List<Blueprint> pool, int slots, int tierIndex)
    {
        var run  = RunState.Instance;
        var rng  = new Random(run.JumpNumber * 1000 + tierIndex * 100 + run.TierRerolls[tierIndex]);
        var offers = new List<Blueprint>(slots);
        for (var i = 0; i < slots; i++)
            offers.Add(pool[rng.Next(pool.Count)]);
        return offers;
    }

    private void OnRerollTier(int tierIndex)
    {
        if (!RunState.Instance.RerollTier(tierIndex, RerollCost))
        {
            _statusLabel.Text = $"Need {RerollCost} salvage to reroll.";
            return;
        }
        _statusLabel.Text = "Dockyard updated.";
        Refresh();
    }

    private void OnCommissionShip(Blueprint bp)
    {
        if (!RunState.Instance.CommissionShip(bp))
            return;
        _statusLabel.Text = $"Commissioned {bp.DisplayName}.";
        Refresh();
    }

    // ── Battle ────────────────────────────────────────────────────────────────

    private void OnBattlePressed()
    {
        var run = RunState.Instance;
        if (run.Fleet.Ships.Count == 0)
        {
            _statusLabel.Text = "Need at least one ship to battle.";
            return;
        }
        run.Save();
        GetTree().ChangeSceneToFile("res://scenes/Battle.tscn");
    }
}
