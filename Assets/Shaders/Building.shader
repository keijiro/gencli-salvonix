Shader "Salvonix/Building"
{
    Properties
    {
        _FacadeTex("Facade Texture", 2D) = "white" {}
        _RoofColor("Roof Color", Color) = (0.74, 0.76, 0.78, 1)
        _Ambient("Ambient", Color) = (0.55, 0.6, 0.68, 1)
    }

HLSLINCLUDE

#include "SalvonixCommon.hlsl"

TEXTURE2D(_FacadeTex); SAMPLER(sampler_FacadeTex);

CBUFFER_START(UnityPerMaterial)
float4 _RoofColor;
float4 _Ambient;
CBUFFER_END

void Vert(float4 position : POSITION,
          float3 normal : NORMAL,
          float4 color : COLOR,
          float2 texCoord0 : TEXCOORD0,
          float2 texCoord1 : TEXCOORD1,
          out float4 outPosition : SV_Position,
          out float3 outNormal : NORMAL,
          out float4 outColor : COLOR,
          out float3 outTexCoord : TEXCOORD0,
          out float outFog : TEXCOORD1)
{
    outPosition = TransformObjectToHClip(position.xyz);
    outNormal = TransformObjectToWorldNormal(normal);
    outColor = color;
    outTexCoord = float3(texCoord0, texCoord1.x);
    outFog = ComputeFogFactor(outPosition.z);
}

float4 Frag(float4 position : SV_Position,
            float3 normal : NORMAL,
            float4 color : COLOR,
            float3 texCoord : TEXCOORD0,
            float fog : TEXCOORD1) : SV_Target
{
    half3 facade = SAMPLE_TEXTURE2D(_FacadeTex, sampler_FacadeTex, texCoord.xy).rgb;
    half3 col = lerp(facade * color.rgb * 1.1, _RoofColor.rgb * color.a, texCoord.z);
    Light light = GetMainLight();
    half ndl = dot(normalize(normal), light.direction);
    col *= lerp(0.72, 1.08, saturate(ndl * 0.5 + 0.5)) * lerp(0.9, 1.0, texCoord.z);
    col = MixFog(col, fog);
    return float4(col, 1);
}

ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Building"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            ENDHLSL
        }
    }
}
