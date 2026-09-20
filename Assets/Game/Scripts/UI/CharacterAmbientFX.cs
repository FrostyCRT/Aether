using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// AJOUTE (2026-09-19) - ambiance magique procedurale de l'onglet Personnage
// (retour utilisateur : "pas juste des points qui s'illuminent, des lueurs, une
// fine brume a faible alpha qui se deplace lentement, des petits elements qui
// rendent ca magique et vivant, sans que ca se voit trop - le plus important
// c'est le personnage").
//
// Quatre couches, toutes generees en code (zero asset), toutes teintees a la
// couleur d'identite du personnage courant (SetTint) et centrees sur le portrait
// (le cadre CharacterImage est le meme pour les 3 persos), du plus diffus au plus net :
//   1. BRUME    - grandes nappes de bruit fractal (fBm) a tres faible alpha, qui
//                 derivent/tournent tres lentement et "respirent". Un fondu sur les
//                 bords de la texture evite toute arete rectangulaire visible.
//   2. BOKEH    - quelques grosses lueurs floues (disque + liseré) tres discretes,
//                 cantonnees a la peripherie (profondeur de champ).
//   3. POUSSIERES - petites particules douces qui montent en ondulant, naissent /
//                 meurent en fondu et scintillent legerement. Melange de points fins
//                 et de petits orbes plus doux/plus gros.
//   4. ECLATS   - rares scintillements en croix (4 branches) qui apparaissent puis
//                 s'eteignent, chacun a un endroit different.
//
// "Le personnage d'abord" :
//  - la BRUME et le BOKEH sont dessines DERRIERE le portrait (objet frere place
//    juste avant CharacterImage, voir _mistBehindCharacter) : ils peuvent etre bien
//    visibles autour/derriere le perso sans jamais le voiler. Poussieres et eclats,
//    eux, flottent devant ;
//  - _characterProtection attenue ce qui passe au centre de la zone pour les
//    elements de devant, et _intensity regle tout d'un seul curseur.
//
// Choix techniques :
//  - sprites blancs generes UNE fois par instance (Awake) et detruits en OnDestroy ;
//    la teinte/l'alpha passent par CanvasRenderer.SetColor (multiplie les sommets,
//    ne regenere pas le maillage - bien moins cher que Image.color a chaque frame) ;
//  - aucune allocation dans Update (tableaux/objets crees une fois) ;
//  - Time.unscaledTime : l'animation ne depend pas de Time.timeScale.
[RequireComponent(typeof(RectTransform))]
public class CharacterAmbientFX : MonoBehaviour
{
    [Header("Zone & intensité")]
    [Tooltip("Taille de la zone d'ambiance, centrée sur le portrait (parent CharacterImage, ~614x836). Les 3 portraits occupent le même cadre : la zone reste donc sur le personnage quel qu'il soit.")]
    [SerializeField] private Vector2 _areaSize = new Vector2(620f, 840f);
    [Tooltip("Curseur global : 0 = rien, 1 = réglage par défaut, 2 = deux fois plus visible.")]
    [Range(0f, 2f)] [SerializeField] private float _intensity = 1f;
    [Tooltip("Atténuation de l'ambiance au centre de la zone, là où se tient le personnage (0 = aucune, 1 = presque invisible au centre).")]
    [Range(0f, 1f)] [SerializeField] private float _characterProtection = 0.6f;
    [Tooltip("Durée du fondu d'apparition quand l'onglet s'ouvre (évite que tout surgisse d'un coup).")]
    [SerializeField] private float _fadeInDuration = 1.5f;
    [Tooltip("Dessine la brume et le bokeh DERRIÈRE le portrait (recommandé) : ils peuvent alors être bien visibles sans voiler le personnage. Décoché = tout devant, atténué au centre.")]
    [SerializeField] private bool _mistBehindCharacter = true;

