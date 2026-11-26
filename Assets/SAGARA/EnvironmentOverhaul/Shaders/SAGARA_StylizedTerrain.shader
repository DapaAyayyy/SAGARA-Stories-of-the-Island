Shader "SAGARA/Environment/StylizedTerrain"
{
    Properties
    {
        _SplatMap ("World Splat Mask (R=Grass G=Dirt B=Sand A=Silt)", 2D) = "red" {}
        _SplatBounds ("World Bounds (minX, minZ, spanX, spanZ)", Vector) = (-82, -22, 114, 114)
        _GrassMap ("Lush Grass Albedo", 2D) = "white" {}
        _DirtMap ("Warm Dirt Trail Albedo", 2D) = "white" {}
        _SandMap ("Golden Beach Sand Albedo", 2D) = "white" {}
        _SiltMap ("Mangrove Silt Albedo", 2D) = "white" {}
        _CliffMap ("Coastal Cliff Albedo (Triplanar)", 2D) = "white" {}
        [Normal] _CliffNormal ("Coastal Cliff Normal (Triplanar)", 2D) = "bump" {}

        _GrassTint ("Sunlit Grass Tint", Color) = (0.98, 1.02, 0.88, 1)
        _GrassShadeTint ("Shaded Meadow Patch Tint", Color) = (0.64, 0.84, 0.58, 1)
        _PathBorderTint ("Mossy Path Edge Tint", Color) = (0.48, 0.66, 0.34, 1)
        _DirtTint ("Warm Dirt Trail Tint", Color) = (1.02, 0.95, 0.84, 1)
        _SandTint ("Dry Beach Sand Tint", Color) = (1.00, 0.96, 0.88, 1)
        _WetSandTint ("Wet Shoreline Sand Tint", Color) = (0.62, 0.54, 0.40, 1)
        _CliffTint ("Warm Sandstone Cliff Tint", Color) = (0.94, 0.86, 0.72, 1)
        _CliffCrevice ("Cool Slate Cliff Crevice Tint", Color) = (0.42, 0.40, 0.38, 1)
        _CliffMossTint ("Overhanging Grass Lip Tint", Color) = (0.28, 0.64, 0.24, 1)

        _GroundScale ("Ground World Tile Scale", Range(0.05, 0.5)) = 0.20
        _DirtScale ("Dirt Path Tile Scale", Range(0.08, 0.6)) = 0.26
        _SandScale ("Sand World Tile Scale", Range(0.05, 0.5)) = 0.16
        _CliffScale ("Cliff Triplanar Scale", Range(0.08, 0.8)) = 0.28
        _CliffNormalStrength ("Cliff Normal Strength", Range(0, 2.5)) = 1.45
        _SlopeStart ("Cliff Slope Start (Normal Y)", Range(0.25, 0.95)) = 0.76
        _SlopeEnd ("Cliff Slope Full (Normal Y)", Range(0.10, 0.85)) = 0.56
        _WaterlineY ("Wet Sand Waterline Y", Float) = 0.18
        _Smoothness ("Matte Ground Smoothness", Range(0, 0.4)) = 0.08
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_SplatMap); SAMPLER(sampler_SplatMap);
            TEXTURE2D(_GrassMap); SAMPLER(sampler_GrassMap);
            TEXTURE2D(_DirtMap);  SAMPLER(sampler_DirtMap);
            TEXTURE2D(_SandMap);  SAMPLER(sampler_SandMap);
            TEXTURE2D(_SiltMap);  SAMPLER(sampler_SiltMap);
            TEXTURE2D(_CliffMap); SAMPLER(sampler_CliffMap);
            TEXTURE2D(_CliffNormal); SAMPLER(sampler_CliffNormal);

            CBUFFER_START(UnityPerMaterial)
                float4 _SplatBounds;
                half4 _GrassTint;
                half4 _GrassShadeTint;
                half4 _PathBorderTint;
                half4 _DirtTint;
                half4 _SandTint;
                half4 _WetSandTint;
                half4 _CliffTint;
                half4 _CliffCrevice;
                half4 _CliffMossTint;
                float _GroundScale;
                float _DirtScale;
                float _SandScale;
                float _CliffScale;
                half _CliffNormalStrength;
                half _SlopeStart;
                half _SlopeEnd;
                float _WaterlineY;
                half _Smoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;   // R=Grass, G=Dirt, B=BeachSand, A=MangroveSilt
                float2 uv : TEXCOORD0;  // x=CliffCrest, y=CliffBaseAO
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 vSplat : TEXCOORD2;
                half2 cliffEdge : TEXCOORD3;
                float fogCoord : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = frac(sin(dot(i, float2(127.1, 311.7))) * 43758.5453);
                float b = frac(sin(dot(i + float2(1, 0), float2(127.1, 311.7))) * 43758.5453);
                float c = frac(sin(dot(i + float2(0, 1), float2(127.1, 311.7))) * 43758.5453);
                float d = frac(sin(dot(i + float2(1, 1), float2(127.1, 311.7))) * 43758.5453);
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                OUT.vSplat = IN.color;
                OUT.cliffEdge = IN.uv;
                OUT.fogCoord = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 pWS = IN.positionWS;
                float3 nWS = normalize(IN.normalWS);

                // Pixel-accurate 1K World Splat Map for buttery smooth dirt paths!
                float2 splatUV = saturate((pWS.xz - _SplatBounds.xy) / max(float2(1, 1), _SplatBounds.zw));
                half4 mapSplat = SAMPLE_TEXTURE2D(_SplatMap, sampler_SplatMap, splatUV);

                // Dual-scale rotated sampling eliminates visible tiling while keeping crisp painted blade strokes!
                float2 uvG1 = pWS.xz * _GroundScale;
                float2 uvG2 = float2(0.8 * pWS.x + 0.6 * pWS.z, -0.6 * pWS.x + 0.8 * pWS.z) * (_GroundScale * 0.58) + float2(0.37, 0.19);
                float macroPatch = ValueNoise(pWS.xz * 0.085) * 0.60 + ValueNoise(pWS.xz * 0.22) * 0.40;

                // 1. Lush Story of Seasons: FoMT Hand-Painted Meadow Grass
                half3 grass1 = SAMPLE_TEXTURE2D(_GrassMap, sampler_GrassMap, uvG1).rgb;
                half3 grass2 = SAMPLE_TEXTURE2D(_GrassMap, sampler_GrassMap, uvG2).rgb;
                half3 grassCol = lerp(grass1, grass2, 0.34) * lerp(_GrassShadeTint.rgb, _GrassTint.rgb, smoothstep(0.16, 0.84, macroPatch));

                // 2. Warm Golden-Ochre Dirt Footpath + Crisp Mossy Edge Rim
                float2 uvD1 = pWS.xz * _DirtScale;
                float2 uvD2 = float2(0.75 * pWS.x - 0.66 * pWS.z, 0.66 * pWS.x + 0.75 * pWS.z) * (_DirtScale * 0.56) + float2(0.21, 0.63);
                half3 dirtCol = lerp(
                    SAMPLE_TEXTURE2D(_DirtMap, sampler_DirtMap, uvD1).rgb,
                    SAMPLE_TEXTURE2D(_DirtMap, sampler_DirtMap, uvD2).rgb,
                    0.32
                ) * _DirtTint.rgb;

                // 3. Wind-Rippled Tropical Beach Sand + Rich Golden-Amber Wet Tide Band
                float2 uvS1 = pWS.xz * _SandScale;
                float2 uvS2 = float2(0.82 * pWS.x + 0.57 * pWS.z, -0.57 * pWS.x + 0.82 * pWS.z) * (_SandScale * 0.54);
                half3 sandTex = lerp(
                    SAMPLE_TEXTURE2D(_SandMap, sampler_SandMap, uvS1).rgb,
                    SAMPLE_TEXTURE2D(_SandMap, sampler_SandMap, uvS2).rgb,
                    0.30
                );
                float wetNoise = (ValueNoise(pWS.xz * 0.48) - 0.5) * 0.20;
                float wetMask = 1.0 - smoothstep(_WaterlineY - 0.32, _WaterlineY + 0.28, pWS.y + wetNoise);
                half3 sandCol = sandTex * lerp(_SandTint.rgb, _WetSandTint.rgb, wetMask * 0.85);

                // 4. Mangrove Tidal Silt
                half3 siltCol = SAMPLE_TEXTURE2D(_SiltMap, sampler_SiltMap, pWS.xz * 0.24).rgb * lerp(1.0, 0.72, wetMask);

                // Crisp organic feathered masks (with subtle Story-of-Seasons grassy border hugging dirt paths!)
                float edgeNoise = (ValueNoise(pWS.xz * 1.15) - 0.5) * 0.16 + (ValueNoise(pWS.xz * 3.1) - 0.5) * 0.06;
                half wSand = smoothstep(0.32, 0.70, IN.vSplat.b + edgeNoise * 0.55);
                half wSilt = smoothstep(0.32, 0.72, IN.vSplat.a + edgeNoise * 0.55) * (1.0 - wSand);
                half rawDirt = mapSplat.g + edgeNoise;
                half wDirt = smoothstep(0.26, 0.68, rawDirt) * (1.0 - wSand) * (1.0 - wSilt);
                half pathBorder = smoothstep(0.10, 0.32, rawDirt) * (1.0 - smoothstep(0.32, 0.62, rawDirt)) * (1.0 - wSand) * (1.0 - wSilt);

                half3 groundAlbedo = lerp(grassCol, grassCol * _PathBorderTint.rgb * 1.38, pathBorder * 0.55);
                groundAlbedo = lerp(groundAlbedo, dirtCol, wDirt);
                groundAlbedo = lerp(groundAlbedo, sandCol, wSand);
                groundAlbedo = lerp(groundAlbedo, siltCol, wSilt);

                // 5. SCULPTED STYLIZED COASTAL SANDSTONE/LIMESTONE CLIFFS (Triplanar Faceted Strata + Crisp Overhanging Grass Lip!)
                float3 triW = pow(abs(nWS), 4.0);
                triW /= max(0.0001, triW.x + triW.y + triW.z);
                float3 tp = pWS * _CliffScale;
                half3 cX = SAMPLE_TEXTURE2D(_CliffMap, sampler_CliffMap, tp.zy).rgb;
                half3 cY = SAMPLE_TEXTURE2D(_CliffMap, sampler_CliffMap, tp.xz).rgb;
                half3 cZ = SAMPLE_TEXTURE2D(_CliffMap, sampler_CliffMap, tp.xy).rgb;
                half3 cliffTex = cX * triW.x + cY * triW.y + cZ * triW.z;

                float3 nx = UnpackNormalScale(SAMPLE_TEXTURE2D(_CliffNormal, sampler_CliffNormal, tp.zy), _CliffNormalStrength);
                float3 ny = UnpackNormalScale(SAMPLE_TEXTURE2D(_CliffNormal, sampler_CliffNormal, tp.xz), _CliffNormalStrength);
                float3 nz = UnpackNormalScale(SAMPLE_TEXTURE2D(_CliffNormal, sampler_CliffNormal, tp.xy), _CliffNormalStrength);
                float3 nTri = normalize(
                    float3(nWS.x, nx.y + nWS.y, nx.x + nWS.z) * triW.x +
                    float3(ny.x + nWS.x, nWS.y, ny.y + nWS.z) * triW.y +
                    float3(nz.x + nWS.x, nz.y + nWS.y, nWS.z) * triW.z
                );

                // Bold organic coastal rock shading: warm sunlit sandstone shelves + soft taupe shadow recesses
                float rockMacro = ValueNoise(pWS.xz * 0.18 + pWS.y * 0.25);
                half3 cliffCol = cliffTex * lerp(_CliffCrevice.rgb * 1.45, _CliffTint.rgb, 0.72) * lerp(0.88, 1.08, rockMacro);

                // Crisp full-height cliff mask: steep rock walls ALWAYS show full rock down to beach floor (y > 0.62) instead of fading to sand halfway!
                float slopeNoise = (ValueNoise(pWS.xz * 0.60) - 0.5) * 0.08 + (ValueNoise(pWS.xz * 1.8) - 0.5) * 0.03;
                float aboveBeach = smoothstep(0.56, 0.76, pWS.y);
                float aboveSilt  = smoothstep(0.42, 0.68, pWS.y);
                float sandSuppress = wSand * (1.0 - aboveBeach);
                float siltSuppress = wSilt * (1.0 - aboveSilt);
                float cliffMask = smoothstep(_SlopeStart, _SlopeEnd, nWS.y + slopeNoise) * (1.0 - sandSuppress * 0.96) * (1.0 - siltSuppress * 0.85);

                // Crisp Zelda/Story-of-Seasons overhanging grass rim + drop-shadow tucked just underneath the rim!
                float crest = saturate(IN.cliffEdge.x);
                float baseAO = saturate(IN.cliffEdge.y);

                // Deep vibrant emerald overhanging grass lip along cliff crest
                float grassLip = smoothstep(0.06, 0.36, cliffMask) * (1.0 - smoothstep(0.36, 0.68, cliffMask)) * saturate(0.35 + crest * 1.15);
                // Crisp subtle dark shadow tucked directly underneath the overhanging grass lip onto the rock face!
                float underLipShadow = smoothstep(0.45, 0.78, cliffMask) * (1.0 - smoothstep(0.78, 0.98, cliffMask)) * saturate(0.30 + crest * 1.25);

                half3 cliffShaded = cliffCol * lerp(1.0, 0.72, underLipShadow * 0.85);
                half3 cliffWithLip = lerp(cliffShaded, _CliffMossTint.rgb * lerp(0.88, 1.12, macroPatch), grassLip * 0.82);

                half3 albedo = lerp(groundAlbedo, cliffWithLip, cliffMask);
                // Grounding contact AO at bottom seam where every cliff meets beach sand or grass terrace!
                albedo *= lerp(1.0, 0.80, baseAO * 0.75);

                float3 finalN = normalize(lerp(nWS, nTri, cliffMask));

                // Bright Tropical Daylight Lighting with Crisp Sculptural Rock Contrast
                float4 shadowCoord = TransformWorldToShadowCoord(pWS);
                Light mainLight = GetMainLight(shadowCoord);
                float rawNdl = dot(finalN, mainLight.direction);
                // Slightly crisper lighting wrap on cliffs so sculpted rock facets pop cleanly from 46.5 deg!
                float ndlGrass = saturate(rawNdl * 0.78 + 0.22);
                float ndlCliff = saturate(rawNdl * 0.96 + 0.08);
                float ndl = lerp(ndlGrass, ndlCliff, cliffMask);
                float shadow = lerp(0.56, 1.0, mainLight.shadowAttenuation);

                float3 viewDir = normalize(GetWorldSpaceViewDir(pWS));
                float3 halfVec = normalize(mainLight.direction + viewDir);
                float spec = pow(saturate(dot(finalN, halfVec)), 24.0) * (wetMask * wSand * 0.28 + _Smoothness * 0.16) * mainLight.shadowAttenuation;

                // Subtle cool sky fill in rock shadows for painterly color harmony
                half3 ambient = SampleSH(finalN) * lerp(0.84, 0.74, cliffMask);
                half3 finalCol = albedo * (mainLight.color * ndl * shadow + ambient) + mainLight.color * spec * 0.26;
                finalCol = MixFog(finalCol, IN.fogCoord);
                return half4(finalCol, 1.0);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
