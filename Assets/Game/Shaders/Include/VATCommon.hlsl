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
    UNITY_DEFINE_INSTANCED_PROP(float, _VatFrameNext)
    UNITY_DEFINE_INSTANCED_PROP(float, _Dissolve)
UNITY_INSTANCING_BUFFER_END(VatPerInstance)

// AJOUTÉ (2026-11-14, retour utilisateur : « quand les ennemis meurent [...] ils dépopent juste, on peut faire une fondue
// rapide ? ») - fondu de la copie laissée par un ennemi mort (EnemyDeathDissolve) : _Dissolve 0 -> 1 en ~0,35 s.
// MODIFIÉ (même jour, 2e retour : la 1re version, dissolution en blocs de pixels, « fait jeu rétro, pas dans la DA ») - vrai
// fondu en transparence : la copie utilise une variante transparente du matériau (Blend SrcAlpha OneMinusSrcAlpha, file
// Transparent), ApplyVatDeathFade règle son alpha. Seules les copies (quelques-unes à la fois) sont transparentes, jamais les
// ennemis vivants : pour eux _Dissolve = 0 et rien ne change. L'ombre portée, elle, s'efface en pointillé
// (ApplyVatDissolveClip, passe ShadowCaster seulement : une ombre n'a pas de transparence, et ce pointillé doux ne se voit
// pas). Propriété PAR INSTANCE comme _VatFrame : aucune incidence sur le regroupement GPU.
float VatDissolveNoise(float2 pixel)
{
    float2 cell = floor(pixel / 3.0);
    return frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);
}

void ApplyVatDissolveClip(float4 positionCS)
{
    float d = UNITY_ACCESS_INSTANCED_PROP(VatPerInstance, _Dissolve);
    if (d > 0.0) clip(VatDissolveNoise(positionCS.xy) - d);
}

// Alpha = 1 - _Dissolve, avec un léger éclaircissement chaud au tout début (le coup qui tue). Sans effet sur un matériau
// opaque (Blend One Zero ignore l'alpha) : seul le matériau transparent de la copie en tient compte.
half4 ApplyVatDeathFade(half4 color)
{
    float d = UNITY_ACCESS_INSTANCED_PROP(VatPerInstance, _Dissolve);
    if (d <= 0.0) return color;
    float flash = saturate(1.0 - d / 0.3);
    color.rgb = lerp(color.rgb, half3(1.0, 0.96, 0.88), flash * 0.35);
    color.a = 1.0 - d;
    return color;
}

// vertexU : coordonnee horizontale du sommet dans la texture VAT, precalculee a la cuisson et stockee dans un
// canal UV libre du mesh (voir VATBaker.cs - canal uv4/TEXCOORD3, inutilise par LitForwardPass.hlsl de base).
// MODIFIÉ (2026-11-11, retour utilisateur : "revenir sur une animation générale des ennemis plus fluide") - les clips
// sont cuits à 30 images/s et chaque image était tenue telle quelle : à 60-240 FPS, les ennemis bougeaient par paliers
// de 1/30 s. instanceFrame porte maintenant une partie décimale (avancement vers l'image suivante, _VatFrameNext, qui
// tient compte du bouclage du clip) : position et normale sont interpolées entre les deux images. Deux lectures de
// texture de plus par sommet, aucun coût processeur, regroupement GPU des instances inchangé.
void ApplyVatDisplacement(inout float3 positionOS, inout float3 normalOS, float vertexU, float instanceFrame)
{
    float frameA = floor(instanceFrame);
    float blend = instanceFrame - frameA;
    float frameB = UNITY_ACCESS_INSTANCED_PROP(VatPerInstance, _VatFrameNext);

    float2 uvA = float2(vertexU, (frameA + 0.5) * _VatPositionTex_TexelSize.y);
    float2 uvB = float2(vertexU, (frameB + 0.5) * _VatPositionTex_TexelSize.y);

    float3 posA = SAMPLE_TEXTURE2D_LOD(_VatPositionTex, sampler_VatPositionTex, uvA, 0).xyz;
    float3 nrmA = SAMPLE_TEXTURE2D_LOD(_VatNormalTex, sampler_VatNormalTex, uvA, 0).xyz;
    float3 posB = SAMPLE_TEXTURE2D_LOD(_VatPositionTex, sampler_VatPositionTex, uvB, 0).xyz;
    float3 nrmB = SAMPLE_TEXTURE2D_LOD(_VatNormalTex, sampler_VatNormalTex, uvB, 0).xyz;

    positionOS = lerp(posA, posB, blend);
    normalOS = normalize(lerp(nrmA, nrmB, blend));
}

#endif