    [Header("Brume (nappes diffuses)")]
    [SerializeField] private int _mistCount = 7;
    [Tooltip("Opacité maximale d'une nappe. Elle est derrière le personnage, donc peut être nettement visible.")]
    [SerializeField] private float _mistMaxAlpha = 0.65f;
    [SerializeField] private Vector2 _mistWidthRange = new Vector2(360f, 600f);
    [Tooltip("Éloignement horizontal maximal de la brume, en fraction de la demi-largeur de la zone. Petit = reste sur le personnage. Au-delà, la brume s'efface.")]
    [Range(0.1f, 1f)] [SerializeField] private float _mistHorizontalReach = 0.5f;
    [Tooltip("Nappes de brume au sol placées DEVANT le bas du personnage (0 = aucune).")]
    [SerializeField] private int _frontMistCount = 2;
    [Tooltip("Force de ces nappes de devant, relativement à la brume arrière (elles ne doivent pas voiler le personnage).")]
    [Range(0f, 1f)] [SerializeField] private float _frontMistAlpha = 0.45f;
    [Tooltip("Vitesse de dérive/rotation des nappes. Petit = lent.")]
    [SerializeField] private float _mistDriftSpeed = 0.05f;

    [Header("Bokeh (grosses lueurs floues en périphérie)")]
    [SerializeField] private int _bokehCount = 6;
    [SerializeField] private float _bokehMaxAlpha = 0.18f;
    [SerializeField] private Vector2 _bokehSizeRange = new Vector2(40f, 100f);

    [Header("Poussières de lumière")]
    [SerializeField] private int _moteCount = 40;
    [SerializeField] private float _moteMaxAlpha = 0.85f;
    [SerializeField] private Vector2 _moteSizeRange = new Vector2(3f, 10f);
    [Tooltip("Vitesse d'ascension en pixels/seconde.")]
    [SerializeField] private Vector2 _moteRiseSpeedRange = new Vector2(8f, 24f);
    [SerializeField] private Vector2 _moteLifetimeRange = new Vector2(7f, 15f);

    [Header("Éclats (scintillements rares)")]
    [SerializeField] private int _glintCount = 4;
    [SerializeField] private float _glintMaxAlpha = 0.8f;
    [SerializeField] private Vector2 _glintSizeRange = new Vector2(22f, 44f);
    [Tooltip("Délai (secondes) entre deux scintillements d'un même éclat.")]
    [SerializeField] private Vector2 _glintIntervalRange = new Vector2(2.5f, 7f);

    // --- éléments (créés une seule fois) ---------------------------------

    private class Mist
    {
        public RectTransform rt; public CanvasRenderer cr;
        public bool front;                 // true = devant le personnage (brume au sol légère)
        public Vector2 basePos; public float wanderX, wanderY;
        public float seedX, seedY, seedRot;
        public float baseRot, rotRange, baseAlpha;
        public float breatheFreq, breathePhase, whiten;
        public Color rgb;
    }

    private class Bokeh
    {
        public RectTransform rt; public CanvasRenderer cr;
        public float birth, life, startX, startY, driftX, driftY, swayAmp, swayFreq, swayPhase, maxAlpha, whiten;
        public Color rgb;
    }

    private class Mote
    {
        public RectTransform rt; public CanvasRenderer cr;
        public float birth, life, startX, startY, rise, swayAmp, swayFreq, swayPhase, maxAlpha, twFreq, twPhase, whiten;
        public Color rgb;
    }

    private class Glint
    {
        public RectTransform rt; public CanvasRenderer cr;
        public float start, duration, size, rot, whiten;
        public Vector2 pos;
        public Color rgb;
    }

    private readonly List<Mist> _mists = new List<Mist>();
    private readonly List<Bokeh> _bokehs = new List<Bokeh>();
    private readonly List<Mote> _motes = new List<Mote>();
    private readonly List<Glint> _glints = new List<Glint>();

    // sprites/textures possédés par cette instance (détruits en OnDestroy)
    private readonly List<Object> _owned = new List<Object>();
    private Sprite _dotSprite, _bokehSprite, _glintSprite, _mistSpriteA, _mistSpriteB;

    private RectTransform _rect;
    private RectTransform _backRoot;      // conteneur derrière le portrait (brume + bokeh)
    private Vector2 _restingPos;          // position de repos du portrait (pas celle d'un glissement en cours)
    private Color _tint = new Color(0.6f, 0.85f, 1f);
    private Color _mistColor = new Color(0.6f, 0.85f, 1f);
    private float _mistWhitenScale = 1f;   // 1 = brume éclaircie comme avant ; petit = couleur restée saturée
    private float _mistAlphaScale = 1f;
    private float _enableTime;
    private bool _built;

    // --- API publique (appelée par CharacterSelectUI) --------------------

