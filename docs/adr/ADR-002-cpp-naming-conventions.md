# ADR-002: C++ Naming Conventions

Status: Accepted

## Context

Consistent naming makes code scannable without IDE support and signals intent (ownership, scope, type category) through the name alone. Without an agreed standard, contributors default to personal style and the codebase accumulates inconsistency.

## Decision

Follow standard C++ community conventions:

| Construct | Convention | Example |
|---|---|---|
| Types (class, struct, enum) | PascalCase | `PokerHand`, `StratBundle` |
| Functions and methods | camelCase | `bestHandOutOf7`, `getStratForHistory` |
| Local variables | camelCase | `normalSum`, `currentAction` |
| Class member variables | `m_` prefix + camelCase | `m_regretSum`, `m_strategySum` |
| Struct member variables | camelCase (no prefix) | `regretSum`, `strategySum` |
| Static member variables | `s_` prefix + camelCase | `s_instanceCount` |
| Constants and `constexpr` | ALL_CAPS with underscores | `LEFT_WIN`, `BIT_ACE` |
| Enum values | PascalCase or ALL_CAPS | `RoyalFlush`, `History::none` |
| Macros | ALL_CAPS with underscores | `ZERO`, `ONE` |
| Namespaces | snake_case or PascalCase | `poker`, `CardOps` |
| Template parameters | PascalCase | `T`, `ValueType` |

## Rationale

- `m_` prefix on class members instantly distinguishes encapsulated state from local variables — no need to look up the class definition to know a variable's lifetime. Structs are plain data holders with public members by default, so the prefix adds noise without benefit
- camelCase for functions matches the dominant convention in CUDA and GPU compute codebases, and is consistent with the CUDA runtime API style
- PascalCase for types follows the C++ standard library convention and distinguishes types from values at a glance
- ALL_CAPS for macros and constants follows universal C/C++ convention and warns readers of potentially unsafe substitution or compile-time-only values

## Consequences

- Existing code (`strategy.h`, `poker_compare.h`, etc.) uses bare member names without `m_` — apply the convention to new code and opportunistically when refactoring existing code, not as a forced mass rename
- `History` enum values use lowercase (`none`, `a`, `b`) as short action identifiers — these are an accepted local exception given their domain semantics as terse action tokens
