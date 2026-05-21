using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace Skock.Meta;

public partial class DockUi : Control
{
    private Label _titleLabel = null!;
    private Label _resourcesLabel = null!;
    private VBoxContainer _fleetContainer = null!;
    private VBoxContainer _dockContainer = null!;
    private HBoxContainer _researchContainer = null!;
    private Label _statusLabel = null!;
    private Button _battleButton = null!;

    private DockSessionModel? _model;

    // ── Godot lifecycle ───────────────────────────────────────────────────────

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("MarginContainer/VBox/Header/TitleLabel");
        _resourcesLabel = GetNode<Label>("MarginContainer/VBox/Header/ResourcesLabel");
        _fleetContainer = GetNode<VBoxContainer>(
            "MarginContainer/VBox/Main/FleetPanel/FleetScroll/FleetContainer"
        );
        _dockContainer = GetNode<VBoxContainer>(
            "MarginContainer/VBox/Main/DockPanel/DockScroll/DockContainer"
        );
        _researchContainer = GetNode<HBoxContainer>(
            "MarginContainer/VBox/ResearchSection/ResearchContainer"
        );
        _statusLabel = GetNode<Label>("MarginContainer/VBox/Footer/StatusLabel");
        _battleButton = GetNode<Button>("MarginContainer/VBox/Footer/BattleButton");
        _battleButton.Pressed += OnBattlePressed;
        _battleButton.Disabled = true;

        var run = RunState.Instance;
        _titleLabel.Text = $"DOCKYARD — Jump {run.JumpNumber} / 8";

        var initialFleet = run
            .Fleet.Ships.Select(
                (s, i) => new SessionShip(i, ShipDisplay.NameFor(s), s.HullClass.Tonnage())
            )
            .ToList();
        _model = new DockSessionModel(run.OpenDockyardSession(), initialFleet);

