---
name: tais-context-pruner
description: Use this skill when the user wants to prune, tighten, audit, or shorten a CONTEXT.md, CLAUDE.md, docs/CONVENTIONS.md, or similar instruction file for an AI coding assistant. Triggers include requests to "review my context file," "clean up CLAUDE.md," "this is getting too long," or when the user pastes a context/instructions file and asks for cuts. Do NOT use for general prose editing, code review, or documentation aimed at humans rather than AI assistants.
---

# Context Pruner

Audit and tighten instruction files written for AI coding assistants (CONTEXT.md, CLAUDE.md, docs/CONVENTIONS.md, etc.). The goal is fewer lines, sharper rules, less drift.

## Before the passes: build context

Before auditing a single line, read:

1. **`docs/adr/`** — if the directory exists, read every file. Extract the decision topic from each filename slug (e.g. `0002-fixed-point-math.md` → topic: "fixed-point math"). Build a **topic map**: `{topic → ADR filename}`.
2. **Explicitly linked markdown files** — scan the target file for `[text](path.md)` references. Read each linked file. Note what each one covers.

From these two sources, build:
- **Topic map** — what is already documented in ADRs or linked files.
- **Gap list** — decision-shaped content in the target file whose topic does not appear in the topic map. These are ADR candidates; treat them differently from plain bloat.

If neither `docs/adr/` nor any linked files exist, skip this step and proceed directly to Pass 1.

## The ADR escape hatch

Before suggesting a cut in any pass, apply this check to the flagged content:

> Is this (1) hard to reverse, (2) surprising without context, and (3) the result of a real trade-off?

If **all three** are true and the topic is **not in the topic map**: do not suggest cutting. Instead, draft a ready-to-save ADR stub (see output format below) and suggest moving the content there.

If all three are true and the topic **is in the topic map**: suggest cutting and cite the ADR. "Duplicates ADR-000X — cut with confidence."

If fewer than three are true: proceed with the normal cut suggestion.

## Pass 1: Form

For every violation below, **provide the rewritten version** — do not just flag it. The user should be able to copy-paste the suggestion directly.

- **Passive explanations** → rewrite as imperatives. "We tend to use X" → "Use X."
- **Prose where bullets would work** → rewrite as bullets. Any list of rules, conventions, steps, or options expressed in paragraph form gets converted. Show the full before/after block.
- **Verbose phrasing** → strip filler. "In order to ensure that" → "To ensure". "It is important to note that" → delete. "You should make sure to" → "Always". Rewrite the line; don't just flag it.
- **Meta-commentary** → delete entirely. "This section covers..." or "The following rules apply..." adds no instruction value.
- **Justifications without decision value** → if the "because" doesn't change how Claude acts, cut it. Apply the ADR escape hatch first — a "because" that encodes a hard-to-reverse constraint belongs in an ADR, not in the bin.

## Pass 2: Content

- **Rules without examples** where the rule is ambiguous → flag for a good/bad snippet.
- **Defensive "don't" rules** with no clear trigger → ask the user if this mistake has ever actually occurred. Apply the ADR escape hatch first — a deliberate deviation from the obvious path qualifies even if the mistake hasn't occurred yet.
- **Duplication with code, ADRs, or linked files** → if the file describes what a function/module does, or restates a decision already in the topic map, flag and suggest pointing to the source instead.
- **Stale references** → file paths, function names, or decisions that may no longer match the codebase. Check the topic map first: if the content matches an ADR, it is not stale — it is documented. Flag only what is genuinely unverified.

## Pass 3: Structure

- **Contradictions** → rules that conflict with each other or with newer sections. List pairs explicitly.
- **Concern-mixing** → if stable conventions (naming, style) and volatile state (current task, recent decisions) live in one file, suggest splitting.
- **Section bloat** → any section over ~15 lines: check the topic map first. If the section's topic is already in an ADR or linked file, suggest cutting with a citation. If it is ADR-worthy but undocumented, draft an ADR stub and suggest moving. Only suggest trimming in-place if the content is neither documented nor ADR-worthy.
- **CLAUDE.md length** → if the file being audited is `CLAUDE.md` and it exceeds 60 lines, treat every line past 30 as a candidate for eviction. "Always do X" / "Never do X" behavioural rules are the first to move — they belong in `docs/CONVENTIONS.md`. Provide the specific lines to extract and the exact `docs/CONVENTIONS.md` block they would become. Also add a one-line `[conventions](docs/CONVENTIONS.md)` reference in `CLAUDE.md` to replace them if it isn't already there.

## Output format

Return findings as a numbered list. For each finding, label it as one of three types:

- **CUT** — safe to remove. Quote the relevant lines, state which pass/rule it violates, give the specific cut.
- **MOVE TO ADR** — load-bearing decision not yet documented. Quote the lines, explain which ADR criteria it meets, then include a ready-to-save ADR stub:
  ```
  File: docs/adr/000N-slug.md
  ---
  # {Short title}

  {1-3 sentences: context, decision, why.}
  ```
  Use the next available ADR number from `docs/adr/`.
- **ALREADY IN ADR** — content duplicates an existing ADR. Quote the lines, cite the ADR filename. Mark as safe to cut.

End with a one-line summary: original line count → estimated line count after suggested cuts, and count of ADR stubs drafted.

## What NOT to do

- Do not rewrite the whole file unless asked. Suggestions only.
- Do not preserve content out of politeness. The user is asking for cuts.
- Do not add new rules or sections the user didn't ask for.
- Do not soften suggestions with hedging ("you might want to consider possibly..."). Be direct.
- Do not cut a load-bearing decision without first checking the ADR escape hatch. Deleting undocumented architectural decisions is worse than a bloated context file.