    public void SetTint(Color tint) => SetTint(tint, tint, 1f, 1f);

    // Reteinte toute l'ambiance (couleur d'aura du personnage). Seuls les RGB
    // mis en cache changent - l'alpha reste piloté par frame dans Update.
    // La BRUME peut avoir sa propre couleur : sur un fond de la même teinte que l'aura
    // (Aether : fond doré derrière une brume jaune pâle = invisible) on la fonce/sature
    // (mistWhitenScale petit, mistColor plus profonde) et on la renforce (mistAlphaScale).
    public void SetTint(Color tint, Color mistColor, float mistWhitenScale, float mistAlphaScale)
    {
        _tint = tint;
        _mistColor = mistColor;
        _mistWhitenScale = mistWhitenScale;
        _mistAlphaScale = mistAlphaScale;
        EnsureBuilt();
        foreach (Mist m in _mists) m.rgb = Color.Lerp(_mistColor, Color.white, m.whiten * _mistWhitenScale);
        foreach (Bokeh b in _bokehs) b.rgb = Tinted(b.whiten);
        foreach (Mote m in _motes) m.rgb = Tinted(m.whiten);
        foreach (Glint g in _glints) g.rgb = Tinted(g.whiten);
    }

    public void SetIntensity(float intensity) => _intensity = Mathf.Max(0f, intensity);

    // --- cycle de vie ----------------------------------------------------

    private void Awake() => EnsureBuilt();

    private void OnEnable()
    {
        _enableTime = Time.unscaledTime;

        // le conteneur arrière est un frère du portrait, pas un enfant : il ne suit
        // donc pas automatiquement l'activation de ce composant.
        if (_backRoot != null)
        {
            SyncBackRoot();
            _backRoot.gameObject.SetActive(true);
        }
    }

