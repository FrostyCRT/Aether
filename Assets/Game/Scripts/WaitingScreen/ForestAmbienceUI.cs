using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// AJOUTÉ (2026-11-14, retour utilisateur : « des éclats qui bougent comme ça devant ce fond d'écran, je trouve que ce n'est
// pas trop adapté [...] c'est le premier écran que le joueur voit [...] les animations doivent être cohérentes avec le fond »)
// - remplace les éclats flottants (AetherShardsUI, désactivés dans la scène) par une ambiance qui prolonge l'illustration
// (forêt, rayons de soleil en haut à gauche, brume au sol) au lieu de s'y superposer. Choix de l'utilisateur, trois couches :
//  1) brume au sol : deux bandes de brume douce qui dérivent lentement à des vitesses différentes (bas de l'image) ;
//  2) rayons + poussière : les rayons peints respirent très doucement (faisceaux additifs posés dessus) et de fines
//     poussières dorées flottent dedans ;
//  3) feuilles : quelques feuilles mousse / brunes tombent en virevoltant, poussées par le vent vers la droite.
// Tout est généré en code (aucune image à importer) et enfant de l'arrière-plan : le lent zoom / panoramique de
// SlowKenBurnsEffect emporte les effets avec l'image, les rayons restent donc calés sur ceux de l'illustration.
// Temps réel non mis à l'échelle (l'écran de chargement peut tourner pendant un chargement qui bloque le jeu).
// MODIFIÉ (même jour, 2e retour : « la brume n'est pas assez visible, comme les lueurs du soleil, les feuilles bougent trop
// vite et je veux qu'elles disparaissent au bout d'un certain temps ; le vent, je veux qu'on le voie réellement, comme un
// dessin animé ») - brume et rayons renforcés (une 3e bande de brume, faisceaux plus larges), feuilles 2 fois plus lentes
// qui vivent 5 à 9 s (apparition puis disparition en fondu), et 4e couche : traits de vent blancs façon dessin animé
// (WindStreakGraphic) qui se dessinent puis s'effacent en filant vers la droite, dans le sens des feuilles.
// MODIFIÉ (même jour, 3e retour : « le vent trop souvent et toujours la même forme ; la brume et les lumières doivent être
// animées, là on dirait que ça fait partie du fond ; brume moins épaisse ; le vent et les feuilles doivent passer DERRIÈRE
// les personnages, leurs bâtons, leurs lueurs et les loups, comme s'il y avait de la perspective ») :
//  - PROFONDEUR : _foreground = copie de l'illustration où seuls les personnages, bâtons, lueurs et loups sont gardés
//    (détourage automatique BiRefNet + lueurs repérées par leur couleur, voir NOTES.md), posée à sa place exacte par
//    _foregroundAnchorMin/Max. Ordre de dessin : fond peint -> plan arrière (brume du fond, rayons, poussières, feuilles,
//    vent) -> personnages détourés -> plan avant (nappe de brume au ras du sol). Le vent et les feuilles passent donc derrière
//    les personnages et les loups, la brume du sol devant leurs pieds ;
//  - brume plus légère mais qui bouge franchement (dérive 3 à 4 fois plus rapide, nappes en sens opposés, respiration et
//    léger flottement vertical) ; rayons qui pulsent nettement, se balancent et s'élargissent / s'affinent ;
//  - vent : une bourrasque toutes les 2,2 à 4,5 s, 5 formes (ondulation simple, boucle au bout vers le haut ou le bas, boucle
//    au milieu, tourbillon au départ), inclinaison et longueur au hasard, 2e trait seulement une fois sur deux.
[RequireComponent(typeof(RectTransform))]
public class ForestAmbienceUI : MonoBehaviour
{
    [Tooltip("Aether/UI/Additive : les rayons et poussières AJOUTENT de la lumière au lieu de grisailler l'image.")]
    [SerializeField] private Material _additiveMaterial;

