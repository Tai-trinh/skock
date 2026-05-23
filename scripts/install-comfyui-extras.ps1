# Extra dependencies for the ComfyUI asset generation pipeline.
# Run after install-comfyui. Safe to re-run — git pull updates nodes, skips existing models.
$repoRoot   = Split-Path $PSScriptRoot -Parent
$comfyRoot  = Join-Path $repoRoot "ComfyUI_windows_portable\ComfyUI"
$python     = Join-Path $repoRoot "ComfyUI_windows_portable\python_embeded\python.exe"

if (-not (Test-Path $python)) {
    Write-Error "ComfyUI Python not found at $python - run make install-comfyui first."
    exit 1
}

# --- Python packages ---
Write-Host "Installing Python packages..."
& $python -m pip install "rembg[gpu]" onnxruntime-gpu

# --- Custom nodes ---
$customNodes = Join-Path $comfyRoot "custom_nodes"

function Install-Node($repo, $dir) {
    $path = Join-Path $customNodes $dir
    if (Test-Path $path) {
        Write-Host "Updating $dir..."
        git -C $path pull
    } else {
        Write-Host "Cloning $dir..."
        git clone $repo $path
    }
    $req = Join-Path $path "requirements.txt"
    if (Test-Path $req) { & $python -m pip install -r $req }
}

Install-Node "https://github.com/cubiq/ComfyUI_IPAdapter_plus" "ComfyUI_IPAdapter_plus"
Install-Node "https://github.com/Fannovel16/comfyui_controlnet_aux" "comfyui_controlnet_aux"

# --- Models ---
$ProgressPreference = 'Continue'

function Download-Model($url, $dest) {
    if (Test-Path $dest) {
        Write-Host "Already exists: $dest"
        return
    }
    Write-Host "Downloading $(Split-Path $dest -Leaf)..."
    Import-Module BitsTransfer
    Start-BitsTransfer -Source $url -Destination $dest
}

function Download-Model-Firefox($url, $dest) {
    if (Test-Path $dest) {
        Write-Host "Already exists: $dest"
        return
    }
    Write-Host "Downloading $(Split-Path $dest -Leaf) via Firefox session..."

    # Find default Firefox profile
    $profileBase = "$env:APPDATA\Mozilla\Firefox\Profiles"
    $profile = Get-ChildItem $profileBase -Directory | Where-Object { $_.Name -match 'default' } | Select-Object -First 1
    if (-not $profile) { Write-Error "No Firefox profile found"; return }

    # Copy cookies.sqlite (Firefox locks the live file)
    $tempDb = Join-Path $env:TEMP "ff_cookies_temp.sqlite"
    Copy-Item (Join-Path $profile.FullName "cookies.sqlite") $tempDb -Force

    # Extract cookies for the target host using embedded Python
    $urlHost = ([System.Uri]$url).Host
    $cookieStr = & $python -c "import sqlite3; conn = sqlite3.connect(r'$tempDb'); rows = conn.execute('SELECT name, value FROM moz_cookies WHERE host LIKE ?', ('%$urlHost%',)).fetchall(); conn.close(); print('; '.join(f'{n}={v}' for n, v in rows))"
    Remove-Item $tempDb -Force

    curl.exe -L --progress-bar --cookie "$cookieStr" -o "$dest" "$url"
}

$models = Join-Path $comfyRoot "models"

# Ensure model subdirectories exist
@("ipadapter", "clip_vision", "controlnet", "vae", "checkpoints", "loras", "text_encoders") | ForEach-Object {
    New-Item -ItemType Directory -Force -Path (Join-Path $models $_) | Out-Null
}

Download-Model `
    "https://huggingface.co/h94/IP-Adapter/resolve/main/sdxl_models/ip-adapter_sdxl_vit-h.safetensors" `
    (Join-Path $models "ipadapter\ip-adapter_sdxl_vit-h.safetensors")

Download-Model `
    "https://huggingface.co/h94/IP-Adapter/resolve/main/models/image_encoder/model.safetensors" `
    (Join-Path $models "clip_vision\clip_vision_h14.safetensors")

Download-Model `
    "https://huggingface.co/xinsir/controlnet-canny-sdxl-1.0/resolve/main/diffusion_pytorch_model_V2.safetensors" `
    (Join-Path $models "controlnet\controlnet-canny-sdxl.safetensors")

Download-Model `
    "https://huggingface.co/stabilityai/sdxl-vae/resolve/main/sdxl_vae.safetensors" `
    (Join-Path $models "vae\sdxl_vae.safetensors")

# 90s anime aesthetic checkpoint (CivitAI — uses Firefox session for auth)
Download-Model-Firefox `
    "https://civitai.com/api/download/models/1922528?fileId=1820751" `
    (Join-Path $models "checkpoints\90sAnime77_7790sAnimeII.safetensors")

# Anima LoRA (CivitAI — uses Firefox session for auth)
Download-Model-Firefox `
    "https://civitai.com/api/download/models/2795022?fileId=2680992" `
    (Join-Path $models "loras\90s anime aesthetic Anima-step00001600.safetensors")

# GallForce LoRA (CivitAI — uses Firefox session for auth)
Download-Model-Firefox `
    "https://civitai.com/api/download/models/1390790?fileId=1293334" `
    (Join-Path $models "loras\GallForce_IllustriousV1.safetensors")

# Starsector sci-fi ship LoRA (CivitAI — uses Firefox session for auth)
Download-Model-Firefox `
    "https://civitai.com/api/download/models/1278829?fileId=1183513" `
    (Join-Path $models "loras\StarsectorFluxV0.4FantasyFM-000148.safetensors")

# Spaceships LoRA (CivitAI — uses Firefox session for auth)
Download-Model-Firefox `
    "https://civitai.com/api/download/models/2117856?fileId=2012406" `
    (Join-Path $models "loras\Spaceships.safetensors")

# --- Flux.1-dev (requires HuggingFace login in Firefox) ---
# Checkpoint (~24 GB)
Download-Model-Firefox `
    "https://huggingface.co/black-forest-labs/FLUX.1-dev/resolve/main/flux1-dev.safetensors" `
    (Join-Path $models "checkpoints\flux1-dev.safetensors")

# VAE
Download-Model-Firefox `
    "https://huggingface.co/black-forest-labs/FLUX.1-dev/resolve/main/ae.safetensors" `
    (Join-Path $models "vae\ae.safetensors")

# Text encoders (public — no auth needed)
Download-Model `
    "https://huggingface.co/comfyanonymous/flux_text_encoders/resolve/main/clip_l.safetensors" `
    (Join-Path $models "text_encoders\clip_l.safetensors")

Download-Model `
    "https://huggingface.co/comfyanonymous/flux_text_encoders/resolve/main/t5xxl_fp16.safetensors" `
    (Join-Path $models "text_encoders\t5xxl_fp16.safetensors")

Write-Host "Done."
