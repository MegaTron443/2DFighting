#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;

uniform sampler2D texture0;
uniform float u_Time;

void main()
{
    vec2 p = (fragTexCoord - 0.5) * 2.0;
    float r = length(p);
    float angle = atan(p.y, p.x);

    // Subtle animated flame flicker around the outer rim
    float flicker = sin(angle * 7.0 + u_Time * 14.0) * 0.05
                  + cos(angle * 4.0 - u_Time * 10.0) * 0.04;
    float radius = 0.82 + flicker;

    float alpha = 1.0 - smoothstep(radius - 0.10, radius + 0.04, r);
    if (alpha < 0.02) discard;

    vec3 outerRed   = vec3(0.92, 0.12, 0.05);
    vec3 midOrange  = vec3(1.00, 0.45, 0.08);
    vec3 hotCore    = vec3(1.00, 0.92, 0.55);

    vec3 col = mix(hotCore, midOrange, smoothstep(0.0, 0.48, r));
    col = mix(col, outerRed, smoothstep(0.42, radius, r));

    finalColor = vec4(col, alpha * fragColor.a);
}