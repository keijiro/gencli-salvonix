Shader "Salvonix/FlameBillboard"
{
    Properties
    {
        [HDR] _CoreColor("Core Color", Color) = (6, 7, 8, 1)
        [HDR] _Color("Outer Color", Color) = (0.6, 1.2, 6, 1)
        _Intensity("Intensity", Float) = 1
    }

HLSLINCLUDE

#include "SalvonixCommon.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _CoreColor;
float4 _Color;
float _Intensity;
CBUFFER_END

void Vert(float4 position : POSITION,
          float2 texCoord : TEXCOORD0,
          out float4 outPosition : SV_Position,
          out float2 outTexCoord : TEXCOORD0,
          out float outFog : TEXCOORD1)
{
    // Axial billboard: the flame extends along the object's local Z axis
    // (length = Z scale) and faces the camera around that axis (width = X scale).
    float3 origin = TransformObjectToWorld(float3(0, 0, 0));
    float3 axisZ = mul((float3x3)UNITY_MATRIX_M, float3(0, 0, 1));
    float3 axisX = mul((float3x3)UNITY_MATRIX_M, float3(1, 0, 0));
    float len = length(axisZ);
    float width = length(axisX);
    float3 axis = axisZ / max(len, 1e-5);
    float3 toCam = normalize(GetCameraPositionWS() - origin);
    float3 side = cross(axis, toCam);
    side = dot(side, side) > 1e-6 ? normalize(side) : normalize(axisX);
    float t = position.y + 0.5;
    float3 wp = origin + axis * (t * len) + side * (position.x * width);
    outPosition = TransformWorldToHClip(wp);
    outTexCoord = float2(position.x + 0.5, t);
    outFog = ComputeFogFactor(outPosition.z);
}

float4 Frag(float4 position : SV_Position,
            float2 texCoord : TEXCOORD0,
            float fog : TEXCOORD1) : SV_Target
{
    float across = 1 - abs(texCoord.x * 2 - 1);
    float along = texCoord.y;
    float flicker = 0.85 + 0.15 * sin(_Time.y * 61 + along * 13);
    float shape = saturate(across - along * 0.85);
    float core = pow(shape, 3) * (1 - along);
    float body = pow(shape, 1.2) * pow(1 - along, 1.5);
    half3 c = (_CoreColor.rgb * core + _Color.rgb * body) * flicker * _Intensity;
    c = MixFogColor(c, 0, fog);
    return float4(c, 1);
}

ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "DisableBatching" = "True" }

        Pass
        {
            Name "FlameBillboard"
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
