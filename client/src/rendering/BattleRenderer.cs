using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using Skock.Meta;
using Skock.Sim;
using Skock.UI;

namespace Skock.Rendering;

public partial class BattleRenderer : Node2D
{
    [Export]
    public int TickRate { get; set; } = 30;

    // Sim world → Godot world: 1 sim unit = SimScale pixels, y-axis flipped.
    private const float SimScale = 1f;

    // ── Scene children ────────────────────────────────────────────────────────

    private Camera2D _camera = null!;
    private Node2D _shipsContainer = null!;
    private Node2D _projectilesContainer = null!;
    private Node2D _beamsContainer = null!;
    private Node2D _effectsContainer = null!;
    private Label _debugLabel = null!;
    private Label _resultLabel = null!;
    private DebugOverlay _debugOverlay = null!;

    // ── Seam ──────────────────────────────────────────────────────────────────

    /// Override before this node is added to the scene tree to substitute a different adapter
    /// (e.g. StoredLogSimRunner for debug replay). Defaults to LocalSimRunner when null.
    public ISimRunner? SimRunner { get; set; }

    // ── State ─────────────────────────────────────────────────────────────────

    private PlaybackState? _playback;
    private readonly Dictionary<uint, ShipNode> _shipNodes = [];
    private readonly Dictionary<uint, ProjectileNode> _projNodes = [];
    private readonly Dictionary<uint, BeamNode> _beamNodes = [];
    private int _lastEventTick = -1;
    private Control? _inspectorOverlay;
    private ConfirmationDialog _abandonConfirm = null!;
    private Task? _recordTask;
    private BattleEffects _effects = null!;

    // Mine rotation accumulator (per projectile id → accumulated rotation)
    private readonly Dictionary<uint, float> _mineRotation = [];

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    // ── Godot lifecycle ───────────────────────────────────────────────────────

    public override void _Ready()
    {
        _camera = GetNode<Camera2D>("Camera2D");
        _shipsContainer = GetNode<Node2D>("Ships");
        _debugLabel = GetNode<Label>("DebugUI/DebugLabel");
        _resultLabel = GetNode<Label>("DebugUI/ResultLabel");
        _debugOverlay = GetNode<DebugOverlay>("DebugOverlay");

        // Create containers for projectiles, beams, effects (rendered below ships)
        _beamsContainer = new Node2D();
        _projectilesContainer = new Node2D();
        _effectsContainer = new Node2D();
        AddChild(_beamsContainer);
        AddChild(_projectilesContainer);
        AddChild(_effectsContainer);
        // Move ships container to front
        MoveChild(_shipsContainer, -1);

        _effects = new BattleEffects();
        AddChild(_effects);
        _effects.Init(_camera);

        FitCamera();
        _resultLabel.Visible = false;
        _debugLabel.Text = "Loading sim...";

        _abandonConfirm = new ConfirmationDialog
        {
            Title = "Abandon Battle",
            DialogText = "Abandon this battle? Your run will be lost.",
        };
        _abandonConfirm.Confirmed += OnAbandonConfirmed;
        AddChild(_abandonConfirm);

        _ = InitBattleAsync();
    }

    private async Task InitBattleAsync()
    {
        var run = RunState.Instance;
        var fleetA = File.Exists(run.PlayerFleetPath) ? run.PlayerFleetPath : run.FallbackFleetPath;
        var opponentPath = run.GetOpponentFleetPath();
        var fleetBPath = File.Exists(opponentPath)
            ? opponentPath
            : Path.GetFullPath(
                Path.Combine(run.ProjectDir, "..", "sim", "test_data", "fleet_b.json")
            );

        FleetJsonData? opponentFleet = null;
        try
        {
            var fleetBJson = File.ReadAllText(fleetBPath);
            opponentFleet = JsonSerializer.Deserialize<FleetJsonData>(fleetBJson, JsonOptions);
            if (opponentFleet is not null)
                opponentFleet.Mothership.IsMothership = true;
        }
        catch
        { /* fleet inspector shows empty if file unreadable */
        }

        var seed = await run.GetBattleSeed();
        run.BeginBattle(
            new BattleInputs
            {
                Seed = seed,
                FleetAJson = File.ReadAllText(fleetA),
                FleetBJson = File.Exists(fleetBPath) ? File.ReadAllText(fleetBPath) : "",
            },
            opponentFleet
        );

        var simRunner = SimRunner ?? new LocalSimRunner(run.SimBinaryPath);
        _ = Task.Run(() =>
        {
            try
            {
                var (log, result) = simRunner.Run(seed, fleetA, fleetBPath);
                Callable.From(() => Initialize(log, result)).CallDeferred();
            }
            catch (Exception e)
            {
                GD.PrintErr($"[sim] {e.GetType().Name}: {e.Message}");
                Callable
                    .From(() => _debugLabel.Text = $"Error: {e.GetType().Name} (see Output panel)")
                    .CallDeferred();
            }
        });
    }

