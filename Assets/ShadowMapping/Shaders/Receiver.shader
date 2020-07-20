Shader "XXL/Receiver"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma fragmentoption ARB_precision_hint_fastest  

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 shadowCoord : TEXCOORD0;
                float4 pos : SV_POSITION;
            };

            uniform float4x4 _gWorldToShadow;
            uniform sampler2D _gShadowMapTexture;
            uniform float4 _gShadowMapTexture_TexelSize;
            uniform float _gShadowStrength;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float4 worldPos=mul(unity_ObjectToWorld,v.vertex);
                o.shadowCoord=mul(_gWorldToShadow,worldPos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                //shadow 
                i.shadowCoord.xy=i.shadowCoord.xy/i.shadowCoord.w;
                float2 uv=i.shadowCoord.xy;
                uv=uv*0.5 + 0.5; //(-1,1) → (0,1)

                float depth = i.shadowCoord.z / i.shadowCoord.w; //当前片段在光源空间的深度
                #if defined(SHADER_TARGET_GLSL)
                    depth = depth*0.5 + 0.5;    //(-1,1) → (0,1)
                #elif defined(UNITY_REVERSED_Z)
                    depth = 1 - depth;      //(1,0) → (0,1)
                #endif

                //sample depth Texture;
                float4 col=tex2D(_gShadowMapTexture,uv);
                float sampleDepth=DecodeFloatRGBA(col);
                float shadow=sampleDepth<depth?_gShadowStrength : 1 ;
                return shadow;
            }
            ENDCG
        }
    }
}
