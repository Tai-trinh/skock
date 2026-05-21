---
name: tais-context-splitter
description: Splits a monolithic CONTEXT.md or CLAUDE.md into focused docs/ files. Plans the split first, then walks through every placement decision interactively one at a time before writing anything. Use when user wants to split context files, break up CLAUDE.md, or says "my context file is too big / hard to maintain."
---

# Context Splitter

Read the target file, plan a split, grill through every placement with the user, then write on confirmation.

## Anchor files (never moved)

- **CLAUDE.md** — index only: project summary, commands, links to `docs/`. Hard cap: 60 lines.
- **CONTEXT.md** — domain glossary and product concepts. Owned by `/grill-with-docs`. Split internally if needed; never relocate to `docs/`.

## Standard taxonomy (`docs/`)

| File | Contents |
|------|----------|
| `docs/ARCHITECTURE.md` | System shape, modules, data flow, tech stack, key design decisions |
| `docs/CONVENTIONS.md` | Naming, style, Always/Never rules Claude must follow |
| `docs/WORKFLOWS.md` | Build, test, deploy, common commands |
| `docs/API-CONTRACTS.md` | Endpoint shapes, wire formats, external interfaces |
| `docs/DATA-MODEL.md` | Schema, types, entity relationships |
| `docs/CURRENT-TASK.md` | What's actively in flight, recent decisions (volatile — never mix with stable content) |
| `scratch/TODO.md` | Deferred work, open questions, "revisit after X" items from context docs (created lazily) |

`docs/adr/` is managed by `/grill-with-docs`. Do not touch it. If a block genuinely doesn't fit any category above, ask before inventing a new filename.

## Process

### Step 1: Read and categorize

Read the target file and any linked files. Assign each section or block to a target file. Separate stable content (conventions, architecture) from volatile content (current task, open questions). Flag ambiguous placements.

### Step 2: Compile the plan

Present a concise split plan: a table of `[section] → [target file]` for every block. List ambiguous placements below the table with your best guess. End with: "Ready to walk through N placements?"

### Step 3: Grill loop

Walk placements one at a time. For each:

1. State the section and proposed target file
2. One sentence: why this file
3. Ask: "Sound right?"

Wait for the answer before moving on:
- **"yes"** → accepted, next
- **"no" / alternative** → update the placement, next
- **"skip"** → leave it in place, next

After the last item, ask: "Write the files?"

### Step 4: Execute

Write each `docs/` file using confirmed placements. Then rewrite CLAUDE.md as a clean index:
- One-sentence project summary
- Commands block (if >8 lines, link to `docs/WORKFLOWS.md` instead)
- Docs section linking only files actually created
- Any Always/Never behavioral rules found inline in CLAUDE.md → move to `docs/CONVENTIONS.md`

Report files written and line counts. Suggest running `/tais-context-pruner` on each output to tighten.
