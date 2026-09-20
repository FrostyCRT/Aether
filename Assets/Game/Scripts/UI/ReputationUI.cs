using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// MODIFIE (2026-09-20) - REFONTE COMPLÈTE de l'onglet Réputation (l'ancienne version n'avait que 3 cartes brutes,
// des boutons Unity par défaut et une zone « ComingSoon »).
//
// À poser sur ReputationPanel (OnEnable : rafraîchit et rejoue l'entrée à chaque ouverture). Deux parties :
//   HAUT  : 3 bonus permanents de Réputation (Dégâts / Vitesse / Régénération), payés en ÉCLATS
//           (ReputationStatCardUI, une par carte).
//   BAS   : boutique de SKINS par personnage. Skins CLASSIQUES = OR, skins PRESTIGE = ÉCLATS. Les rangées lisent
//           SkinCatalog ; les skins n'étant pas encore fabriqués (Tripo3D), les emplacements libres affichent
//           « BIENTÔT ». Aperçu à gauche (portrait, nom, gamme, description) avec l'action Acheter / Équiper.
//
// Ce composant ne construit que les cartes de skins (copies du modèle) : le reste de la hiérarchie vient de la scène
// (Aether > Rebuild Reputation Page). Les données viennent de MetaProgressionManager (sauvegarde) et SkinCatalog.
public class ReputationUI : MonoBehaviour
{
    [Serializable]
    public class CharTab
    {
        public Button button;
        public Image fill;
        public Image border;
        public TextMeshProUGUI label;
        public Image lockIcon;
    }

    [Serializable]
    public class Preview
    {
        public CanvasGroup group;
        public Image frame;
        public Image glow;
        public RectTransform portrait;
        public Image portraitImage;
        public GameObject lockGroup;
        public TextMeshProUGUI lockText;
        public Image tierChip;
        public TextMeshProUGUI tierText;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI descText;
        public Button actionButton;
        public Image actionBg;
        public Image actionGlow;
        public TextMeshProUGUI actionLabel;
        public GameObject actionPriceRoot;
        public Image actionPriceIcon;
        public TextMeshProUGUI actionPriceText;
    }

    [Header("Monnaies")]
    [SerializeField] private TextMeshProUGUI _goldText;
    [SerializeField] private TextMeshProUGUI _eclatsText;
    [SerializeField] private RectTransform _goldPill;
    [SerializeField] private RectTransform _eclatsPill;

    [Header("Bonus de Réputation")]
    [SerializeField] private ReputationStatCardUI[] _statCards;

    [Header("Entrée animée (blocs de la page)")]
    [SerializeField] private CanvasGroup[] _introBlocks;

    [Header("Skins : personnage")]
    [SerializeField] private CharTab[] _charTabs;

    [Header("Skins : rangées")]
    [SerializeField] private RectTransform _classicContent;
    [SerializeField] private RectTransform _prestigeContent;
    [SerializeField] private SkinCardUI _cardTemplate;
    [SerializeField] private int _slotsPerRow = 4;

    [Header("Skins : aperçu")]
    [SerializeField] private Preview _preview;

    [Header("Images")]
    [SerializeField] private Sprite _goldIcon;
    [SerializeField] private Sprite _eclatIcon;
    [Tooltip("Portrait d'origine de chaque personnage (Aether, Kael, Lyra), utilisé quand un skin n'a pas d'image d'aperçu.")]
    [SerializeField] private Sprite[] _portraits = new Sprite[3];
    [Tooltip("Matériau désaturant (Aether/UI/Desaturate) : portrait d'un personnage verrouillé.")]
    [SerializeField] private Material _lockedMaterial;

    [Header("Message")]
    [SerializeField] private CanvasGroup _toastGroup;
    [SerializeField] private TextMeshProUGUI _toastText;

    // AJOUTE - bouton reserve au developpement, PAS destine a la version finale.
    [Header("DEBUG UNIQUEMENT - a retirer avant release")]
    [SerializeField] private Button _debugResetButton;