    [Header("Brume au sol")]
    [SerializeField] private Color _fogColor = new Color(0.80f, 0.88f, 0.90f, 1f);
    [SerializeField] private float _fogAlphaBack = 0.22f;
    [SerializeField] private float _fogAlphaFront = 0.26f;

    [Header("Rayons de soleil (coordonnées normalisées de l'image : 0,0 = bas gauche)")]
    [SerializeField] private Vector2 _shaftOrigin = new Vector2(0.385f, 1.02f);   // haut des rayons peints
    [SerializeField] private float _shaftAngle = 16f;                              // inclinaison (degrés : > 0 = le bas du faisceau part vers la droite)
    [SerializeField] private float _shaftPeakAlpha = 0.75f;
    [SerializeField] private int _dustCount = 26;

    [Header("Feuilles")]
    [SerializeField] private int _leafCount = 6;
    [SerializeField] private Vector2 _leafSize = new Vector2(22f, 38f);   // px de référence (1080p)
    [SerializeField] private float _wind = 24f;                          // dérive vers la droite (px/s)
    [SerializeField] private Vector2 _leafLife = new Vector2(5f, 9f);     // durée de vie d'une feuille (s), fondu compris

    [Header("Traits de vent")]
    [SerializeField] private int _maxStreaks = 2;
    [SerializeField] private Vector2 _streakInterval = new Vector2(2.2f, 4.5f);   // délai entre deux bourrasques (s) : entre-deux demandé entre 0,9-2 s (trop souvent) et 3,5-7 s (trop lent)
    [SerializeField] private float _streakAlpha = 0.75f;

    [Header("Premier plan (personnages et loups détourés)")]
    [Tooltip("Chargement_PremierPlan.png : l'illustration réduite aux personnages, bâtons, lueurs et loups (alpha = détourage).")]
    [SerializeField] private Sprite _foreground;
    [Tooltip("Emplacement du recadrage dans l'illustration (0-1, 0,0 = bas gauche), calculé à sa création.")]
    [SerializeField] private Vector2 _foregroundAnchorMin = new Vector2(0.13698f, 0.10278f);
    [SerializeField] private Vector2 _foregroundAnchorMax = new Vector2(0.84688f, 0.73426f);

    private RectTransform _rect;
    // Repère fixe 1920 x 1080 (comme l'illustration) mis à l'échelle de l'arrière-plan à chaque image : positions, vitesses
    // et tailles restent justes quelle que soit la résolution, et calées sur l'image même si elle est étirée.
    private static readonly Vector2 Ref = new Vector2(1920f, 1080f);
    private RectTransform _space, _spaceFront;   // plan arrière (derrière les personnages) / plan avant

    private class Fog { public RawImage image; public float speed; public float baseAlpha; public float phase; public float bob; }
    private class Shaft { public Image image; public float baseAlpha; public float phase; public float speed; public float baseAngle; public float baseWidth; }
    private class Dust { public RectTransform rt; public Image image; public Vector2 pos; public Vector2 vel; public float phase; public float size; }
    private class Leaf { public RectTransform rt; public Image image; public Color color; public Vector2 pos; public float fall; public float swayAmp; public float swayFreq; public float spin; public float flip; public float phase; public float size; public float age; public float life; }
    // Une bourrasque = deux traits parallèles (le 2e plus court, un peu en dessous et en retard), comme dans les dessins animés.
    private class Streak { public WindStreakGraphic g, g2; public bool pair; public float age; public float life; public float speed; public Vector2 origin; public Vector2 offset2; }

    private readonly List<Fog> _fogs = new List<Fog>();
    private readonly List<Shaft> _shafts = new List<Shaft>();
    private readonly List<Dust> _dust = new List<Dust>();
    private readonly List<Leaf> _leaves = new List<Leaf>();
    private readonly List<Streak> _streaks = new List<Streak>();
    private float _time, _nextStreak;

