// 光の放射：画面の中心から吹き出す、虹色がかった細い光の筋。カメラの目の前の板に足し算で重ねる
// （チラ見せの超アップでは文字が画面いっぱいで背景が映らないので、手前に重ねて「文字の裏から光が漏れる」ように見せる）
// あわせて、切り裂き（画面を斜めに横切る紫の光線）もここで描く
Shader "PachiMoji/Rays"
{
    Properties
    {
        _Rays ("Strength", Float) = 0
        _RayReach ("Reach", Float) = 2
        _T ("Time", Float) = 0
        _Slash ("Slash Strength", Float) = 0
        _SlashProg ("Slash Progress", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" }
        Pass
        {
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Rays, _RayReach, _T, _Slash, _SlashProg;
            CBUFFER_END
            float4 vert(float4 pos : POSITION) : SV_POSITION { return TransformObjectToHClip(pos.xyz); }

            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), u.x), lerp(hash21(i + float2(0, 1)), hash21(i + float2(1, 1)), u.x), u.y);
            }
            float3 hsv2rgb(float h, float s, float v)
            {
                float3 k = saturate(abs(frac(h + float3(0, 2.0/3.0, 1.0/3.0)) * 6 - 3) - 1);
                return v * lerp(1, k, s);
            }

            half4 frag(float4 sp : SV_Position) : SV_Target
            {
                float2 uv = sp.xy / _ScreenParams.xy;
                float2 p = (uv - 0.5) * float2(_ScreenParams.x / _ScreenParams.y, 1);
                float r = length(p);
                float a = atan2(p.y, p.x) / 6.2831853 + 0.5;
                // 筋の並びはゆっくり回る
                float rn = vnoise(float2(a * 70 + _T * 1.5, 3.1)) * 0.65 + vnoise(float2(a * 190 - _T * 2.0, 8.7)) * 0.35;
                float ray = pow(saturate(rn * 1.3), 6) * 3 * smoothstep(0.0, 0.12, r) * (1 - smoothstep(_RayReach * 0.7, _RayReach, r));
                float3 prism = hsv2rgb(frac(a * 9 + r * 0.6), 0.5, 1);
                float3 c = lerp(float3(1.0, 0.95, 0.85), prism, 0.55) * ray;
                // 中心のにじみ
                c += float3(1.0, 0.9, 0.7) * exp(-r * 4) * 0.5;
                c *= _Rays;

                // 切り裂き：左下から右上へ少し傾いた光線。_SlashProg（0〜1）で左から伸びる
                if (_Slash > 0)
                {
                    float2 dirS = normalize(float2(1, 0.22));
                    float2 nS = float2(-dirS.y, dirS.x);
                    float along = dot(p, dirS), across = dot(p, nS);
                    float head = lerp(-1.2, 1.2, _SlashProg);
                    float m = smoothstep(head, head - 0.08, along);
                    // ゆらぐ太さ（ジジジと震える）
                    float wob = 1 + 0.25 * sin(along * 40 + _T * 60) * sin(_T * 37);
                    float d = abs(across) / wob;
                    float3 beam = float3(1, 0.9, 1) * exp(-d * 260) * 2.5
                                + float3(0.95, 0.15, 1.0) * exp(-d * 40) * 1.6
                                + float3(0.6, 0.05, 0.9) * exp(-d * 9) * 0.5;
                    c += beam * m * _Slash;
                }
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
