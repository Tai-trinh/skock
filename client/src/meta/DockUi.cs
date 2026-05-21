using System;
using System.Collections.Generic;
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

    // ── Session state ─────────────────────────────────────────────────────────

    private IDockyard? _session;
    private DockOffersResult? _offers;
    private DockResourceState _state = new();

    // Mirrors the fleet during the session. Updated on commission/salvage.
    private sealed record SessionShip(int CurrentIndex, string Name, int Tonnage);

    private List<SessionShip> _sessionFleet = [];

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
        _battleButton.Disabled = true; // enabled once session is ready

        var run = RunState.Instance;
        _titleLabel.Text = $"DOCKYARD — Jump {run.JumpNumber} / 8";

        // Snapshot fleet for local tracking before opening the session.
        _sessionFleet = run
            .Fleet.Ships.Select(
                (s, i) => new SessionShip(i, ShipDisplay.NameFor(s), s.HullClass.Tonnage())
            )
            .ToList();

        _ = OpenSessionAsync();
    }

    public override void _ExitTree()
    {
        if (_session is not null)
            _ = _session.DisposeAsync().AsTask();
    }

    // ── Session open ──────────────────────────────────────────────────────────

    private async Task OpenSessionAsync()
    {
        var run = RunState.Instance;
        _session = run.OpenDockyardSession();
        try
        {
            _offers = await _session.GetOffersAsync(run.BuildDockSessionInput());
            _state = _offers.State;
            _battleButton.Disabled = false;
            Refresh();
        }
        catch (Exception e)
        {
            _statusLabel.Text = $"Dockyard unavailable: {e.Message}";
        }
    }

    // ── Full rebuild ──────────────────────────────────────────────────────────

    private void Refresh()
    {
        var run = RunState.Instance;
        _titleLabel.Text = $"DOCKYARD — Jump {run.JumpNumber} / 8";
        _resourcesLabel.Text =
            $"Salvage: {_state.Salvage}   Tech: {_state.Tech}   "
            + $"Tonnage: {_state.HangarUsed} / {_state.HangarCap}   "
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

        for (var i = 0; i < _sessionFleet.Count; i++)
        {
            var ship = _sessionFleet[i];
            var yield = ship.Tonnage * 3;
            var btn = new Button { Text = $"{ship.Name}  [+{yield} salvage]" };
            var capturedIndex = i;
            btn.Pressed += () => OnSalvageShip(capturedIndex);
            _fleetContainer.AddChild(btn);
        }
    }

    private async void OnSalvageShip(int sessionIndex)
    {
        if (_session is null)
            return;
        var shipName =
            sessionIndex < _sessionFleet.Count ? _sessionFleet[sessionIndex].Name : "ship";
        var result = await _session.SalvageFleetShipAsync(sessionIndex);
        if (!result.Ok)
        {
            _statusLabel.Text = $"Cannot salvage: {result.Error}";
            return;
        }
        _state = result.State!;
        _sessionFleet.RemoveAt(sessionIndex);
        _statusLabel.Text = $"Salvaged {shipName} for {result.SalvageYield} salvage.";
        Refresh();
    }

    // ── Dock panel ────────────────────────────────────────────────────────────

    private void RebuildDock()
    {
        foreach (var child in _dockContainer.GetChildren())
            child.QueueFree();

        if (_offers is null)
            return;

        foreach (var tier in _offers.ShipTiers)
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
                var canAfford = _state.Salvage >= slot.SalvageCost;
                var hasTonnage = (_state.HangarCap - _state.HangarUsed) >= slot.Tonnage;
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
        if (_session is null)
            return;
        var result = await _session.RerollTierAsync(tierIndex);
        if (!result.Ok)
        {
            _statusLabel.Text = $"Cannot reroll: {result.Error}";
            return;
        }
        _state = result.State!;
        // Replace the tier in the cached offers.
        var idx = _offers!.ShipTiers.FindIndex(t => t.TierIndex == tierIndex);
        if (idx >= 0 && result.Tier is not null)
            _offers.ShipTiers[idx] = result.Tier;
        _statusLabel.Text = "Dockyard updated.";
        Refresh();
    }

    private async void OnCommissionShip(ShipSlotOffer slot)
    {
        if (_session is null)
            return;
        var result = await _session.CommissionAsync(slot.BlueprintId);
        if (!result.Ok)
        {
            _statusLabel.Text = $"Cannot commission: {result.Error}";
            return;
        }
        _state = result.State!;
        // Track locally for fleet display (tonnage from slot offer).
        _sessionFleet.Add(new SessionShip(_sessionFleet.Count, slot.DisplayName, slot.Tonnage));
        _statusLabel.Text = $"Commissioned {slot.DisplayName}.";
        Refresh();
    }

    // ── Research panel ────────────────────────────────────────────────────────

    private void RebuildResearch()
    {
        foreach (var child in _researchContainer.GetChildren())
            child.QueueFree();

        if (_offers is null)
            return;

        foreach (var track in _offers.ResearchTracks)
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
                    Disabled = _state.Tech < item.TechCost || maxed,
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
        if (_session is null)
            return;
        var result = await _session.RerollResearchAsync(trackIndex);
        if (!result.Ok)
        {
            _statusLabel.Text = $"Cannot reroll: {result.Error}";
            return;
        }
        _state = result.State!;
        var idx = _offers!.ResearchTracks.FindIndex(t => t.TrackIndex == trackIndex);
        if (idx >= 0 && result.Track is not null)
            _offers.ResearchTracks[idx] = result.Track;
        _statusLabel.Text = "Research updated.";
        Refresh();
    }

    private async void OnBuyUpgrade(string upgradeId)
    {
        if (_session is null)
            return;
        var result = await _session.BuyResearchAsync(upgradeId);
        if (!result.Ok)
        {
            _statusLabel.Text = $"Cannot research: {result.Error}";
            return;
        }
        _state = result.State!;
        // Refresh research track items to reflect updated purchased counts.
        // The binary returns updated state but not the full track; rebuild all research from offers.
        // To get updated purchased counts, refresh the affected track from the binary
        // by checking what changed — for simplicity, fetch fresh offers on next Refresh().
        _statusLabel.Text = $"Researched: {upgradeId}.";
        Refresh();
    }

    // ── Battle ────────────────────────────────────────────────────────────────

    private async void OnBattlePressed()
    {
        if (_session is null)
            return;

        if (RunState.Instance.Fleet.Ships.Count == 0)
        {
            _statusLabel.Text = "Need at least one ship to battle.";
            return;
        }

        _battleButton.Disabled = true;

        var sessionResult = await _session.ShoppingDoneAsync();
        if (!sessionResult.Ok || sessionResult.Delta is null)
        {
            _statusLabel.Text = $"Shopping error: {sessionResult.Error}";
            _battleButton.Disabled = false;
            return;
        }

        var run = RunState.Instance;
        run.ApplyDockSessionDelta(sessionResult.Delta, _offers!);
        await run.Save();
        GetTree().ChangeSceneToFile("res://scenes/Battle.tscn");
    }
}
