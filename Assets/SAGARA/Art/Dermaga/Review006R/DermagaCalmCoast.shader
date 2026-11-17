Shader "SAGARA/Dermaga/CalmAnimatedCoast"
{
 Properties { _ShoreMap("Coast distance",2D)="white"{} _Near("Lagoon turquoise",Color)=(.07,.51,.57,1) _Far("Offshore blue",Color)=(.025,.20,.42,1) _Foam("Foam overlay",Float)=0 }
 SubShader { Tags{"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"} Pass { Tags{"LightMode"="UniversalForward"}
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 TEXTURE2D(_ShoreMap);SAMPLER(sampler_ShoreMap);
 CBUFFER_START(UnityPerMaterial) half4 _Near;half4 _Far;float _Foam;float4 _ShoreMap_ST; CBUFFER_END
 struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;};
 V vert(A a){V o;VertexPositionInputs p=GetVertexPositionInputs(a.p.xyz);o.p=p.positionCS;o.world=p.positionWS;return o;}
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
 half4 frag(V i):SV_Target{
 float2 p=i.world.xz;float t=_Time.y;
 float distance=SAMPLE_TEXTURE2D(_ShoreMap,sampler_ShoreMap,(p+75)/150).r*30;
 float offshore=smoothstep(2,24,distance);
 float n=noise(p*.32+float2(t*.028,t*-.018));
 float wave=sin(p.x*2.3+p.y*3.1+t*.75+noise(p*.62-t*.025)*4);
 float crest=smoothstep(.87,.98,wave)*smoothstep(.4,.76,n);
 half3 color=lerp(_Near.rgb,_Far.rgb,offshore)*( .98+.04*n);
 color+=half3(.12,.19,.18)*crest*.06;
 color+=.0035*sin(p.x*6.3-p.y*4.4+t*.58)*sin(p.y*3.2+t*.34);
 if(_Foam>.5){float l=noise(p*3.7+float2(t*.12,t*.055));float breathing=.5+.5*sin(p.x*1.3+p.y*.7+t*.6);color=lerp(_Near.rgb,half3(.82,.94,.85),smoothstep(.34,.8,l*.8+breathing*.2));}
 Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
 half3 lighting=SampleSH(half3(0,1,0))*.65+sun.color*(.25+.75*saturate(sun.direction.y))*lerp(.5,1,sun.shadowAttenuation);
 return half4(color*lighting,1);
 }
 ENDHLSL
 } }
 Fallback "Universal Render Pipeline/Lit"
}