# ADR-0001: Deterministic containers in game rule crates

**Status:** Accepted

Any Rust crate that must produce deterministic output — currently `sim` and `dockyard`, and the server when it runs their logic — must use ordered containers only: `BTreeMap` and `BTreeSet` from the standard library, or plain arrays/vecs indexed by a stable ID. Hash maps and hash sets iterate in non-deterministic order in Rust (and most languages), making them a silent source of desyncs that are extremely difficult to track down after the fact.

`HashMap` and `HashSet` are permitted only in code that does not need to produce deterministic game output: server HTTP infrastructure, tooling, and the C#/Godot renderer. See ADR-0009 for the principle that Rust is the single source of truth for game rules — any crate that implements game rules inherits this constraint.
