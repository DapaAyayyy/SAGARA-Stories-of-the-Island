Shader "SAGARA/Environment/StylizedFoliage"
{
    Properties
    {
        [MainTexture] _BaseMap ("Foliage Albedo (RGBA)", 2D) = "white" {}
        [MainColor] _TopColor ("Sunlit Highlight Tint", Color) = (0.58, 0.94, 0.56, 1)
        _BottomColor ("Cool Emerald Shadow Tint", Color) = (0.11, 0.42, 0.36, 1)
        _RimSunColor ("Warm Sun Rim", Color) = (0.86, 0.98, 0.60, 1)
        _TexColorBlend ("Preserve Painted Texture Hue", Range(0, 1)) = 0.55
        _GradientMinY ("Gradient Local Min Y", Float) = 0.5
        _GradientMaxY ("Gradient Local Max Y", Float) = 6.5
        _WorldVariation ("World Hue Variation", Range(0, 0.35)) = 0.14
        _WrapLighting ("Plush Soft Wrap", Range(0, 1)) = 0.68
        _RimStrength ("Sun Subsurface Strength", Range(0, 1)) = 0.24
        _WindSpeed ("Wind Speed", Range(0, 4)) = 1.25
        _WindStrength ("Wind Amplitude", Range(0, 0.5)) = 0.08
        _WindScale ("Wind Wave Scale", Range(0.05, 2.0)) = 0.26
        _WindStartY ("Wind Base Lock Y", Float) = 0.4
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clipping", Float) = 1
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.38
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Cull [_Cull]

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _TopColor;
                half4 _BottomColor;
                half4 _RimSunColor;
                half _TexColorBlend;
                float _GradientMinY;
                float _GradientMaxY;
                half _WorldVariation;
                half _WrapLighting;
                half _RimStrength;
                float _WindSpeed;
                float _WindStrength;
                float _WindScale;
                float _WindStartY;
                half _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float heightT : TEXCOORD3;
                float aoWeight : TEXCOORD4;
                float fogCoord : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 posOS = IN.positionOS.xyz;
                float heightRange = max(0.05, _GradientMaxY - _GradientMinY);
                float hT = saturate((posOS.y - _GradientMinY) / heightRange);

                float3 posWS = TransformObjectToWorld(posOS);

                // Smooth coastal breeze
                float windMask = saturate((posOS.y - _WindStartY) / max(0.25, heightRange));
                windMask = windMask * windMask;
                // Vertex color R = wind weight (default 1.0), G = baked canopy AO (default 1.0)
                float vWind = lerp(1.0, IN.color.r, step(0.01, abs(IN.color.r - 1.0)));
                float phase = _Time.y * _WindSpeed + (posWS.x * 0.65 + posWS.z * 0.85) * _WindScale;
                float gust = sin(phase) * 0.68 + sin(phase * 1.85 + 1.2) * 0.32;
                float flutter = cos(phase * 2.5 + posOS.x * 2.0 + posOS.z * 1.7) * 0.22;
                posWS.x += (gust + flutter) * _WindStrength * windMask * vWind;
                posWS.z += (gust * 0.55 - flutter * 0.35) * _WindStrength * windMask * vWind;

                OUT.positionWS = posWS;
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.heightT = hT;
                OUT.aoWeight = lerp(0.62, 1.0, IN.color.g);
                OUT.fogCoord = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 texCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                #if defined(_ALPHATEST_ON)
                    clip(texCol.a - _Cutoff);
                #endif

                // Keep baked spherical dome normals intact (do NOT flip by VFACE so cards shade as one plush cloud!)
                float3 nWS = normalize(IN.normalWS);

                // Gentle world-space variation between groves
                float2 cell = floor(IN.positionWS.xz * 0.12);
                float varN = lerp(
                    lerp(Hash21(cell), Hash21(cell + float2(1, 0)), frac(IN.positionWS.x * 0.12)),
                    lerp(Hash21(cell + float2(0, 1)), Hash21(cell + float2(1, 1)), frac(IN.positionWS.x * 0.12)),
                    frac(IN.positionWS.z * 0.12)
                );

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                // Plush spherical wrap lighting + soft shadow ramp (Story of Seasons FoMT look)
                float ndl = dot(nWS, mainLight.direction);
                float wrap = saturate((ndl + _WrapLighting) / (1.0 + _WrapLighting));
                float sunTerm = wrap * lerp(0.62, 1.0, mainLight.shadowAttenuation);

                // Rich teal-emerald shadow to vibrant sunlit mint/emerald highlight
                half gradT = saturate(IN.heightT * 0.45 + sunTerm * 0.65) * IN.aoWeight;
                half3 rampCol = lerp(_BottomColor.rgb, _TopColor.rgb, smoothstep(0.08, 0.94, gradT));

                // Preserve painterly leaf-rosette shading detail from texture
                half texLum = saturate(dot(texCol.rgb, half3(0.30, 0.59, 0.11)));
                half3 monoDetail = rampCol * lerp(0.76, 1.18, texLum);
                half3 paintedDetail = texCol.rgb * lerp(_BottomColor.rgb * 1.45, _TopColor.rgb * 1.25, smoothstep(0.10, 0.92, gradT));
                half3 baseAlbedo = lerp(monoDetail, paintedDetail, _TexColorBlend);

                half3 groveShift = lerp(half3(0.92, 1.02, 1.04), half3(1.06, 1.02, 0.92), varN);
                half3 albedo = baseAlbedo * lerp(half3(1, 1, 1), groveShift, _WorldVariation);

                // Soft sunlit crown glow
                float3 viewDir = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float rim = pow(1.0 - saturate(dot(nWS, viewDir)), 2.0) * saturate(ndl * 0.5 + 0.5);
                half3 sss = _RimSunColor.rgb * (rim * _RimStrength * mainLight.shadowAttenuation);

                half3 ambient = SampleSH(nWS) * 0.58 + half3(0.18, 0.24, 0.22);
                half3 finalCol = albedo * (mainLight.color * (0.48 + 0.58 * sunTerm) + ambient * 0.42) + sss * albedo;
                finalCol = MixFog(finalCol, IN.fogCoord);
                return half4(finalCol, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _TopColor;
                half4 _BottomColor;
                half4 _RimSunColor;
                half _TexColorBlend;
                float _GradientMinY;
                float _GradientMaxY;
                half _WorldVariation;
                half _WrapLighting;
                half _RimStrength;
                float _WindSpeed;
                float _WindStrength;
                float _WindScale;
                float _WindStartY;
                half _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float3 _LightDirection;

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                float3 posOS = IN.positionOS.xyz;
                float heightRange = max(0.05, _GradientMaxY - _GradientMinY);
                float3 posWS = TransformObjectToWorld(posOS);
                float windMask = saturate((posOS.y - _WindStartY) / max(0.25, heightRange));
                windMask = windMask * windMask;
                float phase = _Time.y * _WindSpeed + (posWS.x * 0.65 + posWS.z * 0.85) * _WindScale;
                float gust = sin(phase) * 0.68 + sin(phase * 1.85 + 1.2) * 0.32;
                posWS.x += gust * _WindStrength * windMask;
                posWS.z += gust * 0.55 * _WindStrength * windMask;
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, _LightDirection));
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 ShadowFrag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                #if defined(_ALPHATEST_ON)
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _TopColor;
                half4 _BottomColor;
                half4 _RimSunColor;
                half _TexColorBlend;
                float _GradientMinY;
                float _GradientMaxY;
                half _WorldVariation;
                half _WrapLighting;
                half _RimStrength;
                float _WindSpeed;
                float _WindStrength;
                float _WindScale;
                float _WindStartY;
                half _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthVert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 DepthFrag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                #if defined(_ALPHATEST_ON)
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }
    }
}
