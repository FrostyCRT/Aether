using UnityEngine;
using UnityEngine.UI;

// Barre de navigation du menu principal : ajoute un MenuTabAnimator à chaque onglet et un liseré doré qui GLISSE d'un
// onglet à l'autre (position et largeur amorties) avec un halo qui respire doucement. Créé et branché automatiquement par
// MainMenuManager (aucun objet à poser dans la scène). Le liseré ignore le HorizontalLayoutGroup de la barre.
public class MenuTabBarFX : MonoBehaviour
{
    [Tooltip("Liseré doré (et halo) sous l'onglet actif. Désactivé (2026-09-26) : demande utilisateur, l'onglet actif se distingue déjà par sa couleur et sa taille.")]
    [SerializeField] private bool _showIndicator = false;
    [SerializeField] private Color _lineColor = new Color(1f, 0.79f, 0.30f, 1f);   // or, comme les états « prêt / maximum » du HUD
    [SerializeField] private float _lineHeight = 6f;
    [SerializeField] private float _glowExtraHeight = 22f;
    [SerializeField] private float _slideSmoothTime = 0.12f;
    [Header("Fond de la barre (2026-09-26)")]
    [Tooltip("Poutre de bois sombre veinée, liserés de laiton et clous, dans l'esprit des parchemins / cadres dorés du jeu (remplace la bande grise unie).")]
    [SerializeField] private bool _fancyBackground = true;
    [SerializeField] private Color _woodTop = new Color(0.310f, 0.205f, 0.115f, 1f);       // bois chaud
    [SerializeField] private Color _woodBottom = new Color(0.165f, 0.105f, 0.062f, 1f);
    [SerializeField] private Color _trimColor = new Color(0.80f, 0.61f, 0.27f, 1f);        // laiton / or vieilli
    [SerializeField] [Range(0f, 1f)] private float _grain = 0.55f;                          // intensité des veines
    [SerializeField] private bool _studs = true;
    [SerializeField] private float _shadowHeight = 30f;
    [Tooltip("Espace entre le bas du ruban et le liseré, en pixels du canvas.")]
    [SerializeField] private float _gapBelowTab = 2f;

    private RectTransform _bar;
    private RectTransform _line;
    private RectTransform _glow;
    private Image _lineImage;
    private Image _glowImage;

    private RectTransform _activeTab;
    private bool _snapNext = true;

    private Vector3 _worldPos;
    private Vector3 _worldVel;
    private float _worldWidth;
    private float _widthVel;

    public void Init(Image[] tabImages)
    {
        _bar = transform as RectTransform;

        foreach (Image tab in tabImages)
        {
            if (tab == null) continue;
            if (tab.GetComponent<MenuTabAnimator>() == null)
                tab.gameObject.AddComponent<MenuTabAnimator>();
        }

        if (_fancyBackground) BuildBackground();
        if (_showIndicator) BuildIndicator();
    }

    // ---- Fond de la barre -----------------------------------------------------------------------------------------
    // Poutre de bois sombre (dégradé + veines horizontales), liserés de laiton identiques en haut et en bas, clous
    // en laiton entre les onglets et ombre chaude qui retombe sur la page. Tout est procédural (aucune image à importer).
    private const float TrimHeight = 3f;
    private const float TopOverscan = 3f;

