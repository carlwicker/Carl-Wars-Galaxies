Shader "Custom/PlayerOcclusionOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0.9, 0.05, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.02
        _EdgeThreshold ("Edge Threshold", Range(0.05, 0.8)) = 0.3
        _GlowStrength ("Glow Strength", Range(0, 3)) = 1.5
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
            Name "OccludedOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZTest Always
            ZWrite Off
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float _EdgeThreshold;
                float _GlowStrength;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 expandedPositionOS = input.positionOS + input.normalOS * _OutlineWidth;
                output.positionWS = TransformObjectToWorld(expandedPositionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 viewDirection = normalize(GetWorldSpaceViewDir(input.positionWS));
                float edge = abs(dot(normalize(input.normalWS), viewDirection));
                half edgeStrength = 1.0 - smoothstep(_EdgeThreshold * 0.6, _EdgeThreshold, edge);
                clip(edgeStrength - 0.01);
                return half4(_OutlineColor.rgb * _GlowStrength, _OutlineColor.a * edgeStrength);
            }
            ENDHLSL
        }
    }
}