    private int _char;
    private string _selectedSkinId;
    private string _confirmSkinId;
    private float _confirmUntil;
    private float _shownGold, _shownEclats;
    private int _lastGold = -1, _lastEclats = -1;
    private Coroutine _toast, _intro, _fade, _punch;
    private readonly List<SkinCardUI> _cards = new List<SkinCardUI>();
    private readonly Dictionary<CanvasGroup, Vector2> _blockBase = new Dictionary<CanvasGroup, Vector2>();

    // ---- cycle de vie --------------------------------------------------------------------------------------
    private void Awake()
    {
        if (_statCards != null)
            foreach (ReputationStatCardUI c in _statCards) if (c != null) c.BuyClicked += OnBuyStat;

        if (_charTabs != null)
            for (int i = 0; i < _charTabs.Length; i++)
            {
                int index = i;
                if (_charTabs[i].button != null) _charTabs[i].button.onClick.AddListener(() => SelectCharacter(index));
            }

        if (_preview != null && _preview.actionButton != null) _preview.actionButton.onClick.AddListener(OnAction);

        if (_debugResetButton != null)
        {
            _debugResetButton.onClick.RemoveAllListeners();
            _debugResetButton.onClick.AddListener(OnDebugResetClicked);
        }

        if (_introBlocks != null)
            foreach (CanvasGroup g in _introBlocks) if (g != null) _blockBase[g] = ((RectTransform)g.transform).anchoredPosition;
        if (_toastGroup != null) _toastGroup.alpha = 0f;
        if (_cardTemplate != null) _cardTemplate.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        MetaProgressionManager meta = MetaProgressionManager.Instance;
        _char = meta != null ? meta.GetSelectedCharacterIndex() : 0;
        SkinEntry equipped = meta != null ? meta.GetEquippedSkin(_char) : SkinCatalog.Instance.DefaultFor(_char);
        _selectedSkinId = equipped != null ? equipped.id : null;
        _confirmSkinId = null;
        if (_toastGroup != null) _toastGroup.alpha = 0f;

        if (meta != null && meta.Data != null)
        {
            _shownGold = meta.Data.totalGold;
            _shownEclats = meta.TotalEclats;
        }
        _lastGold = _lastEclats = -1;

        RefreshAll();
        if (_intro != null) StopCoroutine(_intro);
        _intro = StartCoroutine(IntroRoutine());
    }

    private void OnDisable()
    {
        _toast = _intro = _fade = _punch = null;
    }

    private void Update()
    {
        MetaProgressionManager meta = MetaProgressionManager.Instance;
        if (meta != null && meta.Data != null)
        {
            _shownGold = Mathf.MoveTowards(_shownGold, meta.Data.totalGold, Mathf.Max(3f, Mathf.Abs(_shownGold - meta.Data.totalGold) * 6f) * Time.unscaledDeltaTime);
            _shownEclats = Mathf.MoveTowards(_shownEclats, meta.TotalEclats, Mathf.Max(3f, Mathf.Abs(_shownEclats - meta.TotalEclats) * 6f) * Time.unscaledDeltaTime);
            int g = Mathf.RoundToInt(_shownGold), e = Mathf.RoundToInt(_shownEclats);
            if (g != _lastGold && _goldText != null) { _goldText.text = g.ToString(); _lastGold = g; }
            if (e != _lastEclats && _eclatsText != null) { _eclatsText.text = e.ToString(); _lastEclats = e; }
        }

        // l'achat de skin demande une seconde confirmation : elle expire au bout de 3 s
        if (_confirmSkinId != null && Time.unscaledTime > _confirmUntil)
        {
            _confirmSkinId = null;
            RefreshPreview();
        }
    }

    // ---- rafraîchissement ------------------------------------------------------------------------------------
    private void RefreshAll()
    {
        RefreshStatCards();
        RefreshTabs();
        RebuildRows();
        RefreshPreview();
    }

    private void RefreshStatCards()
    {
        if (_statCards == null) return;
        foreach (ReputationStatCardUI c in _statCards) if (c != null) c.Refresh();
    }

