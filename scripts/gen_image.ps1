# Wrapper: converts repo-relative paths to absolute Windows paths and calls comfyui_submit.py.
param(
    [Parameter(Mandatory)][string]$Prompt,
    [string]$Negative = "blurry, low quality, watermark, text, signature, border, frame",
    [string]$StyleImage = "",
    [string]$ShapeImage = "",
    [Parameter(Mandatory)][string]$Output,
    [int]$Size = 512,
    [int]$Width = 0,
    [int]$Height = 0,
    [int]$Steps = 100,
    [float]$Cfg = 7.0,
    [float]$IpAdapterStrength = 0.6,
    [float]$ControlNetStrength = 0.7,
    [int]$Seed = -1,
    [switch]$NoRembg,
    [switch]$Symmetry,
    [string]$Background = "transparent",
    [string]$Model = "",
    [string]$DiffusionModel = "",
    [string]$ClipName1 = "clip_l.safetensors",
    [string]$ClipName2 = "t5xxl_fp16.safetensors",
    [string]$Vae = "",
    [string]$Lora = "",
    [float]$LoraStrength = 0.8
)

$repoRoot = Split-Path $PSScriptRoot -Parent
$python   = Join-Path $repoRoot "ComfyUI_windows_portable\python_embeded\python.exe"
$script   = Join-Path $repoRoot "scripts\comfyui_submit.py"

if (-not (Test-Path $python)) {
    Write-Error "ComfyUI Python not found - run make install-comfyui first."
    exit 1
}

$argList = @($script,
    "--prompt",  $Prompt,
    "--negative", $Negative,
    "--output",  (Join-Path $repoRoot $Output),
    "--size",    $Size,
    "--ipadapter-strength", $IpAdapterStrength,
    "--controlnet-strength", $ControlNetStrength
)
$argList += "--steps", $Steps, "--cfg", $Cfg
if ($Width -gt 0)  { $argList += "--width",  $Width }
if ($Height -gt 0) { $argList += "--height", $Height }
if ($StyleImage)  { $argList += "--style-image",  (Join-Path $repoRoot $StyleImage) }
if ($ShapeImage)  { $argList += "--shape-image",  (Join-Path $repoRoot $ShapeImage) }
if ($Seed -ge 0)  { $argList += "--seed", $Seed }
if ($NoRembg)     { $argList += "--no-rembg" }
if ($Symmetry)    { $argList += "--symmetry" }
if ($Background)  { $argList += "--background", $Background }
if ($Model)           { $argList += "--model", $Model }
if ($DiffusionModel)  { $argList += "--diffusion-model", $DiffusionModel, "--clip-l", $ClipName1, "--t5xxl", $ClipName2 }
if ($Vae)             { $argList += "--vae", $Vae }
if ($Lora)            { $argList += "--lora", $Lora, "--lora-strength", $LoraStrength }

& $python @argList
