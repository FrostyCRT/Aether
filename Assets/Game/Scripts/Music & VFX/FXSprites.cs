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

    private static Sprite _glow, _disc, _thinRing, _tickRing, _square, _chevron, _pillar, _gem, _flameBurst, _crystalShard, _splinter, _shatterRing;
    private static readonly Sprite[] _crystalFragments = new Sprite[CrystalFragmentVariants];

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

    // AJOUTÉ (2026-10-21, retour utilisateur sur la refonte de la Boule de feu : "c'est juste un rond [...] ça doit
    // être très reconnaissable [que c'est une déflagration de boule de feu]") - voir FireExplosionVFX.cs. Forme de
    // déflagration en POINTES DE FLAMME irrégulières, pas un disque/anneau géométrique : contour défini par une
    // somme de 3 harmoniques angulaires d'amplitude décroissante et déphasées (jamais de Random ici - la texture
    // est générée UNE SEULE fois et mise en cache, elle doit toujours ressortir identique ; la variété vient de
    // FireExplosionVFX qui superpose 2 instances de ce même sprite tournées différemment). Cœur plein commun à
    // toutes les pointes (coefficient 0,42 du rayon max local), qui s'effile ensuite vers chaque pointe - se lit
    // comme des langues de flamme qui jaillissent du centre, jamais comme un symbole géométrique régulier (une
    // étoile à N branches identiques se serait lue comme une icône, pas comme du feu).
    public static Sprite FlameBurst()
    {
        if (_flameBurst != null) return _flameBurst;

        const int n = 256;
        Texture2D tex = NewTexture(n, n, FilterMode.Bilinear);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float px = (x + 0.5f) / n * 2f - 1f;
                float py = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(px * px + py * py);
                float angle = Mathf.Atan2(py, px);

                float lobes = 0.60f
                    + 0.20f * Mathf.Sin(angle * 5f + 0.6f)
                    + 0.12f * Mathf.Sin(angle * 8f + 2.4f)
                    + 0.08f * Mathf.Sin(angle * 13f + 4.0f);
                float maxR = Mathf.Clamp(lobes, 0.30f, 1f);
                float hub = maxR * 0.42f;

                float alpha;
                if (r <= hub) alpha = 1f;
                else if (r >= maxR) alpha = 0f;
                else alpha = Smooth(1f - (r - hub) / (maxR - hub));

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        _flameBurst = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        return _flameBurst;
    }

    // AJOUTÉ (2026-10-22, aura de Surpuissance de CrystalSystem/GameUI, retour utilisateur : "tu pourrais faire
    // quelque chose d'aussi précis graphiquement [que la déflagration de Boule de feu] ?") - mêmes principes que
    // FlameBurst ci-dessus (contour irrégulier par harmoniques angulaires, jamais de Random - texture générée une
    // seule fois et mise en cache, la variété vient de l'animation qui superpose/tourne plusieurs instances), mais
    // volontairement DIFFÉRENT en silhouette pour ne jamais se confondre avec une explosion de Boule de feu à
    // l'écran : hub beaucoup plus petit (0,16 du rayon local contre 0,42 pour FlameBurst) -> pointes plus longues
    // et plus fines, moins "boule de pétales" ; chute d'alpha en loi de puissance (pas un simple lissage) -> bords
    // plus nets, lecture "cristal taillé" plutôt que "flamme molle". Facettes à luminosité alternée BAKÉES dans le
    // RGB (même technique que Gem() plus haut) en plus de l'alpha : donne un vrai scintillement facetté une fois
    // teinté en or au runtime, pas un aplat uni comme FlameBurst.
    public static Sprite CrystalShard()
    {
        if (_crystalShard != null) return _crystalShard;

        const int n = 256;
        Texture2D tex = NewTexture(n, n, FilterMode.Bilinear);
        float[] facetBrightness = { 0.78f, 1.2f, 0.85f, 1.35f, 0.72f, 1.1f };

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float px = (x + 0.5f) / n * 2f - 1f;
                float py = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(px * px + py * py);
                float angle = Mathf.Atan2(py, px);

                float lobes = 0.42f
                    + 0.32f * Mathf.Sin(angle * 4f + 1.1f)
                    + 0.16f * Mathf.Sin(angle * 7f + 3.2f)
                    + 0.09f * Mathf.Sin(angle * 11f + 0.4f);
                float maxR = Mathf.Clamp(lobes, 0.12f, 1f);
                float hub = maxR * 0.16f;

                if (r >= maxR) { tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f)); continue; }

                float alpha = (r <= hub) ? 1f : Mathf.Pow(Smooth(1f - (r - hub) / (maxR - hub)), 1.4f);

                int segIndex = Mathf.FloorToInt((angle + Mathf.PI) / (2f * Mathf.PI) * facetBrightness.Length) % facetBrightness.Length;
                float brightness = facetBrightness[segIndex];

                tex.SetPixel(x, y, new Color(brightness, brightness, brightness, alpha));
            }
        }
        tex.Apply();
        _crystalShard = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        return _crystalShard;
    }

    // AJOUTÉ (2026-10-27, explosion de Fragmentation, voir ArcaneBurstVFX) - éclat de cristal allongé, pointe vers +X
    // (orienter avec SetFlat(sr, longueur, angle de la direction)). Deux facettes de luminosité différente bakées dans le
    // RGB (même technique que Gem/CrystalShard) et une arête claire au milieu : il scintille une fois teinté.
    public static Sprite Splinter()
    {
        if (_splinter != null) return _splinter;

        const int n = 128;
        Texture2D tex = NewTexture(n, n, FilterMode.Bilinear);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float px = (x + 0.5f) / n * 2f - 1f;
                float py = (y + 0.5f) / n * 2f - 1f;
                // Losange asymétrique : talon court à -0,55, pointe longue à +1, plus large à -0,2.
                const float back = -0.55f, widest = -0.2f, maxHalfWidth = 0.22f;
                float half = px < widest
                    ? maxHalfWidth * Mathf.Clamp01((px - back) / (widest - back))
                    : maxHalfWidth * Mathf.Clamp01((1f - px) / (1f - widest));
                if (half <= 0f || Mathf.Abs(py) >= half) { tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f)); continue; }

                float edge = Mathf.Abs(py) / half;                 // 0 sur l'arête, 1 au bord
                float alpha = Smooth((1f - edge) / 0.25f);
                float brightness = py > 0f ? 1.25f : 0.72f;
                brightness += 0.5f * Smooth(1f - edge / 0.18f);    // arête claire
                tex.SetPixel(x, y, new Color(brightness, brightness, brightness, alpha));
            }
        }
        tex.Apply();
        _splinter = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        return _splinter;
    }

    // AJOUTÉ (2026-10-27, retour utilisateur sur l'explosion de Fragmentation : "elle ressemble beaucoup à ce qui a déjà
    // été vu dans les autres télégraphes") - ThinRing/TickRing sont la grammaire des TÉLÉGRAPHES ENNEMIS (GroundTelegraphFX :
    // "danger, sors de là"). Les effets du joueur ne doivent pas les réutiliser. Les deux sprites suivants sont propres
    // aux explosions du joueur (ArcaneBurstVFX) : du cristal facetté et cassé, jamais un cercle lisse ou gradué.

    // Anneau de cristal BRISÉ : couronne épaisse dont le bord intérieur est en dents de cristal (pointes vers le centre),
    // coupée par des fissures inégales, chaque dent en deux facettes de luminosité différente et un liseré sombre au bord
    // intérieur (contraste sur le sol clair). Le bord EXTÉRIEUR est à 93 % du rayon du sprite : pour qu'il tombe à R,
    // utiliser un diamètre de 2R / ShatterRingOuter.
    public const float ShatterRingOuter = 0.93f;
    public static Sprite ShatterRing()
    {
        if (_shatterRing != null) return _shatterRing;

        const int n = 512;
        const int segments = 13;                       // morceaux de couronne séparés par des fissures
        const float aa = 2.2f / n;                     // largeur d'anticrénelage (en rayon normalisé)
        System.Random rng = new System.Random(4127);   // graine fixe : même dessin à chaque lancement

        // Découpage irrégulier du tour : bornes des segments, largeur de chaque fissure (certaines quasi fermées),
        // profondeur des deux dents de chaque segment.
        float[] bounds = new float[segments + 1];
        float acc = 0f;
        float[] weights = new float[segments];
        for (int i = 0; i < segments; i++) { weights[i] = 0.7f + (float)rng.NextDouble() * 0.6f; acc += weights[i]; }
        bounds[0] = 0f;
        for (int i = 0; i < segments; i++) bounds[i + 1] = bounds[i] + weights[i] / acc * Mathf.PI * 2f;
        float[] gap = new float[segments];
        float[] toothA = new float[segments];
        float[] toothB = new float[segments];
        float[] shade = new float[segments];
        for (int i = 0; i < segments; i++)
        {
            gap[i] = rng.NextDouble() < 0.7 ? 0.035f + (float)rng.NextDouble() * 0.05f : 0.012f;
            toothA[i] = 0.07f + (float)rng.NextDouble() * 0.09f;
            toothB[i] = 0.05f + (float)rng.NextDouble() * 0.07f;
            shade[i] = 0.9f + (float)rng.NextDouble() * 0.15f;
        }

        Texture2D tex = NewTexture(n, n, FilterMode.Bilinear);
        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float px = (x + 0.5f) / n * 2f - 1f;
                float py = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(px * px + py * py);
                if (r > ShatterRingOuter + 0.02f || r < 0.6f) { tex.SetPixel(x, y, clear); continue; }

                float ang = Mathf.Atan2(py, px);
                if (ang < 0f) ang += Mathf.PI * 2f;
                int s = 0;
                while (s < segments - 1 && ang >= bounds[s + 1]) s++;
                float a0 = bounds[s], a1 = bounds[s + 1];
                float u = (ang - a0) / (a1 - a0);                       // 0..1 le long du segment

                // Fissures : distance angulaire (en longueur d'arc au rayon r) aux deux bords du segment.
                float halfGap = gap[s] * 0.5f;
                float distStart = (ang - a0) * r, distEnd = (a1 - ang) * r;
                float crack = Mathf.Min(distStart, distEnd) - halfGap;

                // Bord intérieur : deux dents par segment (pics vers le centre à u = 0,25 et 0,75).
                float tooth = u < 0.5f
                    ? toothA[s] * (1f - Mathf.Abs(u - 0.25f) / 0.25f)
                    : toothB[s] * (1f - Mathf.Abs(u - 0.75f) / 0.25f);
                float inner = 0.82f - tooth;
                // Bord extérieur légèrement facetté (plat par moitié de segment, pas un cercle parfait).
                float outer = ShatterRingOuter - 0.012f * Mathf.Abs(Mathf.Sin(u * Mathf.PI * 2f));

                float alpha = Smooth((r - inner) / aa + 0.5f) * Smooth((outer - r) / aa + 0.5f) * Smooth(crack / aa + 0.5f);
                if (alpha <= 0f) { tex.SetPixel(x, y, clear); continue; }

                // Facettes : la pente gauche et la pente droite de chaque dent n'ont pas la même lumière.
                float local = u < 0.5f ? u / 0.5f : (u - 0.5f) / 0.5f;
                float facet = local < 0.5f ? 0.95f : 0.62f;   // < 1 : laisse de la marge au liseré clair (texture plafonnée à 1)
                float brightness = facet * shade[s];
                // Liseré lumineux au bord extérieur, liseré sombre au bord intérieur (contour qui détache du sol).
                brightness += 0.45f * Smooth(1f - (outer - r) / 0.03f);
                brightness *= Mathf.Lerp(0.35f, 1f, Smooth((r - inner) / 0.035f));
                tex.SetPixel(x, y, new Color(brightness, brightness, brightness, alpha));
            }
        }
        tex.Apply();
        _shatterRing = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        return _shatterRing;
    }

    // Fragment de cristal trapu et irrégulier (polygone de 5 à 7 sommets), facetté en éventail depuis un centre décalé
    // (chaque facette a sa propre luminosité), arête claire en haut à gauche, contour sombre. Plusieurs variantes, pour
    // qu'une couronne de fragments ne montre jamais deux fois le même morceau côte à côte.
    public const int CrystalFragmentVariants = 4;
    public static Sprite CrystalFragment(int variant)
    {
        variant = ((variant % CrystalFragmentVariants) + CrystalFragmentVariants) % CrystalFragmentVariants;
        if (_crystalFragments[variant] != null) return _crystalFragments[variant];

        const int n = 128;
        const float aa = 2.2f / n;
        System.Random rng = new System.Random(9001 + variant * 131);
        int count = 5 + rng.Next(3);
        float[] vAng = new float[count];
        float[] vRad = new float[count];
        float start = (float)rng.NextDouble() * Mathf.PI * 2f;
        for (int i = 0; i < count; i++)
        {
            vAng[i] = start + (i + ((float)rng.NextDouble() - 0.5f) * 0.5f) / count * Mathf.PI * 2f;
            vRad[i] = 0.55f + (float)rng.NextDouble() * 0.4f;
        }
        // Centre des facettes décalé : le "sommet" du cristal n'est pas au milieu, il paraît en relief et incliné.
        Vector2 apex = new Vector2(((float)rng.NextDouble() - 0.5f) * 0.25f, ((float)rng.NextDouble() - 0.5f) * 0.25f);
        Vector2[] verts = new Vector2[count];
        float[] facet = new float[count];
        for (int i = 0; i < count; i++)
        {
            verts[i] = new Vector2(Mathf.Cos(vAng[i]), Mathf.Sin(vAng[i])) * vRad[i];
            // Lumière venant d'en haut à gauche : la facette tournée vers elle est claire, l'opposée sombre.
            Vector2 mid = (new Vector2(Mathf.Cos(vAng[i]), Mathf.Sin(vAng[i])) + new Vector2(Mathf.Cos(vAng[(i + 1) % count]), Mathf.Sin(vAng[(i + 1) % count]))).normalized;
            facet[i] = Mathf.Lerp(0.5f, 0.92f, Vector2.Dot(mid, new Vector2(-0.6f, 0.8f)) * 0.5f + 0.5f);
        }

        Texture2D tex = NewTexture(n, n, FilterMode.Bilinear);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / n * 2f - 1f, (y + 0.5f) / n * 2f - 1f);
                // Distance signée au polygone (négative dedans) et facette contenant le point (secteur autour de l'apex).
                float inside = float.MaxValue;
                int sector = 0;
                float bestSector = float.MinValue;
                for (int i = 0; i < count; i++)
                {
                    Vector2 a = verts[i], b = verts[(i + 1) % count];
                    Vector2 e = b - a;
                    Vector2 nrm = new Vector2(e.y, -e.x).normalized;          // normale sortante (sommets dans le sens trigonométrique)
                    float d = Vector2.Dot(p - a, nrm);
                    inside = Mathf.Min(inside, -d);
                    // Secteur : le point est du bon côté des deux rayons apex->a et apex->b.
                    float s1 = Cross(a - apex, p - apex), s2 = Cross(p - apex, b - apex);
                    float score = Mathf.Min(s1, s2);
                    if (score > bestSector) { bestSector = score; sector = i; }
                }
                float alpha = Smooth(inside / aa + 0.5f);
                if (alpha <= 0f) { tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f)); continue; }

                float brightness = facet[sector];
                brightness += 0.35f * Smooth(1f - Mathf.Abs(bestSector) / 0.03f);   // arêtes entre facettes, claires
                brightness *= Mathf.Lerp(0.3f, 1f, Smooth(inside / 0.07f));          // contour sombre
                tex.SetPixel(x, y, new Color(brightness, brightness, brightness, alpha));
            }
        }
        tex.Apply();
        _crystalFragments[variant] = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        return _crystalFragments[variant];
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

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