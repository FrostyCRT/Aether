using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// AJOUTÉ (2026-11-11, retour utilisateur sur l'écran de chargement : "les particules bougent trop rapidement, et ce sont juste
// des ronds orange qui flottent, c'est pas pro visuellement, on ne sait pas ce que ça représente") - remplace AmbientDustUI
// (ronds « Knob » orange). Des ÉCLATS DE CRISTAL D'AETHER : le cristal est l'élément du joueur dans tout le jeu (grammaire
// visuelle : cristal = joueur), et celui du logo est bleu ; le violet rappelle Lyra et la tempête.
//  - forme : éclat facetté dessiné en code (pointe courte en haut, longue en bas), face gauche claire, face droite plus
//    sombre, crête centrale lumineuse ; une lueur additive douce derrière chaque éclat ;
//  - mouvement : montée LENTE (14 à 24 s pour traverser la bande), léger balancement, rotation douce, et un basculement
//    « 3D » (l'éclat s'affine puis s'élargit comme s'il tournait sur lui-même) qui fait accrocher la lumière aux facettes ;
//  - profondeur : trois plans (petits, pâles et lents au fond ; plus grands, vifs et un peu plus rapides devant) ;
//  - scintillements : de temps en temps une petite étoile à quatre branches s'allume sur un éclat.
// Deux bandes comme avant (bas de l'écran près de la barre, haut près du logo) : le centre de l'illustration reste libre.
public class AetherShardsUI : MonoBehaviour
{
    [Header("Zone verticale (0 = bas du conteneur, 1 = haut)")]
    [SerializeField] private float _bandMinY = 0f;
    [SerializeField] private float _bandMaxY = 0.3f;
    [Tooltip("Demi-largeur (px de référence) laissée libre au centre : bande du haut, pour ne pas passer devant le logo.")]
    [SerializeField] private float _avoidCenterHalfWidth = 0f;

    [Header("Éclats")]
    [SerializeField] private int _count = 12;   // 24 -> 12 (2026-11-12, « mettre moins de cristaux »)
    // MODIFIÉ (2026-11-13) - 18-40 -> 26-60 px : les éclats peints ont des détails qui se perdaient en tout petit.
    [SerializeField] private Vector2 _lengthRange = new Vector2(26f, 60f);     // hauteur de l'éclat (px de référence 1080p)
    // AJOUTÉ (2026-11-13, retour utilisateur : « rendre ça plus graphique, je peux faire une image ») - éclats peints par
    // l'utilisateur (Eclat_Chargement_A/B, fond détouré) : chacun en tire un au hasard, couleurs d'origine gardées (seule la
    // lueur derrière prend la teinte de la palette). Liste vide = ancien éclat dessiné en code.
    [SerializeField] private Sprite[] _crystalSprites;
    [SerializeField] private Vector2 _lifetimeRange = new Vector2(14f, 24f);   // durée pour traverser la bande
    [SerializeField] private Material _additiveMaterial;                       // UI_Additive : la lueur ajoute de la lumière

    private static readonly Color[] Palette =
    {
        new Color(0.56f, 0.93f, 1.00f),   // cyan clair (cristal du logo)
        new Color(0.42f, 0.70f, 1.00f),   // azur
        new Color(0.80f, 0.60f, 1.00f),   // améthyste
    };
    private static readonly float[] PaletteWeight = { 0.45f, 0.30f, 0.25f };

    private class Shard
    {
        public RectTransform root, body;
        public Image shard, glow;
        public Color tint;
        public float depth;          // 0 = fond, 1 = premier plan
        public float age, lifetime;
        public float x, swayAmp, swayFreq, swayPhase;
        public float spin, angle;
        public float tumbleFreq, tumblePhase;
        public float length;
        public bool painted;
    }

    private class Sparkle
    {
        public RectTransform rect;
        public Image image;
        public float age = -1f, life;
        public Shard host;
    }

