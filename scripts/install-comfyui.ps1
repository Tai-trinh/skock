# ComfyUI — portable Windows build with NVIDIA support
$repoRoot = Split-Path $PSScriptRoot -Parent
$archive = Join-Path $repoRoot "ComfyUI_windows_portable_nvidia.7z"

Import-Module BitsTransfer
Start-BitsTransfer -Source "https://github.com/comfyanonymous/ComfyUI/releases/latest/download/ComfyUI_windows_portable_nvidia.7z" -Destination $archive -DisplayName "ComfyUI" -Description "Downloading ComfyUI portable NVIDIA build"

$7z = "C:\Program Files\7-Zip\7z.exe"
& $7z x $archive "-o$repoRoot" -y
if (Test-Path $archive) { Remove-Item $archive }
