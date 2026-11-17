Shader "SAGARA/FishingUI/SoftModelPreview" {
 Properties { [MainColor] _BaseColor("Original fish palette",Color)=(1,1,1,1) }
 SubShader { Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
 Pass { Tags{"LightMode"="UniversalForward"} Cull Off ZWrite On ZTest LEqual
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial) half4 _BaseColor; CBUFFER_END
 struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;};
 struct V{float4 positionCS:SV_POSITION;float3 normalVS:TEXCOORD0;};
 V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.normalVS=mul((float3x3)UNITY_MATRIX_V,TransformObjectToWorldNormal(i.normalOS));return o;}
 half4 frag(V i,bool front:SV_IsFrontFace):SV_Target {
  float3 n=normalize(i.normalVS)*(front?1:-1);
  float key=saturate(dot(n,normalize(float3(-.48,.65,.80))));
  float fill=saturate(dot(n,normalize(float3(.7,.2,.55))));
  float diffuse=.64+.32*key+.12*fill;
  float highlight=pow(saturate(dot(n,normalize(float3(-.25,.42,1)))),28)*.09;
  return half4(_BaseColor.rgb*diffuse+highlight*half3(1,.95,.84),1);
 }
 ENDHLSL }
 } Fallback Off
}