Shader "Cavehunt/BowHealthBar"
{
    Properties
    {
        _HealthFraction ("Health Fraction", Range(0, 1)) = 1
        _HealthColor ("Health Color", Color) = (1, 0, 0, 1)
        _MissingHealthColor ("Missing Health Color", Color) = (1, 1, 1, 1)
        _Axis ("Object Space Axis", Vector) = (0, 1, 0, 0)
        _AxisMin ("Axis Min", Float) = 0
        _AxisMax ("Axis Max", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "BowHealth"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _HealthColor;
                half4 _MissingHealthColor;
                float4 _Axis;
                float _AxisMin;
                float _AxisMax;
                float _HealthFraction;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float axisValue = dot(input.positionOS, normalize(_Axis.xyz));
                float axisRange = max(0.0001, _AxisMax - _AxisMin);
                float axisT = saturate((axisValue - _AxisMin) / axisRange);
                half isHealth = step(axisT, saturate(_HealthFraction));
                return lerp(_MissingHealthColor, _HealthColor, isHealth);
            }
            ENDHLSL
        }
    }
}
