---
status: accepted
---
# Beam dual tracking rates

Beam weapons use two angular velocities: `slew_rate` (fast) when the current ray is not hitting any enemy, and `track_rate` (slow) when the ray is firing on an enemy. The alternative — a single constant tracking rate — either makes acquisition too slow (if the rate is low) or makes the weapon too easy to hold on a fast target (if the rate is high). The dual-rate model decouples acquisition speed from tracking difficulty: the turret swings quickly to find a target but slows once it locks, making fast ships a meaningful counter to beam weapons. The rate switch triggers on any enemy in the ray (not just the intended target), so a ship flying into the beam's path naturally causes the beam to slow and linger — an emergent risk for clustering ships near an active beam.
