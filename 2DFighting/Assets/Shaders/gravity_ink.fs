#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;

uniform sampler2D texture0;
uniform float u_Time;
uniform vec2 u_Resolution;

// Custom Gameplay & Parallax Uniforms
uniform vec2 u_CharacterPos;
uniform vec2 u_CameraPos;
uniform vec2 u_CharacterVelocity;
uniform float u_ParallaxStrength;
uniform float u_SpaceScrollSpeed;  // 0.0 = stationary stencil, >0.0 = drifting space motion
uniform float u_AttackIntensity;
uniform vec2 u_SingularityUV;
uniform float u_SingularityScale;
uniform float u_SingularityAngle;  // Optional tilt in radians for tilted ovals like the tsuba

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
    for (int i = 0; i < 4; i++) {
        v += amp * noise2D(p);
        p = rot * p * 2.04 + vec2(13.1, 7.7);
        amp *= 0.5;
    }
    return v;
}

// --- 1. "WINDOW TO COSMOS" (Smaller Stars + Controllable Drift & Parallax) ---
vec3 renderCosmosWindow(vec2 uv, vec2 screenUV, float borderProximity) {
    vec2 res = (u_Resolution.x > 0.0) ? u_Resolution : vec2(800.0, 600.0);
    float aspect = res.x / res.y;

    float pFactor = (u_ParallaxStrength > 0.001) ? u_ParallaxStrength : 0.15;
    vec2 camShift = (u_CameraPos / res) * pFactor;
    vec2 velShift = (u_CharacterVelocity / res) * (pFactor * 0.35);
    vec2 timeDrift = vec2(u_Time * u_SpaceScrollSpeed, u_Time * u_SpaceScrollSpeed * 0.45);

    vec2 cosmosUV = (screenUV - 0.5) * vec2(aspect, 1.0) + camShift - velShift + timeDrift;

    // Higher-frequency FBM Nebula Clouds so details fit inside smaller stencil areas
    float n1 = fbm(cosmosUV * 6.5 + vec2(2.1, -1.4));
    float n2 = fbm(cosmosUV * 13.0 - vec2(4.3, 3.2) + n1 * 0.85);
    vec3 col = vec3(0.04, 0.015, 0.11);
    col = mix(col, vec3(0.24, 0.07, 0.46), smoothstep(0.25, 0.75, n1));
    col = mix(col, vec3(0.65, 0.18, 0.76), smoothstep(0.45, 0.85, n1 * n2 * 1.8));
    col = mix(col, vec3(0.20, 0.68, 0.95), smoothstep(0.58, 0.92, n2) * 0.60);

    // Smaller, denser Spiral Galaxy Clusters
    vec2 galPos = fract(cosmosUV * 2.6 + vec2(0.35, 0.42)) - 0.5;
    vec2 tilted = vec2(galPos.x * 1.15 + galPos.y * 0.45, -galPos.x * 0.35 + galPos.y * 1.85);
    float gDist = length(tilted);
    float gAngle = atan(tilted.y, tilted.x) + gDist * 11.0 - (u_Time * 0.35);
    float spiralArms = pow(0.5 + 0.5 * cos(gAngle * 2.0), 2.5);
    float galCore = exp(-gDist * 18.0);
    float galDisk = exp(-gDist * 7.5) * spiralArms;
    col += vec3(0.95, 0.88, 1.0) * galCore * 1.1;
    col += vec3(0.62, 0.45, 0.98) * galDisk * 0.85;

    // Crisp pinpoint Starfield (higher grid density = much smaller stars)
    vec2 starGrid1 = floor(cosmosUV * 95.0);
    float s1 = step(0.986, hash21(starGrid1));
    float tw1 = 0.65 + 0.35 * sin(u_Time * 4.5 + hash21(starGrid1) * 30.0);

    vec2 starGrid2 = floor((cosmosUV + vec2(0.37, 0.19)) * 185.0);
    float s2 = step(0.990, hash21(starGrid2));

    vec3 starTint = mix(vec3(0.75, 0.88, 1.0), vec3(1.0, 0.82, 0.98), hash21(starGrid1 + 7.0));
    col += starTint * (s1 * tw1 * 1.15 + s2 * 0.85);

    // Subtle glowing lavender-white inner rim along black ink border
    float rimGlow = smoothstep(0.15, 0.95, borderProximity);
    col += vec3(0.82, 0.68, 1.0) * rimGlow * 0.65;

    return col;
}

