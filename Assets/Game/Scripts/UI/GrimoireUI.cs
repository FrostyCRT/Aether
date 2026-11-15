using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// AJOUTÉ (2026-11-15, chantier Grimoire - emplacement choisi par l'utilisateur : « icône de livre en bas à gauche de
// l'Accueil », en face du bouton œil, pas de nouvelle page) - posé sous MenuPanel (page Accueil), construit par le code comme
// LastRunPanel. Un bouton rond (livre) ouvre une fenêtre par-dessus le menu :
//  - en-tête : titre, nombre de découvertes, éclats déjà gagnés par le Grimoire ;
//  - trois rangées : Cartes (11), Fusions (6), Boss (3). Une entrée non découverte est une silhouette sombre avec « ??? » et
//    sa prime (+5 / +15 / +20) ; découverte, elle montre son icône (portrait pour les boss) et son nom ;
//  - pied : au survol d'une entrée, sa description, ou comment la découvrir.
// Fermeture : croix, clic hors de la fenêtre, Échap / B. La règle des découvertes et les primes sont dans Grimoire.cs.
public class GrimoireUI : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset _titleFont;   // Bangers (titres)
    [SerializeField] private Sprite _eclatIcon;
    [Tooltip("Cartes puis fusions, dans l'ordre d'affichage (les fusions sont reconnues à leur type).")]
    [SerializeField] private UpgradeData[] _upgrades;
    [Tooltip("Portraits des boss 1, 2, 3 (rendus depuis leurs modèles 3D).")]
    [SerializeField] private Sprite[] _bossPortraits = new Sprite[3];
    [SerializeField] private string[] _bossNames = { "Le Dragon de Mana", "Le Cerf Ancestral", "La Source Corrompue" };

    private const float ButtonSize = 56f, Margin = 14f;
    private static readonly Vector2 WindowSize = new Vector2(1260f, 810f);
    private static readonly Color Wood = new Color(0.165f, 0.105f, 0.062f, 1f);
    private static readonly Color Gold = new Color(1f, 0.82f, 0.36f, 1f);
    private static readonly Color Parchment = new Color(0.95f, 0.88f, 0.74f, 1f);
    private static readonly Color Muted = new Color(0.80f, 0.72f, 0.60f, 0.85f);
    private static readonly Color EclatText = new Color(0.62f, 0.86f, 1f, 1f);
    private static readonly Color Locked = new Color(0.05f, 0.04f, 0.04f, 0.92f);

    public static bool IsOpen { get; private set; }

    private Canvas _rootCanvas;
    private GameObject _window;
    private CanvasGroup _windowGroup;
    private RectTransform _panel;
    private TextMeshProUGUI _progress, _footer;
    private readonly List<Tile> _tiles = new List<Tile>();
    private Coroutine _anim;

    private class Tile
    {
        public string id, name, description, hint;
        public int eclats;
        public Sprite sprite;
        public Image icon;
        public TextMeshProUGUI label, prime;
    }

    private void Start()
    {
        _rootCanvas = GetComponentInParent<Canvas>().rootCanvas;
        BuildButton();
        Loc.Changed += OnLanguageChanged;
    }

    private void OnDestroy()
    {
        Loc.Changed -= OnLanguageChanged;
        IsOpen = false;
    }

    private void OnDisable()
    {
        if (_window != null && _window.activeSelf) Close(instant: true);   // changement de page : la fenêtre ne reste pas ouverte
    }

    private void OnLanguageChanged()
    {
        if (_window == null) return;
        Destroy(_window);
        _window = null;
        _tiles.Clear();
        if (IsOpen) { IsOpen = false; Open(); }
    }

    private void Update()
    {
        if (IsOpen && GameInput.BackDown()) Close(instant: false);
        if (IsOpen) TickFooter();
    }

    // ---- bouton (même habillage que le bouton œil de MenuViewMode, en miroir) -----------------------------------------------
    private void BuildButton()
    {
        var go = new GameObject("GrimoireButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
        rt.anchoredPosition = new Vector2(Margin, Margin);
        rt.sizeDelta = new Vector2(ButtonSize, ButtonSize);
        var back = go.GetComponent<Image>();
        back.sprite = HudSprites.Disc;
        back.color = new Color(0.06f, 0.05f, 0.08f, 0.55f);

        Image ring = NewImage("Ring", rt, HudSprites.Ring, new Color(0.86f, 0.72f, 0.44f, 0.75f));
        Stretch(ring.rectTransform, 0f);
        Image book = NewImage("Book", rt, BookSprite, new Color(0.95f, 0.88f, 0.72f, 1f));
        Stretch(book.rectTransform, 13f);
        book.preserveAspect = true;

        var btn = go.GetComponent<Button>();
        var cb = btn.colors;
        cb.normalColor = Color.white; cb.highlightedColor = new Color(1.25f, 1.15f, 1f, 1f); cb.pressedColor = new Color(0.8f, 0.75f, 0.7f, 1f);
        cb.selectedColor = Color.white; cb.colorMultiplier = 1.2f; cb.fadeDuration = 0.1f;
        btn.colors = cb;
        btn.targetGraphic = ring;
        btn.onClick.AddListener(Open);
    }

    // ---- fenêtre ---------------------------------------------------------------------------------------------------------
    public void Open()
    {
        if (IsOpen) return;
        if (_window == null) BuildWindow();
        Refresh();
        IsOpen = true;
        _window.transform.SetAsLastSibling();
        _window.SetActive(true);
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(Fade(true));
    }

    private void Close(bool instant)
    {
        IsOpen = false;
        if (_window == null) return;
        if (_anim != null) StopCoroutine(_anim);
        if (instant || !isActiveAndEnabled) { _window.SetActive(false); return; }
        _anim = StartCoroutine(Fade(false));
    }

    private IEnumerator Fade(bool opening)
    {
        const float duration = 0.18f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float a = opening ? k : 1f - k;
            _windowGroup.alpha = a;
            float s = Mathf.Lerp(0.96f, 1f, opening ? 1f - (1f - k) * (1f - k) : 1f - k * 0.5f);
            _panel.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        _windowGroup.alpha = opening ? 1f : 0f;
        _panel.localScale = Vector3.one;
        if (!opening) _window.SetActive(false);
        _anim = null;
    }

    private void BuildWindow()
    {
        _window = new GameObject("GrimoireWindow", typeof(RectTransform), typeof(CanvasGroup));
        var wrt = (RectTransform)_window.transform;
        wrt.SetParent(_rootCanvas.transform, false);
        Stretch(wrt, 0f);
        _windowGroup = _window.GetComponent<CanvasGroup>();
        // Pas de Canvas propre (overrideSorting) : testé le 2026-11-15, la fenêtre ne s'affichait plus du tout. Dernier enfant
        // du Canvas racine (SetAsLastSibling à chaque ouverture) = au-dessus de tout le menu.

        // voile : assombrit le menu et ferme la fenêtre au clic
        Image dim = NewImage("Dim", wrt, null, new Color(0f, 0f, 0f, 0.62f));
        Stretch(dim.rectTransform, 0f);
        dim.raycastTarget = true;
        var dimBtn = dim.gameObject.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        dimBtn.onClick.AddListener(() => Close(instant: false));
        ButtonHoverFX.Exclude(dimBtn);

        Image panel = NewImage("Panel", wrt, null, Wood);
        panel.raycastTarget = true;   // un clic dans la fenêtre ne la ferme pas
        _panel = panel.rectTransform;
        _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(0.5f, 0.5f);
        _panel.sizeDelta = WindowSize;
        _panel.gameObject.AddComponent<BrassFrameBorder>();   // même cadre que le menu Pause

        float w = WindowSize.x, pad = 46f, y = -24f;
        TextMeshProUGUI title = NewText("Title", _panel, _titleFont != null ? _titleFont : GameFonts.Body, 58f, Gold, TextAlignmentOptions.Center);
        Place(title.rectTransform, 0f, y, w, 66f);
        title.text = Loc.T("Grimoire");
        y -= 60f;
        _progress = NewText("Progress", _panel, GameFonts.Body, 21f, Parchment, TextAlignmentOptions.Center);
        Place(_progress.rectTransform, 0f, y, w, 30f);
        y -= 36f;

        // bouton fermer
        var close = new GameObject("Close", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var crt = (RectTransform)close.transform;
        crt.SetParent(_panel, false);
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1f, 1f);
        crt.anchoredPosition = new Vector2(-22f, -22f);
        crt.sizeDelta = new Vector2(44f, 44f);
        Image cback = close.GetComponent<Image>();
        cback.sprite = HudSprites.Disc;
        cback.color = new Color(0f, 0f, 0f, 0.35f);
        TextMeshProUGUI cx = NewText("X", crt, _titleFont != null ? _titleFont : GameFonts.Body, 30f, Parchment, TextAlignmentOptions.Center);
        Stretch(cx.rectTransform, 0f);
        cx.text = "X";
        close.GetComponent<Button>().onClick.AddListener(() => Close(instant: false));

        // trois rangées
        var cards = new List<UpgradeData>();
        var fusions = new List<UpgradeData>();
        if (_upgrades != null)
            foreach (UpgradeData u in _upgrades)
            {
                if (u == null) continue;
                if (u.upgradeType == UpgradeType.Fusion) fusions.Add(u); else cards.Add(u);
            }

        y = Section(Loc.T("Cartes"), y, pad);
        y = Row(cards.Count, 98f, 98f, y, i => UpgradeTile(cards[i])) - 14f;
        y = Section(Loc.T("Fusions"), y, pad);
        y = Row(fusions.Count, 134f, 116f, y, i => UpgradeTile(fusions[i])) - 14f;
        y = Section(Loc.T("Boss"), y, pad);
        y = Row(3, 190f, 150f, y, BossTile) - 10f;

        _footer = NewText("Footer", _panel, GameFonts.Body, 19f, Muted, TextAlignmentOptions.Center);
        Place(_footer.rectTransform, pad, y, w - pad * 2f, 56f);
        _footer.enableWordWrapping = true;
        _window.SetActive(false);
    }

    private Tile UpgradeTile(UpgradeData u)
    {
        bool fusion = u.upgradeType == UpgradeType.Fusion;
        int eclats = Grimoire.UpgradeEclats(u);
        return new Tile
        {
            id = Grimoire.UpgradeId(u),
            name = u.Name,
            sprite = u.icon,
            eclats = eclats,
            description = fusion ? u.FusionSourcesLabel() + " : " + u.Description : u.Description,
            hint = fusion ? Loc.F("Obtiens cette fusion une première fois : +{0} éclats", eclats)
                          : Loc.F("Choisis cette carte une première fois : +{0} éclats", eclats),
        };
    }

    private Tile BossTile(int i)
    {
        string name = Loc.T(i < _bossNames.Length ? _bossNames[i] : "Boss");
        return new Tile
        {
            id = Grimoire.BossId(i + 1),
            name = name,
            sprite = _bossPortraits != null && i < _bossPortraits.Length ? _bossPortraits[i] : null,
            eclats = Grimoire.BossEclats,
            description = Loc.F("Boss n°{0}", i + 1),
            hint = Loc.F("Bats ce boss une première fois : +{0} éclats", Grimoire.BossEclats),
        };
    }

    private float Section(string label, float y, float pad)
    {
        TextMeshProUGUI t = NewText("Section", _panel, _titleFont != null ? _titleFont : GameFonts.Body, 28f, Gold, TextAlignmentOptions.MidlineLeft);
        Place(t.rectTransform, pad, y, 400f, 34f);
        t.text = label;
        Image rule = NewImage("Rule", _panel, OrnamentSprites.Rule(fadeEnds: true), new Color(0.86f, 0.66f, 0.32f, 0.45f));
        Place(rule.rectTransform, pad, y - 34f, WindowSize.x - pad * 2f, 6f);
        return y - 44f;
    }

    // Rangée centrée de n vignettes ; iconSize = côté de l'icône, tileWidth = place réservée à chaque vignette.
    private float Row(int n, float tileWidth, float iconSize, float y, System.Func<int, Tile> make)
    {
        float gap = 8f;
        float total = n * tileWidth + (n - 1) * gap;
        float x0 = (WindowSize.x - total) * 0.5f;
        float labelH = 24f;
        for (int i = 0; i < n; i++)
        {
            Tile tile = make(i);
            float x = x0 + i * (tileWidth + gap);
            var cell = new GameObject("Entry", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
            cell.SetParent(_panel, false);
            Place(cell, x, y, tileWidth, iconSize + labelH + 6f);
            Image cellBack = cell.GetComponent<Image>();
            cellBack.color = new Color(0f, 0f, 0f, 0f);   // zone de survol
            AddHover(cell.gameObject, tile);

            Image frame = NewImage("Frame", cell, HudSprites.Disc, new Color(0f, 0f, 0f, 0.38f));
            float s = Mathf.Min(iconSize, tileWidth);
            Place(frame.rectTransform, (tileWidth - s) * 0.5f, 0f, s, s);
            tile.icon = NewImage("Icon", frame.rectTransform, tile.sprite, Color.white);
            Stretch(tile.icon.rectTransform, s * 0.1f);
            tile.icon.preserveAspect = true;

            tile.prime = NewText("Prime", frame.rectTransform, GameFonts.Body, 20f, EclatText, TextAlignmentOptions.Center);
            Stretch(tile.prime.rectTransform, 0f);

            // centré (pas Top) : un nom réduit par la taille auto reste au milieu de sa ligne, comme les autres (retour 2026-11-15)
            tile.label = NewText("Name", cell, GameFonts.Body, 16f, Parchment, TextAlignmentOptions.Center);
            Place(tile.label.rectTransform, -6f, -s - 4f, tileWidth + 12f, labelH);
            tile.label.enableAutoSizing = true; tile.label.fontSizeMax = 16f; tile.label.fontSizeMin = 10f;
            _tiles.Add(tile);
        }
        return y - (Mathf.Min(iconSize, tileWidth) + labelH + 6f);
    }

    private void AddHover(GameObject go, Tile tile)
    {
        var trigger = go.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => QueueFooter(tile));
        trigger.triggers.Add(enter);
        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => QueueFooter(null));
        trigger.triggers.Add(exit);
    }

    // AJOUTÉ (2026-11-15, retour utilisateur : "quand on passe vite sur les vignettes, le message change trop vite") - le texte
    // du bas ne change qu'après un court arrêt du curseur sur la même vignette (ou hors des vignettes).
    private const float FooterDelay = 0.3f;
    private Tile _pendingTile;
    private float _pendingTimer = -1f;

    private void QueueFooter(Tile tile)
    {
        _pendingTile = tile;
        _pendingTimer = FooterDelay;
    }

    private void TickFooter()
    {
        if (_pendingTimer < 0f) return;
        _pendingTimer -= Time.unscaledDeltaTime;
        if (_pendingTimer < 0f) ShowFooter(_pendingTile);
    }

    private void ShowFooter(Tile tile)
    {
        if (_footer == null) return;
        if (tile == null) { _footer.text = Loc.T("Survole une illustration pour en lire la page"); return; }
        bool found = Grimoire.IsDiscovered(Data, tile.id);
        _footer.text = found ? "<color=#" + ColorUtility.ToHtmlStringRGB(Gold) + ">" + tile.name + "</color>  ·  " + tile.description : tile.hint;
    }

    private static SaveData Data => MetaProgressionManager.Instance != null ? MetaProgressionManager.Instance.Data : null;

    private void Refresh()
    {
        SaveData data = Data;
        int found = 0, eclats = 0;
        foreach (Tile t in _tiles)
        {
            bool d = Grimoire.IsDiscovered(data, t.id);
            if (d) { found++; eclats += t.eclats; }
            t.icon.enabled = t.sprite != null;
            t.icon.color = d ? Color.white : Locked;   // silhouette sombre tant qu'elle n'est pas découverte
            t.label.text = d ? t.name : "???";
            t.label.color = d ? Parchment : Muted;
            t.prime.text = d ? "" : "+" + t.eclats;
        }
        _progress.text = Loc.F("{0} / {1} découvertes", found, _tiles.Count) + "   ·   "
                         + Loc.F("{0} éclats gagnés", eclats);
        _pendingTimer = -1f;
        ShowFooter(null);
    }

    // ---- utilitaires -----------------------------------------------------------------------------------------------------
    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        Image img = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) { t.font = font; t.fontSharedMaterial = font.material; }
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        return t;
    }

    // Place un élément par son coin haut-gauche (x vers la droite, y vers le bas en négatif) dans son parent.
    private static void Place(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
    }

    // Livre fermé vu de face, dessiné en code : couverture, dos à gauche, tranche des pages à droite, losange au centre.
    private static Sprite _book;
    private static Sprite BookSprite
    {
        get
        {
            if (_book != null) return _book;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                    float a = 0f, shade = 1f;
                    bool cover = u > 0.20f && u < 0.78f && v > 0.12f && v < 0.88f;
                    bool spine = u > 0.15f && u <= 0.24f && v > 0.10f && v < 0.90f;
                    bool pages = u >= 0.78f && u < 0.84f && v > 0.16f && v < 0.84f;
                    if (cover) { a = 1f; shade = 0.95f; }
                    if (spine) { a = 1f; shade = 0.75f; }
                    if (pages) { a = 1f; shade = (Mathf.Repeat(v * 40f, 1f) < 0.5f) ? 0.85f : 0.6f; }
                    // losange gravé au centre de la couverture
                    float du = Mathf.Abs(u - 0.49f) / 0.12f, dv = Mathf.Abs(v - 0.5f) / 0.16f;
                    if (cover && du + dv < 1f) shade = du + dv > 0.72f ? 0.45f : 0.95f;
                    // filets de la couverture
                    if (cover && (Mathf.Abs(v - 0.2f) < 0.012f || Mathf.Abs(v - 0.8f) < 0.012f)) shade = 0.5f;
                    px[y * n + x] = new Color(shade, shade, shade, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            _book = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return _book;
        }
    }
}
