// WATER: draws a sprite as a solid glowing silhouette (additive). WaterBreathingVisual places four of these,
// each nudged one pixel out, behind Harvey to give him a pulsing aqua outline while the Water Breathing
// talisman is active. The SpriteRenderer colour sets the glow colour and strength.
// FX: also used by SpriteAura (talisman auras, the stomp combo glow, Chi Stone and item glows). The renderer
// colour is read through unity_SpriteColor as well as the vertex colour: in Unity 6 a SpriteRenderer's colour
// isn't always in the vertex colour, which drew every glow white.
// COMBO: _DstBlend picks the blend: One (the default, additive glow) or OneMinusSrcAlpha (a solid coloured outline that
// still reads over bright skies, where an additive glow washes out). Resources/FX/SpriteGlowSolid.mat is the solid one.
Shader "AnalogueWay/SpriteGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend (One = glow, OneMinusSrcAlpha = solid)", Float) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Blend SrcAlpha [_DstBlend]
        Cull Off
        ZWrite Off

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                SetUpSpriteInstanceProperties();
                o.positionCS = TransformObjectToHClip(UnityFlipSprite(v.positionOS.xyz, unity_SpriteProps.xy));
                o.uv = v.uv;
                o.color = v.color * unity_SpriteColor;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                return half4(i.color.rgb, step(0.5, a) * i.color.a);
            }
            ENDHLSL
        }
    }
}
