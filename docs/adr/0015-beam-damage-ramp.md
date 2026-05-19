---
status: accepted
---
# Beam damage ramp-up

Beam weapons deal `damage` per tick with an optional linear ramp: damage scales from base to `damage × ramp_max` over `ramp_ticks` ticks of continuous on-target contact. The ramp counter resets to zero on any tick the beam is not hitting an enemy. The alternative — flat damage per tick — is simpler but removes the "sustained aim" design axis. The ramp makes beam weapons feel distinct from hitscan: a player (or boid AI) that keeps a ship continuously in the beam path is rewarded with escalating damage, while a target that breaks contact frequently limits the attacker to near-base damage. Instant reset on losing target (vs. gradual decay) maximises the pressure to hold aim and keeps the formula fixed-point friendly.
