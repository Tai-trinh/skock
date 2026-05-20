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

#[cfg(test)]
mod tests {
    use super::*;

    fn p(x: f64, y: f64) -> Pos2 {
        Pos2::from_f64(x, y)
    }

    #[test]
    fn point_on_segment_midpoint_has_zero_dist() {
        let d = min_dist_sq_to_segment(&p(5.0, 0.0), &p(0.0, 0.0), &p(10.0, 0.0));
        assert_eq!(d, I32F32::ZERO);
    }

    #[test]
    fn point_perpendicular_to_segment_returns_squared_perpendicular_dist() {
        // Segment (0,0)→(10,0), point at (5,3) — closest point is (5,0), dist²=9
        let d = min_dist_sq_to_segment(&p(5.0, 3.0), &p(0.0, 0.0), &p(10.0, 0.0));
        assert_eq!(d, I32F32::from_num(9));
    }

    #[test]
    fn point_past_segment_end_clamps_to_endpoint() {
        // Segment (0,0)→(10,0), point at (15,0) — closest is (10,0), dist²=25
        let d = min_dist_sq_to_segment(&p(15.0, 0.0), &p(0.0, 0.0), &p(10.0, 0.0));
        assert_eq!(d, I32F32::from_num(25));
    }

    #[test]
    fn degenerate_segment_falls_back_to_point_distance() {
        // seg_a == seg_b == (0,0), point at (3,4) — dist²=25
        let d = min_dist_sq_to_segment(&p(3.0, 4.0), &p(0.0, 0.0), &p(0.0, 0.0));
        assert_eq!(d, I32F32::from_num(25));
    }
}
