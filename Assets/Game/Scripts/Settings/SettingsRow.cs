using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Palette de la page Paramètres : même famille que la fiche Personnage / l'arbre (fond nuit, gris-lavande),
// avec un accent bleu "cristal" (les autres pages ont l'orange / vert / cyan de leur personnage).
public static class SettingsStyle
{
    public static readonly Color PanelDark = new Color32(0x0C, 0x0A, 0x12, 0xFF);
    public static readonly Color Field = new Color32(0x15, 0x11, 0x1F, 0xFF);
    public static readonly Color FieldHover = new Color32(0x22, 0x1C, 0x31, 0xFF);
    public static readonly Color Accent = new Color32(0x4F, 0xA8, 0xE8, 0xFF);
    public static readonly Color AccentSoft = new Color32(0x2F, 0x62, 0x8C, 0xFF);
    // AJOUTÉ (2026-10-25, audit de lisibilité) - fond plein sous un texte blanc (interrupteur « OUI », onglet choisi) :
    // le blanc sur Accent ne faisait que 2,6:1, 4,7:1 sur ce bleu plus profond. Accent reste pour les liserés.
    public static readonly Color AccentFill = new Color32(0x2B, 0x78, 0xB8, 0xFF);
    public static readonly Color Muted = new Color32(0x9C, 0x93, 0xAE, 0xFF);
    public static readonly Color Body = new Color32(0xD7, 0xD0, 0xE2, 0xFF);
    public static readonly Color Track = new Color32(0x3A, 0x35, 0x46, 0xFF);
    public static readonly Color Gold = new Color32(0xFF, 0xC8, 0x3D, 0xFF);
    public static readonly Color Warn = new Color32(0xE2, 0x60, 0x4F, 0xFF);
    public static readonly Color Ok = new Color32(0x52, 0xD4, 0x6B, 0xFF);
}

