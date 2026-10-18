// 火花の粒：速さの向きに伸びた光の筋。先頭が白く、尾へ行くほど細く消える。足し算で重ねる（HDR で光らせる）
Shader "PachiMoji/Spark"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.85, 0.5, 1)
        _Intensity ("Intensity", Float) = 8
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = i.uv * 2 - 1; return o; }
            half4 frag(V i) : SV_Target
            {
                // x＝太さの向き、y＝伸びる向き（+1 が進む先）
                float along = saturate(1 - abs(i.uv.y));
                float head = saturate(i.uv.y * 0.5 + 0.5);
                float w = lerp(0.15, 0.7, head) + 1e-3;
                float body = exp(-pow(i.uv.x / w, 2) * 3) * along * lerp(0.25, 1, head);
                float core = exp(-pow(i.uv.x / (w * 0.35), 2)) * along * head * head;
                float3 c = _Color.rgb * body + float3(1, 1, 1) * core;
                return half4(c * _Intensity, 1);
            }
            ENDHLSL
        }
    }
}
