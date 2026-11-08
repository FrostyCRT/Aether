using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// AJOUTE (2026-09-19) - ambiance lumineuse procédurale de l'onglet Compétences, calée sur
// l'illustration de fond (BackgroundV4 : arbre aux trois feuillages orange / vert / turquoise,
// rayons de soleil venant du haut à gauche, poussière dorée, lucioles). Le décor est une
// image fixe ; ce composant y ajoute de la LUMIÈRE VIVANTE, aux endroits où l'illustration
// en suggère déjà :
//   - HALOS de zone (orange à gauche, or au centre, vert, turquoise à droite) qui respirent
//     très lentement, chacun à son rythme ;
//   - RAYONS de soleil : longues bandes douces qui ondulent et changent d'intensité ;
//   - FEUILLES qui tombent lentement, de la couleur du feuillage dont elles viennent ;
//   - BRUME rasante sur les racines, derrière les logos ;
//   - CRISTAUX des trois logos : pulsation + éclat en croix ;
//   - GERBE d'étincelles quand une compétence est achetée (Burst).
//
// MODIFIÉ (2026-11-08) - nouveau fond à trois heures (SkillTreeBackground) : halos recalculés sur ce fond, rayons de soleil
// retirés (l'image n'en a plus). Retour utilisateur : "enlève les petits ronds de couleur, et à la place un peu plus de
// feuilles de couleurs qui tombent" : poussières, étincelles et braises supprimées, 20 feuilles au lieu de 9. Puis
// "tu peux remettre des petits lucioles" : 10 lucioles, 3 fois plus petites qu'avant (18-30 px au lieu de 52-86).
//
// Les lueurs sont dessinées en ADDITIF (matériau Aether/UI/Additive) : elles éclairent le décor
// au lieu de le grisailler, ce qui est la seule façon de les voir sur un fond déjà lumineux.
// Les nœuds et la fiche de détail passent AU-DESSUS : le décor s'anime, les infos restent lisibles.
//
// Deux plans dans TreeContainer (créés en Start, une fois) :
//   FX_Back  : derrière les logos (halos, rayons, brume) ;
//   FX_Front : devant les logos, derrière les nœuds (poussières, lucioles, feuilles, cristaux) ;
//   FX_Burst : au-dessus de tout (gerbes d'achat).
// Choix techniques identiques à CharacterAmbientFX : sprites générés une fois puis détruits,
// teinte/alpha par CanvasRenderer.SetColor (pas de reconstruction de maillage), zéro allocation
// dans Update, Time.unscaledTime (indépendant de timeScale).
[RequireComponent(typeof(RectTransform))]
public class TreeAmbientFX : MonoBehaviour
{
    [System.Serializable]
    public class CrystalSpot
    {
        [Tooltip("Logo dont on éclaire le cristal.")]
        public RectTransform logo;
        [Tooltip("Position du cristal dans le logo, en fraction de sa taille depuis son centre (x, y). y > 0 = vers le haut.")]
        public Vector2 offset = new Vector2(0f, 0.3f);
        public Color color = new Color(0.45f, 0.75f, 1f);
        [HideInInspector] public float phase;
        [HideInInspector] public RectTransform glowRt, glintRt;
        [HideInInspector] public CanvasRenderer glowCr, glintCr;
        [HideInInspector] public float glintStart, glintDuration;
    }

    [Header("Général")]
    [SerializeField] private Material _additiveMaterial;
    [Tooltip("Curseur global : 0 = rien, 1 = réglage par défaut, 2 = deux fois plus visible.")]
    [Range(0f, 2f)] [SerializeField] private float _intensity = 1f;
    [SerializeField] private float _fadeInDuration = 1.6f;

    [Header("Cristaux des logos")]
    [SerializeField] private CrystalSpot[] _crystals;

    private const int LeafCount = 20, FireflyCount = 10;

    // Repère de conception : positions données en pixels de l'illustration 1920x1080
    // (le fond est étiré sur tout l'onglet, l'arbre est donc toujours au même endroit relatif).
    private const float RefW = 1920f, RefH = 1080f, TopBar = 100f;

    private enum Kind { Halo, Firefly, Leaf, Mist }

    private class Item
    {
        public Kind kind;
        public RectTransform rt; public CanvasRenderer cr;
        public Vector2 basePos;           // position de repos (repère TreeContainer)
        public Color rgb;
        public float alpha;               // opacité maximale
        public float size;
        public float freq, phase;         // respiration / scintillement
        public float speed, sway, swayFreq, life, birth, spin;
        public float seedX, seedY;
        public Vector2 region0, region1;  // zone d'apparition
        public float flip;
    }

