---
name: tais-context-slasher
description: Aggressively cuts CONTEXT.md, CLAUDE.md, and every md file they reference, removing content that doesn't change how Claude writes code today. Compiles candidates, then walks through them one at a time for approval before applying edits. Use when the user wants to prune, trim, or remove fluff from AI instruction files, or mentions that context files are getting too long.
---

# Context Slasher

Read every instruction file in scope, compile a ruthless list of cut candidates, then walk through them interactively — one at a time — applying cuts on approval.

## Step 1: Read everything in scope

1. Read `CLAUDE.md` and `CONTEXT.md`
2. Collect every `[text](path.md)` link from both files
3. Read each linked file
4. Read `docs/adr/` if it exists — build a topic list so you know what's already captured elsewhere

## Step 2: Build the cut list

For every file, apply one test to every section, paragraph, or bullet:

> **The coding impact test:** If I removed this, would Claude write different code today?

Flag as cut candidates if any of these are true:
- **TODO / deferred** — "revisit after playtesting," unimplemented features, open questions. Claude can't act on them; they only age badly.
- **Lore and flavor** — narrative framing, example names, bios, story rationale. The story doesn't change function signatures.
- **Placeholder numbers** — costs, multipliers, slot counts marked as first-pass guesses or to-be-tuned. Misleads more than it guides.
- **Specs for unbuilt systems** — full sections on morale, meta-progression, status effects, shopping, etc. If the code doesn't exist, the spec is noise.
- **Justifications that don't constrain** — "we chose X because Y" where Y doesn't prevent any alternative implementation. Belongs in a commit message, not a context file.
- **Duplication** — anything restating what an ADR, linked file, or the code itself already captures.

Do NOT flag:
- Core invariants (determinism rules, tick rate, coordinate space, win conditions)
- Mechanics the sim already implements (weapon archetypes, boid forces, event types, ship fields)
- ADR-covered decisions — confirm before cutting

Present the full list, numbered, one-sentence justification each. End with: "Found N candidates. Walk through them one at a time?"

## Step 3: Grill loop

Walk candidates one at a time. For each:

1. Quote the text (max 5 lines — truncate longer blocks)
2. One sentence: why it fails the coding impact test
3. **Recommendation: CUT** or **KEEP**, with a single reason
4. Ask: "Cut it?"

Wait for the user's answer:
- **"yes" / "cut"** → edit the file immediately, confirm done, next candidate
- **"no" / "keep"** → move on, no argument
- **"shrink it"** → offer a specific rewrite (max 2 lines), apply on confirmation

After the last candidate: state the before/after line count for each file touched.

## The posture

Default is cut. If uncertain, recommend cutting and let the user override. A short context file that's wrong about one thing is easier to fix than a long one that's right about everything — including things Claude didn't need to know.
