using UnityEngine;

// AJOUTE (2026-09-20) - petits sprites du fond du menu principal (feuilles, oiseaux, poussières), générés en code :
// aucun asset à importer, aucun problème de compression / d'alpha (même approche que TreeSprites). Créés à la première
// demande, partagés, recréés si Unity les a détruits. Tous sont NORMAUX (pas additifs) : ce sont des objets du décor,
// pas des lueurs.
public static class MenuSprites
{
    private static Sprite[] _leaves;
    private static Sprite _speck;

    public static Sprite Leaf(int variant)
    {
        if (_leaves == null || _leaves.Length == 0 || _leaves[0] == null)
            _leaves = new[] { MakeLeaf(0.90f, 0.62f), MakeLeaf(1.15f, 0.82f), MakeLeaf(0.75f, 0.50f) };
        return _leaves[Mathf.Abs(variant) % _leaves.Length];
    }

    // AILE d'oiseau en croissant (silhouette de mouette, comme sur la référence) : épaisse à l'épaule, effilée en pointe,
    // légèrement courbée vers le bas. UN SEUL sprite, tourné autour de l'épaule par MenuLeavesFX : le battement est une
    // vraie rotation continue (aucune image intermédiaire, donc fluide à n'importe quelle cadence d'affichage).
    // Le sprite va de l'épaule (à gauche, u = ShoulderU) à la pointe (à droite) ; l'épaule est le pivot de rotation.
    public const float WingShoulderU = 0.05f;
    public const float WingAspect = 112f / 192f;          // hauteur / largeur du sprite
    private static Sprite _wing;

    public static Sprite Wing
    {
        get
        {
            if (_wing != null) return _wing;
            const int w = 192, h = 112;
            Texture2D tex = NewTex(w, h);
            float bow = 0.095f * w;        // courbure : le milieu de l'aile est plus bas que ses extrémités
            float root = 0.118f * w;       // demi-épaisseur à l'épaule (aile bien pleine, comme sur la référence)
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float v = y + 0.5f - h * 0.5f;                                        // px, vers le haut
                    float local = Mathf.Clamp01((u - WingShoulderU) / (1f - WingShoulderU));   // 0 à l'épaule, 1 à la pointe
                    float yc = -bow * Mathf.Sin(Mathf.PI * Mathf.Pow(local, 0.85f));      // ligne médiane
                    // épaisseur : forte près de l'épaule, en fuseau effilé jusqu'à une pointe fine ; le bord du haut est un peu plus plat
                    float taper = Mathf.Pow(1f - local, 0.85f);
                    float half = root * taper * (0.7f + 0.3f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(local * 1.6f)));
                    float top = half * 0.8f, bottom = half * 1.15f;
                    float d = v > yc ? (v - yc) - top : (yc - v) - bottom;               // < 0 = dedans
                    float alpha = 1f - Smooth(-0.9f, 0.9f, d);
                    if (u < WingShoulderU) alpha = 0f;                                    // rien à gauche de l'épaule
                    if (local >= 1f) alpha = 0f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            tex.Apply();
            _wing = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            _wing.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return _wing;
        }
    }

    public static Sprite Speck
    {
        get
        {
            if (_speck != null) return _speck;
            const int n = 16;
            Texture2D tex = NewTex(n, n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, 1f - Smooth(0.55f, 0.95f, r)));   // petit grain net, pas de halo
                }
            tex.Apply();
            _speck = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            _speck.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return _speck;
        }
    }

    // ---- fabrication ----------------------------------------------------------------------------------------

    private static Sprite MakeLeaf(float length, float width)
    {
        const int w = 96, h = 48;
        Texture2D tex = NewTex(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;                       // 0 = queue, 1 = pointe
                float v = ((y + 0.5f) / h) * 2f - 1f;           // -1 .. 1 en travers
                float half = width * Mathf.Sin(Mathf.PI * Mathf.Pow(Mathf.Clamp01(u), 0.8f)) * (1f - 0.25f * u);
                half = Mathf.Max(half, u < 0.09f ? 0.07f : 0f);  // la queue
                float edge = 1f - Smooth(half - 0.10f, half, Mathf.Abs(v));
                if (u > 0.985f) edge = 0f;
                float shade = 0.86f + 0.14f * v;                 // dessus plus clair
                float vein = 1f - 0.22f * (1f - Smooth(0f, 0.09f, Mathf.Abs(v)));      // nervure centrale plus sombre
                float side = 0.5f + 0.5f * Mathf.Sin((u * 9f - Mathf.Abs(v) * 2.2f) * Mathf.PI);
                float veins = 1f - 0.07f * side * (1f - Smooth(0.15f, 0.75f, Mathf.Abs(v)));   // nervures latérales très discrètes
                float c = Mathf.Clamp01(shade * vein * veins);
                tex.SetPixel(x, y, new Color(c, c, c, edge));
            }
        tex.Apply();
        Sprite s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        s.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return s;
    }

    private static Texture2D NewTex(int w, int h)
    {
        return new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontUnloadUnusedAsset
        };
    }

    private static float Smooth(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }
}
