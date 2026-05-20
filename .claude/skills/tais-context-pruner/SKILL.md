---
name: tais-context-pruner
description: Audits and tightens CONTEXT.md, CLAUDE.md, docs/CONVENTIONS.md, and similar AI instruction files. Runs three passes to find cut candidates, then walks through findings one at a time for approval before applying any edits. Use when user wants to review, clean up, or shorten a context or instruction file, or says it is getting too long.
---

# Context Pruner

Audit an instruction file in three passes, compile findings, then grill through them one at a time — applying edits on approval.

## Before the passes: build context

1. Read `docs/adr/` if it exists. Build a **topic map**: `{topic → ADR filename}` from each filename slug (e.g. `0002-fixed-point-math.md` → "fixed-point math").
2. Scan the target file for `[text](path.md)` links. Read each linked file.

If neither exists, skip this step and proceed to Pass 1.

## ADR escape hatch

Before flagging any cut, ask: is this (1) hard to reverse, (2) surprising without context, and (3) the result of a real trade-off?

If **all three**: don't cut. If the topic is absent from the topic map, draft an ADR stub and suggest moving. If it's already in the topic map, mark **ALREADY IN ADR** — safe to cut.

## Pass 1: Form

Rewrite — don't just flag. Provide the replacement so the user can copy-paste directly.

- **Passive explanations** → imperatives. "We tend to use X" → "Use X."
- **Prose where bullets work** → rewrite as bullets (lists of rules, steps, options)
- **Verbose phrasing** → strip filler. Show the rewritten line.
- **Meta-commentary** → delete. "This section covers..." adds nothing.
- **Justifications without decision value** → cut if removing it doesn't change how Claude acts. Apply ADR escape hatch first.

## Pass 2: Content

- **Ambiguous rules without examples** → flag for a good/bad snippet
- **Defensive "don't" rules with no clear trigger** → ask if the mistake has actually occurred. Apply ADR escape hatch first.
- **Duplication with ADRs, linked files, or code** → flag and suggest pointing to the source
- **Stale references** → file paths or decisions that may no longer match the codebase

## Pass 3: Structure

- **Contradictions** → list conflicting rule pairs explicitly
- **Concern-mixing** → stable conventions and volatile state in one file → suggest splitting
- **Section bloat** (>15 lines) → check topic map. If in ADR, cite and cut. If ADR-worthy, draft stub. Otherwise trim in-place.
- **CLAUDE.md length** → if >60 lines, treat every line past 30 as a candidate. Always/Never behavioral rules move to `docs/CONVENTIONS.md` first. Show the exact lines and the block they become.

## Grill loop

After all three passes, present findings as a numbered list. Each finding: type (**CUT** / **MOVE TO ADR** / **ALREADY IN ADR**) and a one-line reason. For MOVE TO ADR, include a ready-to-save stub using the next available ADR number. End with: "Found N findings. Walk through them one at a time?"

For each finding:
1. Quote the relevant lines
2. State which pass/rule it violates and the finding type
3. **Recommendation** with one sentence of reasoning
4. Ask: "Apply it?"

Wait for the answer:
- **"yes"** → edit the file immediately, confirm done, next
- **"no"** → move on, no argument
- **"rewrite it"** → apply the Pass 1 rewrite on confirmation

After the last finding: state before/after line count and count of ADR stubs drafted.
