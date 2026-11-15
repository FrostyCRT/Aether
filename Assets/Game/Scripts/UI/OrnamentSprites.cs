using UnityEngine;

// AJOUTÉ (2026-11-14) - textures des filets décoratifs, partagées par la barre des fusions du menu Pause
// (GameUI.StyleFusionDivider) et le cadre du menu Pause (BrassFrameBorder) : même dessin partout. Blanc, teinté par
// Image.color. Générées une seule fois.
public static class OrnamentSprites
{
    private static Sprite _ruleFaded, _ruleSolid, _ruleSolidVertical;

    // Filet horizontal : cœur net + halo doux. fadeEnds = s'estompe sur les 25 % de chaque bout (barre isolée) ; sinon
    // opaque d'un bout à l'autre (bords d'un cadre, qui se rejoignent aux coins).
    public static Sprite Rule(bool fadeEnds)
    {
        if (fadeEnds) return _ruleFaded != null ? _ruleFaded : (_ruleFaded = MakeRule(true));
        return _ruleSolid != null ? _ruleSolid : (_ruleSolid = MakeRule(false));
    }

    // Même filet plein, dessiné à la verticale (bords gauche / droit d'un cadre, sans rotation d'objet).
    public static Sprite RuleVertical => _ruleSolidVertical != null ? _ruleSolidVertical : (_ruleSolidVertical = MakeRule(false, true));

    // Texture 128 x 16 : cœur de ~6 px sur 16 (≈ 3 px à l'écran pour 8 px d'épaisseur) + halo doux.
    private static Sprite MakeRule(bool fadeEnds, bool vertical = false)
    {
        const int w = 128, h = 16;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float dy = Mathf.Abs(y + 0.5f - h * 0.5f);
            float core = Mathf.Clamp01(3.5f - dy);
            float glow = Mathf.Exp(-dy * dy / 16f) * 0.35f;
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                float ends = fadeEnds ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(u, 1f - u) / 0.25f)) : 1f;
                float a = Mathf.Max(core, glow) * ends;
                float bright = Mathf.Lerp(0.8f, 1f, core);
                px[y * w + x] = new Color(bright, bright, bright, a);
            }
        }
        if (vertical)
        {
            var tv = new Texture2D(h, w, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pv = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    pv[x * h + y] = px[y * w + x];   // transposée : x de l'horizontale -> y de la verticale
            tv.SetPixels32(pv);
            tv.Apply();
            return Sprite.Create(tv, new Rect(0, 0, h, w), new Vector2(0.5f, 0.5f), 100f);
        }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }
}
