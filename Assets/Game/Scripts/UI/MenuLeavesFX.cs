using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// AJOUTE (2026-09-20) - VIE DU DÉCOR du menu principal : feuilles emportées par le vent, quelques oiseaux lointains,
// grains de poussière dans la lumière. Aucune lueur : tout est fait de sprites normaux (voir MenuSprites).
//
// - FEUILLES : nées dans la cime de l'arbre (à droite) ou arrivant de hors-champ, elles traversent la cour vers la
//   gauche. Le vent les pousse : une rafale (MenuBackgroundController.Gust) les accélère, les soulève et les fait
//   tourbillonner ; elles tournent, se retournent (le sens « recto / verso » se voit à leur largeur) et s'éteignent
//   en touchant le sol ou en sortant de l'écran. Petites = lointaines (lentes, pâles), grandes = proches (rapides).
// - OISEAUX : de temps en temps, une petite volée traverse le ciel, ailes qui battent (2 images), très discrète.
// - POUSSIÈRE : quelques grains clairs qui dérivent doucement avec la brise.
//
// Positionné sur le décor lui-même (MenuBackgroundController.ArtToLocal) : l'arbre reste l'origine des feuilles
// quel que soit le format d'écran. Respecte « Effets d'ambiance des menus » (désactivé = rien ne s'affiche).
[RequireComponent(typeof(RectTransform))]
public class MenuLeavesFX : MonoBehaviour
{
    [SerializeField] private MenuBackgroundController _background;
    [SerializeField] private int _leafCount = 26;
    [SerializeField] private int _speckCount = 22;

    private static readonly Color[] LeafColors =
    {
        new Color(0.97f, 0.63f, 0.22f),   // orange (feuillage de l'arbre)
        new Color(0.98f, 0.80f, 0.32f),   // or
        new Color(0.62f, 0.76f, 0.28f),   // vert clair
        new Color(0.38f, 0.62f, 0.24f),   // vert
        new Color(0.86f, 0.44f, 0.18f),   // roux
    };

    private class Item
    {
        public RectTransform rt; public CanvasRenderer cr; public Image img;
        public float x, y, vx, vy, size, rot, spin, phase, freq, flip, life, age, alphaMax, depth;
        public Color color; public bool active;
    }

    // Un oiseau = une racine + 2 ailes (même sprite, l'une en miroir) qui pivotent autour de l'épaule.
    private class Bird
    {
        public RectTransform root, left, right;
        public CanvasRenderer crLeft, crRight;
        public float x, y;
    }

    private readonly List<Item> _leaves = new List<Item>();
    private readonly List<Item> _specks = new List<Item>();
    private readonly List<Bird> _birds = new List<Bird>();
    private RectTransform _rect;
    private float _birdTimer = 4f;
    private float _enable;
    private bool _needInit = true;      // la taille de la page n'est connue qu'après la 1re mise en page

    private void Awake()
    {
        _rect = (RectTransform)transform;
        if (_background == null) _background = GetComponentInParent<MenuBackgroundController>();
        for (int i = 0; i < _leafCount; i++) _leaves.Add(NewItem("Leaf", MenuSprites.Leaf(i)));
        for (int i = 0; i < _speckCount; i++) _specks.Add(NewItem("Speck", MenuSprites.Speck));
        for (int i = 0; i < FormationSlots.Length; i++) _birds.Add(NewBird());
    }

    private void OnEnable()
    {
        _enable = Time.unscaledTime;
        _needInit = true;
        HideBirds();
        _flockActive = false;
        _birdTimer = Random.Range(3f, 8f);
    }

    private Item NewItem(string name, Sprite sprite)
    {
        Item it = new Item();
        Setup(it, name, sprite);
        return it;
    }

    private void Setup(Item it, string name, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(_rect, false);
        it.rt = (RectTransform)go.transform;
        it.rt.anchorMin = it.rt.anchorMax = it.rt.pivot = new Vector2(0.5f, 0.5f);
        it.img = go.GetComponent<Image>();
        it.img.sprite = sprite;
        it.img.raycastTarget = false;
        it.img.preserveAspect = true;
        it.cr = go.GetComponent<CanvasRenderer>();
        it.cr.SetColor(new Color(1, 1, 1, 0));
    }

    private Rect Bounds() => _rect.rect;

