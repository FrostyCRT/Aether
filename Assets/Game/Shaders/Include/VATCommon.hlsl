#ifndef AETHER_VAT_COMMON_INCLUDED
#define AETHER_VAT_COMMON_INCLUDED

// AJOUTÉ (2026-10-05) - pilier de la solution "texture d'animation GPU" (Vertex Animation Texture) pour les
// ennemis courants : but = garder une animation 3D squelettique complète à l'écran, sans que le coût CPU de
// l'Animator (confirmé ~22% du temps de frame à 300 ennemis, voir NOTES.md) n'existe du tout pour ces ennemis.
//
// Principe : chaque pixel de _VatPositionTex contient la position OBJECT SPACE d'UN sommet du mesh, pour UNE
// frame d'animation précuite (texture_x = index du sommet, texture_y = numéro de frame). Le vertex shader lit
// directement la position déjà animée dans la texture au lieu de calculer un skinning osseux à chaque frame -
// tout le travail se fait une seule fois, à la cuisson (VATBaker.cs), jamais en jeu. _VatNormalTex fait la même
// chose pour les normales, pour un éclairage correct pendant le mouvement (pas juste la pose de bind).
//
// _VatFrame est une propriété PAR INSTANCE (UNITY_INSTANCING_BUFFER) : chaque ennemi peut être sur sa propre
// frame d'animation (vitesse differente, dephasage pour eviter l'effet "armee de robots synchronises") tout en
// partageant le MEME mesh/materiau/texture - condition necessaire pour que le SRP Batcher / GPU Resident Drawer
// puisse regrouper le rendu de centaines d'instances.

TEXTURE2D(_VatPositionTex); SAMPLER(sampler_VatPositionTex);
TEXTURE2D(_VatNormalTex);   SAMPLER(sampler_VatNormalTex);

CBUFFER_START(UnityPerMaterialVat)
float4 _VatPositionTex_TexelSize; // x=1/width, y=1/height, z=width, w=height (rempli automatiquement par Unity)
CBUFFER_END

// CORRIGÉ (2026-10-06, retour utilisateur : toujours <30 FPS avec beaucoup d'ennemis malgré la suppression de
// l'Animator) - la 1ère tentative de rendre _VatFrame par-instance (UNITY_INSTANCING_BUFFER) avait été abandonnée
// début octobre faute de fonctionner (valeur toujours à 0 en Play), remplacée par une propriété globale simple -
// ce qui a réglé le CPU (plus d'Animator) mais a silencieusement exclu CHAQUE ennemi du regroupement de rendu GPU
// (un MaterialPropertyBlock sur un Renderer le sort du lot dès qu'il n'est pas déclaré "par instance" côté
// shader) : avec des dizaines d'ennemis à l'écran, autant d'appels de dessin séparés - le vrai goulot
// d'étranglement restant, invisible aux mesures de temps CPU utilisées jusqu'ici. Remis en UNITY_INSTANCING_BUFFER
// et revérifié en isolation AVANT tout changement de prefab (voir NOTES.md) - fonctionne cette fois.
UNITY_INSTANCING_BUFFER_START(VatPerInstance)
    UNITY_DEFINE_INSTANCED_PROP(float, _VatFrame)
UNITY_INSTANCING_BUFFER_END(VatPerInstance)

// vertexU : coordonnee horizontale du sommet dans la texture VAT, precalculee a la cuisson et stockee dans un
// canal UV libre du mesh (voir VATBaker.cs - canal uv4/TEXCOORD3, inutilise par LitForwardPass.hlsl de base).
void ApplyVatDisplacement(inout float3 positionOS, inout float3 normalOS, float vertexU, float instanceFrame)
{
    float frameV = (instanceFrame + 0.5) * _VatPositionTex_TexelSize.y;
    float2 uv = float2(vertexU, frameV);

    float3 vatPosition = SAMPLE_TEXTURE2D_LOD(_VatPositionTex, sampler_VatPositionTex, uv, 0).xyz;
    float3 vatNormal = SAMPLE_TEXTURE2D_LOD(_VatNormalTex, sampler_VatNormalTex, uv, 0).xyz;

    positionOS = vatPosition;
    normalOS = normalize(vatNormal);
}

#endif