    private void RefreshTabs()
    {
        if (_charTabs == null) return;
        MetaProgressionManager meta = MetaProgressionManager.Instance;
        for (int i = 0; i < _charTabs.Length; i++)
        {
            CharTab t = _charTabs[i];
            bool sel = i == _char;
            bool locked = meta != null && !meta.IsCharacterUnlocked(i);
            Color accent = ReputationStyle.CharacterAccent(i);
            if (t.fill != null) t.fill.color = sel ? new Color(accent.r * 0.85f, accent.g * 0.85f, accent.b * 0.85f, 1f) : ReputationStyle.Field;
            if (t.border != null) t.border.color = new Color(accent.r, accent.g, accent.b, sel ? 1f : 0.4f);
            if (t.label != null) t.label.color = sel ? Color.white : (locked ? new Color(ReputationStyle.Muted.r, ReputationStyle.Muted.g, ReputationStyle.Muted.b, 0.7f) : ReputationStyle.Muted);
            if (t.lockIcon != null) t.lockIcon.gameObject.SetActive(locked);
        }
    }

    private void RebuildRows()
    {
        if (_cardTemplate == null) return;
        foreach (SkinCardUI c in _cards) if (c != null) { c.transform.SetParent(null, false); Destroy(c.gameObject); }
        _cards.Clear();

        MetaProgressionManager meta = MetaProgressionManager.Instance;
        bool locked = meta != null && !meta.IsCharacterUnlocked(_char);
        SkinCatalog catalog = SkinCatalog.Instance;
        Sprite portrait = Portrait(_char);

        FillRow(_classicContent, SkinTier.Classic, catalog, meta, locked, portrait);
        FillRow(_prestigeContent, SkinTier.Prestige, catalog, meta, locked, portrait);
    }

