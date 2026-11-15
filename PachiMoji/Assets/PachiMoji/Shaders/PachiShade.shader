// 半透明の色の板（激アツの帯の地。写真を透かして暗くする）。手前のものの奥行きは守る
Shader "PachiMoji/Shade"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 0.5)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back   // 帯の板は表と裏の三角を両方持つので、片方だけ描く（2回重ねて濃くならないように）
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
