shader_type canvas_item;

float hash(vec2 p) {
    p = fract(p * vec2(443.897, 441.423));
    p += dot(p, p + 19.19);
    return fract(p.x * p.y);
}

void fragment() {
    vec2 px = FRAGCOORD.xy;
    float brightness = 0.0;

    // Layer 1 — dense dim stars (4px cells)
    {
        vec2 cell = floor(px / 4.0);
        float h = hash(cell);
        if (h > 0.94) {
            vec2 jitter = vec2(hash(cell + 0.3), hash(cell + 0.7)) - 0.5;
            float d = length(fract(px / 4.0) - 0.5 - jitter * 0.4);
            brightness = max(brightness, (1.0 - smoothstep(0.0, 0.25, d)) * mix(0.25, 0.65, hash(cell + 1.1)));
        }
    }

    // Layer 2 — sparse bright stars (16px cells)
    {
        vec2 cell = floor(px / 16.0);
        float h = hash(cell + 50.0);
        if (h > 0.965) {
            vec2 jitter = vec2(hash(cell + 10.3), hash(cell + 10.7)) - 0.5;
            float d = length(fract(px / 16.0) - 0.5 - jitter * 0.6);
            brightness = max(brightness, 1.0 - smoothstep(0.0, 0.3, d));
        }
    }

    COLOR = vec4(brightness, brightness, brightness, 1.0);
}
