# Expressions over statements; functional style where it earns its place

## Functional style — general principle

Prefer a functional style (transform data through expressions, avoid shared mutable state, compose small pure functions) over imperative step-by-step mutation. Functional code tends to be easier to reason about because each step has no hidden dependency on external state.

This is a preference, not a rule. Break it when:
- The functional version requires reading the same collection more than once — pay the cost of one extra pass only if clarity clearly wins.
- Deep recursion would replace a simple loop — prefer iteration; this codebase has no TCO guarantee in C# and finite stack in both languages.
- The resulting chain is harder to read than the equivalent loop — elegance is the goal, not functional purity.

The test: would a reader understand the intent faster than the imperative alternative? If yes, use the functional form. If not, use the loop.

## Rust

Prefer expression-oriented style: let bindings that resolve to a value, `match` arms that return, `if let` chains, and iterator adapters (`map`, `filter`, `fold`, `collect`) over imperative statement sequences. This keeps logic local, reduces mutable intermediate state, and makes data flow obvious at a glance.

Use `for` loops instead of iterator chains when the loop body has side effects, mutates external state, or when an early `continue`/`break` makes the intent clearer than a chain of adapters. Forcing a side-effectful loop into an iterator chain obscures intent and gains nothing.

The dividing line: **no mutation of external state inside a `map` or `filter` closure**. If the body needs `mut`, reach for a `for` loop.

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

## C# / .NET

Prefer the ternary operator over `if` statements when assigning a value. Prefer LINQ over `foreach` when transforming or filtering without side effects. The same dividing line applies: if the body mutates external state, use `foreach`.

Use `const` for any value that is a compile-time constant. Use `static readonly` for values computed once at class load time. Use `readonly` for instance fields set only in the constructor. Never use a mutable field or variable when the value never changes after assignment — the immutability is part of the documentation.

```csharp
// Good
private const float SimScale = 1f;
private static readonly Color FleetAColor = new(0.35f, 0.55f, 1f);
private readonly Dictionary<uint, ShipNode> _shipNodes = [];

// Avoid — mutable when it never changes
private float SimScale = 1f;
private Color FleetAColor = new(0.35f, 0.55f, 1f);
```

Prefer `switch` expressions over `switch` statements when returning a value — they enforce exhaustiveness and read as data, not control flow.

```csharp
// Good — ternary
var damage = crit ? base * critMultiplier : base;

// Avoid — if statement for a simple value assignment
int damage;
if (crit) damage = base * critMultiplier;
else damage = base;

// Good — LINQ, no side effects
var survivors = ships.Values.Where(s => s.Hp > 0).ToList();

// Good — foreach with side effects
foreach (var id in shipIds)
    ApplyDamage(state, id, dmg);

// Good — switch expression
var tonnage = hullClass switch
{
    "Corvette" => 2,
    "Frigate"  => 4,
    "Destroyer" => 6,
    _ => 2,
};

// Avoid — switch statement returning a value
int tonnage;
switch (hullClass) { case "Corvette": tonnage = 2; break; ... }
```