    // ---- naissance ----------------------------------------------------------------------------------------------
    private void Respawn(Item l, bool scatter)
    {
        Rect b = Bounds();
        l.depth = Random.value;                                    // 0 = lointain, 1 = proche
        l.size = Mathf.Lerp(14f, 46f, l.depth * l.depth * 0.6f + l.depth * 0.4f) * b.height / 980f;
        Sprite s = MenuSprites.Leaf(Random.Range(0, 3));
        l.img.sprite = s;
        l.rt.sizeDelta = new Vector2(l.size * 1.7f, l.size * 0.85f);

        // origine : cime de l'arbre (60 %) ou hors-champ à droite (40 %)
        Vector2 p;
        if (Random.value < 0.6f) p = _background != null ? _background.ArtToLocal(Random.Range(1480f, 1890f), Random.Range(560f, 820f)) : new Vector2(b.xMax - 200f, b.center.y + 60f);
        else p = new Vector2(b.xMax + Random.Range(20f, 140f), Random.Range(b.center.y - 40f, b.yMax - 60f));
        l.x = p.x; l.y = p.y;
        if (scatter)                                              // au tout début, réparties sur tout l'écran
        {
            l.x = Random.Range(b.xMin, b.xMax);
            l.y = Random.Range(b.yMin + 100f, b.yMax - 40f);
        }

        float spd = Mathf.Lerp(38f, 98f, l.depth) * b.width / 1920f;
        l.vx = -spd * Random.Range(0.75f, 1.25f);
        l.vy = -Random.Range(6f, 26f) * (0.5f + l.depth);
        l.rot = Random.Range(0f, 360f);
        l.spin = Random.Range(-110f, 110f);
        l.phase = Random.Range(0f, 6.28f);
        l.freq = Random.Range(0.9f, 2.0f);
        l.flip = Random.Range(1.4f, 3.0f);
        l.life = Random.Range(11f, 20f);
        l.age = scatter ? Random.Range(0f, l.life * 0.7f) : 0f;
        l.alphaMax = Mathf.Lerp(0.55f, 0.95f, l.depth);
        l.color = LeafColors[Random.Range(0, LeafColors.Length)];
        // les feuilles lointaines sont un peu délavées par la distance
        l.color = Color.Lerp(new Color(0.85f, 0.88f, 0.9f), l.color, 0.55f + 0.45f * l.depth);
        l.active = true;
    }

    private void RespawnSpeck(Item s, bool scatter)
    {
        Rect b = Bounds();
        s.size = Random.Range(2.5f, 6f) * b.height / 980f;
        s.rt.sizeDelta = new Vector2(s.size, s.size);
        s.x = scatter ? Random.Range(b.xMin, b.xMax) : b.xMax + Random.Range(10f, 80f);
        s.y = Random.Range(b.yMin + 60f, b.center.y + 160f);
        s.vx = -Random.Range(12f, 40f);
        s.vy = Random.Range(-4f, 8f);
        s.phase = Random.Range(0f, 6.28f);
        s.freq = Random.Range(0.4f, 1.1f);
        s.life = Random.Range(12f, 24f);
        s.age = scatter ? Random.Range(0f, s.life) : 0f;
        s.alphaMax = Random.Range(0.25f, 0.6f);
        s.color = Random.value < 0.7f ? new Color(1f, 0.96f, 0.84f) : new Color(1f, 1f, 1f);
        s.active = true;
    }

