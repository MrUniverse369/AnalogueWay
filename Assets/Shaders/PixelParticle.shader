// FX: particles for PixelFX. Every quad corner is snapped to the 16 pixels-per-unit art grid, so a particle is
// always a crisp block of whole art pixels (never a soft or blurry dot). Optional shape texture (stars, feathers,
// leaves, petals) drawn point-sampled. _SrcBlend/_DstBlend: alpha (SrcAlpha, OneMinusSrcAlpha) or glow (SrcAlpha, One).
Shader "AnalogueWay/PixelParticle"
{
    Properties
    {
        _MainTex ("Shape", 2D) = "white" {}
        _PixelsPerUnit ("Art pixels per unit", Float) = 16
        [HideInInspector] _SrcBlend ("Src", Float) = 5
        [HideInInspector] _DstBlend ("Dst", Float) = 10
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend [_SrcBlend] [_DstBlend]
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
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_point_clamp);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _PixelsPerUnit;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                ws.xy = round(ws.xy * _PixelsPerUnit) / _PixelsPerUnit;
                o.positionCS = TransformWorldToHClip(ws);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_point_clamp, i.uv) * i.color;
                clip(c.a - 0.02);
                return c;
            }
            ENDHLSL
        }
    }
}
