Shader "VEVE/AdvancedPBR"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0,2)) = 1.0
        _AOMap ("AO Map", 2D) = "white" {}
        _AOScale ("AO Scale", Range(0,1)) = 1.0
        _HeightMap ("Height Map", 2D) = "gray" {}
        _HeightScale ("Height Scale", Range(0,0.2)) = 0.05f
        _ParallaxSteps ("Parallax Steps", Range(1,32)) = 8

        [Header(Subsurface Scattering)]
        _SSSEnabled ("SSS Enabled", Float) = 0
        _SSSDiffusion ("SSS Diffusion", Color) = (1,0.2,0.1,1)
        _SSSScale ("SSS Scale", Range(0.01,2)) = 1.0

        [Header(Clearcoat)]
        _Clearcoat ("Clearcoat", Range(0,1)) = 0.0
        _ClearcoatRoughness ("Clearcoat Roughness", Range(0,1)) = 0.0

        [Header(Anisotropy)]
        _Anisotropy ("Anisotropy", Range(0,1)) = 0.0
        _AnisotropyRotation ("Anisotropy Rotation", Range(0,1)) = 0.0

        [Header(Sheen)]
        _Sheen ("Sheen", Range(0,1)) = 0.0
        _SheenRoughness ("Sheen Roughness", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _PARALLAXMAP
            #pragma prefer_hlslcc gles
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 tangentWS : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                float4 screenPos : TEXCOORD5;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _BaseMap_ST;
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_AOMap);
            SAMPLER(sampler_AOMap);
            TEXTURE2D(_HeightMap);
            SAMPLER(sampler_HeightMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Metallic;
                float _Smoothness;
                float _NormalStrength;
                float _AOScale;
                float _HeightScale;
                float _ParallaxSteps;
                float _SSSEnabled;
                float3 _SSSDiffusion;
                float _SSSScale;
                float _Clearcoat;
                float _ClearcoatRoughness;
                float _Anisotropy;
                float _AnisotropyRotation;
                float _Sheen;
                float _SheenRoughness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posn = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posn.positionCS;
                output.positionWS = posn.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz);
                output.bitangentWS = cross(output.normalWS, output.tangentWS) * input.tangentOS.w;
                return output;
            }

            float2 ParallaxOffset(float2 uv, float3 viewDirTS)
            {
                float height = SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, uv).r;
                float2 offset = viewDirTS.xy * (height * _HeightScale);
                return uv - offset;
            }

            float3 CalculateNormal(Varyings input, float2 uv)
            {
                float3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv));
                normalTS.xy *= _NormalStrength;
                normalTS = normalize(normalTS);
                return normalize(mul(normalTS, float3x3(input.tangentWS, input.bitangentWS, input.normalWS)));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);
                float3 viewDirTS = normalize(mul(viewDirWS, float3x3(input.tangentWS, input.bitangentWS, input.normalWS)));

                #if defined(_PARALLAXMAP) && !defined(SHADER_API_MOBILE)
                    uv = ParallaxOffset(uv, viewDirTS);
                #endif

                float3 normalWS = CalculateNormal(input, uv);
                float3 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb * _BaseColor.rgb;
                float ao = SAMPLE_TEXTURE2D(_AOMap, sampler_AOMap, uv).r * _AOScale + (1.0 - _AOScale);

                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float3 halfVec = normalize(mainLight.direction + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfVec));
                float NdotV = saturate(dot(normalWS, viewDirWS));

                float3 F0 = lerp(float3(0.04f, 0.04f, 0.04f), baseColor, _Metallic);
                float3 F = F0 + (1.0 - F0) * pow(1.0 - NdotV, 5.0);
                float D = _Smoothness * _Smoothness;
                float3 spec = F * pow(NdotH, D * D * 100.0f) / (4.0 * NdotL * NdotV + 0.001f);

                float3 kD = (1.0 - F) * (1.0 - _Metallic);
                float3 diffuse = kD * baseColor / 3.14159f;

                float3 color = (diffuse + spec) * mainLight.color * NdotL * ao;
                color += baseColor * SampleSH(normalWS) * ao;

                #if _SSSEnabled
                    float3 sss = _SSSDiffusion * (1.0 - NdotL) * _SSSScale;
                    color += baseColor * sss * ao;
                #endif

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
