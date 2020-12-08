﻿Shader "ShadowMap/Custom/Depth" {

    SubShader {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            CGPROGRAM
            #include "UnityCG.cginc"
            
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ DRAW_TRANSPARENT_SHADOWS

            sampler2D _MainTex;
            float4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // TODO: Understand why depth is reversed
                float depth = 1 - i.vertex.z;
                return float4(depth, pow(depth, 2), 0, 0);
            }
            ENDCG
        }
    }

    Fallback "VertexLit"
}