Shader "SAGARA/Dermaga/SoftFishingAura" {
Properties {[MainColor] _BaseColor("Aura color",Color)=(1,1,1,.22)}
SubShader {Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"} Pass {Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
CBUFFER_START(UnityPerMaterial) half4 _BaseColor; CBUFFER_END
struct A{float4 p:POSITION;float2 uv:TEXCOORD0;};struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
V vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.uv=a.uv;return o;}
half4 frag(V i):SV_Target{float r=length(i.uv*2-1);return half4(_BaseColor.rgb,_BaseColor.a*(1-smoothstep(.15,1,r)));}
ENDHLSL
}}}