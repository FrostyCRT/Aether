using UnityEngine;
using UnityEngine.UI;

// AJOUTE (2026-09-20) - FOND DU MENU PRINCIPAL selon le personnage choisi (3 illustrations : Aether, Kael, Lyra) et
// VENT sur le décor.
//
// - Affiche le fond du personnage sélectionné (MetaProgressionManager.GetSelectedCharacterIndex) ; quand la sélection
//   change (onglet Personnages), le nouveau fond apparaît en fondu enchaîné à l'ouverture du menu.
// - Chaque fond est un Image « cover » (remplit la page sans déformer, calé vers le bas : le sol et les personnages
//   restent visibles, on rogne surtout le ciel) avec le shader Aether/UI/WindSway : l'arbre, le lierre, les
//   bannières, les herbes et les nuages bougent au vent DANS l'image. Les zones sont des ellipses en pixels de
//   l'illustration (1920 x 1280), propres à chaque fond (les personnages n'ont pas les mêmes bannières / capes).
// - Expose Gust (rafale, 0..1) : les feuilles (MenuLeavesFX) l'utilisent aussi, pour que tout réagisse ensemble.
//
// - AJOUTÉ (2026-10-29, demande utilisateur) - l'heure du décor suit la difficulté choisie : Modéré = jour, Violent =
//   crépuscule, Apocalyptique = nuit (3 illustrations par personnage, même cadrage). Changer de difficulté ou de
//   personnage fait apparaître le nouveau fond en fondu PAR-DESSUS l'ancien (aucun creux sombre pendant le fondu).
//   Les images de la scène (_layers) servent de modèles : 6 emplacements au total, chacun reçoit le fond demandé.
//
// Respecte Paramètres > Graphismes > Effets d'ambiance des menus : désactivé = image immobile.
[RequireComponent(typeof(RectTransform))]
public class MenuBackgroundController : MonoBehaviour
{
    public const float ArtWidth = 1920f, ArtHeight = 1280f;

    [SerializeField] private Image[] _layers = new Image[3];          // Aether, Kael, Lyra (modèles des emplacements)
    [Header("Fonds selon la difficulté (Aether, Kael, Lyra)")]
    [SerializeField] private Sprite[] _day = new Sprite[3];           // Modéré
    [SerializeField] private Sprite[] _dusk = new Sprite[3];          // Violent
    [SerializeField] private Sprite[] _night = new Sprite[3];         // Apocalyptique
    [SerializeField] private Shader _windShader;
    [SerializeField] private float _fadeSeconds = 0.9f;
    [Tooltip("Part du rognage vertical retirée EN BAS (0 = on garde tout le bas de l'image, 1 = tout le haut).")]
    [Range(0f, 1f)] [SerializeField] private float _bottomCropBias = 0.12f;

    // zone de vent : centre et rayons (pixels de l'illustration), amplitude (px), vitesse, fréquence spatiale
    private struct Zone
    {
        public float x, y, rx, ry, ampX, ampY, speed, freq;
        public Zone(float x, float y, float rx, float ry, float ampX, float ampY, float speed, float freq)
        { this.x = x; this.y = y; this.rx = rx; this.ry = ry; this.ampX = ampX; this.ampY = ampY; this.speed = speed; this.freq = freq; }
    }

    // communes aux 3 fonds (même décor)
    private static readonly Zone[] Common =
    {
        new Zone(1730f, 720f, 250f, 200f, 7f, 3.5f, 0.85f, 2.6f),      // cime de l'arbre
        new Zone(190f, 380f, 170f, 340f, 4f, 2f, 0.70f, 3.0f),         // lierre de la tour
        new Zone(1150f, 935f, 620f, 42f, 2f, 1f, 0.95f, 5.0f),        // lierre du haut du mur (la pierre reste fixe)
        new Zone(1100f, 250f, 850f, 260f, 4.5f, 1.5f, 0.16f, 0.6f),    // nuages : dérive très lente
        new Zone(40f, 1190f, 130f, 110f, 6f, 3f, 1.20f, 3.5f),         // herbes, coin bas gauche
        new Zone(1860f, 1200f, 100f, 110f, 7f, 3f, 1.35f, 3.5f),      // plante, coin bas droit
        new Zone(1375f, 1005f, 48f, 62f, 6f, 2f, 2.1f, 1.5f),          // bannière du mur
    };

    // propres à chaque personnage : [0] Aether, [1] Kael, [2] Lyra
    private static readonly Zone[][] PerCharacter =
    {
        // MODIFIÉ (2026-10-29, fond MenuAetherV2 : Aether plus petit et plus à gauche) - zone recalée sur le pan de cape
        new[] { new Zone(370f, 1050f, 55f, 45f, 3f, 1.2f, 1.7f, 1.2f) },                                             // cape d'Aether
        new[] { new Zone(495f, 955f, 45f, 62f, 6f, 2f, 2.0f, 1.5f), new Zone(235f, 880f, 45f, 40f, 2f, 1f, 1.4f, 1.0f) },   // bannière + écharpe de Kael
        new[] { new Zone(495f, 950f, 45f, 62f, 6f, 2f, 2.0f, 1.5f) },                                              // bannière (Lyra reste immobile)
    };

