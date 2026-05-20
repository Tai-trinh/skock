#!/usr/bin/env bash
# Generates a PlantUML @startdot/@enddot diagram per Rust crate using cargo-modules.
# Called by .githooks/pre-commit when .rs or Cargo.toml files are staged.
set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
OUT_DIR="$REPO_ROOT/diagrams/rust/architecture"
CARGO=$(command -v cargo.exe 2>/dev/null && echo cargo.exe || echo cargo)

if ! $CARGO modules --version &>/dev/null 2>&1; then
    echo "warning: cargo-modules not installed — skipping Rust diagram generation"
    echo "         install with: cargo install cargo-modules"
    exit 0
fi

cd "$REPO_ROOT"
mkdir -p "$OUT_DIR"

while IFS= read -r crate; do
    out="$OUT_DIR/${crate}.puml"
    {
        echo "@startdot"
        $CARGO modules generate graph --package "$crate" 2>/dev/null || true
        echo "@enddot"
    } > "$out"
    git add "$out"
done < <($CARGO metadata --no-deps --format-version 1 | \
    python3 -c "import sys,json; [print(p['name']) for p in json.load(sys.stdin)['packages']]")
