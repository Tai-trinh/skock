shader_type canvas_item;

// sin-based hash21 — numerically stable under mediump / gl_compatibility
float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

void fragment() {
    vec2 px = FRAGCOORD.xy;
    float brightness = 0.0;

    // Layer 1 — dense dim stars (4px cells)
    {
        vec2 cell = floor(px / 4.0);
        float h = hash(cell);
        if (h > 0.94) {
            vec2 jitter = vec2(hash(cell + vec2(0.3, 0.0)), hash(cell + vec2(0.0, 0.7))) - 0.5;
            float d = length(fract(px / 4.0) - 0.5 - jitter * 0.4);
            brightness = max(brightness, (1.0 - smoothstep(0.0, 0.25, d)) * mix(0.25, 0.65, hash(cell + vec2(1.1, 0.0))));
        }
    }

    // Layer 2 — sparse bright stars (16px cells)
    {
        vec2 cell = floor(px / 16.0);
        float h = hash(cell + vec2(50.0, 0.0));
        if (h > 0.965) {
            vec2 jitter = vec2(hash(cell + vec2(10.3, 0.0)), hash(cell + vec2(0.0, 10.7))) - 0.5;
            float d = length(fract(px / 16.0) - 0.5 - jitter * 0.6);
            brightness = max(brightness, 1.0 - smoothstep(0.0, 0.3, d));
        }
    }

    COLOR = vec4(brightness, brightness, brightness, 1.0);
}