    private class Spark
    {
        public RectTransform rt; public CanvasRenderer cr;
        public Vector2 pos, vel; public float birth, life, size; public Color rgb;
        public bool alive;
    }

    private readonly List<Item> _items = new List<Item>();
    private const int BurstPool = 48;
    private readonly List<Spark> _burst = new List<Spark>(BurstPool);
    private readonly List<Object> _owned = new List<Object>();

    private RectTransform _rect, _back, _front, _crystalRoot, _burstRoot;
    private Sprite _dotSprite, _glintSprite, _leafSprite, _mistA, _mistB;
    private float _enableTime;
    private bool _built;
    private Color _mistColor = new Color(1f, 0.97f, 0.9f);

    // --- API --------------------------------------------------------------

    public void SetIntensity(float v) => _intensity = Mathf.Max(0f, v);

    // La brume prend la couleur de l'heure du décor (crème en journée, bleutée la nuit).
    public void SetMistColor(Color c) => _mistColor = c;

    // Gerbe d'étincelles autour d'un point (repère TreeContainer : on convertit depuis un RectTransform).
    public void Burst(RectTransform at, Color color, int count = 22)
    {
        if (!_built || at == null) return;
        Vector2 center = ToLocal(at);
        float t = Time.unscaledTime;
        int spawned = 0;
        for (int i = 0; i < _burst.Count && spawned < count; i++)
        {
            Spark s = _burst[i];
            if (s.alive) continue;
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float spd = Random.Range(90f, 320f);
            s.pos = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(28f, 46f);
            s.vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd;
            s.birth = t;
            s.life = Random.Range(0.55f, 1.15f);
            s.size = Random.Range(16f, 40f);
            s.rgb = Color.Lerp(color, Color.white, Random.Range(0.2f, 0.7f));
            s.alive = true;
            spawned++;
        }
    }

    // --- cycle de vie -------------------------------------------------------

    private void Awake() => _rect = (RectTransform)transform;

    private void Start() => EnsureBuilt();

    private void OnEnable() => _enableTime = Time.unscaledTime;

    private void OnDestroy()
    {
        if (_back != null) Destroy(_back.gameObject);
        if (_front != null) Destroy(_front.gameObject);
        if (_crystalRoot != null) Destroy(_crystalRoot.gameObject);
        if (_burstRoot != null) Destroy(_burstRoot.gameObject);
        foreach (Object o in _owned) if (o != null) Destroy(o);
        _owned.Clear();
    }

