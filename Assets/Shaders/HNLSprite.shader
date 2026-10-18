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
//   _HNL_CLOUDS  pixel clouds scrolling inside the sprite (parallax skies)
//   _HNL_HAZE    heat shimmer near a horizon line (Egypt backgrounds)
//   _HNL_PATTERN a slowly scrolling pattern of paw prints and twinkling stars (the title screen backdrop)
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
        _CloudColor ("Cloud", Color) = (0.84, 0.92, 1, 1)
        _CloudShadeColor ("Cloud shade", Color) = (0.69, 0.81, 0.95, 1)
        _CloudScale ("Cloud size (units)", Float) = 3
        _CloudSpeed ("Cloud speed (units/s)", Float) = 0.15
        _CloudCover ("Cloud cover", Range(0, 1)) = 0.62
        _CloudBand ("Cloud band local y (min, max)", Vector) = (0, 100, 0, 0)

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
                float _CloudScale;
                float _CloudSpeed;
                float _CloudCover;
                float4 _CloudBand;
                float _HazeY;
                float _HazeHeight;
                float _HazePixels;
                float _HazeSpeed;
                half4 _PatternColor;
                float4 _PatternCell;
                float4 _PatternSpeed;
                float _PatternMinY;
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

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                SetUpSpriteInstanceProperties();
                float3 pos = UnityFlipSprite(v.positionOS, unity_SpriteProps.xy);
            #if defined(_HNL_FLUFF)
                // springy squash about the sprite's pivot: wider and flatter when pressed, taller on the rebound
                pos.x *= 1.0 + _Squash;
                pos.y *= 1.0 - _Squash;
            #endif
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
                    float2 cp = floor(i.positionOS.xy * _PixelsPerUnit) / _PixelsPerUnit; // snapped to art pixels
                    float2 q = float2(cp.x + t * _CloudSpeed, cp.y * 1.8) / max(_CloudScale, 0.01);
                    float n = ValueNoise(q) * 0.65 + ValueNoise(q * 2.1 + 7.3) * 0.35;
                    float band = step(_CloudBand.x, cp.y) * step(cp.y, _CloudBand.y);
                    float edge = saturate((cp.y - _CloudBand.x) * 2.0) * saturate((_CloudBand.y - cp.y) * 2.0);
                    float cover = _CloudCover + (1.0 - edge) * 0.3;
                    if (band > 0 && n > cover && tex.a > 0.5)
                    {
                        float under = ValueNoise(q + float2(0, 0.18)) * 0.65 + ValueNoise((q + float2(0, 0.18)) * 2.1 + 7.3) * 0.35;
                        c.rgb = under > cover ? _CloudColor.rgb : _CloudShadeColor.rgb;
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

                // dither: ordered for flickers, random pixels for dissolves
                float threshold = lerp(Bayer4(artPx + float2(_DitherShift, _DitherShift * 2.0)), Hash21(artPx + 3.7), _DitherNoise);
                clip(threshold - _Dither);

                return c;
            }
            ENDHLSL
        }
    }
}
