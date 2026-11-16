using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// AJOUTÉ (2026-11-16, didacticiel) - une étape de didacticiel : un titre, un texte, et (facultatif) un élément de l'interface
// mis en lumière (le reste de l'écran s'assombrit). Les textes sont des fonctions : ils sont recalculés à chaque affichage
// (langue, touches choisies, manette).
public class TutorialStep
{
    public Func<string> Title;
    public Func<string> Body;
    public Func<RectTransform> Target;            // null = pas de projecteur (carte au centre)
    public float Padding = 16f;
    public enum Place { Auto, Center, Top, Bottom, BottomLeft, BottomRight }
    public Place Position = Place.Auto;
    public bool RequireAction;                     // pas de bouton « Suivant » : l'étape finit quand DoneWhen devient vrai
    public Func<bool> DoneWhen;
    public bool PassClicks;                        // les clics dans le trou du projecteur atteignent l'interface dessous
    public bool Dim = true;                        // assombrir le reste de l'écran
    public float DimAlpha = 0.80f;                 // opacité du voile (partie d'entraînement : plus léger pour voir l'action)
    public bool BlockInput = true;                 // l'écran hors carte ne reçoit pas de clics
    public Func<string> NextLabel;                 // défaut : « Suivant »
    public bool ShowSkip = true;
    public bool LanguagePicker;
    public Func<GameObject> Panel;                 // page du menu qui doit rester affichée pendant l'étape (le joueur ne peut pas en sortir)
    public Func<string> SkipLabel;                 // texte du bouton « passer » (défaut : Passer le didacticiel)
    public bool SkipConfirm = true;                // 2e pression pour confirmer (faux = passe tout de suite)
    public bool Tight;                             // projecteur calé sur le contenu visible de la cible (et non sur son rectangle)
    public float Width;                            // largeur de la carte (0 = défaut 860)
    public float TextScale = 1f;                   // taille du texte (conseils de partie : plus petits)
    public Action OnEnter;
    public Action OnExit;
}

// AJOUTÉ (2026-11-16, didacticiel) - fenêtre du didacticiel, construite entièrement en code (aucun objet de scène), au-dessus
// de toute autre interface, qui survit aux changements de scène. Fonctionne à l'arrêt (Time.timeScale = 0) : tout est animé
// en temps réel. Style : carte brun sombre, liseré laiton aux losanges (BrassFrameBorder, comme le menu Pause), titre en
// Bangers doré, texte en Fira. Projecteur : quatre bandes sombres autour de l'élément visé + un cadre laiton qui pulse.
public class TutorialOverlay : MonoBehaviour
{
    private static TutorialOverlay _instance;
    public static bool IsShowing => _instance != null && _instance._step != null;
    public static int ClosedFrame { get; private set; } = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { _instance = null; ClosedFrame = -1; }

    public static TutorialOverlay Get()
    {
        if (_instance != null) return _instance;
        GameObject go = new GameObject("TutorialOverlay");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<TutorialOverlay>();
        _instance.Build();
        return _instance;
    }

    private static readonly Color DimColor = new Color(0.02f, 0.015f, 0.03f, 0.80f);
    private static readonly Color CardColor = new Color(0.10f, 0.07f, 0.05f, 1f);
    private static readonly Color TitleColor = new Color(1f, 0.80f, 0.36f, 1f);
    private static readonly Color BodyColor = new Color(0.94f, 0.91f, 0.83f, 1f);
    private static readonly Color BrassColor = new Color(0.86f, 0.58f, 0.16f, 1f);
    private static readonly Color BrassDark = new Color(0.30f, 0.18f, 0.07f, 1f);
    private const float CardWidth = 1080f;
    private const float TopMargin = 125f;

    private Canvas _canvas;
    private RectTransform _root;
    private Image _dimTop, _dimBottom, _dimLeft, _dimRight, _holeBlocker;
    private RectTransform _hole;
    private CanvasGroup _holeGroup;
    private RectTransform _card;
    private CanvasGroup _cardGroup;
    private TextMeshProUGUI _title, _body, _counter, _nextLabel, _skipLabel;
    private RectTransform _footer;
    private Button _next, _skip;
    private RectTransform _languageRow;
    private Button[] _languageButtons;
    private TextMeshProUGUI[] _languageLabels;

