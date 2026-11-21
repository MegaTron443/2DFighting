#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;

uniform sampler2D texture0;
uniform float u_Time;
uniform vec2 u_Resolution;

// Dynamic Water Vector (modified in Lua by skill usage)
uniform vec3 u_WaterColor;     // 100% white water tint (0..1)

// Static Clothing & Metal Colors
uniform vec3 u_ClothColor1;    // 40% gray clothing
uniform vec3 u_ClothColor2;    // 70% gray secondary color
uniform vec3 u_MetalColor;     // 30% gray staff metal

// Fixed Potion Bottle Region (enabled on body.png only)
uniform float u_HasPotion;     // 1.0 on body, 0.0 on other parts
uniform vec2 u_PotionUV;       // Center of bottle in body.png UV (e.g. 0.20, 0.78)
uniform vec2 u_PotionSize;     // Half-width / half-height in UV (e.g. 0.14, 0.15)
uniform vec3 u_PotionColor;    // Fixed potion liquid color (default emerald/teal)

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
        p = rot * p * 2.02 + vec2(11.3, 5.7);
        amp *= 0.5;
    }
    return v;
}

// Sleek stylized water (clean depth gradient + thin crisp caustic lines)
vec3 renderColoredWater(vec2 uv, vec3 waterTint, bool isSecondary80) {
    vec2 flowUV = uv * 5.0 + vec2(u_Time * 0.18, -u_Time * 0.38);
    float w1 = fbm(flowUV);
    float w2 = fbm(flowUV * 1.4 - vec2(u_Time * 0.25, u_Time * 0.22) + w1 * 0.6);

    // Thin, crisp caustic streaks instead of blurry blobs
    float caustic = smoothstep(0.76, 0.94, 1.0 - abs(w2 * 2.0 - 1.0));
    float waveBar = smoothstep(0.82, 0.98, 0.5 + 0.5 * sin(uv.y * 22.0 - u_Time * 3.8 + w1 * 3.5));

    vec3 baseCol = isSecondary80 ? (waterTint * 0.62 + vec3(0.02, 0.04, 0.08)) : waterTint;
    // Smooth top-to-bottom liquid depth gradient
    vec3 col = mix(baseCol * 1.08, baseCol * 0.52, uv.y);

    // Subtle shimmer + crisp highlights
    col = mix(col, min(baseCol * 1.35 + vec3(0.25), vec3(1.0)), caustic * (isSecondary80 ? 0.30 : 0.48));
    col += vec3(0.92, 0.98, 1.0) * waveBar * (isSecondary80 ? 0.15 : 0.32);

    return col;
}

// Fixed-color glass potion with sloshing liquid & rising bubbles
vec3 renderPotionBottle(vec2 localUV, vec3 potionCol) {
    // localUV is roughly -1..1 inside the bottle box (y = -1 top, +1 bottom)
    float wave = sin(localUV.x * 5.5 + u_Time * 4.0) * 0.08;
    float fillLine = -0.05 + wave; // Liquid fills lower ~55% of bottle

    if (localUV.y < fillLine) {
        // Upper empty glass with faint specular reflection
        vec3 glass = vec3(0.76, 0.84, 0.90) * 0.72;
        float spec = smoothstep(0.18, 0.0, abs(localUV.x + 0.42));
        return mix(glass, vec3(1.0), spec * 0.45);
    }

    // Glowing potion liquid + rising bubbles
    float depthT = clamp((localUV.y - fillLine) / 1.05, 0.0, 1.0);
    vec3 liq = mix(potionCol * 1.25, potionCol * 0.55, depthT);

    // Meniscus highlight right at surface
    float surface = exp(-abs(localUV.y - fillLine) * 24.0);
    liq += vec3(0.9, 1.0, 0.95) * surface * 0.55;

    // Tiny rising bubbles
    vec2 bUV = vec2(localUV.x * 3.5, localUV.y * 3.5 + u_Time * 1.4);
    vec2 bId = floor(bUV);
    vec2 bGv = fract(bUV) - 0.5;
    float bRnd = hash21(bId);
    if (bRnd > 0.72) {
        float b = smoothstep(0.24, 0.10, length(bGv));
        liq = mix(liq, vec3(1.0), b * 0.60);
    }

    return liq;
}

void main()
{
    vec4 tex = texture(texture0, fragTexCoord);
    if (tex.a < 0.20) discard;

    // Force semi-transparent anti-aliased outer edges to ink color (eliminates white halos)
    if (tex.a < 0.85) {
        finalColor = vec4(0.03, 0.02, 0.05, tex.a) * fragColor;
        return;
    }

    float luma = dot(tex.rgb, vec3(0.299, 0.587, 0.114));

    vec3 waterCol  = (length(u_WaterColor)  > 0.01) ? u_WaterColor  : vec3(0.28, 0.76, 1.00);
    vec3 cloth1    = (length(u_ClothColor1) > 0.01) ? u_ClothColor1 : vec3(0.24, 0.25, 0.34);
    vec3 cloth2    = (length(u_ClothColor2) > 0.01) ? u_ClothColor2 : vec3(0.76, 0.73, 0.68);
    vec3 metalCol  = (length(u_MetalColor)  > 0.01) ? u_MetalColor  : vec3(0.42, 0.46, 0.54);
    vec3 potionCol = (length(u_PotionColor) > 0.01) ? u_PotionColor : vec3(0.18, 0.92, 0.48);

    vec3 outCol;

    // 1. Crisp black ink outlines
    if (luma < 0.12) {
        outCol = vec3(0.03, 0.02, 0.05);
    }
    // 2. Fixed Potion Bottle region on body.png (ONLY fills the gray flask interior, never the white cloak)
    else if (u_HasPotion > 0.5 && luma > 0.75 && luma < 0.93) {
        vec2 pSize = max(u_PotionSize, vec2(0.05));
        vec2 local = (fragTexCoord - u_PotionUV) / pSize;
        if (abs(local.x) <= 1.0 && abs(local.y) <= 1.0) {
            outCol = renderPotionBottle(local, potionCol);
            finalColor = vec4(outCol, tex.a) * fragColor;
            return;
        }
        outCol = renderColoredWater(fragTexCoord, waterCol, true);
    }
    // 3. 30% Gray: Metallic staff
    else if (luma < 0.35) {
        float sheenPos = fract(u_Time * 0.35);
        float diag = (fragTexCoord.x * 0.6 + fragTexCoord.y * 0.4);
        float spec = smoothstep(0.14, 0.0, abs(diag - sheenPos)) * 0.55;
        float cyl  = 0.78 + 0.32 * (1.0 - abs(fragTexCoord.x - 0.5) * 2.0);
        outCol = metalCol * cyl + vec3(0.85, 0.90, 1.0) * spec;
    }
    // 4. 40% Gray: Normal primary clothing (no special effects)
    else if (luma < 0.55) {
        float softGrad = mix(1.06, 0.88, fragTexCoord.y);
        outCol = cloth1 * softGrad;
    }
    // 5. 70% Gray: Normal secondary color (different from 40%)
    else if (luma < 0.76) {
        float softGrad = mix(1.05, 0.90, fragTexCoord.y);
        outCol = cloth2 * softGrad;
    }
    // 6. 80% Gray: Secondary water (distinct from 100%)
    else if (luma < 0.92) {
        outCol = renderColoredWater(fragTexCoord, waterCol, true);
    }
    // 7. 100% White: Primary colored water
    else {
        outCol = renderColoredWater(fragTexCoord, waterCol, false);
    }

    finalColor = vec4(outCol, tex.a) * fragColor;
}