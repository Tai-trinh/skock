---
status: accepted
---
# Swept segment projectile collision

Projectile hit detection uses a swept segment test: the projectile's movement each tick is treated as a line segment `prev_pos → pos`, and a hit occurs when the minimum distance from any ship circle center to that segment is ≤ `(circle.radius + projectile.hit_radius)`. The alternative — point-in-circle per tick with a projectile speed cap to prevent tunneling — was the original approach. Rejected because multi-circle hull hit shapes (ADR-0014) include small circles (radius 4–8 units); the speed cap required to prevent tunneling through a small circle would make fast projectiles feel sluggish. The swept test eliminates the tunneling problem entirely and removes the speed cap constraint, at negligible cost for the sim's entity counts (< 100 ships, < 300 projectiles).
