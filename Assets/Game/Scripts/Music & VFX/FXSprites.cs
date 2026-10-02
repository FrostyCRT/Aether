using UnityEngine;

// Bibliothèque partagée des effets visuels de combat (BossFX, GroundTelegraphFX, ChargeLaneFX) : sprites générés par
// le code (aucune image à importer), matériau commun, courbes d'animation et pose à plat sur le sol.
// Tous les sprites sont BLANCS : la couleur vient du SpriteRenderer (SpriteRenderer.color).
public static class FXSprites
{
    private static Material _mat;
    private static Sprite _glow, _disc, _thinRing, _tickRing, _square, _chevron, _pillar;

    // Même matériau que le télégraphe de charge historique de BossBase (Sprites/Default) : fonctionne en 3D, sans éclairage.
    public static Material SpriteMaterial()
    {
        if (_mat == null) _mat = new Material(Shader.Find("Sprites/Default"));
        return _mat;
    }

    // ------------------------------------------------------------------
    // Courbes et petits utilitaires
    // ------------------------------------------------------------------

    public static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    public static Color A(Color c, float alpha)
    {
        c.a = Mathf.Clamp01(alpha);
        return c;
    }

    public static float EaseOutCubic(float t)
    {
        float u = 1f - Mathf.Clamp01(t);
        return 1f - u * u * u;
    }

    public static float EaseOutQuad(float t)
    {
        float u = 1f - Mathf.Clamp01(t);
        return 1f - u * u;
    }

    // Rebond léger : dépasse un peu la valeur finale puis revient.
    public static float EaseOutBack(float t, float c1 = 1.5f)
    {
        float c3 = c1 + 1f;
        float u = Mathf.Clamp01(t) - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    // ------------------------------------------------------------------
    // Création / pose
    // ------------------------------------------------------------------

    public static SpriteRenderer MakeRenderer(Transform parent, string objName, Sprite sprite, int sortingOrder, float localY)
    {
        GameObject g = new GameObject(objName);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = new Vector3(0f, localY, 0f);

        SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sharedMaterial = SpriteMaterial();
        sr.sortingOrder = sortingOrder;
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sr.receiveShadows = false;
        sr.color = Color.clear;
        return sr;
    }

    // Pose le sprite À PLAT sur le sol, à un diamètre monde donné, tourné de angleDeg autour de la verticale.
    public static void SetFlat(SpriteRenderer sr, float diameter, float angleDeg)
    {
        float s = diameter / Mathf.Max(0.01f, sr.sprite.bounds.size.x);
        sr.transform.localScale = new Vector3(s, s, 1f);
        sr.transform.localRotation = Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, 0f, angleDeg);
    }

    // ------------------------------------------------------------------
    // Sprites procéduraux
    // ------------------------------------------------------------------

