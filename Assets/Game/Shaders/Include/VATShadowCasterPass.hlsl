#ifndef AETHER_VAT_SHADOW_CASTER_PASS_INCLUDED
#define AETHER_VAT_SHADOW_CASTER_PASS_INCLUDED

// AJOUTÉ (2026-10-25, retour utilisateur : "il n'y a pas d'ombre sur les ennemis, ce n'est pas normal") - copie de
// ShadowCasterPass.hlsl (URP) + déplacement VAT. Sans cette passe, aucun ennemi VAT ne pouvait projeter d'ombre, même
// avec shadowCastingMode = On ; avec la passe URP d'origine, l'ombre aurait gardé la pose figée du mesh de cuisson au
// lieu de suivre l'animation. Même déplacement que VATDepthOnlyPass.hlsl (position ET normale, la normale servant au
// biais d'ombre).
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Assets/Game/Shaders/Include/VATCommon.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

float3 _LightDirection;
float3 _LightPosition;

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float2 texcoord     : TEXCOORD0;
    float2 vatUV        : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    #if defined(_ALPHATEST_ON)
        float2 uv       : TEXCOORD0;
    #endif
    float4 positionCS   : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings ShadowPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    float3 posOS = input.positionOS.xyz;
    float3 normOS = input.normalOS;
    ApplyVatDisplacement(posOS, normOS, input.vatUV.x, UNITY_ACCESS_INSTANCED_PROP(VatPerInstance, _VatFrame));

    float3 positionWS = TransformObjectToWorld(posOS);
    float3 normalWS = TransformObjectToWorldNormal(normOS);
    #if _CASTING_PUNCTUAL_LIGHT_SHADOW
        float3 lightDirectionWS = normalize(_LightPosition - positionWS);
    #else
        float3 lightDirectionWS = _LightDirection;
    #endif
    output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));

    #if defined(_ALPHATEST_ON)
        output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    #endif
    return output;
}

half4 ShadowPassFragment(Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);

    #if defined(_ALPHATEST_ON)
        Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
    #endif

    #if defined(LOD_FADE_CROSSFADE)
        LODFadeCrossFade(input.positionCS);
    #endif

    return 0;
}
#endif
