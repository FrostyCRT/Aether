using UnityEngine;

// Sprites du HUD en jeu, générés en code (aucun fichier d'image à maintenir) — style « métal forgé » médiéval-fantasy :
//   • Frame  : cadre biseauté (bronze éclairé en haut à gauche, liseré d'or, coins coupés), 9 tranches ;
//   • Inset  : forme à coins coupés pleine (fond et remplissage des barres), 9 tranches ;
//   • Sheen  : lumière fine sur le bord d'une barre + ombre en bas (horizontale ou verticale), 9 tranches ;
//   • Ring   : médaillon rond biseauté au centre sombre ;
//   • Disc / Pip : disques (pastille, pions) ; Heart / Bolt / Crystal : icônes.
// Tous sont clairs / neutres : la couleur vient de l'Image qui les affiche.
public static class HudSprites
{
    // Cadre : 48 px, 14 px de bordure, épaisseur totale de la bande 7,2 px (à multiplier par 1/pixelsPerUnitMultiplier).
    public const float FrameThickness = 7.2f;
    private const int FrameSize = 48, FrameBorder = 14;
    private const int InsetSize = 32, InsetBorder = 8;

    private static Sprite _pill, _pillOutline, _diamond, _ringFlat;
    private static Sprite _frame, _inset, _sheenH, _sheenV, _ring, _disc, _pip, _heart, _bolt, _crystal;

    public static Sprite Frame  { get { if (_frame == null)  _frame  = MakeFrame();     return _frame; } }
    public static Sprite Inset  { get { if (_inset == null)  _inset  = MakeInset(0);    return _inset; } }
    public static Sprite SheenH { get { if (_sheenH == null) _sheenH = MakeInset(1);    return _sheenH; } }
    public static Sprite SheenV { get { if (_sheenV == null) _sheenV = MakeInset(2);    return _sheenV; } }
    public static Sprite Ring   { get { if (_ring == null)   _ring   = MakeRing();      return _ring; } }
    public static Sprite Disc   { get { if (_disc == null)   _disc   = MakeDisc(false); return _disc; } }
    public static Sprite Pip    { get { if (_pip == null)    _pip    = MakeDisc(true);  return _pip; } }
    public static Sprite Heart  { get { if (_heart == null)  _heart  = MakeHeart();     return _heart; } }
    public static Sprite Bolt   { get { if (_bolt == null)   _bolt   = MakeBolt();      return _bolt; } }
    // Formes « modernes » (sans biseau) : pastille arrondie pleine, son contour fin (9 tranches) et le losange de l'ultime.
    public static Sprite Pill        { get { if (_pill == null)        _pill        = MakePill(false); return _pill; } }
    public static Sprite PillOutline { get { if (_pillOutline == null) _pillOutline = MakePill(true);  return _pillOutline; } }
    public static Sprite RingFlat    { get { if (_ringFlat == null)    _ringFlat    = MakeRingFlat();  return _ringFlat; } }
    public static Sprite Diamond     { get { if (_diamond == null)     _diamond     = MakeDiamond();   return _diamond; } }
    public static Sprite Crystal{ get { if (_crystal == null)_crystal= MakeCrystal();   return _crystal; } }

