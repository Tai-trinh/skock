#!/usr/bin/env bash
# Generates PlantUML class diagrams for the C# client using PlantUmlClassDiagramGenerator.
# Called by .githooks/pre-commit when .cs files are staged.
set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
OUT_DIR="diagrams/csharp"
PUML_GEN=puml-gen.exe

if ! command -v $PUML_GEN &>/dev/null; then
    echo "warning: puml-gen not installed — skipping C# diagram generation"
    echo "         install with: dotnet tool install --global PlantUmlClassDiagramGenerator"
    exit 0
fi

cd $REPO_ROOT
mkdir -p "$OUT_DIR"
$PUML_GEN "client/src" "$OUT_DIR" -dir -excludePaths obj,bin,.godot,addons
git add "$OUT_DIR"