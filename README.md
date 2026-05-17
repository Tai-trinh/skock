# Skock

Top-down 2D space fleet auto-battler roguelite. Fleets fight using boids-based movement. Fully deterministic — same seed + fleet = byte-identical replay every time.

## Prerequisites

Run once on a fresh Windows machine:

```powershell
make install-win
```

This installs: Visual Studio C++ build tools, Rust (stable), .NET 8 SDK, Godot 4 (Mono/C# edition), and restores NuGet packages for the client.

## Common tasks

| Task | Command |
|------|---------|
| Build sim (Windows) | `make win-build-sim` |
| Build C# client (Windows) | `make win-build-godot` |
| Open Godot editor | `make start-godot` |
| Run determinism tests | `make det` |
| Build sim (Docker) | `make docker-build-sim` |
| Build C# client (Docker) | `make docker-build-godot` |
| Interactive Docker shell | `make docker-run` |

## Running the Godot client

### 1. Build the sim binary

The Godot client launches the sim as a subprocess. Build it first:

```bash
make win-build-sim
```

Output: `target/release/skock-sim.exe`

### 2. Open the project in Godot

```bash
make start-godot
```

This finds the Godot Mono executable installed by winget and opens the project directly. On first launch Godot will import assets before opening the editor.

### 3. Build the C# project

```bash
make win-build-godot
```

Or inside the Godot editor: **Build** (hammer icon, top-right) / **Alt+B**.

### 4. Run

Press **F5** (or the Play button) to run the Battle scene.

The renderer launches the sim binary with a test seed, parses the battle log, and plays back the battle at 30 ticks/sec with interpolation.

### Controls

| Key | Action |
|-----|--------|
| Space | Toggle 1× / 4× playback speed |
| F1 | Toggle debug overlay |

## Running the sim directly

```bash
cargo run -p sim -- --seed 42 sim/tests/fixtures/fleet_a.json sim/tests/fixtures/fleet_b.json
```

Writes MessagePack battle log to stdout, result JSON to stderr.

## Determinism tests

```bash
make det
```

Hashes a fixed corpus of battles and asserts against golden SHA-256 values. Run after any sim logic change.

## Docker

```bash
make docker-engine-start   # start Docker daemon (WSL)
make docker-image          # build the image (first time or after Dockerfile changes)
make docker-run            # interactive shell with GPU and display forwarding
make docker-build-sim      # cargo build --release inside container
make docker-build-godot    # dotnet build inside container
```

## Releases

Every push to `master` automatically builds and uploads a dev build to the [GitHub Releases page](../../releases/tag/dev) (tagged `dev`, marked pre-release). Download `skock-windows.zip`, extract, run `skock.exe`.

To cut a versioned release:

```bash
git tag v0.1.0
git push origin v0.1.0
```

GitHub Actions builds the release and creates a `v0.1.0` entry on the releases page.

## CI

Pull requests and pushes to `master` run:

- `cargo fmt --check` + `cargo clippy -D warnings` + `cargo test` (includes determinism golden-hash tests)
- `dotnet build` (C# type check)

## Code layout

```
sim/                  Rust — headless battle simulation (all game logic)
types/                Rust — shared types between sim and server
client/               Godot 4 + C# — renderer and meta-layer (shop, fleet builder, run map)
.github/workflows/    CI (ci.yml) and release pipeline (release.yml)
docs/adr/             Architectural decision records
```
