// 角のキラッ：十字の光＋細い斜めの光＋丸い芯。足し算で重ねる（HDR で光らせる）
Shader "PachiMoji/Glint"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.95, 0.8, 1)
        _Intensity ("Intensity", Float) = 6
        _Angle ("Angle", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
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
                float4 _Color;
                float _Intensity, _Angle;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = i.uv * 2 - 1; return o; }
            half4 frag(V i) : SV_Target
            {
                float s = sin(_Angle), c = cos(_Angle);
                float2 p = float2(c * i.uv.x - s * i.uv.y, s * i.uv.x + c * i.uv.y);
                float core = exp(-dot(p, p) * 60);
                float cross = exp(-abs(p.y) * 90) * exp(-abs(p.x) * 2.6) + exp(-abs(p.x) * 90) * exp(-abs(p.y) * 2.6);
                float2 q = float2(p.x + p.y, p.x - p.y) * 0.7071;
                float diag = (exp(-abs(q.y) * 110) * exp(-abs(q.x) * 6) + exp(-abs(q.x) * 110) * exp(-abs(q.y) * 6)) * 0.4;
                float edge = saturate(1 - length(i.uv));
                float v = (core * 1.5 + cross + diag) * edge;
                return half4(_Color.rgb * v * _Intensity, 1);
            }
            ENDHLSL
        }
    }
}
