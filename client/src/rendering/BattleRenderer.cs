using System.Collections.Generic;
using System.Linq;
using Godot;
using Skock.Sim;

namespace Skock.Rendering;

/// <summary>
/// Root scene script for battle playback. Attach to the Battle scene root node.
///
/// Call LoadFromFile() or LoadFromSimRun() before the scene becomes visible.
/// The renderer interpolates ship positions between sim ticks at display framerate.
/// </summary>
public partial class BattleRenderer : Node2D
{
    // ── Exported paths (set in Godot editor or via code) ─────────────────────

    [Export]
    public string SimBinaryPath { get; set; } = "res://../../target/debug/skock-sim";

    [Export]
    public int TickRate { get; set; } = 30;

    // ── Scene children (assigned in _Ready) ──────────────────────────────────

    private Camera2D _camera = null!;
    private Node2D _shipsContainer = null!;
    private Label _debugLabel = null!;
    private Label _resultLabel = null!;

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

        FitCamera();
        _resultLabel.Visible = false;
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

    /// <summary>Load a battle log that was already written to disk (--debug flag).</summary>
    public void LoadFromFile(string msgpackPath)
    {
        var bytes = FileAccess.GetFileAsBytes(msgpackPath);
        var log = BattleLogParser.Parse(bytes);
        var result = new BattleResult { Winner = "unknown", Ticks = (uint)log.Ticks.Length, Reason = "loaded_from_file" };
        Initialize(log, result);
    }

    /// <summary>
    /// Spawns the sim binary, waits for it to finish, then loads the resulting log.
    /// Call from a background thread to avoid blocking the main thread.
    /// </summary>
    public void LoadFromSimRun(ulong seed, string fleetAPath, string fleetBPath, string? configPath = null)
    {
        var (logBytes, result) = SimRunner.Run(
            ProjectSettings.GlobalizePath(SimBinaryPath),
            seed, fleetAPath, fleetBPath, configPath
        );
        var log = BattleLogParser.Parse(logBytes);
        // Marshal back to main thread before touching Godot nodes.
        CallDeferred(MethodName.InitializeDeferred, log, result);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void Initialize(BattleLog log, BattleResult result)
    {
        _playback = new PlaybackState(log, result);
        RebuildShipNodes(log);
    }

    // Godot callable for cross-thread init (called via CallDeferred).
    private void InitializeDeferred(BattleLog log, BattleResult result) =>
        Initialize(log, result);

    private void RebuildShipNodes(BattleLog log)
    {
        foreach (var node in _shipNodes.Values)
            node.QueueFree();
        _shipNodes.Clear();

        if (log.Ticks.Length == 0)
            return;

        // Discover all ships from the first tick and build nodes for them.
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

        // Index snapshot B by id for quick lookup.
        var snapshotsB = new Dictionary<uint, ShipSnapshot>(tickB.Ships.Length);
        foreach (var s in tickB.Ships)
            snapshotsB[s.Id] = s;

        // Show/hide nodes based on which ships are alive this tick.
        var aliveIds = new HashSet<uint>(tickA.Ships.Length);
        foreach (var snap in tickA.Ships)
            aliveIds.Add(snap.Id);

        foreach (var (id, node) in _shipNodes)
            node.Visible = aliveIds.Contains(id);

        // Interpolate position and heading for each alive ship.
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
                posB = posA; // ship died by tick B — hold last position
                headingRad = snapA.HeadingRad;
            }

            node.ApplySnapshot(posA.Lerp(posB, t), headingRad, hpFrac);
        }

        UpdateDebugLabel(tickA);
    }

    private void HandleInput()
    {
        if (_playback is null)
            return;

        if (Input.IsActionJustPressed("ui_accept"))
            _playback.PlaybackSpeed = _playback.PlaybackSpeed == 1f ? 4f : 1f;
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
        _resultLabel.Text = result.Winner == "draw"
            ? "DRAW — time limit reached"
            : $"{result.Winner.ToUpperInvariant()} wins  ({result.Ticks} ticks)";
    }

    private void FitCamera()
    {
        // Fit the 1000×1000 sim battlefield into the viewport.
        var viewport = GetViewport().GetVisibleRect().Size;
        var zoom = Mathf.Min(viewport.X, viewport.Y) / 1000f * 0.9f;
        _camera.Zoom = new Vector2(zoom, zoom);
        _camera.Position = Vector2.Zero;
    }
}
