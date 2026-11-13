using System.Collections.Generic;
using TMPro;
using UnityEngine;

// AJOUTÉ (2026-11-11, retour utilisateur : "mettre plus en valeur la couleur du niveau de difficulté Facile Moyen Difficile dans
// les défis, que ce soit dans l'animation de début ou dans le pause panel, les couleurs on ne les voit pas bien avec ce fond
// derrière [...] pas en rajoutant un fond noir derrière" ; pistes retenues : A + C) -
//  - C : couleurs plus claires et plus saturées (avant : vert / ambre / rouille sombres, proches de l'encre du parchemin) ;
//  - A : le MOT seul reçoit un contour sombre de sa propre teinte et une légère lueur colorée. TextMeshPro n'applique un
//    contour qu'à tout un texte : le mot passe donc par la balise <material="...">, avec un matériau créé ici (un par
//    difficulté et par police) et déclaré au MaterialReferenceManager, où TextMeshPro le retrouve par son nom.
// Utilisé par l'annonce du défi (GameUI) et le menu Pause (PauseMenuUI) : mêmes couleurs partout.
public static class DifficultyStyle
{
    private static readonly Color Easy = new Color32(0x5C, 0xE0, 0x4E, 0xFF);     // vert vif
    private static readonly Color Medium = new Color32(0xFF, 0xB3, 0x1F, 0xFF);   // ambre doré
    private static readonly Color Hard = new Color32(0xFF, 0x4D, 0x3A, 0xFF);     // rouge vif

    // Contour poussé vers l'extérieur (dilatation de la moitié de sa largeur : il ne mange plus l'intérieur des lettres) et
    // lueur faite avec l'ombre adoucie (underlay) : disponible aussi dans le shader « Mobile » du texte du menu Pause, qui
    // n'a pas de lueur et n'active son contour qu'avec le mot-clé OUTLINE_ON.
    private const float OutlineWidth = 0.30f;
    private const float GlowDilate = 0.55f;
    private const float GlowSoftness = 0.75f;
    private const float GlowAlpha = 0.55f;

    private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

    public static Color Tint(ChallengeDifficulty difficulty)
    {
        switch (difficulty)
        {
            case ChallengeDifficulty.Easy: return Easy;
            case ChallengeDifficulty.Hard: return Hard;
            default: return Medium;
        }
    }

    // Même teinte, très assombrie : le contour fait ressortir le mot sur un fond clair comme sur un fond chargé.
    private static Color Dark(Color c)
    {
        UnityEngine.Color.RGBToHSV(c, out float h, out float s, out float v);
        return UnityEngine.Color.HSVToRGB(h, Mathf.Min(1f, s * 1.05f), 0.22f);
    }

    // Le mot, coloré et habillé, prêt à insérer dans un texte riche écrit avec la police font (le matériau en dépend). Le
    // texte qui l'affiche doit avoir extraPadding à vrai : contour et lueur du mot dépassent ceux du reste du texte.
    public static string Wrap(TMP_FontAsset font, ChallengeDifficulty difficulty, string label)
    {
        string hex = ColorUtility.ToHtmlStringRGB(Tint(difficulty));
        string name = MaterialName(font, difficulty);
        if (name == null) return $"<color=#{hex}>{label}</color>";
        return $"<material=\"{name}\"><color=#{hex}>{label}</color></material>";
    }

    // AJOUTÉ (2026-11-11, retour utilisateur : dans le menu Pause « c'est trop flash, mets-le comme celui de l'animation de
    // défi du début ») - le mot est écrit dans la police titre (wordFont, Bangers) au lieu de celle du texte courant, avec le
    // même habillage que dans l'annonce : à petite taille dans la police de lecture, la couleur vive dominait le contour.
    public static string WrapInFont(TMP_FontAsset wordFont, ChallengeDifficulty difficulty, string label, int sizePercent)
    {
        string name = MaterialName(wordFont, difficulty);
        string hex = ColorUtility.ToHtmlStringRGB(Tint(difficulty));
        if (name == null) return $"<color=#{hex}>{label}</color>";
        MaterialReferenceManager.AddFontAsset(wordFont);   // retrouvée par son nom dans la balise <font>
        return $"<font=\"{wordFont.name}\" material=\"{name}\"><size={sizePercent}%><color=#{hex}>{label}</color></size></font>";
    }

    private static string MaterialName(TMP_FontAsset font, ChallengeDifficulty difficulty)
    {
        if (font == null || font.material == null) return null;
        string name = "Difficulty_" + difficulty + "_" + font.GetInstanceID();
        if (Materials.TryGetValue(name, out Material existing) && existing != null) return name;

        Material m = new Material(font.material) { name = name, hideFlags = HideFlags.HideAndDontSave };
        Color c = Tint(difficulty);
        m.EnableKeyword("OUTLINE_ON");
        m.SetFloat(ShaderUtilities.ID_FaceDilate, OutlineWidth * 0.5f);
        m.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineWidth);
        m.SetColor(ShaderUtilities.ID_OutlineColor, Dark(c));
        if (m.HasProperty(ShaderUtilities.ID_UnderlayColor))
        {
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(c.r, c.g, c.b, GlowAlpha));
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
            m.SetFloat(ShaderUtilities.ID_UnderlayDilate, GlowDilate);
            m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, GlowSoftness);
        }
        ShaderUtilities.UpdateShaderRatios(m);
        Materials[name] = m;
        MaterialReferenceManager.AddFontMaterial(TMP_TextUtilities.GetHashCode(name), m);
        return name;
    }
}
