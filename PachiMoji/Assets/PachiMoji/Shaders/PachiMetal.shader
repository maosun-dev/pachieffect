// パチンコ文字の金属。業界の金文字の作り方（白黒の濃淡 → トーンカーブで金にする）を立体でやる。
// 1) 面の向きから「スタジオの映り込み」の明るさを出す（明るい空／暗い水平線／茶色い地面＋縦の光の帯）
// 2) その明るさを、波打つ色の対応表（_Ramp）に通して金・銀・黒などにする
// 3) 一番明るいところは 1 を超える明るさにして、光のにじみ（ブルーム）を起こす
// 4) 格上げ：もう1組の色（_RampB／_RainbowB）を持ち、文字の左から _WipeX まで塗り替える
// 5) 昇格の瞬間：_White で全体を白く飛ばす。_HueShift で虹の色を流す
Shader "PachiMoji/Metal"
{
    Properties
    {
        _Ramp ("Ramp", 2D) = "white" {}
        _Rainbow ("Rainbow (0/1)", Float) = 0
        _HueScale ("Hue Scale", Float) = 0.35
        _Boost ("Highlight Boost", Float) = 3
        _Horizon ("Horizon", Float) = -0.02
        _BandWidth ("Dark Band Width", Float) = 0.025
        _Sweep ("Sweep", Float) = 0.0
        _Diffuse ("Facet Diffuse", Float) = 0.2
        _Brightness ("Brightness", Float) = 1
        _BackFade ("Back Fade", Float) = 0.3
        _RampB ("Ramp B", 2D) = "white" {}
        _RainbowB ("Rainbow B (0/1)", Float) = 0
        _WipeX ("Wipe X (object)", Float) = -100000
        _WipeGlow ("Wipe Glow", Float) = 0
        _White ("White Hot", Float) = 0
        _HueShift ("Hue Shift", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "Unlit"
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Ramp); SAMPLER(sampler_Ramp);
            TEXTURE2D(_RampB); SAMPLER(sampler_RampB);
            CBUFFER_START(UnityPerMaterial)
                float4 _Ramp_ST;
                float _Rainbow, _HueScale, _Boost, _Horizon, _BandWidth, _Sweep, _Diffuse, _Brightness, _BackFade;
                float4 _RampB_ST;
                float _RainbowB, _WipeX, _WipeGlow, _White, _HueShift;
            CBUFFER_END

            struct A { float4 pos : POSITION; float3 nrm : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float3 wn : TEXCOORD0; float3 wp : TEXCOORD1; float2 uv : TEXCOORD2; float ox : TEXCOORD3; };

            V vert(A i)
            {
                V o;
                o.wp = TransformObjectToWorld(i.pos.xyz);
                o.pos = TransformWorldToHClip(o.wp);
                o.wn = TransformObjectToWorldNormal(i.nrm);
                o.uv = i.uv;
                o.ox = i.pos.x;
                return o;
            }

            float3 hsv2rgb(float h, float s, float v)
            {
                float3 k = saturate(abs(frac(h + float3(0, 2.0/3.0, 1.0/3.0)) * 6 - 3) - 1);
                return v * lerp(1, k, s);
            }

            // スタジオの映り込み：明るさ 0〜1。r はカメラから見た向き（y が上）
            float Env(float3 r)
            {
                float y = r.y - _Horizon;
                // 平らな表の面でも、画面の上側は空・下側は地面が映る → 確定の「上が金・下が焦げ茶」の割れ
                float sky = lerp(0.60, 0.86, saturate(y * 5));
                float band = 0.03;
                float ground = lerp(0.21, 0.13, saturate((-y - _BandWidth) * 3));
                // 地面側にも、床の照り返しの細い明るい線を1本
                ground += 0.22 * exp(-pow((y + 0.12) / 0.018, 2));
                float e = y > 0 ? sky : (y > -_BandWidth ? band : ground);
                // 縦長のソフトボックス（光の帯）を2本。_Sweep で横に流す
                float x = r.x + _Sweep;
                e += 0.30 * smoothstep(0.06, 0.0, abs(x - 0.42)) * step(0, y);
                e += 0.22 * smoothstep(0.04, 0.0, abs(x + 0.5)) * step(0, y);
                return e;
            }

            half4 frag(V i) : SV_Target
            {
                float3 n = normalize(i.wn);
                float3 v = normalize(GetWorldSpaceViewDir(i.wp));
                float3 r = reflect(-v, n);
                // 水平線がいつも文字の中心を通るように、「文字の中心で表の面に映る向き」を基準にする
                float3 oc = UNITY_MATRIX_M._m03_m13_m23;
                float3 nf = normalize(TransformObjectToWorldDir(float3(0, 0, -1)));
                float3 rc = reflect(-normalize(_WorldSpaceCameraPos - oc), nf);
                float e = Env(r - float3(rc.x, rc.y, 0));
                // 面ごとの明るさの差を少し足して、カクカクを強める
                float lam = saturate(dot(n, normalize(float3(-0.45, 0.75, -0.5))));
                float L = saturate(e * (1 - _Diffuse) + lam * _Diffuse);

                // 格上げの塗り替え：左から _WipeX までは B の色
                bool useB = i.ox < _WipeX;
                float3 c;
                if ((useB ? _RainbowB : _Rainbow) > 0.5)
                {
                    float hue = frac(i.wp.x * _HueScale + i.wp.y * 0.08 + _HueShift);
                    float3 base = hsv2rgb(hue, 0.9, 1);
                    // 虹も金属：暗いところは沈め、明るいところは白く飛ばす
                    c = base * (0.16 + 1.05 * pow(L, 1.15)) + pow(L, 9) * 1.2;
                }
                else
                {
                    c = useB ? SAMPLE_TEXTURE2D(_RampB, sampler_RampB, float2(L, 0.5)).rgb
                             : SAMPLE_TEXTURE2D(_Ramp, sampler_Ramp, float2(L, 0.5)).rgb;
                }
                // 白く飛ぶ：色を抜いて、明暗の形だけ残した白熱の白にする
                c = lerp(c, float3(1.0, 0.97, 0.9) * (0.55 + 1.3 * L), _White);
                // 塗り替えの境目に光の帯
                c += float3(1.0, 0.95, 0.8) * exp(-abs(i.ox - _WipeX) * 18) * _WipeGlow;
                c *= _Brightness;
                // 側面は奥へ行くほど暗く沈める（塊の重さが出る）
                c *= lerp(1, _BackFade, i.uv.y);
                // 一番明るいところだけ 1 を超えさせる（ブルームで光る）
                c *= 1 + _Boost * pow(L, 12);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
