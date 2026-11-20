#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;

uniform sampler2D texture0;
uniform float u_Time;
uniform vec2 u_Resolution;

// Optional overrides (falls back to projectile's fragColor if not set)
uniform vec3 u_AccentColor;
uniform float u_FlipX;       // Set 1.0 if facing left (if not already flipped via srcRect)
uniform float u_ScrollSpeed; // Star/nebula drift speed (default 1.2)

float hash21(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

float noise2D(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + vec2(1.0, 0.0));
    float c = hash21(i + vec2(0.0, 1.0));
    float d = hash21(i + vec2(1.0, 1.0));
    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float fbm(vec2 p) {
    float v = 0.0;
    float amp = 0.5;
    mat2 rot = mat2(0.80, 0.60, -0.60, 0.80);
    for (int i = 0; i < 3; i++) {
        v += amp * noise2D(p);
        p = rot * p * 2.04 + vec2(13.1, 7.7);
        amp *= 0.5;
    }
    return v;
}

float starField(vec2 uv, float density, float threshold) {
    vec2 id = floor(uv * density);
    vec2 gv = fract(uv * density) - 0.5;
    float rnd = hash21(id);
    if (rnd < threshold) return 0.0;

    vec2 off = vec2(hash21(id + 11.3), hash21(id + 27.1)) - 0.5;
    float d = length(gv - off * 0.55);
    float twinkle = 0.70 + 0.30 * sin(u_Time * 6.0 + rnd * 35.0);
    return smoothstep(0.16, 0.0, d) * twinkle;
}

void main()
{
    vec2 uv = fragTexCoord;
    if (u_FlipX > 0.5) uv.x = 1.0 - uv.x;

    vec2 p = (uv - 0.5) * 2.0;
    float y2 = p.y * p.y;
    if (y2 > 0.96) discard;

    // Sharp forward-curved crescent wave
    float outerX = 0.88 - 1.15 * y2;
    float thickness = 0.70 * pow(max(0.0, 1.0 - y2 / 0.96), 0.75);
    float innerX = outerX - thickness;

    // Crisp leading edge, slightly softer trailing tail
    float outerCut = 1.0 - smoothstep(outerX - 0.03, outerX + 0.02, p.x);
    float innerCut = smoothstep(innerX - 0.12, innerX + 0.05, p.x);
    float shape = outerCut * innerCut;

    if (shape < 0.02) discard;

    // Normalized position from trailing edge (0.0) to razor leading edge (1.0)
    float edgeT = clamp((p.x - innerX) / max(0.001, thickness), 0.0, 1.0);

    // Use u_AccentColor if provided, otherwise inherit the projectile's Lua color
    vec3 accent = (length(u_AccentColor) > 0.01)
        ? u_AccentColor
        : (length(fragColor.rgb) > 0.01 ? fragColor.rgb : vec3(0.72, 0.25, 1.0));

    // Scrolling dark starry sky + subtle cosmic nebula inside the blade
    float speed = (u_ScrollSpeed > 0.001) ? u_ScrollSpeed : 1.2;
    vec2 skyUV = uv * vec2(1.5, 1.2) + vec2(-u_Time * speed * 0.35, u_Time * 0.06);

    float neb = fbm(skyUV * 4.5);
    float stars = starField(skyUV, 22.0, 0.83) + starField(skyUV + 0.37, 42.0, 0.87) * 0.75;

    vec3 darkSky = mix(vec3(0.025, 0.012, 0.075), accent * 0.42, smoothstep(0.30, 0.80, neb));
    darkSky += vec3(0.95, 0.92, 1.0) * stars * 1.35;

    // Razor-sharp glowing outer edge + white-hot core + faint trailing rim
    float edgeBand  = smoothstep(0.58, 0.95, edgeT);
    float hotCore   = smoothstep(0.84, 0.98, edgeT);
    float trailGlow = (1.0 - smoothstep(0.0, 0.25, edgeT)) * 0.45;

    vec3 col = mix(darkSky, accent * 1.35, clamp(edgeBand * 0.92 + trailGlow, 0.0, 1.0));
    col = mix(col, vec3(1.0, 0.97, 1.0), hotCore * 0.88);

    finalColor = vec4(col, shape * fragColor.a);
}