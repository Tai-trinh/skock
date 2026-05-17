# Code standards

## Formatting

All Rust source files are formatted with `rustfmt` using the project's `rustfmt.toml` as the single source of truth. Run before committing, or configure your editor to format on save (`rust-analyzer` does this by default). CI rejects PRs where `cargo fmt --check` reports a diff.

When `rustfmt` would damage readability — aligned columns, hand-tuned tables, or bit-flag layouts — wrap the block with `#[rustfmt::skip]` on the item. The skip must be as narrow as possible and accompanied by a comment explaining why manual formatting is clearer here.

## Functional style

Prefer a functional style (transform data through expressions, avoid shared mutable state, compose small pure functions) over imperative step-by-step mutation. Functional code tends to be easier to reason about because each step has no hidden dependency on external state.

This is a preference, not a rule. Break it when:
- The functional version requires reading the same collection more than once.
- Deep recursion would replace a simple loop — prefer iteration; this codebase has no TCO guarantee in C# and finite stack in both languages.
- The resulting chain is harder to read than the equivalent loop — elegance is the goal, not functional purity.

The test: would a reader understand the intent faster than the imperative alternative?

### Rust

Prefer expression-oriented style: let bindings that resolve to a value, `match` arms that return, `if let` chains, and iterator adapters (`map`, `filter`, `fold`, `collect`) over imperative statement sequences.

Use `for` loops instead of iterator chains when the loop body has side effects, mutates external state, or when an early `continue`/`break` makes the intent clearer. **No mutation of external state inside a `map` or `filter` closure** — if the body needs `mut`, reach for a `for` loop.

```rust
// Good — expression binding
let damage = if crit { base * crit_multiplier } else { base };

// Good — iterator chain, no side effects
let survivors: Vec<_> = ships.values().filter(|s| s.hp > 0).collect();

// Good — for loop with side effects
for id in ship_ids {
    apply_damage(&mut state, id, dmg);
}

// Avoid — mutation inside map
let _ = ship_ids.iter().map(|id| apply_damage(&mut state, *id, dmg));
```

### C# / .NET

Prefer the ternary operator over `if` statements when assigning a value. Prefer LINQ over `foreach` when transforming or filtering without side effects. If the body mutates external state, use `foreach`.

Use `const` for compile-time constants, `static readonly` for values computed once at class load time, `readonly` for instance fields set only in the constructor. Never use a mutable field when the value never changes after assignment.

```csharp
// Good
private const float SimScale = 1f;
private static readonly Color FleetAColor = new(0.35f, 0.55f, 1f);
private readonly Dictionary<uint, ShipNode> _shipNodes = [];
```

Prefer `switch` expressions over `switch` statements when returning a value — they enforce exhaustiveness and read as data, not control flow.

## Godot scene design

Prefer composition over inheritance. Build behaviour by combining small, focused nodes rather than by extending a base class. A `ShipNode` that owns a `HealthBar` child is easier to reason about than a `ShipNode` that inherits from `HealthBarBase`. Inheritance is appropriate only for the Godot-required root (`Node2D`, `Control`, etc.) and for genuine is-a relationships — which are rare.

Corollary: if two scene types share behaviour, extract it into a reusable child node or a plain C# helper class, not a shared base class.

## When to extract shared code

The default is repetition. The domain is still changing; an abstraction built on an unstable model becomes an anchor.

**Extract when you see the same thing three times** — not two. Two similar functions is probably coincidence. Three is a pattern worth naming.

But structural similarity alone doesn't count. The test: *would a single change to the underlying rule require updating all three places?* If yes, the duplication is real — extract it. If changing one doesn't obligate changing the others, they are incidentally similar and should stay separate.

**Extracted code belongs to the layer that owns the concept.** Logic that touches run state lives in `src/meta/`. Logic that touches rendering lives in `src/rendering/`. If neither layer clearly owns the logic, that is a signal the concept needs a name and a home — not a `utils/` folder.

**`utils/`, `helpers/`, and `common/` are banned.** These names skip the ownership question. Every extracted type or function belongs to a specific domain concept; name the file after the concept.

## Naming

Names are the first line of documentation. A well-named function needs no comment.

- Names describe **what**, not **how**: `resolve_hitscan`, not `do_damage_loop`
- No abbreviations except established domain terms (`hp`, `rng`, `vel`) and types (`u32`, `I16F16`)
- Boolean names answer a yes/no question: `is_mothership`, `has_shield`
- Functions that return `bool` start with `is_`, `has_`, or `can_`
- Avoid noise words: `data`, `info`, `manager`, `handler`, `util`

## Functions

A function should do one thing. The test: can you describe it in one sentence without using "and"?

- Prefer short functions. If a function doesn't fit on a screen, consider splitting it.
- Functions that return a value are easier to test and compose than functions that mutate state as a side effect.
- Parameter count above 4 is a signal the function is doing too much or that a struct would be cleaner.

## Design before optimisation

The right data structure beats the fastest algorithm on the wrong one. Before writing any non-trivial system, spend time on the data model:

- Does the structure make illegal states unrepresentable? Prefer types that encode constraints (`NonZeroU32` over `u32` for IDs, enums over stringly-typed flags).
- Does the structure make the common case cheap and the edge case possible?

A poor data model produces code that fights the domain at every turn. Get the model right first; optimise the implementation only when profiling identifies a real bottleneck.

## Elegance over cleverness

Elegant code is the simplest code that correctly solves the problem.

- If a solution requires a comment to explain *what* it does (not *why*), it is too clever — simplify it.
- Prefer the obvious algorithm until profiling proves it's a bottleneck (`TODO(perf):` to mark the spot).
- Iterator chains are elegant when they read like a sentence; they are clever when they require tracing through four nested closures to understand.

## Comments

Comments explain **why**, never **what**. The code explains what.

Write a comment when:
- A constraint is non-obvious (`// xoshiro256+ requires explicit state — no global RNG in sim`)
- A workaround exists for a specific bug or platform quirk
- An invariant must hold that isn't enforced by the type system

Never write:
- Comments that restate the code
- Docstrings on trivial getters
- Section dividers (`// --- helpers ---`) as a substitute for splitting a file

## TODOs

Mark debt with `// TODO(scope): description` where scope is one of:

- `perf` — known inefficiency acceptable now (e.g. O(N²) boid search)
- `feature` — placeholder for future functionality
- `cleanup` — awkward code that works but should be revisited

No `FIXME`, `HACK`, or bare `TODO` without a scope. TODOs are reviewed at the start of each new system being built — not on a calendar. Delete stale TODOs. Resolve blocking TODOs before starting the system that needs them.

## What is never allowed

- Dead code left behind "just in case" — delete it; git history exists
- Commented-out code blocks
- `unwrap()` in sim code on paths that can fail at runtime — use `expect("invariant: ...")` with an explanation, or propagate the error
- `clone()` as a shortcut when a borrow would work — flag it with `// TODO(perf): clone`
- Magic numbers without a named constant

## Self-review checklist

Before committing any non-trivial change:

1. Compiles with no warnings (`cargo build` / `dotnet build`)?
2. Determinism tests pass (`make det`)?
3. Any new bare `unwrap()` in sim code?
4. Any name that made you pause — is it the right name?
5. Any function longer than ~40 lines — does it need splitting?

## When to pay debt

Pay debt when it blocks forward progress or makes a new system harder to understand — not on a schedule. Resist refactoring before shipping the next playable milestone.