    private void FillRow(RectTransform content, SkinTier tier, SkinCatalog catalog, MetaProgressionManager meta, bool locked, Sprite portrait)
    {
        if (content == null) return;
        List<SkinEntry> list = catalog.For(_char, tier);
        SkinEntry equipped = meta != null ? meta.GetEquippedSkin(_char) : null;
        foreach (SkinEntry skin in list)
        {
            SkinCardUI card = Instantiate(_cardTemplate, content);
            card.gameObject.SetActive(true);
            bool owned = meta != null && meta.IsSkinOwned(skin);
            card.Bind(skin, portrait, _goldIcon, _eclatIcon, owned, equipped != null && equipped.id == skin.id, locked, _lockedMaterial);
            card.SetAffordable(CanAfford(meta, skin));
            card.SetSelected(skin.id == _selectedSkinId);
            card.Clicked += OnSkinClicked;
            _cards.Add(card);
        }
        for (int i = list.Count; i < _slotsPerRow; i++)
        {
            SkinCardUI ph = Instantiate(_cardTemplate, content);
            ph.gameObject.SetActive(true);
            ph.BindPlaceholder(tier);
            _cards.Add(ph);
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    private static bool CanAfford(MetaProgressionManager meta, SkinEntry skin)
    {
        if (meta == null || meta.Data == null) return false;
        return skin.tier == SkinTier.Prestige ? meta.TotalEclats >= skin.cost : meta.Data.totalGold >= skin.cost;
    }

    private Sprite Portrait(int characterIndex)
    {
        return _portraits != null && characterIndex >= 0 && characterIndex < _portraits.Length ? _portraits[characterIndex] : null;
    }

    private void RefreshPreview()
    {
        if (_preview == null) return;
        MetaProgressionManager meta = MetaProgressionManager.Instance;
        SkinCatalog catalog = SkinCatalog.Instance;
        SkinEntry skin = catalog.Get(_selectedSkinId);
        if (skin == null || skin.characterIndex != _char) skin = catalog.DefaultFor(_char);
        if (skin == null) return;
        _selectedSkinId = skin.id;

        bool locked = meta != null && !meta.IsCharacterUnlocked(_char);
        bool owned = meta != null && meta.IsSkinOwned(skin);
        SkinEntry equippedSkin = meta != null ? meta.GetEquippedSkin(_char) : null;
        bool equipped = equippedSkin != null && equippedSkin.id == skin.id;
        Color accent = ReputationStyle.CharacterAccent(_char);
        Color tierColor = ReputationStyle.TierColor(skin.tier);

        if (_preview.frame != null) _preview.frame.color = new Color(accent.r, accent.g, accent.b, 0.9f);
        if (_preview.glow != null) _preview.glow.color = new Color(accent.r, accent.g, accent.b, locked ? 0.08f : 0.22f);
        if (_preview.portraitImage != null)
        {
            _preview.portraitImage.sprite = skin.preview != null ? skin.preview : Portrait(_char);
            _preview.portraitImage.material = locked && _lockedMaterial != null ? _lockedMaterial : null;
            _preview.portraitImage.color = locked ? new Color(1f, 1f, 1f, 0.75f) : Color.white;
            RectTransform pm = _preview.portraitImage.transform.parent as RectTransform;
            ReputationStyle.FitPortrait(_preview.portraitImage, pm != null ? pm.rect.width * 0.92f : 380f);
        }
        if (_preview.lockGroup != null) _preview.lockGroup.SetActive(locked);
        if (_preview.lockText != null) _preview.lockText.text = SkinCatalog.CharacterNames[_char] + " EST VERROUILLÉ\n<size=70%><color=#" + ReputationStyle.Hex(ReputationStyle.Muted) + ">Débloque-le dans l'onglet Personnages\npour accéder à ses skins.</color></size>";

        if (_preview.tierChip != null) _preview.tierChip.color = new Color(tierColor.r, tierColor.g, tierColor.b, 0.2f);
        if (_preview.tierText != null)
        {
            _preview.tierText.text = skin.tier == SkinTier.Prestige ? "PRESTIGE  ·  ÉCLATS" : "CLASSIQUE  ·  OR";
            _preview.tierText.color = tierColor;
        }
        if (_preview.nameText != null) _preview.nameText.text = skin.displayName.ToUpperInvariant();
        if (_preview.descText != null) _preview.descText.text = string.IsNullOrEmpty(skin.description) ? "" : skin.description;

        // action
        string label; bool interactable; bool showPrice = false; Color tint = Color.white; Color labelColor = ReputationStyle.ButtonText; bool glow = false;
        if (locked) { label = "PERSONNAGE VERROUILLÉ"; interactable = false; tint = new Color(0.6f, 0.55f, 0.55f, 0.85f); labelColor = new Color32(0x8A, 0x2E, 0x24, 0xFF); }
        else if (owned && equipped) { label = "ÉQUIPÉ"; interactable = false; tint = new Color(0.85f, 0.8f, 0.6f, 0.95f); labelColor = new Color32(0x6B, 0x55, 0x1E, 0xFF); }
        else if (owned) { label = "ÉQUIPER"; interactable = true; glow = true; }
        else if (!CanAfford(meta, skin))
        {
            label = skin.tier == SkinTier.Prestige ? "ÉCLATS INSUFFISANTS" : "OR INSUFFISANT";
            interactable = false; tint = new Color(0.62f, 0.56f, 0.54f, 1f); labelColor = new Color32(0x8A, 0x2E, 0x24, 0xFF);
            showPrice = false;
        }
        else
        {
            bool confirm = _confirmSkinId == skin.id;
            label = confirm ? "CONFIRMER L'ACHAT ?" : "ACHETER";
            interactable = true; showPrice = !confirm; glow = true;
        }

        if (_preview.actionButton != null) _preview.actionButton.interactable = interactable;
        if (_preview.actionBg != null) _preview.actionBg.color = tint;
        if (_preview.actionGlow != null) _preview.actionGlow.gameObject.SetActive(glow);
        if (_preview.actionLabel != null) { _preview.actionLabel.text = label; _preview.actionLabel.color = labelColor; }
        if (_preview.actionLabel != null) _preview.actionLabel.rectTransform.offsetMax = new Vector2(showPrice ? -150f : -24f, 0f);
        if (_preview.actionPriceRoot != null) _preview.actionPriceRoot.SetActive(showPrice);
        if (showPrice)
        {
            if (_preview.actionPriceIcon != null) _preview.actionPriceIcon.sprite = skin.tier == SkinTier.Prestige ? _eclatIcon : _goldIcon;
            if (_preview.actionPriceText != null) _preview.actionPriceText.text = skin.cost.ToString();
        }

        // sélection sur les cartes + prix en rouge si la monnaie manque
        foreach (SkinCardUI c in _cards)
        {
            if (c == null || c.IsPlaceholder) continue;
            c.SetSelected(c.Skin.id == skin.id);
            c.SetAffordable(CanAfford(meta, c.Skin));
        }
    }

    // ---- interactions ------------------------------------------------------------------------------------------
    private void OnBuyStat(ReputationStatCardUI card)
    {
        MetaProgressionManager meta = MetaProgressionManager.Instance;
        if (meta == null) return;
        if (meta.TryBuyNode(card.NodeId))
        {
            RefreshStatCards();
            RefreshPreview();                 // les Éclats ont baissé : l'état des boutons de skins Prestige change
            PunchPill(_eclatsPill);
        }
    }

    private void SelectCharacter(int index)
    {
        if (index == _char) return;
        _char = index;
        MetaProgressionManager meta = MetaProgressionManager.Instance;
        SkinEntry equipped = meta != null ? meta.GetEquippedSkin(_char) : SkinCatalog.Instance.DefaultFor(_char);
        _selectedSkinId = equipped != null ? equipped.id : null;
        _confirmSkinId = null;
        RefreshTabs();
        RebuildRows();
        RefreshPreview();
        if (_fade != null) StopCoroutine(_fade);
        _fade = StartCoroutine(PreviewFade());
    }

    private void OnSkinClicked(SkinCardUI card)
    {
        if (card == null || card.Skin == null) return;
        _selectedSkinId = card.Skin.id;
        _confirmSkinId = null;
        RefreshPreview();
        if (_fade != null) StopCoroutine(_fade);
        _fade = StartCoroutine(PreviewFade());
    }

    private void OnAction()
    {
        MetaProgressionManager meta = MetaProgressionManager.Instance;
        SkinCatalog catalog = SkinCatalog.Instance;
        SkinEntry skin = catalog.Get(_selectedSkinId);
        if (meta == null || skin == null) return;

        if (!meta.IsCharacterUnlocked(skin.characterIndex))
        {
            Toast(SkinCatalog.CharacterNames[skin.characterIndex] + " est verrouillé.");
            return;
        }

        if (meta.IsSkinOwned(skin))
        {
            if (meta.EquipSkin(skin.id))
            {
                Toast(skin.displayName + " équipé pour " + SkinCatalog.CharacterNames[skin.characterIndex] + ".");
                RebuildRows();
                RefreshPreview();
                PunchPreview();
            }
            return;
        }

        // achat : deux clics (le premier arme la confirmation, 3 s pour confirmer)
        if (_confirmSkinId != skin.id)
        {
            _confirmSkinId = skin.id;
            _confirmUntil = Time.unscaledTime + 3f;
            RefreshPreview();
            return;
        }
        _confirmSkinId = null;
        MetaProgressionManager.SkinBuyResult r = meta.TryBuySkin(skin.id);
        switch (r)
        {
            case MetaProgressionManager.SkinBuyResult.Ok:
                meta.EquipSkin(skin.id);           // un skin acheté est équipé tout de suite
                Toast("Nouveau skin : " + skin.displayName + " !");
                RebuildRows();
                RefreshPreview();
                PunchPreview();
                PunchPill(skin.tier == SkinTier.Prestige ? _eclatsPill : _goldPill);
                break;
            case MetaProgressionManager.SkinBuyResult.NotEnoughCurrency:
                Toast(skin.tier == SkinTier.Prestige ? "Il te manque des Éclats." : "Il te manque de l'or.");
                RefreshPreview();
                break;
            case MetaProgressionManager.SkinBuyResult.CharacterLocked:
                Toast(SkinCatalog.CharacterNames[skin.characterIndex] + " est verrouillé.");
                RefreshPreview();
                break;
            default:
                RefreshPreview();
                break;
        }
    }

    // AJOUTE
    private void OnDebugResetClicked()
    {
        if (MetaProgressionManager.Instance == null) return;
        MetaProgressionManager.Instance.DebugResetReputation();
        RefreshAll();
    }

    // ---- animations / message ------------------------------------------------------------------------------------
    private IEnumerator IntroRoutine()
    {
        if (_introBlocks == null || _introBlocks.Length == 0) yield break;
        for (int i = 0; i < _introBlocks.Length; i++)
        {
            CanvasGroup g = _introBlocks[i];
            if (g == null) continue;
            g.alpha = 0f;
            ((RectTransform)g.transform).anchoredPosition = _blockBase[g] + new Vector2(0f, -28f);
        }
        float t = 0f;
        float total = 0.42f + 0.09f * _introBlocks.Length;
        while (t < total)
        {
            t += Time.unscaledDeltaTime;
            for (int i = 0; i < _introBlocks.Length; i++)
            {
                CanvasGroup g = _introBlocks[i];
                if (g == null) continue;
                float u = Mathf.Clamp01((t - i * 0.09f) / 0.42f);
                float e = 1f - (1f - u) * (1f - u) * (1f - u);
                g.alpha = e;
                ((RectTransform)g.transform).anchoredPosition = _blockBase[g] + new Vector2(0f, -28f * (1f - e));
            }
            yield return null;
        }
        for (int i = 0; i < _introBlocks.Length; i++)
        {
            CanvasGroup g = _introBlocks[i];
            if (g == null) continue;
            g.alpha = 1f;
            ((RectTransform)g.transform).anchoredPosition = _blockBase[g];
        }
        _intro = null;
    }

    private IEnumerator PreviewFade()
    {
        if (_preview == null || _preview.group == null) yield break;
        float t = 0f;
        while (t < 0.22f)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / 0.22f);
            _preview.group.alpha = 0.25f + 0.75f * u;
            if (_preview.portrait != null) _preview.portrait.localScale = Vector3.one * (1.03f - 0.03f * u);
            yield return null;
        }
        _preview.group.alpha = 1f;
        if (_preview.portrait != null) _preview.portrait.localScale = Vector3.one;
        _fade = null;
    }

