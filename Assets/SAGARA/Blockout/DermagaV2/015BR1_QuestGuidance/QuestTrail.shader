Shader "SAGARA/Dermaga V2/Quest Trail"
{
    Properties
    {
        _GlowIntensity ("Glow Intensity", Range(0.5, 3.0)) = 1.45
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+15" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _GlowIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 ws = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = ws;
                output.positionCS = TransformWorldToHClip(ws);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Subtle warm sunlight shimmer wave along the ground guide
                half shimmer = 0.92h + 0.14h * sin(_Time.y * 4.4h + (input.positionWS.x + input.positionWS.z) * 1.1h);
                // Keep soft grounding cushion dark (low luminance), lift golden core for bloom shimmer
                half lum = dot(input.color.rgb, half3(0.299h, 0.587h, 0.114h));
                half boost = lerp(1.0h, _GlowIntensity * shimmer, smoothstep(0.28h, 0.70h, lum));
                return half4(input.color.rgb * boost, input.color.a);
            }
            ENDHLSL
        }
    }
}
