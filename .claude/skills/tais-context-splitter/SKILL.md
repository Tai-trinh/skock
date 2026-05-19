---
name: tais-context-splitter
description: Use this skill when the user wants to split a large context.md, CLAUDE.md, or similar monolithic instruction file into focused, separate markdown files under docs/ and wire them up with references in CLAUDE.md. Triggers include "split my context file," "break this up," "my CLAUDE.md is too big," or pasting a long context file and asking for it to be organized into multiple files. Do NOT use for splitting code files, general documentation, or content aimed at human readers.
---

# Context Splitter

Split a monolithic context file into focused files under `docs/`, each covering one concern. CLAUDE.md becomes a 30-line index pointing at them. Designed to run first — then `/tais-context-pruner` on each output file to tighten.

## Anchor files (root — never moved)

- **CLAUDE.md** — index only: one-line project summary, key commands, links to `docs/` files. Hard cap: 60 lines.
- **CONTEXT.md** — domain glossary and product concepts. Owned by `/grill-with-docs`. Do not split or relocate.

## Standard taxonomy (`docs/`)

All split files live under `docs/` with uppercase names. Don't create new names unless content genuinely doesn't fit — ask first.

- **docs/ARCHITECTURE.md** — system shape, module boundaries, data flow, tech stack, key design decisions
- **docs/CONVENTIONS.md** — naming, style, formatting, code patterns, Always/Never rules Claude must follow
- **docs/WORKFLOWS.md** — how to run, build, test, deploy; common commands
- **docs/API-CONTRACTS.md** — endpoint shapes, request/response formats, external service interfaces
- **docs/DATA-MODEL.md** — schema, types, entity relationships
- **docs/CURRENT-TASK.md** — what's in flight right now, recent decisions, open questions (volatile)
- **docs/GLOSSARY.md** — domain terms (only if no CONTEXT.md exists; otherwise CONTEXT.md owns the glossary)

`docs/adr/` is managed by `/grill-with-docs`. The splitter does not touch it.

## How to split

### Step 1: Read and categorize
Go through the source file section by section. Assign each block to a target file. Flag anything ambiguous and ask before placing.

### Step 2: Separate stable from volatile
Stable content (conventions, architecture, glossary) → its own file. Volatile content (current task, open questions, recent decisions) → `docs/CURRENT-TASK.md`. Never mix them.

### Step 3: Deduplicate
Before writing, find content repeated across sections. Merge into the most appropriate file; drop the duplicates.

### Step 4: Write the `docs/` files
Each file:
- Opens with a one-line purpose (no header needed)
- Uses bullets and imperatives, not prose
- Stays under ~50 lines — if it exceeds that, ask whether to split further

### Step 5: Rewrite CLAUDE.md

CLAUDE.md is the index. Hard cap: 60 lines.

```md
# {Project name}

{One sentence: what this project is.}

## Commands

- `{build}` — build
- `{test}` — test
- `{format}` — format

## Docs

- [Architecture](docs/ARCHITECTURE.md) — {one-line description}
- [Conventions](docs/CONVENTIONS.md) — {one-line description}
- [Workflows](docs/WORKFLOWS.md) — {one-line description}
- ... (only files actually created)
```

Enforcement:
- Any "Always do X" / "Never do X" rules found inline in CLAUDE.md → move to `docs/CONVENTIONS.md`. They are not index content.
- If the commands block grows past ~8 lines → move to `docs/WORKFLOWS.md`, replace with a single link.
- Only link files that were actually created.

## Closing output

After all files are written, output this block verbatim:

```
## Files written
- CLAUDE.md (updated)
- docs/ARCHITECTURE.md
- docs/CONVENTIONS.md
- ... (every file created or modified)

## Next step
Run `/tais-context-pruner` on each file above to tighten.
Start with CLAUDE.md — if it exceeds 60 lines the pruner will identify what to move.
```
