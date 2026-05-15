# Technical debt strategy, code quality, and elegance

## Core principle

Code is read far more than it is written. The standard for every piece of code is: **would a fresh reader understand the intent immediately, without asking why?** If not, it needs work before it ships — not later.

Technical debt is allowed to accumulate in two places only: **explicitly marked TODOs** and **deferred ADRs**. Everything else ships clean or not at all.

## Naming

Names are the first line of documentation. A well-named function needs no comment.

- Names describe **what**, not **how**: `resolve_hitscan`, not `do_damage_loop`
- No abbreviations except established domain terms (`hp`, `rng`, `vel`) and types (`u32`, `I16F16`)
- Boolean names answer a yes/no question: `is_mothership`, `has_shield`, not `mothership`, `shield`
- Functions that return `bool` start with `is_`, `has_`, or `can_`
- Avoid noise words: `data`, `info`, `manager`, `handler`, `util`

## Functions

A function should do one thing. The test: can you describe it in one sentence without using "and"?

- Prefer short functions. If a function doesn't fit on a screen, consider splitting it
- Functions that return a value are easier to test and compose than functions that mutate state as a side effect — prefer the former where it doesn't create awkward ownership
- Parameter count above 4 is a signal the function is doing too much or that a struct would be cleaner

## Design before optimisation

The right data structure beats the fastest algorithm on the wrong one. Before writing any non-trivial system, spend time on the data model:

- What is the natural representation of this domain concept? A `Ship` is not a row of floats — it has identity, state, and behaviour. Model it that way.
- Does the structure make illegal states unrepresentable? Prefer types that encode constraints (`NonZeroU32` over `u32` for IDs, enums over stringly-typed flags).
- Does the structure make the common case cheap and the edge case possible? A `BTreeMap<ShipId, Ship>` makes per-ship lookup O(log N) and iteration ordered — both useful properties for this domain.

A poor data model produces code that fights the domain at every turn. No amount of micro-optimisation recovers the clarity lost to a wrong abstraction. Get the model right first; optimise the implementation only when profiling identifies a real bottleneck.

When the data model is clean, the algorithms tend to write themselves.

## Elegance over cleverness

Elegant code is the simplest code that correctly solves the problem. Clever code optimises for the author's satisfaction at the reader's expense.

- If a solution requires a comment to explain *what* it does (not *why*), it is too clever — simplify it
- Prefer the obvious algorithm until profiling proves it's a bottleneck (`TODO(perf):` to mark the spot)
- Iterator chains (see ADR-0003) are elegant when they read like a sentence; they are clever when they require tracing through four nested closures to understand

## Comments

Comments explain **why**, never **what**. The code explains what.

Write a comment when:
- A constraint is non-obvious (`// xoshiro256+ requires explicit state — no global RNG in sim`)
- A workaround exists for a specific bug or platform quirk
- An invariant must hold that isn't enforced by the type system

Never write:
- Comments that restate the code (`// increment counter` above `count += 1`)
- Docstrings on trivial getters
- Section dividers (`// --- helpers ---`) as a substitute for splitting a file

## TODOs

Mark debt with `// TODO(scope): description` where scope is one of:

- `perf` — known inefficiency acceptable now (e.g. O(N²) boid search)
- `feature` — placeholder for future functionality
- `cleanup` — awkward code that works but should be revisited

No `FIXME`, `HACK`, or bare `TODO` without a scope. A scoped TODO is a decision; a bare TODO is noise.

TODOs are reviewed at the start of each new system being built — not on a calendar. Delete stale TODOs. Resolve blocking TODOs before starting the system that needs them.

## What is never allowed

- Dead code left behind "just in case" — delete it; git history exists
- Commented-out code blocks
- `unwrap()` in sim code on paths that can fail at runtime — use `expect("invariant: ...")` with an explanation, or propagate the error
- `clone()` as a shortcut when a borrow would work — flag it with `// TODO(perf): clone`
- Magic numbers without a named constant

## Self-review checklist (solo project)

Before committing any non-trivial change:

1. Compiles with no warnings (`cargo build` / `dotnet build`)?
2. Determinism tests pass (`make det`)?
3. Any new bare `unwrap()` in sim code?
4. Any name that made you pause — is it the right name?
5. Any function longer than ~40 lines — does it need splitting?

## When to pay debt

Pay debt when it blocks forward progress or makes a new system harder to understand — not on a schedule. Resist refactoring before shipping the next playable milestone. Good code today is worth more than perfect code never.
