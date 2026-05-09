# ADR-001: Prefer Expressions Over Statements

Status: Accepted

## Context

Code can express computation as statements (imperative, side-effect-driven) or expressions (value-producing, composable). Expressions return values; statements execute for effect.

## Decision

Prefer expressions over statements where language permits.

Examples:
- Ternary / conditional expression over `if`/`else` block when result assigned to variable
- Expression-bodied functions over multi-statement bodies for simple transforms
- `map`/`filter`/`reduce` over imperative loops when transforming collections
- Inline lambda/closure over named statement block for short-lived logic

## Rationale

- Expressions compose -> fewer temp variables, less mutable state
- Value-producing -> easier to reason about (no hidden side effects)
- Reduces cognitive load: reader sees result directly, not sequence of mutations

## Consequences

- Overly nested ternaries harm readability — break into named expression or fn instead
- Statement form acceptable when side effects are intentional and primary (logging, I/O, mutation)
- Don't force expression style on inherently imperative operations
