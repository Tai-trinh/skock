#!/usr/bin/env python3
"""
Submit a ComfyUI SDXL workflow and save the output PNG.
Called via scripts/gen_image.ps1 — receives absolute Windows paths.
"""
import argparse
import json
import random
import sys
import time
import urllib.request
import urllib.parse
from pathlib import Path

COMFYUI_URL = "http://localhost:8188"
REPO_ROOT = Path(__file__).resolve().parent.parent


def upload_image(path: Path) -> str:
    with open(path, "rb") as f:
        data = f.read()
    boundary = "----SkockBoundary"
    body = (
        f"--{boundary}\r\n"
        f'Content-Disposition: form-data; name="image"; filename="{path.name}"\r\n'
        f"Content-Type: image/png\r\n\r\n"
    ).encode() + data + f"\r\n--{boundary}--\r\n".encode()
    req = urllib.request.Request(
        f"{COMFYUI_URL}/upload/image",
        data=body,
        headers={"Content-Type": f"multipart/form-data; boundary={boundary}"},
        method="POST",
    )
    with urllib.request.urlopen(req) as resp:
        return json.loads(resp.read())["name"]


def queue_prompt(workflow: dict) -> str:
    payload = json.dumps({"prompt": workflow}).encode()
    req = urllib.request.Request(
        f"{COMFYUI_URL}/prompt",
        data=payload,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req) as resp:
            return json.loads(resp.read())["prompt_id"]
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"ComfyUI rejected workflow ({e.code}): {body}") from None


def wait_for_outputs(prompt_id: str, timeout: int = 600) -> dict:
    deadline = time.time() + timeout
    while time.time() < deadline:
        url = f"{COMFYUI_URL}/history/{urllib.parse.quote(prompt_id)}"
        with urllib.request.urlopen(url) as resp:
            history = json.loads(resp.read())
        if prompt_id in history:
            entry = history[prompt_id]
            status = entry.get("status", {})
            if status.get("status_str") == "error":
                msgs = status.get("messages", [])
                raise RuntimeError(f"ComfyUI execution failed: {msgs}")
            return entry["outputs"]
        time.sleep(2)
        print(".", end="", flush=True)
    raise TimeoutError(f"ComfyUI timed out after {timeout}s")


def download_image(filename: str, subfolder: str = "", img_type: str = "output") -> bytes:
    params = urllib.parse.urlencode({"filename": filename, "subfolder": subfolder, "type": img_type})
    with urllib.request.urlopen(f"{COMFYUI_URL}/view?{params}") as resp:
        return resp.read()


def get_available_checkpoints() -> list[str]:
    with urllib.request.urlopen(f"{COMFYUI_URL}/object_info/CheckpointLoaderSimple") as resp:
        info = json.loads(resp.read())
    return info["CheckpointLoaderSimple"]["input"]["required"]["ckpt_name"][0]


def inject_lora(workflow: dict, lora_name: str, strength: float) -> dict:
    """Insert a LoraLoader after the checkpoint/unet node and rewire model/clip outputs."""
    import copy
    wf = copy.deepcopy(workflow)
    ckpt_id = next(
        (nid for nid, n in wf.items()
         if n.get("class_type") in ("CheckpointLoaderSimple", "UnetLoaderGGUF")),
        None,
    )
    if ckpt_id is None:
        return wf
    is_gguf = wf[ckpt_id].get("class_type") == "UnetLoaderGGUF"
    lora_id = "lora_0"
    if is_gguf:
        # GGUF has no bundled CLIP — use model-only LoRA loader
        wf[lora_id] = {
            "inputs": {
                "lora_name": lora_name,
                "strength_model": strength,
                "model": [ckpt_id, 0],
            },
            "class_type": "LoraLoaderModelOnly",
        }
        def rewire(node):
            if isinstance(node, dict):
                return {k: rewire(v) for k, v in node.items()}
            if isinstance(node, list) and len(node) == 2 and node[0] == ckpt_id and node[1] == 0:
                return [lora_id, 0]
            if isinstance(node, list):
                return [rewire(v) for v in node]
            return node
    else:
        wf[lora_id] = {
            "inputs": {
                "lora_name": lora_name,
                "strength_model": strength,
                "strength_clip": strength,
                "model": [ckpt_id, 0],
                "clip": [ckpt_id, 1],
            },
            "class_type": "LoraLoader",
        }
        def rewire(node):
            if isinstance(node, dict):
                return {k: rewire(v) for k, v in node.items()}
            if isinstance(node, list) and len(node) == 2 and node[0] == ckpt_id and node[1] in (0, 1):
                return [lora_id, node[1]]
            if isinstance(node, list):
                return [rewire(v) for v in node]
            return node
    for nid in list(wf.keys()):
        if nid not in (ckpt_id, lora_id):
            wf[nid] = rewire(wf[nid])
    return wf


