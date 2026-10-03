Shader "Salvonix/Additive"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        [HDR] _Color("Color", Color) = (1, 1, 1, 1)
        [Enum(Texture, 0, Glow, 1, Trail, 2)] _Mode("Mode", Float) = 0
        _Sharpness("Sharpness", Float) = 2
    }

HLSLINCLUDE

#include "SalvonixCommon.hlsl"

TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

CBUFFER_START(UnityPerMaterial)
float4 _MainTex_ST;
float4 _Color;
float _Mode;
float _Sharpness;
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
    half3 c;
    if (_Mode < 0.5)
    {
        half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, texCoord);
        c = tex.rgb * tex.a;
    }
    else if (_Mode < 1.5)
    {
        c = GlowProfile(texCoord, _Sharpness);
    }
    else
    {
        half across = 1 - abs(texCoord.y * 2 - 1);
        c = pow(across, _Sharpness) * (1 - texCoord.x);
    }
    c *= color.rgb * color.a * _Color.rgb;
    c = MixFogColor(c, 0, fog);
    return float4(c, 1);
}

ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Additive"
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
