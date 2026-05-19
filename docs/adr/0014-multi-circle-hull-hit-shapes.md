---
status: accepted
---
# Multi-circle hull hit shapes

Each hull class is assigned a list of circles (`ox`, `oy`, `r` in ship-local space) rather than a single circle. Circles rotate with the ship's heading. The alternative — one circle per hull class — is simpler but represents elongated hulls (Battlecruiser, Dreadnought) as fat circles that don't match their visual footprint. Multi-circle allows a Battlecruiser to be two overlapping circles staggered along the forward axis, making it a correct obstacle to projectiles and beams threading past it. All current ships are configured symmetrically (left-right); the 2D offset format is chosen over a 1D forward-axis-only format to leave room for asymmetric hulls without a schema change. Mine proximity triggers and explosion radius damage use ship center only — explosion radii are large enough that the multi-circle precision is negligible there.
