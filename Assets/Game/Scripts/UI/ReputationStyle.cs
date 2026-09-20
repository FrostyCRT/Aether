using UnityEngine;

// AJOUTE (2026-09-20) - langage visuel de l'onglet Réputation, aligné sur les autres onglets refondus
// (fond violet nuit, textes gris-lavande, Bangers). Code couleur de la page : OR = doré (skins Classiques),
// ÉCLATS = lilas (bonus de Réputation, skins Prestige) ; un accent par stat et par personnage.
public static class ReputationStyle
{
    public static readonly Color PanelDark = new Color32(0x0C, 0x0A, 0x12, 0xFF);
    public static readonly Color Field = new Color32(0x15, 0x11, 0x1F, 0xFF);
    public static readonly Color Muted = new Color32(0x9C, 0x93, 0xAE, 0xFF);
    public static readonly Color Body = new Color32(0xD7, 0xD0, 0xE2, 0xFF);
    public static readonly Color EmptyPip = new Color32(0x3A, 0x35, 0x4A, 0xFF);
    public static readonly Color Gold = new Color32(0xFF, 0xC8, 0x3D, 0xFF);
    public static readonly Color Eclat = new Color32(0xB7, 0x9C, 0xFF, 0xFF);
    public static readonly Color Warn = new Color32(0xE2, 0x60, 0x4F, 0xFF);
    public static readonly Color Ok = new Color32(0x52, 0xD4, 0x6B, 0xFF);
    public static readonly Color ButtonText = new Color32(0x32, 0x32, 0x32, 0xFF);   // texte sur parchemin (comme l'onglet Compétences)

    public static Color StatAccent(string nodeId)
    {
        switch (nodeId)
        {
            case "reputationSpeed": return new Color32(0x6F, 0xD3, 0xFF, 0xFF);   // bleu glace (bottes ailées)
            case "reputationRegen": return new Color32(0x8B, 0xE0, 0x6A, 0xFF);   // vert (cœur de ronces)
            default: return new Color32(0xFF, 0x8A, 0x3D, 0xFF);                  // orange (lame de feu)
        }
    }

    public static string StatName(string nodeId)
    {
        switch (nodeId)
        {
            case "reputationSpeed": return "VITESSE";
            case "reputationRegen": return "RÉGÉNÉRATION";
            default: return "DÉGÂTS";
        }
    }

    // Même couleurs d'identité que les autres onglets : orange Aether, vert Kael, cyan Lyra.
    public static Color CharacterAccent(int characterIndex)
    {
        switch (characterIndex)
        {
            case 1: return new Color32(0x52, 0xD4, 0x6B, 0xFF);
            case 2: return new Color32(0x33, 0xC6, 0xF0, 0xFF);
            default: return new Color32(0xF2, 0x8A, 0x2E, 0xFF);
        }
    }

    public static Color TierColor(SkinTier tier) => tier == SkinTier.Prestige ? Eclat : Gold;

    // "+42 %" (Dégâts, Vitesse : fraction) ou "+70 PV/s" (Régénération).
    public static string FormatBonus(string nodeId, float value)
    {
        if (nodeId == "reputationRegen") return "+" + value.ToString("0.#") + " PV/s";
        return "+" + Mathf.RoundToInt(value * 100f) + "%";
    }

    public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

    // Cadre un portrait (sprite plus haut que large) dans un masque : largeur donnée, hauteur déduite du ratio, centré
    // en largeur. Sans focus, le portrait est collé en haut du masque (buste). Avec focusY (0 = haut du sprite,
    // 1 = bas) et la hauteur du masque, le point visé (ex. 0,2 = visage) est ramené au centre du masque.
    public static void FitPortrait(UnityEngine.UI.Image img, float width, float maskHeight = 0f, float focusY = 0f)
    {
        if (img == null || img.sprite == null) return;
        Rect r = img.sprite.rect;
        float aspect = r.width / Mathf.Max(1f, r.height);
        float height = width / aspect;
        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(0f, maskHeight > 0f ? focusY * height - maskHeight * 0.5f : 0f);
    }

    // Fait tenir un sprite ENTIER (aucun rognage) dans une boîte maxW x maxH, centré : la figurine complète du personnage.
    public static void FitWhole(UnityEngine.UI.Image img, float maxW, float maxH)
    {
        if (img == null || img.sprite == null) return;
        Rect r = img.sprite.rect;
        float aspect = r.width / Mathf.Max(1f, r.height);
        float w = maxW, h = maxW / aspect;
        if (h > maxH) { h = maxH; w = maxH * aspect; }
        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(w, h);
    }

    private static Sprite _bottomFade;

    // Dégradé vertical (opaque en bas -> transparent en haut) à teinter : fond doux sous une figurine rognée en buste.
    public static Sprite BottomFade
    {
        get
        {
            if (_bottomFade != null) return _bottomFade;
            Texture2D tex = new Texture2D(1, 64, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontUnloadUnusedAsset
            };
            for (int y = 0; y < 64; y++)
            {
                float t = 1f - y / 63f;            // 1 en bas, 0 en haut
                tex.SetPixel(0, y, new Color(1f, 1f, 1f, t * t * (3f - 2f * t)));
            }
            tex.Apply();
            _bottomFade = Sprite.Create(tex, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f), 100f);
            _bottomFade.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return _bottomFade;
        }
    }
}
