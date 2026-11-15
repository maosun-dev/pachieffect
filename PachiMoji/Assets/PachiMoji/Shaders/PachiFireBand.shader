// 激アツの帯の光と炎。帯に沿った細長い板に、半透明で重ねる（まぶしい筋と火の粉は足し算）。
// uv.x＝帯に沿った長さ（世界の単位）、uv.y＝帯の幅の向き（帯の半分の幅を 1 とする。帯のふちが ±1）。
// 模様は帯に沿って _Loop の長さでくり返し、文字と同じ速さで流れる。10 秒でちょうど元に戻る（書き出しのくり返しでつなぎ目が出ない）
Shader "PachiMoji/FireBand"
{
    Properties
    {
        _T ("Time (0-10)", Float) = 0
        _Loop ("Loop Length", Float) = 20
        _Speed ("Speed", Float) = 1.6
        _Dir ("Direction", Float) = 1
        _Cell ("Noise Cell", Float) = 0.55
        _Intensity ("Intensity", Float) = 1.35
        _HalfW ("Band Half Width (world)", Float) = 0.675
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Blend One OneMinusSrcAlpha   // 色に濃さを掛けてある半透明（＋まぶしい筋と火の粉は足し算）
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _T, _Loop, _Speed, _Dir, _Cell, _Intensity, _HalfW;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = i.uv; return o; }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float wrap(float x, float n) { return x - n * floor(x / n); }

            // 横（x）が n 区切りでくり返す値のノイズ
            float noise(float2 p, float n)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                float x0 = wrap(i.x, n), x1 = wrap(i.x + 1, n);
                float a = hash(float2(x0, i.y)), b = hash(float2(x1, i.y));
                float c = hash(float2(x0, i.y + 1)), d = hash(float2(x1, i.y + 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p, float n)
            {
                float s = 0, amp = 0.55;
                for (int k = 0; k < 4; k++) { s += amp * noise(p, n); p *= 2; n *= 2; amp *= 0.5; }
                return s;
            }

            half4 frag(V i) : SV_Target
            {
                float n = max(1, round(_Loop / _Cell));              // 1 周に入るノイズの区切りの数（整数＝くり返す）
                float y = i.uv.y, reach = abs(y) / 2.1;               // reach：炎の届く範囲（帯のふちの少し外まで）
                float u = i.uv.x - _Dir * _Speed * _T;                // 文字と同じ速さで流す
                // 炎の舌は、流れの後ろへなびく（外へ行くほど遅れる）
                float lean = _Dir * abs(y) * 0.55;
                float big = fbm(float2((u - lean) / _Loop * n, y * 0.7 + 3.1), n);
                float u2 = i.uv.x - _Dir * _Speed * 2 * _T;
                float n4 = max(1, round(n * 0.25));
                float streak = fbm(float2(u2 / _Loop * n4, y * 3.4 + 11.3), n4);        // 流れる向きに長い筋（スピード線）
                float fine = fbm(float2((u2 - lean * 1.6) / _Loop * n * 2, y * 1.7 + 7.7), n * 2);
                float flame = big * 0.55 + fine * 0.32 + streak * 0.33;
                // 熱さ：まん中ほど熱く、外へ行くほど炎の舌の先だけ残る
                float heat = flame * 1.6 - reach * 1.15 - 0.03;
                // 色：ふちは赤、すぐ橙〜黄。まん中は黄が主役で、筋のところだけ白く焼ける
                float3 red = float3(1.0, 0.1, 0.0), orange = float3(1.0, 0.42, 0.0), yellow = float3(1.0, 0.84, 0.12), white = float3(1.0, 0.98, 0.78);
                float3 col = lerp(red, orange, smoothstep(0.0, 0.13, heat));
                col = lerp(col, yellow, smoothstep(0.1, 0.34, heat));
                float hot = smoothstep(0.62, 0.8, streak) * smoothstep(0.3, 0.6, heat);
                col = lerp(col, white, hot);
                float a = smoothstep(0.0, 0.2, heat);
                float alpha = a * 0.84;
                float3 outc = col * alpha * _Intensity + white * hot * 0.9;

                // 火の粉：帯のふちから外へ流れ飛ぶ小さな光（帯に沿って区切り、区切りごとに上と下へ1つずつ）
                float nE = max(1, round(_Loop / 0.5));
                float cellLen = _Loop / nE;
                float cu = u / cellLen;
                float cell = wrap(floor(cu), nE);
                float side = y > 0 ? 1 : -1;
                float r1 = hash(float2(cell, side * 3.0 + 5.0)), r2 = hash(float2(cell + 17.0, side * 7.0 + 1.0)), r3 = hash(float2(cell + 41.0, side));
                float life = frac(_T * 0.1 * (2 + floor(r1 * 3)) + r2);          // 10 秒で 2〜4 回（整数回＝元に戻る）
                float py = 0.75 + life * (1.2 + r3 * 0.9);                       // ふちの内側から外へ
                float px = (r2 - 0.5) * 0.5;                                      // 区切りの中での横の位置
                float dx = (frac(cu) - 0.5 - px) * cellLen;
                float dy = (abs(y) - py) * _HalfW;
                float rad = 0.028 + r1 * 0.03;
                float spark = exp(-(dx * dx * 0.35 + dy * dy) / (rad * rad)) * (1 - life) * (1 - life) * smoothstep(0.0, 0.08, life);
                outc += lerp(float3(1.0, 0.45, 0.05), float3(1.0, 0.92, 0.5), r3) * spark * 2.6;

                return half4(outc, alpha);
            }
            ENDHLSL
        }
    }
}
