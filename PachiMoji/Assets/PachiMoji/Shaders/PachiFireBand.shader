// 激アツの帯の炎。帯に沿った細長い板に、足し算で重ねる（光のにじみで、まぶしく燃える）。
// uv.x＝帯に沿った長さ（世界の単位）、uv.y＝帯の幅の向き（-1〜1、まん中が 0）。
// 炎の模様は帯に沿って _Loop の長さでくり返し、文字と同じ速さで流れる。10 秒でちょうど元に戻る（書き出しのくり返しでつなぎ目が出ない）
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
        _Fade ("End Fade", Float) = 0
        _Len ("Arc Length", Float) = 20
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Blend One OneMinusSrcAlpha   // 色に濃さを掛けてある半透明（＋まぶしい筋は足し算）
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _T, _Loop, _Speed, _Dir, _Cell, _Intensity, _Fade, _Len;
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
                float u = i.uv.x - _Dir * _Speed * _T;                // 文字と同じ速さで流す
                float y = i.uv.y, reach = abs(y);
                // 炎のうねり（大きい）と、流れる向きに長く伸びた筋（スピード線）。どれも 10 秒で元に戻る
                float big = fbm(float2(u / _Loop * n, y * 1.4 + 3.1), n);
                float u2 = i.uv.x - _Dir * _Speed * 2 * _T;
                float n4 = max(1, round(n * 0.25));
                float streak = fbm(float2(u2 / _Loop * n4, y * 7.0 + 11.3), n4);        // 横に長い筋
                float fine = fbm(float2(u2 / _Loop * n * 2, y * 3.5 + 7.7), n * 2);
                float flame = big * 0.55 + fine * 0.3 + streak * 0.35;
                // 熱さ：まん中ほど熱く、外へ行くほど炎の舌の先だけ残る
                float heat = flame * 1.55 - reach * 1.1 - 0.02;
                // 色：外は赤、すぐ橙〜黄。まん中は黄が主役で、筋のところだけ白く焼ける
                float3 red = float3(1.0, 0.13, 0.0), orange = float3(1.0, 0.45, 0.0), yellow = float3(1.0, 0.84, 0.12), white = float3(1.0, 0.98, 0.78);
                float3 col = lerp(red, orange, smoothstep(0.0, 0.16, heat));
                col = lerp(col, yellow, smoothstep(0.12, 0.38, heat));
                float hot = smoothstep(0.62, 0.8, streak) * smoothstep(0.3, 0.6, heat);
                col = lerp(col, white, hot);
                float a = smoothstep(0.0, 0.22, heat);
                // 半透明で重ねる（明るい写真の上でも黄色い光として見える）。筋は足し算でまぶしく
                float alpha = a * 0.82;
                float3 outc = col * alpha * _Intensity + white * hot * 0.9;
                return half4(outc, alpha);
            }
            ENDHLSL
        }
    }
}