    private static readonly Color[] LeafColors =
    {
        new Color(0.42f, 0.55f, 0.22f, 1f),   // vert mousse
        new Color(0.55f, 0.60f, 0.26f, 1f),   // vert olive clair
        new Color(0.58f, 0.40f, 0.18f, 1f),   // brun
        new Color(0.70f, 0.52f, 0.22f, 1f),   // ocre
    };

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _space = NewSpace("Space");
        if (_foreground != null)
        {
            Image fg = new GameObject("Foreground", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            fg.transform.SetParent(_rect, false);
            fg.sprite = _foreground;
            fg.raycastTarget = false;
            RectTransform rt = fg.rectTransform;
            rt.anchorMin = _foregroundAnchorMin;
            rt.anchorMax = _foregroundAnchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        _spaceFront = NewSpace("SpaceFront");
        FitSpace();
        BuildFog();
        BuildShafts();
        BuildDust();
        BuildLeaves();
        BuildStreaks();
    }

    private RectTransform NewSpace(string name)
    {
        RectTransform space = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        space.SetParent(_rect, false);
        space.anchorMin = space.anchorMax = space.pivot = new Vector2(0.5f, 0.5f);
        space.sizeDelta = Ref;
        return space;
    }

    private static Vector2 Size => Ref;

    private void FitSpace()
    {
        Vector2 s = _rect.rect.size;
        _space.localScale = _spaceFront.localScale = new Vector3(s.x / Ref.x, s.y / Ref.y, 1f);
    }

    // ---- Brume ---------------------------------------------------------------------------------------------------------
    private void BuildFog()
    {
        // nappes qui dérivent en sens opposés, à des vitesses différentes : le mouvement se lit tout de suite
        AddFog(_space, "FogBack", 0.00f, 0.42f, _fogAlphaBack, 0.022f, 0.9f, 10f);
        AddFog(_space, "FogMid", 0.12f, 0.30f, _fogAlphaBack * 0.75f, -0.03f, 1.2f, 14f);   // nappe qui flotte plus haut, entre les arbres
        AddFog(_spaceFront, "FogFront", -0.06f, 0.24f, _fogAlphaFront, 0.04f, 1.4f, 6f);  // au ras du sol, devant les pieds
    }

    private void AddFog(RectTransform parent, string name, float bottom, float height, float alpha, float speed, float tiles, float bob)
    {
        RawImage img = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        img.transform.SetParent(parent, false);
        img.raycastTarget = false;
        img.texture = FogTexture;
        img.color = new Color(_fogColor.r, _fogColor.g, _fogColor.b, alpha);
        img.uvRect = new Rect(Random.value, 0f, tiles, 1f);
        RectTransform rt = img.rectTransform;
        rt.anchorMin = new Vector2(-0.05f, bottom);
        rt.anchorMax = new Vector2(1.05f, bottom + height);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        _fogs.Add(new Fog { image = img, speed = speed, baseAlpha = alpha, phase = Random.value * 10f, bob = bob });
    }

    // ---- Rayons + poussière --------------------------------------------------------------------------------------------
    private void BuildShafts()
    {
        // trois faisceaux de largeurs différentes, légèrement décalés, qui respirent chacun à son rythme
        float[] offsets = { -0.035f, 0.0f, 0.04f };
        float[] widths = { 110f, 180f, 85f };
        for (int i = 0; i < offsets.Length; i++)
        {
            Image img = NewImage("Shaft" + i, ShaftSprite, _additiveMaterial);
            RectTransform rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = _shaftOrigin + new Vector2(offsets[i], 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(widths[i], 620f);
            rt.localRotation = Quaternion.Euler(0f, 0f, _shaftAngle + i * 2.5f - 2.5f);
            img.color = new Color(1f, 0.90f, 0.62f, 0f);
            _shafts.Add(new Shaft { image = img, baseAlpha = _shaftPeakAlpha * (i == 1 ? 1f : 0.75f), phase = i * 2.1f, speed = 0.13f + i * 0.04f,
                                    baseAngle = _shaftAngle + i * 2.5f - 2.5f, baseWidth = widths[i] });
        }
    }

    private void BuildDust()
    {
        for (int i = 0; i < _dustCount; i++)
        {
            Image img = NewImage("Dust" + i, DustSprite, _additiveMaterial);
            var d = new Dust { rt = img.rectTransform, image = img };
            d.rt.anchorMin = d.rt.anchorMax = Vector2.zero;
            RespawnDust(d, true);
            _dust.Add(d);
        }
    }

    // Une poussière vit dans le cône des rayons : point tiré le long du faisceau central, légèrement de part et d'autre.
    private void RespawnDust(Dust d, bool anywhere)
    {
        Vector2 size = Size;
        float along = Random.Range(0.05f, 0.95f);
        float across = Random.Range(-1f, 1f) * Mathf.Lerp(30f, 110f, along);
        float rad = _shaftAngle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad));    // vers le bas du faisceau
        Vector2 perp = new Vector2(-dir.y, dir.x);
        Vector2 origin = new Vector2(_shaftOrigin.x * size.x, _shaftOrigin.y * size.y);
        d.pos = origin + dir * along * 560f + perp * across;
        d.vel = new Vector2(Random.Range(2f, 9f), Random.Range(-4f, 5f));
        d.phase = anywhere ? Random.value * 6f : 0f;
        d.size = Random.Range(2.5f, 6f);
        d.rt.sizeDelta = new Vector2(d.size, d.size);
    }

