#!/usr/bin/env bash
# Generates PlantUML class diagrams for the C# client using PlantUmlClassDiagramGenerator.
# Called by .githooks/pre-commit when .cs files are staged.
set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
OUT_DIR="$REPO_ROOT/diagrams/csharp"

if ! command -v puml-gen &>/dev/null; then
    echo "warning: puml-gen not installed — skipping C# diagram generation"
    echo "         install with: dotnet tool install --global PlantUmlClassDiagramGenerator"
    exit 0
fi

mkdir -p "$OUT_DIR"

puml-gen "$REPO_ROOT/client/src" "$OUT_DIR" -dir -public -excludePaths obj,bin,.godot,addons

git add "$OUT_DIR"
