// 背景：中心から吹き出す「炎のトンネル」。参考（遊技機系の演出チュートリアル）の作り方を1枚のシェーダーで再現する
// ・フラクタルノイズを極座標で放射状に引き延ばし、外へ流す（奥・中・手前の3層を違う速さで）
// ・左右対称、白く飛んだ中心、横長の光の線、金の火花のチカチカ、稲妻のバチバチ
// ・色は炎（黒→赤→橙→黄→白）か虹（中心からの距離で色相が回る）か青
// ・最後に RGB を少しずらして色ずれ
Shader "PachiMoji/Burst"
{
    Properties
    {
        _Mode ("Mode 0=Fire 1=Rainbow 2=Blue", Float) = 0
        _T ("Time", Float) = 0
        _Center ("Center UV", Vector) = (0.5, 0.5, 0, 0)
        _Bolts ("Lightning", Float) = 1
        _Sparks ("Sparks", Float) = 1
        _Core ("Core Glow", Float) = 1
        _Aberration ("Chromatic Aberration", Float) = 0.012
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Background" }
        Pass
        {
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Center;
                float _Mode, _T, _Bolts, _Sparks, _Core, _Aberration;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = i.uv; return o; }

            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), u.x), lerp(hash21(i + float2(0, 1)), hash21(i + float2(1, 1)), u.x), u.y);
            }
            float fbm(float2 p)
            {
                float s = 0, a = 0.5;
                for (int k = 0; k < 5; k++) { s += a * vnoise(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return s;
            }
            // 折れ線のノイズ（なめらかにせず直線でつなぐ → 稲妻のギザギザ）
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

            // 放射状の炎ノイズ。a は角度（左右対称にたたんだもの）、lr は log(半径)
            float flame(float a, float lr, float t, float speed, float freq)
            {
                // 角度方向に細かく、半径方向に長く → 中心から吹き出す筋になる
                float2 p = float2(a * freq * 1.6, lr * 1.1 - t * speed);
                float2 q = float2(fbm(p + float2(0, t * 0.2)), fbm(p + float2(5.2, 1.3)));   // ゆらぎ（炎の舌）
                return fbm(p + q * 0.7);
            }

            // 1点の明るさ（モノクロ）と色の材料を出す
            float3 scene(float2 p, float t)
            {
                float r = length(p);
                float a = abs(atan2(p.x, p.y)) / 3.14159265;        // 0..1（左右対称）
                float lr = log(max(r, 0.002));

                // 3層：奥はゆっくり大きく、手前は速く細かく
                float n1 = flame(a, lr, t, 0.35, 5);
                float n2 = flame(a + 0.37, lr, t, 0.9, 11);
                float n3 = flame(a + 0.71, lr, t, 1.8, 23);
                float n = n1 * 0.45 + n2 * 0.4 + n3 * 0.3;
                n = saturate((n - 0.40) * 3.6);                       // コントラストを上げる（黒くつぶれる所を作る）
                n *= smoothstep(0.02, 0.25, r);                       // 中心の少し外から吹き出す
                n *= lerp(1.0, 0.75, saturate(r - 0.7));              // 外ほど少し落とす

                float3 c;
                if (_Mode < 0.5)
                {
                    // 炎：黒 → 暗い赤 → 橙 → 黄 → 白
                    c = lerp(float3(0.01, 0.0, 0.0), float3(0.6, 0.05, 0.0), saturate(n * 2.2));
                    c = lerp(c, float3(1.0, 0.45, 0.03), saturate(n * 2.2 - 0.5));
                    c = lerp(c, float3(1.0, 0.86, 0.4), saturate(n * 2.2 - 1.1));
                    c += float3(1.0, 0.92, 0.7) * pow(n, 4) * 2.5;
                }
                else if (_Mode < 1.5)
                {
                    // 虹：中心からの距離で色相が回る。ノイズで形を抜く
                    float hue = frac(lr * 0.45 - t * 0.15 + n * 0.25);
                    c = hsv2rgb(hue, 0.95, 1) * (n * 1.4) + pow(n, 5) * 1.5;
                }
                else
                {
                    c = lerp(float3(0.0, 0.0, 0.03), float3(0.05, 0.15, 0.7), saturate(n * 2));
                    c = lerp(c, float3(0.4, 0.75, 1.0), saturate(n * 2 - 0.8));
                    c += float3(0.8, 0.9, 1.0) * pow(n, 6) * 2.5;
                }
                return c;
            }

            half4 frag(V i) : SV_Target
            {
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 p = (i.uv - _Center.xy) * float2(aspect, 1);
                float t = _T + _Time.y;
                float r = length(p);

                // 色ずれ：R・G・B を中心から少しずつずらした位置で取る
                float2 dir = p / max(r, 1e-4);
                float ab = _Aberration * r;
                float3 c;
                c.r = scene(p + dir * ab, t).r;
                c.g = scene(p, t).g;
                c.b = scene(p - dir * ab, t).b;

                // 白く飛んだ中心と、横長の光の線
                float core = exp(-r * r * 40) * 2.2 + exp(-r * 7) * 0.5;
                float streak = exp(-abs(p.y) * 120) * exp(-abs(p.x) * 1.8) * 1.6;
                float3 coreCol = _Mode > 1.5 ? float3(0.7, 0.85, 1.0) : float3(1.0, 0.9, 0.7);
                if (_Mode > 0.5 && _Mode < 1.5) core *= 0.35;   // 虹は中心を暗めに
                c += coreCol * (core + streak) * _Core;

                // 金の火花：放射状のマス目ごとに1つ。外へ流れ、チカチカ点滅する
                float a = atan2(p.y, p.x) / 6.2831853 + 0.5;
                float lr = log(max(r, 0.002));
                float2 g = float2(a * 90, lr * 9 - t * 4);
                float2 gi = floor(g), gf = frac(g);
                float h = hash21(gi);
                float2 sp = float2(hash21(gi + 3.1), hash21(gi + 7.7));
                float d = length((gf - sp) * float2(1, 0.6));
                float tw = step(0.82, h) * (0.5 + 0.5 * sin(t * 25 + h * 60));
                float spark = exp(-d * d * 160) * tw * smoothstep(0.03, 0.15, r);
                c += float3(1.0, 0.8, 0.35) * spark * 3 * _Sparks;

                // 稲妻：中心から外へ走るギザギザの線を数本（枝分かれ付き）。一定時間ごとに形が変わる
                float bolts = 0;
                float tb = floor(t * 8);
                float pa = atan2(p.y, p.x);
                [unroll] for (int k = 0; k < 7; k++)
                {
                    float seed = k * 13.7 + tb * 1.37;
                    float ang = hash21(float2(seed, 1.3)) * 6.2831853;
                    float len = lerp(0.6, 1.5, hash21(float2(seed, 7.9)));
                    float on = step(0.4, hash21(float2(seed, 4.2)));
                    float da = pa - ang; da = da - 6.2831853 * round(da / 6.2831853);
                    float jag = zigzag(r * 9, seed) * 0.55;
                    float dist = abs((da - jag) * r);
                    // 枝：途中から少し角度をずらしてもう1本
                    float jag2 = jag + zigzag(r * 12, seed + 3.3) * 0.9 * smoothstep(0.3, 0.6, r);
                    float dist2 = abs((da - jag2) * r);
                    float mask = smoothstep(0.08, 0.16, r) * (1 - smoothstep(len * 0.75, len, r));
                    float mask2 = mask * smoothstep(0.35, 0.5, r);
                    bolts += on * (mask * (exp(-dist * 700) * 2.4 + exp(-dist * 70) * 0.6) + mask2 * (exp(-dist2 * 900) * 1.4 + exp(-dist2 * 90) * 0.3));
                }
                float3 boltCol = _Mode > 1.5 ? float3(0.6, 0.8, 1.0) : float3(0.85, 0.8, 1.0);
                c += boltCol * bolts * _Bolts;

                // まわりを少し暗く
                c *= lerp(1.0, 0.6, saturate(r * 0.8 - 0.35));
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
