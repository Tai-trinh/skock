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

## Pre-flight check

ComfyUI runs on Windows; Claude runs in WSL — check via powershell.exe:

```bash
powershell.exe -Command "try { Invoke-RestMethod http://localhost:8188/system_stats | Out-Null; Write-Host 'ComfyUI OK' } catch { Write-Host 'ComfyUI not running' }"
```

If not running, tell the user to run `make comfyui` first.

## Before running — print generation summary

**Always print this block before invoking `gen_image.ps1`**, so the user can review what will run:

```
=== gen-image ===
Checkpoint : <model name, e.g. 90sAnime77_7790sAnimeII.safetensors or "auto-detect">
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
  [-Steps 100] \
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
| `-Steps` | `100` | Denoising iterations — more = sharper detail, slower generation |
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
