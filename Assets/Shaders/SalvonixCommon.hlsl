#ifndef SALVONIX_COMMON_INCLUDED
#define SALVONIX_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Hash function (Dave Hoskins)
float Hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

// 3D value noise in [0, 1]
float ValueNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * (3 - 2 * f);
    float n000 = Hash13(i + float3(0, 0, 0));
    float n100 = Hash13(i + float3(1, 0, 0));
    float n010 = Hash13(i + float3(0, 1, 0));
    float n110 = Hash13(i + float3(1, 1, 0));
    float n001 = Hash13(i + float3(0, 0, 1));
    float n101 = Hash13(i + float3(1, 0, 1));
    float n011 = Hash13(i + float3(0, 1, 1));
    float n111 = Hash13(i + float3(1, 1, 1));
    float nx00 = lerp(n000, n100, u.x);
    float nx10 = lerp(n010, n110, u.x);
    float nx01 = lerp(n001, n101, u.x);
    float nx11 = lerp(n011, n111, u.x);
    return lerp(lerp(nx00, nx10, u.y), lerp(nx01, nx11, u.y), u.z);
}

// Simple diffuse lighting with the main light and a flat ambient term
half3 SimpleLighting(float3 normalWS, half3 ambient)
{
    Light light = GetMainLight();
    half ndl = saturate(dot(normalWS, light.direction));
    return ambient + light.color * ndl;
}

// Radial glow profile for a quad UV
half GlowProfile(float2 uv, half sharpness)
{
    float r = length(uv * 2 - 1);
    half core = saturate(1 - r);
    return pow(core, sharpness) + exp(-r * r * 9) * 0.35 * core;
}

#endif
