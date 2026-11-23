#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;

uniform sampler2D texture0;
uniform float u_Time;
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
    vec2 p = (fragTexCoord - 0.5) * 2.0;
    float r = length(p);
    if (r > 1.0) discard;

    float speed = (u_ScrollSpeed > 0.01) ? u_ScrollSpeed : 8.0;
    float angle = atan(p.y, p.x);

    // Tight swirling spiral ribbons
    float spiral1 = sin(angle * 3.0 - r * 11.0 + u_Time * speed);
    float spiral2 = sin(angle * 5.0 - r * 16.0 + u_Time * (speed * 1.35));
    float streaks = smoothstep(0.15, 0.88, spiral1 * 0.65 + spiral2 * 0.35);

    // Subtle wind turbulence
    float turb = noise2D(vec2(angle * 3.0 + u_Time * 2.5, r * 8.0 - u_Time * 4.0));

    // Soft outer fade + hollow swirling eye in the center
    float outerFade = 1.0 - smoothstep(0.62, 1.0, r);
    float ringBoost = smoothstep(0.08, 0.42, r);
    float alpha = (streaks * 0.85 + 0.18 * turb) * outerFade * (0.45 + 0.55 * ringBoost);

    if (alpha < 0.03) discard;

    vec3 baseCol = (length(fragColor.rgb) > 0.01) ? fragColor.rgb : vec3(0.32, 0.90, 0.58);
    vec3 col = mix(baseCol * 0.65, baseCol * 1.25, streaks);
    // Crisp white air-current highlights
    col = mix(col, vec3(0.92, 1.0, 0.96), smoothstep(0.68, 0.96, streaks) * 0.75);

    finalColor = vec4(col, alpha * fragColor.a);
}