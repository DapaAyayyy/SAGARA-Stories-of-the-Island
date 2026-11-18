Shader "SAGARA/Dermaga/TropicalMovingOcean" {
Properties { _ShoreMap("Shore distance",2D)="white"{} _Shallow("Shallows",Color)=(.035,.62,.54,1) _Deep("Offshore",Color)=(.015,.18,.39,1) }
SubShader { Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
Pass { Tags{"LightMode"="UniversalForward"}
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
TEXTURE2D(_ShoreMap);SAMPLER(sampler_ShoreMap);
CBUFFER_START(UnityPerMaterial) half4 _Shallow,_Deep;CBUFFER_END
struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;};
V vert(A a){V o;VertexPositionInputs v=GetVertexPositionInputs(a.p.xyz);o.p=v.positionCS;o.w=v.positionWS;return o;}
float detail(float2 p){float a=sin(p.x*2.4+sin(p.y*1.7)*1.25);float b=sin(p.y*2.8+sin(p.x*1.1)*.85);return 1-smoothstep(.04,.16,abs(a+b));}
half4 frag(V i):SV_Target {
 float2 p=i.w.xz;float t=_Time.y;
 float d=SAMPLE_TEXTURE2D(_ShoreMap,sampler_ShoreMap,(p+75)/150).r*30;
 float offshore=smoothstep(1,21,d);
 float2 flowA=p+float2(-.24,.09)*t;
 float2 flowB=float2(p.x*.81+p.y*.36,-p.x*.36+p.y*.81)+float2(.16,-.31)*t;
 float a=detail(flowA),b=detail(flowB*1.85);
 float broad=.5+.5*sin(flowA.x*.53+sin(flowA.y*.42));
 half3 col=lerp(_Shallow.rgb,_Deep.rgb,offshore);
 col=lerp(col,col*half3(.8,1.12,1.1),broad*.16);
 float caustics=a*.19+b*.11;
 col=lerp(col,half3(.25,.88,.79),caustics*(1-offshore*.55));
 float crest=pow(saturate(.5+.5*sin(flowA.x*1.45+flowA.y*2.1+sin(flowB.x)*1.4+sin(flowB.y*2.4)*.8)),40);
 float broken=smoothstep(.45,.85,.5+.5*sin(flowB.y*3+sin(flowA.x*2)));
 col=lerp(col,half3(.73,.96,.88),crest*broken*.20);
 float shine=pow(saturate(sin(flowB.x*4.2)*sin(flowB.y*3.1)),28);
 col+=half3(.25,.32,.29)*shine*.18;
 Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));col*=lerp(.82,1,sun.shadowAttenuation);
 return half4(col,1);
}
ENDHLSL
}}
}