    public override void _Process(double delta)
    {
        if (_playback is null || !_playback.IsLoaded)
            return;
        if (_recordTask is not null)
            return;

        HandleInput();
        _playback.Advance(delta, TickRate);
        Render();
        MineRotationTick(delta);

        if (_playback.IsFinished)
            ShowResult();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
            return;
        if (key.Keycode != Key.Escape)
            return;
        if (_playback is null || _playback.IsFinished)
            return;

        _abandonConfirm.PopupCentered();
        GetViewport().SetInputAsHandled();
    }

    private async void OnAbandonConfirmed()
    {
        await RunState.Instance.AbandonCurrentRun();
        GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void Initialize(BattleLog log, BattleResult result)
    {
        _playback = new PlaybackState(log, result);
        RebuildShipNodes(log);
        _lastEventTick = -1;
    }

    private void RebuildShipNodes(BattleLog log)
    {
        foreach (var node in _shipNodes.Values)
            node.QueueFree();
        _shipNodes.Clear();

        if (log.Ticks.Length == 0)
            return;

        foreach (var snapshot in log.Ticks[0].Ships)
        {
            var node = new ShipNode();
            _shipsContainer.AddChild(node);
            node.Init(
                snapshot.Id,
                snapshot.Fleet,
                snapshot.IsMothership,
                snapshot.BlueprintDrawingId
            );
            _shipNodes[snapshot.Id] = node;
        }
    }

    private void Render()
    {
        if (_playback is null)
            return;

        var (tickA, tickB, t) = _playback.CurrentFrame();
        var currentTickIndex = (int)Math.Floor(_playback.Time);

        // Fire events for any ticks we've advanced past since last frame
        FirePendingEvents(currentTickIndex, tickA);

        RenderShips(tickA, tickB, t);
        RenderProjectiles(tickA, tickB, t);
        RenderBeams(tickA);
        UpdateDebugLabel(tickA);
        _debugOverlay.UpdateFromSnapshot(tickA.Ships, 0);
    }

    private void FirePendingEvents(int currentTickIndex, TickRecord currentTick)
    {
        if (currentTickIndex <= _lastEventTick)
            return;

        // Fire events for the current tick (and any skipped ticks we don't have direct access to)
        FireTickEvents(currentTick.Events);
        _lastEventTick = currentTickIndex;
    }

    private void FireTickEvents(LogEvent[] events)
    {
        foreach (var ev in events)
        {
            switch (ev.Type)
            {
                case "hitscan_fired":
                    SpawnHitscanEffect(ev);
                    SpawnHitscanImpactEffect(ev);
                    _effects.Play(BattleEffects.SfxHitscan);
                    break;
                case "hitscan_missed":
                    SpawnHitscanEffect(ev);
                    _effects.Play(BattleEffects.SfxHitscan);
                    break;
                case "projectile_explosion":
                    SpawnExplosionEffect(ev);
                    _effects.Play(
                        ev.RadiusUnits >= 20f
                            ? BattleEffects.SfxExplosionLarge
                            : BattleEffects.SfxExplosionSmall
                    );
                    break;
                case "ship_destroyed":
                    OnShipDestroyed(ev);
                    break;
            }
        }
    }

    private void OnShipDestroyed(LogEvent ev)
    {
        if (!_shipNodes.TryGetValue(ev.Id, out var node))
            return;

        var pos = node.Position;
        var magnitude = BattleEffects.ShakeMagnitudeFor(node.IsMothership, node.BlueprintDrawingId);

        // Visual: expanding debris ring scaled to hull size.
        var effect = new ShipDestroyedEffect();
        _effectsContainer.AddChild(effect);
        effect.Spawn(pos, magnitude * 8f, ev.Fleet);

        // Audio + shake.
        _effects.Play(
            node.IsMothership
                ? BattleEffects.SfxMothershipDestroyed
                : BattleEffects.SfxShipDestroyed
        );
        _effects.TriggerShake(magnitude);
    }

    private void SpawnHitscanImpactEffect(LogEvent ev)
    {
        var effect = new HitscanImpactEffect();
        _effectsContainer.AddChild(effect);
        effect.Spawn(new Vector2(ev.TargetWorldX, -ev.TargetWorldY) * SimScale, ev.Fleet);
    }

    private void SpawnHitscanEffect(LogEvent ev)
    {
        var effect = new HitscanEffect();
        _effectsContainer.AddChild(effect);
        effect.Spawn(
            new Vector2(ev.SourceWorldX, -ev.SourceWorldY),
            new Vector2(ev.TargetWorldX, -ev.TargetWorldY),
            ev.Fleet,
            SimScale
        );
    }

    private void SpawnExplosionEffect(LogEvent ev)
    {
        // Simple expanding circle that fades — reuse HitscanEffect container for now
        // TODO: dedicated ExplosionEffect node with expanding radius
        var pos = new Vector2(ev.ExplosionWorldX, -ev.ExplosionWorldY) * SimScale;
        var r = ev.RadiusUnits * SimScale;
        var effect = new ExplosionEffect();
        _effectsContainer.AddChild(effect);
        effect.Spawn(pos, r, ev.Fleet);
    }

    // ── Ship rendering ────────────────────────────────────────────────────────

    private void RenderShips(TickRecord tickA, TickRecord tickB, float t)
    {
        var snapshotsB = tickB.Ships.ToDictionary(s => s.Id);
        var aliveIds = tickA.Ships.Select(snap => snap.Id).ToHashSet();

        foreach (var (id, node) in _shipNodes)
            node.Visible = aliveIds.Contains(id);

        foreach (var snapA in tickA.Ships)
        {
            if (!_shipNodes.TryGetValue(snapA.Id, out var node))
                continue;

            var posA = new Vector2(snapA.WorldX, -snapA.WorldY) * SimScale;
            var hpFrac = snapA.HpFraction;
            float headingRad;
            Vector2 posB;

            if (snapshotsB.TryGetValue(snapA.Id, out var snapB))
            {
                posB = new Vector2(snapB.WorldX, -snapB.WorldY) * SimScale;
                headingRad = Mathf.LerpAngle(snapA.HeadingRad, snapB.HeadingRad, t);
            }
            else
            {
                posB = posA;
                headingRad = snapA.HeadingRad;
            }

            node.ApplySnapshot(posA.Lerp(posB, t), headingRad, hpFrac);
        }
    }

    // ── Projectile rendering ──────────────────────────────────────────────────

    private void RenderProjectiles(TickRecord tickA, TickRecord tickB, float t)
    {
        var projB = tickB.Projectiles.ToDictionary(p => p.Id);
        var liveIds = tickA.Projectiles.Select(p => p.Id).ToHashSet();

        // Create nodes for newly appearing projectiles and play their launch sound.
        foreach (var snap in tickA.Projectiles)
        {
            if (!_projNodes.ContainsKey(snap.Id))
            {
                var node = new ProjectileNode();
                _projectilesContainer.AddChild(node);
                node.Init(snap.Id, snap.Fleet, snap.Subtype);
                _projNodes[snap.Id] = node;

                var launchSfx = snap.Subtype switch
                {
                    ProjectileNode.SubtypeMissile => BattleEffects.SfxMissileLaunch,
                    ProjectileNode.SubtypeTorpedo => BattleEffects.SfxTorpedoLaunch,
                    ProjectileNode.SubtypeMine => BattleEffects.SfxMineDeploy,
                    _ => BattleEffects.SfxTorpedoLaunch,
                };
                _effects.Play(launchSfx);
            }
        }

        // Remove nodes for projectiles that have disappeared
        var toRemove = _projNodes.Keys.Where(id => !liveIds.Contains(id)).ToList();
        foreach (var id in toRemove)
        {
            _projNodes[id].QueueFree();
            _projNodes.Remove(id);
            _mineRotation.Remove(id);
        }

        // Update position/rotation for live projectiles
        foreach (var snapA in tickA.Projectiles)
        {
            if (!_projNodes.TryGetValue(snapA.Id, out var node))
                continue;

            var posA = new Vector2(snapA.WorldX, -snapA.WorldY) * SimScale;
            float heading;
            Vector2 pos;

            if (projB.TryGetValue(snapA.Id, out var snapBp))
            {
                pos = posA.Lerp(new Vector2(snapBp.WorldX, -snapBp.WorldY) * SimScale, t);
                heading = Mathf.LerpAngle(snapA.HeadingRad, snapBp.HeadingRad, t);
            }
            else
            {
                pos = posA;
                heading = snapA.HeadingRad;
            }

            // Mines rotate slowly — override heading with accumulated rotation
            if (snapA.Subtype == 2) // Mine
            {
                _mineRotation.TryGetValue(snapA.Id, out var rot);
                node.Rotation = rot;
                node.Position = pos;
            }
            else
            {
                node.ApplySnapshot(pos, heading);
            }
        }
    }

    private void MineRotationTick(double delta)
    {
        const float MineRotSpeed = 0.1f; // ~0.1 rad/frame at 30fps
        foreach (var id in _mineRotation.Keys.ToList())
        {
            _mineRotation[id] += MineRotSpeed * (float)delta * 30f;
        }
        // Register newly seen mines
        if (_playback is null)
            return;
        var (tickA, _, _) = _playback.CurrentFrame();
        foreach (var p in tickA.Projectiles)
        {
            if (p.Subtype == 2 && !_mineRotation.ContainsKey(p.Id))
                _mineRotation[p.Id] = 0f;
        }
    }

    // ── Beam rendering ────────────────────────────────────────────────────────

    private void RenderBeams(TickRecord tickA)
    {
        var liveIds = tickA.Beams.Select(b => b.Id).ToHashSet();

        // Create nodes for new beams and play the one-shot fire sound.
        foreach (var snap in tickA.Beams)
        {
            if (!_beamNodes.ContainsKey(snap.Id))
            {
                var node = new BeamNode();
                _beamsContainer.AddChild(node);
                node.Init(snap.Id, snap.Fleet);
                _beamNodes[snap.Id] = node;
                _effects.Play(BattleEffects.SfxBeam);
            }
        }

        // Remove ended beams
        var toRemove = _beamNodes.Keys.Where(id => !liveIds.Contains(id)).ToList();
        foreach (var id in toRemove)
        {
            _beamNodes[id].QueueFree();
            _beamNodes.Remove(id);
        }

        // Update active beams
        foreach (var snap in tickA.Beams)
        {
            if (_beamNodes.TryGetValue(snap.Id, out var node))
                node.ApplySnapshot(snap, SimScale);
        }
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    private void HandleInput()
    {
        if (_playback is null)
            return;

        if (Input.IsActionJustPressed("ui_accept"))
        {
            if (_playback.IsFinished)
                _recordTask = RunState.Instance.RecordBattleResult(
                    _playback.Result ?? new BattleResult()
                );
            else
                _playback.PlaybackSpeed = _playback.PlaybackSpeed == 1f ? 4f : 1f;
        }
    }

    private void UpdateDebugLabel(TickRecord tick)
    {
        if (_playback is null)
            return;

        var fleetA = tick.Ships.Count(s => s.Fleet == 0);
        var fleetB = tick.Ships.Count(s => s.Fleet == 1);
        _debugLabel.Text =
            $"Tick {tick.Tick} / {_playback.TotalTicks}  |  "
            + $"Speed {_playback.PlaybackSpeed}×  |  "
            + $"Fleet A: {fleetA} ships  Fleet B: {fleetB} ships  "
            + $"[Space] toggle speed";
    }

    private void ShowResult()
    {
        if (_playback?.Result is not { } result || _inspectorOverlay is not null)
            return;

        _resultLabel.Visible = false;

        var run = RunState.Instance;
        var overlay = new PanelContainer();
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var outer = new VBoxContainer();
        overlay.AddChild(outer);

        var resultText =
            result.Winner == "draw"
                ? "DRAW — time limit reached"
                : $"{result.Winner.ToUpperInvariant()} WINS  ({result.Ticks} ticks)";
        outer.AddChild(
            new Label { Text = resultText, HorizontalAlignment = HorizontalAlignment.Center }
        );

        var enemyKills = result.FleetBKilled.Count(k => !k.IsMothership);
        var ownLost = result.FleetAKilled.Count(k => !k.IsMothership);
        outer.AddChild(
            new Label
            {
                Text =
                    $"Enemies killed: {enemyKills}  |  Own ships lost: {ownLost}"
                    + $"  |  Damage dealt: {result.FleetADamageDealt:0}  |  Damage taken: {result.FleetBDamageDealt:0}",
                HorizontalAlignment = HorizontalAlignment.Center,
            }
        );

        outer.AddChild(new HSeparator());

        var fleetsRow = new HBoxContainer();
        fleetsRow.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        outer.AddChild(fleetsRow);

        var playerFleet = run.Fleet;
        fleetsRow.AddChild(FleetInspector.Build(playerFleet, run.UpgradePurchases, true));
        fleetsRow.AddChild(new VSeparator());
        var opponentFleet = RunState.Instance.CurrentOpponentFleet ?? new FleetJsonData();
        fleetsRow.AddChild(FleetInspector.Build(opponentFleet, null, false));

        outer.AddChild(new HSeparator());
        outer.AddChild(
            new Label
            {
                Text = "[Space] Continue",
                HorizontalAlignment = HorizontalAlignment.Center,
            }
        );

        AddChild(overlay);
        _inspectorOverlay = overlay;
    }

    private void FitCamera()
    {
        var viewport = GetViewport().GetVisibleRect().Size;
        var zoom = Mathf.Min(viewport.X, viewport.Y) / 1000f * 0.9f;
        _camera.Zoom = new Vector2(zoom, zoom);
        _camera.Position = Vector2.Zero;
    }
}
