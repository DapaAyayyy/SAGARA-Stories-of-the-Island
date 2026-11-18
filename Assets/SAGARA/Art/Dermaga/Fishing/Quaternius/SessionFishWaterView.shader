Shader "SAGARA/Dermaga/SessionFishWaterView" {
 Properties { [MainColor] _BaseColor("Color",Color)=(1,1,1,1) _WaterHeight("Water level",Float)=-.9 }
 SubShader {
 Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-30" "RenderType"="Transparent"}
 HLSLINCLUDE
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 CBUFFER_START(UnityPerMaterial) half4 _BaseColor;float _WaterHeight;CBUFFER_END
 struct A{float4 p:POSITION;float3 n:NORMAL;};struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;};
 V vert(A a){V o;VertexPositionInputs p=GetVertexPositionInputs(a.p.xyz);o.p=p.positionCS;o.w=p.positionWS;o.n=TransformObjectToWorldNormal(a.n);return o;}
 half4 fishColor(V i){Light sun=GetMainLight();half light=.65+.35*saturate(dot(normalize(i.n),sun.direction));return half4(_BaseColor.rgb*light,1);}
 half4 under(V i):SV_Target{clip(_WaterHeight-i.w.y);half4 c=fishColor(i);c.rgb=lerp(c.rgb,half3(.12,.65,.68),.38);c.a=.62;return c;}
 half4 above(V i):SV_Target{clip(i.w.y-_WaterHeight);return fishColor(i);}
 ENDHLSL
 Pass { Name "UnderwaterSessionPreview" Tags {"LightMode"="SRPDefaultUnlit"} ZWrite Off ZTest Always Cull Back Blend SrcAlpha OneMinusSrcAlpha
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment under
 ENDHLSL
 }
 Pass { Name "AboveWaterFish" Tags {"LightMode"="UniversalForward"} ZWrite On ZTest LEqual Cull Back
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment above
 ENDHLSL
 }
 }
}
