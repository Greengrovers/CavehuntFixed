Shader "Cavehunt/DifficultyCaveText"
{
    Properties
    {
        _MainTex ("Font Atlas", 2D) = "white" {}
        _CaveTex ("Cave Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Tiling ("Texture Tiling", Float) = 2.4
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON UNITY_SINGLE_PASS_STEREO

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_CaveTex);
            SAMPLER(sampler_CaveTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Tiling;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 font = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 cave = SAMPLE_TEXTURE2D(_CaveTex, sampler_CaveTex, input.uv * max(_Tiling, 0.01));
                half alpha = font.a * _Color.a;
                return half4(cave.rgb * _Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
