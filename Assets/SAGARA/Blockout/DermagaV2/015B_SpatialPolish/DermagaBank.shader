Shader "SAGARA/DermagaV2/Coastal Bank"
{
    Properties
    {
        [MainTexture] _BaseMap ("Existing surface", 2D) = "white" {}
        [MainColor] _BaseColor ("Surface tint", Color) = (1,1,1,1)
        [NoScaleOffset] _GroundGrass ("Village grass", 2D) = "white" {}
        [NoScaleOffset] _GroundYoungGrass ("Village young grass", 2D) = "white" {}
        [NoScaleOffset] _GroundDirt ("Village dirt path", 2D) = "white" {}
        [Toggle(_GROUND_DETAIL)] _GroundDetail ("Use layered ground textures", Float) = 0
        [KeywordEnum(Woodland,Bluff,Forest,Beach,Stair,Mangrove,Ground)] _Habitat ("Bank habitat", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LitPassVertex
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma shader_feature_local _HABITAT_WOODLAND _HABITAT_BLUFF _HABITAT_FOREST _HABITAT_BEACH _HABITAT_STAIR _HABITAT_MANGROVE _HABITAT_GROUND
            #pragma shader_feature_local_fragment _GROUND_DETAIL
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            #if defined(_GROUND_DETAIL)
            TEXTURE2D(_GroundGrass);
            SAMPLER(sampler_GroundGrass);
            TEXTURE2D(_GroundYoungGrass);
            SAMPLER(sampler_GroundYoungGrass);
            TEXTURE2D(_GroundDirt);
            SAMPLER(sampler_GroundDirt);
            #endif
            float randomCell(float2 p)
            {
                float3 q = frac(float3(p.xyx) * .1031);
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }
            float valueNoise(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                f = f*f*(3.-2.*f);
                return lerp(lerp(randomCell(cell),randomCell(cell+float2(1,0)),f.x),
                            lerp(randomCell(cell+float2(0,1)),randomCell(cell+1),f.x), f.y);
            }
            #if defined(_HABITAT_FOREST) || defined(_HABITAT_BEACH)
            half3 TerraceRock(float3 positionWS, float2 face, float fine, out float relief)
            {
                float bend = valueNoise(float2(face.x * .39, face.y * .84));
                float bedding = positionWS.y * 2.38 + face.x * .17
                    + (bend - .5) * 1.12 + (fine - .5) * .15;
                float layer = floor(bedding);
                float depth = frac(bedding);
                float softness = clamp(fwidth(bedding) * .85, .026, .18);
                float continuity = smoothstep(.37, .78,
                    valueNoise(float2(face.x * .89, layer * .67 + bend)));
                float lineDistance = min(depth, 1. - depth);
                float seam = continuity * (1. - smoothstep(.026, .145 + softness, lineDistance));
                float rim = continuity * smoothstep(.71, .87, depth) * (1. - smoothstep(.89, 1., depth));

                float column = face.x * .54 + layer * .31 + positionWS.y * .13 + bend * .44;
                float split = abs(frac(column) - .5);
                float fissure = (1. - smoothstep(.008, .047 + softness * .34, split))
                    * smoothstep(.10, .31, depth) * (1. - smoothstep(.66, .98, depth));
                float mineral = valueNoise(float2(face.x * .6 + layer * .21, face.y * 1.55));
                float grain = valueNoise(face * 10.7);
                half3 stone = lerp(half3(.205,.182,.145), half3(.405,.352,.272), mineral);
                relief = .019 * valueNoise(face * 2.9) + .009 * fine + .004 * grain;
                return stone * (1. + .19 * (fine - .5) + .16 * (grain - .5)
                    - .22 * seam - .19 * fissure + .095 * rim);
            }

            half3 TerraceNormal(float3 positionWS, half3 normal, float relief)
            {
                float3 alongX = ddx(positionWS);
                float3 alongY = ddy(positionWS);
                float3 crossX = cross(alongY, normal);
                float3 crossY = cross(normal, alongX);
                float determinant = dot(alongX, crossX);
                float3 gradient = sign(determinant) * (ddx(relief) * crossX + ddy(relief) * crossY)
                    / max(abs(determinant), 1e-6);
                gradient /= max(1., length(gradient) * 1.8);
                return normalize(normal - gradient);
            }
            #endif

            #if defined(_GROUND_DETAIL)
            half3 GroundAlbedo(float2 world, half3 baked, half3 surface, half amount)
            {
                // World-space tiling makes detail continuous across adjoining terrain meshes.
                float patches = smoothstep(.18, .84, valueNoise(world * .31));
                float2 rotated = float2(.80 * world.x + .60 * world.y, -.60 * world.x + .80 * world.y);
                half3 grass = SAMPLE_TEXTURE2D(_GroundGrass, sampler_GroundGrass, world * .105).rgb;
                half3 youngGrass = SAMPLE_TEXTURE2D(_GroundYoungGrass, sampler_GroundYoungGrass,
                    rotated * .115 + float2(.41, .23)).rgb;
                half3 meadow = lerp(grass * half3(.89,.76,1.01), youngGrass * half3(.70,.55,.90),
                    .24 + .42 * patches);

                half lift = (grass.g - .402) * 1.16 + (youngGrass.g - .775) * .73;
                meadow *= clamp(1. + 3.4 * lift, .73, 1.25);

                half3 dirt = SAMPLE_TEXTURE2D(_GroundDirt, sampler_GroundDirt,
                    rotated * .486 + float2(.14, .38)).rgb;
                half3 dirtVariation = SAMPLE_TEXTURE2D(_GroundDirt, sampler_GroundDirt,
                    world.yx * .227 + float2(.45, .81)).rgb;
                half3 trail = lerp(dirt, dirtVariation, .20 + .28 * patches) * half3(1.03, .96, .94);

                // The original baked colors retain the footprints of trails and grass.
                float pathEdge = (valueNoise(world * 2.2) - .5) * .10;
                float path = smoothstep(-.052, .152, baked.r - baked.g + pathEdge);
                float colorMask = smoothstep(.055, .16, max(baked.r, baked.g) - baked.b);
                half3 detail = lerp(meadow, trail, path);
                return lerp(surface, detail, amount * colorMask * lerp(.82, .67, path));
            }
            #endif

            half4 frag(Varyings i) : SV_Target
            {
                float3 p = i.positionWS;
                half3 normal = normalize(i.normalWS);
                SurfaceData surface;
                InitializeStandardLitSurfaceData(i.uv, surface);
                half3 original = surface.albedo;
                float2 face = float2(p.x + .36*p.z, p.y + .16*p.z);
                float broad = valueNoise(face * .58);
                float fine = valueNoise(face * float2(2.3,4.2));
                float strata = valueNoise(float2(face.x*.87, p.y*3.4 + broad*2.7 + .3*fine));
                float mossNoise = .50*valueNoise(p.xz*.73 + face*.23) + .32*fine + .18*valueNoise(face*6.5);
                // Linear albedos for weathered coastal sandstone, shaded crevices and lush grass lip.
                half3 earth = lerp(half3(.175,.156,.110),half3(.385,.326,.232),.54*broad+.46*strata);
                // Interrupted bedding planes and short cross-joints give the exposed face a rock scale.
                float bedding = p.y*2.15 + .20*p.x + .54*valueNoise(face*.95) + .12*valueNoise(face*4.1);
                float bedDistance = frac(bedding);
                float outcrop = smoothstep(.25,.65,valueNoise(float2(face.x*.65,bedding*.22)));
                float seam = (1. - smoothstep(.027,.16,min(bedDistance,1. - bedDistance))) * outcrop;
                float lip = smoothstep(.74,.86,bedDistance) * (1. - smoothstep(.9,1.,bedDistance)) * outcrop;
                float joints = abs(frac(face.x*.82 + floor(bedding)*.41 + .24*valueNoise(face*2.3))-.5);
                float joint = (1.-smoothstep(.008,.045,joints))*(1.-smoothstep(.23,.78,bedDistance));
                float grain = valueNoise(face*18.3);
                earth *= .94 + .165*fine + .11*grain - .20*seam - .16*joint + .09*lip;
                #if defined(_HABITAT_FOREST) || defined(_HABITAT_BEACH)
                    half3 originalEarth = earth;
                    float relief;
                    half3 terrace = TerraceRock(p, face, fine, relief);
                    // Fade into the original terrain shading outside the two seaward faces.
                    #if defined(_HABITAT_FOREST)
                        float exposure = smoothstep(-25., -22., p.x)
                            * (1. - smoothstep(-5., -2.1, p.x))
                            * (1. - smoothstep(21., 23., p.z));
                    #else
                        float exposure = smoothstep(-31., -29.4, p.x)
                            * (1. - smoothstep(7.7, 9.1, p.x))
                            * (1. - smoothstep(11.8, 13.8, p.z));
                        float aroundBoardwalk = smoothstep(4.7, 6.8, p.x)
                            * (1. - smoothstep(8.95, 9.55, p.x)) * smoothstep(7.95, 9.08, p.z)
                            * (1. - smoothstep(15.2, 17.1, p.z));
                        exposure = max(exposure, aroundBoardwalk);
                    #endif
                    earth = terrace;
                #endif
                half3 moss = lerp(half3(.10,.176,.043),half3(.186,.283,.077),broad);
                half3 lawn = lerp(half3(.194,.345,.084),half3(.235,.398,.108),fine);
                #if defined(_HABITAT_BEACH)
                    const bool beach = true;
                #else
                    const bool beach = false;
                #endif
                float top = beach ? 2.35 : 4.35;
                float rim = smoothstep(top-1.34,top-.04,p.y+(mossNoise-.5)*1.32);
                float patches = smoothstep(.45,.75,mossNoise+.06*normal.y) * (beach?.40:.74);
                half3 bank = lerp(earth,moss,max(.85*rim,patches));
                bank = lerp(bank,lawn,.85*smoothstep(top-.46,top+.02,p.y+(fine-.5)*.46));
                float influence = 1.;
                
                #if defined(_HABITAT_WOODLAND) || defined(_HABITAT_BLUFF)
                {
                    // Keep the existing grass and road on the terrace top.
                    influence = 1.-smoothstep(3.68,4.28,p.y+(mossNoise-.5)*.78);
                    float toe = smoothstep(-31.5,-28.5,p.x)*(1.-smoothstep(2.35,2.70,p.y));
                    influence *= 1.-toe;
                    #if defined(_HABITAT_WOODLAND)
                        influence *= 1.-smoothstep(20.,26.,p.z);
                    #endif
                }
                #endif
                #if defined(_HABITAT_FOREST)
                    bank = lerp(bank,moss,.35+.36*rim);
                #endif
                #if defined(_HABITAT_STAIR)
                {
                    // The forest stair cuts through a grassy bank on the cottage terrace.
                    bank = lerp(moss,lawn,.47+.41*mossNoise);
                    bank = lerp(bank,earth,.28*smoothstep(.50,.75,fine));
                }
                #endif
                #if defined(_HABITAT_GROUND)
                    influence = 0.;
                #endif
                surface.albedo = lerp(original,bank,influence);
                #if defined(_HABITAT_FOREST) || defined(_HABITAT_BEACH)
                    // Keep the prior woodland finish around the unmarked sides of each terrace.
                    float woodlandRim = smoothstep(3.01, 4.31, p.y + (mossNoise - .5) * 1.32);
                    float woodlandCover = max(.85 * woodlandRim,
                        .74 * smoothstep(.45, .75, mossNoise + .06 * normal.y));
                    half3 woodland = lerp(originalEarth, moss, woodlandCover);
                    woodland = lerp(woodland, lawn, .85 * smoothstep(3.89, 4.37, p.y + (fine - .5) * .46));
                    float woodlandInfluence = (1. - smoothstep(3.68, 4.28, p.y + (mossNoise - .5) * .78))
                        * (1. - smoothstep(-31.5, -28.5, p.x) * (1. - smoothstep(2.35, 2.70, p.y)))
                        * (1. - smoothstep(20., 26., p.z));
                    // The mesh lip shares the terrain's UV projection and material tint.
                    float grassLip = smoothstep(top - .35, top - .035, p.y);
                    half3 seawardBank = lerp(surface.albedo, original, grassLip);
                    surface.albedo = lerp(lerp(original, woodland, woodlandInfluence), seawardBank, exposure);
                #endif
                #if defined(_HABITAT_MANGROVE)
                    // Keep the shore's existing grass projection at the lip and darken toward the tidal silt.
                    half3 wetSilt = lerp(half3(.128,.111,.077), half3(.257,.214,.149), broad);
                    half3 mineralBank = lerp(earth, moss, .13 + .22 * mossNoise);
                    float tidalEdge = smoothstep(-.62, .02, p.y + (fine - .5) * .19);
                    half3 headland = lerp(wetSilt, mineralBank, tidalEdge);
                    float headlandGrass = smoothstep(.43, .85, p.y + (fine - .5) * .12);
                    surface.albedo = lerp(headland, original, headlandGrass);
                #endif
                #if defined(_HABITAT_BEACH)
                    // The eastern toe meets mangrove silt below the entrance to the wooden walkway.
                    float tidalJoin = smoothstep(6.20, 7.58, p.x)
                        * smoothstep(8.12, 9.05, p.z) * (1. - smoothstep(13.1, 14.4, p.z))
                        * (1. - smoothstep(.11, .73, p.y));
                    half3 tidalShade = lerp(half3(.243,.185,.116), half3(.339,.263,.173), broad);
                    surface.albedo = lerp(surface.albedo, tidalShade, tidalJoin * .85);
                #endif
                #if defined(_GROUND_DETAIL)
                    half groundCover = 1.;
                    #if defined(_HABITAT_FOREST) || defined(_HABITAT_BEACH)
                        groundCover = grassLip;
                    #elif defined(_HABITAT_MANGROVE)
                        groundCover = headlandGrass;
                    #elif defined(_HABITAT_WOODLAND) || defined(_HABITAT_BLUFF)
                        groundCover = 1. - influence;
                    #endif
                    groundCover *= smoothstep(.46,.91,normal.y);
                    surface.albedo = GroundAlbedo(p.xz, original, surface.albedo, groundCover);
                #endif
                surface.smoothness = .12;
                InputData inputData;
                InitializeInputData(i,surface.normalTS,inputData);
                #if defined(_HABITAT_FOREST) || defined(_HABITAT_BEACH)
                    inputData.normalWS = TerraceNormal(p, inputData.normalWS,
                        relief * exposure * (1. - grassLip) * (1. - .86 * rim));
                #endif
                InitializeBakedGIData(i,inputData);
                half4 shaded = UniversalFragmentPBR(inputData,surface);
                return half4(MixFog(shaded.rgb,inputData.fogCoord),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
