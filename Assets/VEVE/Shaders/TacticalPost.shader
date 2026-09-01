// VEVE/TacticalPost - built-in pipeline fullscreen post effect.
// Pure CGPROGRAM image effect (no SRP package includes): consumed by
// TacticalPostController via Graphics.Blit. Under URP/HDRP the controller
// self-disables (OnRenderImage never fires under scripted pipelines), so this
// shader is authored exclusively against the built-in pipeline contract.
Shader "VEVE/TacticalPost"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _VignetteIntensity ("Vignette Intensity", Range(0,1)) = 0.3
        _VignetteSmoothness ("Vignette Smoothness", Range(0.1,2)) = 1.0
        _GrainIntensity ("Film Grain Intensity", Range(0,1)) = 0.03
        _ChromaticAberration ("Chromatic Aberration", Range(0,1)) = 0.0
        _Exposure ("Exposure", Range(0.2,3)) = 1.0
        _Contrast ("Contrast", Range(0.5,2)) = 1.0
        _Saturation ("Saturation", Range(0,2)) = 1.0
        _TonemapSwitch ("Tonemap Switch (0 none, 1 ACES, 2 Reinhard)", Float) = 0.0
        _LensDirtStrength ("Lens Dirt Strength", Range(0,1)) = 0.0
        _LensDirtTex ("Lens Dirt", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _LensDirtTex;
            float _VignetteIntensity;
            float _VignetteSmoothness;
            float _GrainIntensity;
            float _ChromaticAberration;
            float _Exposure;
            float _Contrast;
            float _Saturation;
            float _TonemapSwitch;
            float _LensDirtStrength;

            // Dave Hoskins-style hash: deterministic, target-3.0 safe, animated
            // through the third coordinate fed with _Time.
            float Hash13(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            // Narkowicz ACES film approximation (fit, no LUT needed at target 3.0).
            float3 ACESFilmApprox(float3 x)
            {
                const float a = 2.51;
                const float b = 0.03;
                const float c = 2.43;
                const float d = 0.59;
                const float e = 0.14;
                return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
            }

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 fromCenter = i.uv - 0.5;
                float dist = length(fromCenter);

                // Radial chromatic aberration: R/B channels pushed along the
                // center vector; 1.0 maps to ~1.5% screen distance at corners.
                float2 caOffset = fromCenter * dist * (_ChromaticAberration * 0.015);
                float3 col;
                col.r = tex2D(_MainTex, i.uv + caOffset).r;
                col.g = tex2D(_MainTex, i.uv).g;
                col.b = tex2D(_MainTex, i.uv - caOffset).b;

                col *= _Exposure;

                if (_TonemapSwitch > 1.5)
                {
                    col = col / (1.0 + col); // Reinhard
                }
                else if (_TonemapSwitch > 0.5)
                {
                    col = ACESFilmApprox(col); // ACES approximation
                }

                // Color grading: contrast around 0.5 pivot, Rec709-luma saturation.
                col = (col - 0.5) * _Contrast + 0.5;
                float luma = dot(col, float3(0.2126, 0.7152, 0.0722));
                col = lerp(luma.xxx, col, _Saturation);

                // Optional lens dirt (additive; default "black" texture is a no-op).
                float3 dirt = tex2D(_LensDirtTex, i.uv).rgb;
                col += dirt * (_LensDirtStrength * 0.5);

                // Vignette: smoothness pulls the start radius toward the center.
                float smooth01 = saturate(_VignetteSmoothness * 0.5);
                float start = lerp(0.85, 0.25, smooth01);
                float vig = 1.0 - smoothstep(start, 0.95, dist);
                col *= lerp(1.0, vig, _VignetteIntensity);

                // Hash-based animated film grain (subtle, +/-0.125 at intensity 1).
                float grain = Hash13(float3(i.uv * _MainTex_TexelSize.zw, frac(_Time.y) * 61.7));
                col += (grain - 0.5) * (_GrainIntensity * 0.25);

                return fixed4(saturate(col), 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
