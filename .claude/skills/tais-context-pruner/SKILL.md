---
name: tais-context-pruner
description: Audits and tightens CONTEXT.md, CLAUDE.md, docs/CONVENTIONS.md, and similar AI instruction files. Three passes to find cut candidates, then walks through findings one at a time for approval before applying edits. Use when the user wants to prune, trim, review, or shorten a context or instruction file, or says it is getting too long.
---

# Context Pruner

Read every instruction file in scope, run three passes to build a findings list, then walk through them one at a time — applying edits on approval.

## Step 1: Build context

1. Read `CLAUDE.md` and `CONTEXT.md`.
2. Collect every `[text](path.md)` link from both files and read each linked file.
3. Read `docs/adr/` if it exists — build a **topic map**: `{topic → ADR filename}` from each slug (e.g. `0002-fixed-point-math.md` → "fixed-point math").

## GDD escape hatch

Before cutting lore, flavor, design intent, or art direction: check if `docs/GDD.md` exists. If yes, mark **MOVE TO GDD** — append to GDD.md and strip from the source file. If GDD.md doesn't exist, create it with a minimal header first.

Do NOT send implementation constraints to GDD.md. "Win condition: destroy the enemy Mothership" → CONTEXT.md. "Ships are the primary combo pieces" → GDD.md.

## ADR escape hatch

Before flagging any cut, ask: is this (1) hard to reverse, (2) surprising without context, and (3) the result of a real trade-off?

If **all three**: don't cut. If the topic is absent from the topic map, draft an ADR stub and suggest moving. If it's already in the topic map, mark **ALREADY IN ADR** — safe to cut.

## Pass 1: Form

Rewrite — don't just flag. Provide the replacement so the user can copy-paste directly.

- **Passive explanations** → imperatives. "We tend to use X" → "Use X."
- **Prose where bullets work** → rewrite as bullets (lists of rules, steps, options).
- **Verbose phrasing** → strip filler. Show the rewritten line.
- **Meta-commentary** → delete. "This section covers..." adds nothing.
- **Justifications without decision value** → cut if removing it doesn't change how Claude acts. Apply ADR escape hatch first.

## Pass 2: Content

Apply the **coding impact test** to every section, paragraph, or bullet: *If I removed this, would Claude write different code today?*

Flag as cut candidates if any of these are true:
- **TODO / deferred** — "revisit after playtesting," unimplemented features, open questions. Claude can't act on them; they only age badly.
- **Lore and flavor** — narrative framing, example names, bios, story rationale. Apply GDD escape hatch first — move, don't cut.
- **Placeholder numbers** — costs, multipliers, slot counts marked as first-pass guesses or to-be-tuned. Misleads more than it guides.
- **Specs for unbuilt systems** — full sections on systems the code doesn't implement yet. If the code doesn't exist, the spec is noise.
- **Justifications that don't constrain** — "we chose X because Y" where Y doesn't prevent any alternative implementation. Belongs in a commit message, not a context file.
- **Duplication** — anything restating what an ADR, linked file, or the code itself already captures.
- **Ambiguous rules without examples** — flag for a good/bad snippet.
- **Defensive "don't" rules with no clear trigger** — ask if the mistake has actually occurred. Apply ADR escape hatch first.
- **Stale references** — file paths or decisions that may no longer match the codebase.

Do NOT flag:
- Core invariants (determinism rules, tick rate, coordinate space, win conditions)
- Mechanics the code already implements
- ADR-covered decisions — apply the ADR escape hatch first

## Pass 3: Structure

- **Contradictions** → list conflicting rule pairs explicitly.
- **Concern-mixing** → stable conventions and volatile state in one file → suggest splitting.
- **Section bloat** (>15 lines) → check topic map. If in ADR, cite and cut. If ADR-worthy, draft stub. Otherwise trim in-place.
- **CLAUDE.md length** → if >60 lines, treat every line past 30 as a candidate. Always/Never behavioral rules move to `docs/CONVENTIONS.md` first. Show the exact lines and the block they become.

## Grill loop

After all three passes, present findings as a numbered list. Each finding: type (**CUT** / **MOVE TO GDD** / **MOVE TO ADR** / **ALREADY IN ADR**) and a one-line reason. For MOVE TO ADR, include a ready-to-save stub using the next available ADR number. For MOVE TO GDD, name the section of `docs/GDD.md` it belongs in. End with: "Found N findings. Walk through them one at a time?"

For each finding:
1. Quote the relevant lines (max 5 — truncate longer blocks).
2. State which pass/rule it violates and the finding type.
3. **Recommendation** with one sentence of reasoning.
4. Ask: "Apply it?"

Wait for the answer:
- **"yes"** → edit the file immediately, confirm done, next.
- **"no"** → move on, no argument.
- **"shrink it"** → offer a specific rewrite (max 2 lines), apply on confirmation.

After the last finding: state before/after line count for each file touched, and count of ADR stubs drafted.

## The posture

Default is cut. If uncertain, recommend cutting and let the user override. A short context file that's wrong about one thing is easier to fix than a long one that's right about everything.
