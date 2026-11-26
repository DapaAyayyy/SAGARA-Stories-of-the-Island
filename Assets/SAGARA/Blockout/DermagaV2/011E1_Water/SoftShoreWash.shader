Shader "SAGARA/DermagaV2/SoftShoreWash"
{
    Properties
    {
        _Color("Surf color", Color) = (.82, .91, .81, 1)
        _Opacity("Opacity", Range(0, 1)) = 0.76
        _FlowSpeed("Flow speed", Range(0, 2)) = 0.83
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
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
                half4 _Color;
                half _Opacity;
                half _FlowSpeed;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float along = input.uv.x;
                float across = input.uv.y;
                float time = _Time.y * _FlowSpeed;
                float surge = sin(time * 1.22 + along * 0.18);
                float runup = 0.47 + 0.125 * surge + 0.065 * sin(along * 0.73 - time * 0.54);
                float breakup = sin(along * 1.27 - time * 0.64) + 0.62 * sin(along * 0.47 + time * 0.45);
                float continuity = smoothstep(-0.55, 1.14, breakup);
                float width = lerp(0.105, 0.145, 0.5 + 0.5 * surge);

                float wash = 1.0 - smoothstep(0.025, 0.24, abs(across - runup + 0.04));
                float rim = 1.0 - smoothstep(0.020, width, abs(across - runup));
                float retreat = (1.0 - smoothstep(0.035, 0.11, abs(across - runup - 0.17))) * (1.0 - continuity);
                float edge = smoothstep(0.0, 0.14, across) * (1.0 - smoothstep(0.82, 1.0, across));
                half3 tint = lerp(half3(0.35, 0.76, 0.68), _Color.rgb, rim * (0.55 + 0.45 * continuity));
                float opacity = (wash * 0.19 + rim * (0.16 + 0.47 * continuity) + retreat * 0.10) * edge * _Opacity;
                return half4(tint, opacity * _Color.a);
            }
            ENDHLSL
        }
    }
}
