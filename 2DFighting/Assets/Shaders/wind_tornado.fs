#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;

uniform sampler2D texture0;
uniform float u_Time;
uniform float u_Progress;    // 0.0 -> 1.0 over tornado lifetime
uniform float u_ScrollSpeed;

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
    float speed = (u_ScrollSpeed > 0.01) ? u_ScrollSpeed : 11.0;

    // Height factor: 0.0 at bottom (base), 1.0 at top (flared crown)
    float h = 1.0 - uv.y;

    // Slight S-curve sway as the funnel spins
    float sway = sin(h * 7.0 - u_Time * speed * 0.65) * 0.06 * h;
    float dx = abs((uv.x - 0.5) - sway);

    // Funnel width: narrow at bottom, wide at top, grows slightly with u_Progress
    float grow = mix(0.75, 1.08, smoothstep(0.0, 0.35, u_Progress));
    float halfWidth = mix(0.10, 0.44, pow(h, 0.75)) * grow;

    float edgeMask = 1.0 - smoothstep(halfWidth - 0.06, halfWidth + 0.02, dx);
    float vertFade = smoothstep(0.0, 0.12, h) * (1.0 - smoothstep(0.84, 1.0, h));
    if (edgeMask * vertFade < 0.02) discard;

    // Fast horizontal spinning wind ribbons + rising turbulence
    float localX = ((uv.x - 0.5) - sway) / max(0.01, halfWidth);
    float band1 = sin(h * 26.0 + localX * 4.5 - u_Time * speed * 1.4);
    float band2 = sin(h * 15.0 - localX * 6.0 - u_Time * speed * 1.9);
    float ribbons = smoothstep(-0.15, 0.85, band1 * 0.6 + band2 * 0.4);

    float turb = noise2D(vec2(localX * 4.0 + u_Time * 6.0, h * 10.0 - u_Time * 8.0));

    // Brighter wind rim along the outer funnel walls
    float rim = smoothstep(0.45, 0.95, abs(localX));

    vec3 baseCol = (length(fragColor.rgb) > 0.01) ? fragColor.rgb : vec3(0.32, 0.92, 0.60);
    vec3 col = mix(baseCol * 0.55, baseCol * 1.25, ribbons * 0.75 + turb * 0.25);
    col = mix(col, vec3(0.92, 1.0, 0.96), clamp(rim * 0.45 + smoothstep(0.70, 0.98, ribbons) * 0.65, 0.0, 1.0));

    float fadeOut = 1.0 - smoothstep(0.65, 1.0, u_Progress);
    float alpha = edgeMask * vertFade * (0.35 + 0.65 * ribbons) * fadeOut * fragColor.a;

    finalColor = vec4(col, alpha);
}