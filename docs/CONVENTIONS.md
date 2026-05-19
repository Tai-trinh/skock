Sim determinism rules and working conventions for Skock.

## Sim determinism (never break these)

Breaking determinism is a silent, catastrophic bug. Given the same seed and fleet snapshots, every sim run must produce byte-identical output.

- **Fixed-point math only** — `fixed` crate: `I32F32` for positions, `I16F16` for most else. No floats in sim code.
- **Explicit RNG state** — xoshiro256+ (`rand_xoshiro`), 4× u64 state, no globals. State is serialized into fleet snapshots for replays.
- **Ordered containers only** — `BTreeMap`, `BTreeSet`, or arrays indexed by stable ID. Never `HashMap`/`HashSet` in `/sim`, `/dockyard`, or server game logic. See ADR-0001.
- **Single-threaded sim** — no parallelism inside a battle tick.
- `HashMap`/`HashSet` are allowed only in code that does not produce deterministic output: server HTTP infrastructure, tooling, C#/Godot renderer.

## Writing context and docs

- Imperatives, not explanations. "Use X" not "We use X because..."
- Bullets over prose.
- One good/bad example beats three paragraphs.
- No meta-commentary or section intros.

## Working in this repo

- Read only the files needed for the current task. Don't load the whole codebase.
- When a module stabilises, add a short `README.md` in its folder instead of expanding root context.
- If something here contradicts newer guidance in chat, ask.
