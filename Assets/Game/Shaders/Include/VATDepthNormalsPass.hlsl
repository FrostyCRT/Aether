#ifndef AETHER_VAT_DEPTH_NORMALS_PASS_INCLUDED
#define AETHER_VAT_DEPTH_NORMALS_PASS_INCLUDED

// Copie de LitDepthNormalsPass.hlsl (URP) + deplacement VAT - voir VATDepthOnlyPass.hlsl pour pourquoi cette
// passe est OBLIGATOIRE (pas juste pour la qualite du SSAO) sur ce projet : "Source: DepthNormals" dans les
// reglages SSAO fait de cette passe (pas DepthOnly) le vrai pre-passe de profondeur utilise par le Depth Priming.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Assets/Game/Shaders/Include/VATCommon.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

struct Attributes
{
    float4 positionOS   : POSITION;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    float3 normal       : NORMAL;
    float2 vatUV        : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS  : SV_POSITION;
    float2 uv          : TEXCOORD1;
    half3 normalWS     : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings DepthNormalsVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 posOS = input.positionOS.xyz;
    float3 normOS = input.normal;
    ApplyVatDisplacement(posOS, normOS, input.vatUV.x, UNITY_ACCESS_INSTANCED_PROP(VatPerInstance, _VatFrame));

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.positionCS = TransformObjectToHClip(posOS);

    VertexNormalInputs normalInput = GetVertexNormalInputs(normOS, input.tangentOS);
    output.normalWS = half3(normalInput.normalWS);

    return output;
}

half4 DepthNormalsFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    #if defined(_ALPHATEST_ON)
        Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
    #endif

    #if defined(LOD_FADE_CROSSFADE)
        LODFadeCrossFade(input.positionCS);
    #endif

    return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
}
#endif
