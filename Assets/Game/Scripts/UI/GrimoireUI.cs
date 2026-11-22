using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// AJOUTÉ (2026-11-15, chantier Grimoire - emplacement choisi par l'utilisateur : « icône de livre en bas à gauche de
// l'Accueil », en face du bouton œil, pas de nouvelle page) - posé sous MenuPanel (page Accueil), construit par le code comme
// LastRunPanel. Un bouton rond (livre) ouvre une fenêtre par-dessus le menu :
//  - en-tête : titre, nombre de découvertes, éclats déjà récupérés ;
//  - trois rangées : Cartes (11), Fusions (6), Boss (3). Une entrée non découverte est une silhouette sombre avec « ??? » et
//    sa prime ; découverte, elle montre son icône (portrait pour les boss) et son nom.
// Fermeture : croix, clic hors de la fenêtre, Échap / B. La règle des découvertes et les primes sont dans Grimoire.cs.
//
// REFONTE (2026-11-22, demandes de l'utilisateur) :
//  - la prime ne tombe plus toute seule : un bouton « Récupérer » apparaît sous chaque découverte (plus satisfaisant), et une
//    pastille sur le bouton livre compte les primes qui attendent ;
//  - fenêtre agrandie et aérée (1600 x 960) ; le texte d'aide du bas est retiré ;
//  - au survol d'une entrée, une fiche s'ouvre : icône à gauche, nom et description à droite, récompense en bas ;
//  - croix de fermeture animée comme celle de la fiche d'un nœud de l'arbre (CloseButtonHover : disque rouge + grossissement) ;
//  - couleurs « nuit arcane » (choix de l'utilisateur, pour sortir du duo marron/doré omniprésent) : fond bleu nuit violacé,
//    rubriques lavande, primes et boutons en bleu éclat ; l'or ne reste que pour le titre et le cadre en laiton.
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
    private static readonly Vector2 WindowSize = new Vector2(1600f, 960f);
    private static readonly string[] BossTimes = { "5:00", "10:00", "15:00" };

    // palette « nuit arcane »
    private static readonly Color Night = new Color(0.068f, 0.062f, 0.118f, 1f);
    private static readonly Color NightRaised = new Color(0.125f, 0.112f, 0.215f, 1f);
    private static readonly Color Gold = new Color(1f, 0.82f, 0.36f, 1f);
    private static readonly Color Lavender = new Color(0.76f, 0.72f, 0.98f, 1f);
    private static readonly Color TextColor = new Color(0.93f, 0.91f, 0.98f, 1f);
    private static readonly Color Muted = new Color(0.60f, 0.58f, 0.74f, 0.9f);
    private static readonly Color EclatText = new Color(0.62f, 0.86f, 1f, 1f);
    private static readonly Color ClaimBlue = new Color(0.20f, 0.50f, 0.95f, 1f);
    private static readonly Color Locked = new Color(0.03f, 0.03f, 0.06f, 0.92f);

    public static bool IsOpen { get; private set; }

    private Canvas _rootCanvas;
    private GameObject _window;
    private CanvasGroup _windowGroup;
    private RectTransform _panel;
    private TextMeshProUGUI _progress;
    private readonly List<Tile> _tiles = new List<Tile>();
    private Coroutine _anim;

    // pastille du bouton livre (primes à récupérer)
    private RectTransform _badge;
    private TextMeshProUGUI _badgeText;
    private float _badgeCheck;
    private int _badgeCount = -1;

    // fiche d'une entrée (ouverte au clic)
    private RectTransform _detail;
    private CanvasGroup _detailGroup;
    private Image _detailIcon, _detailFrame;
    private TextMeshProUGUI _detailName, _detailText, _detailReward, _detailStatus;
    private Tile _shown;
    private float _detailAlpha, _detailTarget;

    private class Tile
    {
        public string id, name, description, hint;
        public int eclats;
        public Sprite sprite;
        public RectTransform cell;
        public Image icon, frame, ring;
        public TextMeshProUGUI label, prime;
        public GameObject claim;
        public bool discovered, claimed;
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
        _badgeCount = -1;
        if (_window == null) return;
        Destroy(_window);
        _window = null;
        _detail = null;
        _tiles.Clear();
        if (IsOpen) { IsOpen = false; Open(); }
    }

    private void Update()
    {
        UpdateBadge();
        if (!IsOpen) return;
        if (GameInput.BackDown())
        {
            if (DetailOpen) HideDetail(); else { Close(instant: false); return; }
        }
        TickDetail();
        PulseClaimable();
    }

    // ---- bouton d'accès ---------------------------------------------------------------------------------------------------
    // CHANGÉ (2026-11-22, demande utilisateur : « le petit bouton en bas à gauche n'est pas très visible ») - grand bouton
    // illustré en haut à droite de l'Accueil (en face de la carte Dernière partie) : l'image d'un grimoire ouvert
    // (Assets/Resources/Grimoire/GrimoireBook.png, générée par l'utilisateur ; à défaut, le livre dessiné en code sur un
    // disque), un halo violet qui respire derrière, le mot « Grimoire » dessous et la pastille des primes à récupérer.
    // Le grossissement au survol vient de ButtonHoverFX, comme pour tous les boutons du jeu.
    // (2026-11-22, retour utilisateur « un peu plus petit » : 250 x 204 -> 210 x 172)
    private const float AccessW = 210f, AccessArt = 172f;   // AccessArt = hauteur de l'image (livre 1024 x 837)
    private Image _accessGlow;

    private void BuildButton()
    {
        var go = new GameObject("GrimoireButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-24f, -14f);
        rt.sizeDelta = new Vector2(AccessW, AccessArt + 44f);
        var hit = go.GetComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);   // zone cliquable invisible (image + mot)

        const float glow = AccessW * 1.3f;
        _accessGlow = NewImage("Glow", rt, TreeSprites.Glow, new Color(0.55f, 0.40f, 1f, 0.45f));
        Place(_accessGlow.rectTransform, (AccessW - glow) * 0.5f, -AccessArt * 0.5f + glow * 0.5f, glow, glow);

        Sprite art = LoadBookArt();
        Image book;
        if (art != null)
        {
            // l'image est recadrée au livre (plus large que haute) : elle remplit toute la largeur du bouton
            book = NewImage("Book", rt, art, Color.white);
            Place(book.rectTransform, 0f, 0f, AccessW, AccessArt);
        }
        else
        {
            // sans illustration : le livre dessiné en code, posé sur un disque sombre cerclé d'or
            const float d = 128f;
            Image disc = NewImage("Disc", rt, HudSprites.Disc, new Color(0.07f, 0.06f, 0.12f, 0.85f));
            Place(disc.rectTransform, (AccessW - d) * 0.5f, -(AccessArt - d) * 0.5f, d, d);
            Image ring = NewImage("Ring", disc.rectTransform, HudSprites.Ring, new Color(0.86f, 0.72f, 0.44f, 0.85f));
            Stretch(ring.rectTransform, 0f);
            book = NewImage("Book", disc.rectTransform, BookSprite, new Color(0.95f, 0.88f, 0.72f, 1f));
            Stretch(book.rectTransform, 26f);
        }
        book.preserveAspect = true;

        TextMeshProUGUI label = NewText("Label", rt, TitleFont, 32f, Gold, TextAlignmentOptions.Center);
        Place(label.rectTransform, -20f, -AccessArt + 4f, AccessW + 40f, 42f);
        label.text = Loc.T("Grimoire");
        label.outlineWidth = 0.26f;   // contour épais : le mot reste lisible sur les nuages clairs
        label.outlineColor = new Color32(24, 14, 40, 255);
        label.enableAutoSizing = true; label.fontSizeMax = 32f; label.fontSizeMin = 20f;

        var btn = go.GetComponent<Button>();
        var cb = btn.colors;
        // appui : même teinte que le survol et pas d'enfoncement (retour utilisateur : le « rebond » au clic déstabilisait)
        cb.normalColor = Color.white; cb.highlightedColor = new Color(1.15f, 1.1f, 1.2f, 1f); cb.pressedColor = cb.highlightedColor;
        cb.selectedColor = Color.white; cb.colorMultiplier = 1.15f; cb.fadeDuration = 0.1f;
        btn.colors = cb;
        btn.targetGraphic = book;
        btn.onClick.AddListener(Open);
        go.AddComponent<ButtonHoverFX>().PressDip = false;   // grossit au survol comme les autres, sans s'enfoncer au clic

        // pastille : nombre de primes à récupérer, en haut à droite de l'image
        Image badge = NewImage("Badge", rt, HudSprites.Disc, ClaimBlue);
        _badge = badge.rectTransform;
        _badge.anchorMin = _badge.anchorMax = new Vector2(0.5f, 1f);
        _badge.pivot = new Vector2(0.5f, 0.5f);
        _badge.anchoredPosition = new Vector2(AccessW * 0.40f, -AccessArt * 0.14f);
        _badge.sizeDelta = new Vector2(34f, 34f);
        Image badgeRing = NewImage("Ring", _badge, HudSprites.Ring, new Color(1f, 1f, 1f, 0.9f));
        Stretch(badgeRing.rectTransform, 0f);
        _badgeText = NewText("Count", _badge, GameFonts.Body, 19f, Color.white, TextAlignmentOptions.Center);
        Stretch(_badgeText.rectTransform, 0f);
        _badgeText.fontStyle = FontStyles.Bold;
        _badge.gameObject.SetActive(false);
    }

    // Illustration du bouton : un Sprite si l'image est importée comme Sprite, sinon la texture brute.
    private static Sprite LoadBookArt()
    {
        Sprite s = Resources.Load<Sprite>("Grimoire/GrimoireBook");
        if (s != null) return s;
        Texture2D tex = Resources.Load<Texture2D>("Grimoire/GrimoireBook");
        return tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f) : null;
    }

    private void UpdateBadge()
    {
        if (_badge == null) return;
        _badgeCheck -= Time.unscaledDeltaTime;
        if (_badgeCheck <= 0f)
        {
            _badgeCheck = 0.5f;
            MetaProgressionManager meta = MetaProgressionManager.Instance;
            int n = meta != null ? meta.UnclaimedDiscoveries() : 0;
            if (n != _badgeCount)
            {
                _badgeCount = n;
                _badge.gameObject.SetActive(n > 0);
                _badgeText.text = n > 9 ? "9+" : n.ToString();
            }
        }
        if (_badgeCount > 0)
        {
            float s = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 3.2f);   // respiration discrète : quelque chose attend
            _badge.localScale = new Vector3(s, s, 1f);
        }
        if (_accessGlow != null)
        {
            // halo lent derrière le livre ; plus vif quand une prime attend
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 1.6f);
            float a = (_badgeCount > 0 ? 0.42f : 0.26f) + 0.14f * k;
            _accessGlow.color = new Color(0.55f, 0.40f, 1f, a * GameSettings.MenuEffectsFactor);
        }
    }

    // ---- fenêtre ---------------------------------------------------------------------------------------------------------
    public void Open()
    {
        if (IsOpen) return;
        if (_window == null) BuildWindow();
        Refresh();
        HideDetailNow();
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

    private TMP_FontAsset TitleFont => _titleFont != null ? _titleFont : GameFonts.Body;

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
        Image dim = NewImage("Dim", wrt, null, new Color(0f, 0f, 0f, 0.66f));
        Stretch(dim.rectTransform, 0f);
        dim.raycastTarget = true;
        var dimBtn = dim.gameObject.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        dimBtn.onClick.AddListener(() => Close(instant: false));
        ButtonHoverFX.Exclude(dimBtn);

        Image panel = NewImage("Panel", wrt, null, Night);
        panel.raycastTarget = true;   // un clic dans la fenêtre ne la ferme pas...
        var panelClick = panel.gameObject.AddComponent<EventTrigger>();   // ...mais ferme la fiche ouverte (clic hors des entrées)
        var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        click.callback.AddListener(_ => HideDetail());
        panelClick.triggers.Add(click);
        _panel = panel.rectTransform;
        _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(0.5f, 0.5f);
        _panel.sizeDelta = WindowSize;

        // profondeur : halo violet très doux derrière le titre, et un autre, bleuté, en bas
        Image glowTop = NewImage("GlowTop", _panel, TreeSprites.Glow, new Color(0.45f, 0.32f, 0.85f, 0.22f));
        Place(glowTop.rectTransform, WindowSize.x * 0.5f - 600f, -6f, 1200f, 300f);   // à l'intérieur de la fenêtre (pas de masque)
        Image glowBottom = NewImage("GlowBottom", _panel, TreeSprites.Glow, new Color(0.25f, 0.45f, 0.95f, 0.10f));
        Place(glowBottom.rectTransform, WindowSize.x * 0.5f - 700f, -WindowSize.y + 386f, 1400f, 380f);
        _panel.gameObject.AddComponent<BrassFrameBorder>();   // même cadre laiton que le menu Pause (l'or reste sur le cadre)

        float w = WindowSize.x, y = -30f;
        TextMeshProUGUI title = NewText("Title", _panel, TitleFont, 62f, Gold, TextAlignmentOptions.Center);
        Place(title.rectTransform, 0f, y, w, 70f);
        title.text = Loc.T("Grimoire");
        y -= 66f;
        _progress = NewText("Progress", _panel, GameFonts.Body, 21f, Lavender, TextAlignmentOptions.Center);
        Place(_progress.rectTransform, 0f, y, w, 30f);
        y -= 52f;

        BuildCloseButton();

        // trois rangées
        var cards = new List<UpgradeData>();
        var fusions = new List<UpgradeData>();
        if (_upgrades != null)
            foreach (UpgradeData u in _upgrades)
            {
                if (u == null) continue;
                if (u.upgradeType == UpgradeType.Fusion) fusions.Add(u); else cards.Add(u);
            }

        const float pad = 56f;
        y = Section(Loc.T("Cartes"), y, pad);
        y = Row(cards.Count, 122f, 14f, 96f, y, i => UpgradeTile(cards[i])) - 16f;
        y = Section(Loc.T("Fusions"), y, pad);
        y = Row(fusions.Count, 186f, 34f, 108f, y, i => UpgradeTile(fusions[i])) - 16f;
        y = Section(Loc.T("Boss"), y, pad);
        Row(3, 250f, 70f, 132f, y, BossTile);

        BuildDetail();
        _window.SetActive(false);
    }

    // Croix de fermeture : même bouton que la fiche d'un nœud de l'arbre (NodeDetailPanel/CloseButton : disque violet sombre,
    // X Bangers lavande), avec la même animation (CloseButtonHover : le disque passe au rouge et le bouton grossit).
    private void BuildCloseButton() => MakeCloseButton(_panel, 46f, new Vector2(-48f, -48f), () => Close(instant: false));

    private void MakeCloseButton(RectTransform parent, float size, Vector2 offsetFromTopRight, UnityEngine.Events.UnityAction action)
    {
        var close = new GameObject("Close", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var crt = (RectTransform)close.transform;
        crt.SetParent(parent, false);
        crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = offsetFromTopRight;
        crt.sizeDelta = new Vector2(size, size);
        Image cback = close.GetComponent<Image>();
        cback.sprite = HudSprites.Disc;
        cback.color = new Color(0.082f, 0.067f, 0.122f, 0.95f);
        Image cring = NewImage("Ring", crt, HudSprites.Ring, new Color(0.61f, 0.58f, 0.68f, 0.35f));
        Stretch(cring.rectTransform, 0f);
        TextMeshProUGUI cx = NewText("X", crt, TitleFont, size * 0.65f, new Color(0.612f, 0.576f, 0.682f, 1f), TextAlignmentOptions.Center);
        Stretch(cx.rectTransform, 0f);
        cx.text = "X";
        var btn = close.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;   // la couleur est animée par CloseButtonHover
        btn.onClick.AddListener(action);
        close.AddComponent<CloseButtonHover>();       // lit la couleur de repos du disque et de la croix à son Awake
    }

    // AJOUTÉ (2026-11-22, retour utilisateur : « mets de la ponctuation dans les descriptions, des points en fin de phrase ») -
    // les textes des cartes s'écrivent sans point final ailleurs dans le jeu (typographie des cartes) ; dans la fiche du Grimoire,
    // ce sont des phrases : on ajoute le point s'il manque (point idéographique en chinois et en japonais).
    private static string Sentence(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = s.TrimEnd();
        char last = s[s.Length - 1];
        if (".!?…。！？".IndexOf(last) >= 0) return s;
        bool cjk = Loc.Current == GameLanguage.ChineseSimplified || Loc.Current == GameLanguage.Japanese;
        return s + (cjk ? "。" : ".");
    }

    private Tile UpgradeTile(UpgradeData u)
    {
        bool fusion = u.upgradeType == UpgradeType.Fusion;
        return new Tile
        {
            id = Grimoire.UpgradeId(u),
            name = u.Name,
            sprite = u.icon,
            eclats = Grimoire.UpgradeEclats(u),
            description = Sentence(fusion ? u.FusionSourcesLabel() + " : " + u.Description : u.Description),
            hint = Sentence(fusion ? Loc.T("Obtiens cette fusion une première fois pour la découvrir")
                                   : Loc.T("Choisis cette carte une première fois pour la découvrir")),
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
            description = Sentence(Loc.F("Boss n°{0}", i + 1)) + "\n" + Sentence(Loc.F("Apparaît à {0} en mode Classique", BossTimes[Mathf.Clamp(i, 0, 2)])),
            hint = Sentence(Loc.T("Bats ce boss une première fois pour le découvrir")),
        };
    }

    private float Section(string label, float y, float pad)
    {
        TextMeshProUGUI t = NewText("Section", _panel, TitleFont, 30f, Lavender, TextAlignmentOptions.MidlineLeft);
        Place(t.rectTransform, pad, y, 600f, 36f);
        t.text = label;
        Image rule = NewImage("Rule", _panel, OrnamentSprites.Rule(fadeEnds: true), new Color(0.58f, 0.52f, 0.92f, 0.42f));
        Place(rule.rectTransform, pad, y - 38f, WindowSize.x - pad * 2f, 6f);
        return y - 58f;
    }

    // Rangée centrée de n vignettes : icône (iconSize), nom, puis la ligne du bouton « Récupérer ».
    private const float LabelH = 26f, ClaimH = 34f;
    // marge de l'icône dans son cadre rond, par côté (2026-11-22, retour utilisateur : certaines icônes dépassaient du cadre ;
    // 0,1 -> 0,17 : l'icône occupe 66 % du cadre au lieu de 80 %)
    private const float IconInset = 0.17f;

    private float Row(int n, float tileWidth, float gap, float iconSize, float y, System.Func<int, Tile> make)
    {
        float total = n * tileWidth + (n - 1) * gap;
        float x0 = (WindowSize.x - total) * 0.5f;
        float s = Mathf.Min(iconSize, tileWidth);
        float cellH = s + 8f + LabelH + 8f + ClaimH;
        for (int i = 0; i < n; i++)
        {
            Tile tile = make(i);
            float x = x0 + i * (tileWidth + gap);
            var cell = new GameObject("Entry", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
            cell.SetParent(_panel, false);
            Place(cell, x, y, tileWidth, s + 8f + LabelH);   // zone cliquable : icône + nom (le bouton Récupérer a la sienne)
            tile.cell = cell;
            Image cellBack = cell.GetComponent<Image>();
            cellBack.color = new Color(0f, 0f, 0f, 0f);
            // CHANGÉ (2026-11-22, retour utilisateur) - la fiche s'ouvre au CLIC (plus au survol) : chaque entrée est un bouton ;
            // le léger grossissement au survol (x1,06) vient de ButtonHoverFX et laisse de l'air avec les voisines.
            var entryBtn = cell.gameObject.AddComponent<Button>();
            entryBtn.transition = Selectable.Transition.None;
            entryBtn.targetGraphic = cellBack;
            entryBtn.onClick.AddListener(() => ToggleDetail(tile));

            tile.frame = NewImage("Frame", cell, HudSprites.Disc, NightRaised);
            Place(tile.frame.rectTransform, (tileWidth - s) * 0.5f, 0f, s, s);
            tile.ring = NewImage("Ring", tile.frame.rectTransform, HudSprites.RingFlat, new Color(0.58f, 0.52f, 0.92f, 0.45f));
            Stretch(tile.ring.rectTransform, -2f);
            tile.icon = NewImage("Icon", tile.frame.rectTransform, tile.sprite, Color.white);
            Stretch(tile.icon.rectTransform, s * IconInset);
            tile.icon.preserveAspect = true;

            tile.prime = NewText("Prime", tile.frame.rectTransform, GameFonts.Body, Mathf.Clamp(s * 0.2f, 18f, 30f), EclatText, TextAlignmentOptions.Center);
            Stretch(tile.prime.rectTransform, 0f);
            tile.prime.fontStyle = FontStyles.Bold;

            // centré : un nom réduit par la taille auto reste au milieu de sa ligne, comme les autres (retour 2026-11-15)
            tile.label = NewText("Name", cell, GameFonts.Body, 17f, TextColor, TextAlignmentOptions.Center);
            Place(tile.label.rectTransform, -8f, -s - 8f, tileWidth + 16f, LabelH);
            tile.label.enableAutoSizing = true; tile.label.fontSizeMax = 17f; tile.label.fontSizeMin = 11f;

            tile.claim = BuildClaimButton(tile, x + tileWidth * 0.5f, y - s - 8f - LabelH - 8f, Mathf.Min(tileWidth - 6f, 150f));
            _tiles.Add(tile);
        }
        return y - cellH;
    }

    private GameObject BuildClaimButton(Tile tile, float centerX, float top, float width)
    {
        var go = new GameObject("Claim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform;
        rt.SetParent(_panel, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(centerX, top);
        rt.sizeDelta = new Vector2(width, ClaimH);
        Image back = go.GetComponent<Image>();
        back.sprite = HudSprites.Pill;
        back.type = Image.Type.Sliced;
        back.color = ClaimBlue;
        TextMeshProUGUI label = NewText("Label", rt, TitleFont, 21f, Color.white, TextAlignmentOptions.Center);
        Stretch(label.rectTransform, 0f);
        label.margin = new Vector4(8f, 0f, 8f, 0f);
        label.enableAutoSizing = true; label.fontSizeMax = 21f; label.fontSizeMin = 13f;
        label.text = Loc.T("Récupérer");
        var btn = go.GetComponent<Button>();
        var cb = btn.colors;
        cb.normalColor = Color.white; cb.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f); cb.pressedColor = new Color(0.82f, 0.82f, 0.9f, 1f);
        cb.selectedColor = Color.white; cb.colorMultiplier = 1.25f; cb.fadeDuration = 0.08f;
        btn.colors = cb;
        btn.onClick.AddListener(() => Claim(tile));
        return go;
    }

    // ---- récupération d'une prime -----------------------------------------------------------------------------------------
    private void Claim(Tile tile)
    {
        MetaProgressionManager meta = MetaProgressionManager.Instance;
        if (meta == null || !meta.ClaimDiscovery(tile.id, tile.eclats)) return;
        _badgeCheck = 0f;
        Refresh();
        if (_shown == tile) FillDetail(tile);
        StartCoroutine(ClaimFX(tile));
    }

    // « +50 » et l'icône d'éclat montent depuis l'entrée en s'effaçant ; l'icône de l'entrée fait un petit bond.
    private IEnumerator ClaimFX(Tile tile)
    {
        var go = new GameObject("ClaimFX", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(_panel, false);
        RectTransform frame = tile.frame.rectTransform;
        Vector2 start = (Vector2)_panel.InverseTransformPoint(frame.TransformPoint(frame.rect.center));
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(220f, 48f);
        var group = go.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        TextMeshProUGUI amount = NewText("Amount", rt, TitleFont, 40f, EclatText, TextAlignmentOptions.Center);
        Stretch(amount.rectTransform, 0f);
        amount.outlineWidth = 0.2f;
        amount.outlineColor = new Color32(10, 14, 40, 255);
        amount.text = "+" + tile.eclats;
        if (_eclatIcon != null)
        {
            Image ic = NewImage("Eclat", rt, _eclatIcon, Color.white);
            ic.preserveAspect = true;
            ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            ic.rectTransform.sizeDelta = new Vector2(34f, 34f);
            ic.rectTransform.anchoredPosition = new Vector2(amount.GetPreferredValues(amount.text).x * 0.5f + 22f, 2f);
        }

        const float duration = 1.1f;
        float t = 0f;
        while (t < duration && go != null && _panel != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            rt.anchoredPosition = start + new Vector2(0f, 20f + 70f * (1f - (1f - k) * (1f - k)));
            group.alpha = k < 0.15f ? k / 0.15f : 1f - Mathf.SmoothStep(0f, 1f, (k - 0.5f) / 0.5f);
            float pop = k < 0.25f ? Mathf.Sin(k / 0.25f * Mathf.PI) * 0.18f : 0f;   // bond de l'icône
            frame.localScale = new Vector3(1f + pop, 1f + pop, 1f);
            yield return null;
        }
        if (frame != null) frame.localScale = Vector3.one;
        if (go != null) Destroy(go);
    }

    // Les entrées dont la prime attend : anneau bleu qui respire.
    private void PulseClaimable()
    {
        float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
        Color c = Color.Lerp(new Color(0.40f, 0.68f, 1f, 0.55f), new Color(0.62f, 0.86f, 1f, 1f), k);
        foreach (Tile t in _tiles)
            if (t.discovered && !t.claimed) t.ring.color = c;
    }

    // ---- fiche de survol --------------------------------------------------------------------------------------------------
    private const float DetailW = 620f, DetailH = 250f, DetailIcon = 150f;

    private void BuildDetail()
    {
        var go = new GameObject("Detail", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        _detail = (RectTransform)go.transform;
        _detail.SetParent(_panel, false);
        Place(_detail, 0f, 0f, DetailW, DetailH);
        Image back = go.GetComponent<Image>();
        back.color = new Color(0.10f, 0.09f, 0.17f, 1f);
        back.raycastTarget = true;   // fiche ouverte : un clic dessus ne passe pas aux entrées de dessous
        _detailGroup = go.GetComponent<CanvasGroup>();
        go.AddComponent<BrassFrameBorder>();
        MakeCloseButton(_detail, 34f, new Vector2(-26f, -26f), HideDetail);

        const float p = 22f;
        _detailFrame = NewImage("Frame", _detail, HudSprites.Disc, NightRaised);
        Place(_detailFrame.rectTransform, p, -p, DetailIcon, DetailIcon);
        Image ring = NewImage("Ring", _detailFrame.rectTransform, HudSprites.RingFlat, new Color(0.58f, 0.52f, 0.92f, 0.5f));
        Stretch(ring.rectTransform, -2f);
        _detailIcon = NewImage("Icon", _detailFrame.rectTransform, null, Color.white);
        Stretch(_detailIcon.rectTransform, DetailIcon * IconInset);
        _detailIcon.preserveAspect = true;

        float tx = p + DetailIcon + 22f, tw = DetailW - tx - p - 34f;   // 34 : place de la croix
        _detailName = NewText("Name", _detail, TitleFont, 32f, Gold, TextAlignmentOptions.TopLeft);
        Place(_detailName.rectTransform, tx, -p + 2f, tw, 40f);
        _detailName.enableAutoSizing = true; _detailName.fontSizeMax = 32f; _detailName.fontSizeMin = 20f;
        _detailText = NewText("Description", _detail, GameFonts.Body, 19f, TextColor, TextAlignmentOptions.TopLeft);
        Place(_detailText.rectTransform, tx, -p - 46f, tw, DetailIcon - 46f + 4f);
        _detailText.enableWordWrapping = true;
        _detailText.enableAutoSizing = true; _detailText.fontSizeMax = 19f; _detailText.fontSizeMin = 13f;

        // bas : récompense
        float by = -p - DetailIcon - 12f;
        Image rule = NewImage("Rule", _detail, OrnamentSprites.Rule(fadeEnds: true), new Color(0.58f, 0.52f, 0.92f, 0.4f));
        Place(rule.rectTransform, p, by, DetailW - p * 2f, 5f);
        by -= 12f;
        TextMeshProUGUI label = NewText("RewardLabel", _detail, GameFonts.Body, 18f, Muted, TextAlignmentOptions.MidlineLeft);
        Place(label.rectTransform, p, by, 200f, 40f);
        label.text = Loc.T("Récompense");
        float lx = p + label.GetPreferredValues(label.text).x + 14f;
        if (_eclatIcon != null)
        {
            Image ic = NewImage("Eclat", _detail, _eclatIcon, Color.white);
            ic.preserveAspect = true;
            Place(ic.rectTransform, lx + 11f, by - 4f, 32f, 32f);   // presque collée au « + » (retour utilisateur)
            lx += 38f;
        }
        _detailReward = NewText("Reward", _detail, TitleFont, 30f, EclatText, TextAlignmentOptions.MidlineLeft);
        Place(_detailReward.rectTransform, lx, by, 160f, 40f);
        _detailStatus = NewText("Status", _detail, GameFonts.Body, 18f, EclatText, TextAlignmentOptions.MidlineRight);
        Place(_detailStatus.rectTransform, DetailW - p - 300f, by, 300f, 40f);
        _detailStatus.fontStyle = FontStyles.Bold;

        HideDetailNow();
    }

    // Clic sur une entrée : ouvre sa fiche ; un 2e clic sur la même la referme ; un clic sur une autre entrée la remplace.
    // Fermeture aussi par la croix de la fiche, un clic ailleurs dans la fenêtre, ou Échap / B (qui ferme d'abord la fiche).
    private void ToggleDetail(Tile tile)
    {
        if (_shown == tile && _detailTarget > 0f) HideDetail();
        else ShowDetail(tile);
    }

    private void TickDetail()
    {
        if (_detail == null) return;
        _detailAlpha = Mathf.MoveTowards(_detailAlpha, _detailTarget, Time.unscaledDeltaTime / 0.12f);
        _detailGroup.alpha = _detailAlpha;
        float s = Mathf.Lerp(0.97f, 1f, _detailAlpha);
        _detail.localScale = new Vector3(s, s, 1f);
        if (_detailAlpha <= 0f && _detailTarget <= 0f && _detail.gameObject.activeSelf) _detail.gameObject.SetActive(false);
    }

    private bool DetailOpen => _detail != null && _detailTarget > 0f;

    private void HideDetail()
    {
        _shown = null;
        _detailTarget = 0f;
        if (_detailGroup != null) _detailGroup.blocksRaycasts = false;   // les entrées redeviennent cliquables tout de suite
    }

    private void HideDetailNow()
    {
        HideDetail();
        _detailAlpha = 0f;
        if (_detailGroup != null) _detailGroup.alpha = 0f;
        if (_detail != null) _detail.gameObject.SetActive(false);
    }

    private void ShowDetail(Tile tile)
    {
        if (_detail == null || tile == null) return;
        _shown = tile;
        FillDetail(tile);
        PositionDetail(tile);
        _detail.gameObject.SetActive(true);
        _detail.SetAsLastSibling();
        _detailGroup.blocksRaycasts = true;
        _detailTarget = 1f;
    }

    private void FillDetail(Tile tile)
    {
        bool found = tile.discovered;
        _detailIcon.sprite = tile.sprite;
        _detailIcon.enabled = tile.sprite != null;
        _detailIcon.color = found ? Color.white : Locked;
        _detailName.text = found ? tile.name : "???";
        _detailName.color = found ? Gold : Muted;
        _detailText.text = found ? tile.description : tile.hint;
        _detailText.color = found ? TextColor : Muted;
        _detailReward.text = "+" + tile.eclats;
        _detailReward.color = tile.claimed ? Muted : EclatText;
        if (!found) { _detailStatus.text = Loc.T("À découvrir"); _detailStatus.color = Muted; }
        else if (!tile.claimed) { _detailStatus.text = Loc.T("À récupérer"); _detailStatus.color = EclatText; }
        else { _detailStatus.text = Loc.T("Récupérée"); _detailStatus.color = Muted; }
    }

    // Au-dessus de l'entrée s'il y a la place, sinon en dessous ; toujours à l'intérieur de la fenêtre.
    private void PositionDetail(Tile tile)
    {
        const float edge = 18f, gap = 14f;
        Vector2 cellPos = tile.cell.anchoredPosition;   // coin haut-gauche dans le panneau (y négatif vers le bas)
        float cellW = tile.cell.sizeDelta.x;
        float cellBottom = cellPos.y - tile.cell.sizeDelta.y - ClaimH - 8f;
        float x = Mathf.Clamp(cellPos.x + cellW * 0.5f - DetailW * 0.5f, edge, WindowSize.x - DetailW - edge);
        float above = cellPos.y + gap + DetailH;   // haut de la fiche si elle est posée au-dessus
        float y = above <= -edge ? above : cellBottom - gap;
        y = Mathf.Clamp(y, -WindowSize.y + DetailH + edge, -edge);
        _detail.anchoredPosition = new Vector2(x, y);
    }

    private static SaveData Data => MetaProgressionManager.Instance != null ? MetaProgressionManager.Instance.Data : null;

    private void Refresh()
    {
        SaveData data = Data;
        MetaProgressionManager meta = MetaProgressionManager.Instance;
        int found = 0, eclats = 0;
        foreach (Tile t in _tiles)
        {
            t.discovered = Grimoire.IsDiscovered(data, t.id);
            t.claimed = t.discovered && meta != null && meta.IsDiscoveryClaimed(t.id);
            if (t.discovered) found++;
            if (t.claimed) eclats += t.eclats;
            t.icon.enabled = t.sprite != null;
            t.icon.color = t.discovered ? Color.white : Locked;   // silhouette sombre tant qu'elle n'est pas découverte
            t.label.text = t.discovered ? t.name : "???";
            t.label.color = t.discovered ? TextColor : Muted;
            t.prime.text = t.discovered ? "" : "+" + t.eclats;
            t.frame.color = t.discovered ? NightRaised : new Color(0.085f, 0.078f, 0.15f, 1f);
            t.ring.color = t.discovered ? new Color(0.58f, 0.52f, 0.92f, 0.45f) : new Color(0.58f, 0.52f, 0.92f, 0.18f);
            t.claim.SetActive(t.discovered && !t.claimed);
        }
        _progress.text = Loc.F("{0} / {1} découvertes", found, _tiles.Count) + "   ·   "
                         + Loc.F("{0} éclats gagnés", eclats);
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
        // CORRIGÉ (2026-11-18) - « Ellipsis » supprimait TOUT le texte en japonais / chinois / coréen : la ligne de ces polices est
        // plus haute que le cadre (titre 58 px dans 66, rubrique 28 dans 34), TMP jugeait qu'aucune ligne ne tenait et n'écrivait
        // rien (titre, compteur et rubriques du Grimoire absents). Les cadres sont assez larges : le texte déborde seulement en hauteur.
        t.overflowMode = TextOverflowModes.Overflow;
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