    // Force globale du vent sur le décor (1 = réglage d'origine, jugé un peu trop fort à 1).
    private const float WindStrength = 0.6f;

    // Zones où RIEN ne bouge (le personnage) : ellipses en pixels de l'illustration (centre, rayons), par personnage.
    private static readonly Vector4[][] Protected =
    {
        new Vector4[0],                                                              // Aether : sa cape a sa propre zone
        new[] { new Vector4(245f, 950f, 100f, 200f) },                               // Kael
        new[] { new Vector4(1765f, 900f, 100f, 215f) },                              // Lyra, collée à l'arbre : immobile
    };

    private static readonly int GustId = Shader.PropertyToID("_Gust");
    private static readonly int SwayId = Shader.PropertyToID("_Sway");

    private const int SlotCount = 6;
    private readonly System.Collections.Generic.List<Transform> _front = new System.Collections.Generic.List<Transform>();

    private RectTransform _rect;
    private Image[] _slots;
    private Material[] _mats;
    private float[] _alpha;
    private int[] _slotKey;            // fond affiché par chaque emplacement : personnage * 3 + difficulté (-1 = aucun)
    private int _currentKey = -1;
    private Vector2 _lastSize;
    private bool _dirtyLayout = true;

    // Rafale courante (0..1), lue par MenuLeavesFX.
    public float Gust { get; private set; }

    // Rectangle de l'illustration affichée, dans le repère de ce conteneur (pour poser des éléments sur le décor).
    public Vector2 ArtToLocal(float artX, float artY)
    {
        Rect r = ArtRect();
        return new Vector2(r.xMin + artX / ArtWidth * r.width, r.yMax - artY / ArtHeight * r.height);
    }

    public Rect ArtRect()
    {
        Rect c = _rect != null && _rect.rect.width > 10f ? _rect.rect : new Rect(-960f, -490f, 1920f, 980f);
        float scale = Mathf.Max(c.width / ArtWidth, c.height / ArtHeight);
        float w = ArtWidth * scale, h = ArtHeight * scale;
        float crop = Mathf.Max(0f, h - c.height);
        // bas aligné (moins le biais), centré en largeur
        float yMin = c.yMin - _bottomCropBias * crop;
        return new Rect(c.center.x - w * 0.5f, yMin, w, h);
    }

    private void Awake()
    {
        _rect = (RectTransform)transform;

        // emplacements : les 3 images de la scène + 3 copies (un fondu peut croiser deux fonds du même personnage)
        Image model = null;
        foreach (Image l in _layers) if (l != null) { model = l; break; }
        _slots = new Image[SlotCount];
        for (int i = 0; i < SlotCount; i++)
        {
            Image img = i < _layers.Length ? _layers[i] : null;
            if (img == null && model != null)
            {
                img = Instantiate(model, model.transform.parent);
                img.name = "Layer_Slot" + i;
            }
            _slots[i] = img;
        }
        _mats = new Material[SlotCount];
        _alpha = new float[SlotCount];
        _slotKey = new int[SlotCount];
        for (int i = 0; i < SlotCount; i++)
        {
            _slotKey[i] = -1;
            if (_slots[i] == null) continue;
            if (_windShader != null)
            {
                _mats[i] = new Material(_windShader);
                _slots[i].material = _mats[i];
            }
            SetLayerAlpha(i, 0f);
        }
        // les images de la scène gardent leur fond d'origine (crépuscule), utilisé si un fond manque
        for (int c = 0; c < 3; c++)
        {
            if (c < _layers.Length && _layers[c] != null && _dusk[c] == null) _dusk[c] = _layers[c].sprite;
        }
    }

    private void OnEnable()
    {
        // 1re ouverture : instantané. Ouvertures suivantes : fondu si le personnage ou la difficulté a changé.
        if (_currentKey < 0)
        {
            int key = WantedKey();
            int slot = Assign(key);
            for (int i = 0; i < SlotCount; i++) { _alpha[i] = i == slot ? 1f : 0f; SetLayerAlpha(i, _alpha[i]); }
            _currentKey = key;
        }
        _dirtyLayout = true;
    }

    private static int SelectedIndex()
    {
        MetaProgressionManager meta = MetaProgressionManager.Instance;
        return meta != null ? Mathf.Clamp(meta.GetSelectedCharacterIndex(), 0, 2) : 0;
    }

    private static int WantedKey() => SelectedIndex() * 3 + Mathf.Clamp((int)DifficultyTiers.Current, 0, 2);

    private Sprite SpriteFor(int key)
    {
        int c = key / 3, tier = key % 3;
        Sprite[] set = tier == 0 ? _day : tier == 1 ? _dusk : _night;
        Sprite s = set != null && c < set.Length ? set[c] : null;
        return s != null ? s : (_dusk != null && c < _dusk.Length ? _dusk[c] : null);
    }

