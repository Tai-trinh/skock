#!/usr/bin/env bash
# Print installed ComfyUI models grouped by category.
MODELS="$(dirname "$0")/../ComfyUI_windows_portable/ComfyUI/models"

list_dir() {
    local dir="$MODELS/$1"
    [ -d "$dir" ] || return
    find "$dir" -maxdepth 1 -type f ! -name 'put_*' -printf '%f\n' | sort
}

print_section() {
    local header="$1"; shift
    local files
    files=$(for dir in "$@"; do list_dir "$dir"; done)
    [ -z "$files" ] && return
    echo ""
    echo "$header"
    echo "$files" | while read -r f; do echo "  $f"; done
}

print_section "Checkpoints"       checkpoints
print_section "Diffusion models"  diffusion_models
print_section "LoRAs"             loras
print_section "VAEs"              vae
print_section "Text encoders"     text_encoders
print_section "IP-Adapter"        ipadapter
print_section "ControlNet"        controlnet
print_section "CLIP Vision"       clip_vision
