# Skock - space fleet auto battler using boids movement

The game is a rougelike/lite. It will have a semi multiplayer mode where you can auto battle others fleets. The battles are deterministic, so replaying the results locally should yeild the same results for both players and or verification-server (for a given seed). The multi play part is just retrieving opponents from the the server to get a fleet to auto battle.

Game story is a homage to Gallforce and homeworld. It is a story of fleet hyperspace jumping from place to place to scavange and survive

* The game will be top down 2d, but use 3d engine/textures as sprites to easier rotate the graphics
* The game will use boids for flock movement of the ships
* The ships should have inertia or something to simulate space movement
* The ships should have acceleration attribute
* The game should run in 30 ticks/sec and a simulated battle should always end after 120sec.
* The game should be fully deterministic given the random seed
* The art style should be anime like macross and gallforce
* A game session should hopefully take 15-30 min
* Run state lives on the server
* Seeded run generation
* Encounter database. We will have currated opponent fleets that always exist for players to face
* Single threaded

1) A headless simulation runner. Command-line tool that takes a seed + two fleet JSONs and prints the result + a tick-by-tick log. Lets you write tests, debug desyncs, run balance simulations (Monte Carlo over 10,000 battles to see win rates per ship type).

2) A determinism CI test. On every commit, run a fixed corpus of (seed, fleetA, fleetB) battles and assert the result hash matches a golden file. The day this test breaks, you know exactly which commit introduced non-determinism. Without this, desyncs become impossible to debug because they accumulate over weeks.

Game loop:

The game consist of 8jumps+, you face 8 fleets.

Shopping mode:
1) you start with one ship the Mothership and you only have one, your mothership has logistic limiting fleet size.
2) You repair/build/scrap new ships/squadrons to add to the fleet using resources
3) you buy weapon/equipment upgrades that apply to all ships/squadrons.
4) the screen of weapons and tech is randomized, and it costs to reroll the table.
Battle mode:
5) Facing a fleet, you will face a fleet of of equal level and has the same win loss ratio as you (1jump, 2jump, 3jump, 4jump ... )
6) you hyper space jump in your fleet on the left opponent on the right
7) The fleet battles it out, if the simulation takes longer than 60s attrition will start to kick in ships take 1% damage every second increasing with 1% every second. And if it takes longer than 120sec both fleet warp out and it is considered a victory for both/draw.
8) Winning/loosing for that jump round gives you resources
9) your fleet is healed to full, and the game loop repeats until the 8th round or you have lost 3 rounds
10) optional you can continue after the 8th round but get minimal resources and game over as soon as you loose to any fleet.


The server side
Embarrassingly simple by comparison:

* Stateless HTTP API, not real-time. REST or gRPC, doesn't matter.
* Postgres (or SQLite if you're tiny). Tables: users, fleets, battle_results, runs, leaderboards.
* Endpoints: register/login, save fleet, fetch opponent (random, or matched by ELO/run-progress), submit battle result, fetch leaderboard.
* Server language: whatever you're comfortable with. Go, Rust + axum, Python + FastAPI, Node + Fastify, C# ASP.NET. The load is trivial — a single small VM serves thousands of concurrent users for this kind of game.


## Language
Concrete recommendation
Engine:           Godot 4 with C#  (or Bevy + Rust if you prefer)
Sim language:     Pure C# (or Rust) — engine-agnostic module
Math:             Fixed-point (32.32 for positions, 16.16 for most else)
RNG:              PCG or xoshiro256**, explicit state, no globals
Containers:       SortedDictionary / BTreeMap / arrays-by-ID only
Tick rate:        30Hz logical, render interpolates
Boids:            SoA + uniform spatial grid, ≤16 neighbors per ship
Server lang:      Whatever you're fastest in (Go / Rust+axum / Python+FastAPI)
Server DB:        Postgres
Server protocol:  REST/JSON, async only, no real-time
Anti-cheat:       Server re-simulates sampled battles using same sim code
Replays:          Store seed + value-snapshot of both fleets
Tooling:          Headless sim runner + determinism CI test from day one



## Build order

1. Headless deterministic boids sim in pure C#/Rust. No graphics. Two fleets, fly at each other, shoot, one wins. Tick log to stdout. Test: run twice, diff output, must be byte-identical.
2. Determinism CI test. Lock in a corpus of battles with known result hashes. This now runs on every commit forever.
3. Engine layer. Godot/Unity scene that loads a battle log and renders it. Camera, ship sprites, projectile effects, victory screen.
4. Meta layer. Run map, shop, fleet builder, ship roster. All local first.
5. Local roguelike loop end-to-end. Single player, no server, full run start to finish. Playtest the hell out of this. The game lives or dies here.
6. Server. Account system, fleet upload, opponent fetch. Async multiplayer dropped onto the existing single-player game.
7. Verification. Server-side replay simulation as a worker.
8. Polish, balance, content.