// --- 2. GARGANTUA GRAVITATIONALLY LENSED BLACK HOLE ---
vec3 renderBlackHole(vec2 uv, float flare) {
    vec2 center = (length(u_SingularityUV) > 0.001) ? u_SingularityUV : vec2(0.5, 0.5);
    float scale = (u_SingularityScale > 0.01) ? u_SingularityScale : 1.0;

    // Aspect-ratio correction so non-square textures (like tall boots or sword) keep round cores
    vec2 texSize = vec2(textureSize(texture0, 0));
    float texAspect = texSize.x / max(1.0, texSize.y);
    vec2 d = (uv - center);
    d.x *= texAspect;
    d /= scale;

    // Rotate local coordinates so tilted ovals (like the sword tsuba) align their accretion disk
    float ca = cos(u_SingularityAngle);
    float sa = sin(u_SingularityAngle);
    d = mat2(ca, -sa, sa, ca) * d;

    float r = length(d);
    float safeR = max(r, 0.03);
    float lensStrength = 0.055 * (1.0 + flare * 0.25);
    vec2 warpedD = d * (1.0 - (lensStrength / safeR));

    float swirlAngle = atan(warpedD.y, warpedD.x) + (1.2 / (safeR + 0.15)) - (u_Time * (2.4 + flare * 1.8));
    float plasmaNoise = fbm(vec2(swirlAngle * 2.5, r * 14.0 - u_Time * 1.6));

    // Horizontal Equatorial Accretion Disk
    vec2 diskCoord = vec2(d.x * 0.80, d.y * 4.2);
    float diskR = length(diskCoord);
    float diskBand = exp(-pow((diskR - 0.32) * 7.5, 2.0)) * smoothstep(0.13, 0.22, r);

    // Gravitationally Lensed Upper & Lower Photon Ring
    float lensedRing = exp(-pow((r - 0.24) * 14.0, 2.0));
    float archMask = 0.55 + 0.45 * abs(d.y / safeR);
    float totalPlasma = (diskBand * 1.2 + lensedRing * archMask * 0.95) * (0.68 + 0.65 * plasmaNoise);
    totalPlasma *= (1.0 + flare * 0.55);

    vec3 colOuter = vec3(0.42, 0.14, 0.72);
    vec3 colMid   = vec3(0.96, 0.52, 0.22);
    vec3 colHot   = vec3(1.00, 0.94, 0.86);

    vec3 plasmaCol = mix(colOuter, colMid, smoothstep(0.15, 0.65, totalPlasma));
    plasmaCol = mix(plasmaCol, colHot, smoothstep(0.60, 1.15, totalPlasma));

    float horizonRadius = 0.17;
    float eventHorizon = smoothstep(horizonRadius - 0.015, horizonRadius + 0.015, r);

    float photonEdge = exp(-pow((r - horizonRadius) * 32.0, 2.0)) * (0.85 + flare * 0.4);
    plasmaCol += colHot * photonEdge;

    return plasmaCol * eventHorizon;
}

void main()
{
    vec4 tex = texture(texture0, fragTexCoord);
    if (tex.a < 0.01) discard;

    float luma = dot(tex.rgb, vec3(0.299, 0.587, 0.114));
    vec2 res = (u_Resolution.x > 0.0) ? u_Resolution : vec2(800.0, 600.0);
    vec2 screenUV = gl_FragCoord.xy / res;

    // Sample neighbor texels for dark outline proximity (crevice AO & inner rim glow)
    vec2 texel = 2.0 / vec2(textureSize(texture0, 0));
    float nLuma = 0.0;
    nLuma += dot(texture(texture0, fragTexCoord + vec2( texel.x,  0.0)).rgb, vec3(0.299, 0.587, 0.114));
    nLuma += dot(texture(texture0, fragTexCoord + vec2(-texel.x,  0.0)).rgb, vec3(0.299, 0.587, 0.114));
    nLuma += dot(texture(texture0, fragTexCoord + vec2( 0.0,  texel.y)).rgb, vec3(0.299, 0.587, 0.114));
    nLuma += dot(texture(texture0, fragTexCoord + vec2( 0.0, -texel.y)).rgb, vec3(0.299, 0.587, 0.114));
    nLuma *= 0.25;
    float nearInkBorder = clamp(1.0 - smoothstep(0.05, 0.45, nLuma), 0.0, 1.0);

    float flare = clamp(u_AttackIntensity, 0.0, 2.0);
    vec3 outCol;

    if (luma < 0.08) {
        outCol = vec3(0.02, 0.008, 0.04);
    }
    else if (luma > 0.85) {
        outCol = renderCosmosWindow(fragTexCoord, screenUV, nearInkBorder);
    }
    else if (luma > 0.63) {
        outCol = renderBlackHole(fragTexCoord, flare);
    }
    else {
        // 3. VOLUMETRIC ROYAL PURPLE WITH ORGANIC TEXTURE NOISE
        float tone = clamp((luma - 0.08) / 0.55, 0.0, 1.0);

        vec3 deepPlum     = vec3(0.169, 0.063, 0.271); // #2B1045
        vec3 royalPurple  = vec3(0.357, 0.169, 0.557); // #5B2B8E
        vec3 brightViolet = vec3(0.545, 0.294, 0.749); // #8B4BBF

        vec3 basePurple = mix(deepPlum, royalPurple, smoothstep(0.0, 0.55, tone));
        basePurple = mix(basePurple, brightViolet, smoothstep(0.50, 1.0, tone));

        // Multi-scale procedural noise so purple armor has forged metallic/astral grain
        float macroNoise = fbm(fragTexCoord * 9.0);
        float microGrain = noise2D(fragTexCoord * 48.0);
        float texPattern = mix(0.80, 1.24, macroNoise * 0.75 + microGrain * 0.25);

        float cylLight = 1.0 - pow((fragTexCoord.x - 0.5) * 1.5, 2.0) * 0.35;
        float topLight = mix(1.18, 0.72, fragTexCoord.y);
        float sheen = 0.92 + 0.16 * sin((fragTexCoord.x * 5.0 + fragTexCoord.y * 3.0) + u_Time * 0.4);

        outCol = basePurple * texPattern * cylLight * topLight * sheen;
        outCol = mix(outCol, vec3(0.08, 0.025, 0.16), nearInkBorder * 0.65);

        float rimHighlight = smoothstep(0.25, 0.0, fragTexCoord.y) * (1.0 - nearInkBorder);
        outCol += vec3(0.45, 0.22, 0.78) * rimHighlight * 0.28;
    }

    finalColor = vec4(outCol, tex.a) * fragColor;
}