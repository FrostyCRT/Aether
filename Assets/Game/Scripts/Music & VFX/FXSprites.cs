using UnityEngine;

// Bibliothèque partagée des effets visuels de combat (BossFX, GroundTelegraphFX, ChargeLaneFX) : sprites générés par
// le code (aucune image à importer), matériau commun, courbes d'animation et pose à plat sur le sol.
// Tous les sprites sont BLANCS : la couleur vient du SpriteRenderer (SpriteRenderer.color).
public static class FXSprites
{
    // CORRIGE (2026-10-03) - retour utilisateur : le couloir de charge (et les autres effets de cette bibliotheque)
    // affichait des blocs rectangulaires nets a la place des chevrons/anneaux. Cause trouvee par test empirique :
    // TOUS les sprites proceduraux (carre, chevron, anneau...) partageaient UN SEUL Material. Quand plusieurs
    // SpriteRenderer utilisant des sprites DIFFERENTS (ex. Square pour Base/Fill et Chevron pour les chevrons) mais le
    // MEME Material se superposent, le moteur en affiche certains avec la forme/texture d'un AUTRE (le chevron prenait
    // l'aspect plein du carre) - confirme en isolant : seul(e) le chevron rendait juste ; des que Base (Square) etait
    // present a cote avec le meme Material, le chevron redevenait un bloc plein, meme decale de 1 unite en Y (donc pas
    // un probleme de Z-fighting). Un Material DEDIE par chevron supprimait le bug net. Desormais : un Material par
    // SPRITE DISTINCT (mis en cache), partage seulement entre renderers qui affichent le MEME sprite (ex. les 6
    // chevrons entre eux, ou les ~140 etincelles de BossFX entre elles) - sans danger, deja le cas avant et jamais
    // fautif dans les tests. Impact perf negligeable (une poignee de Material par effet, effets courts/transitoires).
    private static readonly System.Collections.Generic.Dictionary<Sprite, Material> _matBySprite = new System.Collections.Generic.Dictionary<Sprite, Material>();

    private static Sprite _glow, _disc, _thinRing, _tickRing, _square, _chevron, _pillar, _gem;

    // Un Material par sprite (voir commentaire ci-dessus) : jamais partage entre deux sprites DIFFERENTS.
    public static Material SpriteMaterial(Sprite sprite)
    {
        if (sprite == null) return new Material(Shader.Find("Sprites/Default"));
        if (!_matBySprite.TryGetValue(sprite, out Material m) || m == null)
        {
            m = new Material(Shader.Find("Sprites/Default"));
            _matBySprite[sprite] = m;
        }
        return m;
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
        sr.sharedMaterial = SpriteMaterial(sprite);
        sr.sortingOrder = sortingOrder;
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sr.receiveShadows = false;
        sr.color = Color.clear;
        return sr;
    }

    // Pose le sprite à PLAT sur le sol, à un diamètre monde donné, tourné de angleDeg autour de la verticale.
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

    // AJOUTÉ (2026-10-04) - retour utilisateur sur les gemmes d'XP : "une gemme c'est un losange, pas un rond...
    // pas de relief, motif, dessin, rien". Contrairement à TOUS les autres sprites de cette bibliothèque (blancs,
    // la couleur ne vient QUE de SpriteRenderer.color), celui-ci encode un vrai dégradé de LUMINOSITÉ par facette
    // directement dans le RGB (pas seulement l'alpha) : multiplié par une teinte au runtime, les facettes
    // sombres/claires restent lisibles - c'est ce qui donne le relief "pierre taillée" plutôt qu'un aplat uni.
    // Losange élancé (proportions gemme, plus haut que large), 8 facettes à luminosité alternée (aucun dégradé
    // lissé entre elles - un vrai diamant taillé a des arêtes nettes), un reflet décalé en haut à gauche (comme une
    // lumière qui accroche une facette) et un cerne sombre au contour pour détacher la gemme du fond.
    public static Sprite Gem()
    {
        if (_gem != null) return _gem;

        const int n = 160;
        Texture2D tex = NewTexture(n, n, FilterMode.Bilinear);
        // ÉCLAIRCI (2026-10-04, suite 2) - retour utilisateur : "c'est un peu sombre". Plancher remonté nettement
        // (0.45-0.58 -> 0.65-0.78), le contraste entre facettes reste lisible mais plus aucune ne tombe dans le gris foncé.
        float[] facetBrightness = { 0.7f, 1.0f, 0.78f, 1.1f, 0.65f, 0.95f, 0.72f, 1.05f };

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float px = (x + 0.5f) / n * 2f - 1f;
                float py = (y + 0.5f) / n * 2f - 1f;

                // Losange (norme L1), élancé verticalement.
                float dist = Mathf.Abs(px) / 0.72f + Mathf.Abs(py) / 0.95f;
                if (dist >= 1f) { tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f)); continue; }

                float alpha = Smooth((1f - dist) / 0.035f);

                float angle = Mathf.Atan2(py, px);
                int segIndex = Mathf.FloorToInt((angle + Mathf.PI) / (2f * Mathf.PI) * facetBrightness.Length) % facetBrightness.Length;
                float brightness = facetBrightness[segIndex];

                // Profondeur : plus sombre vers le bord, plus clair vers le centre.
                brightness *= Mathf.Lerp(1.2f, 0.88f, Smooth(dist));

                // Reflet décalé (lumière qui accroche une facette).
                float hdx = px + 0.25f;
                float hdy = py - 0.35f;
                float highlight = Smooth(1f - Mathf.Sqrt(hdx * hdx + hdy * hdy) / 0.38f);
                brightness = Mathf.Lerp(brightness, 1.5f, highlight * 0.75f);

                // Cerne sombre au contour (détache la gemme du sol/fond) - adouci pour ne pas trop assombrir l'ensemble.
                float edge = 1f - Smooth((1f - dist) / 0.07f);
                brightness *= Mathf.Lerp(1f, 0.6f, edge);

                tex.SetPixel(x, y, new Color(brightness, brightness, brightness, alpha));
            }
        }
        tex.Apply();
        _gem = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        return _gem;
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