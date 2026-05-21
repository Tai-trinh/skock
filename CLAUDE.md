# Skock

Top-down 2D space fleet auto-battler roguelite. Core invariant: **full determinism** — same seed + fleet snapshots → byte-identical sim output on any machine.

See [CONTEXT.md](CONTEXT.md) for domain model, [docs/adr/](docs/adr/) for architectural decisions.

## Commands

```
cargo.exe build -p sim --release   # build sim
cargo.exe test                     # all Rust tests
make det                           # determinism tests only
dotnet.exe build client/skock.csproj          # type-check client
dotnet.exe test client.tests/skock.tests.csproj  # C# tests
make fmt                           # format everything
```

See [Workflows](docs/WORKFLOWS.md) for full command reference and before-commit checklist.

## Docs

- [Architecture](docs/ARCHITECTURE.md) — components, tech stack, sim code design, client rendering, build order
- [API Contracts](docs/API-CONTRACTS.md) — dockyard binary protocol, sim↔client wire format, fleet JSON spec
- [Conventions](docs/CONVENTIONS.md) — sim determinism rules, writing and working conventions
- [Standards](docs/STANDARDS.md) — code style, naming, functions, comments, testing patterns
- [Workflows](docs/WORKFLOWS.md) — full build/test commands, CI/CD, before-commit checklist
- [GDD](docs/GDD.md) — game design reference (lore, art direction, mechanics intent); read on demand, not by default
