using UnityEngine;

// AJOUTE (2026-09-19) - sprites de lumière de l'onglet Compétences, générés en code (même
// principe que CharacterAmbientFX : zéro asset à importer/maintenir). Créés à la première
// demande, partagés par tous les nœuds / liaisons / lueurs, recréés si Unity les a détruits.
//
// (Un premier essai avec des PNG générés puis importés a donné des alphas faux - le halo
// central sortait à 17 % au lieu de 100 % - ; la génération à l'exécution évite tout le
// problème d'import : compression, mode de sprite, espace colorimétrique.)
public static class TreeSprites
{
    private static Sprite _glow, _ring, _ray;

    // Halo radial doux : coeur lumineux, queue qui s'éteint avant le bord (aucune arête carrée).
    public static Sprite Glow => Get(ref _glow, 128, 128, (u, v) =>
    {
        float r = Mathf.Sqrt(u * u + v * v);
        float g = Mathf.Exp(-r * r * 4.5f);
        return g * (1f - Smooth(0.72f, 1f, r));
    });

    // Couronne : le maximum de lumière est juste au-delà du cadre d'un médaillon (rayon ~0,5 du
    // sprite), pas au centre (caché de toute façon par le médaillon).
    public static Sprite Ring => Get(ref _ring, 128, 128, (u, v) =>
    {
        float r = Mathf.Sqrt(u * u + v * v);
        float d = (r - 0.5f) / 0.2f;
        float a = Mathf.Exp(-d * d) + 0.30f * Mathf.Exp(-r * r * 7f);
        return a * (1f - Smooth(0.8f, 1f, r));
    });

    // Rayon de lumière vertical : bord gaussien en largeur ; le plus lumineux vers le HAUT (côté
    // source), qui s'éteint en descendant.
    public static Sprite Ray => Get(ref _ray, 32, 256, (u, v) =>
    {
        float across = Mathf.Exp(-u * u * 4.5f) * (1f - Smooth(0.7f, 1f, Mathf.Abs(u)));
        float t = v * 0.5f + 0.5f; // 0 = bas, 1 = haut
        float along = Mathf.Pow(Smooth(0f, 0.85f, t), 1.3f) * (1f - Smooth(0.92f, 1f, t));
        return across * along;
    });

    // smoothstep "shader" (bords e0/e1 -> 0..1). Attention : Mathf.SmoothStep(from, to, t) d'Unity
    // n'est PAS cette fonction (elle interpole entre deux valeurs) ; l'utiliser comme un smoothstep
    // GLSL donnait des halos ~4x trop faibles.
    public static float Smooth(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }

    private static Sprite Get(ref Sprite cache, int w, int h, System.Func<float, float, float> alphaAt)
    {
        if (cache != null) return cache;

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontUnloadUnusedAsset
        };
        Color32[] px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f;
                float v = (y + 0.5f) / h * 2f - 1f;
                px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alphaAt(u, v)) * 255f + 0.5f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);

        cache = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        cache.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return cache;
    }
}
