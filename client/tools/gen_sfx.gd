## Procedural placeholder SFX generator.
## Run once in the Godot editor: File → Run → Run Current Script (or via EditorScript).
## Commit the generated client/assets/sfx/*.wav files so CI can pick them up.
## Re-run whenever you want to tweak a sound; replace individual .wav files with real
## assets at any time — the C# audio pool loads by filename, so drops-in just work.
@tool
extends EditorScript

const SAMPLE_RATE := 44100
const CHANNELS := 1
const BIT_DEPTH := 16

const SFX_DIR := "res://assets/sfx"

func _run() -> void:
	var abs_dir := ProjectSettings.globalize_path(SFX_DIR)
	DirAccess.make_dir_recursive_absolute(abs_dir)

	_write("sfx_hitscan",             _gen_hitscan())
	_write("sfx_beam",                _gen_beam())
	_write("sfx_torpedo_launch",      _gen_torpedo_launch())
	_write("sfx_missile_launch",      _gen_missile_launch())
	_write("sfx_mine_deploy",         _gen_mine_deploy())
	_write("sfx_explosion_small",     _gen_explosion(0.25, 90.0))
	_write("sfx_explosion_large",     _gen_explosion(0.50, 55.0))
	_write("sfx_ship_destroyed",      _gen_explosion(0.45, 65.0))
	_write("sfx_mothership_destroyed",_gen_explosion(0.85, 38.0))

	print("[gen_sfx] Done — wrote 9 files to ", abs_dir)

# ── Synthesis functions ───────────────────────────────────────────────────────

# Short electronic zap: sine sweep 900→150 Hz with exponential decay.
func _gen_hitscan() -> PackedFloat32Array:
	var dur := 0.15
	var n := int(SAMPLE_RATE * dur)
	var s := PackedFloat32Array(); s.resize(n)
	for i in n:
		var t := float(i) / SAMPLE_RATE
		var freq := lerpf(900.0, 150.0, t / dur)
		var env := exp(-t * 22.0)
		s[i] = sin(2.0 * PI * freq * t) * env * 0.70
	return s

# Sustained hum with slow pitch wobble: models a beam weapon holding fire.
func _gen_beam() -> PackedFloat32Array:
	var dur := 0.40
	var n := int(SAMPLE_RATE * dur)
	var s := PackedFloat32Array(); s.resize(n)
	var phase := 0.0
	for i in n:
		var t := float(i) / SAMPLE_RATE
		var freq := 280.0 + sin(t * 7.0) * 18.0
		phase += freq / SAMPLE_RATE
		# Fast attack (30 ms), gradual release.
		var env := minf(t / 0.03, 1.0) * exp(-t * 3.5)
		s[i] = sin(phase * TAU) * env * 0.60
	return s

# Deep descending thump: low sine sweep 120→60 Hz for torpedo launch.
func _gen_torpedo_launch() -> PackedFloat32Array:
	var dur := 0.28
	var n := int(SAMPLE_RATE * dur)
	var s := PackedFloat32Array(); s.resize(n)
	for i in n:
		var t := float(i) / SAMPLE_RATE
		var freq := lerpf(120.0, 55.0, t / dur)
		var env := exp(-t * 9.0)
		s[i] = sin(TAU * freq * t) * env * 0.85
	return s

# Mid-frequency whoosh: low-pass filtered noise with swept cutoff.
func _gen_missile_launch() -> PackedFloat32Array:
	var dur := 0.24
	var n := int(SAMPLE_RATE * dur)
	var s := PackedFloat32Array(); s.resize(n)
	var prev := 0.0
	for i in n:
		var t := float(i) / SAMPLE_RATE
		var noise := randf() * 2.0 - 1.0
		var cutoff := lerpf(480.0, 140.0, t / dur)
		var alpha := 1.0 - exp(-TAU * cutoff / SAMPLE_RATE)
		prev = prev + alpha * (noise - prev)
		var env := exp(-t * 7.0) * minf(t / 0.012, 1.0)
		s[i] = prev * env * 0.68
	return s

# Metallic clunk: three inharmonic partials, very short decay.
func _gen_mine_deploy() -> PackedFloat32Array:
	var dur := 0.13
	var n := int(SAMPLE_RATE * dur)
	var s := PackedFloat32Array(); s.resize(n)
	for i in n:
		var t := float(i) / SAMPLE_RATE
		var env := exp(-t * 42.0)
		var sample := sin(TAU * 390.0 * t) * 0.50
		sample    += sin(TAU * 670.0 * t) * 0.30
		sample    += sin(TAU * 1090.0 * t) * 0.20
		s[i] = sample * env * 0.65
	return s

# Generic explosion: low-pass noise burst + low sine, envelope scaled by duration.
# base_freq controls the low-end weight — lower = bigger boom.
func _gen_explosion(dur: float, base_freq: float) -> PackedFloat32Array:
	var n := int(SAMPLE_RATE * dur)
	var s := PackedFloat32Array(); s.resize(n)
	var prev := 0.0
	for i in n:
		var t := float(i) / SAMPLE_RATE
		var noise := randf() * 2.0 - 1.0
		# Cutoff sweeps from base_freq down to 30% as explosion fades.
		var cutoff := lerpf(base_freq, base_freq * 0.30, t / dur)
		var alpha := 1.0 - exp(-TAU * cutoff / SAMPLE_RATE)
		prev = prev + alpha * (noise - prev)
		var tone := sin(TAU * (base_freq * 0.45) * t)
		# Mix: 30% tone, 70% filtered noise.
		var mixed := lerpf(tone, prev, 0.70)
		var env := exp(-t * 4.8 / dur) * minf(t / 0.005, 1.0)
		s[i] = mixed * env * 0.90
	return s

# ── WAV writer ────────────────────────────────────────────────────────────────

func _write(name: String, samples: PackedFloat32Array) -> void:
	var path := SFX_DIR + "/" + name + ".wav"
	var abs_path := ProjectSettings.globalize_path(path)
	var pcm := _to_pcm16(samples)

	var f := FileAccess.open(abs_path, FileAccess.WRITE)
	if not f:
		push_error("[gen_sfx] Cannot write %s (err %d)" % [abs_path, FileAccess.get_open_error()])
		return

	var data_size := pcm.size()
	var byte_rate := SAMPLE_RATE * CHANNELS * (BIT_DEPTH / 8)
	var block_align := CHANNELS * (BIT_DEPTH / 8)

	# RIFF / WAVE header
	f.store_buffer("RIFF".to_ascii_buffer())
	f.store_32(36 + data_size)
	f.store_buffer("WAVE".to_ascii_buffer())
	# fmt chunk
	f.store_buffer("fmt ".to_ascii_buffer())
	f.store_32(16)
	f.store_16(1)            # PCM
	f.store_16(CHANNELS)
	f.store_32(SAMPLE_RATE)
	f.store_32(byte_rate)
	f.store_16(block_align)
	f.store_16(BIT_DEPTH)
	# data chunk
	f.store_buffer("data".to_ascii_buffer())
	f.store_32(data_size)
	f.store_buffer(pcm)
	f.close()

	print("[gen_sfx]  wrote ", path, "  (", samples.size(), " samples)")

func _to_pcm16(samples: PackedFloat32Array) -> PackedByteArray:
	var out := PackedByteArray()
	out.resize(samples.size() * 2)
	for i in samples.size():
		var pcm := int(clampf(samples[i], -1.0, 1.0) * 32767.0)
		out[i * 2]     = pcm & 0xFF
		out[i * 2 + 1] = (pcm >> 8) & 0xFF
	return out
