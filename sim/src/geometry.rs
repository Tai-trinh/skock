use fixed::types::I32F32;

use crate::state::Pos2;

pub fn dist_sq(a: &Pos2, b: &Pos2) -> I32F32 {
    let dx = a.x - b.x;
    let dy = a.y - b.y;
    dx * dx + dy * dy
}

/// Minimum squared distance from `point` to the segment `seg_a → seg_b`.
/// Used for swept-segment projectile hit detection.
pub fn min_dist_sq_to_segment(point: &Pos2, seg_a: &Pos2, seg_b: &Pos2) -> I32F32 {
    let abx = seg_b.x - seg_a.x;
    let aby = seg_b.y - seg_a.y;
    let seg_len_sq = abx * abx + aby * aby;

    if seg_len_sq == I32F32::ZERO {
        // Degenerate segment — just return distance to the point
        return dist_sq(point, seg_a);
    }

    // Project point onto segment: t = dot(AP, AB) / dot(AB, AB), clamped to [0, 1]
    let apx = point.x - seg_a.x;
    let apy = point.y - seg_a.y;
    let dot = apx * abx + apy * aby;

    let t = if dot <= I32F32::ZERO {
        I32F32::ZERO
    } else if dot >= seg_len_sq {
        I32F32::ONE
    } else {
        dot / seg_len_sq
    };

    let closest_x = seg_a.x + abx * t;
    let closest_y = seg_a.y + aby * t;

    let dx = point.x - closest_x;
    let dy = point.y - closest_y;
    dx * dx + dy * dy
}
