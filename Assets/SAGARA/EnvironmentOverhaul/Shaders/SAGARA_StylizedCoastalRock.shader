Shader "SAGARA/Environment/StylizedCoastalRock"
{
    Properties
    {
        [MainTexture] _RockMap ("Limestone Albedo (Triplanar)", 2D) = "white" {}
        [Normal] _RockNormal ("Limestone Normal (Triplanar)", 2D) = "bump" {}
        [MainColor] _RockColor ("Warm Limestone Tint", Color) = (0.86, 0.82, 0.74, 1)
        _CreviceColor ("Warm Shadow Crevice Tint", Color) = (0.56, 0.52, 0.46, 1)
        _MossColor ("Coastal Moss Cap Color", Color) = (0.40, 0.62, 0.24, 1)
        _MossAmount ("Moss Coverage", Range(0, 1)) = 0.45
        _MossThreshold ("Moss Slope Threshold", Range(0.2, 0.95)) = 0.62
        _TriplanarScale ("Triplanar World Scale", Range(0.05, 1.5)) = 0.28
        _NormalStrength ("Normal Strength", Range(0, 1.5)) = 0.65
        _Smoothness ("Matte Smoothness", Range(0, 0.5)) = 0.14
        _StrataStrength ("Coastal Strata Accent", Range(0, 0.5)) = 0.18
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_RockMap);
            SAMPLER(sampler_RockMap);
            TEXTURE2D(_RockNormal);
            SAMPLER(sampler_RockNormal);

            CBUFFER_START(UnityPerMaterial)
                half4 _RockColor;
                half4 _CreviceColor;
                half4 _MossColor;
                half _MossAmount;
                half _MossThreshold;
                float _TriplanarScale;
                half _NormalStrength;
                half _Smoothness;
                half _StrataStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogCoord : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = frac(sin(dot(i, float2(127.1, 311.7))) * 43758.5453);
                float b = frac(sin(dot(i + float2(1, 0), float2(127.1, 311.7))) * 43758.5453);
                float c = frac(sin(dot(i + float2(0, 1), float2(127.1, 311.7))) * 43758.5453);
                float d = frac(sin(dot(i + float2(1, 1), float2(127.1, 311.7))) * 43758.5453);
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.fogCoord = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 nWS = normalize(IN.normalWS);
                float3 blend = pow(abs(nWS), 4.0);
                blend /= max(0.0001, blend.x + blend.y + blend.z);

                float3 p = IN.positionWS * _TriplanarScale;
                half3 colX = SAMPLE_TEXTURE2D(_RockMap, sampler_RockMap, p.zy).rgb;
                half3 colY = SAMPLE_TEXTURE2D(_RockMap, sampler_RockMap, p.xz).rgb;
                half3 colZ = SAMPLE_TEXTURE2D(_RockMap, sampler_RockMap, p.xy).rgb;
                half3 triCol = colX * blend.x + colY * blend.y + colZ * blend.z;

                // World-space triplanar normal perturbation
                float3 nx = UnpackNormalScale(SAMPLE_TEXTURE2D(_RockNormal, sampler_RockNormal, p.zy), _NormalStrength);
                float3 ny = UnpackNormalScale(SAMPLE_TEXTURE2D(_RockNormal, sampler_RockNormal, p.xz), _NormalStrength);
                float3 nz = UnpackNormalScale(SAMPLE_TEXTURE2D(_RockNormal, sampler_RockNormal, p.xy), _NormalStrength);
                float3 nWorldX = float3(nWS.x, nx.y + nWS.y, nx.x + nWS.z);
                float3 nWorldY = float3(ny.x + nWS.x, nWS.y, ny.y + nWS.z);
                float3 nWorldZ = float3(nz.x + nWS.x, nz.y + nWS.y, nWS.z);
                float3 finalN = normalize(nWorldX * blend.x + nWorldY * blend.y + nWorldZ * blend.z);

                // Subtle horizontal coastal limestone strata
                float strataN = ValueNoise(float2(IN.positionWS.x * 0.25 + IN.positionWS.z * 0.25, IN.positionWS.y * 1.45));
                float strata = sin(IN.positionWS.y * 2.6 + strataN * 2.4) * 0.5 + 0.5;
                half3 stoneBase = lerp(_CreviceColor.rgb, _RockColor.rgb, saturate(dot(triCol, half3(0.33, 0.34, 0.33)) * 1.15));
                stoneBase *= lerp(1.0 - _StrataStrength, 1.0 + _StrataStrength * 0.55, strata);

                // Natural upward-facing mossy cap
                float mossNoise = ValueNoise(IN.positionWS.xz * 0.55) * 0.35 + ValueNoise(IN.positionWS.xz * 1.7) * 0.15;
                float mossMask = smoothstep(_MossThreshold - 0.14, _MossThreshold + 0.16, finalN.y + mossNoise - 0.18) * _MossAmount;
                half3 mossShaded = _MossColor.rgb * lerp(0.86, 1.12, mossNoise);
                half3 albedo = lerp(stoneBase, mossShaded, mossMask);

                // Stylized soft URP lighting
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float ndl = saturate(dot(finalN, mainLight.direction) * 0.85 + 0.15);
                float shadow = lerp(0.55, 1.0, mainLight.shadowAttenuation);

                // Subtle matte specular only on bare rock
                float3 viewDir = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float3 halfVec = normalize(mainLight.direction + viewDir);
                float spec = pow(saturate(dot(finalN, halfVec)), 18.0) * _Smoothness * (1.0 - mossMask) * mainLight.shadowAttenuation;

                half3 ambient = SampleSH(finalN) * 0.88;
                half3 finalCol = albedo * (mainLight.color * ndl * shadow + ambient) + mainLight.color * spec * 0.28;
                finalCol = MixFog(finalCol, IN.fogCoord);
                return half4(finalCol, 1.0);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
