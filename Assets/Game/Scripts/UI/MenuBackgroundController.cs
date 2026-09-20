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
// Respecte Paramètres > Graphismes > Effets d'ambiance des menus : désactivé = image immobile.
[RequireComponent(typeof(RectTransform))]
public class MenuBackgroundController : MonoBehaviour
{
    public const float ArtWidth = 1920f, ArtHeight = 1280f;

    [SerializeField] private Image[] _layers = new Image[3];          // Aether, Kael, Lyra
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
        new[] { new Zone(575f, 1070f, 80f, 60f, 4f, 1.5f, 1.7f, 1.2f) },                                             // cape d'Aether
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

    private RectTransform _rect;
    private readonly float[] _alpha = new float[3];
    private readonly Material[] _mats = new Material[3];
    private Vector2 _lastSize;
    private int _current = -1;
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
        for (int i = 0; i < _layers.Length; i++)
        {
            if (_layers[i] == null) continue;
            Material m = _windShader != null ? new Material(_windShader) : null;
            _mats[i] = m;
            if (m != null)
            {
                _layers[i].material = m;
                ApplyZones(m, i);
            }
            _alpha[i] = 0f;
            SetLayerAlpha(i, 0f);
        }
    }

    private void OnEnable()
    {
        int wanted = SelectedIndex();
        // 1re ouverture : instantané. Ouvertures suivantes : fondu si le personnage a changé.
        if (_current < 0)
        {
            _current = wanted;
            for (int i = 0; i < _layers.Length; i++) { _alpha[i] = i == wanted ? 1f : 0f; SetLayerAlpha(i, _alpha[i]); }
        }
        else
        {
            _current = wanted;
        }
        _dirtyLayout = true;
    }

    private static int SelectedIndex()
    {
        MetaProgressionManager meta = MetaProgressionManager.Instance;
        return meta != null ? Mathf.Clamp(meta.GetSelectedCharacterIndex(), 0, 2) : 0;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float t = Time.unscaledTime;

        // ---- fond du personnage (fondu enchaîné) ----
        int wanted = SelectedIndex();
        if (wanted != _current) _current = wanted;
        for (int i = 0; i < _layers.Length; i++)
        {
            if (_layers[i] == null) continue;
            float target = i == _current ? 1f : 0f;
            _alpha[i] = Mathf.MoveTowards(_alpha[i], target, dt / Mathf.Max(0.05f, _fadeSeconds));
            SetLayerAlpha(i, _alpha[i]);
        }

        // ---- cadrage « cover » quand la taille de la page change ----
        if (_dirtyLayout || (_rect.rect.size - _lastSize).sqrMagnitude > 0.25f)
        {
            _lastSize = _rect.rect.size;
            _dirtyLayout = false;
            Rect art = ArtRect();
            for (int i = 0; i < _layers.Length; i++)
            {
                if (_layers[i] == null) continue;
                RectTransform lr = _layers[i].rectTransform;
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
        Image img = _layers[i];
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
