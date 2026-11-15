// 激アツの帯の、写真が無いときの背景。黒に、暗い赤の熱がゆっくりうねる（帯より目立たせない）。
// 時刻は輪を描くずらしとして入れるので、10 秒でちょうど元に戻る
Shader "PachiMoji/ObiBack"
{
    Properties
    {
        _T ("Time (0-10)", Float) = 0
        _Aspect ("Aspect (w/h)", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _T, _Aspect;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = i.uv; return o; }
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }
            float fbm(float2 p)
            {
                float s = 0, a = 0.55;
                for (int k = 0; k < 4; k++) { s += a * noise(p); p = p * 2.03 + 11.7; a *= 0.5; }
                return s;
            }
            half4 frag(V i) : SV_Target
            {
                float w = _T * 0.6283185;   // 10 秒で 1 周
                float2 c = float2(cos(w), sin(w));
                float2 p = (i.uv - 0.5) * float2(_Aspect, 1) * 5.0;
                float n1 = fbm(p + c * 0.9);
                float n2 = fbm(p * 1.7 - c * 0.7 + 23.0);
                float g = pow(saturate(n1 * n2 * 2.3), 2.0);
                // 帯の通る左上と右下ほど、熱が少し見える
                float d = abs((i.uv.y - 0.5) + (i.uv.x - 0.5) * 0.35);
                float near = smoothstep(0.05, 0.5, d);
                float3 col = float3(0.07, 0.004, 0.0) * g * (0.2 + near) + float3(0.3, 0.06, 0.0) * g * g * near * 0.3;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