    // ---- Feuilles ------------------------------------------------------------------------------------------------------
    private void BuildLeaves()
    {
        for (int i = 0; i < _leafCount; i++)
        {
            Image img = NewImage("Leaf" + i, LeafSprite, null);
            img.preserveAspect = true;
            var l = new Leaf { rt = img.rectTransform, image = img };
            l.rt.anchorMin = l.rt.anchorMax = Vector2.zero;
            RespawnLeaf(l, true);
            _leaves.Add(l);
        }
    }

    private void RespawnLeaf(Leaf l, bool anywhere)
    {
        Vector2 size = Size;
        l.size = Random.Range(_leafSize.x, _leafSize.y);
        l.rt.sizeDelta = new Vector2(l.size * 0.55f, l.size);
        l.pos = new Vector2(Random.Range(-0.15f, 0.9f) * size.x, anywhere ? Random.Range(0.1f, 1.05f) * size.y : size.y + l.size);
        l.fall = Random.Range(16f, 32f);
        l.swayAmp = Random.Range(22f, 45f);
        l.swayFreq = Random.Range(0.16f, 0.32f);
        l.spin = Random.Range(-30f, 30f);
        l.flip = Random.Range(0.6f, 1.3f);
        l.phase = Random.value * 10f;
        l.life = Random.Range(_leafLife.x, _leafLife.y);
        l.age = anywhere ? Random.Range(0f, l.life * 0.7f) : 0f;
        // une feuille peut naître n'importe où dans la moitié haute : elle apparaît en fondu au lieu d'entrer par le bord
        if (!anywhere) l.pos.y = Random.Range(0.45f, 1.0f) * size.y;
        Color c = LeafColors[Random.Range(0, LeafColors.Length)];
        float shade = Random.Range(0.8f, 1f);   // feuilles plus sombres = plus loin, comme dans l'illustration
        l.color = new Color(c.r * shade, c.g * shade, c.b * shade, 0.95f);
        l.image.color = new Color(l.color.r, l.color.g, l.color.b, 0f);
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);
        _time += dt;
        FitSpace();
        Vector2 size = Size;

