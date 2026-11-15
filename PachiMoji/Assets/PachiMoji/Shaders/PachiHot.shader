// 激アツの帯の文字の塗り（焼けた金）。金属の映り込みではなく、字の高さで色を決める：
// 下は濃い金、上は黄、まん中に白く焼けた横の帯が1本。そこへ、斜めの光がときどき走り抜ける。
// 縁・厚みは同じ材質の色ちがい（赤〜橙）。_Lit を上げると面の向きで明暗が付く（厚みの面用）
Shader "PachiMoji/Hot"
{
    Properties
    {
        _Top ("Top", Color) = (1, 0.82, 0.1, 1)
        _Bot ("Bottom", Color) = (1, 0.6, 0, 1)
        _Band ("White Band (pos, width, amount, 0)", Vector) = (0.56, 0.085, 0.95, 0)
        _Y ("Y Range (min, max)", Vector) = (0, 1, 0, 0)
        _Lit ("Facet Shading", Float) = 0
        _Glow ("Glow", Float) = 1.15
        _Sheen ("Sheen", Float) = 0.9
        _T ("Time (0-10)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Top, _Bot, _Band, _Y;
                float _Lit, _Glow, _Sheen, _T;
            CBUFFER_END
            struct A { float4 pos : POSITION; float3 n : NORMAL; };
            struct V { float4 pos : SV_POSITION; float h : TEXCOORD0; float3 nWS : TEXCOORD1; float3 pWS : TEXCOORD2; };
            V vert(A i)
            {
                V o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.h = saturate((i.pos.y - _Y.x) / max(0.0001, _Y.y - _Y.x));
                o.nWS = TransformObjectToWorldNormal(i.n);
                o.pWS = TransformObjectToWorld(i.pos.xyz);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                float3 col = lerp(_Bot.rgb, _Top.rgb, smoothstep(0.05, 0.95, i.h));
                // 白く焼けた横の帯（上のふちは切れよく、下へはやわらかく）
                float d = (i.h - _Band.x) / _Band.y;
                float band = d > 0 ? exp(-d * d * 3.0) : exp(-d * d * 0.7);
                col = lerp(col, float3(1, 1, 0.92), band * _Band.z);
                // 斜めの光が走り抜ける（10 秒で 4 回。世界の位置で決めるので、となりの字へ順に渡っていく）
                float ph = frac((i.pWS.x * 0.16 + i.pWS.y * 0.07) - _T * 0.4);
                float sheen = exp(-pow((ph - 0.5) / 0.035, 2));
                col += float3(1, 0.95, 0.7) * sheen * _Sheen;
                // 面の向きで明暗（厚みの面）
                float ndl = saturate(dot(normalize(i.nWS), normalize(float3(-0.35, 0.75, -0.55))));
                col *= lerp(1, 0.5 + 0.75 * ndl, _Lit);
                return half4(col * _Glow, 1);
            }
            ENDHLSL
        }
    }
}
