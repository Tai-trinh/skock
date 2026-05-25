---
name: calibrate-fleet
description: Reads ship sprite pixel dimensions from client/assets/sprites/, computes current world-space sizes from ScaleFor() in ShipNode.cs, shows a calibration table, then updates ScaleFor() and sim_config.json hull_hit_radii to match new target sizes. Use when ship sprites have been regenerated at different pixel dimensions, when ships look too big/small in battle, or when hit radii feel off after a /gen-ship run.
---

# calibrate-fleet

Syncs three things that must agree: sprite pixel size, `ScaleFor()` in ShipNode.cs, and `hull_hit_radii` in sim_config.json.

## Step 1 — Read current sprite dimensions

Use PowerShell (running in WSL) to get the pixel height of each hull sprite:

```bash
for f in client/assets/sprites/*_a.png; do
  hull=$(basename $f _a.png)
  dims=$(powershell.exe -Command "Add-Type -AssemblyName System.Drawing; \$img = [System.Drawing.Image]::FromFile('$(wslpath -w $f)'); Write-Host \"\$(\$img.Width)x\$(\$img.Height)\"; \$img.Dispose()" 2>/dev/null)
  echo "$hull: $dims"
done
```

## Step 2 — Read current scale factors

Parse the `ScaleFor()` switch in `client/src/rendering/ShipNode.cs`. Each arm has the form `target_world / sprite_px`. Compute `current_world = target_world` (the numerator).

## Step 3 — Show calibration table

Print a table:

| Hull | Sprite px (h) | Current world height | Hit radius |
|---|---|---|---|
| corvette | 256 | 30 | 15 |
| … | … | … | … |

Flag any hull where `sprite_px` doesn't match the denominator in `ScaleFor()` — this means the sprite was regenerated at a different size and the scale is stale.

## Step 4 — Ask for new target world heights

Ask the user to confirm or adjust each target. Recommended proportions (feel free to propose these as defaults):

| Hull | Suggested world height |
|---|---|
| Corvette | 30 |
| Frigate | 40 |
| Destroyer | 50 |
| Cruiser | 60 |
| Battlecruiser | 70 |
| Dreadnought | 80 |
| Mothership | 100 |

## Step 5 — Update ShipNode.cs

`ScaleFor()` is derived from two dictionaries in `ShipNode.cs`:

- **`SpriteHeightPx`** — sprite pixel heights. Update this when a sprite is regenerated at a new pixel size.
- **`HullHitRadius`** — hit radii in sim-units. Update this when the target world size changes (Step 6 sets `hull_hit_radii = radius`; the two must be identical).

`ScaleFor(hull)` computes `radius * 2 / px` automatically — no other edits needed once these two dicts are current.

## Step 6 — Update sim_config.json

Set `hull_hit_radii` to the same values as `HullHitRadius` in ShipNode.cs (i.e. `target_world / 2`):

```json
"hull_hit_radii": {
  "Corvette": 15.0,
  "Frigate": 20.0,
  ...
}
```

Keys use PascalCase to match Rust's `{:?}` debug format for `HullClass`.

## Step 7 — Type-check

```bash
dotnet.exe build client/skock.csproj
```

## Notes

- **Boid separation side-effect**: `min_dist = radius_a + radius_b + sep_margin`. Larger radii spread fleets out. After a big radius increase, watch the first battle to confirm formations still look right.
- **Portrait sprites**: use pixel *height* (not width) as the dominant dimension — ships are taller than they are wide.
- **Square sprites** (e.g. corvette, mothership): height == width, either works.
- **Future**: when normal-map sprites exist, this skill should also check for `{hull}_a_normal.png` alongside each sprite.
