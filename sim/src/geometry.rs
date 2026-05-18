use fixed::types::I32F32;

use crate::state::Pos2;

pub fn dist_sq(a: &Pos2, b: &Pos2) -> I32F32 {
    let dx = a.x - b.x;
    let dy = a.y - b.y;
    dx * dx + dy * dy
}
