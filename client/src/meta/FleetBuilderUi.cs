using Godot;

namespace Skock.Meta;

public partial class FleetBuilderUi : Control
{
    private Label _resourcesLabel = null!;
    private VBoxContainer _fleetContainer = null!;
    private VBoxContainer _shopContainer = null!;
    private Label _statusLabel = null!;

    private PlayerState _state = null!;
    private string _projectDir = "";

    public override void _Ready()
    {
        _resourcesLabel = GetNode<Label>("MarginContainer/VBox/Header/ResourcesLabel");
        _fleetContainer = GetNode<VBoxContainer>("MarginContainer/VBox/Main/FleetPanel/FleetScroll/FleetContainer");
        _shopContainer = GetNode<VBoxContainer>("MarginContainer/VBox/Main/ShopPanel/ShopScroll/ShopContainer");
        _statusLabel = GetNode<Label>("MarginContainer/VBox/Footer/StatusLabel");
        GetNode<Button>("MarginContainer/VBox/Footer/BattleButton").Pressed += OnBattlePressed;

        _projectDir = ProjectSettings.GlobalizePath("res://");
        _state = PlayerState.LoadOrDefault(_projectDir);
        Refresh();
    }

    private void Refresh()
    {
        _resourcesLabel.Text =
            $"Salvage: {_state.Salvage}   Tech: {_state.Tech}   " +
            $"Tonnage: {_state.UsedTonnage} / {_state.HangarCapacity}";

        foreach (var child in _fleetContainer.GetChildren())
            child.QueueFree();

        for (var i = 0; i < _state.Fleet.Ships.Count; i++)
        {
            var ship = _state.Fleet.Ships[i];
            var index = i;
            var yield = ShipDisplay.TonnageFor(ship.HullClass) * 3;
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
            var canAfford = _state.Salvage >= bp.SalvageCost;
            var fits = _state.FreeTonnage >= bp.Tonnage;
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
        if (index >= _state.Fleet.Ships.Count)
            return;
        var ship = _state.Fleet.Ships[index];
        var yield = ShipDisplay.TonnageFor(ship.HullClass) * 3;
        _state.Fleet.Ships.RemoveAt(index);
        _state.Salvage += yield;
        _statusLabel.Text = $"Salvaged {ShipDisplay.NameFor(ship)} for {yield} salvage.";
        Refresh();
    }

    private void BuyShip(Blueprint bp)
    {
        if (_state.Salvage < bp.SalvageCost)
            return;
        if (_state.FreeTonnage < bp.Tonnage)
        {
            _statusLabel.Text = "Not enough hangar space.";
            return;
        }
        _state.Fleet.Ships.Add(bp.Instantiate());
        _state.Salvage -= bp.SalvageCost;
        _statusLabel.Text = $"Built {bp.DisplayName}.";
        Refresh();
    }

    private void OnBattlePressed()
    {
        if (_state.Fleet.Ships.Count == 0)
        {
            _statusLabel.Text = "Need at least one ship to battle.";
            return;
        }
        _state.Save(_projectDir);
        GetTree().ChangeSceneToFile("res://scenes/Battle.tscn");
    }
}
