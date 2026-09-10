// Puruzsuz arazi icin uc eksenli (triplanar) dokuma.
//
// NEDEN GEREKLI: Surface Nets ile uretilen yuzeyin dogal UV'si yok - koseler
// yogunluk alaninin kestigi yerde olusuyor, bir izgaraya oturmuyorlar. Kup
// mesher'inda her yuz duz bir dortgen oldugu icin UV atamak kolaydi; burada
// degil. Uc eksenli dokuma, dokuyu dunya konumundan uc yonden yansitip normale
// gore harmanliyor ve UV'ye hic ihtiyac duymuyor.
//
// Doku dizisi (Texture2DArray) yerine MEVCUT ATLAS kullaniliyor: atlas zaten
// uretiliyor ve tile indeksi dogrudan BlockId degerine esit. Ikinci bir varlik
// hattı kurmamak icin karo icine frac ile giriliyor.
//
// frac() karo sinirinda turevleri patlatiyor ve mipmap secimi bozuluyor
// (sinirlarda bulanik seritler). Bu yuzden ornekleme acik turevle (GRAD)
// yapiliyor: turevler frac ONCESI koordinattan aliniyor.

Shader "LastLight/TerrainTriplanar"
{
    Properties
    {
        _Atlas       ("Atlas (albedo)", 2D) = "white" {}
        _NormalAtlas ("Atlas (normal)", 2D) = "bump" {}
        _Tiling      ("Dunya olcegi", Float) = 0.35
        _Smoothness  ("Puruzsuzluk", Range(0,1)) = 0.08
        _NormalScale ("Normal siddeti", Range(0,2)) = 1.0
        _BlendSharp  ("Eksen harmanlama keskinligi", Range(1,16)) = 6.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _Tiling;
            float _Smoothness;
            float _NormalScale;
            float _BlendSharp;
        CBUFFER_END

        TEXTURE2D(_Atlas);       SAMPLER(sampler_Atlas);
        TEXTURE2D(_NormalAtlas); SAMPLER(sampler_NormalAtlas);

        #define ATLAS_TILES 4.0
        #define ATLAS_PAD   0.004

        // Sonsuz duzlem koordinatini atlasin tek bir karosuna hapseder.
        float2 TileUV(float2 uv, float tile)
        {
            float cell = 1.0 / ATLAS_TILES;
            float2 origin = float2(fmod(tile, ATLAS_TILES), floor(tile / ATLAS_TILES)) * cell;
            float2 f = frac(uv) * (cell - 2.0 * ATLAS_PAD) + ATLAS_PAD;
            return origin + f;
        }

        // Turevler frac oncesi koordinattan geliyor; aksi halde karo
        // sinirinda mipmap en dusuk seviyeye atliyor ve bulanik serit cikiyor.
        float4 SampleTile(TEXTURE2D_PARAM(tex, smp), float2 uv, float tile)
        {
            float cell = 1.0 / ATLAS_TILES;
            float2 dx = ddx(uv) * cell;
            float2 dy = ddy(uv) * cell;
            return SAMPLE_TEXTURE2D_GRAD(tex, smp, TileUV(uv, tile), dx, dy);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;   // uv.x = malzeme (BlockId) indeksi
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                // nointerpolation SART: malzeme bir INDEKS, sayisal bir
                // buyukluk degil. Interpolasyon acikken Tas(2) ile Kar(9)
                // koseleri arasindaki pikseller 3, 4, 5 degerlerine dusuyor
                // ve ekranda Odun/Metal/Beton karolari olarak kirmizi-beyaz
                // zikzak seritler cikiyordu. Duz aktarimla her ucgen tek bir
                // malzeme kullaniyor.
                nointerpolation float material : TEXCOORD2;
                float  fogCoord   : TEXCOORD3;
                float4 shadowCoord: TEXCOORD4;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);

                OUT.positionWS = posWS;
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.material   = IN.uv.x;
                OUT.fogCoord   = ComputeFogFactor(OUT.positionCS.z);
                OUT.shadowCoord = TransformWorldToShadowCoord(posWS);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float3 n = normalize(IN.normalWS);

                // Eksen agirliklari: yuzey hangi eksene bakiyorsa o yansitma
                // baskin. Us alma gecisi daraltiyor; dusuk usta uc doku birden
                // gorunup bulanik bir karisim olusuyor.
                float3 blend = pow(abs(n), _BlendSharp);
                blend /= max(dot(blend, float3(1, 1, 1)), 1e-4);

                float tile = round(IN.material);

                float2 uvX = IN.positionWS.zy * _Tiling;
                float2 uvY = IN.positionWS.xz * _Tiling;
                float2 uvZ = IN.positionWS.xy * _Tiling;

                half4 cx = SampleTile(TEXTURE2D_ARGS(_Atlas, sampler_Atlas), uvX, tile);
                half4 cy = SampleTile(TEXTURE2D_ARGS(_Atlas, sampler_Atlas), uvY, tile);
                half4 cz = SampleTile(TEXTURE2D_ARGS(_Atlas, sampler_Atlas), uvZ, tile);
                half3 albedo = cx.rgb * blend.x + cy.rgb * blend.y + cz.rgb * blend.z;

                // Normal harmanlama: her yansitmanin teget normali kendi
                // eksenine gore cevriliyor, sonra geometrik normalle
                // toplaniyor ("whiteout" yontemi). Duz toplama yuzeyi
                // duzlestiriyordu.
                half3 nx = UnpackNormalScale(SampleTile(TEXTURE2D_ARGS(_NormalAtlas, sampler_NormalAtlas), uvX, tile), _NormalScale);
                half3 ny = UnpackNormalScale(SampleTile(TEXTURE2D_ARGS(_NormalAtlas, sampler_NormalAtlas), uvY, tile), _NormalScale);
                half3 nz = UnpackNormalScale(SampleTile(TEXTURE2D_ARGS(_NormalAtlas, sampler_NormalAtlas), uvZ, tile), _NormalScale);

                half3 wx = half3(nx.z, nx.y, nx.x);
                half3 wy = half3(ny.x, ny.z, ny.y);
                half3 wz = half3(nz.x, nz.y, nz.z);

                float3 normalWS = normalize(
                    (n + wx) * blend.x + (n + wy) * blend.y + (n + wz) * blend.z);

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.shadowCoord = IN.shadowCoord;
                inputData.fogCoord = IN.fogCoord;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.metallic = 0;
                surfaceData.smoothness = _Smoothness;
                surfaceData.occlusion = 1;
                surfaceData.alpha = 1;
                surfaceData.normalTS = half3(0, 0, 1);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, IN.fogCoord);
                return color;
            }
            ENDHLSL
        }

        // Golge ve derinlik gecisleri olmadan arazi golge dusurmuyor ve
        // SSAO onu gormuyor.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct SAttr { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct SVary { float4 positionCS : SV_POSITION; };

            SVary ShadowVert(SAttr IN)
            {
                SVary OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nrmWS = TransformObjectToWorldNormal(IN.normalOS);
                float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, nrmWS, _LightDirection));
            #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
            #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                OUT.positionCS = cs;
                return OUT;
            }

            half4 ShadowFrag(SVary IN) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct DAttr { float4 positionOS : POSITION; };
            struct DVary { float4 positionCS : SV_POSITION; };

            DVary DepthVert(DAttr IN)
            {
                DVary OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 DepthFrag(DVary IN) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex DNVert
            #pragma fragment DNFrag

            struct DNAttr { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct DNVary { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            DNVary DNVert(DNAttr IN)
            {
                DNVary OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 DNFrag(DNVary IN) : SV_Target
            {
                return half4(normalize(IN.normalWS) * 0.5 + 0.5, 0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
