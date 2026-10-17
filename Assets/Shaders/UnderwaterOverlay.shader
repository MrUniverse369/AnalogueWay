// WATER: full-screen underwater look (URP, 2D Renderer). Drawn on a white sprite stretched over the camera
// view by PlayerWater: a blue tint, shimmering caustic light anchored to the world, soft light shafts from
// above and darker edges. The SpriteRenderer colour drives it: alpha fades the whole effect in and out,
// rgb tints it (PlayerWater pulses it red while Harvey is out of breath).
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
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.color = v.color;
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
                return half4(rgb, saturate(a) * i.color.a);
            }
            ENDHLSL
        }
    }
}
