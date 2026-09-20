using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// AJOUTE (2026-09-20) - une carte de skin dans les rangées « Classiques » (or) et « Prestige » (éclats) de
// l'onglet Réputation. Trois formes : skin réel (aperçu, nom, prix / POSSÉDÉ / ÉQUIPÉ), emplacement « à venir »
// (silhouette + BIENTÔT : les skins ne sont pas encore fabriqués), et l'état sélectionné / survolé.
// Ne construit rien : la hiérarchie du modèle vient de la scène ; ReputationUI en fait des copies.
public class SkinCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private RectTransform _body;
    [SerializeField] private Image _frame;
    [SerializeField] private Image _fill;
    [SerializeField] private Image _selectGlow;
    [SerializeField] private RectTransform _thumbMask;
    [SerializeField] private Image _thumb;
    [SerializeField] private Image _thumbGlow;            // halo de la gamme derrière la figurine
    [SerializeField] private Image _thumbFade;            // fondu bas de la figurine vers le fond de la carte
    [SerializeField] private Image _thumbShade;           // assombrit l'aperçu (skin verrouillé / à venir)
    [SerializeField] private TextMeshProUGUI _name;
    [SerializeField] private GameObject _priceRoot;
    [SerializeField] private Image _priceIcon;
    [SerializeField] private TextMeshProUGUI _priceText;
    [SerializeField] private Image _chip;
    [SerializeField] private TextMeshProUGUI _chipText;
    [SerializeField] private GameObject _placeholderRoot;
    [SerializeField] private TextMeshProUGUI _placeholderText;

    public SkinEntry Skin { get; private set; }
    public bool IsPlaceholder { get; private set; }
    public event Action<SkinCardUI> Clicked;

    private SkinTier _tier;
    private bool _selected;
    private bool _hover;
    private float _hoverT;
    private float _selT;
    private bool _dim;
    private Material _defaultThumbMaterial;

    private void Awake()
    {
        if (_thumb != null) _defaultThumbMaterial = _thumb.material;
        if (_thumbFade != null) _thumbFade.sprite = ReputationStyle.BottomFade;
    }

    public void OnPointerEnter(PointerEventData e) { if (!IsPlaceholder) _hover = true; }
    public void OnPointerExit(PointerEventData e) => _hover = false;
    public void OnPointerClick(PointerEventData e) { if (!IsPlaceholder) Clicked?.Invoke(this); }

    // ---- remplissage ---------------------------------------------------------------------------------------------
    public void BindPlaceholder(SkinTier tier)
    {
        Skin = null; IsPlaceholder = true; _tier = tier; _selected = false; _dim = true;
        if (_placeholderRoot != null) _placeholderRoot.SetActive(true);
        if (_thumbMask != null) _thumbMask.gameObject.SetActive(false);
        if (_thumbGlow != null) _thumbGlow.gameObject.SetActive(false);
        if (_name != null) _name.gameObject.SetActive(false);
        if (_priceRoot != null) _priceRoot.SetActive(false);
        if (_chip != null) _chip.gameObject.SetActive(false);
        if (_placeholderText != null) _placeholderText.text = "BIENTÔT";
        ApplyColors();
    }

    // portrait : image d'aperçu à utiliser si le skin n'en a pas ; goldIcon / eclatIcon : icône de la monnaie du prix.
    public void Bind(SkinEntry skin, Sprite portrait, Sprite goldIcon, Sprite eclatIcon, bool owned, bool equipped, bool characterLocked, Material lockedMaterial)
    {
        Skin = skin; IsPlaceholder = false; _tier = skin.tier; _dim = characterLocked;
        if (_placeholderRoot != null) _placeholderRoot.SetActive(false);
        if (_thumbMask != null) _thumbMask.gameObject.SetActive(true);
        if (_thumbGlow != null) _thumbGlow.gameObject.SetActive(true);
        if (_thumb != null)
        {
            _thumb.sprite = skin.preview != null ? skin.preview : portrait;
            _thumb.material = characterLocked && lockedMaterial != null ? lockedMaterial : _defaultThumbMaterial;
            _thumb.color = Color.white;
            if (_thumbMask != null) ReputationStyle.FitPortrait(_thumb, _thumbMask.rect.width * 1.55f);   // buste : tête entière, coupé au fondu sous les épaules
        }
        if (_thumbShade != null) _thumbShade.color = new Color(0f, 0f, 0f, characterLocked ? 0.35f : 0f);

        if (_name != null) { _name.gameObject.SetActive(true); _name.text = skin.displayName.ToUpperInvariant(); }

        // pastille d'état / prix
        bool showPrice = !owned && !characterLocked;
        if (_priceRoot != null) _priceRoot.SetActive(showPrice);
        if (showPrice)
        {
            if (_priceIcon != null) _priceIcon.sprite = skin.tier == SkinTier.Prestige ? eclatIcon : goldIcon;
            if (_priceText != null) { _priceText.text = skin.cost.ToString(); _priceText.color = Color.white; }
        }
        bool showChip = owned || characterLocked;
        if (_chip != null) _chip.gameObject.SetActive(showChip);
        if (showChip && _chip != null && _chipText != null)
        {
            if (characterLocked)
            {
                _chip.color = new Color(1f, 1f, 1f, 0.1f);
                _chipText.text = "VERROUILLÉ"; _chipText.color = ReputationStyle.Muted;
            }
            else if (equipped)
            {
                _chip.color = ReputationStyle.Gold;
                _chipText.text = "ÉQUIPÉ"; _chipText.color = ReputationStyle.ButtonText;
            }
            else
            {
                _chip.color = new Color(1f, 1f, 1f, 0.14f);
                _chipText.text = "POSSÉDÉ"; _chipText.color = ReputationStyle.Body;
            }
        }
        ApplyColors();
    }

    // Recolore le prix en rouge quand la monnaie manque (appelé par ReputationUI à chaque rafraîchissement).
    public void SetAffordable(bool affordable)
    {
        if (_priceText != null && !IsPlaceholder) _priceText.color = affordable ? Color.white : ReputationStyle.Warn;
    }

    public void SetSelected(bool selected) { _selected = selected; }

    private void ApplyColors()
    {
        Color tier = ReputationStyle.TierColor(_tier);
        if (_thumbFade != null && _fill != null) _thumbFade.color = new Color(0.09f, 0.075f, 0.13f, 1f);
        if (_fill != null) _fill.color = IsPlaceholder ? new Color(0.055f, 0.048f, 0.085f, 0.97f) : new Color(0.09f, 0.075f, 0.13f, 0.98f);
        if (_placeholderText != null) _placeholderText.color = new Color(tier.r, tier.g, tier.b, 0.55f);
        if (_thumbGlow != null) _thumbGlow.color = new Color(tier.r, tier.g, tier.b, _dim ? 0.10f : 0.26f);
        if (_priceText != null && IsPlaceholder) _priceText.color = Color.white;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        _hoverT = Mathf.MoveTowards(_hoverT, _hover ? 1f : 0f, dt * 7f);
        _selT = Mathf.MoveTowards(_selT, _selected ? 1f : 0f, dt * 8f);
        float h = _hoverT * _hoverT * (3f - 2f * _hoverT);
        float s = _selT * _selT * (3f - 2f * _selT);

        if (_body != null) _body.localScale = Vector3.one * (1f + 0.025f * h + 0.012f * s);

        Color tier = ReputationStyle.TierColor(_tier);
        if (_frame != null)
        {
            float a = IsPlaceholder ? 0.28f : Mathf.Lerp(0.5f, 1f, Mathf.Max(h, s));
            if (_dim && !IsPlaceholder) a *= 0.6f;
            _frame.color = new Color(tier.r, tier.g, tier.b, a);
        }
        if (_selectGlow != null)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.6f);
            _selectGlow.color = new Color(tier.r, tier.g, tier.b, s * (0.22f + 0.1f * pulse));
        }
    }
}