    private void OnDisable()
    {
        if (_backRoot != null) _backRoot.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_backRoot != null) Destroy(_backRoot.gameObject);
        foreach (Object o in _owned)
            if (o != null) Destroy(o);
        _owned.Clear();
    }

    // Place le conteneur arrière sur le portrait : mêmes ancres/taille/échelle, mais à la
    // position de REPOS (pas celle du glissement de changement de personnage : la brume
    // reste en place pendant que le portrait entre/sort, c'est voulu).
    private void SyncBackRoot()
    {
        RectTransform portrait = transform.parent as RectTransform;
        if (portrait == null || _backRoot == null) return;

        _backRoot.anchorMin = portrait.anchorMin;
        _backRoot.anchorMax = portrait.anchorMax;
        _backRoot.pivot = portrait.pivot;
        _backRoot.sizeDelta = portrait.sizeDelta;
        _backRoot.localScale = portrait.localScale;
        _backRoot.localRotation = portrait.localRotation;
        _backRoot.anchoredPosition = _restingPos;
    }

    private RectTransform CreateBackRoot()
    {
        RectTransform portrait = transform.parent as RectTransform;
        if (portrait == null || portrait.parent == null) return null;

        GameObject go = new GameObject("AmbientFX_Back", typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(portrait.parent, false);
        rt.SetSiblingIndex(portrait.GetSiblingIndex()); // juste avant (= derrière) le portrait

        _backRoot = rt;
        _restingPos = portrait.anchoredPosition;
        SyncBackRoot();
        return rt;
    }

    private void EnsureBuilt()
    {
        if (_built) return;
        _built = true;

        _rect = (RectTransform)transform;
        _rect.sizeDelta = _areaSize;

        _dotSprite = MakeSprite(64, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            return Mathf.Pow(Mathf.Clamp01(1f - r), 1.8f);
        });

        // disque doux + liseré légèrement plus marqué : rappelle une lentille floue
        _bokehSprite = MakeSprite(128, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float disk = 1f - Mathf.SmoothStep(0.75f, 1f, r);
            float rim = Mathf.Exp(-Mathf.Pow((r - 0.88f) / 0.07f, 2f));
            return disk * (0.35f + 0.65f * rim);
        });

        // croix à 4 branches fines + noyau doux, fondue sur les bords
        _glintSprite = MakeSprite(64, (u, v) =>
        {
            float core = Mathf.Exp(-(u * u + v * v) * 22f);
            float h = Mathf.Exp(-v * v * 260f) * Mathf.Exp(-u * u * 5f);
            float vert = Mathf.Exp(-u * u * 260f) * Mathf.Exp(-v * v * 5f);
            float edge = 1f - Mathf.SmoothStep(0.85f, 1f, Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)));
            return (core + 0.85f * Mathf.Max(h, vert)) * edge;
        });

        _mistSpriteA = MakeMistSprite(192, 3.7f);
        _mistSpriteB = MakeMistSprite(192, 41.3f);

        if (_mistBehindCharacter) CreateBackRoot();

        BuildMist();
        BuildBokeh();
        BuildMotes();
        BuildGlints();

        // Construit alors que ce composant est inactif (ex. personnage verrouillé) : le
        // conteneur arrière est un FRÈRE, pas un enfant, il ne suit donc pas cet état -
        // on le masque ici ; OnEnable le réactivera.
        if (_backRoot != null && !isActiveAndEnabled)
            _backRoot.gameObject.SetActive(false);

        // teinte initiale (SetTint écrasera dès que CharacterSelectUI connaît le perso)
        SetTint(_tint, _mistColor, _mistWhitenScale, _mistAlphaScale);
    }

    // --- construction ----------------------------------------------------

    private void BuildMist()
    {
        Vector2 half = _areaSize * 0.5f;
        float reachX = half.x * _mistHorizontalReach;

        // Deux populations :
        //  - ARRIÈRE (_mistCount) : derrière le personnage, centrées de part et d'autre de
        //    son axe (une sur deux à gauche/droite) pour que la brume déborde AUTOUR de la
        //    silhouette - centrée pile derrière lui elle serait entièrement masquée par le
        //    corps (constaté en mesure : presque rien de visible) ;
        //  - AVANT (_frontMistCount) : 2 nappes de brume au sol devant le bas du corps, plus
        //    faibles, pour que la brume soit aussi visible SUR le personnage sans le voiler.
        int total = _mistCount + _frontMistCount;
        for (int i = 0; i < total; i++)
        {
            bool front = i >= _mistCount;
            Mist m = new Mist { front = front };
            CreateImage($"Mist{i + 1}", (i % 2 == 0) ? _mistSpriteA : _mistSpriteB, front ? transform : BackParent, out m.rt, out m.cr);

            float width = Random.Range(_mistWidthRange.x, _mistWidthRange.y);
            float aspect = front ? Random.Range(0.35f, 0.5f) : Random.Range(0.55f, 0.85f); // brume au sol : plus plate
            m.rt.sizeDelta = new Vector2(width, width * aspect);
            if (Random.value < 0.5f) m.rt.localScale = new Vector3(-1f, 1f, 1f);

            if (front)
            {
                m.basePos = new Vector2(
                    Random.Range(-reachX * 0.4f, reachX * 0.4f),
                    Mathf.Lerp(-half.y * 0.85f, -half.y * 0.45f, Random.value));
            }
            else
            {
                float side = (i % 2 == 0) ? -1f : 1f;
                m.basePos = new Vector2(
                    side * Random.Range(0.35f, 0.9f) * reachX * 0.8f,
                    Mathf.Lerp(-half.y * 0.8f, half.y * 0.7f, Random.value));
            }
            m.wanderX = Random.Range(25f, 55f);
            m.wanderY = Random.Range(70f, 150f);
            m.seedX = Random.Range(0f, 100f);
            m.seedY = Random.Range(0f, 100f);
            m.seedRot = Random.Range(0f, 100f);
            m.baseRot = Random.Range(-25f, 25f);
            m.rotRange = Random.Range(8f, 20f);
            m.baseAlpha = _mistMaxAlpha * (front ? _frontMistAlpha : 1f) * Random.Range(0.6f, 1f);
            m.breatheFreq = Random.Range(0.12f, 0.28f);
            m.breathePhase = Random.Range(0f, Mathf.PI * 2f);
            m.whiten = Random.Range(0.4f, 0.65f); // brume claire : se lit sur fond chaud comme sur fond sombre
            _mists.Add(m);
        }
    }

    private void BuildBokeh()
    {
        float t = Time.unscaledTime;
        for (int i = 0; i < _bokehCount; i++)
        {
            Bokeh b = new Bokeh();
            CreateImage($"Bokeh{i + 1}", _bokehSprite, BackParent, out b.rt, out b.cr);
            b.whiten = Random.Range(0.3f, 0.7f);
            RespawnBokeh(b, t, true);
            _bokehs.Add(b);
        }
    }

    private void BuildMotes()
    {
        float t = Time.unscaledTime;
        for (int i = 0; i < _moteCount; i++)
        {
            Mote m = new Mote();
            CreateImage($"Mote{i + 1}", _dotSprite, transform, out m.rt, out m.cr);
            RespawnMote(m, t, true);
            _motes.Add(m);
        }
    }

    private void BuildGlints()
    {
        float t = Time.unscaledTime;
        for (int i = 0; i < _glintCount; i++)
        {
            Glint g = new Glint();
            CreateImage($"Glint{i + 1}", _glintSprite, transform, out g.rt, out g.cr);
            g.whiten = 0.6f;
            g.start = t + Random.Range(0f, _glintIntervalRange.y);
            ScheduleGlint(g, g.start);
            _glints.Add(g);
        }
    }

    // Parent des couches "arrière" (brume, bokeh) : le conteneur derrière le portrait,
    // ou ce composant lui-même si _mistBehindCharacter est décoché / impossible.
    private Transform BackParent => _backRoot != null ? (Transform)_backRoot : transform;

    private void CreateImage(string objName, Sprite sprite, Transform parent, out RectTransform rt, out CanvasRenderer cr)
    {
        // Tous les composants passés au constructeur (pattern fiable du projet).
        GameObject go = new GameObject(objName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.color = Color.white; // teinte/alpha réels via CanvasRenderer.SetColor

        cr = go.GetComponent<CanvasRenderer>();
        cr.SetColor(new Color(1f, 1f, 1f, 0f)); // invisible tant que Update n'a pas tourné
    }

    // --- respawn / planification -----------------------------------------

    private void RespawnBokeh(Bokeh b, float t, bool randomizeAge)
    {
        b.life = Random.Range(14f, 26f);
        b.birth = t - (randomizeAge ? Random.Range(0f, b.life) : 0f);

        // anneau périphérique (jamais sur le personnage)
        Vector2 half = _areaSize * 0.5f;
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float radius = Random.Range(0.55f, 0.95f);
        b.startX = Mathf.Cos(angle) * half.x * radius * 0.6f; // resserré horizontalement : reste autour du personnage
        b.startY = Mathf.Sin(angle) * half.y * radius;
        b.driftX = Random.Range(-6f, 6f);
        b.driftY = Random.Range(2f, 8f);
        b.swayAmp = Random.Range(12f, 34f);
        b.swayFreq = Random.Range(0.1f, 0.25f);
        b.swayPhase = Random.Range(0f, Mathf.PI * 2f);
        b.maxAlpha = _bokehMaxAlpha * Random.Range(0.5f, 1f);
        float size = Random.Range(_bokehSizeRange.x, _bokehSizeRange.y);
        b.rt.sizeDelta = new Vector2(size, size);
    }

    private void RespawnMote(Mote m, float t, bool randomizeAge)
    {
        m.life = Random.Range(_moteLifetimeRange.x, _moteLifetimeRange.y);
        m.birth = t - (randomizeAge ? Random.Range(0f, m.life) : 0f);

        Vector2 half = _areaSize * 0.5f;
        m.startX = Random.Range(-half.x, half.x) * 0.95f;
        // naissent surtout vers le bas puis montent : donne l'impression de
        // particules qui s'élèvent du sol autour du personnage
        m.startY = Mathf.Lerp(-half.y, half.y * 0.35f, Mathf.Pow(Random.value, 1.3f));
        m.rise = Random.Range(_moteRiseSpeedRange.x, _moteRiseSpeedRange.y);
        m.swayAmp = Random.Range(8f, 30f);
        m.swayFreq = Random.Range(0.25f, 0.7f);
        m.swayPhase = Random.Range(0f, Mathf.PI * 2f);
        m.twFreq = Random.Range(0.5f, 1.3f); // scintillement lent (retour utilisateur : les clignotements étaient trop rapides)
        m.twPhase = Random.Range(0f, Mathf.PI * 2f);

        // un quart de "gros orbes doux" (plus grands, plus transparents), le reste
        // de fins points de poussière
        float size = Random.Range(_moteSizeRange.x, _moteSizeRange.y);
        m.maxAlpha = _moteMaxAlpha * Random.Range(0.45f, 1f);
        if (Random.value < 0.25f)
        {
            size *= Random.Range(1.8f, 2.6f);
            m.maxAlpha *= 0.55f;
        }
        m.rt.sizeDelta = new Vector2(size, size);

        m.whiten = Random.value < 0.15f ? 0.9f : Random.Range(0.25f, 0.7f);
        m.rgb = Tinted(m.whiten);
    }

    private void ScheduleGlint(Glint g, float startTime)
    {
        g.start = startTime;
        g.duration = Random.Range(1.8f, 3.0f); // apparition/extinction lentes
        g.size = Random.Range(_glintSizeRange.x, _glintSizeRange.y);
        g.rot = Random.Range(-20f, 20f);

        // évite le centre (le personnage) : quelques essais, sinon on garde le dernier
        Vector2 half = _areaSize * 0.5f;
        Vector2 p = Vector2.zero;
        for (int i = 0; i < 6; i++)
        {
            p = new Vector2(Random.Range(-half.x, half.x) * 0.9f, Random.Range(-half.y, half.y) * 0.9f);
            if (EdgeWeight(p, 1f) > 0.75f) break;
        }
        g.pos = p;
    }

    // --- boucle ----------------------------------------------------------

    private void Update()
    {
        if (!_built) return;

        float t = Time.unscaledTime;
        float fade = _fadeInDuration <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, (t - _enableTime) / _fadeInDuration);
        float k = _intensity * fade * GameSettings.MenuEffectsFactor;   // Paramètres > Graphismes > Effets d'ambiance

        UpdateMist(t, k);
        UpdateBokeh(t, k);
        UpdateMotes(t, k);
        UpdateGlints(t, k);
    }

    private void UpdateMist(float t, float k)
    {
        float speed = _mistDriftSpeed;
        for (int i = 0; i < _mists.Count; i++)
        {
            Mist m = _mists[i];
            float nx = Mathf.PerlinNoise(m.seedX, t * speed) - 0.5f;
            float ny = Mathf.PerlinNoise(m.seedY, t * speed + 50f) - 0.5f;
            Vector2 pos = m.basePos + new Vector2(nx * 2f * m.wanderX, ny * 2f * m.wanderY);

            float rot = m.baseRot + (Mathf.PerlinNoise(m.seedRot, t * speed * 0.7f) - 0.5f) * 2f * m.rotRange;
            float breathe = 0.65f + 0.35f * Mathf.Sin(t * m.breatheFreq + m.breathePhase);
            // derrière le perso : aucune atténuation nécessaire (il est devant, opaque)
            float protect = (_backRoot != null && !m.front) ? 0f : (m.front ? 0.5f : 1f);

            // Enveloppe horizontale : au-delà de _mistHorizontalReach (fraction de la
            // demi-largeur de la zone) la nappe s'efface - elle ne s'éparpille pas sur les côtés.
            float reach = _areaSize.x * 0.5f * _mistHorizontalReach;
            float side = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(reach, reach * 1.9f, Mathf.Abs(pos.x)));

            float a = m.baseAlpha * breathe * side * _mistAlphaScale * EdgeWeight(pos, protect) * k;

            m.rt.anchoredPosition = pos;
            m.rt.localRotation = Quaternion.Euler(0f, 0f, rot);
            m.cr.SetColor(new Color(m.rgb.r, m.rgb.g, m.rgb.b, a));
        }
    }

    private void UpdateBokeh(float t, float k)
    {
        for (int i = 0; i < _bokehs.Count; i++)
        {
            Bokeh b = _bokehs[i];
            float age = t - b.birth;
            float u = age / b.life;
            if (u >= 1f)
            {
                RespawnBokeh(b, t, false);
                age = 0f;
                u = 0f;
            }

            float env = Mathf.SmoothStep(0f, 1f, u / 0.3f) * (1f - Mathf.SmoothStep(0f, 1f, (u - 0.6f) / 0.4f));
            Vector2 pos = new Vector2(
                b.startX + b.driftX * age + Mathf.Sin(age * b.swayFreq + b.swayPhase) * b.swayAmp,
                b.startY + b.driftY * age);
            float breathe = 0.75f + 0.25f * Mathf.Sin(age * 0.4f + b.swayPhase);

            b.rt.anchoredPosition = pos;
            b.cr.SetColor(new Color(b.rgb.r, b.rgb.g, b.rgb.b, b.maxAlpha * env * breathe * k));
        }
    }

    private void UpdateMotes(float t, float k)
    {
        for (int i = 0; i < _motes.Count; i++)
        {
            Mote m = _motes[i];
            float age = t - m.birth;
            float u = age / m.life;
            if (u >= 1f)
            {
                RespawnMote(m, t, false);
                age = 0f;
                u = 0f;
            }

            float env = Mathf.SmoothStep(0f, 1f, u / 0.2f) * (1f - Mathf.SmoothStep(0f, 1f, (u - 0.6f) / 0.4f));
            float twinkle = 0.7f + 0.3f * Mathf.Sin(age * m.twFreq + m.twPhase);
            Vector2 pos = new Vector2(
                m.startX + Mathf.Sin(age * m.swayFreq + m.swayPhase) * m.swayAmp,
                m.startY + m.rise * age);

            // les poussières ont le droit de passer devant le perso, mais bien atténuées
            // (constaté en rendu : des orbes vifs sur le torse concurrençaient le personnage)
            float a = m.maxAlpha * env * twinkle * EdgeWeight(pos, 0.9f) * k;

            m.rt.anchoredPosition = pos;
            m.cr.SetColor(new Color(m.rgb.r, m.rgb.g, m.rgb.b, a));
        }
    }

    private void UpdateGlints(float t, float k)
    {
        for (int i = 0; i < _glints.Count; i++)
        {
            Glint g = _glints[i];
            float u = (t - g.start) / g.duration;

            if (u < 0f)
            {
                g.cr.SetColor(new Color(g.rgb.r, g.rgb.g, g.rgb.b, 0f));
                continue;
            }

            if (u > 1f)
            {
                ScheduleGlint(g, t + Random.Range(_glintIntervalRange.x, _glintIntervalRange.y));
                g.cr.SetColor(new Color(g.rgb.r, g.rgb.g, g.rgb.b, 0f));
                continue;
            }

            float bell = Mathf.Sin(u * Mathf.PI);
            bell *= bell;

            g.rt.anchoredPosition = g.pos;
            g.rt.sizeDelta = new Vector2(g.size, g.size);
            g.rt.localScale = Vector3.one * (0.55f + 0.45f * bell);
            g.rt.localRotation = Quaternion.Euler(0f, 0f, g.rot + u * 15f);
            g.cr.SetColor(new Color(g.rgb.r, g.rgb.g, g.rgb.b, _glintMaxAlpha * bell * k));
        }
    }

    // --- utilitaires -----------------------------------------------------

    // Poids 0..1 selon la distance au centre de la zone (elliptique) : proche du
    // centre = atténué par _characterProtection (le personnage reste lisible),
    // périphérie = plein. protectionScale permet d'être plus indulgent pour les
    // petits éléments (poussières) que pour les grandes nappes.
    private float EdgeWeight(Vector2 p, float protectionScale)
    {
        Vector2 half = _areaSize * 0.5f;
        float d = new Vector2(p.x / half.x, p.y / half.y).magnitude;
        float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.7f, d));
        return Mathf.Lerp(1f - _characterProtection * protectionScale, 1f, edge);
    }

    private Color Tinted(float whiten) => Color.Lerp(_tint, Color.white, whiten);

    private Sprite MakeSprite(int size, System.Func<float, float, float> alphaAt)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.Clamp01(alphaAt(u, v));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f + 0.5f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        _owned.Add(tex);
        _owned.Add(sprite);
        return sprite;
    }

    // Nappe de brume : bruit fractal (4 octaves) contrasté puis multiplié par un
    // masque radial qui tombe à 0 avant le bord de la texture - aucune arête.
    private Sprite MakeMistSprite(int size, float seed)
    {
        return MakeSprite(size, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float mask = 1f - Mathf.SmoothStep(0.30f, 1f, r);
            if (mask <= 0f) return 0f;

            float n = 0f, amp = 0.5f, freq = 2.2f, norm = 0f;
            for (int o = 0; o < 4; o++)
            {
                n += amp * Mathf.PerlinNoise(seed + (u * 0.5f + 0.5f) * freq, seed * 0.7f + (v * 0.5f + 0.5f) * freq);
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            n /= norm;
            n = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.26f, 0.76f, n));
            return n * mask;
        });
    }
}
