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
//   - POUSSIÈRE d'or qui monte dans les rayons, ÉTINCELLES turquoise qui scintillent dans
//     le feuillage de droite, BRAISES orange qui montent à gauche ;
//   - LUCIOLES : grosses lueurs chaudes/froides qui errent dans la clairière ;
//   - FEUILLES qui tombent lentement, de la couleur du feuillage dont elles viennent ;
//   - BRUME rasante sur les racines, derrière les logos ;
//   - CRISTAUX des trois logos : pulsation + éclat en croix ;
//   - GERBE d'étincelles quand une compétence est achetée (Burst).
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

    [Header("Densités")]
    [SerializeField] private int _goldMotes = 34;
    [SerializeField] private int _cyanSparks = 26;
    [SerializeField] private int _embers = 16;
    [SerializeField] private int _fireflies = 7;
    [SerializeField] private int _leaves = 9;

    // Repère de conception : positions données en pixels de l'illustration 1920x1080
    // (le fond est étiré sur tout l'onglet, l'arbre est donc toujours au même endroit relatif).
    private const float RefW = 1920f, RefH = 1080f, TopBar = 100f;

    private enum Kind { Halo, Ray, Mote, Spark, Ember, Firefly, Leaf, Mist }

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
        public float angle;               // rayons : inclinaison de repos
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

    private RectTransform _rect, _back, _front, _burstRoot;
    private Sprite _dotSprite, _glintSprite, _leafSprite, _mistA, _mistB;
    private float _enableTime;
    private bool _built;

    // --- API --------------------------------------------------------------

    public void SetIntensity(float v) => _intensity = Mathf.Max(0f, v);

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
        _burstRoot = CreateRoot("FX_Burst", parent);

        PlaceRoots();

        BuildHalos();
        BuildRays();
        BuildMist();
        BuildMotes();
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

    private void BuildHalos()
    {
        // (x, y illustration), taille, couleur, alpha, période de respiration (s)
        // Placés sur les zones de lumière de l'illustration : canopée orange, soleil doré,
        // feuillage vert, canopée turquoise, clairière éclairée par les rayons.
        AddHalo(330, 270, 720, 560, new Color(1f, 0.60f, 0.20f), 0.34f, 11f);
        AddHalo(730, 300, 660, 560, new Color(1f, 0.87f, 0.45f), 0.40f, 9f);
        AddHalo(1010, 250, 560, 440, new Color(0.55f, 1f, 0.62f), 0.20f, 13f);
        AddHalo(1500, 280, 720, 560, new Color(0.35f, 0.85f, 1f), 0.34f, 10f);
        AddHalo(1720, 600, 460, 460, new Color(0.40f, 0.85f, 1f), 0.16f, 14f);
        AddHalo(760, 800, 820, 380, new Color(1f, 0.85f, 0.5f), 0.20f, 12f);
    }

    private void AddHalo(float sx, float sy, float w, float h, Color color, float alpha, float period)
    {
        Item it = NewItem(Kind.Halo, TreeSprites.Glow, _back, true, "Halo");
        it.basePos = P(sx, sy);
        it.rt.anchoredPosition = it.basePos;
        it.rt.sizeDelta = new Vector2(w, h);
        it.rgb = color;
        it.alpha = alpha;
        it.freq = Mathf.PI * 2f / period;
        it.sway = Random.Range(10f, 24f);
        it.swayFreq = 0.05f + Random.value * 0.03f;
    }

    private void BuildRays()
    {
        // (haut x, haut y) -> (bas x, bas y) sur l'illustration, largeur, alpha
        AddRay(430, 110, 690, 790, 210, 0.16f);
        AddRay(560, 100, 850, 800, 130, 0.14f);
        AddRay(660, 110, 970, 770, 260, 0.12f);
        AddRay(330, 130, 560, 760, 110, 0.10f);
    }

    private void AddRay(float tx, float ty, float bx, float by, float width, float alpha)
    {
        Vector2 top = P(tx, ty), bottom = P(bx, by);
        Item it = NewItem(Kind.Ray, TreeSprites.Ray, _back, true, "Ray");
        Vector2 dir = top - bottom;                      // le sprite est vertical : son haut pointe vers la source
        it.basePos = (top + bottom) * 0.5f;
        it.angle = Mathf.Atan2(dir.x, dir.y) * -Mathf.Rad2Deg;
        it.rt.anchoredPosition = it.basePos;
        it.rt.sizeDelta = new Vector2(width, dir.magnitude * 1.12f);
        it.rgb = new Color(1f, 0.93f, 0.68f);
        it.alpha = alpha;
        it.freq = Mathf.PI * 2f / Random.Range(7f, 12f);
        it.sway = Random.Range(1.2f, 2.6f);              // ondulation de l'angle en degrés
        it.swayFreq = Random.Range(0.06f, 0.12f);
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

    private void BuildMotes()
    {
        for (int i = 0; i < _goldMotes; i++)
            SpawnMote(Kind.Mote, _front, new Color(1f, 0.86f, 0.48f), P(470, 250), P(1000, 820), 0.9f, new Vector2(5f, 13f), true);
        for (int i = 0; i < _cyanSparks; i++)
            SpawnMote(Kind.Spark, _front, new Color(0.62f, 0.95f, 1f), P(1300, 170), P(1900, 720), 0.9f, new Vector2(4f, 11f), true);
        for (int i = 0; i < _embers; i++)
            SpawnMote(Kind.Ember, _front, new Color(1f, 0.66f, 0.3f), P(90, 260), P(560, 820), 0.85f, new Vector2(4f, 10f), true);
    }

    private void SpawnMote(Kind kind, Transform parent, Color rgb, Vector2 p0, Vector2 p1, float alpha, Vector2 sizeRange, bool randomAge)
    {
        Item it = NewItem(kind, _dotSprite, parent, true, kind.ToString());
        it.rgb = rgb;
        it.alpha = alpha;
        it.region0 = new Vector2(Mathf.Min(p0.x, p1.x), Mathf.Min(p0.y, p1.y));
        it.region1 = new Vector2(Mathf.Max(p0.x, p1.x), Mathf.Max(p0.y, p1.y));
        it.size = Random.Range(sizeRange.x, sizeRange.y);
        if (Random.value < 0.22f) { it.size *= Random.Range(1.8f, 2.6f); it.alpha *= 0.55f; }
        it.rt.sizeDelta = new Vector2(it.size, it.size);
        Respawn(it, Time.unscaledTime, randomAge);
    }

    private void BuildFireflies()
    {
        for (int i = 0; i < _fireflies; i++)
        {
            Item it = NewItem(Kind.Firefly, TreeSprites.Glow, _front, true, "Firefly");
            bool cool = i % 3 == 2;
            it.rgb = cool ? new Color(0.6f, 0.92f, 1f) : new Color(1f, 0.9f, 0.5f);
            it.basePos = P(Random.Range(120f, 1800f), Random.Range(560f, 880f));
            it.size = Random.Range(52f, 86f);
            it.rt.sizeDelta = new Vector2(it.size, it.size);
            it.alpha = 0.55f;
            it.sway = Random.Range(60f, 140f);
            it.swayFreq = Random.Range(0.03f, 0.07f);
            it.freq = Random.Range(0.5f, 1.1f);          // pulsation lente
        }
    }

    private void BuildLeaves()
    {
        for (int i = 0; i < _leaves; i++)
        {
            Item it = NewItem(Kind.Leaf, _leafSprite, _front, false, "Leaf");
            float sx = Mathf.Lerp(120f, 1800f, (i + Random.value * 0.6f) / _leaves);
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
            g.transform.SetParent(_front, false);
            c.glowRt = (RectTransform)g.transform;
            c.glowRt.sizeDelta = new Vector2(120f, 120f);
            Image gi = g.GetComponent<Image>(); gi.sprite = TreeSprites.Glow; gi.material = _additiveMaterial; gi.raycastTarget = false;
            c.glowCr = g.GetComponent<CanvasRenderer>();

            GameObject k = new GameObject("CrystalGlint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            k.transform.SetParent(_front, false);
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
            case Kind.Mote:
            case Kind.Spark:
            case Kind.Ember:
            {
                it.life = it.kind == Kind.Spark ? Random.Range(6f, 12f) : Random.Range(9f, 18f);
                it.birth = t - (randomAge ? Random.Range(0f, it.life) : 0f);
                it.basePos = new Vector2(Random.Range(it.region0.x, it.region1.x), Random.Range(it.region0.y, it.region1.y));
                it.speed = it.kind == Kind.Spark ? Random.Range(-4f, 6f) : Random.Range(7f, 20f);   // px/s vers le haut
                it.sway = Random.Range(8f, 26f);
                it.swayFreq = Random.Range(0.25f, 0.65f);
                it.freq = Random.Range(0.5f, 1.4f);
                break;
            }
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
                           : new Color(0.38f, 0.86f, 0.82f);
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
                case Kind.Halo: UpdateHalo(it, t, k); break;
                case Kind.Ray: UpdateRay(it, t, k); break;
                case Kind.Mist: UpdateMist(it, t, k); break;
                case Kind.Firefly: UpdateFirefly(it, t, k); break;
                case Kind.Leaf: UpdateLeaf(it, t, k); break;
                default: UpdateMote(it, t, k); break;
            }
        }

        UpdateCrystals(t, k);
        UpdateBurst(t, k);
    }

    private static float Env(float u, float inEnd, float outStart)
        => Mathf.SmoothStep(0f, 1f, u / inEnd) * (1f - Mathf.SmoothStep(0f, 1f, (u - outStart) / (1f - outStart)));

    private void UpdateHalo(Item it, float t, float k)
    {
        float breathe = 0.62f + 0.38f * Mathf.Sin(t * it.freq + it.phase);
        Vector2 drift = new Vector2((Mathf.PerlinNoise(it.seedX, t * it.swayFreq) - 0.5f) * 2f * it.sway,
                                    (Mathf.PerlinNoise(it.seedY, t * it.swayFreq) - 0.5f) * 2f * it.sway);
        it.rt.anchoredPosition = it.basePos + drift;
        it.cr.SetColor(new Color(it.rgb.r, it.rgb.g, it.rgb.b, it.alpha * breathe * k));
    }

    private void UpdateRay(Item it, float t, float k)
    {
        float breathe = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(t * it.freq + it.phase));
        float ang = it.angle + Mathf.Sin(t * it.swayFreq * 6.28f + it.phase) * it.sway;
        it.rt.localRotation = Quaternion.Euler(0f, 0f, ang);
        it.cr.SetColor(new Color(it.rgb.r, it.rgb.g, it.rgb.b, it.alpha * breathe * k));
    }

    private void UpdateMist(Item it, float t, float k)
    {
        float nx = Mathf.PerlinNoise(it.seedX, t * it.swayFreq) - 0.5f;
        it.rt.anchoredPosition = it.basePos + new Vector2(nx * 2f * it.sway, 0f);
        float breathe = 0.6f + 0.4f * Mathf.Sin(t * it.freq + it.phase);
        it.cr.SetColor(new Color(it.rgb.r, it.rgb.g, it.rgb.b, it.alpha * breathe * k));
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

    private void UpdateMote(Item it, float t, float k)
    {
        float age = t - it.birth;
        float u = age / it.life;
        if (u >= 1f) { Respawn(it, t, false); age = 0f; u = 0f; }

        float env = Env(u, 0.2f, 0.6f);
        float twinkle = 0.65f + 0.35f * Mathf.Sin(age * it.freq * 1.6f + it.phase);
        if (it.kind == Kind.Spark) twinkle = 0.25f + 0.75f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(age * it.freq * 2.1f + it.phase), 2f);

        it.rt.anchoredPosition = new Vector2(
            it.basePos.x + Mathf.Sin(age * it.swayFreq + it.phase) * it.sway,
            it.basePos.y + it.speed * age);
        it.cr.SetColor(new Color(it.rgb.r, it.rgb.g, it.rgb.b, it.alpha * env * twinkle * k));
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
