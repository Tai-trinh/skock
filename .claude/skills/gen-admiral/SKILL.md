---
name: gen-admiral
description: >
  Generate an admiral portrait for Skock. Composes a character bust prompt from the
  admiral name and faction, then delegates to /gen-image. Output saved to
  client/assets/portraits/admiral_{name}.png. Use when user says /gen-admiral or
  asks to generate an admiral portrait.
---

# gen-admiral

## Usage

```
/gen-admiral name=<admiral_name> faction=<letter> [inspo=<file>] [extra="..."]
```

**Examples:**
- `/gen-admiral name=kira faction=a`
- `/gen-admiral name=kira faction=a inspo=starleaf-02.gif extra="stern expression, battle-worn"`

## Prompt template

Build prompt as:
`"character portrait bust, sci-fi fleet admiral {name}, {faction_aesthetic}, head and shoulders, facing forward, {extra}, game portrait, detailed face"`

## Faction aesthetics

| Faction | Aesthetic |
|---|---|
| `a` | Gallforce fleet uniform, blue and beige military insignia, clean futuristic design |
| *(others TBD)* | — |

## Negative prompt

`"full body, blurry, low quality, watermark, text, signature, border, frame, multiple people"`

## Output path

`client/assets/portraits/admiral_{name}.png`

## Steps

1. Build the full prompt from the template above + faction aesthetic + extra.
2. Set output to `client/assets/portraits/admiral_{name}.png`.
3. Map `inspo` → `-StyleImage` (from `inspo/portraits/` folder).
4. **Invoke the `/gen-image` skill** — models are pre-selected (skip Step 2 of `/gen-image`), pass them directly:
   - `-Lora "GallForce_IllustriousV1.safetensors" -LoraStrength 0.8`
   - `-Vae "sdxl_vae.safetensors"` (ae.safetensors is Flux-only; SDXL models need sdxl_vae)
   - `-Background black` (default — override if user specifies a different color)
5. Follow Step 3 of `/gen-image` (print the summary block), then run.
