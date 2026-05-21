using System.Collections.Generic;
using Godot;
using Skock.Meta;

namespace Skock.Rendering;

/// <summary>
/// Manages battle audio pools and camera screen shake.
/// Add as a child of BattleRenderer; call Init() before first use.
/// </summary>
public partial class BattleEffects : Node
{
    // ── Screen shake config — programmer-adjustable ───────────────────────────

    /// Peak camera offset (pixels) when a Corvette is destroyed.
    private const float ShakeCorvette = 3.0f;
    private const float ShakeFrigate = 5.0f;
    private const float ShakeDestroyer = 8.0f;
    private const float ShakeCruiser = 12.0f;
    private const float ShakeBattlecruiser = 18.0f;
    private const float ShakeDreadnought = 26.0f;

    /// Peak camera offset (pixels) when the Mothership is destroyed.
    private const float ShakeMothership = 40.0f;

    /// Seconds for shake to fully decay after a hit.
    private const float ShakeDuration = 0.5f;

    /// Oscillation frequency in Hz — drives the camera side-to-side rhythm.
    private const float ShakeFreqX = 12.0f;

    /// Slightly different Y frequency for an organic, non-linear feel.
    private const float ShakeFreqY = 15.0f;

    // ── Audio config ──────────────────────────────────────────────────────────

    /// AudioStreamPlayers per sound slot — allows overlapping identical sounds.
    private const int PoolSize = 8;

    private const string SfxDir = "res://assets/sfx/";

    // ── Sound name constants — used by BattleRenderer ─────────────────────────

    public const string SfxHitscan = "sfx_hitscan";
    public const string SfxBeam = "sfx_beam";
    public const string SfxTorpedoLaunch = "sfx_torpedo_launch";
    public const string SfxMissileLaunch = "sfx_missile_launch";
    public const string SfxMineDeploy = "sfx_mine_deploy";
    public const string SfxExplosionSmall = "sfx_explosion_small";
    public const string SfxExplosionLarge = "sfx_explosion_large";
    public const string SfxShipDestroyed = "sfx_ship_destroyed";
    public const string SfxMothershipDestroyed = "sfx_mothership_destroyed";

    private static readonly string[] AllSounds =
    [
        SfxHitscan,
        SfxBeam,
        SfxTorpedoLaunch,
        SfxMissileLaunch,
        SfxMineDeploy,
        SfxExplosionSmall,
        SfxExplosionLarge,
        SfxShipDestroyed,
        SfxMothershipDestroyed,
    ];

    // ── State ─────────────────────────────────────────────────────────────────

    private readonly Dictionary<string, AudioStreamPlayer[]> _pools = [];
    private readonly Dictionary<string, int> _cursors = [];
    private Camera2D _camera = null!;
    private float _shakeT; // current shake magnitude in pixels; tweened to 0
    private Tween? _shakeTween;

    // ── Init ──────────────────────────────────────────────────────────────────

    public void Init(Camera2D camera)
    {
        _camera = camera;
        LoadPools();
    }

    private void LoadPools()
    {
        foreach (var name in AllSounds)
        {
            var path = SfxDir + name + ".wav";
            if (!ResourceLoader.Exists(path))
                continue; // placeholder not generated yet — silently skip

            var stream = ResourceLoader.Load<AudioStream>(path);
            var pool = new AudioStreamPlayer[PoolSize];
            for (var i = 0; i < PoolSize; i++)
            {
                var player = new AudioStreamPlayer { Stream = stream, Bus = "SFX" };
                AddChild(player);
                pool[i] = player;
            }
            _pools[name] = pool;
            _cursors[name] = 0;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// Plays a pooled sound by name. No-op if the .wav file was not loaded.
    public void Play(string name)
    {
        if (!_pools.TryGetValue(name, out var pool))
            return;

        var i = _cursors[name];
        pool[i].Stop();

        // TODO (tai): remove this when better sounds are found
        if (
            name == "sfx_beam"
            | name == "sfx_hitscan"
            | name == "sfx_torpedo_launch"
            | name == "sfx_missile_launch"
        )
            pool[i].VolumeDb = Mathf.LinearToDb(0.05f * RunState.Instance.Settings.SfxVolume);
        else
            pool[i].VolumeDb = Mathf.LinearToDb(2.0f * RunState.Instance.Settings.SfxVolume);
        pool[i].Play();
        _cursors[name] = (i + 1) % pool.Length;
    }

    /// Triggers screen shake with the magnitude appropriate for the given ship.
    /// Overlapping explosions accumulate — current shake is not reset below the new value.
    public void TriggerShake(float magnitude)
    {
        if (!RunState.Instance.Settings.ScreenShake)
            return;

        // Never reduce ongoing shake below what's already in progress.
        _shakeT = Mathf.Max(_shakeT, magnitude);

        _shakeTween?.Kill();
        _shakeTween = CreateTween().SetTrans(Tween.TransitionType.Sine);
        _shakeTween.TweenMethod(Callable.From<float>(v => _shakeT = v), _shakeT, 0f, ShakeDuration);
    }

    /// Returns the shake magnitude (pixels) for a destroyed ship based on its blueprint and
    /// whether it is the mothership. All shake constants live here so they are easy to tune.
    public static float ShakeMagnitudeFor(bool isMothership, string blueprintDrawingId)
    {
        if (isMothership)
            return ShakeMothership;

        return blueprintDrawingId switch
        {
            var s when s.StartsWith("frigate") => ShakeFrigate,
            var s when s.StartsWith("destroyer") => ShakeDestroyer,
            var s when s.StartsWith("cruiser") => ShakeCruiser,
            var s when s.StartsWith("battlecruiser") => ShakeBattlecruiser,
            var s when s.StartsWith("dreadnought") => ShakeDreadnought,
            _ => ShakeCorvette,
        };
    }

    // ── Godot lifecycle ───────────────────────────────────────────────────────

    public override void _Process(double delta)
    {
        if (_shakeT < 0.01f)
        {
            _camera.Offset = Vector2.Zero;
            _shakeT = 0f;
            return;
        }

        // Two slightly different sine frequencies give an organic 2D oscillation.
        var t = (float)Time.GetTicksMsec() / 1000f;
        _camera.Offset = new Vector2(
            Mathf.Sin(t * ShakeFreqX * Mathf.Tau) * _shakeT,
            Mathf.Sin(t * ShakeFreqY * Mathf.Tau) * _shakeT
        );
    }
}
