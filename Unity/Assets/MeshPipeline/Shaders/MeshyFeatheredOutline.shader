Shader "MeshPipeline/MeshyEmissiveRim"
{
    Properties
    {
        _RimColor("Rim Color", Color) = (0.6, 0.1, 1, 1)
        _RimPower("Rim Power", Range(0.5, 8.0)) = 2.4
        _GlowStrength("Glow Strength", Range(0.0, 6.0)) = 1.35
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+30" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "EmissiveRim"
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend One One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _RimColor;
                float _RimPower;
                float _GlowStrength;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = normalize(input.viewDirWS);

                float rim = 1.0 - saturate(dot(n, v));
                float edge = pow(rim, _RimPower);

                half3 emissive = _RimColor.rgb * (_GlowStrength * edge);
                return half4(emissive, edge * _RimColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
