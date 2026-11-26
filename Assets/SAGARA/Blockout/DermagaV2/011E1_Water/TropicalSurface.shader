Shader "SAGARA/DermagaV2/TropicalSurface"
{
    Properties
    {
        _ShoreMap("Shore distances and wavelets", 2D) = "white" {}
        _Shallow("Shallows", Color) = (.14, .78, .74, 1)
        _Deep("Offshore", Color) = (.08, .45, .72, 1)
        _FlowSpeed("Flow speed", Range(0, 2)) = 0.95
        _SwellContrast("Swell contrast", Range(0, 0.25)) = 0.08
        _RippleStrength("Ripple strength", Range(0, 0.5)) = 0.22
        _StrokeStrength("Wind ripples", Range(0, 0.5)) = 0.34
        _NearshoreStrength("Shore foam", Range(0, 0.75)) = 0.62
        _PostStrength("Pile ripple strength", Range(0, 0.5)) = 0.16
        _WestPiles("West piles (x, z, x, z)", Vector) = (13.15, -5.2, 13.15, -8.6)
        _EastPiles("East piles (x, z, x, z)", Vector) = (17.25, -5.2, 17.25, -8.6)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_ShoreMap);
            SAMPLER(sampler_ShoreMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _Shallow;
                half4 _Deep;
                float4 _WestPiles;
                float4 _EastPiles;
                half _FlowSpeed;
                half _SwellContrast;
                half _RippleStrength;
                half _StrokeStrength;
                half _NearshoreStrength;
                half _PostStrength;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                return output;
            }

            float PileRipple(float2 relative, float time)
            {
                float distanceFromPile = length(relative);
                float footprint = smoothstep(0.17, 0.32, distanceFromPile)
                    * (1.0 - smoothstep(0.40, 0.91, distanceFromPile));
                float phase = distanceFromPile * 17.0 - time * 2.6 + relative.x * 1.4;
                float gap = smoothstep(-0.45, 0.62, sin(dot(relative, float2(4.1, -3.0)) + time * 0.78));
                return footprint * smoothstep(0.43, 0.95, sin(phase)) * lerp(0.27, 1.0, gap);
            }

            float WindRipples(float2 surface, float time, float swell, float crossSwell, float2 warp)
            {
                float2 drift = surface + float2(warp.x, crossSwell) * float2(0.83, 0.46)
                    + float2(-0.41, 0.18) * time;
                float2 crossDrift = float2(dot(surface, float2(0.73, 0.68)), dot(surface, float2(-0.68, 0.73)))
                    + float2(warp.y, swell) * float2(0.54, 0.38) + float2(0.20, 0.23) * time;
                float crest = SAMPLE_TEXTURE2D(_ShoreMap, sampler_LinearRepeat, drift / 22.0).b;
                float crossCrest = SAMPLE_TEXTURE2D(_ShoreMap, sampler_LinearRepeat, crossDrift / 16.5).b;
                return max(crest * (0.81 + 0.19 * crossSwell), crossCrest * (0.43 + 0.15 * swell));
            }

            // Stylized shallow tropical caustic web (crisp sunlit turquoise shimmer near beach & dock!)
            float ShallowCaustics(float2 p, float t)
            {
                float2 p1 = p * 0.62 + float2(t * 0.22, -t * 0.16);
                float2 p2 = float2(p.x * 0.54 - p.y * 0.48, p.x * 0.48 + p.y * 0.54) + float2(-t * 0.18, t * 0.21);
                float w1 = abs(sin(p1.x + sin(p1.y * 1.3)) * cos(p1.y + cos(p1.x * 1.1)));
                float w2 = abs(sin(p2.x + cos(p2.y * 1.2)) * cos(p2.y + sin(p2.x * 1.4)));
                float c = 1.0 - min(w1, w2);
                return pow(saturate(c), 4.0);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 surface = input.positionWS.xz;
                float time = _Time.y * _FlowSpeed;
                float2 distances = SAMPLE_TEXTURE2D(_ShoreMap, sampler_ShoreMap, (surface + 75.0) / 150.0).rg;
                float distanceToShore = distances.x * 30.0;
                float foamDistance = min(distanceToShore, distances.y * 6.0);
                float offshore = smoothstep(0.95, 22.0, distanceToShore);
                float crystalShallow = 1.0 - smoothstep(0.4, 7.5, distanceToShore);

                float warpX = sin(dot(surface, float2(0.31, -0.23)) + time * 0.47);
                float warpY = sin(dot(surface, float2(-0.24, 0.30)) - time * 0.39);
                float swell = sin(dot(surface, float2(0.83, 0.56)) + warpX * 1.18 + warpY * 0.47 - time * 1.04);
                float crossSwell = sin(dot(surface, float2(-0.65, 1.09)) + warpY * 1.25 + warpX * 0.45 + time * 0.75);

                float patch = sin(dot(surface, float2(0.32, 0.44)) + warpX * 0.74 - time * 0.46);
                float firstCrest = smoothstep(0.64, 0.97, swell) * smoothstep(-0.30, 0.73, patch);
                float secondCrest = smoothstep(0.73, 0.99, crossSwell) * smoothstep(-0.36, 0.73, -patch);
                float ripple = (firstCrest * 0.66 + secondCrest * 0.34) * lerp(1.0, 0.66, offshore);

                // Vibrant tropical turquoise shallows -> sunlit island azure deep water
                half3 body = lerp(_Shallow.rgb, _Deep.rgb, offshore);
                // Warm sunlit shallow turquoise glow right near golden sand beaches
                body = lerp(body, half3(0.28, 0.86, 0.80), crystalShallow * 0.32);
                body *= 1.0 + (swell * 0.59 + crossSwell * 0.41) * _SwellContrast;

                // Subtle animated sunlit shallow caustics
                float caust = ShallowCaustics(surface, time);
                body += half3(0.20, 0.34, 0.30) * (caust * crystalShallow * 0.34);

                half3 rippleTint = lerp(half3(0.64, 0.94, 0.88), half3(0.38, 0.72, 0.88), offshore);
                body = lerp(body, rippleTint, ripple * _RippleStrength);
                float wind = WindRipples(surface, time, swell, crossSwell, float2(warpX, warpY));
                half3 windTint = lerp(rippleTint, body, 0.22);
                body = lerp(body, windTint, wind * lerp(1.0, 0.80, offshore) * _StrokeStrength);

                // Crisp sunlit seafoam shoreline lace + gentle double-band coastal wash!
                float shoreline = sin(dot(surface, float2(1.77, -1.29)) + warpY * 1.38 - time * 0.89);
                float breaks = smoothstep(-0.28, 0.78, shoreline * 0.63 + patch * 0.37);
                float runup = 0.46 + 0.22 * sin(warpX * 2.48 + warpY * 1.56 - time * 1.32);
                float lace = 1.0 - smoothstep(0.08, 0.46, abs(foamDistance - runup));
                float innerLap = 1.0 - smoothstep(0.05, 0.58, foamDistance);
                float wash = 1.0 - smoothstep(0.45, 2.50, foamDistance);
                float foam = saturate(lace * 0.76 * breaks + innerLap * 0.52 + wash * 0.16 * breaks);
                body = lerp(body, half3(0.88, 0.97, 0.94), foam * _NearshoreStrength);

                // Dock pile ripples
                [branch]
                if (_PostStrength > 0.0 && abs(surface.x - 15.2) < 3.3 && abs(surface.y + 6.9) < 2.9)
                {
                    float contact = PileRipple(surface - _WestPiles.xy, time);
                    contact += PileRipple(surface - _WestPiles.zw, time + 0.36);
                    contact += PileRipple(surface - _EastPiles.xy, time + 0.63);
                    contact += PileRipple(surface - _EastPiles.zw, time + 1.07);
                    body = lerp(body, half3(0.82, 0.96, 0.92), saturate(contact) * _PostStrength);
                }

                // Soft tropical sunlight sparkle on wave crests + grounded shadows from dock/cliffs/boat!
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float sparkle = smoothstep(0.88, 0.99, firstCrest * 0.6 + wind * 0.55) * sun.shadowAttenuation;
                body *= lerp(0.78, 1.0, sun.shadowAttenuation);
                body += half3(0.95, 0.98, 0.92) * (sparkle * 0.22);
                return half4(body, 1.0);
            }
            ENDHLSL
        }
    }
}
