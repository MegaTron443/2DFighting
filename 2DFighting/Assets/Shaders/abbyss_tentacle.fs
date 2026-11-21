#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;

uniform sampler2D texture0;
uniform float u_Time;
uniform float u_ScrollSpeed;
uniform float u_Progress;
uniform float u_Seed;

float hash21(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

float noise2D(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(
        mix(hash21(i), hash21(i + vec2(1.0, 0.0)), u.x),
        mix(hash21(i + vec2(0.0, 1.0)), hash21(i + vec2(1.0, 1.0)), u.x),
        u.y
    );
}

void main()
{
    vec2 uv = fragTexCoord;
    float speed = (u_ScrollSpeed > 0.01) ? u_ScrollSpeed : 7.0;
    float seed  = u_Seed;

    // Straight spine with subtle, unsettling eldritch twitch (no big sine waves)
    float twitch = (noise2D(vec2(uv.x * 8.0 - u_Time * speed * 0.6, seed * 7.3)) - 0.5) * 0.07;
    float micro  = (noise2D(vec2(uv.x * 26.0 - u_Time * speed, seed * 13.1)) - 0.5) * 0.025;
    float centerY = 0.5 + twitch + micro;

    // Thin, needle-like taper from base to tip
    float baseThick = mix(0.11, 0.015, pow(uv.x, 1.05));

    // Jagged eldritch barbs/thorns along the edges
    float barbNoise = noise2D(vec2(uv.x * 38.0 - u_Time * speed * 1.4, seed * 19.7));
    float barbs = smoothstep(0.68, 0.95, barbNoise) * 0.065 * (1.0 - uv.x * 0.6);
    float halfThick = baseThick + barbs;

    float dist = abs(uv.y - centerY);

    // Outer smoky void wisp around the tentacle
    float wispNoise = noise2D(vec2(uv.x * 14.0 - u_Time * speed * 0.8, uv.y * 10.0 + seed));
    float wispMask = (1.0 - smoothstep(halfThick, halfThick + 0.10, dist)) * smoothstep(0.45, 0.85, wispNoise) * 0.45;

    float coreMask = 1.0 - smoothstep(halfThick - 0.015, halfThick + 0.005, dist);
    float totalAlpha = max(coreMask, wispMask);
    if (totalAlpha < 0.02) discard;

    // Pitch-black core, eerie crimson/violet pulsing vein down the center, dark rim
    vec3 coreBlack = vec3(0.01, 0.005, 0.02);
    vec3 rimCol    = vec3(0.32, 0.04, 0.16);
    vec3 veinCol   = vec3(0.75, 0.08, 0.25);

    float edge = smoothstep(halfThick - 0.035, halfThick, dist);
    float vein = (1.0 - smoothstep(0.0, 0.022, dist)) * smoothstep(0.55, 0.92, noise2D(vec2(uv.x * 18.0 - u_Time * 9.0, seed)));

    vec3 col = mix(coreBlack, rimCol, edge * 0.9);
    col = mix(col, veinCol, vein * 0.55);
    col = mix(vec3(0.06, 0.01, 0.05), col, coreMask);

    float fadeOut = 1.0 - smoothstep(0.74, 1.0, u_Progress);
    finalColor = vec4(col, totalAlpha * fadeOut * fragColor.a);
}