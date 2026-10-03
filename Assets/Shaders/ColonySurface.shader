Shader "Salvonix/ColonySurface"
{
    Properties
    {
        _CityTex("City Texture", 2D) = "white" {}
        _NatureTex("Nature Texture", 2D) = "white" {}
        _MaskTex("Zone Mask", 2D) = "gray" {}
        _Radius("Radius", Float) = 1000
        _Length("Length", Float) = 7000
        _CityTiles("City Tiles Around", Float) = 12
        _NatureTiles("Nature Tiles Around", Float) = 8
        _StripCount("Strip Count", Float) = 6
        _StripWidth("Strip Width", Float) = 10
        [HDR] _StripColor("Strip Color", Color) = (2.6, 2.9, 3.4, 1)
        _Brightness("Brightness", Float) = 1
    }

HLSLINCLUDE

#include "SalvonixCommon.hlsl"

TEXTURE2D(_CityTex); SAMPLER(sampler_CityTex);
TEXTURE2D(_NatureTex); SAMPLER(sampler_NatureTex);
TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);

CBUFFER_START(UnityPerMaterial)
float _Radius;
float _Length;
float _CityTiles;
float _NatureTiles;
float _StripCount;
float _StripWidth;
float4 _StripColor;
float _Brightness;
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
    // texCoord.x: fraction around the circumference, texCoord.y: axial position (m)
    float circ = 2 * PI * _Radius;
    float a = texCoord.x;
    float z = texCoord.y;

    // Zone blend (city / nature)
    float2 maskUV = float2(a, z / _Length + 0.5);
    half mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, maskUV).r;
    float2 cityUV = float2(a * _CityTiles, z / (circ / _CityTiles));
    float2 natureUV = float2(a * _NatureTiles, z / (circ / _NatureTiles));
    half3 city = SAMPLE_TEXTURE2D(_CityTex, sampler_CityTex, cityUV).rgb;
    half3 nature = SAMPLE_TEXTURE2D(_NatureTex, sampler_NatureTex, natureUV).rgb;
    half3 col = lerp(nature, city, smoothstep(0.42, 0.58, mask));
    col *= _Brightness;

    // Longitudinal light strips
    float d = abs(frac(a * _StripCount + 0.5) - 0.5) * circ / _StripCount;
    float cover = saturate((_StripWidth * 0.5 - d) / max(fwidth(d), 1e-3) + 0.5);
    float road = saturate((_StripWidth * 1.4 - d) / max(fwidth(d), 1e-3) + 0.5);
    float dz = z / 45;
    float dash = 0.55 + 0.45 * step(frac(dz), 0.5);
    dash = lerp(dash, 0.78, saturate(fwidth(dz) * 1.5));
    col = lerp(col, half3(0.18, 0.19, 0.21), road);
    col += _StripColor.rgb * cover * dash;

    col = MixFog(col, fog);
    return float4(col, 1);
}

ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ColonySurface"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            ENDHLSL
        }
    }
}