        _ = OpenSessionAsync();
    }

    public override void _ExitTree()
    {
        if (_model is not null)
            _ = _model.DisposeAsync().AsTask();
    }

    // ── Session open ──────────────────────────────────────────────────────────

    private async Task OpenSessionAsync()
    {
        var run = RunState.Instance;
        var error = await _model!.OpenAsync(run.BuildDockSessionInput());
        if (error is not null)
        {
            _statusLabel.Text = $"Dockyard unavailable: {error}";
            return;
        }
        _battleButton.Disabled = false;
        Refresh();
    }

    // ── Full rebuild ──────────────────────────────────────────────────────────

    private void Refresh()
    {
        var run = RunState.Instance;
        _titleLabel.Text = $"DOCKYARD — Jump {run.JumpNumber} / 8";
        _resourcesLabel.Text =
            $"Salvage: {_model!.ResourceState.Salvage}   Tech: {_model.ResourceState.Tech}   "
            + $"Tonnage: {_model.ResourceState.HangarUsed} / {_model.ResourceState.HangarCap}   "
            + $"Losses: {run.LossCount} / 3";
        RebuildFleet();
        RebuildDock();
        RebuildResearch();
    }

    // ── Fleet panel ───────────────────────────────────────────────────────────

    private void RebuildFleet()
    {
        foreach (var child in _fleetContainer.GetChildren())
            child.QueueFree();

        for (var i = 0; i < _model!.Fleet.Count; i++)
        {
            var ship = _model.Fleet[i];
            var yield = ship.Tonnage * 3;
            var btn = new Button { Text = $"{ship.Name}  [+{yield} salvage]" };
            var capturedIndex = i;
            btn.Pressed += () => OnSalvageShip(capturedIndex);
            _fleetContainer.AddChild(btn);
        }
    }

    private async void OnSalvageShip(int sessionIndex)
    {
        var shipName =
            sessionIndex < _model!.Fleet.Count ? _model.Fleet[sessionIndex].Name : "ship";
        var result = await _model.SalvageAsync(sessionIndex);
        if (!result.Ok)
        {
            _statusLabel.Text = $"Cannot salvage: {result.Error}";
            return;
        }
        _statusLabel.Text = $"Salvaged {shipName} for {result.SalvageYield} salvage.";
        Refresh();
    }

    // ── Dock panel ────────────────────────────────────────────────────────────

    private void RebuildDock()
    {
        foreach (var child in _dockContainer.GetChildren())
            child.QueueFree();

        if (_model!.Offers is null)
            return;

        foreach (var tier in _model.Offers.ShipTiers)
        {
            if (tier.Slots.Count == 0)
                continue;

            var header = new HBoxContainer();
            var tierLabel = new Label
            {
                Text = $"── {tier.Label} ──",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            var rerollBtn = new Button { Text = $"Reroll [{tier.RerollCost} salvage]" };
            var capturedIndex = tier.TierIndex;
            rerollBtn.Pressed += () => OnRerollTier(capturedIndex);
            header.AddChild(tierLabel);
            header.AddChild(rerollBtn);
            _dockContainer.AddChild(header);

            foreach (var slot in tier.Slots)
            {
                var canAfford = _model.ResourceState.Salvage >= slot.SalvageCost;
                var hasTonnage =
                    (_model.ResourceState.HangarCap - _model.ResourceState.HangarUsed)
                    >= slot.Tonnage;
                var btn = new Button
                {
                    Text = $"{slot.DisplayName}  [{slot.Tonnage}T  {slot.SalvageCost} salvage]",
                    Disabled = !canAfford || !hasTonnage,
                };
                var capturedSlot = slot;
                btn.Pressed += () => OnCommissionShip(capturedSlot);
                _dockContainer.AddChild(btn);
            }
        }
    }

    private async void OnRerollTier(int tierIndex)
    {
        var result = await _model!.RerollTierAsync(tierIndex);
        if (!result.Ok)
        {
            _statusLabel.Text = $"Cannot reroll: {result.Error}";
            return;
        }
        _statusLabel.Text = "Dockyard updated.";
        Refresh();
    }

    private async void OnCommissionShip(ShipSlotOffer slot)
    {
        var result = await _model!.CommissionAsync(slot.BlueprintId);
        if (!result.Ok)
        {
            _statusLabel.Text = $"Cannot commission: {result.Error}";
            return;
        }
        _statusLabel.Text = $"Commissioned {slot.DisplayName}.";
        Refresh();
    }

    // ── Research panel ────────────────────────────────────────────────────────

    private void RebuildResearch()
    {
        foreach (var child in _researchContainer.GetChildren())
            child.QueueFree();

        if (_model!.Offers is null)
            return;

        foreach (var track in _model.Offers.ResearchTracks)
        {
            if (track.Items.Count == 0)
                continue;

            var trackBox = new VBoxContainer { CustomMinimumSize = new Godot.Vector2(170, 0) };
            trackBox.AddChild(new Label { Text = track.Label });

            if (track.RerollCost.HasValue)
            {
                var rerollBtn = new Button { Text = $"Reroll [{track.RerollCost} Tech]" };
                var capturedIndex = track.TrackIndex;
                rerollBtn.Pressed += () => OnRerollResearch(capturedIndex);
                trackBox.AddChild(rerollBtn);
            }

            foreach (var item in track.Items)
            {
                var maxed = item.Purchased >= item.MaxPurchases;
                var countText = $" ({item.Purchased}/{item.MaxPurchases})";
                var btn = new Button
                {
                    Text =
                        $"{item.DisplayName}\n{item.Description}\n[{item.TechCost} Tech]{countText}"
                        + (maxed ? "\nMAXED" : ""),
                    Disabled = _model.ResourceState.Tech < item.TechCost || maxed,
                };
                var capturedId = item.UpgradeId;
                btn.Pressed += () => OnBuyUpgrade(capturedId);
                trackBox.AddChild(btn);
            }

            _researchContainer.AddChild(trackBox);
        }
    }

    private async void OnRerollResearch(int trackIndex)
    {
        var result = await _model!.RerollResearchAsync(trackIndex);
        if (!result.Ok)
        {
            _statusLabel.Text = $"Cannot reroll: {result.Error}";
            return;
        }
        _statusLabel.Text = "Research updated.";
        Refresh();
    }

    private async void OnBuyUpgrade(string upgradeId)
    {
        var result = await _model!.BuyResearchAsync(upgradeId);
        if (!result.Ok)
        {
            _statusLabel.Text = $"Cannot research: {result.Error}";
            return;
        }
        _statusLabel.Text = $"Researched: {upgradeId}.";
        Refresh();
    }

    // ── Battle ────────────────────────────────────────────────────────────────

    private async void OnBattlePressed()
    {
        if (_model?.Offers is null)
            return;

        if (RunState.Instance.Fleet.Ships.Count == 0)
        {
            _statusLabel.Text = "Need at least one ship to battle.";
            return;
        }

        _battleButton.Disabled = true;

        var sessionResult = await _model.ShoppingDoneAsync();
        if (!sessionResult.Ok || sessionResult.Delta is null)
        {
            _statusLabel.Text = $"Shopping error: {sessionResult.Error}";
            _battleButton.Disabled = false;
            return;
        }

        var run = RunState.Instance;
        run.ApplyDockSessionDelta(sessionResult.Delta, _model.Offers);
        await run.Save();
        GetTree().ChangeSceneToFile("res://scenes/Battle.tscn");
    }
}
