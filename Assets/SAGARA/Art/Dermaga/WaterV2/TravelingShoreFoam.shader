Shader "SAGARA/Dermaga/TravelingShoreFoam" {
Properties{ _Color("Foam",Color)=(.85,1,.93,1) }
SubShader{Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
Pass{Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
CBUFFER_START(UnityPerMaterial)half4 _Color;CBUFFER_END
struct A{float4 p:POSITION;float2 uv:TEXCOORD0;};struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
V vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.uv=a.uv;return o;}
half4 frag(V i):SV_Target{
 float t=_Time.y;
 float along=i.uv.x;float cross=i.uv.y;
 float center=.46+.24*sin(t*1.65+along*.18);
 float ruffle=sin(along*7-t*.9)*.045+sin(along*12+t*.5)*.02;
 float band=1-smoothstep(.035,.14,abs(cross-center+ruffle));
 float breakup=smoothstep(-.45,.55,sin(along*4+t*.8)+sin(along*2.2-t*.4));
 float edge=smoothstep(0,.12,cross)*(1-smoothstep(.84,1,cross));
 return half4(lerp(half3(.15,.75,.69),_Color.rgb,band),band*edge*lerp(.35,.85,breakup));
}
ENDHLSL
}}
}
