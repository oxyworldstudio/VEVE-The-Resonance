Shader "VEVE/RayTracedAO_GI"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
        _AORadius ("AO Radius", Range(0.01,2)) = 0.5
        _AOIntensity ("AO Intensity", Range(0,2)) = 1.0
        _GIIntensity ("GI Intensity", Range(0,2)) = 1.0
        _GISamples ("GI Samples", Range(1,64)) = 8
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0,2)) = 1.0
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
            #pragma multi_compile _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _RAYTRACING_INDIRECT
            #pragma prefer_hlslcc gles
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _BaseMap_ST;
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Metallic;
                float _Smoothness;
                float _AORadius;
                float _AOIntensity;
                float _GIIntensity;
                float _GISamples;
                float _NormalStrength;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posn = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posn.positionCS;
                output.positionWS = posn.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float3 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb * _BaseColor.rgb;
                float3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv));
                normalTS.xy *= _NormalStrength;
                float3 normalWS = normalize(mul(normalTS, float3x3(
                    normalize(cross(float3(0,1,0), float3(1,0,0))),
                    float3(0,1,0),
                    float3(1,0,0)
                )));

                float ao = 1.0;
                float gi = 0.0;

                #if defined(_RAYTRACING_INDIRECT)
                    ao = 0.0;
                    gi = 0.0;
                    // Ray traced AO and GI values are injected by the pipeline when available
                #else
                    ao = 1.0;
                    gi = 0.0;
                #endif

                float3 ambient = float3(0.1, 0.1, 0.15) * (1.0 + gi * _GIIntensity);
                float3 finalColor = baseColor * ambient * lerp(1.0, ao, _AOIntensity);
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
