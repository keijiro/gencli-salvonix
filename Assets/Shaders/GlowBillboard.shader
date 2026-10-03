Shader "Salvonix/GlowBillboard"
{
    Properties
    {
        [HDR] _Color("Color", Color) = (4, 1, 0.5, 1)
        _Sharpness("Sharpness", Float) = 2
    }

HLSLINCLUDE

#include "SalvonixCommon.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _Color;
float _Sharpness;
CBUFFER_END

void Vert(float4 position : POSITION,
          float2 texCoord : TEXCOORD0,
          out float4 outPosition : SV_Position,
          out float2 outTexCoord : TEXCOORD0,
          out float outFog : TEXCOORD1)
{
    // Camera-facing quad around the object origin, sized by the X scale
    float3 center = TransformObjectToWorld(float3(0, 0, 0));
    float scale = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
    float3 view = TransformWorldToView(center) + float3(position.xy * scale, 0);
    outPosition = TransformWViewToHClip(view);
    outTexCoord = texCoord;
    outFog = ComputeFogFactor(outPosition.z);
}

float4 Frag(float4 position : SV_Position,
            float2 texCoord : TEXCOORD0,
            float fog : TEXCOORD1) : SV_Target
{
    half3 c = GlowProfile(texCoord, _Sharpness) * _Color.rgb * _Color.a;
    c = MixFogColor(c, 0, fog);
    return float4(c, 1);
}

ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "DisableBatching" = "True" }

        Pass
        {
            Name "GlowBillboard"
            Blend One One
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
