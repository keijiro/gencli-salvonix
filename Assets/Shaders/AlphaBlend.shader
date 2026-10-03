Shader "Salvonix/AlphaBlend"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Color("Color", Color) = (1, 1, 1, 1)
    }

HLSLINCLUDE

#include "SalvonixCommon.hlsl"

TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

CBUFFER_START(UnityPerMaterial)
float4 _MainTex_ST;
float4 _Color;
CBUFFER_END

void Vert(float4 position : POSITION,
          float2 texCoord : TEXCOORD0,
          float4 color : COLOR,
          out float4 outPosition : SV_Position,
          out float2 outTexCoord : TEXCOORD0,
          out float4 outColor : COLOR,
          out float outFog : TEXCOORD1)
{
    outPosition = TransformObjectToHClip(position.xyz);
    outTexCoord = texCoord;
    outColor = color;
    outFog = ComputeFogFactor(outPosition.z);
}

float4 Frag(float4 position : SV_Position,
            float2 texCoord : TEXCOORD0,
            float4 color : COLOR,
            float fog : TEXCOORD1) : SV_Target
{
    half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, texCoord) * color * _Color;
    c.rgb = MixFog(c.rgb, fog);
    return c;
}

ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "AlphaBlend"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            ENDHLSL
        }
    }
}
