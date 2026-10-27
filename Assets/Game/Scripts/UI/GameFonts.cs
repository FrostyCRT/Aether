using TMPro;
using UnityEngine;

// AJOUTÉ (2026-10-25) - deux polices, deux rôles (comme les jeux pro : une police d'affiche + une police de lecture).
//  - Bangers : titres, onglets, boutons, noms (héros, armes, compétences), gros chiffres, étiquettes courtes en majuscules.
//  - Fira Sans SemiBold : toutes les phrases (descriptions, infobulles, sous-titres, notes, lignes d'information).
// Les textes posés dans les scènes / prefabs ont déjà la bonne police ; Body sert aux textes créés par le code.
// Chinois, japonais et coréen passent par les polices de repli de TMP Settings (voir LanguageFonts), comme pour Bangers.
// Licence : SIL Open Font License (Assets/Game/Fonts/FiraSans-OFL.txt).
public static class GameFonts
{
    private static TMP_FontAsset _body;

    public static TMP_FontAsset Body
    {
        get
        {
            if (_body == null) _body = Resources.Load<TMP_FontAsset>("Fonts/FiraSans-SemiBold SDF");
            return _body;
        }
    }

    /// <summary>Largeur de contour adaptée à la police du texte : le trait de Fira est bien plus fin que celui de Bangers,
    /// un contour de taille « Bangers » (0,2 et plus) le ronge et le rend pâle. Divisé par deux pour la police de lecture.</summary>
    public static float Outline(TMP_Text t, float bangersWidth)
    {
        return t != null && Body != null && t.font == Body ? bangersWidth * 0.5f : bangersWidth;
    }

    /// <summary>Passe un texte dans la police de lecture : droit, casse d'origine, sans gras forcé.</summary>
    public static void ToBody(TMP_Text t)
    {
        if (t == null || Body == null) return;
        t.font = Body;
        t.fontSharedMaterial = Body.material;
        t.fontStyle &= ~(FontStyles.Italic | FontStyles.Bold | FontStyles.UpperCase | FontStyles.SmallCaps);
    }
}
