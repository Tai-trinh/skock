---
name: gen-ship
description: >
  Generate a ship sprite for Skock. Composes a prompt from hull class and faction, then
  delegates to /gen-image. Output saved to client/assets/sprites/{hull}_{faction}.png.
  Use when user says /gen-ship or asks to generate a ship sprite.
---

# gen-ship

## Usage

```
/gen-ship hull=<hull_class> faction=<letter> [inspo=<file>] [shape=<file>] [extra="..."]
```

**Examples:**
- `/gen-ship hull=corvette faction=a`
- `/gen-ship hull=dreadnought faction=a inspo=starleaf-02.gif extra="heavy armored plating, dark metal"`
- `/gen-ship hull=destroyer faction=a shape=starleaf-02.gif inspo=starleaf-02.gif`

## Hull class → prompt

Build prompt as: `"{base} {faction_aesthetic}, {extra}, strict top-down view from directly above, overhead orthographic perspective, no perspective distortion, flat top-down game sprite, white background"`

| Hull | Base prompt |
|---|---|
| `corvette` | small fast agile space fighter corvette, triangular shape, light hull |
| `frigate` | medium space frigate, diamond silhouette, balanced hull |
| `destroyer` | space destroyer, square profile, workhorse warship |
| `cruiser` | heavy space cruiser, wide rectangular hull, durable armor |
| `battlecruiser` | battlecruiser, elongated rectangle with forward-pointing prow, capital warship |
| `dreadnought` | massive space dreadnought, rectangular hull with forward prow and two swept wing fins, heavily armored |
| `mothership` | massive hexagonal mothership, command vessel, mobile base |

## Faction aesthetics

| Faction | Aesthetic |
|---|---|
| `a` | beige and blue geometric low-poly panels, clean angular surfaces |
| *(others TBD)* | — |

## Output path

`client/assets/sprites/{hull}_{faction}.png`

## Negative prompt

Always add to `-Negative`: `"side view, angled view, perspective view, 3/4 view, isometric, portrait, character, blurry, low quality, watermark, text, signature, border, frame"`

## Steps

1. Build the full prompt string from the table above + faction aesthetic + extra text.
2. Set output to `client/assets/sprites/{hull}_{faction}.png`.
3. Map args: `inspo` → `-StyleImage`, `shape` → `-ShapeImage` (both from `inspo/` folder).
4. Delegate to `/gen-image`.
