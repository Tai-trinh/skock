# Expressions over statements; iterators over loops where appropriate

Prefer expression-oriented Rust style: let bindings that resolve to a value, `match` arms that return, `if let` chains, and iterator adapters (`map`, `filter`, `fold`, `collect`) over imperative statement sequences. This keeps logic local, reduces mutable intermediate state, and makes data flow obvious at a glance.

Use `for` loops instead of iterator chains when the loop body has side effects, mutates external state, or when an early `continue`/`break` makes the intent clearer than a chain of adapters. Forcing a side-effectful loop into an iterator chain obscures intent and gains nothing.

The dividing line: **no mutation of external state inside a `map` or `filter` closure**. If the body needs `mut`, reach for a `for` loop.

Examples of preferred style:

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
