#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;

uniform sampler2D texture0;
uniform float u_Time;
uniform vec2 u_Resolution;
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

float fbm(vec2 p) {
    float v = 0.0;
    float amp = 0.5;
    mat2 rot = mat2(0.80, 0.60, -0.60, 0.80);
    for (int i = 0; i < 3; i++) {
        v += amp * noise2D(p);
        p = rot * p * 2.02 + vec2(11.3, 5.7);
        amp *= 0.5;
    }
    return v;
}

vec3 hsv2rgb(vec3 c) {
    vec3 p = abs(fract(c.xxx + vec3(1.0, 2.0/3.0, 1.0/3.0)) * 6.0 - 3.0);
    return c.z * mix(vec3(1.0), clamp(p - 1.0, 0.0, 1.0), c.y);
}

void main()
{
    vec2 uv = fragTexCoord;
    float speed = (u_ScrollSpeed > 0.01) ? u_ScrollSpeed : 2.8;

    // Soft horizontal beam edges (vertical column)
    float distX = abs(uv.x - 0.5) * 2.0;
    float waveEdge = sin(uv.y * 18.0 - u_Time * 12.0) * 0.06;
    float alpha = 1.0 - smoothstep(0.72 + waveEdge, 1.0, distX);
    if (alpha < 0.02) discard;

    // Downward flowing water caustics
    vec2 flowUV = vec2(uv.x * 4.0, uv.y * 6.0 - u_Time * speed);
    float w1 = fbm(flowUV);
    float w2 = fbm(flowUV * 1.6 + vec2(w1 * 1.1, -u_Time * 0.8));
    float caustic = smoothstep(0.68, 0.94, 1.0 - abs(w2 * 2.0 - 1.0));

    // Prismatic rainbow shifting down the beam
    float hue = fract(uv.y * 0.85 - u_Time * 0.75 + w1 * 0.35);
    vec3 rainbow = hsv2rgb(vec3(hue, 0.78, 1.0));

    float core = 1.0 - smoothstep(0.0, 0.38, distX);
    vec3 col = mix(rainbow * 0.75, rainbow * 1.25, w1);
    col = mix(col, vec3(1.0, 0.99, 1.0), clamp(core * 0.70 + caustic * 0.55, 0.0, 1.0));

    finalColor = vec4(col, alpha * fragColor.a);
}