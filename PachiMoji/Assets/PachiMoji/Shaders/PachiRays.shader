// 光の放射：画面の中心から吹き出す、虹色がかった細い光の筋。カメラの目の前の板に足し算で重ねる
// （チラ見せの超アップでは文字が画面いっぱいで背景が映らないので、手前に重ねて「文字の裏から光が漏れる」ように見せる）
// あわせて、切り裂き（画面を斜めに横切る紫の光線）と、プチュン（閃光 → 中心の光の点のまわりに電気。色は _PTint・_PRainbow）もここで描く
Shader "PachiMoji/Rays"
{
    Properties
    {
        _Rays ("Strength", Float) = 0
        _RayReach ("Reach", Float) = 2
        _T ("Time", Float) = 0
        _Slash ("Slash Strength", Float) = 0
        _SlashProg ("Slash Progress", Float) = 0
        _PFlash ("Puchun Flash", Float) = 0
        _PDot ("Puchun Dot", Float) = 0
        _PArc ("Puchun Arcs", Float) = 0
        _PScale ("Puchun Flare Scale", Float) = 1
        _PTint ("Puchun Tint", Color) = (0.55, 0.75, 1, 1)
        _PRainbow ("Puchun Rainbow", Float) = 0
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
                float _Rays, _RayReach, _T, _Slash, _SlashProg, _PFlash, _PDot, _PArc, _PScale, _PRainbow;
                float4 _PTint;
            CBUFFER_END
            float4 vert(float4 pos : POSITION) : SV_POSITION { return TransformObjectToHClip(pos.xyz); }

            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), u.x), lerp(hash21(i + float2(0, 1)), hash21(i + float2(1, 1)), u.x), u.y);
            }
            // 折れ線のノイズ（稲妻のギザギザ用）
            float lnoise(float x, float seed)
            {
                float i = floor(x), f = frac(x);
                return lerp(hash21(float2(i, seed)), hash21(float2(i + 1, seed)), f) - 0.5;
            }
            float zigzag(float x, float seed) { return lnoise(x, seed) * 0.6 + lnoise(x * 2.7, seed + 9.1) * 0.3 + lnoise(x * 7.3, seed + 3.7) * 0.12; }

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
                // プチュン
                if (_PFlash + _PDot + _PArc > 0)
                {
                    // 光の色（設定で選ぶ）。虹は中心からの角度と時刻で色相が回る
                    float3 ice = _PRainbow > 0.5 ? hsv2rgb(frac(a + _T * 0.5), 0.6, 1) : _PTint.rgb;
                    // 閃光：画面いっぱい（光の色を白に寄せたもの）＋真ん中に横長の光の筋
                    c += (lerp(ice, float3(1, 1, 1), 0.45) * (0.9 - 0.35 * saturate(r)) + float3(1, 1, 1) * exp(-abs(p.y) * 22) * 1.5) * _PFlash;
                    // レンズフレア（参考：プチュンEFの作り方）。_PScale で全体の大きさ
                    float sc = max(_PScale, 0.05);
                    float2 q = p / sc; float rq = length(q);
                    // 芯とにじみ
                    float core = exp(-rq * rq * 900) * 5 + exp(-rq * 14) * 1.2 + exp(-rq * 4) * 0.25;
                    // 十字：横に長い筋＋縦の短い筋。外へ少しぼかす（細い芯＋太いぼかし）
                    float hx = exp(-abs(q.y) * 380) * exp(-abs(q.x) * 1.6) * 2.6 + exp(-abs(q.y) * 60) * exp(-abs(q.x) * 3.5) * 0.45;
                    float vy = exp(-abs(q.x) * 420) * exp(-abs(q.y) * 4.0) * 1.8 + exp(-abs(q.x) * 70) * exp(-abs(q.y) * 7) * 0.3;
                    // 光の輪（ハロー）：赤は少し外、青は少し内にずらして虹色のにじみ
                    float3 halo = float3(exp(-pow((rq - 0.205) / 0.022, 2)), exp(-pow((rq - 0.195) / 0.022, 2)), exp(-pow((rq - 0.185) / 0.022, 2))) * 0.2
                                + float3(exp(-pow((rq - 0.42) / 0.06, 2)), exp(-pow((rq - 0.40) / 0.06, 2)), exp(-pow((rq - 0.38) / 0.06, 2))) * 0.07;
                    // ゴースト：中心を通る斜めの線上に並ぶ、うすい円
                    float2 gd = normalize(float2(1, -0.55));
                    float ghost = 0;
                    ghost += smoothstep(0.05, 0.035, length(q - gd * 0.55)) * 0.25;
                    ghost += smoothstep(0.03, 0.018, length(q + gd * 0.35)) * 0.3;
                    ghost += smoothstep(0.09, 0.07, length(q + gd * 0.8)) * 0.12;
                    float3 tint = lerp(ice, float3(1, 1, 1), 0.35);
                    c += (float3(1, 1, 1) * core + tint * (hx + vy) + halo * float3(1.0, 0.9, 1.1) + ice * ghost) * _PDot;

                    // 青い電気：中心の近くから、主に左右へ走るギザギザの線。形はすぐ変わる
                    float arcs = 0;
                    float tb = floor(_T * 18);
                    float pa = atan2(p.y, p.x);
                    [unroll] for (int k = 0; k < 6; k++)
                    {
                        float seed = k * 7.31 + tb * 1.93;
                        float side = hash21(float2(seed, 2.2)) < 0.5 ? 0 : 3.14159265;
                        float ang = side + (hash21(float2(seed, 5.5)) - 0.5) * 0.9;
                        float len = lerp(0.18, 0.55, hash21(float2(seed, 8.1)));
                        float on = step(0.3, hash21(float2(seed, 4.4)));
                        float da = pa - ang; da = da - 6.2831853 * round(da / 6.2831853);
                        float jag = zigzag(r * 14, seed) * 0.9;
                        float dist = abs((da - jag) * r);
                        float mask = smoothstep(0.02, 0.06, r) * (1 - smoothstep(len * 0.6, len, r));
                        arcs += on * mask * (exp(-dist * 900) * 2.2 + exp(-dist * 110) * 0.5);
                    }
                    c += ice * arcs * _PArc;
                }
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
