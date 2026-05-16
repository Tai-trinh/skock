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

    // ── Scene children ────────────────────────────────────────────────────────

    private Camera2D _camera = null!;
    private Node2D _shipsContainer = null!;
    private Label _debugLabel = null!;
    private Label _resultLabel = null!;
    private DebugOverlay _debugOverlay = null!;

    // ── State ─────────────────────────────────────────────────────────────────

    private PlaybackState? _playback;
    private readonly Dictionary<uint, ShipNode> _shipNodes = [];
    private Control? _inspectorOverlay;
    private ConfirmationDialog _abandonConfirm = null!;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    // Sim world → Godot world: 1 sim unit = SimScale pixels, y-axis flipped.
    private const float SimScale = 1f;

    // ── Godot lifecycle ───────────────────────────────────────────────────────

    public override void _Ready()
    {
        _camera = GetNode<Camera2D>("Camera2D");
        _shipsContainer = GetNode<Node2D>("Ships");
        _debugLabel = GetNode<Label>("DebugUI/DebugLabel");
        _resultLabel = GetNode<Label>("DebugUI/ResultLabel");
        _debugOverlay = GetNode<DebugOverlay>("DebugOverlay");

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

        var run = RunState.Instance;
        run.IsBattleActive = true;
        var fleetA = File.Exists(run.PlayerFleetPath) ? run.PlayerFleetPath : run.FallbackFleetPath;
        var opponentPath = run.GetOpponentFleetPath();
        var fleetBPath = File.Exists(opponentPath)
            ? opponentPath
            : Path.GetFullPath(
                Path.Combine(run.ProjectDir, "..", "sim", "test_data", "fleet_b.json")
            );

        // Load opponent fleet for the Fleet Inspector and JumpRecord snapshot.
        try
        {
            var fleetBJson = File.ReadAllText(fleetBPath);
            run.CurrentOpponentFleet = JsonSerializer.Deserialize<FleetJsonData>(
                fleetBJson,
                JsonOptions
            );
            if (run.CurrentOpponentFleet is not null)
                run.CurrentOpponentFleet.Mothership.IsMothership = true;
        }
        catch
        { /* fleet inspector shows empty if file unreadable */
        }

        var seed = run.GetBattleSeed();
        run.CurrentBattleInputs = new BattleInputs
        {
            Seed = seed,
            FleetAJson = File.ReadAllText(fleetA),
            FleetBJson = File.Exists(fleetBPath) ? File.ReadAllText(fleetBPath) : "",
        };
        Task.Run(() =>
        {
            try
            {
                LoadFromSimRun(seed, fleetA, fleetBPath, run.SimBinaryPath);
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

        HandleInput();
        _playback.Advance(delta, TickRate);
        Render();

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

    private void OnAbandonConfirmed()
    {
        RunState.Instance.IsBattleActive = false;
        RunState.Instance.AbandonCurrentRun();
        GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void LoadFromFile(string msgpackPath)
    {
        var bytes = Godot.FileAccess.GetFileAsBytes(msgpackPath);
        var log = BattleLogParser.Parse(bytes);
        var result = new BattleResult
        {
            Winner = "unknown",
            Ticks = (uint)log.Ticks.Length,
            Reason = "loaded_from_file",
        };
        Initialize(log, result);
    }

    public void LoadFromSimRun(
        ulong seed,
        string fleetAPath,
        string fleetBPath,
        string simBinPath,
        string? configPath = null
    )
    {
        var (logBytes, result) = SimRunner.Run(
            simBinPath,
            seed,
            fleetAPath,
            fleetBPath,
            configPath
        );
        var log = BattleLogParser.Parse(logBytes);
        Callable.From(() => Initialize(log, result)).CallDeferred();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void Initialize(BattleLog log, BattleResult result)
    {
        _playback = new PlaybackState(log, result);
        RebuildShipNodes(log);
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

        var snapshotsB = new Dictionary<uint, ShipSnapshot>(tickB.Ships.Length);
        foreach (var s in tickB.Ships)
            snapshotsB[s.Id] = s;

        var aliveIds = new HashSet<uint>(tickA.Ships.Length);
        foreach (var snap in tickA.Ships)
            aliveIds.Add(snap.Id);

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

        UpdateDebugLabel(tickA);
        _debugOverlay.UpdateFromSnapshot(tickA.Ships, 0);
    }

    private void HandleInput()
    {
        if (_playback is null)
            return;

        if (Input.IsActionJustPressed("ui_accept"))
        {
            if (_playback.IsFinished)
            {
                RunState.Instance.IsBattleActive = false;
                RunState.Instance.RecordBattleResult(_playback.Result ?? new BattleResult());
            }
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

        // Result line
        var resultText =
            result.Winner == "draw"
                ? "DRAW — time limit reached"
                : $"{result.Winner.ToUpperInvariant()} WINS  ({result.Ticks} ticks)";
        outer.AddChild(
            new Label { Text = resultText, HorizontalAlignment = HorizontalAlignment.Center }
        );

        // Stats line
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

        // Fleet panels side by side
        var fleetsRow = new HBoxContainer();
        fleetsRow.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        outer.AddChild(fleetsRow);

        var playerFleet = run.Fleet;
        fleetsRow.AddChild(FleetInspector.Build(playerFleet, run.UpgradePurchases, true));

        fleetsRow.AddChild(new VSeparator());

        var opponentFleet = run.CurrentOpponentFleet ?? new FleetJsonData();
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