def inject_vae(workflow: dict, vae_name: str) -> dict:
    """Add a VAELoader node and rewire all VAEDecode nodes to use it."""
    import copy
    wf = copy.deepcopy(workflow)
    vae_id = "vae_0"
    wf[vae_id] = {"inputs": {"vae_name": vae_name}, "class_type": "VAELoader"}
    for node in wf.values():
        if node.get("class_type") == "VAEDecode":
            node["inputs"]["vae"] = [vae_id, 0]
    return wf


def apply_symmetry(image_bytes: bytes) -> bytes:
    """Mirror the left half onto the right half for perfect bilateral symmetry."""
    from PIL import Image
    import io
    img = Image.open(io.BytesIO(image_bytes))
    w, h = img.size
    left = img.crop((0, 0, w // 2, h))
    right = left.transpose(Image.FLIP_LEFT_RIGHT)
    result = img.copy()
    result.paste(left, (0, 0))
    result.paste(right, (w // 2, 0))
    buf = io.BytesIO()
    result.save(buf, format="PNG")
    return buf.getvalue()


def apply_background(image_bytes: bytes, color: str) -> bytes:
    """Composite a rembg-transparent image onto a solid background color."""
    from PIL import Image
    import io
    img = Image.open(io.BytesIO(image_bytes)).convert("RGBA")
    bg = Image.new("RGBA", img.size, color)
    bg.paste(img, mask=img.split()[3])
    buf = io.BytesIO()
    bg.convert("RGB").save(buf, format="PNG")
    return buf.getvalue()


def fill_workflow(node, subs: dict):
    if isinstance(node, dict):
        return {k: fill_workflow(v, subs) for k, v in node.items()}
    if isinstance(node, list):
        return [fill_workflow(v, subs) for v in node]
    if isinstance(node, str) and node in subs:
        return subs[node]
    return node


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--prompt", required=True)
    ap.add_argument("--negative", default="blurry, low quality, watermark, text, signature, border, frame")
    ap.add_argument("--style-image")
    ap.add_argument("--shape-image")
    ap.add_argument("--output", required=True)
    ap.add_argument("--size", type=int, default=512)
    ap.add_argument("--width", type=int, default=None)
    ap.add_argument("--height", type=int, default=None)
    ap.add_argument("--ipadapter-strength", type=float, default=0.6)
    ap.add_argument("--controlnet-strength", type=float, default=0.7)
    ap.add_argument("--seed", type=int, default=None)
    ap.add_argument("--steps", type=int, default=100)
    ap.add_argument("--cfg", type=float, default=7.0)
    ap.add_argument("--no-rembg", action="store_true")
    ap.add_argument("--symmetry", action="store_true", help="Mirror left half onto right for bilateral symmetry")
    ap.add_argument("--background", default="transparent", help="'transparent', a CSS color name, or '#rrggbb'")
    ap.add_argument("--workflow")
    ap.add_argument("--model", help="Checkpoint name from models/checkpoints/; auto-selects first available if omitted")
    ap.add_argument("--diffusion-model", help="Diffusion model (UNET/GGUF) from models/diffusion_models/; uses Flux workflow")
    ap.add_argument("--clip-l", default="clip_l.safetensors", help="First text encoder (clip_name1) for diffusion model (models/text_encoders/)")
    ap.add_argument("--t5xxl", default="t5xxl_fp16.safetensors", help="Second text encoder (clip_name2) for diffusion model (models/text_encoders/)")
    ap.add_argument("--vae", help="VAE filename from models/vae/")
    ap.add_argument("--lora", help="LoRA filename from models/loras/")
    ap.add_argument("--lora-strength", type=float, default=0.8)
    ap.add_argument("--timeout", type=int, default=600)
    args = ap.parse_args()

    seed = args.seed if args.seed is not None else random.randint(0, 2**31 - 1)
    width = args.width if args.width is not None else args.size
    height = args.height if args.height is not None else args.size
    is_flux = bool(args.diffusion_model)

    if is_flux:
        print(f"Using diffusion model: {args.diffusion_model}")
        if not args.vae:
            args.vae = "ae.safetensors"
            print(f"Diffusion mode: defaulting VAE to {args.vae}")
        is_gguf = args.diffusion_model.lower().endswith(".gguf")
        default_workflow = "flux_text_only.json" if is_gguf else "unet_text_only.json"
        print(f"Using workflow: {default_workflow}")
    else:
        model = args.model or get_available_checkpoints()[0]
        print(f"Using model: {model}")
        has_refs = bool(args.style_image or args.shape_image)
        default_workflow = "sdxl_ipadapter.json" if has_refs else "sdxl_text_only.json"

    workflow_path = Path(args.workflow) if args.workflow else REPO_ROOT / "scripts" / "workflows" / default_workflow

    with open(workflow_path) as f:
        workflow = json.load(f)

    style_name = None
    if args.style_image:
        print(f"Uploading style: {args.style_image}")
        style_name = upload_image(Path(args.style_image))

    shape_name = None
    controlnet_strength = args.controlnet_strength
    if args.shape_image:
        print(f"Uploading shape: {args.shape_image}")
        shape_name = upload_image(Path(args.shape_image))
    else:
        controlnet_strength = 0.0

    ipadapter_strength = args.ipadapter_strength if style_name else 0.0
    fallback = style_name or shape_name or "white.png"

    subs = {
        "__PROMPT__": args.prompt,
        "__NEGATIVE__": args.negative,
        "__STYLE_IMAGE__": style_name or fallback,
        "__SHAPE_IMAGE__": shape_name or fallback,
        "__WIDTH__": width,
        "__HEIGHT__": height,
        "__SEED__": seed,
        "__IPADAPTER_STRENGTH__": ipadapter_strength,
        "__CONTROLNET_STRENGTH__": controlnet_strength,
        "__OUTPUT_PREFIX__": Path(args.output).stem,
        "__STEPS__": args.steps,
        "__CFG__": args.cfg,
    }
    if is_flux:
        subs["__DIFFUSION_MODEL__"] = args.diffusion_model
        subs["__CLIP_L__"] = args.clip_l
        subs["__T5XXL__"] = args.t5xxl
        subs["__VAE__"] = args.vae
    else:
        subs["__MODEL__"] = model
    workflow = fill_workflow(workflow, subs)
    if args.vae and not is_flux:
        print(f"Injecting VAE: {args.vae}")
        workflow = inject_vae(workflow, args.vae)
    if args.lora:
        print(f"Injecting LoRA: {args.lora} (strength={args.lora_strength})")
        workflow = inject_lora(workflow, args.lora, args.lora_strength)

    print(f"Queuing (seed={seed})...")
    prompt_id = queue_prompt(workflow)
    print(f"Queued: {prompt_id}")
    print("Waiting", end="", flush=True)
    outputs = wait_for_outputs(prompt_id, timeout=args.timeout)
    print()

    image_bytes = None
    for node_output in outputs.values():
        if "images" in node_output:
            info = node_output["images"][0]
            image_bytes = download_image(info["filename"], info.get("subfolder", ""), info.get("type", "output"))
            break

    if image_bytes is None:
        print("ERROR: no image in output", file=sys.stderr)
        sys.exit(1)

    if args.symmetry:
        print("Applying bilateral symmetry...")
        image_bytes = apply_symmetry(image_bytes)

    if not args.no_rembg:
        print("Removing background...")
        from rembg import remove, new_session
        image_bytes = remove(image_bytes, session=new_session("isnet-anime"))
        if args.background.lower() != "transparent":
            print(f"Compositing on background: {args.background}")
            image_bytes = apply_background(image_bytes, args.background)

    out = Path(args.output)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(image_bytes)
    print(f"Saved: {out}")


if __name__ == "__main__":
    main()
