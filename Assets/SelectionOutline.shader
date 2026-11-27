Shader "Custom/SelectionOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0.9, 0.05, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.2)) = 0.06
        _BoundsCenter ("Bounds Center", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+10"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "SelectionOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZTest LEqual
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float4 _BoundsCenter;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 axisScale = float3(
                    length(TransformObjectToWorldDir(float3(1, 0, 0), false)),
                    length(TransformObjectToWorldDir(float3(0, 1, 0), false)),
                    length(TransformObjectToWorldDir(float3(0, 0, 1), false)));
                float3 expandedPositionOS = input.positionOS
                    + sign(input.positionOS - _BoundsCenter.xyz) * _OutlineWidth / max(axisScale, 0.0001);
                output.positionCS = TransformObjectToHClip(expandedPositionOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
