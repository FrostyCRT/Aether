// AJOUTÉ (2026-11-25, optimisation des gemmes d'XP : ~2 ms par image à 8 000 gemmes, coût qui grandissait toute la
// partie) - rendu des gemmes de XPGemField. Même résultat que l'ancien Sprites/Default (texture x intensité, alpha
// prémultiplié, pas d'écriture de profondeur), mais l'animation des gemmes posées au sol (flottement + respiration) est
// calculée ICI à partir d'une horloge, au lieu d'être recalculée et renvoyée par le processeur à chaque image : le
// maillage des gemmes au sol ne change plus que quand une gemme se pose, est ramassée ou aspirée.
//
// Sommet : POSITION = point d'ancrage de la gemme (bas du quad, au sol) ;
//          TEXCOORD0 = (u, v de l'atlas, décalage du coin à droite, décalage du coin vers le haut) ;
//          TEXCOORD1 = (échelle, phase, rythme de respiration, horloge de naissance) ;
//          TEXCOORD2 = intensité fixe (> 0 : gemme en mouvement, calculée par le processeur) ou 0 (gemme posée : animée ici).
// Échelle 0 = quad dégénéré (aucun pixel) : c'est ainsi qu'une gemme ramassée disparaît sans reconstruire le maillage.
Shader "Aether/XPGemField"
{
    Properties
    {
        _MainTex("Atlas des 3 paliers", 2D) = "white" {}
        _Anim("Flottement : vitesse, amplitude ; respiration : vitesse", Vector) = (2.6, 0.1, 1.5, 0)
        _Glow("Intensité : min, max", Vector) = (1, 1.3, 0, 0)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "XPGems"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Anim;
                float4 _Glow;
            CBUFFER_END
            // Globales posées par XPGemField chaque image : orientation face caméra (celle de la caméra du jeu, identique
            // à l'ancien calcul processeur) et horloge des gemmes (n'avance pas pendant les pauses).
            float3 _XPGemRight;
            float3 _XPGemUp;
            float _XPGemClock;

            struct A
            {
                float3 anchor : POSITION;
                float4 uvc : TEXCOORD0;
                float4 anim : TEXCOORD1;
                float fixedGlow : TEXCOORD2;
            };
            struct V
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half glow : TEXCOORD1;
            };

            V vert(A v)
            {
                V o;
                float3 p = TransformObjectToWorld(v.anchor);
                float scale = v.anim.x;
                half glow;
                if (v.fixedGlow > 0.0)
                {
                    glow = v.fixedGlow;
                }
                else
                {
                    float t = _XPGemClock - v.anim.w;                       // temps passé depuis l'apparition
                    p.y += max(0.0, sin(t * _Anim.x + v.anim.y)) * _Anim.y;  // flotte, jamais sous le sol
                    glow = lerp(_Glow.x, _Glow.y, 0.5 + 0.5 * sin(t * _Anim.z * v.anim.z + v.anim.y));
                }
                p += _XPGemRight * (v.uvc.z * scale) + _XPGemUp * (v.uvc.w * scale);
                o.positionCS = TransformWorldToHClip(p);
                o.uv = v.uvc.xy;
                o.glow = glow;
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                c.rgb *= i.glow;
                c.rgb *= c.a;
                return c;
            }
            ENDHLSL
        }
    }
}
