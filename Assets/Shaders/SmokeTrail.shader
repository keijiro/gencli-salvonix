Shader "Salvonix/SmokeTrail"
{
    Properties
    {
        _Color("Color", Color) = (0.92, 0.93, 0.95, 1)
        _ShadeColor("Shade Color", Color) = (0.55, 0.58, 0.64, 1)
        _NoiseScale("Noise Scale", Float) = 0.35
    }

HLSLINCLUDE

#include "SalvonixCommon.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _Color;
float4 _ShadeColor;
float _NoiseScale;
CBUFFER_END

void Vert(float4 position : POSITION,
          float2 texCoord : TEXCOORD0,
          float4 color : COLOR,
          out float4 outPosition : SV_Position,
          out float2 outTexCoord : TEXCOORD0,
          out float4 outColor : COLOR,
          out float3 outWorld : TEXCOORD1,
          out float outFog : TEXCOORD2)
{
    float3 wp = TransformObjectToWorld(position.xyz);
    outPosition = TransformWorldToHClip(wp);
    outTexCoord = texCoord;
    outColor = color;
    outWorld = wp;
    outFog = ComputeFogFactor(outPosition.z);
}

float4 Frag(float4 position : SV_Position,
            float2 texCoord : TEXCOORD0,
            float4 color : COLOR,
            float3 world : TEXCOORD1,
            float fog : TEXCOORD2) : SV_Target
{
    // Puffy noise anchored in world space
    float3 p = world * _NoiseScale;
    float n = ValueNoise3(p) * 0.6 + ValueNoise3(p * 2.3 + 7.1) * 0.4;

    // Soft edges across the trail, eroded by the noise
    float across = 1 - abs(texCoord.y * 2 - 1);
    float edge = saturate(across * 1.6 - (1 - n) * 0.9);
    float alpha = smoothstep(0, 0.5, edge) * color.a;

    half3 c = lerp(_ShadeColor.rgb, _Color.rgb, saturate(n * 1.3 + across * 0.3)) * color.rgb;
    c = MixFog(c, fog);
    return float4(c, alpha);
}

ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "SmokeTrail"
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
