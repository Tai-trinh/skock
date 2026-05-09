# ADR-004: Const Correctness

Status: Accepted

## Context

In CUDA/C++ code it is often hard to tell at a glance whether a variable, parameter, or pointer is ever mutated. Without a consistent use of `const`, a reader must trace all uses of a name to determine whether it is safe to treat it as stable. This slows down code review, makes bugs easier to introduce, and reduces the signal value of a mutable declaration.

## Decision

Mark every variable, parameter, and pointer that is not intentionally mutated as `const`.

| Situation | Rule | Example |
|---|---|---|
| Local variable whose value is set once | `const` | `const int n = count();` |
| Function parameter passed by value that is not modified | `const` | `void f(const int x)` |
| Reference or pointer parameter that is not written through | `const` on the pointee | `void f(const uint64_t* cards, int n)` |
| Return value stored in a variable that is never reassigned | `const` | `const auto result = compute();` |
| Member function that does not modify the object | `const` on the method | `int size() const;` |

`const` is **not** added when:
- The variable is explicitly intended to be mutated (loop counters, accumulators, out-parameters).
- A `curandState*` is passed to device functions — cuRAND updates it in place by design.

## Rationale

- A non-`const` declaration becomes a deliberate signal: "this value changes." That contrast is only meaningful if `const` is the default for everything else.
- `const` enables compiler optimisations and catches accidental writes at compile time.
- It eliminates the need to scan all downstream uses before reasoning about a value's stability.

## Consequences

- New code must apply `const` by default and omit it only where mutation is intentional.
- Existing code is grandfathered until touched; apply `const` opportunistically when editing a function.
- `constexpr` is preferred over `const` for values that are truly compile-time constants (e.g. `constexpr int NUM_TRIALS = 10000`).
