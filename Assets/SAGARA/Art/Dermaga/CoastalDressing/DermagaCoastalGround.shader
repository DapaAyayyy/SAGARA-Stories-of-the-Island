Shader "SAGARA/Dermaga/CoastalGround"
{
 Properties {
 _GrassTex("SAGARA grass",2D)="white"{}
 _DirtTex("SAGARA dirt",2D)="white"{}
 _SandColor("Coastal sand",Color)=(0.83,0.72,0.48,1)
 _GrassTint("Grass tint",Color)=(0.92,0.96,0.86,1)
 _Surface("Surface: sand grass path wet sea foam",Float)=0
 }
 SubShader {
 Tags{"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
 Pass {
 Name "CoastalForward"
 Tags{"LightMode"="UniversalForward"}
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 TEXTURE2D(_GrassTex);SAMPLER(sampler_GrassTex);
 TEXTURE2D(_DirtTex);SAMPLER(sampler_DirtTex);
 CBUFFER_START(UnityPerMaterial)
 float4 _GrassTex_ST;float4 _DirtTex_ST;half4 _SandColor;half4 _GrassTint;float _Surface;
 CBUFFER_END
 struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;half4 color:COLOR;};
 struct V{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half4 color:COLOR;};
 V vert(A a){V o;VertexPositionInputs p=GetVertexPositionInputs(a.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(a.normalOS);o.color=a.color;return o;}
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
 half4 frag(V i):SV_Target{
 float2 p=i.positionWS.xz;
 half3 grass=SAMPLE_TEXTURE2D(_GrassTex,sampler_GrassTex,p/10).rgb*_GrassTint.rgb;
 half3 dirt=SAMPLE_TEXTURE2D(_DirtTex,sampler_DirtTex,p/2).rgb;
 half3 detail=SAMPLE_TEXTURE2D(_DirtTex,sampler_DirtTex,p/3.6).rgb;
 float detailGrey=dot(detail,half3(.3,.59,.11));
 float n=noise(p*.42),fine=noise(p*11);
 half3 sand=_SandColor.rgb*(.84+.23*detailGrey+.10*n+.025*fine);
 half3 albedo=sand;
 float blend=saturate(i.color.a+(noise(p*4)-.5)*.6);
 blend=smoothstep(.08,.92,blend);
 if(_Surface>.5 && _Surface<1.5)albedo=lerp(sand,grass,blend);
 if(_Surface>1.5 && _Surface<2.5)albedo=lerp(lerp(sand,grass,saturate(i.color.b)),dirt,blend);
 if(_Surface>2.5 && _Surface<3.5)albedo=sand*half3(.77,.87,.88);
 if(_Surface>3.5 && _Surface<4.5){
 float r=noise(p*.25),rip=sin(p.x*3.3+p.y*4.4+noise(p*1.3)*5);
 albedo=lerp(half3(.045,.39,.53),half3(.11,.61,.64),r);
 albedo+=smoothstep(.96,.995,rip)*.018*smoothstep(.52,.75,noise(p*.8));
 }
 if(_Surface>4.5){albedo=lerp(half3(.12,.62,.65),half3(.82,.94,.85),smoothstep(.25,.67,noise(p*4)+sin(p.y*5+p.x*3)*.12));}
 Light sun=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
 half ndl=saturate(dot(normalize(i.normalWS),sun.direction));
 half3 indirect=SampleSH(normalize(i.normalWS));
 half3 light=indirect*.75+sun.color*(.22+.78*ndl)*lerp(.36,1,sun.shadowAttenuation);
 return half4(albedo*light,1);
 }
 ENDHLSL
 }
 UsePass "Universal Render Pipeline/Lit/DepthOnly"
 UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 }
 Fallback "Universal Render Pipeline/Lit"
}