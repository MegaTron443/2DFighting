public static class BuiltInShaders
{
    public const string PostFxCosmicId = "postfx_cosmic";
    public const string SpriteLutId = "sprite_lut";
    public const string UiButtonId = "ui_button";
    public const string SceneDefaultId = "scene_default";
    public const string CosmicSlashId = "cosmic_slash";

    public const string PostFxCosmicFragment = @"#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;

uniform sampler2D texture0;
uniform vec2 u_ShakeOffset;      // UV offset for directional screen shake
uniform float u_ChromaticSplit;  // RGB split intensity (0.0 = off, 0.015 = heavy)
uniform float u_PinchAmount;     // Black hole radial pinch (-0.5 to 0.8)
uniform vec2 u_PinchCenter;      // Normalized screen center (default 0.5, 0.5)
uniform float u_ImpactMode;      // 0 = Off, 1 = 2-Tone Monochrome, 2 = Inverted Void
uniform vec3 u_TintColor;        // Palette tint (default 1, 1, 1)
uniform float u_PaletteMix;      // 0.0 = Normal, 1.0 = Full Cosmic Palette Shift

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

    vec3 rgb = clamp(tex.rgb / max(tex.a, 0.001), 0.0, 1.0);
    float luma = dot(rgb, vec3(0.299, 0.587, 0.114));

    // Preserve crisp black ink outlines across all parts (including portal head)
    float outlineMask = 1.0 - smoothstep(0.03, 0.10, luma);
    vec3 inkColor = vec3(0.02, 0.01, 0.03);

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
        finalColor = vec4(portalCol, tex.a) * fragColor;
        return;
    }

    vec3 cDark = (dot(u_SecondaryColor, u_SecondaryColor) > 0.0001) ? u_SecondaryColor : vec3(0.14, 0.12, 0.22);
    vec3 cMid  = (dot(u_PrimaryColor, u_PrimaryColor) > 0.0001)     ? u_PrimaryColor   : vec3(0.45, 0.42, 0.58);
    vec3 cGlow = (dot(u_GlowColor, u_GlowColor) > 0.0001)           ? u_GlowColor      : vec3(0.72, 0.25, 1.00);

    vec3 mapped;
    if (luma < 0.45)
    {
        // Dark gray armor panels -> Secondary to Primary
        float t = clamp((luma - 0.10) / 0.35, 0.0, 1.0);
        mapped = mix(cDark, cMid, t);
    }
    else if (luma < 0.85)
    {
        // Light gray highlights (e.g. sword_1 tsuba, armor accents)
        float t = (luma - 0.45) / 0.40;
        mapped = mix(cMid, mix(cMid * 1.45, cGlow, 0.35), t);
    }
    else
    {
        // Pure white bands (body stripes/diamonds, hat rim, sword_2 energy blade)
        mapped = cGlow * 1.15;
    }

    vec3 finalRgb = mix(mapped, inkColor, outlineMask);
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
    uniform float u_GroundYPct;

    void main()
    {
        float groundY = (u_GroundYPct > 0.01) ? u_GroundYPct : 0.85;
        vec2 uv = fragTexCoord;

        if (uv.y < groundY)
        {
            float t = uv.y / groundY;
            vec3 top = vec3(0.08, 0.09, 0.16);
            vec3 horizon = vec3(0.18, 0.16, 0.28);
            finalColor = vec4(mix(top, horizon, t), 1.0) * fragColor;
        }
        else
        {
            float ft = (uv.y - groundY) / (1.0 - groundY);
            vec3 floorTop = vec3(0.16, 0.16, 0.22);
            vec3 floorBot = vec3(0.07, 0.07, 0.10);
            vec3 col = mix(floorTop, floorBot, ft);

            // Crisp highlight line right at GroundY
            float edge = 1.0 - smoothstep(0.0, 0.004, abs(uv.y - groundY));
            col = mix(col, vec3(0.55, 0.55, 0.70), edge);

            finalColor = vec4(col, 1.0) * fragColor;
        }
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
        [PostFxCosmicId] = PostFxCosmicFragment,
        [SpriteLutId]    = SpriteLutFragment,
        [UiButtonId]     = UiButtonFragment,
        [SceneDefaultId] = SceneDefaultFragment,
        [CosmicSlashId]  = CosmicSlashFragment  
    };
}
