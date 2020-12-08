Shader "ShadowMap/Custom/Lit" {
    Properties
    {
        _Color ("Main Color", Color) = (1,1,1,1)
        _ZWrite ("ZWrite", Float) = 1

	}
    SubShader {
        Tags { "RenderType" = "Opaque" }

        Pass {
            Lighting On
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite[_ZWrite]
        
            CGPROGRAM
            #include "UnityCG.cginc"
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ HARD_SHADOWS PCF_SHADOWS VARIANCE_SHADOWS MOMENT_SHADOWS
        
            float4 _Color;

            // Shadow Map info
            sampler2D _ShadowTex;
            float4x4 _LightMatrix;
            float4 _ShadowTexScale;

            // Shadow Variables
            float _MaxShadowIntensity;
            int _DrawTransparentGeometry;
            float _VarianceShadowExpansion;

            // Code from
            // https://www.gdcvault.com/play/1023808/Rendering-Antialiased-Shadows-with-Moment
            float ComputeMSMShadowIntensity(float4 b, float FragmentDepth) {
                float L32D22 = mad(-b[0], b[1], b[2]);
                float D22 = mad(-b[0], b[0], b[1]);
                float SquaredDepthVariance = mad(-b[1], b[1], b[3]);
                float D33D22 = dot(float2(SquaredDepthVariance, -L32D22),
                float2(D22, L32D22));
                float InvD22 = 1.0f / D22;
                float L32 = L32D22*InvD22;
                float3 z;
                z[0] = FragmentDepth;
                float3 c = float3(1.0f, z[0], z[0] * z[0]);
                c[1] -= b.x;
                c[2] -= b.y + L32*c[1];
                c[1] *= InvD22;
                c[2] *= D22 / D33D22;
                c[1] -= L32*c[2];
                c[0] -= dot(c.yz, b.xy);
                float InvC2 = 1.0f / c[2];
                float p = c[1] * InvC2;
                float q = c[0] * InvC2;
                float r = sqrt((p*p*0.25f) - q);
                z[1] = -p*0.5f - r;
                z[2] = -p*0.5f + r;
                float4 Switch =
                    (z[2]<z[0]) ? float4(z[1], z[0], 1.0f, 1.0f) : (
                    (z[1]<z[0]) ? float4(z[0], z[1], 0.0f, 1.0f) :
                        float4(0.0f, 0.0f, 0.0f, 0.0f));
                float Quotient = (Switch[0] * z[2] - b[0] * (Switch[0] + z[2]) + b[1])
                    / ((z[2] - Switch[1])*(z[0] - z[1]));
                return saturate(Switch[2] + Switch[3] * Quotient);
            }

            struct v2f
            {
                float4 pos : SV_POSITION;
				float4 wPos : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float depth : TEXCOORD2;
            };
            
            float PCFSample(float depth,float2 uv)
            {
                float shadow=0.0;
                for(int x=-1;x<=1;++x)
                {
                    for(int y=-1;y<=1;++y)
                    {
                        float4 col=tex2D(_ShadowTex,uv+float2(x,y)*_ShadowTexScale.w);
                        float sampleDepth=col.r;
                        shadow+=step(sampleDepth,depth);
                    }
                }
                return shadow/9;
            }

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wPos = mul(unity_ObjectToWorld, v.vertex);
                o.normal = v.normal;

                COMPUTE_EYEDEPTH(o.depth);
                return o; 
            }            

            fixed4 frag (v2f i) : COLOR
			{
                // COLOR
                // modulate with lighting
                float4 color = _Color;

                // SHADOWS
                // get distance to lightPos
                float4 lightSpacePos = mul(_LightMatrix, i.wPos);
                float3 lightSpaceNorm = normalize(mul(_LightMatrix, mul(unity_ObjectToWorld, i.normal)));
                float depth = lightSpacePos.z / _ShadowTexScale.z;

                float2 uv = lightSpacePos.xy;
                uv += _ShadowTexScale.xy / 2;
                uv /= _ShadowTexScale.xy;

                float shadowIntensity = 0;
                float2 offset = lightSpaceNorm * _ShadowTexScale.w;
                float4 samp = tex2D(_ShadowTex, uv +offset);

#if defined( HARD_SHADOWS )
                float sDepth = samp.r;
                shadowIntensity = step(sDepth, depth - _ShadowTexScale.w);
#endif

#ifdef PCF_SHADOWS
                shadowIntensity=PCFSample(depth - _ShadowTexScale.w,uv);
#endif

#ifdef VARIANCE_SHADOWS

                // https://www.gdcvault.com/play/1023808/Rendering-Antialiased-Shadows-with-Moment
                // https://developer.nvidia.com/gpugems/GPUGems3/gpugems3_ch08.html
                // The moments of the fragment live in "_shadowTex"
                float2 s = samp.rg;

                // average / expected depth and depth^2 across the texels
                // E(x) and E(x^2)
                float x = s.r; 
                float x2 = s.g;
                
                // calculate the variance of the texel based on
                // the formula var = E(x^2) - E(x)^2
                // https://en.wikipedia.org/wiki/Algebraic_formula_for_the_variance#Proof
                float var = x2 - x*x; 

                // calculate our initial probability based on the basic depths
                // if our depth is closer than x, then the fragment has a 100%
                // probability of being lit (p=1)
                float p = depth <= x;
                
                // calculate the upper bound of the probability using Chebyshev's inequality
                // https://en.wikipedia.org/wiki/Chebyshev%27s_inequality
                float delta = depth - x;
                float p_max = var / (var + delta*delta);

                // To alleviate the light bleeding, expand the shadows to fill in the gaps
                float amount = _VarianceShadowExpansion;
                p_max = clamp( (p_max - amount) / (1 - amount), 0, 1);

                shadowIntensity = 1 - max(p, p_max);
#endif

#ifdef MOMENT_SHADOWS
                shadowIntensity = ComputeMSMShadowIntensity(samp, depth);
#endif

                color.xyz *= 1 - shadowIntensity * _MaxShadowIntensity;
                color.xyz += UNITY_LIGHTMODEL_AMBIENT.xyz;

                return color;

            }
            ENDCG
        }
    }

    Fallback "VertexLit"
}