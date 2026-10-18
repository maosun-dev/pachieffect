// 画面全体の閃光・暗転。カメラの目の前に置いた板に塗る（いちばん最後に重ねる）
Shader "PachiMoji/Flash"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END
            float4 vert(float4 pos : POSITION) : SV_POSITION { return TransformObjectToHClip(pos.xyz); }
            half4 frag() : SV_Target { return _Color; }
            ENDHLSL
        }
    }
}
