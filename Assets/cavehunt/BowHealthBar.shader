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
        _SegmentCount ("Segment Count", Float) = 5
        _MissingSegmentCount ("Missing Segment Count", Float) = 0
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
            #pragma multi_compile_instancing
            #pragma multi_compile _ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON UNITY_SINGLE_PASS_STEREO

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _HealthColor;
                half4 _MissingHealthColor;
                float4 _Axis;
                float _AxisMin;
                float _AxisMax;
                float _HealthFraction;
                float _SegmentCount;
                float _MissingSegmentCount;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float axisLength = length(_Axis.xyz);
                float3 axis = axisLength > 0.0001 ? _Axis.xyz / axisLength : float3(0.0, 1.0, 0.0);
                float axisValue = dot(input.positionOS, axis);
                float axisRange = max(0.0001, _AxisMax - _AxisMin);
                float axisT = saturate((axisValue - _AxisMin) / axisRange);
                float segmentCount = max(1.0, _SegmentCount);
                float segmentIndex = min(floor(axisT * segmentCount), segmentCount - 1.0);
                float missingSegmentCount = clamp(ceil(_MissingSegmentCount), 0.0, segmentCount);
                half isMissing = segmentIndex >= segmentCount - missingSegmentCount ? 1.0 : 0.0;
                return lerp(_HealthColor, _MissingHealthColor, isMissing);
            }
            ENDHLSL
        }
    }
}
