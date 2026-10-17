// WATER: water body for sprites (URP, 2D Renderer). The top edge rolls in two layered sine waves with a
// foam line along the crest; below it the colour fades from the surface colour to the deep colour, slow
// light streaks drift through and caustic light shimmers (the underwater look lives here, in the water). Everything is snapped to the game's pixel grid (16 pixels per unit).
// Put it on a SpriteRenderer whose sprite fills its whole texture (a plain box or square); stretch the
// object with its scale. Keep the SpriteRenderer colour white and set the colours on the material.
Shader "AnalogueWay/WaterSurface"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _SurfaceColor ("Surface colour", Color) = (0.25, 0.72, 1.0, 0.55)
        _DeepColor ("Deep colour", Color) = (0.05, 0.32, 0.72, 0.7)
        _FoamColor ("Foam colour", Color) = (0.86, 1.0, 1.0, 0.95)
        _HighlightColor ("Light streak colour", Color) = (0.7, 1.0, 1.0, 0.35)
        _WaveHeight ("Wave height (units)", Float) = 0.25
        _WaveLength ("Wave length (units)", Float) = 4
        _WaveSpeed ("Wave speed", Float) = 1.4
        _FoamThickness ("Foam thickness (units)", Float) = 0.125
        _FadeDepth ("Surface-to-deep fade depth (units)", Float) = 3
        _StreakAmount ("Light streaks", Range(0, 1)) = 0.5
        _CausticColor ("Caustic light", Color) = (0.7, 1.0, 1.0, 0.3)
        _CausticScale ("Caustic size (per unit)", Float) = 0.9
        _CausticSpeed ("Caustic speed", Float) = 0.8
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
                half4 _FoamColor;
                half4 _HighlightColor;
                float _WaveHeight;
                float _WaveLength;
                float _WaveSpeed;
                float _FoamThickness;
                float _FadeDepth;
                float _StreakAmount;
                half4 _CausticColor;
                float _CausticScale;
                float _CausticSpeed;
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

            // height of the wave crest above its lowest point, at world x (0 .. 2 * _WaveHeight)
            float Wave(float x, float t)
            {
                float k = 6.2831853 / max(_WaveLength, 0.01);
                float w = 0.65 * sin(k * x + t * _WaveSpeed) + 0.35 * sin(k * 1.73 * x - t * _WaveSpeed * 1.31 + 1.7);
                return _WaveHeight * (1.0 + w);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float px = 1.0 / max(_PixelsPerUnit, 1.0);
                float t = _Time.y;

                // world units from the sprite's top edge. The sprite fills its texture (uv.y = 1 at the top),
                // and the world height per uv comes from screen-space derivatives (works with sprite batching).
                float worldPerUv = abs(ddy(i.positionWS.y)) / max(abs(ddy(i.uv.y)), 1e-6);
                float depth = (1.0 - i.uv.y) * worldPerUv;

                // snap to the pixel grid so the waves stay crisp pixel art
                float x = floor(i.positionWS.x / px) * px;
                depth = floor(depth / px) * px;

                float surface = 2.0 * _WaveHeight - Wave(x, t);   // depth of the water line here
                float below = depth - surface;
                clip(below);                                        // above the wave: transparent

                half4 col;
                if (below < _FoamThickness)
                {
                    col = _FoamColor;
                }
                else
                {
                    col = lerp(_SurfaceColor, _DeepColor, saturate((below - _FoamThickness) / max(_FadeDepth, 0.01)));
                    // slow drifting light streaks, strongest near the surface
                    float yw = floor(i.positionWS.y / px) * px;
                    float s = sin(x * 1.3 + sin(yw * 0.9 + t * 0.7) * 1.8 + t * 0.6);
                    float streak = step(0.93, s) * _StreakAmount * saturate(1.0 - below / (_FadeDepth * 2.0));
                    col.rgb = lerp(col.rgb, _HighlightColor.rgb, streak * _HighlightColor.a * 2.0);

                    // caustics: where moving wave fronts cancel out, light gathers into bright threads
                    float2 cp = float2(x, yw) * _CausticScale;
                    float ct = t * _CausticSpeed;
                    float c = sin(cp.x * 2.1 + ct) + sin(cp.y * 1.7 - ct * 0.8) + sin((cp.x + cp.y) * 1.3 + ct * 1.2) + sin((cp.x - cp.y) * 1.9 - ct * 0.6);
                    float caustic = pow(saturate(1.0 - abs(c) * 0.55), 6.0) * _CausticColor.a;
                    col.rgb = lerp(col.rgb, _CausticColor.rgb, caustic);
                    col.a = saturate(col.a + caustic * 0.5);
                }
                col.a *= i.color.a;
                return col;
            }
            ENDHLSL
        }
    }
}
