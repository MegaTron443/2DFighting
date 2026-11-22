#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;

uniform sampler2D texture0;
uniform float u_Time;
uniform vec2 u_Resolution;
uniform float u_AttackIntensity;

uniform vec3 u_PrimaryColor;
uniform vec3 u_SecondaryColor;
uniform vec3 u_GlowColor;
uniform float u_PartMode; // 0 = standard, 1 = Head, 2 = Body 50/50, 4 = Lance, 5 = Arms (steel armor + tan skin)

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
    for (int i = 0; i < 4; i++) {
        v += amp * noise2D(p);
        p = rot * p * 2.04 + vec2(13.1, 7.7);
        amp *= 0.5;
    }
    return v;
}

void main() {
    vec4 tex = texture(texture0, fragTexCoord);
    if (tex.a < 0.01) discard;

    float luma = dot(tex.rgb, vec3(0.299, 0.587, 0.114));

    vec2 texel = 2.0 / vec2(textureSize(texture0, 0));
    float nLuma = 0.0;
    nLuma += dot(texture(texture0, fragTexCoord + vec2( texel.x,  0.0)).rgb, vec3(0.299, 0.587, 0.114));
    nLuma += dot(texture(texture0, fragTexCoord + vec2(-texel.x,  0.0)).rgb, vec3(0.299, 0.587, 0.114));
    nLuma += dot(texture(texture0, fragTexCoord + vec2( 0.0,  texel.y)).rgb, vec3(0.299, 0.587, 0.114));
    nLuma += dot(texture(texture0, fragTexCoord + vec2( 0.0, -texel.y)).rgb, vec3(0.299, 0.587, 0.114));
    nLuma *= 0.25;
    float nearInkBorder = clamp(1.0 - smoothstep(0.05, 0.45, nLuma), 0.0, 1.0);

    vec3 colPrim = (length(u_PrimaryColor) > 0.01)   ? u_PrimaryColor   : vec3(0.68, 0.73, 0.80);
    vec3 colSec  = (length(u_SecondaryColor) > 0.01) ? u_SecondaryColor : vec3(0.22, 0.25, 0.31);
    vec3 colGlow = (length(u_GlowColor) > 0.01)      ? u_GlowColor      : vec3(0.25, 0.88, 0.55);
    float flare  = clamp(u_AttackIntensity, 0.0, 2.0);

    // 1. Crisp Dark Ink Outlines
    if (luma < 0.12 && tex.g < 0.20) {
        finalColor = vec4(vec3(0.05, 0.06, 0.08), tex.a) * fragColor;
        return;
    }

    // 2. Green-Masked Emerald Cloth (Scarf, Belt, Cape)
    bool isGreenMask = (tex.g > tex.r * 1.45) && (tex.g > tex.b * 1.45);
    if (isGreenMask) {
        float foldShade = clamp((tex.g - 0.35) / 0.65, 0.0, 1.0);
        float windRipple = sin(fragTexCoord.y * 11.0 - u_Time * 5.5 + fragTexCoord.x * 4.0) * 0.07;
        float weave = fbm(fragTexCoord * 14.0) * 0.14;
        float t = clamp(foldShade + windRipple * foldShade + (weave - 0.07), 0.0, 1.0);

        vec3 emeraldDark = vec3(0.06, 0.28, 0.16);
        vec3 emeraldMid  = vec3(0.13, 0.56, 0.32);
        vec3 emeraldHi   = vec3(0.24, 0.74, 0.44);

        vec3 cloth = mix(emeraldDark, emeraldMid, smoothstep(0.0, 0.62, t));
        cloth = mix(cloth, emeraldHi, smoothstep(0.62, 1.0, t));
        cloth = mix(cloth, emeraldDark * 0.65, nearInkBorder * 0.50);
        cloth += colGlow * (flare * 0.18 * foldShade);

        finalColor = vec4(cloth, tex.a) * fragColor;
        return;
    }

    // 3. HEAD MODE (u_PartMode == 1.0): Cutoff at y = 0.365 right along the black hairline
    if (abs(u_PartMode - 1.0) < 0.2) {
        vec3 skinHi   = vec3(0.94, 0.74, 0.57);
        vec3 skinBase = vec3(0.89, 0.67, 0.49);
        vec3 skinShad = vec3(0.72, 0.49, 0.34);
        vec3 skin = mix(skinBase, skinHi, smoothstep(0.78, 0.35, fragTexCoord.y));
        skin = mix(skin, skinShad, nearInkBorder * 0.55);
        finalColor = vec4(skin, tex.a) * fragColor;
        return;
    }

    // 4. ARMS MODE (u_PartMode == 5.0): Steel pauldron/bracer plates + warm tan exposed skin & hand
    if (abs(u_PartMode - 5.0) < 0.2) {
        if (luma > 0.88) {
            vec3 skin = mix(vec3(0.89, 0.67, 0.49), vec3(0.71, 0.48, 0.33), nearInkBorder * 0.55);
            finalColor = vec4(skin, tex.a) * fragColor;
        } else {
            float tone = clamp((luma - 0.14) / 0.74, 0.0, 1.0);
            vec3 metalHi = min(colPrim * 1.32 + vec3(0.05), vec3(1.0));
            vec3 shaded = mix(colSec, colPrim, smoothstep(0.0, 0.58, tone));
            shaded = mix(shaded, metalHi, smoothstep(0.58, 1.0, tone));
            shaded = mix(shaded, colSec * 0.50, nearInkBorder * 0.55);
            finalColor = vec4(shaded, tex.a) * fragColor;
        }
        return;
    }

    // 5. LANCE MODE (u_PartMode == 4.0): Emerald diamond gems + silver spearhead + dark shaft
    if (abs(u_PartMode - 4.0) < 0.2) {
        if (luma > 0.84) {
            vec3 gemCol = mix(vec3(0.12, 0.58, 0.34), vec3(0.32, 0.85, 0.52), 1.0 - nearInkBorder);
            finalColor = vec4(gemCol, tex.a) * fragColor;
        } else {
            float tone = clamp((luma - 0.14) / 0.70, 0.0, 1.0);
            vec3 shaftDark = vec3(0.24, 0.27, 0.33);
            vec3 bladeSilv = vec3(0.76, 0.81, 0.88);
            vec3 lanceCol = mix(shaftDark, bladeSilv, smoothstep(0.35, 0.85, tone));
            lanceCol = mix(lanceCol, shaftDark * 0.55, nearInkBorder * 0.55);
            finalColor = vec4(lanceCol, tex.a) * fragColor;
        }
        return;
    }

    // 6. BODY 50/50 SPLIT MODE (u_PartMode == 2.0): Left silver armor, Right dark charcoal tunic
    if (abs(u_PartMode - 2.0) < 0.2) {
        float tone = clamp((luma - 0.14) / 0.74, 0.0, 1.0);
        if (fragTexCoord.x < 0.48) {
            vec3 armDark = vec3(0.34, 0.39, 0.47);
            vec3 armMid  = vec3(0.68, 0.73, 0.80);
            vec3 armHi   = vec3(0.88, 0.92, 0.96);
            vec3 metal = mix(armDark, armMid, smoothstep(0.0, 0.58, tone));
            metal = mix(metal, armHi, smoothstep(0.58, 1.0, tone));
            metal = mix(metal, armDark * 0.5, nearInkBorder * 0.55);
            finalColor = vec4(metal, tex.a) * fragColor;
        } else {
            if (luma > 0.88) {
                finalColor = vec4(vec3(0.84, 0.87, 0.91), tex.a) * fragColor;
            } else {
                vec3 tunDark = vec3(0.14, 0.16, 0.20);
                vec3 tunMid  = vec3(0.26, 0.29, 0.35);
                vec3 tunic = mix(tunDark, tunMid, smoothstep(0.0, 0.75, tone));
                tunic = mix(tunic, tunDark * 0.5, nearInkBorder * 0.55);
                finalColor = vec4(tunic, tex.a) * fragColor;
            }
        }
        return;
    }

    // 7. STANDARD ARMOR / DARK PANTS PARTS (Left_leg, Right_leg)
    if (luma > 0.88) {
        vec3 trimSilver = mix(vec3(0.86, 0.89, 0.93), vec3(0.55, 0.60, 0.68), nearInkBorder * 0.50);
        finalColor = vec4(trimSilver, tex.a) * fragColor;
        return;
    }

    float tone = clamp((luma - 0.14) / 0.74, 0.0, 1.0);
    vec3 metalHi = min(colPrim * 1.32 + vec3(0.05), vec3(1.0));
    vec3 shaded = mix(colSec, colPrim, smoothstep(0.0, 0.58, tone));
    shaded = mix(shaded, metalHi, smoothstep(0.58, 1.0, tone));

    float grain = mix(0.92, 1.10, fbm(fragTexCoord * 12.0) * 0.7 + noise2D(fragTexCoord * 45.0) * 0.3);
    float cylLight = 1.0 - pow((fragTexCoord.x - 0.5) * 1.3, 2.0) * 0.24;
    shaded = shaded * grain * cylLight;
    shaded = mix(shaded, colSec * 0.50, nearInkBorder * 0.55);

    finalColor = vec4(shaded, tex.a) * fragColor;
}