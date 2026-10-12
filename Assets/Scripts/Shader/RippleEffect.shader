Shader "UI/RippleCircle"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Ripple Settings)]
        _RippleCenter ("Center (UV)", Vector) = (0.5, 0.5, 0, 0)
        _RippleColor ("Ripple Color", Color) = (1, 1, 1, 1)
        _Frequency ("Frequency", Float) = 20
        _Speed ("Speed", Float) = 3
        _Amplitude ("Amplitude", Range(0, 1)) = 0.5
        
        _AspectRatio ("Aspect Ratio", Float) = 1.0

        // UGUI 标准属性
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
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

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
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            
            // 变量声明
            float2 _RippleCenter;
            fixed4 _RippleColor;
            float _Frequency;
            float _Speed;
            float _Amplitude;
            float _AspectRatio; // 接收 C# 传来的长宽比

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // 计算当前像素相对于中心点的向量
                float2 dv = IN.texcoord - _RippleCenter;
                
                // 根据长宽比修正 X 轴的长度
                // 如果图片宽是高的2倍 (_AspectRatio = 2)，那么 X 轴的 UV 差值应该放大2倍才能代表真实的物理距离
                dv.x *= _AspectRatio;

                // 计算修正后的真实距离
                float dist = length(dv);
                // ------------------

                // 波纹计算
                float time_val = _Time.y * _Speed;
                float wave = sin(dist * _Frequency - time_val);
                
                // 制作一个锐利的波纹环
                float ripple_mask = smoothstep(0.4, 0.6, wave);
                // 随距离衰减，让边缘自然一点
                ripple_mask *= saturate(1.0 - dist);

                // 颜色叠加
                float3 final_color = color.rgb + _RippleColor.rgb * ripple_mask * _Amplitude;
                color.rgb = final_color;

                // UGUI 裁剪支持
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
