Shader "SAGARA/FishingUI/SoftModelPreview"
{
    Properties
    {
        [MainColor] _BaseColor("Original specimen color", Color) = (1,1,1,1)
        _UseVertexColor("Use Vertex Color", Range(0, 1)) = 0
        _SwayAmount("Underwater Sway Amount", Range(0, 1)) = 0
        _SilhouetteMode("Darkened Silhouette Mode", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _UseVertexColor;
                half _SwayAmount;
                half _SilhouetteMode;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalVS : TEXCOORD0;
                float3 viewDirVS : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 pos = i.positionOS.xyz;

                // Organic underwater current sway for corals & anemones (anchored at base y <= 0.05)
                if (_SwayAmount > 0.001h)
                {
                    float hFactor = saturate((pos.y + 0.05) * 1.15);
                    float bend = hFactor * hFactor * _SwayAmount;
                    float t = _Time.y * 2.25;
                    pos.x += sin(t + pos.y * 3.4 + pos.z * 2.1) * 0.068 * bend;
                    pos.z += cos(t * 0.85 + pos.y * 2.8 + pos.x * 2.4) * 0.052 * bend;
                }

                float3 posWS = TransformObjectToWorld(pos);
                float3 posVS = TransformWorldToView(posWS);
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalVS = mul((float3x3)UNITY_MATRIX_V, TransformObjectToWorldNormal(i.normalOS));
                o.viewDirVS = normalize(-posVS);
                o.color = i.color;
                return o;
            }

            half4 frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(i.normalVS) * (front ? 1.0 : -1.0);
                float3 v = normalize(i.viewDirVS);

                // Crisp 3-point sunlit underwater studio lighting
                float key = saturate(dot(n, normalize(float3(-0.46, 0.72, 0.78))));
                float fill = saturate(dot(n, normalize(float3(0.74, 0.22, 0.50))));
                float bounce = saturate(dot(n, float3(0.0, -1.0, 0.25)));
                float rim = pow(1.0 - saturate(dot(n, v)), 2.6);
                float spec = pow(saturate(dot(n, normalize(float3(-0.28, 0.55, 0.92)))), 26.0);

                half3 albedo = lerp(_BaseColor.rgb, i.color.rgb * _BaseColor.rgb, _UseVertexColor);
                half3 lit = albedo * (0.58h + 0.44h * key + 0.16h * fill)
                    + albedo * half3(0.45h, 0.82h, 0.86h) * (0.14h * bounce)
                    + half3(1.0h, 0.96h, 0.86h) * (0.15h * spec)
                    + half3(0.55h, 0.92h, 0.96h) * (0.22h * rim);

                // Mysterious darkened 3D silhouette with subtle coastal rim light for undiscovered specimens
                half3 silhouette = half3(0.075h, 0.165h, 0.205h) * (0.72h + 0.35h * key)
                    + half3(0.32h, 0.68h, 0.72h) * (0.34h * rim);

                half3 finalRgb = lerp(lit, silhouette, _SilhouetteMode);
                return half4(finalRgb, 1.0h);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