        foreach (Fog f in _fogs)
        {
            Rect uv = f.image.uvRect;
            uv.x += f.speed * dt;
            f.image.uvRect = uv;
            Color c = f.image.color;
            c.a = f.baseAlpha * (0.65f + 0.35f * Mathf.Sin(_time * 0.33f + f.phase));   // la nappe s'épaissit puis s'éclaircit
            f.image.color = c;
            f.image.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Sin(_time * 0.21f + f.phase * 1.3f) * f.bob);
        }

        foreach (Shaft s in _shafts)
        {
            // respiration lente et irrégulière (deux sinus) : jamais un clignotement régulier
            // pulsation nette (de presque éteint à plein) + balancement et largeur qui respirent : la lumière vit
            float k = 0.5f + 0.38f * Mathf.Sin(_time * s.speed * Mathf.PI * 2f + s.phase) + 0.12f * Mathf.Sin(_time * 0.47f + s.phase * 1.7f);
            Color c = s.image.color;
            c.a = s.baseAlpha * Mathf.Clamp01(k);
            s.image.color = c;
            s.image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, s.baseAngle + Mathf.Sin(_time * 0.19f + s.phase) * 2.5f);
            s.image.rectTransform.sizeDelta = new Vector2(s.baseWidth * (0.85f + 0.3f * Mathf.Clamp01(k)), 620f);
        }

        foreach (Dust d in _dust)
        {
            d.phase += dt;
            d.pos += (d.vel + new Vector2(Mathf.Sin(_time * 0.6f + d.size) * 3f, Mathf.Cos(_time * 0.45f + d.size) * 3f)) * dt;
            d.rt.anchoredPosition = d.pos;
            float life = d.phase / 7f;                                     // ~7 s de vie, apparition et disparition en fondu
            float twinkle = 0.65f + 0.35f * Mathf.Sin(_time * 2.3f + d.size * 3f);
            Color c = new Color(1f, 0.88f, 0.55f, Mathf.Sin(Mathf.Clamp01(life) * Mathf.PI) * 0.75f * twinkle);
            d.image.color = c;
            if (life >= 1f) RespawnDust(d, false);
        }

        foreach (Leaf l in _leaves)
        {
            float t = _time + l.phase;
            float gust = 1f + 0.6f * Mathf.Max(0f, Mathf.Sin(_time * 0.13f + 1.3f));   // rafale de temps en temps
            l.pos.y -= l.fall * dt;
            l.pos.x += _wind * gust * dt;
            float sway = Mathf.Sin(t * l.swayFreq * Mathf.PI * 2f) * l.swayAmp;
            l.rt.anchoredPosition = l.pos + new Vector2(sway, 0f);
            // balancier + retournement : la largeur passe par 0 comme une feuille qui tourne sur elle-même
            float angle = Mathf.Sin(t * l.swayFreq * Mathf.PI * 2f + 0.8f) * 35f + t * l.spin * 0.3f;
            l.rt.localRotation = Quaternion.Euler(0f, 0f, angle);
            float flip = Mathf.Cos(t * l.flip);
            l.rt.localScale = new Vector3(Mathf.Lerp(0.25f, 1f, Mathf.Abs(flip)), 1f, 1f);
            l.age += dt;
            float fadeIn = Mathf.Clamp01(l.age / 0.8f), fadeOut = Mathf.Clamp01((l.life - l.age) / 1.5f);
            l.image.color = new Color(l.color.r, l.color.g, l.color.b, l.color.a * Mathf.Min(fadeIn, fadeOut));
            if (l.age >= l.life || l.pos.y < -l.size * 2f || l.pos.x > size.x + l.size * 2f) RespawnLeaf(l, false);
        }

        UpdateStreaks(dt);
    }

    // ---- Traits de vent ---------------------------------------------------------------------------------------------------
    private void BuildStreaks()
    {
        for (int i = 0; i < _maxStreaks; i++)
            _streaks.Add(new Streak { g = NewStreakGraphic("Wind" + i), g2 = NewStreakGraphic("Wind" + i + "b") });
        _nextStreak = 0.4f;
    }

    private WindStreakGraphic NewStreakGraphic(string name)
    {
        WindStreakGraphic g = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(WindStreakGraphic)).GetComponent<WindStreakGraphic>();
        g.transform.SetParent(_space, false);
        g.raycastTarget = false;
        g.color = new Color(1f, 1f, 1f, _streakAlpha);
        RectTransform rt = g.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        g.gameObject.SetActive(false);
        return g;
    }

    private void UpdateStreaks(float dt)
    {
        _nextStreak -= dt;
        foreach (Streak st in _streaks)
        {
            if (!st.g.gameObject.activeSelf)
            {
                if (_nextStreak > 0f) continue;
                LaunchStreak(st);
                _nextStreak = Random.Range(_streakInterval.x, _streakInterval.y);
                continue;
            }
            st.age += dt;
            float k = st.age / st.life;
            // la tête dessine le trait (60 % du temps), la queue le rattrape et l'efface en partant à 35 %
            float head = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / 0.6f));
            float tail = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.35f) / 0.65f));
            st.g.SetWindow(tail, head);
            float k2 = Mathf.Clamp01((k - 0.08f) / 0.92f);   // le 2e trait suit avec un léger retard
            if (st.pair) st.g2.SetWindow(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k2 - 0.35f) / 0.65f)), Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k2 / 0.6f)));
            Vector2 drift = new Vector2(st.speed * st.age, 0f);
            st.g.rectTransform.anchoredPosition = st.origin + drift;
            st.g2.rectTransform.anchoredPosition = st.origin + st.offset2 + drift;
            if (k >= 1f) { st.g.gameObject.SetActive(false); st.g2.gameObject.SetActive(false); }
        }
    }

    // Une bourrasque : une forme tirée parmi 5 (voir StreakPath), inclinée et étirée au hasard ; un 2e trait plus court une
    // fois sur deux.
    private void LaunchStreak(Streak st)
    {
        float length = Random.Range(240f, 560f);
        float amp = Random.Range(6f, 20f);
        float waves = Random.Range(0.5f, 1.3f);
        float curl = Random.Range(18f, 36f);
        float tilt = Random.Range(-12f, 10f);
        st.g.SetPath(StreakPath(Random.Range(0, 5), length, amp, waves, curl, tilt), Random.Range(6f, 10f));
        st.pair = Random.value < 0.5f;
        if (st.pair) st.g2.SetPath(StreakPath(0, length * Random.Range(0.45f, 0.7f), amp * 0.8f, waves, curl, tilt), Random.Range(4.5f, 6.5f));
        st.g.SetWindow(0f, 0f);
        st.g2.SetWindow(0f, 0f);
        st.life = Random.Range(1.9f, 2.8f) * Mathf.Lerp(0.85f, 1.2f, (length - 240f) / 320f);
        st.age = 0f;
        st.speed = Random.Range(70f, 120f);
        // part de la gauche (le vent souffle vers la droite), entre la brume du sol et le bas du titre : le trait est visible
        // dans les trouées et au-dessus des personnages, et ne disparaît derrière eux que lorsqu'il les croise
        st.origin = new Vector2(Random.Range(-0.12f, 0.5f) * Size.x, Random.Range(0.36f, 0.78f) * Size.y);
        st.offset2 = new Vector2(Random.Range(30f, 70f), -Random.Range(18f, 28f));
        st.g.rectTransform.anchoredPosition = st.origin;
        st.g2.rectTransform.anchoredPosition = st.origin + st.offset2;
        st.g.gameObject.SetActive(true);
        st.g2.gameObject.SetActive(st.pair);
    }

    // 5 formes de trait, toutes tracées de gauche à droite (sens du vent) :
    //  0 ondulation simple ; 1 boucle au bout qui remonte ; 2 boucle au bout qui descend ; 3 boucle complète au milieu du
    //  trait (le « looping » des dessins animés) ; 4 petit tourbillon au départ qui se déroule en ondulation.
    public static Vector2[] StreakPath(int shape, float length, float amp, float waves, float curl, float tilt)
    {
        var pts = new List<Vector2>(96);
        if (shape == 3)
        {
            float split = Random.Range(0.35f, 0.6f);
            Wave(pts, 0f, split * length, amp, waves * split, 0f);
            Vector2 p = pts[pts.Count - 1];
            Vector2 center = p + new Vector2(0f, curl);
            for (int i = 1; i <= 30; i++)
            {
                float a = -Mathf.PI * 0.5f + (float)i / 30 * Mathf.PI * 2f;   // tour complet : revient sur le trait
                pts.Add(center + new Vector2(Mathf.Cos(a) * curl * 0.9f, Mathf.Sin(a) * curl));
            }
            Wave(pts, split * length, length, amp, waves * (1f - split), p.y);
        }
        else if (shape == 4)
        {
            // tourbillon : spirale qui s'ouvre (de l'intérieur vers l'extérieur) puis rejoint l'ondulation
            Vector2 center = new Vector2(curl, curl * 0.9f);
            for (int i = 0; i <= 24; i++)
            {
                float u = (float)i / 24;
                float a = Mathf.PI * 0.5f - u * Mathf.PI * 1.75f + Mathf.PI;   // tourne dans le sens horaire vers le bas
                float r = curl * Mathf.Lerp(0.35f, 1f, u);
                pts.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            Vector2 p = pts[pts.Count - 1];
            Wave(pts, p.x, p.x + length, amp, waves, p.y);
        }
        else
        {
            Wave(pts, 0f, length, amp, waves, 0f);
            if (shape == 1 || shape == 2)
            {
                float side = shape == 1 ? 1f : -1f;
                Vector2 end = pts[pts.Count - 1];
                Vector2 center = end + new Vector2(0f, curl * side);   // cercle tangent au bout du trait
                for (int i = 1; i <= 26; i++)
                {
                    float a = -Mathf.PI * 0.5f * side + side * (float)i / 26 * Mathf.PI * 1.7f;
                    float r = curl * Mathf.Lerp(1f, 0.55f, (float)i / 26);
                    pts.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
            }
        }
        Quaternion rot = Quaternion.Euler(0f, 0f, tilt);
        for (int i = 0; i < pts.Count; i++) pts[i] = rot * pts[i];
        return pts.ToArray();
    }

    // Ondulation douce de x0 à x1, d'amplitude croissante, autour de la hauteur y0.
    private static void Wave(List<Vector2> pts, float x0, float x1, float amp, float waves, float y0)
    {
        const int n = 36;
        for (int i = pts.Count == 0 ? 0 : 1; i <= n; i++)
        {
            float u = (float)i / n;
            pts.Add(new Vector2(Mathf.Lerp(x0, x1, u), y0 + Mathf.Sin(u * waves * Mathf.PI * 2f) * amp * Mathf.Min(1f, u * 1.5f + 0.2f)));
        }
    }

    private Image NewImage(string name, Sprite sprite, Material material)
    {
        Image img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(_space, false);
        img.sprite = sprite;
        img.raycastTarget = false;
        if (material != null) img.material = material;
        return img;
    }

    // ---- Textures générées une fois --------------------------------------------------------------------------------------
    private static Texture2D _fogTex;
    private static Sprite _shaftSprite, _dustSprite, _leafSprite;

    // Brume : bruit doux qui se répète horizontalement (bords gauche/droit raccord), fondu en haut et en bas.
    private static Texture2D FogTexture
    {
        get
        {
            if (_fogTex != null) return _fogTex;
            const int w = 256, h = 64;
            _fogTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                float vertical = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(v / 0.35f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - v) / 0.55f));
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w;
                    float n = 0f, amp = 0.55f, total = 0f;
                    for (int o = 0; o < 4; o++)
                    {
                        int freq = 2 << o;   // fréquences entières : la texture boucle sans couture
                        n += amp * TileNoise(u * freq, v * freq * 0.5f + o * 7.3f, freq);
                        total += amp;
                        amp *= 0.5f;
                    }
                    n /= total;
                    float a = Mathf.Clamp01((n - 0.32f) * 2.2f) * vertical;   // plus contrasté : des volutes, pas un voile uniforme
                    px[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            }
            _fogTex.SetPixels32(px);
            _fogTex.Apply();
            return _fogTex;
        }
    }

    // Bruit de valeur lissé, périodique en x sur `period` cellules.
    private static float TileNoise(float x, float y, int period)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Hash(x0 % period, y0), b = Hash((x0 + 1) % period, y0);
        float c = Hash(x0 % period, y0 + 1), d = Hash((x0 + 1) % period, y0 + 1);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    private static float Hash(int x, int y)
    {
        uint h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
    }

    // Faisceau : plus large et plus clair en haut, s'efface vers le bas et sur les côtés.
    private static Sprite ShaftSprite
    {
        get
        {
            if (_shaftSprite != null) return _shaftSprite;
            const int w = 64, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float down = 1f - (y + 0.5f) / h;   // 0 en haut, 1 en bas
                float lengthFade = Mathf.Pow(1f - down, 1.6f) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - down) / 0.08f) + 0.2f);
                for (int x = 0; x < w; x++)
                {
                    float across = Mathf.Abs((x + 0.5f) / w - 0.5f) * 2f;
                    float width = Mathf.Lerp(0.55f, 1f, down);              // le faisceau s'élargit en descendant
                    float side = Mathf.Clamp01(1f - across / width);
                    px[y * w + x] = new Color(1f, 1f, 1f, side * side * lengthFade);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            _shaftSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 1f), 100f);
            return _shaftSprite;
        }
    }

    private static Sprite DustSprite
    {
        get
        {
            if (_dustSprite != null) return _dustSprite;
            const int s = 16;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float r = new Vector2(x + 0.5f - s * 0.5f, y + 0.5f - s * 0.5f).magnitude / (s * 0.5f);
                    px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Exp(-r * r * 5f) * Mathf.Clamp01(1f - r));
                }
            tex.SetPixels32(px);
            tex.Apply();
            _dustSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            return _dustSprite;
        }
    }

    // Feuille en amande pointue, nervure centrale et liseré plus sombres (l'illustration est cernée), dégradé clair -> foncé.
    // Blanc teinté par Image.color.
    private static Sprite LeafSprite
    {
        get
        {
            if (_leafSprite != null) return _leafSprite;
            const int w = 40, h = 72;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;                          // 0 = pointe basse (pétiole), 1 = pointe haute
                float halfWidth = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.Clamp01((v - 0.06f) / 0.94f) * Mathf.PI)), 0.85f) * 0.46f;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w - 0.5f;
                    float a = 0f, shade = 1f;
                    if (halfWidth > 0.001f)
                    {
                        float edge = (halfWidth - Mathf.Abs(u)) * w;        // px jusqu'au bord
                        a = Mathf.Clamp01(edge + 0.5f);
                        float rim = Mathf.Clamp01(1.6f - edge);             // liseré sombre de ~1,5 px
                        float vein = Mathf.Clamp01(1.2f - Mathf.Abs(u) * w) * (v > 0.08f && v < 0.92f ? 1f : 0f);
                        float side = Mathf.Abs(Mathf.Repeat((Mathf.Abs(u) * 2.2f - v) * 7f, 1f) - 0.5f) < 0.06f ? 0.12f : 0f;   // nervures secondaires
                        shade = Mathf.Lerp(1f, 0.82f, (u + 0.5f)) * (1f - 0.45f * rim) * (1f - 0.35f * vein) * (1f - side);
                    }
                    // pétiole : petit trait sous la feuille
                    if (v < 0.08f && Mathf.Abs(u) * w < 1.1f) { a = 1f; shade = 0.55f; }
                    px[y * w + x] = new Color(shade, shade, shade, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            _leafSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            return _leafSprite;
        }
    }
}
