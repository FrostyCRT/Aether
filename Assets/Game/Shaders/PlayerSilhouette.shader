// AJOUTÉ (2026-11-27, Lotus - test en jeu : le joueur disparaissait sous les pétales du boss) - SILHOUETTE du héros,
// dessinée par le renderer URP (Render Objects « joueur caché ») UNIQUEMENT là où il est masqué par quelque chose
// (test de profondeur « plus loin que »). Aplat clair translucide + liseré : on voit toujours où l'on est.
Shader "Aether/PlayerSilhouette"
{
    Properties
    {
        _Color("Silhouette", Color) = (0.92, 0.96, 1, 0.55)
        _RimColor("Liseré", Color) = (1, 1, 1, 0.9)
        _RimPower("Finesse du liseré", Range(0.5, 6)) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Silhouette"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON      // GPU Resident Drawer du projet (sinon erreur + rendu magenta)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _RimColor; half _RimPower;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 n : TEXCOORD0; float3 v : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V vert(A i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_TRANSFER_INSTANCE_ID(i, o);
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(ws); o.n = TransformObjectToWorldNormal(i.normalOS); o.v = GetWorldSpaceViewDir(ws);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                half rim = pow(1.0h - saturate(dot(normalize(i.n), normalize(i.v))), _RimPower);
                return lerp(_Color, _RimColor, rim);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