// Une LIGNE de la page Paramètres. Un seul composant pour tous les types : le modèle contient les commandes de
// chaque type (curseur, interrupteur, choix, touches, préréglages), Bind() n'active que celles de la ligne.
// Les valeurs viennent de GameSettings / InputBindings ; la ligne ne stocke rien.
public class SettingsRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Commun")]
    [SerializeField] private TextMeshProUGUI _label;
    [SerializeField] private TextMeshProUGUI _description;
    [SerializeField] private Image _highlight;
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private Button _defaultButton;

    [Header("Curseur")]
    [SerializeField] private GameObject _sliderRoot;
    [SerializeField] private Slider _slider;
    [SerializeField] private TextMeshProUGUI _sliderValue;

    [Header("Interrupteur")]
    [SerializeField] private GameObject _toggleRoot;
    [SerializeField] private Button _toggleButton;
    [SerializeField] private Image _toggleTrack;
    [SerializeField] private RectTransform _toggleKnob;
    [SerializeField] private TextMeshProUGUI _toggleText;

    [Header("Choix")]
    [SerializeField] private GameObject _choiceRoot;
    [SerializeField] private Button _choiceCenter;
    [SerializeField] private TextMeshProUGUI _choiceText;

    [Header("Touches (2 emplacements)")]
    [SerializeField] private GameObject _keysRoot;
    [SerializeField] private Button[] _keyButtons;
    [SerializeField] private TextMeshProUGUI[] _keyTexts;
    [SerializeField] private Image[] _keyBorders;

    [Header("Préréglages")]
    [SerializeField] private GameObject _presetsRoot;
    [SerializeField] private Button[] _presetButtons;
    [SerializeField] private TextMeshProUGUI[] _presetTexts;
    [SerializeField] private Image[] _presetBorders;

    // AJOUTÉ (2026-10-25) - ligne "pastilles de couleur" (RowKind.Swatches, couleur du compteur d'images) : construite en
    // code au premier Bind, au même emplacement et à la même taille que le bouton de choix, sans toucher au prefab de ligne.
    private RectTransform _swatchRoot;
    private Image[] _swatchBorders;

    public RowDef Def { get; private set; }
    private SettingsPage _page;
    private bool _binding;
    private string[] _options;
    private float _knobT = -1f;       // 0 = éteint, 1 = allumé (animé)
    private bool _hover;
    //private int _lastCaptureSlot = -1;

    // ---- liaison ------------------------------------------------------------------------------------------
    public void Bind(RowDef def, SettingsPage page)
    {
        Def = def;
        _page = page;
        _binding = true;

        _label.text = def.label;
        if (_description != null) _description.text = def.description ?? "";
        // sans description (ni indication de désactivation), le libellé se centre dans la ligne (66 px au lieu de 88)
        RectTransform lr = _label.rectTransform;
        bool bare = string.IsNullOrEmpty(def.description) && string.IsNullOrEmpty(def.disabledHint);
        lr.anchoredPosition = new Vector2(lr.anchoredPosition.x, bare ? -13f : -8f);

        SetActive(_sliderRoot, def.kind == RowKind.Slider);
        SetActive(_toggleRoot, def.kind == RowKind.Toggle);
        SetActive(_choiceRoot, def.kind == RowKind.Choice);
        SetActive(_keysRoot, def.kind == RowKind.Keybind);
        SetActive(_presetsRoot, def.kind == RowKind.Presets);
        if (def.kind == RowKind.Swatches && _swatchRoot == null) BuildSwatches(def.swatches);
        if (_swatchRoot != null) _swatchRoot.gameObject.SetActive(def.kind == RowKind.Swatches);

        if (def.kind == RowKind.Slider)
        {
            _slider.minValue = def.min;
            _slider.maxValue = def.max;
            _slider.onValueChanged.RemoveAllListeners();
            _slider.onValueChanged.AddListener(OnSliderChanged);
        }
        if (def.kind == RowKind.Toggle)
        {
            _toggleButton.onClick.RemoveAllListeners();
            _toggleButton.onClick.AddListener(OnToggleClicked);
        }
        if (def.kind == RowKind.Choice)
        {
            _options = def.dynamicOptions != null ? def.dynamicOptions() : def.options;
            _choiceCenter.onClick.RemoveAllListeners();
            _choiceCenter.onClick.AddListener(OpenChoiceList);
        }
        if (def.kind == RowKind.Keybind)
        {
            for (int s = 0; s < _keyButtons.Length; s++)
            {
                int slot = s;
                _keyButtons[s].onClick.RemoveAllListeners();
                _keyButtons[s].onClick.AddListener(() => _page.BeginCapture(Def.action, slot, this));
            }
        }
        if (def.kind == RowKind.Presets)
        {
            for (int p = 0; p < _presetButtons.Length; p++)
            {
                int preset = p;
                _presetButtons[p].onClick.RemoveAllListeners();
                _presetButtons[p].onClick.AddListener(() => { InputBindings.ApplyPreset((InputBindings.Preset)preset); _page.Toast(Loc.F("Disposition « {0} » appliquée", Loc.T(InputBindings.PresetNames[preset]))); });
            }
        }

        if (_defaultButton != null)
        {
            _defaultButton.onClick.RemoveAllListeners();
            _defaultButton.onClick.AddListener(OnDefaultClicked);
        }

        _binding = false;
        Refresh(true);
    }

    private static void SetActive(GameObject go, bool on) { if (go != null) go.SetActive(on); }

    private const float DisabledAlpha = 0.42f;

    // Estompe toutes les branches de la ligne sauf celle qui mène à la description (un CanvasGroup par branche).
    private void DimExceptDescription(Transform root, float alpha)
    {
        foreach (Transform child in root)
        {
            if (child == _description.transform) continue;
            if (_description.transform.IsChildOf(child)) { DimExceptDescription(child, alpha); continue; }
            CanvasGroup g = child.GetComponent<CanvasGroup>();
            if (g == null)
            {
                if (alpha >= 1f) continue;
                g = child.gameObject.AddComponent<CanvasGroup>();
            }
            g.alpha = alpha;
        }
    }

    // ---- lecture des valeurs -------------------------------------------------------------------------------
    public void Refresh(bool instant = false)
    {
        if (Def == null) return;
        _binding = true;

        bool enabled = Def.isEnabled == null || Def.isEnabled();
        if (_group != null)
        {
            // MODIFIÉ (2026-10-25, audit de lisibilité) - l'estompage de la ligne désactivée épargne la description :
            // c'est elle qui affiche l'explication dorée (POURQUOI la ligne est grisée), illisible à 42 %.
            if (_description != null && _description.transform.IsChildOf(_group.transform))
            {
                _group.alpha = 1f;
                DimExceptDescription(_group.transform, enabled ? 1f : DisabledAlpha);
            }
            else _group.alpha = enabled ? 1f : DisabledAlpha;
            _group.interactable = enabled;
            _group.blocksRaycasts = enabled;
        }
        if (_description != null)
        {
            bool hint = !enabled && !string.IsNullOrEmpty(Def.disabledHint);
            _description.text = hint ? Def.disabledHint : (Def.description ?? "");
            _description.color = hint ? SettingsStyle.Gold : SettingsStyle.Muted;
        }

        switch (Def.kind)
        {
            case RowKind.Slider:
            {
                float v = GameSettings.GetFloat(Def.key);
                _slider.SetValueWithoutNotify(v);
                _sliderValue.text = Mathf.RoundToInt(v * 100f) + "%";
                break;
            }
            case RowKind.Toggle:
            {
                bool on = GameSettings.GetBool(Def.key);
                _toggleText.text = on ? Loc.T("OUI") : Loc.T("NON");
                _toggleText.color = on ? Color.white : SettingsStyle.Muted;
                _toggleText.alignment = on ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;   // à l'opposé du bouton
                _toggleTrack.color = on ? SettingsStyle.AccentFill : SettingsStyle.Track;
                float target = on ? 1f : 0f;
                _knobT = instant || _knobT < 0f ? target : _knobT;
                _toggleTarget = target;
                PlaceKnob();
                break;
            }
            case RowKind.Choice:
            {
                int i = Def.getIndex != null ? Def.getIndex() : GameSettings.GetInt(Def.key);
                i = Mathf.Clamp(i, 0, Mathf.Max(0, _options.Length - 1));
                _choiceText.text = _options.Length > 0 ? _options[i] : "";
                break;
            }
            case RowKind.Keybind:
            {
                for (int s = 0; s < _keyTexts.Length; s++)
                {
                    KeyCode k = InputBindings.Get(Def.action, s);
                    _keyTexts[s].text = GameInput.KeyLabel(k);
                    _keyTexts[s].color = k == KeyCode.None ? SettingsStyle.Muted : Color.white;
                    _keyBorders[s].color = SettingsStyle.AccentSoft;
                }
                break;
            }
            case RowKind.Swatches:
            {
                int cur = GameSettings.GetInt(Def.key);
                for (int s = 0; s < _swatchBorders.Length; s++)
                {
                    _swatchBorders[s].color = s == cur ? Color.white : SettingsStyle.Track;
                    _swatchBorders[s].rectTransform.localScale = Vector3.one * (s == cur ? 1.12f : 1f);
                }
                break;
            }
            case RowKind.Presets:
            {
                int match = InputBindings.MatchingPreset();
                for (int p = 0; p < _presetTexts.Length; p++)
                {
                    bool sel = p == match;
                    _presetTexts[p].text = Loc.T(InputBindings.PresetNames[p]);
                    _presetTexts[p].color = sel ? Color.white : SettingsStyle.Body;
                    _presetBorders[p].color = sel ? SettingsStyle.Accent : SettingsStyle.AccentSoft;
                }
                break;
            }
        }

        RefreshDefaultButton();
        _binding = false;
    }

    private void RefreshDefaultButton()
    {
        if (_defaultButton == null) return;
        bool showable = Def.kind != RowKind.Presets;
        bool isDefault = true;
        if (showable)
        {
            if (Def.isDefault != null) isDefault = Def.isDefault();
            else if (Def.kind == RowKind.Keybind) isDefault = InputBindings.IsDefault(Def.action);
            else if (!string.IsNullOrEmpty(Def.key)) isDefault = GameSettings.IsDefault(Def.key);
        }
        _defaultButton.gameObject.SetActive(showable && !isDefault);
    }

    // ---- interactions ----------------------------------------------------------------------------------------
    private void OnSliderChanged(float v)
    {
        if (_binding) return;
        v = Mathf.Round(v * 100f) / 100f;                       // pas de 1 %
        GameSettings.SetFloat(Def.key, v);
        _page.OnRowChanged(this);
    }

    private void OnToggleClicked()
    {
        if (_binding) return;
        GameSettings.SetBool(Def.key, !GameSettings.GetBool(Def.key));
        _page.OnRowChanged(this);
    }

    // Un clic sur le bouton ouvre la liste de toutes les valeurs (SettingsDropdown) ; le choix s'applique à la sélection.
    private void OpenChoiceList()
    {
        if (_binding || _options == null || _options.Length == 0) return;
        int cur = Def.getIndex != null ? Def.getIndex() : GameSettings.GetInt(Def.key);
        _page.ToggleDropdown((RectTransform)_choiceCenter.transform, _options, cur, ApplyChoice);
    }

    private void ApplyChoice(int index)
    {
        if (_options == null || index < 0 || index >= _options.Length) return;
        int cur = Def.getIndex != null ? Def.getIndex() : GameSettings.GetInt(Def.key);
        if (index == cur) return;
        _page.BeforeDisplayChange(Def);
        if (Def.setIndex != null) Def.setIndex(index); else GameSettings.SetInt(Def.key, index);
        _page.OnRowChanged(this);
    }

    private void BuildSwatches(Color[] colors)
    {
        if (colors == null || colors.Length == 0 || _choiceRoot == null) return;
        RectTransform anchor = (RectTransform)_choiceRoot.transform;
        _swatchRoot = new GameObject("SwatchesRoot", typeof(RectTransform)).GetComponent<RectTransform>();
        _swatchRoot.SetParent(anchor.parent, false);
        _swatchRoot.anchorMin = anchor.anchorMin;
        _swatchRoot.anchorMax = anchor.anchorMax;
        _swatchRoot.pivot = anchor.pivot;
        _swatchRoot.anchoredPosition = anchor.anchoredPosition;
        _swatchRoot.sizeDelta = anchor.sizeDelta;

        int n = colors.Length;
        float size = anchor.sizeDelta.y;
        float gap = n > 1 ? (anchor.sizeDelta.x - n * size) / (n - 1) : 0f;
        _swatchBorders = new Image[n];
        for (int i = 0; i < n; i++)
        {
            int index = i;
            RectTransform rt = new GameObject("Swatch" + (i + 1), typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
            rt.SetParent(_swatchRoot, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(i * (size + gap), 0f);
            _swatchBorders[i] = rt.GetComponent<Image>();
            Button button = rt.GetComponent<Button>();
            button.targetGraphic = _swatchBorders[i];
            button.onClick.AddListener(() => ApplySwatch(index));

            // cadre (couleur = sélection) > liseré sombre > couleur : le cadre blanc de la pastille choisie reste visible
            // même autour de la pastille blanche
            RectTransform gapRt = new GameObject("Gap", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            gapRt.SetParent(rt, false);
            gapRt.anchorMin = Vector2.zero;
            gapRt.anchorMax = Vector2.one;
            gapRt.offsetMin = new Vector2(3f, 3f);
            gapRt.offsetMax = new Vector2(-3f, -3f);
            Image gapImage = gapRt.GetComponent<Image>();
            gapImage.color = SettingsStyle.PanelDark;
            gapImage.raycastTarget = false;

            RectTransform fill = new GameObject("Color", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            fill.SetParent(rt, false);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(6f, 6f);
            fill.offsetMax = new Vector2(-6f, -6f);
            Image swatch = fill.GetComponent<Image>();
            swatch.color = colors[i];
            swatch.raycastTarget = false;
        }
    }

    private void ApplySwatch(int index)
    {
        if (_binding || index == GameSettings.GetInt(Def.key)) return;
        GameSettings.SetInt(Def.key, index);
        _page.OnRowChanged(this);
    }

    private void OnDefaultClicked()
    {
        _page.BeforeDisplayChange(Def);
        if (Def.reset != null) Def.reset();
        else if (Def.kind == RowKind.Keybind) InputBindings.Reset(Def.action);
        else if (!string.IsNullOrEmpty(Def.key)) GameSettings.Reset(Def.key);
        _page.OnRowChanged(this);
    }

    // ---- animation / survol ------------------------------------------------------------------------------------
    private float _toggleTarget;

    private void PlaceKnob()
    {
        if (_toggleKnob == null) return;
        // piste 132 px, bouton 36 px : course de 4 -> 92
        float x = Mathf.Lerp(4f, 92f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_knobT)));
        _toggleKnob.anchoredPosition = new Vector2(x, 0f);
    }

    private void Update()
    {
        if (Def != null && Def.kind == RowKind.Toggle && !Mathf.Approximately(_knobT, _toggleTarget))
        {
            _knobT = Mathf.MoveTowards(_knobT, _toggleTarget, Time.unscaledDeltaTime * 9f);
            PlaceKnob();
        }
        if (_highlight != null)
        {
            Color c = _highlight.color;
            float target = _hover ? 0.07f : 0f;
            c.a = Mathf.MoveTowards(c.a, target, Time.unscaledDeltaTime * 0.6f);
            _highlight.color = c;
        }
    }

    public void OnPointerEnter(PointerEventData e) => _hover = true;
    public void OnPointerExit(PointerEventData e) => _hover = false;

    // Pendant la capture d'une touche, l'emplacement visé clignote (voir SettingsPage).
    public void SetCaptureVisual(int slot, bool on)
    {
        if (_keyBorders == null) return;
        for (int s = 0; s < _keyBorders.Length; s++)
            _keyBorders[s].color = (on && s == slot) ? SettingsStyle.Gold : SettingsStyle.AccentSoft;
        if (on && slot >= 0 && slot < _keyTexts.Length) _keyTexts[slot].text = "…";
        else Refresh();
    }
}
