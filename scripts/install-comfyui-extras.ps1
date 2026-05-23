# Extra Python dependencies for the ComfyUI asset generation pipeline.
# Run this after install-comfyui — requires ComfyUI to already be extracted.
$repoRoot = Split-Path $PSScriptRoot -Parent
$python = Join-Path $repoRoot "ComfyUI_windows_portable\python_embeded\python.exe"

if (-not (Test-Path $python)) {
    Write-Error "ComfyUI embedded Python not found at $python - run make install-comfyui first."
    exit 1
}

& $python -m pip install rembg[gpu] onnxruntime-gpu
