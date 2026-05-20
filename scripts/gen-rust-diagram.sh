#!/usr/bin/env bash
# Generates a Mermaid workspace dependency diagram per crate via cargo metadata.
# Called by .githooks/pre-commit when .rs or Cargo.toml files are staged.
set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
OUT_DIR="$REPO_ROOT/diagrams/rust/architecture"
CARGO=$(command -v cargo.exe &>/dev/null && echo cargo.exe || echo cargo)

mkdir -p "$OUT_DIR"

TMPPY=$(mktemp /tmp/gen-rust-XXXXXX.py)
trap "rm -f $TMPPY" EXIT

cat > "$TMPPY" <<'PYEOF'
import sys, json
from pathlib import Path

out_dir = Path(sys.argv[1])
meta = json.load(sys.stdin)

workspace_ids = set(meta["workspace_members"])
workspace_names = {p["name"] for p in meta["packages"] if p["id"] in workspace_ids}

for pkg in meta["packages"]:
    if pkg["id"] not in workspace_ids:
        continue
    name = pkg["name"]
    ws_deps = sorted({d["name"] for d in pkg["dependencies"] if d["name"] in workspace_names})

    lines = [f"# {name}", "", "```mermaid", "flowchart TD"]
    lines.append(f'    {name}["{name}"]')
    for dep in ws_deps:
        lines.append(f'    {dep}["{dep}"]')
        lines.append(f"    {name} --> {dep}")
    lines.append("```")

    (out_dir / f"{name}.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
PYEOF

$CARGO metadata --format-version 1 2>/dev/null | python3 "$TMPPY" "$OUT_DIR"

git add "$OUT_DIR"
