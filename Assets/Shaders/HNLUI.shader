// FX: the shared UI shader (uGUI Images), a drop-in for UI/Default with two extras:
//   _Grey 0..1        greyscale (unaffordable shop items)
//   _HNL_IRIS         the cat-head iris wipe for scene transitions: the Image covers the screen except inside a
//                     cat-head shaped hole (Resources/FX/Shapes/CatHead) centred on _IrisCenter (viewport 0..1),
//                     _IrisSize = hole height as a fraction of the screen height (0 = fully covered). The edge is
//                     snapped to a 320x180 art-pixel grid so it reads as pixel art.
Shader "AnalogueWay/HNLUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Grey ("Greyscale", Range(0, 1)) = 0
        [Toggle(_HNL_IRIS)] _UseIris ("Iris wipe", Float) = 0
        _IrisMask ("Iris mask", 2D) = "white" {}
        _IrisCenter ("Iris centre (viewport)", Vector) = (0.5, 0.5, 0, 0)
        _IrisSize ("Iris size (screen heights)", Float) = 3

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma shader_feature_local _HNL_IRIS

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _IrisMask;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _Grey;
            float4 _IrisCenter;
            float _IrisSize;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half4 c = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;
                half lum = dot(c.rgb, half3(0.299, 0.587, 0.114));
                c.rgb = lerp(c.rgb, lum.xxx, _Grey);

            #if defined(_HNL_IRIS)
                {
                    float2 uv = i.screenPos.xy / max(i.screenPos.w, 1e-5);
                    float aspect = _ScreenParams.x / _ScreenParams.y;
                    // snap to a 180-row art-pixel grid
                    float2 grid = float2(180.0 * aspect, 180.0);
                    uv = (floor(uv * grid) + 0.5) / grid;
                    float2 d = (uv - _IrisCenter.xy) * float2(aspect, 1.0);
                    float2 m = d / max(_IrisSize, 1e-4) + 0.5;
                    float inside = 0;
                    if (all(m >= 0) && all(m <= 1)) inside = step(0.5, tex2D(_IrisMask, m).a);
                    c.a *= 1.0 - inside;
                }
            #endif

                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a - 0.001);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
