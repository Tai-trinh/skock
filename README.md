# Skock

Top-down 2D space fleet auto-battler roguelite. Fleets fight using boids-based movement. Fully deterministic — same seed + fleet = byte-identical replay every time.

## Prerequisites

Run once on a fresh Windows machine:

```powershell
make install-win
```

This installs: Visual Studio C++ build tools, Rust (stable), .NET 8 SDK, Godot 4 (Mono/C# edition), and restores NuGet packages for the client.

## Running the Godot client

### 1. Build the sim binary

The Godot client launches the sim as a subprocess. Build it first:

```bash
make win-build
```

Output: `target/release/skock-sim.exe`

### 2. Open the project in Godot

1. Launch **Godot Engine (.NET)** — the Mono edition installed by `install-win`
2. Click **Import** → navigate to `client/` → select `project.godot` → **Open**
3. Godot will import assets and open the editor

### 3. Build the C# project

In the Godot editor toolbar: **Build** (the hammer icon, top-right) or press **Alt+B**.

If the build fails with "missing NuGet packages", open a terminal in `client/` and run:

```bash
dotnet restore
```

Then build again in the editor.

### 4. Run

Press **F5** (or the Play button) to run the Battle scene.

The renderer will launch the sim binary with a test seed, parse the battle log, and play back the battle at 30 ticks/sec with interpolation.

### Controls

| Key | Action |
|-----|--------|
| Space | Toggle 1× / 4× playback speed |
| F1 | Toggle debug overlay (raw tick state for selected ship) |

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

## Code layout

```
sim/        Rust — headless battle simulation (all game logic)
types/      Rust — shared types between sim and server
client/     Godot 4 + C# — renderer and future meta-layer (shop, fleet builder, run map)
docs/adr/   Architectural decision records
```
