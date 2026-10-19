// POISON: the tomb's toxic liquid for sprites (URP, 2D Renderer). Built like WaterSurface (the same sprite setup,
// pixel snapping and depth-from-top maths), but thick and sickly instead of clear:
//  - slow, heavy waves with a glowing rim along the crest and a darker band just under it
//  - a surface-to-deep fade into near-black purple, with slow toxic swirls drifting through it
//  - pixel-art bubbles rising in columns (mostly pale purple, now and then sickly green); each one swells the
//    surface into a little bulge just before it reaches it (PoisonLiquid pops them there with particles)
// Put it on a SpriteRenderer whose sprite fills its whole texture (a plain box); stretch the object with its scale.
// Keep the SpriteRenderer colour white (its alpha still fades the whole thing) and set the colours here.
Shader "AnalogueWay/PoisonSurface"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _SurfaceColor ("Surface colour", Color) = (0.62, 0.22, 0.82, 0.88)
        _DeepColor ("Deep colour", Color) = (0.16, 0.06, 0.24, 0.96)
        _RimColor ("Glowing rim", Color) = (0.89, 0.64, 1.0, 1.0)
        _RimShadowColor ("Band under the rim", Color) = (0.42, 0.13, 0.58, 0.95)
        _SwirlColor ("Toxic swirls", Color) = (0.78, 0.36, 0.95, 0.35)
        _BubbleColor ("Bubbles", Color) = (0.94, 0.78, 1.0, 1.0)
        _ToxicColor ("Green bubbles", Color) = (0.6, 0.9, 0.31, 1.0)
        _WaveHeight ("Wave height (units)", Float) = 0.19
        _WaveLength ("Wave length (units)", Float) = 3.2
        _WaveSpeed ("Wave speed", Float) = 0.8
        _RimThickness ("Rim thickness (units)", Float) = 0.0625
        _FadeDepth ("Surface-to-deep fade depth (units)", Float) = 4
        _BubbleSpacing ("Bubble column spacing (units)", Float) = 0.8
        _BubbleSpeed ("Bubble rise speed (units/s)", Float) = 0.9
        _GreenChance ("Share of green bubbles", Range(0, 1)) = 0.25
        _PixelsPerUnit ("Pixel snap (pixels per unit)", Float) = 16
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _SurfaceColor;
                half4 _DeepColor;
                half4 _RimColor;
                half4 _RimShadowColor;
                half4 _SwirlColor;
                half4 _BubbleColor;
                half4 _ToxicColor;
                float _WaveHeight;
                float _WaveLength;
                float _WaveSpeed;
                float _RimThickness;
                float _FadeDepth;
                float _BubbleSpacing;
                float _BubbleSpeed;
                float _GreenChance;
                float _PixelsPerUnit;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float Hash(float n) { return frac(sin(n * 127.1 + 31.7) * 43758.5453); }

            // slow, heavy waves: height of the crest above its lowest point at world x (0 .. 2 * _WaveHeight)
            float Wave(float x, float t)
            {
                float k = 6.2831853 / max(_WaveLength, 0.01);
                float w = 0.6 * sin(k * x + t * _WaveSpeed) + 0.4 * sin(k * 0.61 * x - t * _WaveSpeed * 0.7 + 2.3);
                return _WaveHeight * (1.0 + w);
            }

            // where the bubble in column 'col' is: x, depth below the top of the sprite, and radius in pixels.
            // life runs 0 -> 1 from the bottom of the liquid to the surface, then the column waits a moment.
            float3 Bubble(float col, float t, float depthMax, out float life, out float green)
            {
                float seed = Hash(col);
                float speed = _BubbleSpeed * (0.6 + 0.8 * Hash(col + 3.1));
                float travel = depthMax + 1.0;                         // a little past the surface, then reset
                float cycle = travel / speed + 0.6 + 1.4 * Hash(col + 7.7);
                float phase = frac(t / cycle + seed);
                float risen = phase * cycle * speed;                   // units risen from the bottom
                life = saturate(risen / travel);
                green = step(1.0 - _GreenChance, Hash(col + 11.3));
                float px = 1.0 / max(_PixelsPerUnit, 1.0);
                float x = (col + 0.25 + 0.5 * Hash(col + 5.9)) * _BubbleSpacing + sin(t * 2.6 + col * 1.7) * px; // a 1-pixel wobble
                float depth = depthMax - risen;
                float radius = 1.0 + step(0.55, Hash(col + 9.4)) + step(0.6, life); // grows a little as it rises
                return float3(x, depth, radius);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float px = 1.0 / max(_PixelsPerUnit, 1.0);
                float t = _Time.y;

                // world units from the sprite's top edge (as in WaterSurface: works with sprite batching)
                float worldPerUv = abs(ddy(i.positionWS.y)) / max(abs(ddy(i.uv.y)), 1e-6);
                float depth = (1.0 - i.uv.y) * worldPerUv;
                float depthMax = worldPerUv;

                float x = floor(i.positionWS.x / px) * px;
                depth = floor(depth / px) * px;
                float yw = floor(i.positionWS.y / px) * px;

                // bubbles near the surface push it up into a bulge
                float col0 = floor(x / _BubbleSpacing);
                float bulge = 0.0;
                float life, green;
                for (int c = -1; c <= 1; c++)
                {
                    float3 b = Bubble(col0 + c, t, depthMax, life, green);
                    float near = saturate(1.0 - abs(b.y - 2.0 * _WaveHeight) / 0.6);     // bubble close to the surface
                    float wide = saturate(1.0 - abs(x - b.x) / (3.0 * px));
                    bulge = max(bulge, near * wide * 2.0 * px);
                }

                float surface = 2.0 * _WaveHeight - Wave(x, t) - bulge; // depth of the liquid line here
                surface = floor(surface / px) * px;
                float below = depth - surface;
                clip(below);                                             // above the liquid: transparent

                half4 col;
                if (below < _RimThickness)
                {
                    col = _RimColor;                                     // the glowing crest
                }
                else if (below < _RimThickness + 2.0 * px)
                {
                    col = _RimShadowColor;                               // darker band just under it
                }
                else
                {
                    col = lerp(_SurfaceColor, _DeepColor, saturate((below - _RimThickness) / max(_FadeDepth, 0.01)));

                    // slow toxic swirls: two drifting sine fields, banded into a few pixel-art steps
                    float s1 = sin(x * 0.9 + t * 0.35 + sin(yw * 0.7 - t * 0.25) * 2.0);
                    float s2 = sin(yw * 1.1 - t * 0.3 + sin(x * 0.5 + t * 0.2) * 2.4);
                    float swirl = step(0.72, s1 * s2 + 0.25) * _SwirlColor.a * saturate(1.0 - below / (_FadeDepth * 2.5));
                    col.rgb = lerp(col.rgb, _SwirlColor.rgb, swirl);

                    // rising bubbles: a pixel ring with a bright pixel at the top left
                    for (int c2 = -1; c2 <= 1; c2++)
                    {
                        float3 b = Bubble(col0 + c2, t, depthMax, life, green);
                        if (b.y < surface) continue;                     // already popped
                        float2 d = float2(x - floor(b.x / px) * px, depth - floor(b.y / px) * px) / px;
                        float dist = length(d);
                        half3 bc = lerp(_BubbleColor.rgb, _ToxicColor.rgb, green);
                        float ring = step(abs(dist - b.z), 0.5);
                        float glint = step(length(d + float2(b.z * 0.5, b.z * 0.5)), 0.5) * step(1.5, b.z);
                        float inside = step(dist, b.z - 0.5);
                        col.rgb = lerp(col.rgb, bc * 0.55 + col.rgb * 0.45, inside * 0.6); // a faint tinted fill
                        col.rgb = lerp(col.rgb, bc, max(ring, glint));
                        col.a = max(col.a, max(ring, glint));
                    }
                }
                col.a *= i.color.a;
                return col;
            }
            ENDHLSL
        }
    }
}
