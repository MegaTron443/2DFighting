public static class BuiltInShaders
{
    public const string PostFxCosmicId = "postfx_cosmic";
    public const string SpriteLutId = "sprite_lut";
    public const string UiButtonId = "ui_button";
    public const string SceneDefaultId = "scene_default";
    public const string SceneParallaxId = "scene_parallax";
    public const string SceneForegroundId = "scene_foreground";
    public const string HudBarId = "hud_bar";
    public const string CosmicSlashId = "cosmic_slash";

    public const string PostFxCosmicFragment = @"#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;

uniform sampler2D texture0;
uniform vec2 u_Resolution;
uniform vec2 u_ShakeOffset;      // UV offset for directional screen shake
uniform float u_ChromaticSplit;  // RGB split intensity (0.0 = off, 0.015 = heavy)
uniform float u_PinchAmount;     // Black hole radial pinch (-0.5 to 0.8)
uniform vec2 u_PinchCenter;      // Normalized screen center (default 0.5, 0.5)
uniform float u_ImpactMode;      // 0 = Off, 1 = 2-Tone Monochrome, 2 = Inverted Void
uniform vec3 u_TintColor;        // Palette tint (default 1, 1, 1)
uniform float u_PaletteMix;      // 0.0 = Normal, 1.0 = Full Cosmic Palette Shift

vec3 extractBright(vec3 c)
{
    float l = dot(c, vec3(0.299, 0.587, 0.114));
    return c * smoothstep(0.72, 0.96, l);
}

void main()
{
    vec2 center = (u_PinchCenter.x == 0.0 && u_PinchCenter.y == 0.0) ? vec2(0.5, 0.5) : u_PinchCenter;
    
    // Slight zoom when shaking so edges never clamp/smear
    float zoom = (dot(u_ShakeOffset, u_ShakeOffset) > 0.00000001) ? 0.965 : 1.0;
    vec2 uv = (fragTexCoord - center) * zoom + center + u_ShakeOffset;

    // Radial Gravity Pinch / Shockwave Warp
    if (abs(u_PinchAmount) > 0.001)
    {
        vec2 delta = uv - center;
        float dist = length(delta);
        float factor = 1.0 - (u_PinchAmount * exp(-dist * 4.5));
        uv = center + delta * factor;
    }

    uv = clamp(uv, vec2(0.001), vec2(0.999));

    // Chromatic Aberration RGB Split (skip extra texture fetches when off)
    vec3 col;
    float a;
    if (abs(u_ChromaticSplit) > 0.0001)
    {
        vec2 splitOffset = vec2(u_ChromaticSplit, u_ChromaticSplit * 0.4);
        float r = texture(texture0, clamp(uv + splitOffset, 0.001, 0.999)).r;
        vec4 centerSample = texture(texture0, uv);
        float b = texture(texture0, clamp(uv - splitOffset, 0.001, 0.999)).b;
        col = vec3(r, centerSample.g, b);
        a = centerSample.a;
    }
    else
    {
        vec4 centerSample = texture(texture0, uv);
        col = centerSample.rgb;
        a = centerSample.a;
    }

    // Subtle 8-tap bloom for emissive trims, cosmic slashes, and additive VFX
    vec2 texel = 1.0 / max(u_Resolution, vec2(800.0, 600.0));
    vec2 offCard = texel * 2.2;
    vec2 offDiag = texel * 3.6;

    vec3 bloomSum = extractBright(col);
    bloomSum += extractBright(texture(texture0, clamp(uv + vec2( offCard.x, 0.0), 0.001, 0.999)).rgb);
    bloomSum += extractBright(texture(texture0, clamp(uv + vec2(-offCard.x, 0.0), 0.001, 0.999)).rgb);
    bloomSum += extractBright(texture(texture0, clamp(uv + vec2(0.0,  offCard.y), 0.001, 0.999)).rgb);
    bloomSum += extractBright(texture(texture0, clamp(uv + vec2(0.0, -offCard.y), 0.001, 0.999)).rgb);
    bloomSum += extractBright(texture(texture0, clamp(uv + vec2( offDiag.x,  offDiag.y), 0.001, 0.999)).rgb);
    bloomSum += extractBright(texture(texture0, clamp(uv + vec2(-offDiag.x,  offDiag.y), 0.001, 0.999)).rgb);
    bloomSum += extractBright(texture(texture0, clamp(uv + vec2( offDiag.x, -offDiag.y), 0.001, 0.999)).rgb);
    bloomSum += extractBright(texture(texture0, clamp(uv + vec2(-offDiag.x, -offDiag.y), 0.001, 0.999)).rgb);
    col += (bloomSum / 9.0) * 0.42;

    float luma = dot(col, vec3(0.299, 0.587, 0.114));

    // Impact Frames (1 = Stark Black/White, 2 = Inverted Void)
    if (u_ImpactMode >= 1.5)
    {
        col = vec3(1.0) - col;
        col = mix(col, vec3(1.0 - luma) * vec3(0.65, 0.15, 1.0), 0.55);
    }
    else if (u_ImpactMode >= 0.5)
    {
        float bw = step(0.48, luma);
        col = mix(vec3(0.05, 0.0, 0.12), vec3(1.0, 0.95, 1.0), bw);
    }

    // Cosmic / Chromatic Palette Shift
    if (u_PaletteMix > 0.001)
    {
        vec3 tint = (dot(u_TintColor, u_TintColor) < 0.0001) ? vec3(0.55, 0.15, 0.95) : u_TintColor;
        vec3 voidMapped = pow(luma, 1.25) * tint * 1.4;
        col = mix(col, voidMapped, clamp(u_PaletteMix, 0.0, 1.0));
    }

    // Gentle screen vignette focusing attention toward the center arena
    vec2 vigUv = fragTexCoord * (1.0 - fragTexCoord.yx);
    float vig = clamp(pow(max(vigUv.x * vigUv.y * 15.0, 0.0), 0.18), 0.0, 1.0);
    col *= mix(0.68, 1.0, vig);

    finalColor = vec4(col, a) * fragColor;
}";

    public const string SpriteLutFragment = @"#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;

uniform sampler2D texture0;
uniform vec3 u_PrimaryColor;   // Mid-gray armor/cloth tint
uniform vec3 u_SecondaryColor; // Dark-gray shadow/armor tint
uniform vec3 u_GlowColor;      // Pure-white energy trim & blade tint
uniform float u_PortalMode;    // > 0.5 renders screen-space cosmic starfield inside white mask
uniform float u_Time;
uniform vec2 u_Resolution;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

void main()
{
    vec4 tex = texture(texture0, fragTexCoord);
    if (tex.a < 0.01) discard;

    // Un-premultiply RGB before luma so bilinear borders do not cause dark edge halos
    vec3 rgb = clamp(tex.rgb / max(tex.a, 0.001), 0.0, 1.0);
    float luma = dot(rgb, vec3(0.299, 0.587, 0.114));

    // Preserve crisp black ink outlines across all parts (including portal head)
    float outlineMask = 1.0 - smoothstep(0.03, 0.10, luma);
    vec3 inkColor = vec3(0.02, 0.01, 0.03);

    // Silhouette rim-light detection via neighboring alpha samples
    vec2 texel = 1.0 / max(vec2(textureSize(texture0, 0)), vec2(64.0));
    float aUp    = texture(texture0, clamp(fragTexCoord + vec2(0.0, -texel.y * 2.6), 0.0, 1.0)).a;
    float aSideL = texture(texture0, clamp(fragTexCoord + vec2(-texel.x * 2.2, -texel.y * 0.8), 0.0, 1.0)).a;
    float aSideR = texture(texture0, clamp(fragTexCoord + vec2( texel.x * 2.2, -texel.y * 0.8), 0.0, 1.0)).a;
    float minNeighborA = min(aUp, min(aSideL, aSideR));
    float rimMask = smoothstep(0.35, 0.95, tex.a) * (1.0 - smoothstep(0.10, 0.75, minNeighborA));

    vec3 cDark = (dot(u_SecondaryColor, u_SecondaryColor) > 0.0001) ? u_SecondaryColor : vec3(0.14, 0.12, 0.22);
    vec3 cMid  = (dot(u_PrimaryColor, u_PrimaryColor) > 0.0001)     ? u_PrimaryColor   : vec3(0.45, 0.42, 0.58);
    vec3 cGlow = (dot(u_GlowColor, u_GlowColor) > 0.0001)           ? u_GlowColor      : vec3(0.72, 0.25, 1.00);
    vec3 rimTint = mix(cGlow, vec3(1.0), 0.35);

    // Screen-Space Cosmic Portal Mode (head.png white interior, black outline preserved)
    if (u_PortalMode > 0.5)
    {
        vec2 res = (u_Resolution.x > 0.0) ? u_Resolution : vec2(800.0, 600.0);
        vec2 screenUV = gl_FragCoord.xy / res;
        vec2 cosmicUV = screenUV * 6.0 + vec2(u_Time * 0.15, u_Time * 0.08);
        float star = step(0.984, hash(floor(cosmicUV * 20.0)));
        float swirl = 0.5 + 0.5 * sin(cosmicUV.x * 3.2 + sin(cosmicUV.y * 2.6 + u_Time * 2.0));
        vec3 nebula = mix(vec3(0.05, 0.01, 0.14), vec3(0.58, 0.14, 0.95), swirl) + vec3(star * 1.2);
        vec3 portalCol = mix(nebula, inkColor, outlineMask);
        portalCol += rimTint * rimMask * 0.35;
        finalColor = vec4(portalCol, tex.a) * fragColor;
        return;
    }

    // Subtle top-down + cylindrical surface shading for armor/cloth depth
    float dx = (fragTexCoord.x - 0.5) * 2.0;
    float vertShade = mix(1.18, 0.74, smoothstep(0.04, 0.96, fragTexCoord.y));
    float cylShade  = 1.0 - 0.14 * (dx * dx);
    float volumeShade = vertShade * cylShade;

    vec3 mapped;
    if (luma < 0.45)
    {
        // Dark gray armor panels -> Secondary to Primary with volume shading
        float t = clamp((luma - 0.10) / 0.35, 0.0, 1.0);
        mapped = mix(cDark, cMid, t) * volumeShade;
    }
    else if (luma < 0.85)
    {
        // Light gray highlights (e.g. sword_1 tsuba, armor accents)
        float t = (luma - 0.45) / 0.40;
        mapped = mix(cMid, mix(cMid * 1.45, cGlow, 0.35), t) * volumeShade;
    }
    else
    {
        // Pure white bands (body stripes/diamonds, hat rim, sword_2 energy blade)
        mapped = cGlow * 1.22;
    }

    mapped += rimTint * rimMask * 0.72;
    vec3 finalRgb = mix(mapped, inkColor, outlineMask);
    finalRgb += rimTint * rimMask * 0.20;
    finalColor = vec4(finalRgb, tex.a) * fragColor;
}";

    public const string UiButtonFragment = @"#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;

uniform vec2 u_Size;          // Button width and height in pixels
uniform float u_Time;         // Elapsed time for animations
uniform float u_Hover;        // 0.0 = Idle, 1.0 = Hovered (smoothly lerped)
uniform float u_Selected;     // 0.0 = Normal, 1.0 = Selected
uniform vec3 u_AccentColor;   // Border/glow highlight color

float sdRoundedBox(vec2 p, vec2 b, float r)
{
    vec2 q = abs(p) - b + r;
    return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
}

void main()
{
    vec2 size = max(u_Size, vec2(16.0, 16.0));
    vec2 pixelPos = (fragTexCoord - 0.5) * size;
    vec2 halfSize = size * 0.5;

    float radius = min(8.0, min(halfSize.x, halfSize.y) * 0.38);
    float dist = sdRoundedBox(pixelPos, halfSize - vec2(1.5), radius);

    // Anti-aliased outer cutout (alpha blending handles transparent outer corners without discard)
    float outerAlpha = 1.0 - smoothstep(-0.75, 0.75, dist);

    // Base dark metallic gradient
    vec3 bgTop = mix(vec3(0.16, 0.16, 0.20), vec3(0.24, 0.22, 0.34), u_Hover);
    vec3 bgBot = mix(vec3(0.09, 0.09, 0.12), vec3(0.14, 0.12, 0.22), u_Hover);
    vec3 col = mix(bgTop, bgBot, fragTexCoord.y);

    // Subtle horizontal scanlines
    float scanline = 0.5 + 0.5 * sin(fragTexCoord.y * size.y * 1.6);
    col -= vec3(0.025) * scanline;

    // Animated diagonal hover sheen
    if (u_Hover > 0.01)
    {
        float sweepPos = mod(u_Time * 1.4, 2.6) - 0.8;
        float diag = (fragTexCoord.x * 0.85) + (fragTexCoord.y * 0.35);
        float sheen = smoothstep(0.18, 0.0, abs(diag - sweepPos)) * u_Hover * 0.28;
        col += u_AccentColor * sheen;
    }

    // Crisp SDF border + inner glow
    float borderThickness = mix(1.6, 2.6, max(u_Hover, u_Selected));
    float borderMask = 1.0 - smoothstep(0.0, 1.2, abs(dist + borderThickness * 0.5) - borderThickness * 0.5);

    vec3 idleBorder = vec3(0.45, 0.46, 0.52);
    vec3 activeBorder = (u_Selected > 0.5) ? vec3(1.0, 0.84, 0.15) : u_AccentColor;
    vec3 borderCol = mix(idleBorder, activeBorder, max(u_Hover, u_Selected));

    // Soft inner rim glow on hover/select
    float innerGlow = exp(dist * 0.22) * 0.32 * max(u_Hover, u_Selected);
    col += activeBorder * innerGlow;

    col = mix(col, borderCol, borderMask);

    finalColor = vec4(col, outerAlpha * 0.95);
}";

    public const string SceneDefaultFragment = @"#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;

uniform float u_Time;
uniform vec2 u_Resolution;
uniform float u_GroundYPct;

float hash21(vec2 p)
{
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

void main()
{
    float groundY = (u_GroundYPct > 0.01) ? u_GroundYPct : 0.85;
    vec2 uv = fragTexCoord;

    if (uv.y < groundY)
    {
        float t = clamp(uv.y / groundY, 0.0, 1.0);
        vec3 top = vec3(0.05, 0.06, 0.12);
        vec3 mid = vec3(0.10, 0.12, 0.22);
        vec3 horizon = vec3(0.24, 0.19, 0.36);

        vec3 sky = mix(top, mid, smoothstep(0.0, 0.62, t));
        sky = mix(sky, horizon, smoothstep(0.55, 1.0, t));

        // Subtle atmospheric haze bands + horizon bloom
        float haze = 0.5 + 0.5 * sin(uv.x * 5.0 + u_Time * 0.12) * cos(uv.y * 7.0 - u_Time * 0.08);
        sky += vec3(0.03, 0.04, 0.08) * haze * smoothstep(0.2, 0.95, t);
        sky += vec3(0.26, 0.20, 0.42) * exp(-(groundY - uv.y) * 14.0) * 0.22;

        finalColor = vec4(sky, 1.0) * fragColor;
    }
    else
    {
        float ft = clamp((uv.y - groundY) / max(0.001, 1.0 - groundY), 0.0, 1.0);
        float depth = ft + 0.15;
        float worldX = (uv.x - 0.5) / depth;
        float worldZ = 1.0 / depth;

        vec3 floorTop = vec3(0.15, 0.16, 0.24);
        vec3 floorBot = vec3(0.05, 0.05, 0.09);
        vec3 col = mix(floorTop, floorBot, pow(ft, 0.82));

        // Subtle alternating stone tiles with perspective convergence
        float checker = mod(floor(worldX * 6.5) + floor(worldZ * 1.4), 2.0);
        float lineFade = smoothstep(0.02, 0.28, ft);
        col *= mix(0.95, 1.06, checker * lineFade);

        float lineX = 1.0 - smoothstep(0.0, 0.045, abs(fract(worldX * 6.5) - 0.5));
        float lineZ = 1.0 - smoothstep(0.0, 0.055, abs(fract(worldZ * 1.4) - 0.5));
        float gridLine = max(lineX, lineZ) * lineFade;
        col = mix(col, vec3(0.38, 0.42, 0.60), gridLine * 0.38);

        // Soft horizon sky reflection sheen + fine stone grain
        col += vec3(0.14, 0.12, 0.24) * exp(-ft * 5.5);
        float grain = hash21(floor(uv * 380.0)) - 0.5;
        col += grain * 0.02;

        // Crisp illuminated horizon trim at GroundY
        float edge = 1.0 - smoothstep(0.0, 0.005, abs(uv.y - groundY));
        col = mix(col, vec3(0.62, 0.66, 0.86), edge * 0.85);

        finalColor = vec4(col, 1.0) * fragColor;
    }
}";

    public const string SceneParallaxFragment = @"#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;

uniform float u_Time;
uniform float u_GroundYPct;

void main()
{
    float groundY = (u_GroundYPct > 0.01) ? u_GroundYPct : 0.85;
    vec2 uv = fragTexCoord;
    if (uv.y >= groundY)
    {
        finalColor = vec4(0.0);
        return;
    }

    // Distant silhouette ridges above horizon
    float ridgeH = 0.085 + 0.045 * sin(uv.x * 7.5 + 0.6) + 0.028 * cos(uv.x * 16.0 - 1.2);
    float ridgeMask = smoothstep(groundY - ridgeH, groundY - ridgeH + 0.006, uv.y);

    // Repeating monolith pillars with faint glowing slits
    float colPhase = fract(uv.x * 5.0);
    float pillarW = step(abs(colPhase - 0.5), 0.068);
    float pillarH = groundY - (0.30 + 0.05 * sin(floor(uv.x * 5.0) * 2.1));
    float pillarMask = pillarW * smoothstep(pillarH, pillarH + 0.005, uv.y);

    float slitX = 1.0 - smoothstep(0.0, 0.012, abs(colPhase - 0.5));
    float slitY = smoothstep(pillarH + 0.03, pillarH + 0.05, uv.y) * (1.0 - smoothstep(groundY - 0.08, groundY - 0.04, uv.y));
    float pulse = 0.65 + 0.35 * sin(u_Time * 1.4 + floor(uv.x * 5.0) * 1.7);
    float runeGlow = slitX * slitY * pulse;

    vec3 ridgeCol = vec3(0.09, 0.09, 0.17);
    vec3 pillarCol = vec3(0.11, 0.11, 0.20) + vec3(0.32, 0.45, 0.85) * runeGlow * 0.55;

    vec3 col = mix(ridgeCol, pillarCol, pillarMask);
    float alpha = max(ridgeMask * 0.62, pillarMask * 0.72);
    float horizonFade = 1.0 - smoothstep(groundY - 0.05, groundY, uv.y) * 0.35;
    finalColor = vec4(col, alpha * horizonFade) * fragColor;
}";

    public const string SceneForegroundFragment = @"#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;

uniform float u_Time;
uniform float u_GroundYPct;

float hash21(vec2 p)
{
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

void main()
{
    float groundY = (u_GroundYPct > 0.01) ? u_GroundYPct : 0.85;
    vec2 uv = fragTexCoord;

    // Low-lying rolling mist hugging the floor horizon
    float mistBand = exp(-abs(uv.y - (groundY + 0.01)) * 18.0);
    float wave = 0.5 + 0.5 * sin(uv.x * 11.0 - u_Time * 0.45 + sin(uv.x * 4.0 + u_Time * 0.2));
    float mistAlpha = mistBand * mix(0.04, 0.14, wave);

    // Faint drifting foreground dust motes
    vec2 moteUV = uv * vec2(18.0, 10.0) + vec2(u_Time * 0.18, -u_Time * 0.06);
    vec2 id = floor(moteUV);
    vec2 gv = fract(moteUV) - 0.5;
    float rnd = hash21(id);
    float mote = (rnd > 0.88) ? smoothstep(0.14, 0.0, length(gv - (vec2(hash21(id + 3.1), hash21(id + 7.7)) - 0.5) * 0.5)) : 0.0;
    float moteAlpha = mote * 0.14 * (0.5 + 0.5 * sin(u_Time * 2.5 + rnd * 20.0));

    vec3 col = mix(vec3(0.45, 0.50, 0.78), vec3(0.85, 0.88, 1.0), clamp(moteAlpha * 4.0, 0.0, 1.0));
    finalColor = vec4(col, clamp(mistAlpha + moteAlpha, 0.0, 0.18)) * fragColor;
}";

    public const string HudBarFragment = @"#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;

uniform vec2 u_Size;
uniform float u_Time;
uniform float u_FillPct;
uniform float u_TrailPct;
uniform float u_RightToLeft;
uniform float u_SkewDir;
uniform vec4 u_BgColor;
uniform vec4 u_FillColor;
uniform vec4 u_BorderColor;
uniform float u_BorderThickness;

float sdRoundedBox(vec2 p, vec2 b, float r)
{
    vec2 q = abs(p) - b + r;
    return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
}

void main()
{
    vec2 size = max(u_Size, vec2(16.0, 8.0));
    vec2 halfSize = size * 0.5;
    vec2 p = (fragTexCoord - 0.5) * size;

    float skewDir = (abs(u_SkewDir) > 0.01) ? u_SkewDir : ((u_RightToLeft > 0.5) ? -1.0 : 1.0);
    float skewOffset = (fragTexCoord.y - 0.5) * (size.y * 0.36) * skewDir;
    float insetX = size.y * 0.24;

    vec2 boxHalf = vec2(max(2.0, halfSize.x - insetX - 1.5), max(2.0, halfSize.y - 1.5));
    float radius = min(4.0, boxHalf.y * 0.35);
    float dist = sdRoundedBox(vec2(p.x - skewOffset, p.y), boxHalf, radius);

    float outerAlpha = 1.0 - smoothstep(-0.8, 0.8, dist);
    if (outerAlpha <= 0.005)
    {
        finalColor = vec4(0.0);
        return;
    }

    // Skew-aligned horizontal progress coordinate
    float skewedU = fragTexCoord.x - (skewOffset / size.x);
    float innerMin = insetX / size.x;
    float normU = clamp((skewedU - innerMin) / max(0.001, 1.0 - 2.0 * innerMin), 0.0, 1.0);
    float barT = (u_RightToLeft > 0.5) ? (1.0 - normU) : normU;

    // Recessed track with subtle diagonal tech pattern
    vec3 trackBase = (u_BgColor.a > 0.01) ? u_BgColor.rgb * 0.42 : vec3(0.07, 0.08, 0.12);
    float stripe = 0.5 + 0.5 * sin((p.x + p.y * 1.4) * 0.55);
    vec3 col = trackBase * mix(0.88, 1.08, stripe);
    float alpha = (u_BgColor.a > 0.01) ? max(u_BgColor.a, 0.86) : 0.86;

    float fillPct = clamp(u_FillPct, 0.0, 1.0);
    float trailPct = clamp(u_TrailPct, -1.0, 1.0);

    // Damage trail behind live health
    if (trailPct > fillPct && barT <= trailPct)
    {
        vec3 trailTop = vec3(1.0, 0.88, 0.42);
        vec3 trailBot = vec3(0.88, 0.55, 0.12);
        col = mix(trailTop, trailBot, fragTexCoord.y);
        alpha = 0.96;
    }

    // Live health/cooldown fill with vertical gradient + animated energy sheen
    if (fillPct > 0.001 && barT <= fillPct)
    {
        vec3 fillTop = min(u_FillColor.rgb * 1.25 + vec3(0.06), vec3(1.0));
        vec3 fillBot = u_FillColor.rgb * 0.58;
        vec3 fillCol = mix(fillTop, fillBot, smoothstep(0.15, 0.95, fragTexCoord.y));

        // Animated sweeping energy sheen
        float sweepPos = mod(u_Time * 0.75, 2.4) - 0.5;
        float sheen = smoothstep(0.18, 0.0, abs(barT - sweepPos)) * 0.22;
        fillCol += vec3(1.0) * sheen;

        // Bright meniscus highlight at the active fill edge
        if (fillPct < 0.995)
        {
            float edgeGlow = 1.0 - smoothstep(0.0, 0.016, abs(barT - fillPct));
            fillCol = mix(fillCol, vec3(1.0, 0.98, 0.95), edgeGlow * 0.75);
        }

        col = fillCol;
        alpha = max(u_FillColor.a, 0.94);
    }

    // Glossy top glass highlight
    float gloss = smoothstep(0.06, 0.16, fragTexCoord.y) * (1.0 - smoothstep(0.24, 0.44, fragTexCoord.y));
    col += vec3(1.0) * gloss * 0.16;

    // Crisp beveled border frame
    if (u_BorderThickness > 0.01)
    {
        float bt = max(1.2, u_BorderThickness);
        float borderMask = 1.0 - smoothstep(0.0, 1.1, abs(dist + bt * 0.5) - bt * 0.5);
        vec3 borderTop = min(u_BorderColor.rgb * 1.15, vec3(1.0));
        vec3 borderBot = u_BorderColor.rgb * 0.65;
        vec3 borderCol = mix(borderTop, borderBot, fragTexCoord.y);
        col = mix(col, borderCol, borderMask * 0.95);
        alpha = max(alpha, borderMask * 0.95);
    }

    finalColor = vec4(col, outerAlpha * alpha) * fragColor;
}";

    public const string CosmicSlashFragment = @"#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;

uniform float u_Time;
uniform float u_Progress;    // 0.0 -> 1.0 over slash lifetime
uniform vec3 u_AccentColor;  // Edge highlight
uniform float u_FlipX;       // 1.0 when facing left
uniform float u_Reverse;     // 0.0 = Top->Bottom (overhead), 1.0 = Bottom->Top (uppercut)

float hash21(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

float starField(vec2 uv, float density) {
    vec2 id = floor(uv * density);
    vec2 gv = fract(uv * density) - 0.5;
    float rnd = hash21(id);
    if (rnd < 0.84) return 0.0;
    vec2 off = vec2(hash21(id + 11.3), hash21(id + 27.1)) - 0.5;
    return smoothstep(0.16, 0.0, length(gv - off * 0.55));
}

void main()
{
    vec2 uv = fragTexCoord;
    vec2 p = (uv - 0.5) * 2.0;
    float r = length(p);
    float angle = atan(p.y, p.x);

    // Sweeps from uv.y=0 (top normally, bottom when src.height = -1) to uv.y=1
    float startA = -2.25;
    float endA   =  1.45;
    float arcT = (angle - startA) / (endA - startA);
    if (arcT < 0.0 || arcT > 1.0) discard;

    // Readable whip sweep: head leads from 0->1 over first 45% of lifetime, tail chases behind
    float head = smoothstep(0.0, 0.45, u_Progress);
    float tail = smoothstep(0.12, 1.0, u_Progress);

    float span = max(0.001, head - tail);
    float localT = (arcT - tail) / span; // 0.0 = trailing tail, 1.0 = leading blade tip
    if (localT <= 0.0 || localT >= 1.0) discard;

    // Asymmetric comet taper: razor-thin at tail (0.0), widest near leading tip (0.85)
    float taper = pow(localT, 0.65) * smoothstep(1.0, 0.82, localT);
    float thickness = taper * 0.48 * (1.0 - u_Progress * 0.30);
    float outerR = 0.94;
    float innerR = outerR - thickness;

    float outerCut = 1.0 - smoothstep(outerR - 0.02, outerR + 0.01, r);
    float innerCut = smoothstep(innerR - 0.02, innerR + 0.03, r);
    float shape = outerCut * innerCut;

    if (shape < 0.02) discard;

    // Dark night-sky interior with pinpoint stars
    vec2 skyUV = uv * 1.4 + vec2(u_Time * 0.08, -u_Time * 0.05);
    float stars = starField(skyUV, 26.0) + starField(skyUV + 0.31, 48.0) * 0.75;
    vec3 darkSky = vec3(0.03, 0.015, 0.085) + vec3(0.95, 0.92, 1.0) * stars * 1.3;

    // Glowing outer edge + white-hot leading tip
    float edgeBand = smoothstep(outerR - 0.10, outerR - 0.01, r);
    float hotCore  = smoothstep(outerR - 0.035, outerR - 0.005, r);
    float tipFlash = smoothstep(0.65, 0.98, localT);

    vec3 accent = (length(u_AccentColor) > 0.01) ? u_AccentColor : vec3(0.72, 0.25, 1.0);
    vec3 col = mix(darkSky, accent * 1.35, edgeBand * 0.92);
    col = mix(col, vec3(1.0, 0.97, 1.0), clamp(hotCore * 0.85 + tipFlash * 0.75, 0.0, 1.0));

    float alpha = shape * smoothstep(0.0, 0.18, localT) * (1.0 - smoothstep(0.78, 1.0, u_Progress));
    finalColor = vec4(col, alpha);
}";

    public static readonly IReadOnlyDictionary<string, string> FragmentSources = new Dictionary<string, string>
    {
        [PostFxCosmicId]    = PostFxCosmicFragment,
        [SpriteLutId]       = SpriteLutFragment,
        [UiButtonId]        = UiButtonFragment,
        [SceneDefaultId]    = SceneDefaultFragment,
        [SceneParallaxId]   = SceneParallaxFragment,
        [SceneForegroundId] = SceneForegroundFragment,
        [HudBarId]          = HudBarFragment,
        [CosmicSlashId]     = CosmicSlashFragment
    };
}
