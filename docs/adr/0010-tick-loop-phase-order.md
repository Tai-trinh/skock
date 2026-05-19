---
status: accepted
---
# Tick loop phase order

The 12-phase order (increment tick → continuous effects → rebuild spatial grid → boid forces → integrate positions → weapon firing → advance projectiles → beam damage → damage resolution → end condition → attrition → write log) is part of the determinism contract. Phases are ordered so each reads state written by the previous phase; reordering silently breaks both determinism and combat math. Key constraints: the spatial grid must be rebuilt before forces are computed each tick because ships move; damage must be applied after all damage sources resolve in that tick (beams, projectiles, hitscan all complete before the damage phase runs); end-condition check runs after damage so a tick where both Motherships drop to 0 HP produces a draw, not a false win.
