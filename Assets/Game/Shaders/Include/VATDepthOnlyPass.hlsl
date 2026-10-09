#ifndef AETHER_VAT_DEPTH_ONLY_PASS_INCLUDED
#define AETHER_VAT_DEPTH_ONLY_PASS_INCLUDED

// Copie de DepthOnlyPass.hlsl (URP) + deplacement VAT. INDISPENSABLE (pas optionnel) : le SSAO de ce projet
// utilise "Source: DepthNormals", qui s'appuie sur un pre-passe de profondeur - sans cette passe (ou sans que sa
// position corresponde EXACTEMENT a celle du passe ForwardLit), le Depth Priming d'URP compare la profondeur du
// passe principal (position animee VAT) a une profondeur jamais ecrite ici -> ZTest Equal echoue TOUJOURS ->
// objet invisible en silence, quel que soit le contenu du fragment shader (bug trouve et confirme par
// bisection le 2026-10-05, voir NOTES.md).
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Assets/Game/Shaders/Include/VATCommon.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

struct Attributes
{
    float4 position     : POSITION;
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
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings DepthOnlyVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 posOS = input.position.xyz;
    float3 dummyNormal = float3(0, 1, 0);
    ApplyVatDisplacement(posOS, dummyNormal, input.vatUV.x, UNITY_ACCESS_INSTANCED_PROP(VatPerInstance, _VatFrame));

    #if defined(_ALPHATEST_ON)
        output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    #endif
    output.positionCS = TransformObjectToHClip(posOS);
    return output;
}

half DepthOnlyFragment(Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    #if defined(_ALPHATEST_ON)
        Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
    #endif

    #if defined(LOD_FADE_CROSSFADE)
        LODFadeCrossFade(input.positionCS);
    #endif

    return input.positionCS.z;
}
#endif
