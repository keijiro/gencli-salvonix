Shader "Salvonix/EndCap"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Brightness("Brightness", Float) = 1
        _Glow("Glow", Float) = 6
    }

HLSLINCLUDE

#include "SalvonixCommon.hlsl"

TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

CBUFFER_START(UnityPerMaterial)
float4 _MainTex_ST;
float _Brightness;
float _Glow;
CBUFFER_END

void Vert(float4 position : POSITION,
          float2 texCoord : TEXCOORD0,
          out float4 outPosition : SV_Position,
          out float2 outTexCoord : TEXCOORD0,
          out float outFog : TEXCOORD1)
{
    outPosition = TransformObjectToHClip(position.xyz);
    outTexCoord = texCoord;
    outFog = ComputeFogFactor(outPosition.z);
}

float4 Frag(float4 position : SV_Position,
            float2 texCoord : TEXCOORD0,
            float fog : TEXCOORD1) : SV_Target
{
    half3 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, texCoord).rgb;
    col = col * _Brightness + max(col - 0.72, 0) * _Glow;
    col = MixFog(col, fog);
    return float4(col, 1);
}

ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "EndCap"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            ENDHLSL
        }
    }
}