    private static Texture2D NewTex(int w, int h)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        t.hideFlags = HideFlags.HideAndDontSave;
        return t;
    }

    private static Sprite Finish(Texture2D t, Color[] px, Vector4 border)
    {
        t.SetPixels(px);
        t.Apply(false, true);
        Sprite s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        s.hideFlags = HideFlags.HideAndDontSave;
        return s;
    }

    private static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    // Couche à bords doux : 1 dans [a,b[, avec 1 px de fondu de chaque côté.
    private static float Band(float d, float a, float b)
    {
        return Mathf.Clamp01(d - a + 0.5f) * Mathf.Clamp01(b - d + 0.5f);
    }

    // ---------- rectangle à coins coupés : distance au bord + lumière du bord le plus proche ----------
    private static void ChamferDist(float px, float py, int n, float cut, out float d, out float light)
    {
        const float r2 = 0.70710678f;
        float top = n - py;
        d = px; light = 0.80f;                                       // gauche
        if (top < d) { d = top; light = 1.00f; }                     // haut
        if (n - px < d) { d = n - px; light = 0.35f; }               // droite
        if (py < d) { d = py; light = 0.25f; }                       // bas
        float c;
        c = (px + top - cut) * r2;               if (c < d) { d = c; light = 0.92f; }   // coin haut-gauche
        c = ((n - px) + top - cut) * r2;         if (c < d) { d = c; light = 0.62f; }   // haut-droite
        c = (px + py - cut) * r2;                if (c < d) { d = c; light = 0.55f; }   // bas-gauche
        c = ((n - px) + py - cut) * r2;          if (c < d) { d = c; light = 0.30f; }   // bas-droite
    }

    private static Color Bevel(float d, float light)
    {
        Color dark = new Color(0.30f, 0.20f, 0.10f), lit = new Color(0.90f, 0.70f, 0.38f);
        Color gold = new Color(1.00f, 0.86f, 0.50f), outline = new Color(0.05f, 0.035f, 0.025f), inner = new Color(0.06f, 0.04f, 0.03f);
        Color c = new Color(0, 0, 0, 0);
        // du bord vers l'intérieur : contour sombre, bande de bronze biseautée, liseré d'or, ombre intérieure
        c = Over(c, outline, Band(d, 0f, 1.2f));
        c = Over(c, Color.Lerp(dark, lit, light), Band(d, 1.2f, 4.2f));
        c = Over(c, Color.Lerp(gold * 0.65f, gold, light), Band(d, 4.2f, 5.2f));
        c = Over(c, inner, Band(d, 5.2f, FrameThickness));
        c.a *= Mathf.Clamp01(d + 0.5f);
        return c;
    }

    private static Color Over(Color under, Color top, float a)
    {
        if (a <= 0f) return under;
        float oa = a + under.a * (1f - a);
        if (oa <= 0f) return under;
        Color r = (top * a + under * under.a * (1f - a)) / oa;
        r.a = oa;
        return r;
    }

    private static Sprite MakeFrame()
    {
        int n = FrameSize;
        Texture2D t = NewTex(n, n);
        Color[] px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d, light;
                ChamferDist(x + 0.5f, y + 0.5f, n, 5f, out d, out light);
                px[y * n + x] = Bevel(d, light);
            }
        return Finish(t, px, new Vector4(FrameBorder, FrameBorder, FrameBorder, FrameBorder));
    }

    // mode 0 = forme pleine blanche ; 1 = lumière horizontale (bord haut clair, bas sombre) ; 2 = lumière verticale (bord gauche clair)
    private static Sprite MakeInset(int mode)
    {
        int n = InsetSize;
        Texture2D t = NewTex(n, n);
        Color[] px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d, light;
                ChamferDist(x + 0.5f, y + 0.5f, n, 3f, out d, out light);
                float mask = Mathf.Clamp01(d + 0.5f);
                if (mode == 0) { px[y * n + x] = new Color(1, 1, 1, mask); continue; }

                float tt = mode == 1 ? 1f - (y + 0.5f) / n : (x + 0.5f) / n;   // 0 = côté éclairé
                if (tt < 0.22f)      px[y * n + x] = new Color(1, 1, 1, 0.34f * (1f - tt / 0.22f) * mask);
                else if (tt > 0.55f) px[y * n + x] = new Color(0, 0, 0, 0.30f * Mathf.Pow((tt - 0.55f) / 0.45f, 1.2f) * mask);
                else                 px[y * n + x] = new Color(0, 0, 0, 0f);
            }
        return Finish(t, px, new Vector4(InsetBorder, InsetBorder, InsetBorder, InsetBorder));
    }

    // ---------- médaillon rond biseauté (centre sombre opaque) ----------
    private static Sprite MakeRing()
    {
        int n = 128;
        Texture2D t = NewTex(n, n);
        Color[] px = new Color[n * n];
        float R = n * 0.5f;
        Vector2 toLight = new Vector2(-1f, 1f).normalized;               // lumière en haut à gauche (y vers le haut)
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Vector2 p = new Vector2(x + 0.5f - R, y + 0.5f - R);
                float dist = p.magnitude, d = R - dist;
                float light = Mathf.Clamp01(0.5f + 0.5f * Vector2.Dot(dist > 0.001f ? p / dist : Vector2.zero, toLight));
                Color dark = new Color(0.30f, 0.20f, 0.10f), lit = new Color(0.92f, 0.72f, 0.40f);
                Color gold = new Color(1.00f, 0.86f, 0.50f);
                Color c = new Color(0, 0, 0, 0);
                c = Over(c, new Color(0.05f, 0.035f, 0.025f), Band(d, 0f, 3f));
                c = Over(c, Color.Lerp(dark, lit, light), Band(d, 3f, 15f));
                c = Over(c, Color.Lerp(gold * 0.65f, gold, light), Band(d, 15f, 19f));
                c = Over(c, new Color(0.05f, 0.035f, 0.025f), Band(d, 19f, 24f));
                Color core = Color.Lerp(new Color(0.20f, 0.15f, 0.11f), new Color(0.07f, 0.05f, 0.04f), Smooth(0f, 1f, dist / (R - 24f)));
                c = Over(c, core, Mathf.Clamp01(d - 24f + 0.5f));
                c.a *= Mathf.Clamp01(d + 0.5f);
                px[y * n + x] = c;
            }
        return Finish(t, px, Vector4.zero);
    }

    // ---------- disque (pastille) ----------
    private static Sprite MakeDisc(bool lit)
    {
        int n = 128;
        Texture2D t = NewTex(n, n);
        Color[] px = new Color[n * n];
        float r = n * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Vector2.Distance(p, new Vector2(r, r));
                float mask = Mathf.Clamp01(r - d + 0.5f);
                float c = 1f;
                if (lit)
                {
                    float rim = Smooth(0.72f, 1f, d / r);
                    float hi = Mathf.Clamp01(1f - Vector2.Distance(p, new Vector2(r * 0.7f, r * 1.4f)) / (r * 0.75f));
                    c = Mathf.Clamp01(1f - rim * 0.30f + hi * hi * 0.18f);
                }
                px[y * n + x] = new Color(c, c, c, mask);
            }
        return Finish(t, px, Vector4.zero);
    }

    // Pastille : 32 px, rayon 15,5 (demi-cercles aux extrémités d'une barre de 2×rayon de haut). outline = liseré de 3 px
    // (à 2× la finesse : 1,5 px à l'écran), clair légèrement bleuté.
    private static Sprite MakePill(bool outline)
    {
        const int n = 32;
        Texture2D t = NewTex(n, n);
        Color[] px = new Color[n * n];
        float r = n * 0.5f - 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // distance signée à un rectangle arrondi plein cadre (rayon r)
                float qx = Mathf.Abs(x + 0.5f - n * 0.5f) - (n * 0.5f - r);
                float qy = Mathf.Abs(y + 0.5f - n * 0.5f) - (n * 0.5f - r);
                float sd = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
                float inside = Mathf.Clamp01(0.5f - sd);
                if (!outline) { px[y * n + x] = new Color(1f, 1f, 1f, inside); continue; }
                float ring = inside * Mathf.Clamp01(sd + 3f + 0.5f);      // bande de 3 px le long du bord
                px[y * n + x] = new Color(0.86f, 0.93f, 1f, ring * 0.92f);
            }
        return Finish(t, px, new Vector4(15f, 15f, 15f, 15f));
    }

    // Anneau plat : liseré de 5 px (sur 128) le long du bord d'un disque, même teinte claire que PillOutline.
    private static Sprite MakeRingFlat()
    {
        const int n = 128;
        Texture2D t = NewTex(n, n);
        Color[] px = new Color[n * n];
        float r = n * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                float inside = Mathf.Clamp01(r - d + 0.5f);
                float ring = inside * Mathf.Clamp01(d - (r - 5f) + 0.5f);
                px[y * n + x] = new Color(0.86f, 0.93f, 1f, ring * 0.92f);
            }
        return Finish(t, px, Vector4.zero);
    }

    // Losange (carré tourné de 45°) à coins légèrement adoucis : liseré clair de 2 px, cœur un peu plus sombre avec
    // une lueur en haut à gauche. Teinté par l'Image (plein = couleur du palier, vide = ton sombre).
    private static Sprite MakeDiamond()
    {
        const int n = 64;
        Texture2D t = NewTex(n, n);
        Color[] px = new Color[n * n];
        const float half = 29f, round = 3f;                 // demi-côté du carré (avant rotation) et rayon des coins
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - n * 0.5f, dy = y + 0.5f - n * 0.5f;
                float u = (dx + dy) * 0.70710678f, v = (dx - dy) * 0.70710678f;
                float b = half / 1.41421356f - round;       // demi-côté du carré tourné, après arrondi des coins
                float qx = Mathf.Abs(u) - b, qy = Mathf.Abs(v) - b;
                float sd = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - round;
                float a = Mathf.Clamp01(0.5f - sd);
                float rim = Mathf.Clamp01(sd + 4f);                                  // 0 au cœur, 1 sur le bord
                float glow = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(dx, dy), new Vector2(-8f, 8f)) / 26f);
                float c = Mathf.Lerp(0.80f + glow * 0.16f, 1f, Smooth(0.45f, 0.9f, rim));
                px[y * n + x] = new Color(c, c, c, a);
            }
        return Finish(t, px, Vector4.zero);
    }

    // ---------- formes vectorielles supersamplées ----------
    private delegate float ShapeFn(float u, float v);   // (u,v) dans [0,1], v = 1 en haut ; retourne une teinte 0..1, ou -1 = hors forme

    private static Sprite MakeShape(int w, int h, ShapeFn fn)
    {
        Texture2D t = NewTex(w, h);
        Color[] px = new Color[w * h];
        const int ss = 4;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float cov = 0f, tint = 0f;
                for (int sy = 0; sy < ss; sy++)
                    for (int sx = 0; sx < ss; sx++)
                    {
                        float u = (x + (sx + 0.5f) / ss) / w;
                        float v = (y + (sy + 0.5f) / ss) / h;
                        float s = fn(u, v);
                        if (s >= 0f) { cov += 1f; tint += s; }
                    }
                float a = cov / (ss * ss);
                float c = cov > 0f ? tint / cov : 1f;
                px[y * w + x] = new Color(c, c, c, a);
            }
        return Finish(t, px, Vector4.zero);
    }

    private static bool InPoly(Vector2[] p, float u, float v)
    {
        bool inside = false;
        for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            if (((p[i].y > v) != (p[j].y > v)) && (u < (p[j].x - p[i].x) * (v - p[i].y) / (p[j].y - p[i].y) + p[i].x))
                inside = !inside;
        return inside;
    }

    private static Sprite MakeHeart()
    {
        return MakeShape(128, 128, (u, v) =>
        {
            float x = (u - 0.5f) * 2.5f;
            float yy = (v - 0.5f) * 2.5f + 0.1f;
            float a = x * x + yy * yy - 1f;
            if (a * a * a - x * x * yy * yy * yy > 0f) return -1f;
            return Mathf.Lerp(0.72f, 1f, Smooth(0f, 1f, v));
        });
    }

    private static Sprite MakeBolt()
    {
        Vector2[] p =
        {
            new Vector2(0.60f, 1.00f), new Vector2(0.20f, 0.46f), new Vector2(0.46f, 0.46f), new Vector2(0.34f, 0.00f),
            new Vector2(0.80f, 0.58f), new Vector2(0.54f, 0.58f), new Vector2(0.74f, 1.00f),
        };
        return MakeShape(128, 128, (u, v) => InPoly(p, u, v) ? Mathf.Lerp(0.72f, 1f, Smooth(0f, 1f, v)) : -1f);
    }

    // Cristal : pointe de quartz allongée (prisme à 3 faces visibles, sommet à facettes, pied en pointe).
    // Image 80x128 (rapport 0,625) : à afficher avec preserveAspect.
    private static Sprite MakeCrystal()
    {
        Vector2[] outline = { new Vector2(0.12f, 0.20f), new Vector2(0.12f, 0.72f), new Vector2(0.50f, 1.00f), new Vector2(0.88f, 0.72f), new Vector2(0.88f, 0.20f), new Vector2(0.50f, 0.00f) };
        Vector2[] mid = { new Vector2(0.38f, 0.063f), new Vector2(0.50f, 0f), new Vector2(0.62f, 0.063f), new Vector2(0.62f, 0.75f), new Vector2(0.50f, 1f), new Vector2(0.38f, 0.75f) };
        Vector2[] leftFace = { new Vector2(0.12f, 0.20f), new Vector2(0.12f, 0.72f), new Vector2(0.38f, 0.75f), new Vector2(0.38f, 0.063f) };
        Vector2[] rightFace = { new Vector2(0.88f, 0.20f), new Vector2(0.88f, 0.72f), new Vector2(0.62f, 0.75f), new Vector2(0.62f, 0.063f) };
        Vector2[] leftTop = { new Vector2(0.12f, 0.72f), new Vector2(0.50f, 1.00f), new Vector2(0.38f, 0.75f) };
        Vector2[] rightTop = { new Vector2(0.88f, 0.72f), new Vector2(0.50f, 1.00f), new Vector2(0.62f, 0.75f) };
        return MakeShape(80, 128, (u, v) =>
        {
            if (!InPoly(outline, u, v)) return -1f;
            if (u > 0.19f && u < 0.25f && v > 0.26f && v < 0.62f) return 1.00f;      // reflet
            if (InPoly(mid, u, v)) return 0.96f;
            if (InPoly(leftTop, u, v)) return 0.90f;
            if (InPoly(rightTop, u, v)) return 0.66f;
            if (InPoly(leftFace, u, v)) return 0.78f;
            if (InPoly(rightFace, u, v)) return 0.48f;
            return 0.7f;
        });
    }
}
