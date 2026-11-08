using UnityEngine;
using UnityEngine.UI;

// AJOUTÉ (2026-11-07, retour utilisateur : "met pas de simple ronds colorés, il faut que ce soit précis sur les éléments
// du fond d'écran", puis "fais le même ajustement sur les autres pages") - lueur « sur mesure » d'un fond de menu : une
// texture découpée dans l'illustration elle-même (Tools/build_menu_glows.py -> Assets/Resources/<page>Glow/), posée
// par ANCRES en proportions de l'illustration, donc pixel pour pixel sur son élément quelle que soit la résolution (le
// fond est étiré sur toute la page). Additive, immobile : le script de la page n'anime que l'alpha (CanvasRenderer).
// Utilisé par SettingsAmbientFX, ReputationHallFX et TreeAmbientFX.
public static class MenuShapedGlow
{
    // x, y, w, h : rectangle de la texture en pixels de l'illustration (artW x artH), origine en haut à gauche.
    // Renvoie null si la texture manque (la page garde alors ses autres effets).
    public static CanvasRenderer Create(RectTransform parent, string resourcePath, float x, float y, float w, float h,
                                        float artW, float artH, Material additive)
    {
        Texture2D tex = Resources.Load<Texture2D>(resourcePath);
        if (tex == null) return null;
        string objName = "Glow_" + resourcePath.Substring(resourcePath.LastIndexOf('/') + 1);
        GameObject go = new GameObject(objName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(x / artW, 1f - (y + h) / artH);
        rt.anchorMax = new Vector2((x + w) / artW, 1f - y / artH);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        RawImage im = go.GetComponent<RawImage>();
        im.texture = tex;
        im.raycastTarget = false;
        im.material = additive;
        CanvasRenderer cr = go.GetComponent<CanvasRenderer>();
        cr.SetColor(new Color(1f, 1f, 1f, 0f));
        return cr;
    }
}