    private static Texture2D NewTexture(int w, int h, FilterMode filter)
    {
        return new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = filter };
    }

    private static float Band(float r, float center, float halfWidth)
    {
        return Smooth(1f - Mathf.Abs(r - center) / halfWidth);
    }

    private static float RangeMask(float r, float lo, float hi)
    {
        return Smooth((r - lo) / 0.01f) * Smooth((hi - r) / 0.01f);
    }

    // Disque doux (halo, éclat, étincelles).
    public static Sprite Glow()
    {
        if (_glow != null) return _glow;

        const int n = 128;
        Texture2D tex = NewTexture(n, n, FilterMode.Bilinear);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float px = (x + 0.5f) / n * 2f - 1f;
                float py = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(px * px + py * py);
                float a = Smooth(1f - r);
                a *= a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        _glow = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        return _glow;
    }

    // Disque plein, bord légèrement adouci (remplissage des zones de danger).
    public static Sprite Disc()
    {
        if (_disc != null) return _disc;

        const int n = 128;
        Texture2D tex = NewTexture(n, n, FilterMode.Bilinear);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float px = (x + 0.5f) / n * 2f - 1f;
                float py = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(px * px + py * py);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Smooth((1f - r) / 0.05f)));
            }
        }
        tex.Apply();
        _disc = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        return _disc;
    }

    // Anneau fin dont le trait est centré à 94 % du rayon du sprite (donc pour que le trait tombe à R, utiliser un
    // diamètre de 2R / 0,94), avec un léger dégradé vers l'intérieur.
    public static Sprite ThinRing()
    {
        if (_thinRing != null) return _thinRing;

        const int n = 256;
        Texture2D tex = NewTexture(n, n, FilterMode.Bilinear);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float px = (x + 0.5f) / n * 2f - 1f;
                float py = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(px * px + py * py);
                float a = Band(r, 0.94f, 0.028f);
                if (r < 0.97f) a = Mathf.Max(a, 0.18f * Smooth((r - 0.70f) / 0.24f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        _thinRing = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        return _thinRing;
    }

    // Anneaux + graduations (cercle "magique" de repli).
    public static Sprite TickRing()
    {
        if (_tickRing != null) return _tickRing;

        const int n = 256;
        Texture2D tex = NewTexture(n, n, FilterMode.Bilinear);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float px = (x + 0.5f) / n * 2f - 1f;
                float py = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(px * px + py * py);
                float ang = Mathf.Atan2(py, px);

                float a = 0f;
                a = Mathf.Max(a, Band(r, 0.93f, 0.030f));
                a = Mathf.Max(a, Band(r, 0.84f, 0.010f));
                a = Mathf.Max(a, Band(r, 0.62f, 0.012f));

                float seg = (ang + Mathf.PI) / (2f * Mathf.PI) * 24f;
                float tick = Smooth(1f - Mathf.Abs(seg - Mathf.Round(seg)) / 0.07f);
                a = Mathf.Max(a, tick * RangeMask(r, 0.855f, 0.915f));

                float seg2 = (ang + Mathf.PI) / (2f * Mathf.PI) * 12f;
                float tick2 = Smooth(1f - Mathf.Abs(seg2 - Mathf.Round(seg2)) / 0.06f);
                a = Mathf.Max(a, tick2 * RangeMask(r, 0.64f, 0.74f));

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        _tickRing = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        return _tickRing;
    }

    // Carré blanc de 1 x 1 unité exactement (bandes de la zone de charge : on le met à l'échelle largeur x longueur).
    public static Sprite Square()
    {
        if (_square != null) return _square;

        Texture2D tex = NewTexture(4, 4, FilterMode.Point);
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                tex.SetPixel(x, y, Color.white);
        tex.Apply();
        _square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f, 0, SpriteMeshType.FullRect);
        return _square;
    }

    // Chevron "^" de 1 x 1 unité, pointe vers le haut du sprite (donc vers l'avant une fois posé à plat et orienté).
    public static Sprite Chevron()
    {
        if (_chevron != null) return _chevron;

        const int n = 64;
        Texture2D tex = NewTexture(n, n, FilterMode.Bilinear);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f;
                float v = (y + 0.5f) / n * 2f - 1f;
                float au = Mathf.Abs(u);
                float d = Mathf.Abs(v - (0.55f - au * 1.4f));
                float stroke = Smooth(1f - d / 0.16f);
                float limit = Smooth((0.8f - au) / 0.06f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, stroke * limit));
            }
        }
        tex.Apply();
        _chevron = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 64f, 0, SpriteMeshType.FullRect);
        return _chevron;
    }

    // Pilier de lumière : bords doux, s'estompe vers le haut, pivot en bas.
    public static Sprite Pillar()
    {
        if (_pillar != null) return _pillar;

        const int w = 64, h = 256;
        Texture2D tex = NewTexture(w, h, FilterMode.Bilinear);
        for (int y = 0; y < h; y++)
        {
            float v = y / (h - 1f);
            float vertical = Mathf.Pow(1f - v, 0.7f) * Smooth(v / 0.04f);
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f;
                float side = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u)), 1.6f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, side * vertical));
            }
        }
        tex.Apply();
        _pillar = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f, 0, SpriteMeshType.FullRect);
        return _pillar;
    }
}