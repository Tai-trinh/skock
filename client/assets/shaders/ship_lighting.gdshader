shader_type canvas_item;

// Directional light fixed in world space, arriving from the top-left at 45°.
// The ship's world rotation is passed each frame so the UV offset can be
// rotated into world space without relying on MODEL_MATRIX (unavailable in
// the gl_compatibility renderer).
uniform float light_strength : hint_range(0.0, 1.0) = 0.4;
uniform float ship_rotation = 0.0;

void fragment() {
    vec4 col = texture(TEXTURE, UV);

    // Rotate UV offset into world space using the ship's current rotation.
    vec2 local = UV - 0.5;
    float c = cos(ship_rotation);
    float s = sin(ship_rotation);
    vec2 world = vec2(local.x * c - local.y * s, local.x * s + local.y * c);

    // Light arrives from top-left: in Godot 2D (Y+ down) that is (-1, -1).
    // Divide by 0.7071 to normalise the projection to [-1, 1].
    float lit = dot(world, normalize(vec2(-1.0, -1.0))) / 0.7071;
    float shade = clamp(1.0 + lit * light_strength, 0.1, 2.0);

    COLOR = vec4(col.rgb * shade, col.a);
}
