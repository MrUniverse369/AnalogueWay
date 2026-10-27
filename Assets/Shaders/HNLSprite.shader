// FX: the shared sprite shader for Harvey's Nine Lives (URP 2D Renderer, unlit like Sprites/Default).
// With every effect at zero it draws exactly like Sprites/Default, so it can be swapped onto any sprite.
//
// Runtime effects (set per renderer by SpriteFX through a MaterialPropertyBlock):
//   _FlashColor     rgb = flash colour, a = amount (hit flashes: cream on Harvey, white on enemies and bosses)
//   _Dither         0..1 ordered (Bayer) dither in art pixels: the invincibility flicker and pixel dissolves.
//                   _DitherShift moves the pattern (flicker), _DitherNoise 1 = random pixels instead of Bayer.
//   _Grey           0..1 greyscale (locked/unaffordable, game-over pulse)
//   _Stone          0..1 grey stone overlay with pixel speckle and cracks (Stone Skin)
//   _PaletteAmount  0..1 luminance palette swap onto _PaletteDark/_PaletteMid/_PaletteLight (boss stages);
//                   keeps the sprite bright, unlike multiplying the colour
//
// Material effects (keywords, one material per use, so everything that shares it still batches):
//   _HNL_SHINE   diagonal glint sweeping across opaque pixels (coins, chests, spikes, traps)
//   _HNL_SWAY    wind sway of the top of the sprite, snapped to whole art pixels (trees, bushes)
//   _HNL_FLUFF   soft cream rim on top, cool shade underneath, breathing puffs and a springy squash (bounce clouds)
//   _HNL_CLOUDS  pixel clouds drifting inside the sprite (parallax skies): a library of designed cloud silhouettes
//                (Tools/Art/sky_clouds.py -> Resources/FX/SkyCloudAtlas.png + SkyCloudLibrary.hlsl) laid out in rows,
//                with a bubbling edge, distance haze and an optional sun rim; ZoneAtmosphere sets _CloudArea and
//                _CloudBand per sky
//   _HNL_HAZE    heat shimmer near a horizon line (Egypt backgrounds)
//   _HNL_PATTERN a slowly scrolling pattern of paw prints and twinkling stars (the title screen backdrop)
//   _HNL_GHOST   translucent, gently shimmering body with a pixel purple halo; green pixels (eyes, flames) stay solid
//                (the Tomb's purple ghoul)
Shader "AnalogueWay/HNLSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _PixelsPerUnit ("Art pixels per unit", Float) = 16

        [Header(Runtime)]
        _FlashColor ("Flash (a = amount)", Color) = (1, 1, 1, 0)
        _Dither ("Dither", Range(0, 1)) = 0
        _DitherShift ("Dither pattern shift", Float) = 0
        _DitherNoise ("Dither random pixels", Range(0, 1)) = 0
        _Grey ("Greyscale", Range(0, 1)) = 0
        _Stone ("Stone", Range(0, 1)) = 0
        _PaletteAmount ("Palette swap", Range(0, 1)) = 0
        _PaletteDark ("Palette dark", Color) = (0.2, 0.22, 0.36, 1)
        _PaletteMid ("Palette mid", Color) = (0.6, 0.6, 0.6, 1)
        _PaletteLight ("Palette light", Color) = (1, 1, 0.92, 1)

        [Header(Shine)]
        [Toggle(_HNL_SHINE)] _UseShine ("Shine sweep", Float) = 0
        _ShineColor ("Shine colour (a = strength)", Color) = (1, 1, 0.92, 0.75)
        _ShineWidth ("Shine width (art pixels)", Float) = 2
        _ShineSpacing ("World units between sweeps", Float) = 7
        _ShineSpeed ("Sweep speed (units/s)", Float) = 3

        [Header(Sway)]
        [Toggle(_HNL_SWAY)] _UseSway ("Wind sway", Float) = 0
        _SwayPixels ("Sway (art pixels)", Float) = 1
        _SwaySpeed ("Sway speed", Float) = 1.3
        _SwayBase ("Sway starts at local y", Float) = 0
        _SwayHeight ("Full sway at height above", Float) = 2

        [Header(Fluff)]
        [Toggle(_HNL_FLUFF)] _UseFluff ("Fluffy cloud", Float) = 0
        _FluffLight ("Rim light", Color) = (1, 1, 0.92, 1)
        _FluffShade ("Under shade", Color) = (0.69, 0.78, 0.92, 1)
        _Squash ("Squash (runtime)", Float) = 0

        [Header(Sky clouds)]
        [Toggle(_HNL_CLOUDS)] _UseClouds ("Scrolling clouds", Float) = 0
        [NoScaleOffset] _CloudAtlas ("Cloud library (Tools/Art/sky_clouds.py)", 2D) = "black" {}
        _CloudColor ("Body", Color) = (0.855, 0.929, 0.988, 1)
        _CloudShadeColor ("Shade (back masses, seams)", Color) = (0.831, 0.871, 0.984, 1)
        _CloudRim ("Sun rim on top edges (a = strength)", Color) = (1, 1, 1, 0)
        _CloudHaze ("Distance haze on the far (lower) rows", Range(0, 0.8)) = 0.3
        [IntRange] _CloudType ("Kind (-1 mixed, 0 wide, 1 tall, 2 medium, 3 small)", Range(-1, 3)) = -1
        _CloudSeed ("Layout seed", Float) = 0
        _CloudCover ("Cloud cover (share of slots)", Range(0, 1)) = 0.65
        _CloudGap ("Gap before each cloud (art pixels: min, max)", Vector) = (24, 150, 0, 0)
        _CloudRow ("Row height (art pixels, 56+)", Float) = 64
        _CloudSpan ("Layout span (art pixels, 300+; no cloud crosses one)", Float) = 480
        [IntRange] _CloudPixel ("Pixel size", Range(1, 3)) = 1
        _CloudSpeed ("Drift speed (units/s)", Float) = 0.15
        _CloudBoil ("Edge boil (frame steps/s, 0 = still)", Range(0, 3)) = 0.5
        _CloudBand ("Band (units up from the sprite's bottom: min, max)", Vector) = (0, 100, 0, 0)
        _CloudArea ("Sprite world size (set by ZoneAtmosphere)", Vector) = (0, 0, 0, 0)

        [Header(Heat haze)]
        [Toggle(_HNL_HAZE)] _UseHaze ("Heat haze", Float) = 0
        _HazeY ("Horizon world y", Float) = 0
        _HazeHeight ("Haze height (units)", Float) = 2
        _HazePixels ("Haze shimmer (art pixels)", Float) = 1
        _HazeSpeed ("Haze speed", Float) = 3

        [Header(Pattern)]
        [Toggle(_HNL_PATTERN)] _UsePattern ("Paw and star pattern", Float) = 0
        _PatternColor ("Pattern colour (a = strength)", Color) = (0.42, 0.36, 0.62, 0.6)
        _PatternCell ("Pattern cell (art pixels)", Vector) = (32, 24, 0, 0)
        _PatternSpeed ("Pattern scroll (art pixels/s)", Vector) = (4, -3, 0, 0)
        _PatternMinY ("Pattern only above local y", Float) = -100

        [Header(Ghost)]
        [Toggle(_HNL_GHOST)] _UseGhost ("Ghost", Float) = 0
        _GhostAlpha ("Body opacity", Range(0, 1)) = 0.6
        _GhostPulse ("Opacity shimmer (+/-)", Range(0, 0.5)) = 0.12
        _GhostSpeed ("Shimmer speed", Float) = 2.2
        _GhostGlow ("Halo colour (a = strength)", Color) = (0.68, 0.42, 0.95, 0.4)
        _GhostKeep ("Keep pixels this much greener than red/blue solid", Range(0, 0.5)) = 0.08
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _HNL_SHINE
            #pragma shader_feature_local _HNL_SWAY
            #pragma shader_feature_local _HNL_FLUFF
            #pragma shader_feature_local _HNL_CLOUDS
            #pragma shader_feature_local _HNL_HAZE
            #pragma shader_feature_local _HNL_PATTERN
            #pragma shader_feature_local _HNL_GHOST
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            // NOTE: one layout for every variant (SRP Batcher)
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _PixelsPerUnit;
                half4 _FlashColor;
                float _Dither;
                float _DitherShift;
                float _DitherNoise;
                float _Grey;
                float _Stone;
                float _PaletteAmount;
                half4 _PaletteDark;
                half4 _PaletteMid;
                half4 _PaletteLight;
                half4 _ShineColor;
                float _ShineWidth;
                float _ShineSpacing;
                float _ShineSpeed;
                float _SwayPixels;
                float _SwaySpeed;
                float _SwayBase;
                float _SwayHeight;
                half4 _FluffLight;
                half4 _FluffShade;
                float _Squash;
                half4 _CloudColor;
                half4 _CloudShadeColor;
                half4 _CloudRim;
                float _CloudHaze;
                float _CloudType;
                float _CloudSeed;
                float _CloudCover;
                float4 _CloudGap;
                float _CloudRow;
                float _CloudSpan;
                float _CloudPixel;
                float _CloudSpeed;
                float _CloudBoil;
                float4 _CloudBand;
                float4 _CloudArea;
                float _HazeY;
                float _HazeHeight;
                float _HazePixels;
                float _HazeSpeed;
                half4 _PatternColor;
                float4 _PatternCell;
                float4 _PatternSpeed;
                float _PatternMinY;
                float _GhostAlpha;
                float _GhostPulse;
                float _GhostSpeed;
                half4 _GhostGlow;
                float _GhostKeep;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i), b = Hash21(i + float2(1, 0)), c = Hash21(i + float2(0, 1)), d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // 4x4 ordered dither threshold for an integer pixel
            float Bayer4(float2 px)
            {
                uint2 p = (uint2)(fmod(fmod(px, 4.0) + 4.0, 4.0));
                static const float m[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
                return (m[p.y * 4 + p.x] + 0.5) / 16.0;
            }

        #if defined(_HNL_CLOUDS)
            // Sky clouds are designed pixel art, not shader noise: Tools/Art/sky_clouds.py draws each silhouette (back
            // masses in the shade tone, front masses and a flat slab in the body tone, cauliflower edges) and bakes the
            // library into _CloudAtlas: R/G/B = the tone in boil frame 0/1/2 (0 sky, 1/3 body, 2/3 shade).
            // This only places, drifts and colours them.
            #include "SkyCloudLibrary.hlsl"
            TEXTURE2D(_CloudAtlas);
            SAMPLER(sampler_CloudAtlas);

            // a cloud for one slot, no taller than the room left in the band; near rows favour the banks and towers,
            // far rows the medium and small clouds
            int SkyCloudPick(float2 id, float depth, float room)
            {
                int kind = (int)_CloudType;
                if (kind < 0)
                {
                    float4 w = lerp(float4(0.35, 0.25, 0.28, 0.12), float4(0.15, 0.03, 0.42, 0.4), depth);
                    float k = Hash21(id + 8.3) * dot(w, 1.0);
                    kind = k < w.x ? 0 : k < w.x + w.y ? 1 : k < w.x + w.y + w.z ? 2 : 3;
                }
                int pick = kSkyKindStart[kind] + min((int)(Hash21(id) * kSkyKindCount[kind]), kSkyKindCount[kind] - 1);
                [loop] for (int j = 0; j < SKY_CLOUD_COUNT; j++)
                {
                    int c = (int)((uint)(pick + j) % (uint)SKY_CLOUD_COUNT); // too tall: the next one in the library that fits
                    if (kSkyCloudRect[c].w <= room) return c;
                }
                return -1;
            }

            int SkyCloudTone(float4 rect, float2 l, int frame)
            {
                if (l.y >= rect.w) return 0;
                float3 d = SAMPLE_TEXTURE2D_LOD(_CloudAtlas, sampler_CloudAtlas, (rect.xy + l + 0.5) / kSkyCloudAtlasSize, 0).rgb;
                return (int)round((frame == 0 ? d.r : frame == 1 ? d.g : d.b) * 3.0);
            }

            // Tone of cloud pixel (x, y) from one row: 0 sky, 1 body, 2 shade, 3 body on a top edge (sun rim).
            // Each row is a fixed sequence (gap, cloud, gap, cloud...) inside spans that tile the sky's width, so no
            // two clouds overlap, none crosses a span edge, and tiled parallax skies meet seamlessly.
            int SkyCloudRow(float x, float y, float row, float rowH, float bandH, float depth, float period, float t)
            {
                float rh = Hash21(float2(row + 0.31, _CloudSeed + 5.7));
                float speed = _CloudSpeed * (1.0 - 0.45 * depth) * (0.85 + 0.3 * rh); // far rows drift slower
                x += floor(t * speed * _PixelsPerUnit / max(_CloudPixel, 1.0)) + floor(rh * period); // whole-pixel drift
                x -= floor(x / period) * period;
                float spans = max(1.0, round(period / max(_CloudSpan, 300.0)));
                float spanW = period / spans;
                float span = floor(x / spanW);
                float u = x - span * spanW;

                float cursor = 0;
                [loop] for (int i = 0; i < 16; i++)
                {
                    float2 id = float2(span * 17.0 + i + 0.37, row * 7.1 + _CloudSeed);
                    cursor += floor(lerp(_CloudGap.x, _CloudGap.y, Hash21(id + 3.3)));
                    if (cursor > u) return 0;
                    float y0 = row * rowH + floor(Hash21(id + 4.1) * rowH * 0.5);
                    int c = SkyCloudPick(id, depth, bandH - y0);
                    if (c < 0) return 0;
                    float4 rect = kSkyCloudRect[c];
                    if (cursor + rect.z > spanW) return 0; // the span's tail stays clear sky
                    if (u < cursor + rect.z)
                    {
                        float2 l = float2(u - cursor, y - y0);
                        if (Hash21(id + 0.5) > _CloudCover || l.y < 0) return 0;
                        if (Hash21(id + 6.6) > 0.5) l.x = rect.z - 1 - l.x; // mirrored
                        int step = (int)fmod(floor(t * _CloudBoil + Hash21(id + 9.7) * 4.0), 4.0); // boil: 0 1 2 1
                        int frame = step == 3 ? 1 : step;
                        int tone = SkyCloudTone(rect, l, frame);
                        if (tone == 1 && _CloudRim.a > 0 && SkyCloudTone(rect, l + float2(0, 1), frame) == 0) tone = 3;
                        return tone;
                    }
                    cursor += rect.z;
                }
                return 0;
            }

            // p = whole cloud pixels from the sprite's bottom-left corner; depth = 1 on the farthest (lowest) row
            int SkyCloudShade(float2 p, float t, out float depth)
            {
                float ppu = _PixelsPerUnit / max(_CloudPixel, 1.0);
                float2 band = floor(_CloudBand.xy * ppu);
                float y = p.y - band.x;
                float bandH = band.y - band.x;
                float period = _CloudArea.x > 0 ? _CloudArea.x * ppu : 100000.0;
                float rowH = max(_CloudRow, 56.0); // so a cloud (up to 82 tall, raised up to half a row) reaches at most one row up
                float rows = max(1.0, floor(bandH / rowH));
                float row = min(floor(y / rowH), rows - 1.0);
                depth = 0;
                if (y < 0 || y >= bandH) return 0;

                // the pixel's own row is nearer, so it is drawn over a cloud reaching up from the row below
                [unroll] for (int k = 0; k < 2; k++)
                {
                    float r = row - k;
                    depth = rows > 1.0 ? 1.0 - max(r, 0.0) / (rows - 1.0) : 0.0;
                    int tone = r >= 0 ? SkyCloudRow(p.x, y, r, rowH, bandH, depth, period, t) : 0;
                    if (tone > 0) return tone;
                }
                return 0;
            }
        #endif

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                SetUpSpriteInstanceProperties();
                float3 pos = UnityFlipSprite(v.positionOS, unity_SpriteProps.xy);
                // springy squash about the sprite's pivot: wider and flatter when pressed, taller on the rebound
                // (bounce clouds; MUSHROOM/FOXCHARGE: the bounce mushroom and the fox's crouch too; 0 = untouched)
                pos.x *= 1.0 + _Squash;
                pos.y *= 1.0 - _Squash;
                o.positionOS = pos;
                o.positionWS = TransformObjectToWorld(pos);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * unity_SpriteColor;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                float2 artPx = floor(i.positionWS.xy * _PixelsPerUnit); // world position in whole art pixels
                float t = _Time.y;

            #if defined(_HNL_SWAY)
                // only the top sways; whole-pixel steps per row keep the pixel art crisp
                float sw = saturate((i.positionOS.y - _SwayBase) / max(_SwayHeight, 0.01));
                float swayPx = round(sin(t * _SwaySpeed + i.positionWS.x * 0.55 - artPx.y * 0.02) * _SwayPixels * sw * sw);
                uv.x -= swayPx * _MainTex_TexelSize.x;
            #endif
            #if defined(_HNL_HAZE)
                float hz = 1.0 - saturate(abs(i.positionWS.y - _HazeY) / max(_HazeHeight, 0.01));
                float hazePx = round(sin(artPx.y * 0.9 + t * _HazeSpeed) * _HazePixels * hz * hz);
                uv.x += hazePx * _MainTex_TexelSize.x;
            #endif
            #if defined(_HNL_FLUFF)
                // puffs breathe: the left/right edges of each row shift by a pixel now and then
                float puff = round(sin(t * 1.1 + floor(artPx.y / 3.0) * 1.3) * 0.6); // rows move in 3-pixel bands
                uv.x += puff * _MainTex_TexelSize.x;
            #endif

            #if defined(_HNL_SWAY) || defined(_HNL_HAZE)
                // sample the art pixel's centre so a shifted row never lands between two texels
                uv = (floor(uv * _MainTex_TexelSize.zw) + 0.5) * _MainTex_TexelSize.xy;
            #endif
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                half4 c = tex * i.color;

            #if defined(_HNL_FLUFF)
                {
                    float2 up = float2(0, _MainTex_TexelSize.y);
                    half aboveA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + up).a;
                    half above2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + up * 2).a;
                    half belowA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - up).a;
                    half below2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - up * 2).a;
                    if (tex.a > 0.5)
                    {
                        if (aboveA < 0.5 || above2 < 0.5) c.rgb = lerp(c.rgb, _FluffLight.rgb, aboveA < 0.5 ? 0.85 : 0.45);
                        else if (belowA < 0.5 || below2 < 0.5) c.rgb = lerp(c.rgb, _FluffShade.rgb, belowA < 0.5 ? 0.8 : 0.4);
                    }
                }
            #endif

            #if defined(_HNL_CLOUDS)
                {
                    // whole cloud pixels from the sprite's corner (uv x its world size): right for scaled box skies and for
                    // batched sprites, and the clouds ride with the parallax layer exactly like its own texels
                    float ppu = _PixelsPerUnit / max(_CloudPixel, 1.0);
                    float2 cp = _CloudArea.x > 0 ? floor(i.uv * _CloudArea.xy * ppu) : floor(i.positionOS.xy * ppu);
                    float depth;
                    int shade = tex.a > 0.5 ? SkyCloudShade(cp, t, depth) : 0;
                    if (shade > 0)
                    {
                        half3 cloud = shade == 2 ? _CloudShadeColor.rgb : _CloudColor.rgb;
                        if (shade == 3) cloud = lerp(cloud, _CloudRim.rgb, _CloudRim.a);
                        c.rgb = lerp(cloud, c.rgb, _CloudHaze * depth); // far clouds sink toward the sky behind them
                    }
                }
            #endif

            #if defined(_HNL_PATTERN)
                {
                    // whole art pixels, scrolling diagonally; bricked rows of alternating paws and stars
                    float2 pp = floor(i.positionOS.xy * _PixelsPerUnit + t * _PatternSpeed.xy);
                    float2 cell = _PatternCell.xy;
                    float row = floor(pp.y / cell.y);
                    pp.x += abs(fmod(row, 2.0)) * cell.x * 0.5;
                    float2 id = floor(pp / cell);
                    float2 d = pp - id * cell - floor(cell * 0.5);
                    float m = 0;
                    if (abs(fmod(id.x + id.y, 2.0)) < 0.5)
                    {
                        // paw print: a pad with three toes above it
                        float pad = step(dot(d / float2(2.6, 2.0), d / float2(2.6, 2.0)), 1.0);
                        float toes = step(length(d - float2(-3, 3)), 1.2) + step(length(d - float2(0, 4)), 1.2) + step(length(d - float2(3, 3)), 1.2);
                        m = saturate(pad + toes);
                    }
                    else
                    {
                        // four-point star that twinkles on its own beat
                        float tw = 0.5 + 0.5 * sin(t * 1.7 + Hash21(id) * 6.28);
                        float arm = tw > 0.55 ? 2.5 : 1.5;
                        m = ((abs(d.x) < 0.5 && abs(d.y) < arm) || (abs(d.y) < 0.5 && abs(d.x) < arm)) ? (abs(d.x) + abs(d.y) < 0.5 ? 1.0 : 0.7) : 0.0;
                    }
                    float above = step(_PatternMinY, i.positionOS.y);
                    c.rgb = lerp(c.rgb, _PatternColor.rgb, m * _PatternColor.a * above * step(0.5, tex.a));
                }
            #endif

                half lum = dot(c.rgb, half3(0.299, 0.587, 0.114));

                // boss palette swap: each brightness level maps onto the stage's own three-colour ramp
                half3 ramp = lum < 0.5 ? lerp(_PaletteDark.rgb, _PaletteMid.rgb, lum * 2.0) : lerp(_PaletteMid.rgb, _PaletteLight.rgb, lum * 2.0 - 1.0);
                c.rgb = lerp(c.rgb, ramp, _PaletteAmount);

                // Stone Skin: TT stone ramp, pixel speckle and a few darker crack pixels
                float speckle = Hash21(artPx);
                float crack = step(0.9, Hash21(floor(artPx / 2.0) + 17.0)) * step(0.5, speckle);
                half3 stone = lerp(half3(0.353, 0.412, 0.490), half3(0.725, 0.824, 0.792), saturate(lum * 1.1 + (speckle - 0.5) * 0.18));
                stone = lerp(stone, half3(0.196, 0.224, 0.361), crack * 0.8);
                c.rgb = lerp(c.rgb, stone, _Stone);

                c.rgb = lerp(c.rgb, lum.xxx, _Grey);

            #if defined(_HNL_SHINE)
                {
                    // a narrow diagonal band travels along the world; any shiny sprite it crosses glints
                    float d = (artPx.x + artPx.y * 0.6) / _PixelsPerUnit - t * _ShineSpeed;
                    float m = fmod(fmod(d, _ShineSpacing) + _ShineSpacing, _ShineSpacing) * _PixelsPerUnit;
                    float shine = step(m, _ShineWidth) * step(0.5, tex.a);
                    c.rgb = lerp(c.rgb, _ShineColor.rgb, shine * _ShineColor.a);
                }
            #endif

                c.rgb = lerp(c.rgb, _FlashColor.rgb, _FlashColor.a);

            #if defined(_HNL_GHOST)
                {
                    // each ghoul shimmers on its own beat (phase from its position), never fully fading out
                    float phase = UNITY_MATRIX_M._m03 * 0.9 + UNITY_MATRIX_M._m13 * 0.6;
                    float shimmer = saturate(_GhostAlpha + _GhostPulse * (0.7 * sin(t * _GhostSpeed + phase) + 0.3 * sin(t * _GhostSpeed * 2.3 + phase * 1.7)));
                    bool keep = tex.g - max(tex.r, tex.b) > _GhostKeep; // the green eyes and flames
                    if (tex.a > 0.5)
                    {
                        if (!keep && _FlashColor.a < 0.5) c.a *= shimmer;
                    }
                    else
                    {
                        // halo: empty pixels next to the body (1 px all round, 2 px straight out) get a faint purple
                        float2 tx = _MainTex_TexelSize.xy;
                        half n1 = max(max(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(tx.x, 0)).a, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - float2(tx.x, 0)).a),
                                      max(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0, tx.y)).a, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - float2(0, tx.y)).a));
                        half n2 = max(max(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(tx.x * 2, 0)).a, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - float2(tx.x * 2, 0)).a),
                                      max(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0, tx.y * 2)).a, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - float2(0, tx.y * 2)).a));
                        half halo = n1 > 0.5 ? 1.0 : (n2 > 0.5 ? 0.45 : 0.0);
                        c = half4(_GhostGlow.rgb, _GhostGlow.a * halo * (0.75 + 0.25 * sin(t * _GhostSpeed * 0.8 + phase)) * i.color.a);
                    }
                }
            #endif

                // dither: ordered for flickers, random pixels for dissolves
                float threshold = lerp(Bayer4(artPx + float2(_DitherShift, _DitherShift * 2.0)), Hash21(artPx + 3.7), _DitherNoise);
                clip(threshold - _Dither);

                return c;
            }
            ENDHLSL
        }
    }
}