    private void BuildBackground()
    {
        Image bar = GetComponent<Image>();
        if (bar != null)
        {
            bar.sprite = BuildWoodSprite(_woodTop, _woodBottom, _grain);
            bar.type = Image.Type.Simple;
            bar.color = Color.white;
        }

        RectTransform shadow = NewDecor("BarShadow");
        shadow.anchorMin = new Vector2(0f, 0f);
        shadow.anchorMax = new Vector2(1f, 0f);
        shadow.pivot = new Vector2(0.5f, 1f);
        shadow.anchoredPosition = new Vector2(0f, 0f);
        shadow.sizeDelta = new Vector2(0f, _shadowHeight);
        Image sh = shadow.GetComponent<Image>();
        sh.sprite = BuildShadowSprite();
        sh.color = Color.white;

        // liserés de laiton identiques en haut et en bas (même épaisseur, même couleur)
        AddBand("BarTrimBottom", false, TrimHeight, _trimColor, 0f);
        AddBand("BarTrimTop", true, TrimHeight, _trimColor, -TopOverscan);   // la barre dépasse de ~3 px en haut de l écran : on descend le liseré pour qu il soit visible

        if (_studs)
        {
            // clous en laiton : à mi-chemin entre deux onglets (pas aux extrémités)
            float[] xs = { 0.5f - 0.0908f, 0.5f + 0.0908f, 0.5f - 0.2725f, 0.5f + 0.2725f };
            foreach (float fx in xs)
            {
                RectTransform st = NewDecor("BarStud");
                st.anchorMin = st.anchorMax = new Vector2(fx, 0.5f);
                st.pivot = new Vector2(0.5f, 0.5f);
                st.anchoredPosition = Vector2.zero;
                st.sizeDelta = new Vector2(16f, 16f);
                Image si = st.GetComponent<Image>();
                si.sprite = BuildStudSprite();
                si.color = Color.white;
            }
        }

        // les onglets restent au-dessus de tous les décors
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform ch = transform.GetChild(i);
            if (ch.name.StartsWith("Bar")) ch.SetAsFirstSibling();
        }
    }

    // bandeau pleine largeur collé au bord bas (atTop = false) ou haut (atTop = true) de la barre, décalé de offsetY
    private void AddBand(string name, bool atTop, float height, Color color, float offsetY)
    {
        RectTransform r = NewDecor(name);
        float ay = atTop ? 1f : 0f;
        r.anchorMin = new Vector2(0f, ay);
        r.anchorMax = new Vector2(1f, ay);
        r.pivot = new Vector2(0.5f, ay);
        r.anchoredPosition = new Vector2(0f, offsetY);
        r.sizeDelta = new Vector2(0f, height);
        r.GetComponent<Image>().color = color;
    }

    private RectTransform NewDecor(string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(transform, false);
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        go.GetComponent<Image>().raycastTarget = false;
        return go.GetComponent<RectTransform>();
    }

    private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    // Bois : dégradé vertical + veines allongées (bruit étiré horizontalement) + légère vignette aux extrémités.
    private static Sprite BuildWoodSprite(Color top, Color bottom, float grain)
    {
        const int w = 512, h = 64;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < h; y++)
        {
            float v = y / (h - 1f);                                   // 0 = bas, 1 = haut
            Color baseC = Color.Lerp(bottom, top, Smooth(v));
            for (int x = 0; x < w; x++)
            {
                float u = x / (w - 1f);
                // veines : peu de variation en x, beaucoup en y ; deux couches pour un rendu naturel
                float n1 = Mathf.PerlinNoise(u * 6f + 3.1f, v * 9f + 0.7f);
                float n2 = Mathf.PerlinNoise(u * 22f + 11.3f, v * 34f + 5.9f);
                float streak = (n1 * 0.65f + n2 * 0.35f - 0.5f) * 2f;   // -1..1
                float k = 1f + streak * 0.22f * grain;
                float edge = Mathf.Min(u, 1f - u);
                float vig = (0.86f + 0.14f * Smooth(edge / 0.12f)) * k;
                tex.SetPixel(x, y, new Color(baseC.r * vig, baseC.g * vig, baseC.b * vig, 1f));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    // clou de laiton : disque bombé (reflet en haut à gauche) avec un anneau sombre
    private static Sprite _studSprite;
    private static Sprite BuildStudSprite()
    {
        if (_studSprite != null) return _studSprite;
        const int n = 32;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Vector2 light = new Vector2(-0.35f, 0.35f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float px = (x + 0.5f) / n * 2f - 1f, py = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(px * px + py * py);
                if (r > 1f) { tex.SetPixel(x, y, Color.clear); continue; }
                float a = Smooth((1f - r) / 0.08f);
                float ring = Smooth((r - 0.78f) / 0.22f);                      // anneau sombre au bord
                float hl = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(px, py), light) / 1.0f);
                Color brass = Color.Lerp(new Color(0.55f, 0.38f, 0.15f), new Color(1f, 0.86f, 0.52f), hl * hl);
                brass = Color.Lerp(brass, new Color(0.13f, 0.08f, 0.04f), ring * 0.85f);
                brass.a = a;
                tex.SetPixel(x, y, brass);
            }
        tex.Apply();
        _studSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        return _studSprite;
    }

    // ombre chaude sous la barre : sombre près de la barre, transparente en bas
    private static Sprite BuildShadowSprite()
    {
        const int h = 32;
        Texture2D tex = new Texture2D(2, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < h; y++)
        {
            float v = y / (h - 1f);                                   // 0 = bas, 1 = contre la barre
            float a = 0.55f * v * v * v;
            for (int x = 0; x < 2; x++) tex.SetPixel(x, y, new Color(0.05f, 0.028f, 0.010f, a));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 2, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    private void BuildIndicator()
    {
        if (_line != null) return;

        GameObject glowGO = new GameObject("TabIndicatorGlow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        glowGO.transform.SetParent(transform, false);
        _glow = glowGO.GetComponent<RectTransform>();
        _glowImage = glowGO.GetComponent<Image>();
        _glowImage.raycastTarget = false;
        glowGO.GetComponent<LayoutElement>().ignoreLayout = true;

        GameObject lineGO = new GameObject("TabIndicatorLine", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        lineGO.transform.SetParent(transform, false);
        _line = lineGO.GetComponent<RectTransform>();
        _lineImage = lineGO.GetComponent<Image>();
        _lineImage.raycastTarget = false;
        lineGO.GetComponent<LayoutElement>().ignoreLayout = true;
        _lineImage.color = _lineColor;

        foreach (RectTransform r in new[] { _glow, _line })
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
        }

        // Halo doux : dégradé vertical généré une fois (opaque au centre, transparent aux bords).
        _glowImage.sprite = BuildSoftSprite();
        _glowImage.color = new Color(_lineColor.r, _lineColor.g, _lineColor.b, 0.35f);

        _glow.gameObject.SetActive(false);
        _line.gameObject.SetActive(false);
    }

    private static Sprite _softSprite;
    private static Sprite BuildSoftSprite()
    {
        if (_softSprite != null) return _softSprite;

        const int h = 32;
        Texture2D tex = new Texture2D(4, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < h; y++)
        {
            float t = 1f - Mathf.Abs((y + 0.5f) / h * 2f - 1f);   // 0 aux bords, 1 au centre
            float a = t * t * (3f - 2f * t);                        // smoothstep
            for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        _softSprite = Sprite.Create(tex, new Rect(0, 0, 4, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(1, 0, 1, 0));
        return _softSprite;
    }

    // Désigne l'onglet actif : le liseré glisse vers lui (instantané au tout premier appel ou si snap = true).
    public void SetActiveTab(RectTransform tab, bool snap = false)
    {
        _activeTab = tab;
        if (snap) _snapNext = true;
        if (_line != null && tab != null)
        {
            _line.gameObject.SetActive(true);
            _glow.gameObject.SetActive(true);
        }
    }

    private void OnEnable()
    {
        _snapNext = true;
    }

    private void LateUpdate()
    {
        if (_activeTab == null || _line == null || !_activeTab.gameObject.activeInHierarchy) return;

        // Cible en coordonnées MONDE (indépendant des ancrages) : centre-bas du ruban actif, à sa taille affichée.
        Vector3[] c = new Vector3[4];
        _activeTab.GetWorldCorners(c);
        Vector3 bottomCenter = (c[0] + c[3]) * 0.5f;
        float scaleY = _bar.lossyScale.y;
        Vector3 targetPos = bottomCenter + Vector3.down * ((_gapBelowTab + _lineHeight * 0.5f) * scaleY);
        float targetWidth = Vector3.Distance(c[0], c[3]);   // largeur monde du ruban actif

        float dt = Time.unscaledDeltaTime;
        if (_snapNext)
        {
            _worldPos = targetPos;
            _worldWidth = targetWidth;
            _worldVel = Vector3.zero;
            _widthVel = 0f;
            _snapNext = false;
        }
        else
        {
            _worldPos = Vector3.SmoothDamp(_worldPos, targetPos, ref _worldVel, _slideSmoothTime, Mathf.Infinity, dt);
            _worldWidth = Mathf.SmoothDamp(_worldWidth, targetWidth, ref _widthVel, _slideSmoothTime, Mathf.Infinity, dt);
        }

        float scaleX = Mathf.Max(0.0001f, _bar.lossyScale.x);
        _line.position = _worldPos;
        _line.sizeDelta = new Vector2(_worldWidth / scaleX * 0.9f, _lineHeight);

        // Le halo respire : alpha et hauteur oscillent lentement.
        float breathe = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.2f);
        _glow.position = _worldPos;
        _glow.sizeDelta = new Vector2(_worldWidth / scaleX * 0.95f, _lineHeight + _glowExtraHeight * (0.75f + 0.25f * breathe));
        _glowImage.color = new Color(_lineColor.r, _lineColor.g, _lineColor.b, 0.22f + 0.2f * breathe);
    }
}
