// FX: the Tomb's darkness (URP, 2D Renderer). Drawn on a white sprite stretched over the camera view by
// TombDarkness.cs: the view is shaded toward the HNL dark purple, except inside light circles around Harvey and
// any torches or checkpoints. Circle edges are ordered-dither in whole art pixels, so the light reads as pixel
// art rather than a smooth gradient. Up to 8 lights: _Lights[i] = (world x, world y, radius, strength).
Shader "AnalogueWay/TombDarkness"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _DarkColor ("Darkness (a = strength)", Color) = (0.1, 0.08, 0.14, 0.55)
        _EdgeWidth ("Soft edge (units)", Float) = 1.2
        _PixelsPerUnit ("Art pixels per unit", Float) = 16
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _DarkColor;
                float _EdgeWidth;
                float _PixelsPerUnit;
            CBUFFER_END

            float4 _Lights[8];
            int _LightCount;

            float Bayer4(float2 px)
            {
                uint2 p = (uint2)(fmod(fmod(px, 4.0) + 4.0, 4.0));
                static const float m[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
                return (m[p.y * 4 + p.x] + 0.5) / 16.0;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 px = floor(i.positionWS.xy * _PixelsPerUnit);
                float2 p = (px + 0.5) / _PixelsPerUnit;
                float lit = 0;
                for (int k = 0; k < _LightCount; k++)
                {
                    float4 L = _Lights[k];
                    float d = length(p - L.xy);
                    lit = max(lit, saturate((L.z - d) / max(_EdgeWidth, 0.01)) * L.w);
                }
                // dithered steps instead of a smooth fade: 0, 1/3, 2/3 or full light
                float steps = floor(lit * 3.0 + Bayer4(px)) / 3.0;
                float a = _DarkColor.a * (1.0 - saturate(steps));
                return half4(_DarkColor.rgb, a);
            }
            ENDHLSL
        }
    }
}
