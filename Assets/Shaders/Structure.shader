Shader "Salvonix/Structure"
{
    Properties
    {
        _Color("Color", Color) = (0.55, 0.58, 0.62, 1)
        _Ambient("Ambient", Color) = (0.45, 0.5, 0.58, 1)
        [HDR] _LightColor("Light Color", Color) = (6, 6.5, 7, 1)
        _LightSpacing("Light Spacing", Float) = 24
        _PanelSize("Panel Size", Float) = 14
    }

HLSLINCLUDE

#include "SalvonixCommon.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _Color;
float4 _Ambient;
float4 _LightColor;
float _LightSpacing;
float _PanelSize;
CBUFFER_END

void Vert(float4 position : POSITION,
          float3 normal : NORMAL,
          float2 texCoord0 : TEXCOORD0,
          float2 texCoord1 : TEXCOORD1,
          out float4 outPosition : SV_Position,
          out float3 outNormal : NORMAL,
          out float2 outTexCoord0 : TEXCOORD0,
          out float2 outTexCoord1 : TEXCOORD1,
          out float outFog : TEXCOORD2)
{
    outPosition = TransformObjectToHClip(position.xyz);
    outNormal = TransformObjectToWorldNormal(normal);
    outTexCoord0 = texCoord0;
    outTexCoord1 = texCoord1;
    outFog = ComputeFogFactor(outPosition.z);
}

float4 Frag(float4 position : SV_Position,
            float3 normal : NORMAL,
            float2 texCoord0 : TEXCOORD0,
            float2 texCoord1 : TEXCOORD1,
            float fog : TEXCOORD2) : SV_Target
{
    // texCoord0: x = length (m), y = cross coordinate (0-1)
    // texCoord1: x = 1 on faces carrying lights
    float2 p = texCoord0 / float2(_PanelSize, 1);
    float seam = step(0.04, frac(p.x)) * step(0.06, frac(texCoord0.y * 3));
    float shade = 0.82 + 0.18 * Hash13(float3(floor(p.x), floor(texCoord0.y * 3), 3));
    half3 col = _Color.rgb * shade * lerp(0.6, 1, seam);
    col *= SimpleLighting(normalize(normal), _Ambient.rgb);

    // Light dots
    float lx = abs(frac(texCoord0.x / _LightSpacing) - 0.5) * _LightSpacing;
    float ly = abs(texCoord0.y - 0.5) * 8;
    float dist = length(float2(lx, ly));
    float dot_ = saturate((1.6 - dist) / max(fwidth(dist), 1e-3) + 0.5);
    col += _LightColor.rgb * dot_ * texCoord1.x;

    col = MixFog(col, fog);
    return float4(col, 1);
}

ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Structure"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            ENDHLSL
        }
    }
}
