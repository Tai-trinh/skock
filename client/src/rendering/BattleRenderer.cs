using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        var run = RunState.Instance;
        run.IsBattleActive = true;
        var fleetA = File.Exists(run.PlayerFleetPath) ? run.PlayerFleetPath : run.FallbackFleetPath;
        var fleetB = Path.GetFullPath(
            Path.Combine(run.ProjectDir, "..", "sim", "test_data", "fleet_b.json")
        );

        var seed = run.GetBattleSeed();
        Task.Run(() =>
        {
            try
            {
                LoadFromSimRun(seed, fleetA, fleetB, run.SimBinaryPath);
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
            node.Init(snapshot.Id, snapshot.Fleet, snapshot.IsMothership);
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
        if (_playback?.Result is not { } result)
            return;

        _resultLabel.Visible = true;
        _resultLabel.Text =
            result.Winner == "draw"
                ? "DRAW — time limit reached\n\n[Space] Continue"
                : $"{result.Winner.ToUpperInvariant()} wins  ({result.Ticks} ticks)\n\n[Space] Continue";
    }

    private void FitCamera()
    {
        var viewport = GetViewport().GetVisibleRect().Size;
        var zoom = Mathf.Min(viewport.X, viewport.Y) / 1000f * 0.9f;
        _camera.Zoom = new Vector2(zoom, zoom);
        _camera.Position = Vector2.Zero;
    }
}
