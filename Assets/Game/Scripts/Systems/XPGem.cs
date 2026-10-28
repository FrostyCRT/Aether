using UnityEngine;

// MODIFIÉ (2026-10-28, performances avec beaucoup de gemmes au sol) - les gemmes d'XP ne sont plus des GameObjects :
// elles sont simulées et dessinées toutes ensemble par XPGemField (voir son en-tête : mesures, comportement repris à
// l'identique, règle "jamais fusionnées ni détruites"). Cette classe ne garde que la description des 3 paliers, utilisée
// par XPGemSpawner, XPGemField et DebugCheats. Elle reste un MonoBehaviour car le prefab PFB_XPGem (plus utilisé) la
// référence encore.
//
// Historique de l'apparence (voir l'ancien XPGem, 2026-10-04 -> 2026-10-25) : texture cuite par palier (silhouette,
// dégradé radial décalé bas-gauche, liseré sombre - Tex_XPGem_*.png) sur un quad face caméra ancré par le bas (jamais
// sous le sol), scintillement d'intensité 1 -> 1,3, palette vert/bleu/or calquée sur la référence fournie.
public class XPGem : MonoBehaviour
{
    public enum GemType { Small, Medium, Large }

    public static float Value(GemType type)
    {
        switch (type)
        {
            case GemType.Large: return 50f;
            case GemType.Medium: return 20f;
            default: return 10f;
        }
    }

    // Couleur de la traînée d'aspiration et de l'éclat de ramassage (la couleur du corps est cuite dans la texture).
    public static Color ColorOf(GemType type)
    {
        switch (type)
        {
            case GemType.Large: return new Color(1f, 0.8f, 0.15f);    // or
            case GemType.Medium: return new Color(0.2f, 0.6f, 1f);    // bleu
            default: return new Color(0.25f, 0.95f, 0.35f);          // vert
        }
    }

    public static float ScaleOf(GemType type)
    {
        switch (type)
        {
            case GemType.Large: return 0.8f;
            case GemType.Medium: return 0.6f;
            default: return 0.46f;
        }
    }
}