    private void PunchPreview()
    {
        if (_punch != null) StopCoroutine(_punch);
        _punch = StartCoroutine(PunchRoutine(_preview != null ? _preview.portrait : null, 0.06f));
    }

    private void PunchPill(RectTransform pill)
    {
        if (pill != null && isActiveAndEnabled) StartCoroutine(PunchRoutine(pill, 0.12f));
    }

    private IEnumerator PunchRoutine(RectTransform rt, float amount)
    {
        if (rt == null) yield break;
        float t = 0f;
        while (t < 0.4f)
        {
            t += Time.unscaledDeltaTime;
            float s = 1f + amount * Mathf.Sin(Mathf.Clamp01(t / 0.4f) * Mathf.PI);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    private void Toast(string message)
    {
        if (_toastGroup == null || _toastText == null) return;
        _toastText.text = message;
        if (_toast != null) StopCoroutine(_toast);
        _toast = StartCoroutine(ToastRoutine());
    }

    private IEnumerator ToastRoutine()
    {
        float t = 0f;
        while (t < 0.15f) { t += Time.unscaledDeltaTime; _toastGroup.alpha = t / 0.15f; yield return null; }
        _toastGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(2.4f);
        t = 0f;
        while (t < 0.3f) { t += Time.unscaledDeltaTime; _toastGroup.alpha = 1f - t / 0.3f; yield return null; }
        _toastGroup.alpha = 0f;
        _toast = null;
    }
}
