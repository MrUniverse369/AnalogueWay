// WATER: full-screen underwater look (URP, 2D Renderer). Drawn on a white sprite stretched over the camera
// view by PlayerWater: a blue tint, shimmering caustic light anchored to the world, soft light shafts from
// above and darker edges. The SpriteRenderer colour drives it: alpha fades the whole effect in and out,
// rgb tints it.
// FX: the air warning lives here too. As breath runs low (_Air from 1 to 0) a dark vignette closes in from the
// screen edges with a dithered pixel edge, and while Harvey is drowning (_Drowning 1) its rim pulses red.
// _TintStrength scales the tint/caustics/shafts so PlayerWater can show only the air vignette when its full
// screen tint is switched off. The renderer colour is also read through unity_SpriteColor (Unity 6).
Shader "AnalogueWay/UnderwaterOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _TintColor ("Water tint", Color) = (0.08, 0.38, 0.72, 0.32)
        _CausticColor ("Caustic light", Color) = (0.62, 1.0, 1.0, 0.22)
        _CausticScale ("Caustic size (per unit)", Float) = 0.9
        _CausticSpeed ("Caustic speed", Float) = 0.8
        _ShaftStrength ("Light shafts", Range(0, 1)) = 0.35
        _Vignette ("Dark edges", Range(0, 1)) = 0.55
        _PixelsPerUnit ("Pixel snap (pixels per unit)", Float) = 16
        _TintStrength ("Tint strength (runtime)", Range(0, 1)) = 1
        _Air ("Air left (runtime)", Range(0, 1)) = 1
        _Drowning ("Drowning (runtime)", Range(0, 1)) = 0
        _AirColor ("Air vignette", Color) = (0.05, 0.07, 0.16, 0.9)
        _PulseColor ("Out of breath pulse", Color) = (1, 0.35, 0.35, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

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
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _TintColor;
                half4 _CausticColor;
                float _CausticScale;
                float _CausticSpeed;
                float _ShaftStrength;
                float _Vignette;
                float _PixelsPerUnit;
                float _TintStrength;
                float _Air;
                float _Drowning;
                half4 _AirColor;
                half4 _PulseColor;
            CBUFFER_END

            float Bayer4(float2 px)
            {
                uint2 q = (uint2)(fmod(fmod(px, 4.0) + 4.0, 4.0));
                static const float m[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
                return (m[q.y * 4 + q.x] + 0.5) / 16.0;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.screenPos = ComputeScreenPos(o.positionCS);
                SetUpSpriteInstanceProperties();
                o.color = v.color * unity_SpriteColor;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float px = 1.0 / max(_PixelsPerUnit, 1.0);
                float t = _Time.y * _CausticSpeed;
                float2 p = floor(i.positionWS.xy / px) * px * _CausticScale;

                // caustics: where three moving wave fronts cancel out, light gathers into bright threads
                float c = sin(p.x * 2.1 + t) + sin(p.y * 1.7 - t * 0.8) + sin((p.x + p.y) * 1.3 + t * 1.2) + sin((p.x - p.y) * 1.9 - t * 0.6);
                float caustic = pow(saturate(1.0 - abs(c) * 0.55), 6.0);

                float2 uv = i.screenPos.xy / max(i.screenPos.w, 1e-5);
                // light shafts slanting down from the top of the screen
                float shaft = saturate(sin(uv.x * 9.0 + uv.y * 3.0 + _Time.y * 0.25) * 0.5 + 0.5);
                shaft = pow(shaft, 6.0) * uv.y * _ShaftStrength;
                // darker towards the edges
                float edge = smoothstep(0.3, 0.75, length((uv - 0.5) * float2(1.2, 1.0))) * _Vignette;

                half3 rgb = _TintColor.rgb;
                rgb = lerp(rgb, _CausticColor.rgb, saturate(caustic * _CausticColor.a * 3.0 + shaft));
                rgb *= (1.0 - edge * 0.6);
                float a = _TintColor.a + caustic * _CausticColor.a + shaft * 0.25 + edge * 0.35;

                rgb *= i.color.rgb;
                a = saturate(a) * _TintStrength;

                // FX: air vignette, closing in from 60% breath down to empty (dithered edge in art pixels)
                float closing = saturate((0.6 - _Air) / 0.6);
                float2 pxs = floor(i.positionWS.xy * _PixelsPerUnit);
                float r = length((uv - 0.5) * float2(1.6, 1.0));
                float radius = lerp(1.1, 0.42, closing);
                float vig = saturate((r - radius) / 0.12 + (Bayer4(pxs) - 0.5) * 0.35);
                vig = step(0.5, vig) * step(0.001, closing);
                float rim = vig * (1.0 - saturate((r - radius) / 0.25));
                float pulse = _Drowning * (0.5 + 0.5 * sin(_Time.y * 8.0));
                half3 vigRgb = lerp(_AirColor.rgb, _PulseColor.rgb, rim * pulse);
                float vigA = _AirColor.a * vig;
                rgb = lerp(rgb, vigRgb, vigA);
                a = max(a, vigA);
                return half4(rgb, a * i.color.a);
            }
            ENDHLSL
        }
    }
}
