# ADR-006: Clang-Format as the Shared C++ Formatter

Status: Accepted

## Context

Without a shared formatting rule, every contributor formats code differently. Diffs accumulate whitespace noise, code reviews get derailed by style debates, and merge conflicts are more frequent than they need to be. CUDA/C++ code in this repo mixes template declarations, kernel launch syntax, and initializer lists — all areas where manual formatting choices diverge quickly.

## Decision

All C++ and CUDA source files are formatted with **clang-format**, using the `.clang-format` file at the repository root as the single source of truth. Key settings in the current config:

| Setting | Value | Effect |
|---|---|---|
| `BasedOnStyle` | LLVM | Conservative baseline |
| `ColumnLimit` | 150 | Avoids wrapping on wide monitors |
| `IndentWidth` | 2 | Matches LLVM/Google style |
| `PointerAlignment` | Left | `T* ptr`, not `T *ptr` |
| `BreakConstructorInitializers` | BeforeComma | `: m_ptr(...)` style |
| `UseTab` | Never | Spaces only |

Changes to formatting rules are made by editing `.clang-format` and are applied project-wide — not negotiated file by file.

### Disabling clang-format locally

When clang-format would damage readability — aligned column layouts, hand-tuned tables, or `combos[]` arrays — wrap the block with guard comments:

```cpp
// clang-format off
uint64_t combos[21] = {
  c7|c6|c5|c4|c3, c7|c6|c5|c4|c2, c7|c6|c5|c3|c2,
  c7|c6|c4|c3|c2, c7|c5|c4|c3|c2, c6|c5|c4|c3|c2,
  ...
};
// clang-format on
```

Rules for using the guard:
- The off/on block must be as small as possible — one expression, one array, one table.
- It requires a brief comment explaining why auto-formatting would hurt readability here.
- It is never used to preserve accidental formatting or to avoid applying the shared style.

## Rationale

- A machine-enforced style eliminates formatting debates entirely — there is nothing to argue about.
- `.clang-format` is versioned alongside the code, so every contributor and CI job uses the same rules.
- The `// clang-format off` escape hatch preserves the cases where structure genuinely conveys meaning, without relaxing the rule everywhere.

## Consequences

- Contributors must run clang-format before committing, or configure their editor to do so on save.
- CI should reject PRs where `clang-format --dry-run --Werror` reports a diff.
- New formatting preferences are proposed as a `.clang-format` change, reviewed like any other code change.