    private void EnsureBuilt()
    {
        if (_built) return;
        _built = true;

        _dotSprite = MakeSprite(64, (u, v) => Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v)), 1.8f));
        _glintSprite = MakeSprite(64, (u, v) =>
        {
            float core = Mathf.Exp(-(u * u + v * v) * 22f);
            float h = Mathf.Exp(-v * v * 260f) * Mathf.Exp(-u * u * 5f);
            float vert = Mathf.Exp(-u * u * 260f) * Mathf.Exp(-v * v * 5f);
            float edge = 1f - TreeSprites.Smooth(0.85f, 1f, Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)));
            return (core + 0.85f * Mathf.Max(h, vert)) * edge;
        });
        _leafSprite = MakeSprite(64, (u, v) =>
        {
            // feuille pointue : demi-largeur = sin(pi*t), t le long de l'axe v (-1..1 -> 0..1)
            float t = v * 0.5f + 0.5f;
            float half = Mathf.Sin(t * Mathf.PI) * 0.62f;
            float d = Mathf.Abs(u) / Mathf.Max(0.0001f, half);
            float body = 1f - TreeSprites.Smooth(0.82f, 1f, d);
            float vein = 1f - 0.35f * Mathf.Exp(-u * u * 380f);
            return body * vein * (t > 0.02f && t < 0.98f ? 1f : 0f);
        });
        _mistA = MakeMistSprite(192, 3.7f);
        _mistB = MakeMistSprite(192, 41.3f);

        Transform parent = transform;
        _back = CreateRoot("FX_Back", parent);
        _front = CreateRoot("FX_Front", parent);
        // Sans le préfixe FX_ : ces lueurs appartiennent aux logos et s'effacent avec eux en mode contemplation (MenuViewMode).
        _crystalRoot = CreateRoot("CrystalFX", parent);
        _burstRoot = CreateRoot("FX_Burst", parent);

        PlaceRoots();

        BuildHalos();
        BuildMist();
        BuildFireflies();
        BuildLeaves();
        BuildCrystals();
        BuildBurstPool();
    }

    private RectTransform CreateRoot(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    // Ordre dans TreeContainer : [catcher] FX_Back [logos] [liaisons] FX_Front [nœuds] ... FX_Burst
    private void PlaceRoots()
    {
        int firstLogo = int.MaxValue, firstNode = int.MaxValue;
        foreach (CrystalSpot c in _crystals)
            if (c != null && c.logo != null) firstLogo = Mathf.Min(firstLogo, c.logo.GetSiblingIndex());
        foreach (SkillNode n in GetComponentsInChildren<SkillNode>(true))
            firstNode = Mathf.Min(firstNode, n.transform.GetSiblingIndex());
        Transform links = transform.Find("Links");
        if (links != null) firstNode = Mathf.Min(firstNode, links.GetSiblingIndex());

        if (firstLogo != int.MaxValue) _back.SetSiblingIndex(firstLogo);
        else _back.SetSiblingIndex(Mathf.Min(1, transform.childCount - 1));

        // recalcul après l'insertion de FX_Back (les indices ont bougé de 1)
        if (firstNode != int.MaxValue)
        {
            int idx = firstNode + (_back.GetSiblingIndex() <= firstNode ? 1 : 0);
            _front.SetSiblingIndex(Mathf.Min(idx, transform.childCount - 1));
        }
        _crystalRoot.SetSiblingIndex(_front.GetSiblingIndex() + 1);
        _burstRoot.SetAsLastSibling();
    }

    // --- conversions ----------------------------------------------------------

    // pixels de l'illustration 1920x1080 -> repère local de TreeContainer (centré)
    private Vector2 P(float sx, float sy)
    {
        Rect r = _rect.rect;
        float fx = sx / RefW;
        float fy = (sy - TopBar) / (RefH - TopBar);
        return new Vector2((fx - 0.5f) * r.width, (0.5f - fy) * r.height);
    }

    private Vector2 ToLocal(RectTransform other)
    {
        Vector3 world = other.TransformPoint(other.rect.center);
        Vector3 local = _rect.InverseTransformPoint(world);
        return new Vector2(local.x, local.y);
    }

    // --- construction ---------------------------------------------------------

    private Item NewItem(Kind kind, Sprite sprite, Transform parent, bool additive, string name)
    {
        Item it = new Item { kind = kind };
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        it.rt = (RectTransform)go.transform;
        it.rt.anchorMin = it.rt.anchorMax = it.rt.pivot = new Vector2(0.5f, 0.5f);
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        if (additive) img.material = _additiveMaterial;
        it.cr = go.GetComponent<CanvasRenderer>();
        it.cr.SetColor(new Color(1, 1, 1, 0));
        it.seedX = Random.Range(0f, 100f);
        it.seedY = Random.Range(0f, 100f);
        it.phase = Random.Range(0f, Mathf.PI * 2f);
        _items.Add(it);
        return it;
    }

    // REFAIT (2026-11-07, retour utilisateur : "fais le même ajustement sur les autres pages" que les lueurs des Paramètres)
    // - plus de grandes taches rondes : chaque halo est découpé dans l'illustration (feuillage orange, éclat doré du soleil,
    // feuillage vert, feuillage turquoise et sa traînée de paillettes, clairière), textures Resources/TreeGlow générées par
    // Tools/build_menu_glows.py tree. Immobiles, seule l'intensité respire, chacun à son rythme.
    // Pixels de la partie visible de l'illustration (2048 x 1045, sous la barre d'onglets) : texture, rectangle, alpha, période (s).
    private const float ArtW = 2048f, ArtH = 1045f;
    private static readonly (string name, float x, float y, float w, float h, float a, float period)[] ShapedHalos =
    {
        ("canopy_orange",    0f,   0f, 718f, 611f, 0.30f, 11f),
        ("core_orange",    140f,   0f, 470f, 360f, 0.30f,  8f),
        ("canopy_green",   518f,   0f, 994f, 445f, 0.26f, 13f),
        ("low_green_l",    490f, 319f, 348f, 316f, 0.26f,  9f),
        ("low_green_r",   1210f, 255f, 334f, 380f, 0.26f, 12f),
        ("canopy_blue",   1362f,   0f, 686f, 643f, 0.24f, 10f),
        ("core_blue",     1470f,   0f, 578f, 390f, 0.18f,  7f),
    };

    private struct ShapedHalo { public CanvasRenderer cr; public float alpha, freq, phase; }
    private readonly List<ShapedHalo> _shapedHalos = new List<ShapedHalo>();

    private void BuildHalos()
    {
        foreach (var s in ShapedHalos)
        {
            CanvasRenderer cr = MenuShapedGlow.Create(_back, "TreeGlow/" + s.name, s.x, s.y, s.w, s.h, ArtW, ArtH, _additiveMaterial);
            if (cr == null) continue;
            _shapedHalos.Add(new ShapedHalo { cr = cr, alpha = s.a, freq = Mathf.PI * 2f / s.period, phase = Random.Range(0f, Mathf.PI * 2f) });
        }
    }

    private void BuildMist()
    {
        for (int i = 0; i < 4; i++)
        {
            Item it = NewItem(Kind.Mist, (i % 2 == 0) ? _mistA : _mistB, _back, false, "GroundMist");
            float sx = Mathf.Lerp(250f, 1670f, i / 3f);
            it.basePos = P(sx, Random.Range(900f, 985f));
            it.rt.sizeDelta = new Vector2(Random.Range(700f, 980f), Random.Range(150f, 210f));
            it.rt.anchoredPosition = it.basePos;
            if (Random.value < 0.5f) it.rt.localScale = new Vector3(-1f, 1f, 1f);
            it.rgb = new Color(1f, 0.97f, 0.9f);
            it.alpha = 0.14f;
            it.sway = Random.Range(60f, 130f);
            it.swayFreq = Random.Range(0.035f, 0.06f);
            it.freq = Random.Range(0.10f, 0.20f);
        }
    }

    private void BuildFireflies()
    {
        for (int i = 0; i < FireflyCount; i++)
        {
            Item it = NewItem(Kind.Firefly, TreeSprites.Glow, _front, true, "Firefly");
            it.rgb = i % 3 == 2 ? new Color(0.7f, 1f, 0.6f) : new Color(1f, 0.92f, 0.5f);
            it.basePos = P(Random.Range(120f, 1800f), Random.Range(480f, 860f));
            it.size = Random.Range(18f, 30f);
            it.rt.sizeDelta = new Vector2(it.size, it.size);
            it.alpha = 0.8f;
            it.sway = Random.Range(60f, 140f);
            it.swayFreq = Random.Range(0.03f, 0.07f);
            it.freq = Random.Range(0.5f, 1.1f);          // pulsation lente
        }
    }

    private void BuildLeaves()
    {
        for (int i = 0; i < LeafCount; i++)
        {
            Item it = NewItem(Kind.Leaf, _leafSprite, _front, false, "Leaf");
            float sx = Mathf.Lerp(120f, 1800f, (i + Random.value * 0.6f) / LeafCount);
            it.region0 = P(sx, 130f);
            it.region1 = P(sx + 60f, 260f);
            it.size = Random.Range(18f, 28f);
            it.rt.sizeDelta = new Vector2(it.size * 0.7f, it.size);
            it.alpha = 0.85f;
            Respawn(it, Time.unscaledTime, true);
        }
    }

    private void BuildCrystals()
    {
        if (_crystals == null) return;
        foreach (CrystalSpot c in _crystals)
        {
            if (c == null || c.logo == null) continue;
            c.phase = Random.Range(0f, 6.28f);

            GameObject g = new GameObject("CrystalGlow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            g.transform.SetParent(_crystalRoot, false);
            c.glowRt = (RectTransform)g.transform;
            c.glowRt.sizeDelta = new Vector2(120f, 120f);
            Image gi = g.GetComponent<Image>(); gi.sprite = TreeSprites.Glow; gi.material = _additiveMaterial; gi.raycastTarget = false;
            c.glowCr = g.GetComponent<CanvasRenderer>();

            GameObject k = new GameObject("CrystalGlint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            k.transform.SetParent(_crystalRoot, false);
            c.glintRt = (RectTransform)k.transform;
            c.glintRt.sizeDelta = new Vector2(84f, 84f);
            Image ki = k.GetComponent<Image>(); ki.sprite = _glintSprite; ki.material = _additiveMaterial; ki.raycastTarget = false;
            c.glintCr = k.GetComponent<CanvasRenderer>();

            c.glowCr.SetColor(new Color(1, 1, 1, 0));
            c.glintCr.SetColor(new Color(1, 1, 1, 0));
            c.glintStart = Time.unscaledTime + Random.Range(1f, 5f);
            c.glintDuration = Random.Range(1.6f, 2.4f);
        }
    }

    private void BuildBurstPool()
    {
        for (int i = 0; i < BurstPool; i++)
        {
            GameObject go = new GameObject("Spark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_burstRoot, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            Image img = go.GetComponent<Image>(); img.sprite = _dotSprite; img.material = _additiveMaterial; img.raycastTarget = false;
            CanvasRenderer cr = go.GetComponent<CanvasRenderer>();
            cr.SetColor(new Color(1, 1, 1, 0));
            _burst.Add(new Spark { rt = rt, cr = cr });
        }
    }

    // --- respawn ----------------------------------------------------------------

    private void Respawn(Item it, float t, bool randomAge)
    {
        switch (it.kind)
        {
            case Kind.Leaf:
            {
                it.life = Random.Range(30f, 44f);
                it.birth = t - (randomAge ? Random.Range(0f, it.life) : 0f);
                it.basePos = new Vector2(Random.Range(it.region0.x, it.region1.x), Random.Range(it.region0.y, it.region1.y));
                it.speed = Random.Range(16f, 28f);          // chute px/s
                it.sway = Random.Range(30f, 70f);
                it.swayFreq = Random.Range(0.25f, 0.5f);
                it.spin = Random.Range(-38f, 38f);
                float fx = (it.basePos.x / _rect.rect.width) + 0.5f;    // 0 gauche .. 1 droite
                Color leaf = fx < 0.36f ? new Color(0.96f, 0.48f, 0.14f)
                           : fx < 0.66f ? new Color(0.78f, 0.86f, 0.28f)
                           : new Color(0.32f, 0.62f, 1f);
                it.rgb = Color.Lerp(leaf, Color.white, Random.Range(0f, 0.2f));
                break;
            }
        }
    }

    // --- boucle -----------------------------------------------------------------

    private void Update()
    {
        if (!_built) return;
        float t = Time.unscaledTime;
        float fade = _fadeInDuration <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, (t - _enableTime) / _fadeInDuration);
        float k = _intensity * fade * GameSettings.MenuEffectsFactor;   // Paramètres > Graphismes > Effets d'ambiance

        for (int i = 0; i < _items.Count; i++)
        {
            Item it = _items[i];
            switch (it.kind)
            {
                case Kind.Mist: UpdateMist(it, t, k); break;
                case Kind.Firefly: UpdateFirefly(it, t, k); break;
                case Kind.Leaf: UpdateLeaf(it, t, k); break;
            }
        }

        for (int i = 0; i < _shapedHalos.Count; i++)
        {
            ShapedHalo h = _shapedHalos[i];
            float breathe = 0.62f + 0.38f * Mathf.Sin(t * h.freq + h.phase);
            h.cr.SetColor(new Color(1f, 1f, 1f, h.alpha * breathe * k));
        }

        UpdateCrystals(t, k);
        UpdateBurst(t, k);
    }

    private static float Env(float u, float inEnd, float outStart)
        => Mathf.SmoothStep(0f, 1f, u / inEnd) * (1f - Mathf.SmoothStep(0f, 1f, (u - outStart) / (1f - outStart)));

    private void UpdateMist(Item it, float t, float k)
    {
        float nx = Mathf.PerlinNoise(it.seedX, t * it.swayFreq) - 0.5f;
        it.rt.anchoredPosition = it.basePos + new Vector2(nx * 2f * it.sway, 0f);
        float breathe = 0.6f + 0.4f * Mathf.Sin(t * it.freq + it.phase);
        it.cr.SetColor(new Color(_mistColor.r, _mistColor.g, _mistColor.b, it.alpha * breathe * k));
    }

    private void UpdateFirefly(Item it, float t, float k)
    {
        Vector2 p = it.basePos + new Vector2(
            (Mathf.PerlinNoise(it.seedX, t * it.swayFreq) - 0.5f) * 2f * it.sway,
            (Mathf.PerlinNoise(it.seedY, t * it.swayFreq + 30f) - 0.5f) * 2f * it.sway * 0.6f);
        // pulsation : longues plages éteintes puis montée douce (comme une vraie luciole)
        float s = Mathf.Sin(t * it.freq + it.phase);
        float pulse = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.2f, 0.9f, s));
        it.rt.anchoredPosition = p;
        it.cr.SetColor(new Color(it.rgb.r, it.rgb.g, it.rgb.b, it.alpha * (0.12f + 0.88f * pulse) * k));
    }

    private void UpdateLeaf(Item it, float t, float k)
    {
        float age = t - it.birth;
        float u = age / it.life;
        if (u >= 1f) { Respawn(it, t, false); age = 0f; u = 0f; }

        float env = Env(u, 0.06f, 0.88f);
        float sway = Mathf.Sin(age * it.swayFreq + it.phase);
        it.rt.anchoredPosition = new Vector2(it.basePos.x + sway * it.sway, it.basePos.y - it.speed * age);
        it.rt.localRotation = Quaternion.Euler(0f, 0f, age * it.spin + sway * 35f);
        it.cr.SetColor(new Color(it.rgb.r, it.rgb.g, it.rgb.b, it.alpha * env * Mathf.Min(1f, k)));
    }

    private void UpdateCrystals(float t, float k)
    {
        if (_crystals == null) return;
        foreach (CrystalSpot c in _crystals)
        {
            if (c == null || c.logo == null || c.glowRt == null) continue;

            Rect lr = c.logo.rect;
            Vector3 world = c.logo.TransformPoint(new Vector3(lr.center.x + c.offset.x * lr.width, lr.center.y + c.offset.y * lr.height, 0f));
            Vector3 local = _rect.InverseTransformPoint(world);
            Vector2 pos = new Vector2(local.x, local.y);

            float pulse = 0.55f + 0.45f * Mathf.Sin(t * 1.15f + c.phase);
            c.glowRt.anchoredPosition = pos;
            c.glowRt.localScale = Vector3.one * (0.9f + 0.18f * pulse);
            c.glowCr.SetColor(new Color(c.color.r, c.color.g, c.color.b, 0.55f * pulse * k));

            float u = (t - c.glintStart) / c.glintDuration;
            if (u > 1f)
            {
                c.glintStart = t + Random.Range(2.5f, 6f);
                c.glintDuration = Random.Range(1.6f, 2.4f);
                u = -1f;
            }
            if (u < 0f) { c.glintCr.SetColor(new Color(1, 1, 1, 0)); continue; }

            float bell = Mathf.Sin(u * Mathf.PI); bell *= bell;
            c.glintRt.anchoredPosition = pos;
            c.glintRt.localScale = Vector3.one * (0.55f + 0.6f * bell);
            c.glintRt.localRotation = Quaternion.Euler(0f, 0f, u * 20f);
            Color gc = Color.Lerp(c.color, Color.white, 0.7f);
            c.glintCr.SetColor(new Color(gc.r, gc.g, gc.b, 0.95f * bell * k));
        }
    }

    private void UpdateBurst(float t, float k)
    {
        for (int i = 0; i < _burst.Count; i++)
        {
            Spark s = _burst[i];
            if (!s.alive) continue;
            float age = t - s.birth;
            float u = age / s.life;
            if (u >= 1f) { s.alive = false; s.cr.SetColor(new Color(1, 1, 1, 0)); continue; }

            // freinage progressif + légère retombée
            float drag = 1f - Mathf.Pow(u, 0.6f) * 0.85f;
            s.pos += s.vel * drag * Time.unscaledDeltaTime + new Vector2(0f, -22f * Time.unscaledDeltaTime * u);
            float size = s.size * (1f - 0.55f * u);
            s.rt.anchoredPosition = s.pos;
            s.rt.sizeDelta = new Vector2(size, size);
            s.cr.SetColor(new Color(s.rgb.r, s.rgb.g, s.rgb.b, 0.95f * (1f - u) * (1f - u) * Mathf.Max(0.6f, k)));
        }
    }

    // --- sprites ----------------------------------------------------------------

    private Sprite MakeSprite(int size, System.Func<float, float, float> alphaAt)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alphaAt(u, v)) * 255f + 0.5f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        _owned.Add(tex);
        _owned.Add(sprite);
        return sprite;
    }

    private Sprite MakeMistSprite(int size, float seed)
    {
        return MakeSprite(size, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float mask = 1f - TreeSprites.Smooth(0.30f, 1f, r);
            if (mask <= 0f) return 0f;
            float n = 0f, amp = 0.5f, freq = 2.2f, norm = 0f;
            for (int o = 0; o < 4; o++)
            {
                n += amp * Mathf.PerlinNoise(seed + (u * 0.5f + 0.5f) * freq, seed * 0.7f + (v * 0.5f + 0.5f) * freq);
                norm += amp; amp *= 0.5f; freq *= 2f;
            }
            n /= norm;
            n = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.26f, 0.76f, n));
            return n * mask;
        });
    }
}