    // Emplacement qui affiche `key` ; à défaut, le plus effacé reçoit ce fond et passe au-dessus des autres.
    private int Assign(int key)
    {
        int best = -1;
        for (int i = 0; i < SlotCount; i++)
        {
            if (_slots[i] == null) continue;
            if (_slotKey[i] == key) return i;
            if (best < 0 || _alpha[i] < _alpha[best]) best = i;
        }
        if (best < 0) return -1;
        _slotKey[best] = key;
        _slots[best].sprite = SpriteFor(key);
        if (_mats[best] != null) ApplyZones(_mats[best], key / 3);
        _slots[best].transform.SetAsLastSibling();
        // les éléments posés sur le décor (feuilles...) restent devant les fonds
        _front.Clear();
        foreach (Transform child in transform)
            if (!child.TryGetComponent(out Image img) || System.Array.IndexOf(_slots, img) < 0) _front.Add(child);
        foreach (Transform child in _front) child.SetAsLastSibling();
        _dirtyLayout = true;
        return best;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float t = Time.unscaledTime;

        // ---- fond du personnage et de la difficulté : le nouveau apparaît en fondu PAR-DESSUS l'ancien ----
        int wanted = WantedKey();
        int top = -1;
        for (int i = 0; i < SlotCount; i++) if (_slotKey[i] == wanted && _slots[i] != null) top = i;
        if (wanted != _currentKey || top < 0)
        {
            _currentKey = wanted;
            if (top < 0) top = Assign(wanted);
            else _slots[top].transform.SetAsLastSibling();
        }
        if (top >= 0)
        {
            _alpha[top] = Mathf.MoveTowards(_alpha[top], 1f, dt / Mathf.Max(0.05f, _fadeSeconds));
            SetLayerAlpha(top, _alpha[top]);
            bool covered = _alpha[top] >= 1f;   // le nouveau fond couvre tout : les autres peuvent disparaître
            for (int i = 0; i < SlotCount; i++)
            {
                if (i == top || _slots[i] == null || _alpha[i] <= 0f) continue;
                if (covered) { _alpha[i] = 0f; SetLayerAlpha(i, 0f); }
            }
        }

        // ---- cadrage « cover » quand la taille de la page change ----
        if (_dirtyLayout || (_rect.rect.size - _lastSize).sqrMagnitude > 0.25f)
        {
            _lastSize = _rect.rect.size;
            _dirtyLayout = false;
            Rect art = ArtRect();
            for (int i = 0; i < SlotCount; i++)
            {
                if (_slots[i] == null) continue;
                RectTransform lr = _slots[i].rectTransform;
                lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(0.5f, 0.5f);
                lr.sizeDelta = art.size;
                lr.anchoredPosition = art.center;
            }
        }

        // ---- vent : une brise continue, ponctuée de rafales ----
        float factor = GameSettings.MenuEffectsFactor;
        float noise = Mathf.PerlinNoise(t * 0.10f, 3.7f);
        float gust = Mathf.Clamp01((noise - 0.50f) / 0.30f);
        gust = gust * gust * (3f - 2f * gust);
        Gust = Mathf.Clamp01(0.10f + 0.90f * gust) * factor;
        for (int i = 0; i < _mats.Length; i++)
        {
            if (_mats[i] == null) continue;
            _mats[i].SetFloat(GustId, Gust);
            _mats[i].SetFloat(SwayId, factor);
        }
    }

    private void SetLayerAlpha(int i, float a)
    {
        Image img = _slots[i];
        if (img == null) return;
        Color c = img.color; c.a = a; img.color = c;
        if (img.enabled != a > 0.001f) img.enabled = a > 0.001f;
    }

    private void ApplyZones(Material m, int character)
    {
        Zone[] extra = PerCharacter[Mathf.Clamp(character, 0, 2)];
        int n = Common.Length + extra.Length;
        Vector4[] a = new Vector4[12], b = new Vector4[12];
        for (int i = 0; i < n && i < 12; i++)
        {
            Zone z = i < Common.Length ? Common[i] : extra[i - Common.Length];
            // pixels de l'illustration -> UV (V vers le haut) ; amplitudes -> UV
            a[i] = new Vector4(z.x / ArtWidth, 1f - z.y / ArtHeight, z.rx / ArtWidth, z.ry / ArtHeight);
            b[i] = new Vector4(z.ampX * WindStrength / ArtWidth, z.ampY * WindStrength / ArtHeight, z.speed, z.freq);
        }
        m.SetVectorArray("_WindA", a);
        m.SetVectorArray("_WindB", b);
        m.SetFloat("_WindCount", Mathf.Min(12, n));

        Vector4[] prot = Protected[Mathf.Clamp(character, 0, 2)];
        Vector4[] pa = new Vector4[4];
        for (int i = 0; i < prot.Length && i < 4; i++)
            pa[i] = new Vector4(prot[i].x / ArtWidth, 1f - prot[i].y / ArtHeight, prot[i].z / ArtWidth, prot[i].w / ArtHeight);
        m.SetVectorArray("_ProtectA", pa);
        m.SetFloat("_ProtectCount", Mathf.Min(4, prot.Length));
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _mats.Length; i++) if (_mats[i] != null) Destroy(_mats[i]);
    }
}