    // ---- boucle -------------------------------------------------------------------------------------------------
    private void Update()
    {
        if (_needInit)
        {
            if (_rect.rect.width < 10f) return;
            _needInit = false;
            foreach (Item l in _leaves) Respawn(l, true);
            foreach (Item s in _specks) RespawnSpeck(s, true);
        }

        float factor = GameSettings.MenuEffectsFactor;
        bool on = factor > 0.01f;
        float dt = Time.unscaledDeltaTime;
        float t = Time.unscaledTime;
        float intro = Mathf.SmoothStep(0f, 1f, (t - _enable) / 1.2f);
        float gust = _background != null ? _background.Gust : 0.15f;
        Rect b = Bounds();
        float groundY = _background != null ? _background.ArtToLocal(0f, 1120f).y : b.yMin + 80f;

        // feuilles
        for (int i = 0; i < _leaves.Count; i++)
        {
            Item l = _leaves[i];
            if (!on) { l.cr.SetColor(new Color(1, 1, 1, 0)); continue; }
            l.age += dt;
            float wind = 0.50f + gust * 1.05f;                     // rafale : jusqu'à ~1,5 x plus vite
            l.x += l.vx * wind * dt;
            float lift = Mathf.Sin(t * l.freq + l.phase) * (12f + 20f * gust) + gust * 12f * Mathf.Sin(t * 0.9f + l.phase * 2f);
            l.y += (l.vy + lift) * dt;
            l.rot += (l.spin * (1f + gust * 1.0f) + Mathf.Sin(t * l.freq * 1.7f + l.phase) * 55f) * dt;
            l.rt.localRotation = Quaternion.Euler(0f, 0f, l.rot);
            float flip = 0.38f + 0.62f * Mathf.Abs(Mathf.Cos(t * l.flip + l.phase));        // recto / verso
            l.rt.localScale = new Vector3(1f, flip, 1f);
            l.rt.anchoredPosition = new Vector2(l.x, l.y);

            float u = l.age / l.life;
            float env = Mathf.SmoothStep(0f, 1f, u / 0.08f) * (1f - Mathf.SmoothStep(0f, 1f, (u - 0.85f) / 0.15f));
            float ground = Mathf.SmoothStep(0f, 1f, (l.y - groundY) / 40f);                     // s'éteint en touchant le sol
            l.cr.SetColor(new Color(l.color.r, l.color.g, l.color.b, l.alphaMax * env * ground * intro));

            if (u >= 1f || l.x < b.xMin - 80f || l.y < groundY - 30f) Respawn(l, false);
        }

        // poussières
        for (int i = 0; i < _specks.Count; i++)
        {
            Item s = _specks[i];
            if (!on) { s.cr.SetColor(new Color(1, 1, 1, 0)); continue; }
            s.age += dt;
            float wind = 0.55f + gust * 0.8f;
            s.x += s.vx * wind * dt;
            s.y += (s.vy + Mathf.Sin(t * s.freq + s.phase) * 9f) * dt;
            s.rt.anchoredPosition = new Vector2(s.x, s.y);
            float u = s.age / s.life;
            float env = Mathf.SmoothStep(0f, 1f, u / 0.15f) * (1f - Mathf.SmoothStep(0f, 1f, (u - 0.8f) / 0.2f));
            float tw = 0.75f + 0.25f * Mathf.Sin(t * 1.3f + s.phase);
            s.cr.SetColor(new Color(s.color.r, s.color.g, s.color.b, s.alphaMax * env * tw * intro));
            if (u >= 1f || s.x < b.xMin - 30f) RespawnSpeck(s, false);
        }

        UpdateBirds(on, dt, t, b, factor);
    }

    // ---- oiseaux ------------------------------------------------------------------------------------------------
    // Une volée = 3 oiseaux en V, TOUJOURS la même : mêmes positions relatives, même vitesse, même battement (décalé d'une
    // fraction de cycle d'un rang à l'autre), même ondulation de vol. Rien n'est aléatoire dans le mouvement, donc les
    // oiseaux gardent leurs distances et ne se croisent jamais ; une nouvelle volée ne part que quand la précédente a fini.
    private struct Slot
    {
        public float dx, dy, scale, phase;
        public Slot(float dx, float dy, float scale, float phase) { this.dx = dx; this.dy = dy; this.scale = scale; this.phase = phase; }
    }

    private static readonly Slot[] FormationSlots =
    {
        new Slot(0f, 0f, 1.00f, 0.00f),          // tête
        new Slot(-74f, 27f, 0.88f, 0.08f),
        new Slot(-74f, -27f, 0.88f, 0.08f),
    };

    private const float WingLength = 34f;                    // longueur d'une aile de l'oiseau de tête (px, page de 980 de haut)
    private const float FlapHz = 1.1f;                       // battements par seconde (identique pour tous)
    private const float WingUpDeg = 38f, WingDownDeg = -34f; // de l'aile haute à l'aile basse
    private const float FlightSpeed = 52f;                   // px/s (identique pour tous)
    // Couloir de vol : centre de la volée à 64..76 px SOUS le haut de la page. Au-dessus du logo (dont la pointe commence
    // ~75 px sous le haut) et jamais derrière la barre d'onglets : l'oiseau le plus haut reste à > 25 px du bord, ailes levées comprises.
    private const float TopMargin = 64f, LaneRange = 12f;
    // Bord droit de la tour de gauche ~ x = 450 dans l'illustration : les oiseaux apparaissent (fondu) au-delà.
    private const float FlockFadeInStart = 520f, FlockFadeInLength = 260f;

    private bool _flockActive;
    private float _flockX, _flockLane, _flockTime;