    private TutorialStep _step;
    private int _index, _total;
    private Action _onNext, _onSkip;
    private float _shownAt;
    private float _skipArmedUntil;
    private Vector2 _cardTarget;
    private bool _cardPlaced;

    private static Sprite _roundSprite;

    // ---- API ------------------------------------------------------------------------------------------------------------
    public void Present(TutorialStep step, int index, int total, Action onNext, Action onSkip)
    {
        _step = step;
        _index = index;
        _total = total;
        _onNext = onNext;
        _onSkip = onSkip;
        _shownAt = Time.unscaledTime;
        _skipArmedUntil = 0f;
        _cardPlaced = false;
        gameObject.SetActive(true);
        _root.gameObject.SetActive(true);
        step.OnEnter?.Invoke();
        Refresh();
        _cardGroup.alpha = 0f;
        LateUpdate();
        EnsureEventSystemSelection();
    }

    public void Dismiss()
    {
        if (_step == null) return;
        TutorialStep s = _step;
        _step = null;
        s.OnExit?.Invoke();
        _root.gameObject.SetActive(false);
        ClosedFrame = Time.frameCount;
    }

    // ---- construction -----------------------------------------------------------------------------------------------
    private void Build()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 6000;   // au-dessus de tout, fenêtre Quitter comprise
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        _root = NewRect("Root", transform);
        Stretch(_root);

        _dimTop = NewDim("DimTop");
        _dimBottom = NewDim("DimBottom");
        _dimLeft = NewDim("DimLeft");
        _dimRight = NewDim("DimRight");

        // bloque les clics dans le trou du projecteur quand l'étape ne les laisse pas passer
        _holeBlocker = NewRect("HoleBlocker", _root).gameObject.AddComponent<Image>();
        _holeBlocker.color = new Color(0f, 0f, 0f, 0f);
        _holeBlocker.raycastTarget = true;

        _hole = NewRect("Hole", _root);
        _holeGroup = _hole.gameObject.AddComponent<CanvasGroup>();
        _holeGroup.blocksRaycasts = false;
        _holeGroup.interactable = false;
        _hole.gameObject.AddComponent<BrassFrameBorder>();

        _card = NewRect("Card", _root);
        _card.sizeDelta = new Vector2(CardWidth, 300f);
        _cardGroup = _card.gameObject.AddComponent<CanvasGroup>();
        Image cardBg = _card.gameObject.AddComponent<Image>();
        cardBg.sprite = RoundSprite();
        cardBg.type = Image.Type.Sliced;
        cardBg.color = CardColor;
        _card.gameObject.AddComponent<BrassFrameBorder>();

        VerticalLayoutGroup v = _card.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(72, 72, 52, 44);
        v.spacing = 14f;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        ContentSizeFitter fit = _card.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        _title = NewText("Title", _card, 50f, TitleColor, false);
        _title.alignment = TextAlignmentOptions.Center;
        _title.enableWordWrapping = true;

        _body = NewText("Body", _card, 30f, BodyColor, true);
        _body.alignment = TextAlignmentOptions.Center;
        _body.enableWordWrapping = true;
        _body.lineSpacing = 6f;

