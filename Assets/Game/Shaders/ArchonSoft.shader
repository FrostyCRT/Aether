// AJOUTÉ (2026-11-26, Archonte de la Source v2 - retour utilisateur : la v1 « a l'air robotique », couleurs « blanc
// bleu comme les autres ») - peau, robe et mèches de lumière de l'Archonte :
//  - couleur = dégradé _ColorA -> _ColorB selon le facteur t rangé dans la couleur des sommets (blanc bleuté -> bleu glacier vers le bout des voiles et des
//    mèches) x teinte du matériau ;
//  - 3 tons doux (ombre bleutée, jamais grise) comme le reste des modèles peints du jeu ;
//  - liseré lumineux sur le pourtour (Fresnel) + part d'auto-éclairage : un être de lumière, lisible sur l'herbe ;
//  - vague de lumière qui monte lentement le long du corps (_Shimmer) ;
//  - liseré sombre (trait d'encre, normale skinnée) + ombre portée.
Shader "Aether/ArchonSoft"
{
    Properties
    {
        _BaseColor("Teinte", Color) = (1, 1, 1, 1)
        _ColorA("Couleur de base (t = 0)", Color) = (1, 1, 1, 1)
        _ColorB("Couleur du bout (t = 1)", Color) = (1, 1, 1, 1)
        _ShadowTint("Teinte de l'ombre (multiplie)", Color) = (0.62, 0.7, 0.92, 1)
        _RimColor("Liseré lumineux", Color) = (0.7, 0.92, 1, 1)
        _RimStrength("Force du liseré lumineux", Range(0, 2)) = 0.6
        _RimPower("Finesse du liseré lumineux", Range(0.5, 8)) = 2.5
        _SelfLight("Auto-éclairage", Range(0, 1)) = 0.25
        _Shimmer("Vague de lumière", Range(0, 1)) = 0.15
        _ShimmerSpeed("Vitesse de la vague", Float) = 0.35
        _OutlineColor("Liseré sombre", Color) = (0.14, 0.18, 0.3, 1)
        _OutlineWidth("Épaisseur du liseré sombre", Range(0, 0.05)) = 0.015
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Faces", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _ColorA, _ColorB, _ShadowTint, _RimColor, _OutlineColor;
            half _RimStrength, _RimPower, _SelfLight, _Shimmer, _ShimmerSpeed, _OutlineWidth, _Cull;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardArchonSoft"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; half4 color : TEXCOORD2; float h : TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color; o.h = v.positionOS.y;
                return o;
            }
            half4 frag(V i, bool front : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n = normalize(i.normalWS); if (!front) n = -n;
                float3 vdir = normalize(GetWorldSpaceViewDir(i.positionWS));
                Light sun = GetMainLight();
                half ndl = dot(n, sun.direction) * 0.5h + 0.5h;
                half tone = smoothstep(0.28h, 0.42h, ndl) * 0.5h + smoothstep(0.6h, 0.74h, ndl) * 0.5h;
                half3 albedo = lerp(_ColorA.rgb, _ColorB.rgb, saturate(i.color.r)) * _BaseColor.rgb;   // couleur des sommets = facteur t du dégradé
                half3 col = albedo * lerp(_ShadowTint.rgb, half3(1, 1, 1), tone);
                col = lerp(col, albedo, _SelfLight);
                half rim = pow(1.0h - saturate(dot(n, vdir)), _RimPower) * _RimStrength;
                col += _RimColor.rgb * rim;
                float band = frac(i.h * 0.12 - _Time.y * _ShimmerSpeed);
                col += _RimColor.rgb * (1.0h - smoothstep(0.0h, 0.06h, abs(band - 0.5h))) * _Shimmer;
                col *= lerp(half3(1, 1, 1), sun.color, 0.3h);
                return half4(min(col, 1.6h), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz + normalize(v.normalOS) * _OutlineWidth);
                return o;
            }
            half4 frag(V i) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; };
            V vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v);
                float3 pws = TransformObjectToWorld(v.positionOS.xyz);
                float3 nws = TransformObjectToWorldNormal(v.normalOS);
                float4 cs = TransformWorldToHClip(ApplyShadowBias(pws, nws, _LightDirection));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = cs;
                return o;
            }
            half4 frag(V i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}