    private Bird NewBird()
    {
        Bird b = new Bird();
        GameObject root = new GameObject("Bird", typeof(RectTransform));
        root.transform.SetParent(_rect, false);
        b.root = (RectTransform)root.transform;
        b.root.anchorMin = b.root.anchorMax = b.root.pivot = new Vector2(0.5f, 0.5f);
        b.right = NewWing("WingR", b.root, out b.crRight);
        b.left = NewWing("WingL", b.root, out b.crLeft);
        b.left.localScale = new Vector3(-1f, 1f, 1f);        // aile gauche = miroir de la droite
        b.root.gameObject.SetActive(false);
        return b;
    }

    private RectTransform NewWing(string name, Transform parent, out CanvasRenderer cr)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(MenuSprites.WingShoulderU, 0.5f);       // pivot = l'épaule
        rt.anchoredPosition = Vector2.zero;
        Image img = go.GetComponent<Image>();
        img.sprite = MenuSprites.Wing;
        img.raycastTarget = false;
        cr = go.GetComponent<CanvasRenderer>();
        cr.SetColor(new Color(0, 0, 0, 0));
        return rt;
    }

    private void HideBirds()
    {
        foreach (Bird b in _birds) b.root.gameObject.SetActive(false);
    }

    private void UpdateBirds(bool on, float dt, float t, Rect b, float factor)
    {
        if (!on)
        {
            if (_flockActive) { _flockActive = false; HideBirds(); }
            return;
        }
        Rect art = _background != null ? _background.ArtRect() : b;
        float k = b.height / 980f;

        if (!_flockActive)
        {
            _birdTimer -= dt;
            if (_birdTimer > 0f) return;
            // départ : juste avant la zone de fondu (à droite de la tour), dans le couloir sous la barre d'onglets
            _flockActive = true;
            _flockTime = 0f;
            _flockX = _background != null ? _background.ArtToLocal(FlockFadeInStart, 0f).x : b.xMin + 300f;
            _flockLane = b.yMax - (TopMargin + Random.Range(0f, LaneRange)) * k;
            foreach (Bird bd in _birds) bd.root.gameObject.SetActive(true);
        }

        _flockTime += dt;
        _flockX += FlightSpeed * k * dt;
        float bob = Mathf.Sin(_flockTime * 0.7f) * 7f * k;              // ondulation de vol commune à toute la volée
        bool anyVisible = false;

        for (int i = 0; i < _birds.Count; i++)
        {
            Bird bd = _birds[i];
            Slot sl = FormationSlots[i];
            bd.x = _flockX + sl.dx * k;
            bd.y = _flockLane + bob + sl.dy * k;

            // battement : rotation CONTINUE des deux ailes (aucune image intermédiaire), même cycle pour tous, décalé par rang
            float phase = Mathf.Repeat(_flockTime * FlapHz - sl.phase, 1f);
            float s = Mathf.Sin(phase * Mathf.PI * 2f);                 // 1 = aile haute, -1 = aile basse
            float mid = (WingUpDeg + WingDownDeg) * 0.5f, amp = (WingUpDeg - WingDownDeg) * 0.5f;
            float ang = mid + amp * s;
            float lift = -Mathf.Cos(phase * Mathf.PI * 2f) * 2.4f * k * sl.scale;      // le corps monte pendant l'abaissement des ailes
            bd.root.anchoredPosition = new Vector2(bd.x, bd.y + lift);
            float len = WingLength * k * sl.scale;
            float full = len / (1f - MenuSprites.WingShoulderU);
            Vector2 size = new Vector2(full, full * MenuSprites.WingAspect);
            bd.right.sizeDelta = size;
            bd.left.sizeDelta = size;
            bd.right.localRotation = Quaternion.Euler(0f, 0f, ang);
            bd.left.localRotation = Quaternion.Euler(0f, 0f, -ang);     // (échelle x = -1 : l'angle s'inverse)

            // apparition en fondu APRÈS le bâtiment de gauche, disparition en fondu à droite
            float artX = art.width > 1f ? (bd.x - art.xMin) / art.width * MenuBackgroundController.ArtWidth : 0f;
            float u = Mathf.Clamp01((bd.x - b.xMin) / b.width);
            float edge = Mathf.SmoothStep(0f, 1f, (artX - FlockFadeInStart) / FlockFadeInLength) * (1f - Mathf.SmoothStep(0f, 1f, (u - 0.90f) / 0.10f));
            Color c = new Color(0.10f, 0.10f, 0.16f, 0.85f * edge);
            bd.crRight.SetColor(c);
            bd.crLeft.SetColor(c);
            if (bd.x < b.xMax + 60f) anyVisible = true;
        }

        if (!anyVisible)                                                  // toute la volée est sortie : on attend la suivante
        {
            _flockActive = false;
            HideBirds();
            _birdTimer = Random.Range(14f, 28f);
        }
    }
}