        BuildLanguageRow();
        BuildFooter();
        _root.gameObject.SetActive(false);
    }

    private void BuildLanguageRow()
    {
        _languageRow = NewRect("Languages", _card);
        GridLayoutGroup grid = _languageRow.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(236f, 56f);
        grid.spacing = new Vector2(10f, 10f);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        LayoutElement le = _languageRow.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 3 * 56f + 2 * 10f;

        string[] names = SettingsSchema.LanguageNames;
        _languageButtons = new Button[names.Length];
        _languageLabels = new TextMeshProUGUI[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            int lang = i;
            Button b = NewButton("Lang" + i, _languageRow, names[i], 26f, out TextMeshProUGUI label);
            b.onClick.AddListener(() => ChooseLanguage(lang));
            _languageButtons[i] = b;
            _languageLabels[i] = label;
        }
        _languageRow.gameObject.SetActive(false);
    }

    private void BuildFooter()
    {
        RectTransform row = NewRect("Footer", _card);
        _footer = row;
        HorizontalLayoutGroup h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 18f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        LayoutElement rowLe = row.gameObject.AddComponent<LayoutElement>();
        rowLe.preferredHeight = 64f;

        _counter = NewText("Counter", row, 24f, Color.clear, true);   // (plus affiché : pas de numéro d'étape)
        _counter.gameObject.SetActive(false);

        _skip = NewButton("Skip", row, "", 24f, out _skipLabel);
        SetButtonStyle(_skip, new Color(0.20f, 0.14f, 0.10f, 1f), new Color(0.84f, 0.78f, 0.68f, 1f));
        LayoutElement sle = _skip.gameObject.AddComponent<LayoutElement>();
        sle.preferredWidth = 330f;
        sle.preferredHeight = 60f;
        _skip.onClick.AddListener(OnSkipClicked);

        _next = NewButton("Next", row, "", 30f, out _nextLabel);
        SetButtonStyle(_next, BrassColor, new Color(0.14f, 0.08f, 0.03f, 1f));
        LayoutElement nle = _next.gameObject.AddComponent<LayoutElement>();
        nle.preferredWidth = 300f;
        nle.preferredHeight = 60f;
        _next.onClick.AddListener(OnNextClicked);
    }

    // ---- éléments ---------------------------------------------------------------------------------------------------
    private static RectTransform NewRect(string name, Transform parent)
    {
        RectTransform rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private Image NewDim(string name)
    {
        RectTransform rt = NewRect(name, _root);
        rt.anchorMin = rt.anchorMax = Vector2.zero;   // repère coin bas gauche
        rt.pivot = Vector2.zero;
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = DimColor;
        img.raycastTarget = true;
        return img;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, float size, Color color, bool body)
    {
        TextMeshProUGUI t = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(parent, false);
        t.fontSize = size;
        t.color = color;
        t.raycastTarget = false;
        if (body) GameFonts.ToBody(t);
        return t;
    }

    private static Button NewButton(string name, Transform parent, string text, float size, out TextMeshProUGUI label)
    {
        RectTransform rt = NewRect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = RoundSprite();
        img.type = Image.Type.Sliced;
        img.color = new Color(0.20f, 0.14f, 0.10f, 1f);
        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        cb.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        cb.selectedColor = Color.white;
        b.colors = cb;
        rt.gameObject.AddComponent<ButtonHoverFX>();
        label = NewText("Label", rt, size, Color.white, false);
        Stretch(label.rectTransform);
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = size * 0.55f;
        label.fontSizeMax = size;
        label.text = text;
        return b;
    }

    private static void SetButtonStyle(Button b, Color bg, Color text)
    {
        ((Image)b.targetGraphic).color = bg;
        b.GetComponentInChildren<TextMeshProUGUI>().color = text;
    }

    // Police d'affiche (Bangers) : reprise d'un texte déjà présent dans la scène (les textes créés par le code n'ont pas la police
    // par défaut du jeu). À défaut, la police par défaut de TextMeshPro reste.
    private static TMP_FontAsset _titleFont;
    private static Material _titleMaterial;

    private static void ApplyTitleFont(TMP_Text t)
    {
        if (t == null) return;
        if (_titleFont == null)
        {
            foreach (TextMeshProUGUI x in FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (x != null && x.font != null && x.font.name.Contains("Bangers")) { _titleFont = x.font; _titleMaterial = x.fontSharedMaterial; break; }
            }
        }
        if (_titleFont == null) return;
        t.font = _titleFont;
        if (_titleMaterial != null) t.fontSharedMaterial = _titleMaterial;
    }

    // Rectangle arrondi 9-tranches (généré une fois).
    private static Sprite RoundSprite()
    {
        if (_roundSprite != null) return _roundSprite;
        const int n = 64;
        const float r = 18f;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, n - r), cy = Mathf.Clamp(y + 0.5f, r, n - r);
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                byte a = (byte)(Mathf.Clamp01(r - d + 0.5f) * 255f);
                px[y * n + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        _roundSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        return _roundSprite;
    }

    // ---- affichage ---------------------------------------------------------------------------------------------------
    private void Refresh()
    {
        TutorialStep s = _step;
        if (s == null) return;
        ApplyTitleFont(_title);
        ApplyTitleFont(_nextLabel);
        _title.text = s.Title != null ? s.Title() : "";
        _body.text = s.Body != null ? s.Body() : "";
        _body.gameObject.SetActive(!string.IsNullOrEmpty(_body.text));
        _card.sizeDelta = new Vector2(s.Width > 0f ? s.Width : CardWidth, _card.sizeDelta.y);
        _title.fontSize = 50f * s.TextScale;
        _body.fontSize = 30f * s.TextScale;
        
        _nextLabel.text = s.NextLabel != null ? s.NextLabel() : Loc.T("Suivant");
        _next.gameObject.SetActive(!s.RequireAction);
        _skip.gameObject.SetActive(s.ShowSkip && _onSkip != null);
        _skipLabel.text = s.SkipLabel != null ? s.SkipLabel() : (Time.unscaledTime < _skipArmedUntil ? Loc.T("Confirmer ?") : Loc.T("Passer le didacticiel"));
        _footer.gameObject.SetActive(!s.RequireAction || (s.ShowSkip && _onSkip != null));   // conseil de partie : pas de pied de carte
        _languageRow.gameObject.SetActive(s.LanguagePicker);
        if (s.LanguagePicker)
        {
            int cur = (int)Loc.Current;
            for (int i = 0; i < _languageButtons.Length; i++)
            {
                bool on = i == cur;
                ((Image)_languageButtons[i].targetGraphic).color = on ? BrassColor : new Color(0.20f, 0.14f, 0.10f, 1f);
                _languageLabels[i].color = on ? new Color(0.14f, 0.08f, 0.03f, 1f) : new Color(0.94f, 0.91f, 0.83f, 1f);
            }
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(_card);
    }

    private void ChooseLanguage(int lang)
    {
        GameSettings.SetInt(GameSettings.Language, lang);   // Loc.Changed est émis par GameSettings : les textes se rafraîchissent
        Refresh();
    }

    private void OnNextClicked()
    {
        if (_step == null || _step.RequireAction) return;
        Action cb = _onNext;
        cb?.Invoke();
    }

    private void OnSkipClicked()
    {
        if (_step == null) return;
        if (!_step.SkipConfirm || Time.unscaledTime < _skipArmedUntil)
        {
            Action cb = _onSkip;
            cb?.Invoke();
            return;
        }
        _skipArmedUntil = Time.unscaledTime + 3f;   // 2e clic dans les 3 s pour confirmer
        Refresh();
    }

    private void EnsureEventSystemSelection()
    {
        if (EventSystem.current != null && _step != null && !_step.RequireAction && _next != null && !GameInput.UsingGamepad)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void Update()
    {
        if (_step == null) return;
        if (_skipArmedUntil > 0f && Time.unscaledTime >= _skipArmedUntil) { _skipArmedUntil = 0f; Refresh(); }

        // étape à action : elle se termine toute seule
        if (_step.DoneWhen != null && _step.DoneWhen())
        {
            Action cb = _onNext;
            cb?.Invoke();
            return;
        }
        // clavier : Entrée = Suivant (jamais Espace : c'est le Dash en partie ; pas quand une action est attendue)
        if (!_step.RequireAction && Time.unscaledTime - _shownAt > 0.35f
            && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            OnNextClicked();
    }

    // Projecteur, carte et animations : après tous les autres Update (la cible a bougé / été redimensionnée).
    private void LateUpdate()
    {
        TutorialStep s = _step;
        if (s == null) return;
        float t = Time.unscaledTime - _shownAt;
        _cardGroup.alpha = Mathf.Clamp01(t / 0.25f);

        float w = _root.rect.width, h = _root.rect.height;
        if (w < 10f || h < 10f) return;

        bool hasHole = TryGetTargetRect(s, out Rect hole);
        // bandes sombres autour du trou (ou plein écran)
        SetDim(_dimTop, 0f, hasHole ? hole.yMax : 0f, w, hasHole ? h - hole.yMax : h, s);
        SetDim(_dimBottom, 0f, 0f, w, hasHole ? hole.yMin : 0f, s);
        SetDim(_dimLeft, 0f, hasHole ? hole.yMin : 0f, hasHole ? hole.xMin : 0f, hasHole ? hole.height : 0f, s);
        SetDim(_dimRight, hasHole ? hole.xMax : 0f, hasHole ? hole.yMin : 0f, hasHole ? w - hole.xMax : 0f, hasHole ? hole.height : 0f, s);

        _hole.gameObject.SetActive(hasHole);
        _holeBlocker.gameObject.SetActive(hasHole && !s.PassClicks && s.BlockInput);
        if (hasHole)
        {
            PlaceBottomLeft(_hole, hole);
            PlaceBottomLeft((RectTransform)_holeBlocker.transform, hole);
            _holeGroup.alpha = 0.65f + 0.35f * Mathf.Sin(t * 4f) * 0.5f + 0.17f;
        }

        // carte
        Vector2 size = _card.sizeDelta;
        Vector2 pos = new Vector2(w * 0.5f, h * 0.5f);   // centre
        TutorialStep.Place place = s.Position;
        if (place == TutorialStep.Place.Auto) place = hasHole ? (hole.center.y > h * 0.5f ? TutorialStep.Place.Bottom : TutorialStep.Place.Top) : (s.LanguagePicker ? TutorialStep.Place.Center : TutorialStep.Place.Top);
        float gap = 28f;
        if (place == TutorialStep.Place.Top) pos.y = h - size.y * 0.5f - TopMargin;   // sous la barre d'onglets du menu / le haut du HUD
        else if (place == TutorialStep.Place.Bottom) pos.y = size.y * 0.5f + 40f;
        else if (place == TutorialStep.Place.BottomLeft) { pos.y = size.y * 0.5f + 40f; pos.x = size.x * 0.5f + 20f; }
        else if (place == TutorialStep.Place.BottomRight) { pos.y = size.y * 0.5f + 40f; pos.x = w - size.x * 0.5f - 20f; }
        if (hasHole && s.Position == TutorialStep.Place.Auto)
        {
            // côté où la carte ne recouvre pas l'élément ; à défaut, à côté de lui
            if (place == TutorialStep.Place.Bottom && pos.y + size.y * 0.5f > hole.yMin - gap)
                pos = TrySide(hole, size, w, h, gap, pos);
            else if (place == TutorialStep.Place.Top && pos.y - size.y * 0.5f < hole.yMax + gap)
                pos = TrySide(hole, size, w, h, gap, pos);
        }
        pos.x = Mathf.Clamp(pos.x, size.x * 0.5f + 20f, Mathf.Max(size.x * 0.5f + 20f, w - size.x * 0.5f - 20f));
        pos.y = Mathf.Clamp(pos.y, size.y * 0.5f + 20f, Mathf.Max(size.y * 0.5f + 20f, h - size.y * 0.5f - 20f));
        _card.anchorMin = _card.anchorMax = Vector2.zero;
        _card.pivot = new Vector2(0.5f, 0.5f);
        _card.anchoredPosition = pos;
        _card.SetAsLastSibling();
    }

    // Place la carte à gauche ou à droite de l'élément visé, s'il y a la place ; sinon garde la position proposée.
    private Vector2 TrySide(Rect hole, Vector2 size, float w, float h, float gap, Vector2 fallback)
    {
        float roomRight = w - hole.xMax, roomLeft = hole.xMin;
        float cy = Mathf.Clamp(hole.center.y, size.y * 0.5f + 20f, h - size.y * 0.5f - 20f);
        if (roomRight >= size.x + gap + 20f) return new Vector2(hole.xMax + gap + size.x * 0.5f, cy);
        if (roomLeft >= size.x + gap + 20f) return new Vector2(hole.xMin - gap - size.x * 0.5f, cy);
        return fallback;
    }

    private void SetDim(Image img, float x, float y, float w, float h, TutorialStep s)
    {
        bool visible = s.Dim && w > 0.5f && h > 0.5f;
        img.gameObject.SetActive(visible);
        if (!visible) return;
        RectTransform rt = img.rectTransform;
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
        img.raycastTarget = s.BlockInput;
        Color dc = DimColor; dc.a = s.DimAlpha; img.color = dc;
    }

    private void PlaceBottomLeft(RectTransform rt, Rect r)
    {
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = r.position;
        rt.sizeDelta = r.size;
    }

    // Rectangle de la cible dans le repère de la fenêtre (origine en bas à gauche) : contenu visible ou rectangle, marge
    // IDENTIQUE sur les quatre côtés (réduite si l'élément touche un bord de l'écran), jamais hors de l'écran. Une cible qui
    // couvre presque tout l'écran n'a pas de projecteur.
    private bool TryGetTargetRect(TutorialStep s, out Rect rect)
    {
        rect = default;
        if (s.Target == null) return false;
        RectTransform target = s.Target();
        if (target == null || !target.gameObject.activeInHierarchy) return false;

        Canvas tc = target.GetComponentInParent<Canvas>();
        Camera cam = tc != null && tc.renderMode != RenderMode.ScreenSpaceOverlay ? tc.worldCamera : null;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        Vector3[] c = new Vector3[4];
        bool any = false;
        if (s.Tight)
        {
            float sw = Screen.width, sh = Screen.height;
            foreach (Graphic g in target.GetComponentsInChildren<Graphic>(false))
            {
                if (g == null || !g.enabled) continue;
                Vector2 lo = new Vector2(float.MaxValue, float.MaxValue), hi = new Vector2(float.MinValue, float.MinValue);
                if (g is TMP_Text tmp)
                {
                    if (string.IsNullOrEmpty(tmp.text)) continue;
                    tmp.ForceMeshUpdate();
                    Bounds b = tmp.textBounds;   // limites du texte réellement dessiné (repère local du texte)
                    if (b.size.x < 1f || b.size.y < 1f) continue;
                    Transform tt = tmp.transform;
                    for (int ix = -1; ix <= 1; ix += 2)
                        for (int iy = -1; iy <= 1; iy += 2)
                        {
                            Vector3 wp = tt.TransformPoint(b.center + new Vector3(ix * b.extents.x, iy * b.extents.y, 0f));
                            Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, wp);
                            lo = Vector2.Min(lo, sp); hi = Vector2.Max(hi, sp);
                        }
                }
                else
                {
                    RectTransform r = g.rectTransform;
                    if (r.rect.width < 2f || r.rect.height < 2f) continue;
                    r.GetWorldCorners(c);
                    for (int k2 = 0; k2 < 4; k2++)
                    {
                        Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, c[k2]);
                        lo = Vector2.Min(lo, sp); hi = Vector2.Max(hi, sp);
                    }
                    // fond d'écran / voile : ignoré
                    if (hi.x - lo.x > 0.8f * sw && hi.y - lo.y > 0.5f * sh) continue;
                }
                min = Vector2.Min(min, lo); max = Vector2.Max(max, hi);
                any = true;
            }
        }
        if (!any)
        {
            target.GetWorldCorners(c);
            for (int i = 0; i < 4; i++) { Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, c[i]); min = Vector2.Min(min, sp); max = Vector2.Max(max, sp); }
        }
        // pixels d'écran -> repère de la fenêtre (mise à l'échelle du CanvasScaler)
        float k = _root.rect.width / Mathf.Max(1f, Screen.width);
        min *= k; max *= k;
        float w = _root.rect.width, h = _root.rect.height;
        if (max.x - min.x < 2f || max.y - min.y < 2f) return false;
        if ((max.x - min.x) * (max.y - min.y) > 0.62f * w * h) return false;   // quasi tout l'écran : pas de projecteur
        const float edge = 8f;
        float pad = s.Padding;
        pad = Mathf.Min(pad, min.x - edge, min.y - edge, w - max.x - edge, h - max.y - edge);
        pad = Mathf.Max(pad, 3f);
        rect = new Rect(min.x - pad, min.y - pad, max.x - min.x + 2f * pad, max.y - min.y + 2f * pad);
        return true;
    }
}
