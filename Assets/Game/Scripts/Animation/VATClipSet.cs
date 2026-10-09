using UnityEngine;

// AJOUTÉ (2026-10-05) - decrit les plages de frames cuites par VATBaker.cs dans la texture d'animation d'un
// ennemi (une ligne de texture = une frame), pour que VATAnimatedRenderer sache ou lire chaque clip ("Walk",
// "Bite"...) sans avoir besoin de l'Animator/AnimatorController original en jeu.
[CreateAssetMenu(fileName = "VATClipSet", menuName = "Aether/VAT Clip Set")]
public class VATClipSet : ScriptableObject
{
    public VATAnimatedRenderer.ClipRange[] clips;
}
