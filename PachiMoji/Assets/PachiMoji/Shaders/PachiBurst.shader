// 背景：中心から爆発する光（白 → 黄 → 橙 → 赤 → 黒）と放射状の光の筋
Shader "PachiMoji/Burst"
{
    Properties
    {
        _C0 ("Center", Color) = (1, 0.98, 0.85, 1)
        _C1 ("Inner", Color) = (1, 0.75, 0.15, 1)
        _C2 ("Mid", Color) = (0.85, 0.22, 0.01, 1)
        _C3 ("Outer", Color) = (0.18, 0.01, 0.0, 1)
        _Center ("Center UV", Vector) = (0.5, 0.5, 0, 0)
        _Rays ("Ray Strength", Float) = 0.45
        _Glow ("Center Glow", Float) = 1.2
        _Seed ("Seed", Float) = 1
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
                float4 _C0, _C1, _C2, _C3, _Center;
                float _Rays, _Glow, _Seed;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = i.uv; return o; }

            float hash(float x) { return frac(sin(x * 127.1 + _Seed * 31.7) * 43758.5453); }
            float rays(float a, float n)
            {
                float x = a / 6.2831853 * n;
                float i = floor(x), f = frac(x);
                float w = lerp(0.08, 0.45, hash(i));
                float on = step(0.45, hash(i + 17.0));
                return on * smoothstep(w, 0.0, abs(f - 0.5));
            }

            half4 frag(V i) : SV_Target
            {
                float2 p = (i.uv - _Center.xy) * float2(_ScreenParams.x / _ScreenParams.y, 1);
                float r = length(p);
                float a = atan2(p.y, p.x) + 3.14159265;
                float3 c = lerp(_C0.rgb, _C1.rgb, smoothstep(0.0, 0.18, r));
                c = lerp(c, _C2.rgb, smoothstep(0.15, 0.45, r));
                c = lerp(c, _C3.rgb, smoothstep(0.4, 0.95, r));
                float ry = rays(a, 90) * 0.7 + rays(a + 0.013, 37) * 0.6;
                c += ry * _Rays * _C1.rgb * smoothstep(1.2, 0.1, r);
                c *= 1 + _Glow * exp(-r * r * 30);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
