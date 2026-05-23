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
Install-Node "https://github.com/city96/ComfyUI-GGUF" "ComfyUI-GGUF"

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

function Download-Zip-Firefox($url, $destDir, $zipName) {
    $destDir = [string]$destDir
    $existing = Get-ChildItem $destDir -Filter "*.gguf" -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Host "Already exists: $($existing[0].Name)"
        return
    }
    $tempZip = Join-Path $env:TEMP $zipName
    Download-Model-Firefox $url $tempZip
    if (-not (Test-Path $tempZip)) { return }

    # Check magic bytes — ZIP starts with PK (0x50 0x4B); read only 2 bytes to support >2GB files
    $fs = [System.IO.File]::OpenRead($tempZip)
    $magic = New-Object byte[] 2
    $null = $fs.Read($magic, 0, 2)
    $fs.Close()

    if ($magic[0] -eq 0x50 -and $magic[1] -eq 0x4B) {
        Write-Host "Extracting $zipName with 7-Zip..."
        $7z = "C:\Program Files\7-Zip\7z.exe"
        & $7z x $tempZip "-o$destDir" -y | Out-Null
        Remove-Item $tempZip -Force
    } else {
        # Read first 64 bytes as text to detect HTML auth error pages
        $fs2 = [System.IO.File]::OpenRead($tempZip)
        $headBytes = New-Object byte[] 64
        $null = $fs2.Read($headBytes, 0, 64)
        $fs2.Close()
        $head = [System.Text.Encoding]::UTF8.GetString($headBytes)
        if ($head -match "<!DOCTYPE|<html") {
            Write-Error "CivitAI returned an HTML page — auth failed. Make sure you are logged in to CivitAI in Firefox."
            Remove-Item $tempZip -Force
        } else {
            $stem = [System.IO.Path]::GetFileNameWithoutExtension($zipName)
            $dest = Join-Path $destDir "$stem.safetensors"
            Write-Host "Not a ZIP — treating as safetensors, saving to $(Split-Path $dest -Leaf)"
            Move-Item $tempZip $dest -Force
        }
    }
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

# Anime Pastel Dream checkpoint (CivitAI — uses Firefox session for auth)
Download-Model-Firefox `
    "https://civitai.com/api/download/models/28100?fileId=89850" `
    (Join-Path $models "checkpoints\animePastelDream_softBakedVae.safetensors")

# Anime Model Amore v2 PDXL checkpoint (CivitAI — uses Firefox session for auth)
Download-Model-Firefox `
    "https://civitai.com/api/download/models/583459?fileId=498889" `
    (Join-Path $models "checkpoints\animeModel_amorev2PDXL.safetensors")

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

# --- Flux.1-dev GGUF (CivitAI — uses Firefox session for auth) ---
# Downloads ZIP to diffusion_models/ — extract manually, GGUF goes in diffusion_models/
Download-Model-Firefox `
    "https://civitai.com/api/download/models/741907?fileId=656737" `
    (Join-Path $models "diffusion_models\flux1DevGGUFF16_f16.zip")

# Text encoders (public — no auth needed)
Download-Model `
    "https://huggingface.co/comfyanonymous/flux_text_encoders/resolve/main/clip_l.safetensors" `
    (Join-Path $models "text_encoders\clip_l.safetensors")

Download-Model `
    "https://huggingface.co/comfyanonymous/flux_text_encoders/resolve/main/t5xxl_fp16.safetensors" `
    (Join-Path $models "text_encoders\t5xxl_fp16.safetensors")

Write-Host "Done."
