Shader "Cavehunt/GroundFogVertexColor"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.58, 0.7, 0.74, 0.62)
        _PlayerRevealCenter ("Player Reveal Center", Vector) = (0, 0, 0, 0)
        _PlayerRevealRadius ("Player Reveal Radius", Float) = 3
        _EdgeSoftness ("Edge Softness", Float) = 4
        _NoiseStrength ("Noise Strength", Float) = 0.18
        _NoiseScale ("Noise Scale", Float) = 0.08
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            Name "GroundFogReveal"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor;
                float4 _PlayerRevealCenter;
                float _PlayerRevealRadius;
                float _EdgeSoftness;
                float _NoiseStrength;
                float _NoiseScale;
            CBUFFER_END

            int _RevealCount;
            float4 _RevealCenters[128];

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half HashNoise(float2 value)
            {
                float noise = frac(sin(dot(value, float2(12.9898, 78.233)) * _NoiseScale) * 43758.5453);
                return lerp(0.72h, 1.12h, (half)noise);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 position = input.positionWS.xz;
                half reveal = 0;

                if (_PlayerRevealRadius > 0.0)
                {
                    float playerDistance = distance(position, _PlayerRevealCenter.xz);
                    reveal = max(reveal, (half)(1.0 - smoothstep(_PlayerRevealRadius, _PlayerRevealRadius + _EdgeSoftness, playerDistance)));
                }

                [loop]
                for (int i = 0; i < _RevealCount; i++)
                {
                    float4 center = _RevealCenters[i];
                    float distanceToCenter = distance(position, center.xz);
                    half amount = (half)(1.0 - smoothstep(center.w, center.w + _EdgeSoftness, distanceToCenter));
                    reveal = max(reveal, amount);
                }

                half noise = lerp(1.0h, HashNoise(position), (half)_NoiseStrength);
                half4 color = _FogColor;
                color.a *= noise * (1.0h - reveal);
                return color;
            }
            ENDHLSL
        }
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _FogColor;
            float4 _PlayerRevealCenter;
            float _PlayerRevealRadius;
            float _EdgeSoftness;
            float _NoiseStrength;
            float _NoiseScale;
            int _RevealCount;
            float4 _RevealCenters[128];

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed HashNoise(float2 value)
            {
                float noise = frac(sin(dot(value, float2(12.9898, 78.233)) * _NoiseScale) * 43758.5453);
                return lerp(0.72, 1.12, noise);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 position = i.worldPos.xz;
                fixed reveal = 0;

                if (_PlayerRevealRadius > 0.0)
                {
                    float playerDistance = distance(position, _PlayerRevealCenter.xz);
                    reveal = max(reveal, 1.0 - smoothstep(_PlayerRevealRadius, _PlayerRevealRadius + _EdgeSoftness, playerDistance));
                }

                for (int index = 0; index < _RevealCount; index++)
                {
                    float4 center = _RevealCenters[index];
                    float distanceToCenter = distance(position, center.xz);
                    fixed amount = 1.0 - smoothstep(center.w, center.w + _EdgeSoftness, distanceToCenter);
                    reveal = max(reveal, amount);
                }

                fixed noise = lerp(1.0, HashNoise(position), _NoiseStrength);
                fixed4 color = _FogColor;
                color.a *= noise * (1.0 - reveal);
                return color;
            }
            ENDCG
        }
    }
}
