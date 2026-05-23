---
name: gen-image
description: >
  Generate a 512x512 transparent PNG via ComfyUI (SDXL + IP-Adapter + ControlNet).
  Submits a workflow to localhost:8188, polls for completion, removes background via rembg,
  saves to the specified output path. Use when /gen-ship or /gen-admiral delegate image
  generation, or when directly invoked to generate any game asset.
---

# gen-image

Thin wrapper around `scripts/gen_image.ps1`. Builds the PowerShell call, runs it, reports output.

## Step 1 — Pre-flight check

ComfyUI runs on Windows; Claude runs in WSL — check via powershell.exe:

```bash
powershell.exe -Command "try { Invoke-RestMethod http://localhost:8188/system_stats | Out-Null; Write-Host 'ComfyUI OK' } catch { Write-Host 'ComfyUI not running' }"
```

If not running, tell the user to run `make comfyui` first.

## Step 2 — Model selection (one question at a time)

**Unless the caller already specified all models, always do this step. Ask each question separately and wait for the answer before showing the next.**

Run `make comfyui-models` first to get the live model lists.

### Q1 — Base model type

List checkpoints first, then diffusion models, in a single continuously-numbered table with a Type column:

Ask:
> **Step 1/N — Base model.** Checkpoints bundle model + CLIP + VAE (easiest). Diffusion models are UNET-only (needs separate VAE + text encoders).
>
> | # | Type | Name |
> |---|---|---|
> | 1 | Checkpoint | 90sAnime77_7790sAnimeII.safetensors |
> | 2 | Checkpoint | sdXL_v10VAEFix.safetensors |
> | 3 | Diffusion | flux1-dev-F16.gguf |
> | 4 | Diffusion | NewBie-Image-Exp0.1-bf16.safetensors |

Wait for answer. Record the picked entry's Type — this controls which follow-up questions appear.

### Q2 — LoRA (always ask)

Ask:
> **Step 2/N — LoRA** (optional, type `0` to skip) — fine-tunes style on top of the base model:
> | # | Name |
> |---|---|
> | 0 | none |
> | 1 | ... |

Wait for answer.

### Q3 — VAE

- If **checkpoint**: ask with only SDXL-relevant options + default
- If **diffusion model**: ask with Flux-relevant options, note that ae.safetensors is required for Flux

Ask:
> **Step 3/N — VAE** — decodes latent image to pixels (wrong VAE = washed-out colours):
> | # | Name |
> |---|---|
> | 0 | default (baked into checkpoint) |   ← omit this row if diffusion model
> | 1 | sdxl_vae.safetensors — for SDXL checkpoints |   ← omit if diffusion model
> | 2 | ae.safetensors — required for Flux |

Wait for answer.

If the caller already passed explicit model choices, skip this step and proceed directly to the summary.

Text encoders always default to clip_l + t5xxl — do not ask.

## Step 3 — Print generation summary

**Print this block before invoking `gen_image.ps1`**:

```
=== gen-image ===
Checkpoint : <model name, or "auto-detect">
LoRA       : <lora name + strength, or "none">
VAE        : <vae name, or "default (baked-in)">

Prompt     : <full positive prompt>
Negative   : <negative prompt>

Size       : <WxH, e.g. 512x512>
Steps      : <n>   — more steps = more detail / slower
CFG        : <n>   — how strictly the model follows the prompt (7=balanced, 12=strict)
Seed       : <n or "random">

Style ref  : <inspo file, or "none"> — IP-Adapter: copies colour/style from this image (strength <n>)
Shape ref  : <inspo file, or "none"> — ControlNet Canny: copies silhouette/edges from this image (strength <n>)
Rembg      : <yes / no>   — removes background after generation
Background : <colour or "transparent">

Output     : <output path>
=================
```

Then proceed — no need to wait for user confirmation.

## Invocation

```bash
powershell.exe -ExecutionPolicy Bypass -File "$(wslpath -w $(pwd)/scripts/gen_image.ps1)" \
  -Prompt "PROMPT_HERE" \
  -Output "RELATIVE/OUTPUT/PATH.png" \
  [-StyleImage "inspo/filename.ext"] \
  [-ShapeImage "inspo/filename.ext"] \
  [-Negative "NEGATIVE_PROMPT"] \
  [-Size 512] \
  [-Steps 35] \
  [-Cfg 7.0] \
  [-IpAdapterStrength 0.6] \
  [-ControlNetStrength 0.7] \
  [-Seed 12345] \
  [-NoRembg] \
  [-Background "transparent"] \
  [-Model "checkpoint.safetensors"] \
  [-Vae "vae.safetensors"] \
  [-Lora "lora.safetensors"] \
  [-LoraStrength 0.8]
```

All paths are relative to repo root. `gen_image.ps1` converts them to Windows paths before calling Python.

## Parameters

| Param | Default | What it does |
|---|---|---|
| `-Prompt` | **required** | What to generate — describes content, style, composition |
| `-Negative` | generic | What to avoid — bad quality, wrong angles, unwanted elements |
| `-Output` | **required** | Save path, e.g. `client/assets/sprites/corvette_a.png` |
| `-Size` | `512` | Output resolution in pixels (square) |
| `-Steps` | `35` | Denoising iterations — more = sharper detail, slower generation |
| `-Cfg` | `7.0` | Classifier-free guidance — how strictly the prompt is followed (7 = balanced, 12 = very strict) |
| `-Seed` | random | Random seed — fix to reproduce an exact result |
| `-Model` | auto | Checkpoint filename from `models/checkpoints/` — auto-selects first available if omitted |
| `-Vae` | baked-in | VAE filename from `models/vae/` — controls colour fidelity; use `sdxl_vae.safetensors` for SDXL models |
| `-Lora` | none | LoRA filename from `models/loras/` — fine-tunes style/character on top of the checkpoint |
| `-LoraStrength` | `0.8` | How strongly the LoRA applies (0 = off, 1 = full, 1.2+ = exaggerated) |
| `-StyleImage` | none | IP-Adapter style reference from `inspo/` — transfers colour palette and aesthetic from this image |
| `-IpAdapterStrength` | `0.6` | How much the style image influences output (0 = ignore, 1 = strong) |
| `-ShapeImage` | none | ControlNet Canny reference from `inspo/` — traces edges to guide the silhouette/structure |
| `-ControlNetStrength` | `0.7` | How strictly the shape reference is followed (0 = ignore, 1 = rigid) |
| `-NoRembg` | false | Skip background removal (rembg) — keep raw generated image |
| `-Background` | `transparent` | After rembg: composite onto this colour (`black`, `white`, `#rrggbb`) or `transparent` |

## Models required

Installed by `make install-comfyui-extras`. Located in `ComfyUI_windows_portable\ComfyUI\models\`:

- `checkpoints/` — SDXL base or fine-tuned checkpoint (e.g. `90sAnime77_7790sAnimeII.safetensors`)
- `ipadapter/ip-adapter_sdxl_vit-h.safetensors` — IP-Adapter style transfer
- `clip_vision/clip_vision_h14.safetensors` — vision encoder for IP-Adapter
- `controlnet/controlnet-canny-sdxl.safetensors` — edge-guided shape control
- `vae/sdxl_vae.safetensors` — SDXL VAE for correct colour decoding
- `loras/` — optional LoRA fine-tunes (e.g. `GallForce_IllustriousV1.safetensors`)

Custom nodes required (also installed by extras): `ComfyUI_IPAdapter_plus`, `comfyui_controlnet_aux`.