    private RectTransform _rect;
    private readonly List<Shard> _shards = new List<Shard>();
    private readonly List<Sparkle> _sparkles = new List<Sparkle>();
    private float _nextSparkle;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        for (int i = 0; i < _count; i++)
        {
            Shard s = CreateShard(i);
            Respawn(s);
            s.age = Random.Range(0f, s.lifetime);   // déjà répartis dans la bande au premier affichage
            _shards.Add(s);
        }
        for (int i = 0; i < 5; i++) _sparkles.Add(CreateSparkle(i));
        _nextSparkle = Random.Range(0.4f, 1.2f);
    }

    // ── Construction ────────────────────────────────────────────────────────────────────────────────────────────────

    private Shard CreateShard(int index)
    {
        var s = new Shard();
        s.root = NewRect("Shard" + index, _rect);
        s.glow = NewImage("Glow", s.root, TreeSprites.Glow, _additiveMaterial);
        s.body = NewRect("Body", s.root);
        s.shard = NewImage("Crystal", s.body, ShardSprite, null);
        Stretch(s.shard.rectTransform);
        return s;
    }

    private Sparkle CreateSparkle(int index)
    {
        var sp = new Sparkle();
        sp.image = NewImage("Sparkle" + index, _rect, SparkleSprite, _additiveMaterial);
        sp.rect = sp.image.rectTransform;
        sp.image.enabled = false;
        return sp;
    }

    private void Respawn(Shard s)
    {
        s.depth = Random.value;
        s.tint = PickColor();
        s.length = Mathf.Lerp(_lengthRange.x, _lengthRange.y, s.depth * 0.75f + Random.value * 0.25f);
        // plus loin = plus lent (parallaxe) : la durée de traversée s'allonge pour les éclats du fond
        s.lifetime = Mathf.Lerp(_lifetimeRange.y, _lifetimeRange.x, s.depth) * Random.Range(0.9f, 1.1f);
        s.age = 0f;
        s.x = RandomX();
        s.swayAmp = Random.Range(10f, 22f);
        s.swayFreq = Random.Range(0.08f, 0.16f);
        s.swayPhase = Random.Range(0f, Mathf.PI * 2f);
        s.angle = Random.Range(-35f, 35f);
        s.spin = Random.Range(6f, 16f) * (Random.value < 0.5f ? -1f : 1f);
        s.tumbleFreq = Random.Range(0.12f, 0.30f);
        s.tumblePhase = Random.Range(0f, Mathf.PI * 2f);

        float w = s.length * 0.5f;
        s.painted = _crystalSprites != null && _crystalSprites.Length > 0;
        if (s.painted)
        {
            Sprite sp = _crystalSprites[Random.Range(0, _crystalSprites.Length)];
            s.shard.sprite = sp;
            if (sp != null) w = s.length * sp.rect.width / sp.rect.height;
        }
        s.body.sizeDelta = new Vector2(w, s.length);
        float glow = s.length * 2.3f;
        s.glow.rectTransform.sizeDelta = new Vector2(glow, glow);
    }

    private float RandomX()
    {
        float half = _rect.rect.width * 0.5f;
        float gap = Mathf.Clamp(_avoidCenterHalfWidth, 0f, half - 40f);
        if (gap <= 0f) return Random.Range(-half, half);
        float side = Random.Range(gap, half);
        return Random.value < 0.5f ? -side : side;
    }

    private static Color PickColor()
    {
        float r = Random.value, acc = 0f;
        for (int i = 0; i < Palette.Length; i++)
        {
            acc += PaletteWeight[i];
            if (r <= acc) return Palette[i];
        }
        return Palette[0];
    }

    // ── Animation ──────────────────────────────────────────────────────────────────────────────────────────────────

    private void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);   // un chargement ne fait jamais « sauter » les éclats
        float height = _rect.rect.height;
        float bottom = -height * 0.5f + _bandMinY * height;
        float top = -height * 0.5f + _bandMaxY * height;

        foreach (Shard s in _shards)
        {
            s.age += dt;
            float t = s.age / s.lifetime;
            if (t >= 1f) { Respawn(s); t = 0f; }

            float sway = Mathf.Sin(s.age * s.swayFreq * Mathf.PI * 2f + s.swayPhase) * s.swayAmp;
            s.root.anchoredPosition = new Vector2(s.x + sway, Mathf.Lerp(bottom - s.length, top + s.length, t));

            s.angle += s.spin * dt;
            s.body.localEulerAngles = new Vector3(0f, 0f, s.angle);

            // basculement : la largeur apparente suit cos(phase) ; la facette fait face à la lumière quand elle est large
            float c = Mathf.Cos(s.age * s.tumbleFreq * Mathf.PI * 2f + s.tumblePhase);
            float face = Mathf.Abs(c);
            s.body.localScale = new Vector3(Mathf.Lerp(0.22f, 1f, face) * Mathf.Sign(c == 0f ? 1f : c), 1f, 1f);

            float edge = t < 0.12f ? t / 0.12f : (t > 0.85f ? (1f - t) / 0.15f : 1f);
            float depthAlpha = Mathf.Lerp(0.70f, 1f, s.depth);
            float glint = 0.80f + 0.45f * Mathf.Pow(face, 6f);   // reflet bref quand la facette est de face
            // éclat peint : ses couleurs d'origine, juste un peu plus sombres au fond et un éclat bref quand il fait face
            Color body = s.painted ? Color.white * Mathf.Lerp(0.72f, 1f, s.depth) * Mathf.Min(1f, glint) : s.tint * glint;
            body.a = edge * depthAlpha;
            s.shard.color = body;

            Color g = s.tint;
            g.a = edge * depthAlpha * (0.38f + 0.30f * Mathf.Pow(face, 4f));
            s.glow.color = g;
        }

        UpdateSparkles(dt);
    }

    private void UpdateSparkles(float dt)
    {
        _nextSparkle -= dt;
        if (_nextSparkle <= 0f)
        {
            _nextSparkle = Random.Range(0.5f, 1.4f);
            Sparkle free = _sparkles.Find(sp => sp.age < 0f);
            Shard host = _shards[Random.Range(0, _shards.Count)];
            if (free != null && host.depth > 0.35f)
            {
                free.host = host;
                free.age = 0f;
                free.life = Random.Range(0.45f, 0.75f);
                free.image.enabled = true;
            }
        }

        foreach (Sparkle sp in _sparkles)
        {
            if (sp.age < 0f) continue;
            sp.age += dt;
            float k = sp.age / sp.life;
            if (k >= 1f) { sp.age = -1f; sp.image.enabled = false; continue; }
            float pop = Mathf.Sin(k * Mathf.PI);                      // monte puis s'éteint
            float size = sp.host.length * (1.1f + 0.9f * pop);
            sp.rect.sizeDelta = new Vector2(size, size);
            // sur la pointe haute de l'éclat
            Vector2 tip = (Vector2)(sp.host.body.localRotation * new Vector3(0f, sp.host.length * 0.42f, 0f));
            sp.rect.anchoredPosition = sp.host.root.anchoredPosition + tip;
            sp.rect.localEulerAngles = new Vector3(0f, 0f, k * 45f);
            Color c = Color.Lerp(sp.host.tint, Color.white, 0.6f);
            c.a = pop * 0.9f;
            sp.image.color = c;
        }
    }

    // ── Sprites générés ────────────────────────────────────────────────────────────────────────────────────────────

    private static Sprite _shardSprite, _sparkleSprite;

    // Éclat facetté (blanc, teinté par l'Image) : losange allongé, pointe haute courte, deux faces et une crête.
    private static Sprite ShardSprite
    {
        get
        {
            if (_shardSprite != null) return _shardSprite;
            const int w = 48, h = 96, ss = 4;
            Vector2 top = new Vector2(0.5f, 1f), right = new Vector2(1f, 0.66f), bottom = new Vector2(0.5f, 0f), left = new Vector2(0f, 0.66f);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float cover = 0f, light = 0f;
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                        {
                            Vector2 p = new Vector2((x + (sx + 0.5f) / ss) / w, (y + (sy + 0.5f) / ss) / h);
                            if (!InQuad(p, top, right, bottom, left)) continue;
                            cover += 1f;
                            bool upper = p.y > 0.66f;
                            bool leftSide = p.x < 0.5f;
                            float l = leftSide ? 0.92f : 0.58f;          // face éclairée / face à l'ombre
                            if (upper) l += leftSide ? 0.08f : 0.16f;   // couronne plus claire
                            float ridge = Mathf.Exp(-Mathf.Pow((p.x - 0.5f) / 0.035f, 2f));
                            l = Mathf.Lerp(l, 1f, ridge * 0.85f);       // fine arête centrale lumineuse
                            light += l;
                        }
                    float a = cover / (ss * ss);
                    float lum = cover > 0f ? Mathf.Clamp01(light / cover) : 0f;
                    // liseré sombre d'environ 2 px : l'éclat se détache du décor chargé, comme les objets de l'illustration
                    Vector2 c = new Vector2((x + 0.5f) / w, (y + 0.5f) / h);
                    if (a > 0f && !InQuad(Shrink(c), top, right, bottom, left)) lum *= 0.30f;
                    px[y * w + x] = new Color(lum, lum, lum, a);
                }
            _shardSprite = MakeSprite(w, h, px);
            return _shardSprite;
        }
    }

    // Point éloigné du centre de l'éclat : s'il sort du losange, le point d'origine est à moins de ~2 px du bord.
    private static Vector2 Shrink(Vector2 p)
    {
        Vector2 center = new Vector2(0.5f, 0.55f);
        Vector2 d = p - center;
        return center + new Vector2(d.x * 1.10f, d.y * 1.045f);
    }

    private static bool InQuad(Vector2 p, Vector2 a, Vector2 b, Vector2 c, Vector2 d) =>
        Side(a, b, p) <= 0f && Side(b, c, p) <= 0f && Side(c, d, p) <= 0f && Side(d, a, p) <= 0f;

    private static float Side(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

    // Étoile à quatre branches fines, cœur lumineux.
    private static Sprite SparkleSprite
    {
        get
        {
            if (_sparkleSprite != null) return _sparkleSprite;
            const int n = 64;
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float arms = Mathf.Exp(-Mathf.Abs(u) * 22f) * Mathf.Exp(-v * v * 2.2f) + Mathf.Exp(-Mathf.Abs(v) * 22f) * Mathf.Exp(-u * u * 2.2f);
                    float core = Mathf.Exp(-(u * u + v * v) * 18f);
                    float a = Mathf.Clamp01(arms + core) * Mathf.Clamp01(1f - (Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)) - 0.85f) / 0.15f);
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            _sparkleSprite = MakeSprite(n, n, px);
            return _sparkleSprite;
        }
    }

    private static Sprite MakeSprite(int w, int h, Color[] px)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.HideAndDontSave };
        t.SetPixels(px);
        t.Apply(true, true);
        Sprite s = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        s.hideFlags = HideFlags.HideAndDontSave;
        return s;
    }

    // ── Utilitaires ────────────────────────────────────────────────────────────────────────────────────────────────

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Material material)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        if (material != null) img.material = material;
        img.raycastTarget = false;
        return img;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
