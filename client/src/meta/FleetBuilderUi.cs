using Godot;

namespace Skock.Meta;

public partial class FleetBuilderUi : Control
{
    private Label _resourcesLabel = null!;
    private VBoxContainer _fleetContainer = null!;
    private VBoxContainer _shopContainer = null!;
    private Label _statusLabel = null!;

    public override void _Ready()
    {
        _resourcesLabel = GetNode<Label>("MarginContainer/VBox/Header/ResourcesLabel");
        _fleetContainer = GetNode<VBoxContainer>("MarginContainer/VBox/Main/FleetPanel/FleetScroll/FleetContainer");
        _shopContainer = GetNode<VBoxContainer>("MarginContainer/VBox/Main/ShopPanel/ShopScroll/ShopContainer");
        _statusLabel = GetNode<Label>("MarginContainer/VBox/Footer/StatusLabel");
        GetNode<Button>("MarginContainer/VBox/Footer/BattleButton").Pressed += OnBattlePressed;
        Refresh();
    }

    private void Refresh()
    {
        var run = RunState.Instance;
        _resourcesLabel.Text =
            $"Salvage: {run.Salvage}   Tech: {run.Tech}   " +
            $"Tonnage: {run.UsedTonnage} / {run.HangarCapacity}   " +
            $"Jump: {run.JumpNumber} / 8   Losses: {run.LossCount} / 3";

        foreach (var child in _fleetContainer.GetChildren())
            child.QueueFree();

        for (var i = 0; i < run.Fleet.Ships.Count; i++)
        {
            var ship = run.Fleet.Ships[i];
            var index = i;
            var yield = ship.HullClass.Tonnage() * 3;
            var btn = new Button
            {
                Text = $"{ShipDisplay.NameFor(ship)}  [+{yield} salvage]",
            };
            btn.Pressed += () => SalvageShip(index);
            _fleetContainer.AddChild(btn);
        }

        foreach (var child in _shopContainer.GetChildren())
            child.QueueFree();

        foreach (var bp in BlueprintCatalog.All)
        {
            var canAfford = run.Salvage >= bp.SalvageCost;
            var fits = run.FreeTonnage >= bp.Tonnage;
            var btn = new Button
            {
                Text = $"{bp.DisplayName}  [{bp.Tonnage}T  {bp.SalvageCost} salvage]",
                Disabled = !canAfford || !fits,
            };
            var captured = bp;
            btn.Pressed += () => BuyShip(captured);
            _shopContainer.AddChild(btn);
        }
    }

    private void SalvageShip(int index)
    {
        var run = RunState.Instance;
        if (index >= run.Fleet.Ships.Count)
            return;
        var ship = run.Fleet.Ships[index];
        var yield = ship.HullClass.Tonnage() * 3;
        run.Fleet.Ships.RemoveAt(index);
        run.Salvage += yield;
        _statusLabel.Text = $"Salvaged {ShipDisplay.NameFor(ship)} for {yield} salvage.";
        Refresh();
    }

    private void BuyShip(Blueprint bp)
    {
        var run = RunState.Instance;
        if (run.Salvage < bp.SalvageCost)
            return;
        if (run.FreeTonnage < bp.Tonnage)
        {
            _statusLabel.Text = "Not enough hangar space.";
            return;
        }
        run.Fleet.Ships.Add(bp.Instantiate());
        run.Salvage -= bp.SalvageCost;
        _statusLabel.Text = $"Built {bp.DisplayName}.";
        Refresh();
    }

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
