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
//   _HNL_CLOUDS  pixel clouds drifting inside the sprite (parallax skies): designed lobe silhouettes (SDF circles on a
//                flat base, four archetypes) with flat highlight / base / shadow / underside tones; ZoneAtmosphere sets
//                _CloudArea and _CloudBand per sky
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
        _CloudColor ("Base", Color) = (0.835, 0.922, 1, 1)
        _CloudLight ("Highlight", Color) = (0.969, 0.988, 1, 1)
        _CloudShadeColor ("Shadow", Color) = (0.682, 0.804, 0.953, 1)
        _CloudDeep ("Underside accent (a = strength)", Color) = (0.58, 0.714, 0.918, 1)
        _CloudShading ("Shading amount", Range(0, 1)) = 0.6
        _CloudScale ("Cloud size", Range(0.5, 2)) = 1
        _CloudStretch ("Width, height", Vector) = (1, 1, 0, 0)
        [IntRange] _CloudType ("Archetype (-1 mixed, 0 wide, 1 round, 2 tall, 3 small)", Range(-1, 3)) = -1
        _CloudSeed ("Shape seed", Float) = 0
        _CloudLobe ("Primary lobe scale", Range(0.7, 1.3)) = 1
        _CloudLobeVar ("Secondary lobe variation", Range(0, 1)) = 0.5
        _CloudIrregular ("Shape irregularity", Range(0, 1)) = 0.3
        _CloudCover ("Cloud cover (share of slots)", Range(0, 1)) = 0.5
        _CloudCell ("Slot per cloud (art pixels w, h)", Vector) = (120, 58, 0, 0)
        _CloudSpeed ("Drift speed (units/s)", Float) = 0.15
        _CloudBreathe ("Breathing (art pixels)", Range(0, 1.5)) = 0
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
                half4 _CloudLight;
                half4 _CloudShadeColor;
                half4 _CloudDeep;
                float _CloudShading;
                float _CloudScale;
                float4 _CloudStretch;
                float _CloudType;
                float _CloudSeed;
                float _CloudLobe;
                float _CloudLobeVar;
                float _CloudIrregular;
                float _CloudSpeed;
                float _CloudCover;
                float4 _CloudCell;
                float _CloudBreathe;
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
            // Sky clouds are designed shapes, not thresholded noise: four archetypes, each a flat-bottomed base capsule
            // under five round lobes (art pixels, origin at the bottom centre, biggest lobe first; r = 0 = unused slot).
            // Noise only nudges the outline by about a pixel. 0 wide and low, 1 rounded, 2 tall with a tower, 3 small.
            static const float2 kCloudBase[4] = { float2(25, 7), float2(15, 7), float2(14, 7), float2(8, 5) }; // half-length, radius
            static const float2 kCloudSize[4] = { float2(70, 27), float2(58, 31), float2(56, 38), float2(32, 18) }; // width, height
            static const float3 kCloudLobes[20] =
            {
                float3(-5, 14, 11), float3(-18, 11, 8), float3(9, 13, 9), float3(21, 10, 6), float3(-29, 7, 5),
                float3(2, 17, 12), float3(-11, 12, 8), float3(15, 12, 8), float3(-21, 8, 5), float3(24, 7, 4),
                float3(-2, 17, 12), float3(5, 27, 9), float3(-14, 11, 8), float3(14, 12, 8), float3(-23, 7, 4),
                float3(-3, 9, 7), float3(7, 8, 5), float3(-11, 6, 4), float3(0, 0, 0), float3(0, 0, 0)
            };

            float CloudBody(float2 q, float2 bse, float3 lobes[5])
            {
                float d = length(float2(max(abs(q.x) - bse.x, 0.0), q.y - bse.y)) - bse.y;
                [unroll] for (int k = 0; k < 5; k++) d = min(d, length(q - lobes[k].xy) - lobes[k].z);
                return max(d, -q.y); // flat underside
            }

            // Which tone an art pixel gets: 0 sky, 1 shadow, 2 base, 3 highlight, 4 underside accent.
            // p = pixel centre in art pixels from the sprite's bottom-left corner.
            int CloudShade(float2 p, float t)
            {
                // the band is cut into rows of slots; each slot holds at most one cloud that fits inside it
                float ppu = _PixelsPerUnit;
                float2 stretch = max(_CloudStretch.xy, 0.25);
                float2 cell = max(_CloudCell.xy, 8.0) * _CloudScale * stretch;
                float2 band = _CloudBand.xy * ppu;
                float row = floor((p.y - band.x) / cell.y);
                if (p.y < band.x || row >= floor((band.y - band.x) / cell.y)) return 0;

                // every row drifts at its own pace and repeats once per sprite width, so tiled parallax skies meet seamlessly
                float hr = Hash21(float2(row + 0.31, _CloudSeed + 5.7));
                float period = _CloudArea.x > 0 ? _CloudArea.x * ppu : 100000.0;
                float slots = max(1.0, round(period / (cell.x * (0.85 + 0.3 * hr))));
                float cw = period / slots;
                float u = p.x + floor(t * _CloudSpeed * (0.7 + 0.6 * hr) * ppu) + floor(hr * period); // whole-pixel drift
                u -= floor(u / period) * period;
                float col = floor(u / cw);
                float2 id = float2(col + 0.37, row * 7.1 + _CloudSeed);
                if (Hash21(id + 0.5) > _CloudCover) return 0;

                // this slot's cloud: archetype, size, place in the slot, mirror and lobe variation
                float h = Hash21(id);
                int type = _CloudType >= 0 ? (int)_CloudType : (h < 0.3 ? 0 : h < 0.6 ? 1 : h < 0.75 ? 2 : 3);
                float2 size = kCloudSize[type] * _CloudLobe * stretch;
                float s = min(_CloudScale * (0.75 + 0.4 * Hash21(id + 1.3)),min(cw * 0.92 / size.x, cell.y * 0.96 / size.y));
                float2 slack = float2(cw, cell.y) - size * s;
                float2 origin = float2(col * cw + size.x * s * 0.5 + slack.x * Hash21(id + 2.9), row * cell.y + slack.y * Hash21(id + 4.1));
                float2 q = (float2(u, p.y - band.x) - origin) / (s * stretch);
                float flip = Hash21(id + 6.6) > 0.5 ? -1.0 : 1.0; // mirror the layout, never the light

                float3 lobes[5];
                int k;
                [unroll] for (k = 0; k < 5; k++)
                {
                    float3 L = kCloudLobes[type * 5 + k];
                    float vary = (k == 0 ? 0.15 : 0.35) * _CloudLobeVar;
                    float r = L.z * _CloudLobe * (1.0 + (Hash21(id + k * 7.13) - 0.5) * 2.0 * vary) + _CloudBreathe * sin(t * 0.8 + k * 1.9 + h * 6.28);
                    float x = (L.x + (Hash21(id + k * 3.7 + 9.2) - 0.5) * 4.0 * _CloudLobeVar) * flip;
                    lobes[k] = L.z > 0 ? float3(x, L.y * _CloudLobe, r) : float3(0, 0, -1);
                }
                float2 bse = kCloudBase[type];
                float wob = (ValueNoise(float2(q.x / 9.0, q.y / 7.0) + h * 31.0) - 0.5) * _CloudIrregular * 4.0;
                if (CloudBody(q, bse, lobes) + wob > 0) return 0;

                // shadow: whatever the lobes, lifted toward the light, no longer cover (a scalloped belly)
                float ks = 0.2 + 0.55 * _CloudShading;
                float lit = 1e5;
                [unroll] for (k = 0; k < 5; k++) lit = min(lit, length(q - lobes[k].xy - float2(-0.3, 1.0) * ks * lobes[k].z) - lobes[k].z);
                if (lit > 0) return CloudBody(q - float2(0, 1.5), bse, lobes) + wob > 0 ? 4 : 1;

                // highlight: a cap on the upper left of each lobe, unless a taller lobe in front covers it (with a
                // margin, so no one-pixel slivers are left along its edge); lobes too small for a readable cap get none
                float kh = 0.25 + 0.5 * _CloudShading;
                float margin = 1.5 / s;
                [unroll] for (k = 0; k < 5; k++)
                {
                    float3 L = lobes[k];
                    if (L.z * s < 5.0 || length(q - L.xy) >= L.z || length(q - L.xy - float2(-0.3, 0.55) * L.z) > L.z * (0.35 + 0.45 * kh)) continue;
                    bool hidden = false;
                    [unroll] for (int j = 0; j < 5; j++)
                        hidden = hidden || (j != k && length(q - lobes[j].xy) < lobes[j].z + margin && lobes[j].y + lobes[j].z > L.y + L.z);
                    if (!hidden) return 3;
                }
                return 2;
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
                    // whole art pixels from the sprite's corner (uv x its world size): right for scaled box skies and for
                    // batched sprites, and the clouds ride with the parallax layer exactly like its own texels
                    float2 cp = (_CloudArea.x > 0 ? floor(i.uv * _CloudArea.xy * _PixelsPerUnit) : floor(i.positionOS.xy * _PixelsPerUnit)) + 0.5;
                    int shade = tex.a > 0.5 ? CloudShade(cp, t) : 0;
                    if (shade == 1) c.rgb = _CloudShadeColor.rgb;
                    else if (shade == 2) c.rgb = _CloudColor.rgb;
                    else if (shade == 3) c.rgb = _CloudLight.rgb;
                    else if (shade == 4) c.rgb = lerp(_CloudShadeColor.rgb, _CloudDeep.rgb, _CloudDeep.a);
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
