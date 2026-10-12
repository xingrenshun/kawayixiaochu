Shader "UI/HandDrawLineArt"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _LineWidth ("Line Width", Range(0, 0.01)) = 0.002
        _Threshold ("Threshold", Range(0, 1)) = 0.2

        _BackgroundColor ("Background Color", Color) = (1,1,1,1)
        _LineColor ("Line Color", Color) = (0,0,0,1)

        _NoiseScale ("Noise Scale", Range(1, 20)) = 8
        _NoiseStrength ("Noise Strength", Range(0, 0.02)) = 0.01
        _LineJitter ("Line Jitter", Range(0, 1)) = 0.3

        // UI Stencil
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _WriteMask ("Stencil Write Mask", Float) = 255
        _ReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

    }

    SubShader
    {
        Tags { 
            "Queue"="Transparent" 
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_ReadMask]
            WriteMask [_WriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

            fixed4 _Color;
            float _LineWidth;
            float _Threshold;

            fixed4 _BackgroundColor;
            fixed4 _LineColor;

            float _NoiseScale;
            float _NoiseStrength;
            float _LineJitter;

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float2 uv       : TEXCOORD0;
                float4 color    : COLOR;
            };

            // ----- Simple hash noise -----
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));

                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            v2f vert(appdata_t v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;

                return o;
            }

            float luminance(float3 c)
            {
                return dot(c, float3(0.299, 0.587, 0.114));
            }

            float sobel(float2 uv)
            {
                float2 offset = _MainTex_TexelSize.xy * (_LineWidth * 1000);

                float tl = luminance(tex2D(_MainTex, uv + float2(-offset.x, offset.y)).rgb);
                float t  = luminance(tex2D(_MainTex, uv + float2(0, offset.y)).rgb);
                float tr = luminance(tex2D(_MainTex, uv + float2(offset.x, offset.y)).rgb);

                float l  = luminance(tex2D(_MainTex, uv + float2(-offset.x, 0)).rgb);
                float r  = luminance(tex2D(_MainTex, uv + float2(offset.x, 0)).rgb);

                float bl = luminance(tex2D(_MainTex, uv + float2(-offset.x, -offset.y)).rgb);
                float b  = luminance(tex2D(_MainTex, uv + float2(0, -offset.y)).rgb);
                float br = luminance(tex2D(_MainTex, uv + float2(offset.x, -offset.y)).rgb);

                float gx = -tl + tr - 2*l + 2*r - bl + br;
                float gy = -tl - 2*t - tr + bl + 2*b + br;

                return sqrt(gx*gx + gy*gy);
            }


            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // --- UV 手绘风抖动 ---
                float n = noise(uv * _NoiseScale);
                uv += (n - 0.5) * _NoiseStrength;

                fixed4 col = tex2D(_MainTex, uv);

                if (col.a < 0.1) discard;

                float e = sobel(uv);

                // --- 线条手绘抖动 ---
                float jitter = noise(uv * (_NoiseScale * 1.5)) * _LineJitter;
                e *= (1.0 + jitter);

                float t = smoothstep(_Threshold, _Threshold + 0.2, e);

                fixed4 finalColor = lerp(_BackgroundColor, _LineColor, t);

                finalColor.a = col.a * i.color.a;
                return finalColor;
            }
            ENDCG
        }


    }
}
