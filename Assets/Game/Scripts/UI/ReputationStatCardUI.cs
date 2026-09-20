using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// AJOUTE (2026-09-20) - une carte de bonus de Réputation (Dégâts / Vitesse / Régénération) : icône, bonus actuel,
// 5 paliers, bonus du PROCHAIN palier, coût en Éclats et bouton d'achat. Ne construit rien : la hiérarchie vient de
// la scène (Aether > Rebuild Reputation Page). Lit tout dans MetaProgressionManager (aucun tableau dupliqué ici).
//
// États : à améliorer (bouton parchemin qui respire) / Éclats insuffisants (bouton grisé, coût en rouge) /
// palier maximum (cadre et paliers dorés). L'achat fait éclater un flash sur la carte, "pop" le palier gagné et
// fait rebondir le bonus. Tout tourne en temps réel (menus).
public class ReputationStatCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Identité")]
    [Tooltip("Identifiant MetaProgressionManager : reputationDamage / reputationSpeed / reputationRegen")]
    [SerializeField] private string _nodeId;

    [Header("Cadre")]
    [SerializeField] private RectTransform _body;         // monte légèrement au survol
    [SerializeField] private Image _frame;
    [SerializeField] private Image _accentBar;
    [SerializeField] private Image _glow;                 // halo additif derrière l'icône
    [SerializeField] private Image _flash;                // éclair additif de l'achat
    [SerializeField] private RectTransform _icon;

    [Header("Textes")]
    [SerializeField] private TextMeshProUGUI _name;
    [SerializeField] private TextMeshProUGUI _value;
    [SerializeField] private TextMeshProUGUI _level;
    [SerializeField] private TextMeshProUGUI _next;

    [Header("Paliers")]
    [SerializeField] private Image[] _pips;

    [Header("Achat")]
    [SerializeField] private RectTransform _costRow;
    [SerializeField] private TextMeshProUGUI _costText;
    [SerializeField] private Button _buy;
    [SerializeField] private Image _buyImage;
    [SerializeField] private TextMeshProUGUI _buyLabel;
    [SerializeField] private Image _buyGlow;

    public string NodeId => _nodeId;
    public event Action<ReputationStatCardUI> BuyClicked;

    private Color _accent;
    private bool _hover;
    private float _hoverT;
    private float _phase;
    private Vector2 _iconBase;
    private int _shownLevel = -1;
    private bool _affordable;
    private bool _maxed;
    private Coroutine _fx;

    private void Awake()
    {
        _accent = ReputationStyle.StatAccent(_nodeId);
        _phase = UnityEngine.Random.Range(0f, 6.28f);
        if (_icon != null) _iconBase = _icon.anchoredPosition;
        if (_buy != null) _buy.onClick.AddListener(() => BuyClicked?.Invoke(this));
        if (_flash != null) _flash.color = new Color(1f, 1f, 1f, 0f);
        if (_name != null) _name.text = ReputationStyle.StatName(_nodeId);
    }

    private void OnEnable()
    {
        _hover = false; _hoverT = 0f;
        if (_body != null) _body.localScale = Vector3.one;
        _shownLevel = -1;          // pas de "pop" au premier affichage
    }

    public void OnPointerEnter(PointerEventData e) => _hover = true;
    public void OnPointerExit(PointerEventData e) => _hover = false;

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        _hoverT = Mathf.MoveTowards(_hoverT, _hover ? 1f : 0f, dt * 6f);
        float k = _hoverT * _hoverT * (3f - 2f * _hoverT);
        if (_body != null) _body.localScale = Vector3.one * (1f + 0.018f * k);
        if (_frame != null) _frame.color = FrameColor(k);

        float t = Time.unscaledTime;
        if (_icon != null) _icon.anchoredPosition = _iconBase + new Vector2(0f, Mathf.Sin(t * 1.1f + _phase) * 5f);
        if (_glow != null)
        {
            float g = _maxed ? 0.30f : 0.20f;
            _glow.color = new Color(_maxed ? ReputationStyle.Gold.r : _accent.r, _maxed ? ReputationStyle.Gold.g : _accent.g, _maxed ? ReputationStyle.Gold.b : _accent.b,
                                    (g + 0.12f * Mathf.Sin(t * 1.6f + _phase)) * (0.85f + 0.3f * k));
        }
        if (_buyGlow != null)
        {
            float a = _affordable ? 0.22f + 0.16f * Mathf.Sin(t * 3.2f + _phase) : 0f;
            _buyGlow.color = new Color(1f, 0.86f, 0.5f, Mathf.Max(0f, a));
        }
    }

    private Color FrameColor(float hover)
    {
        Color c = _maxed ? ReputationStyle.Gold : _accent;
        float a = _maxed ? 0.85f : (_affordable ? 0.9f : 0.55f);
        a = Mathf.Lerp(a, 1f, hover);
        return new Color(c.r, c.g, c.b, a);
    }

    // ---- affichage --------------------------------------------------------------------------------------------
    public void Refresh()
    {
        MetaProgressionManager meta = MetaProgressionManager.Instance;
        if (meta == null || meta.Data == null) return;

        int max = MetaProgressionManager.MaxReputationLevel;
        int level = Mathf.Clamp(meta.GetNodeLevel(_nodeId), 0, max);
        int cost = meta.GetNodeCost(_nodeId);
        _maxed = cost == -1 || level >= max;
        _affordable = !_maxed && meta.TotalEclats >= cost;
        Color accent = _maxed ? ReputationStyle.Gold : _accent;

        float now = meta.GetReputationValueAt(_nodeId, level);
        if (_value != null)
        {
            _value.text = ReputationStyle.FormatBonus(_nodeId, now);
            _value.color = level == 0 ? ReputationStyle.Muted : accent;
        }
        if (_level != null)
        {
            _level.text = _maxed ? "NIVEAU MAXIMUM" : "NIVEAU " + level + " / " + max;
            _level.color = _maxed ? ReputationStyle.Gold : ReputationStyle.Muted;
        }

        if (_next != null)
        {
            if (_maxed) _next.text = "<color=#" + ReputationStyle.Hex(ReputationStyle.Gold) + ">PALIER MAXIMUM ATTEINT</color>";
            else
            {
                float nextValue = meta.GetReputationValueAt(_nodeId, level + 1);
                string gain = ReputationStyle.FormatBonus(_nodeId, nextValue);
                _next.text = "PROCHAIN NIVEAU   <color=#" + ReputationStyle.Hex(_accent) + ">" + gain + "</color>";
            }
        }

        if (_pips != null)
        {
            for (int i = 0; i < _pips.Length; i++)
            {
                if (_pips[i] == null) continue;
                bool filled = i < level;
                _pips[i].color = filled ? accent : ReputationStyle.EmptyPip;
                if (_shownLevel >= 0 && i == level - 1 && level > _shownLevel) StartFx(i);   // le palier tout juste gagné
            }
        }
        if (_accentBar != null) _accentBar.color = new Color(accent.r, accent.g, accent.b, 1f);

        if (_costRow != null) _costRow.gameObject.SetActive(!_maxed);
        if (_costText != null)
        {
            _costText.text = cost.ToString();
            _costText.color = _affordable ? Color.white : ReputationStyle.Warn;
        }

        if (_buy != null)
        {
            _buy.interactable = _affordable;
            if (_buyImage != null) _buyImage.color = _affordable ? Color.white : new Color(0.62f, 0.56f, 0.54f, _maxed ? 0.7f : 1f);
            if (_buyLabel != null)
            {
                _buyLabel.text = _maxed ? "MAX" : (_affordable ? "AMÉLIORER" : "ÉCLATS INSUFFISANTS");
                _buyLabel.rectTransform.offsetMax = new Vector2(_maxed ? -16f : -150f, 0f);
                _buyLabel.color = _affordable ? ReputationStyle.ButtonText : (_maxed ? new Color32(0x6B, 0x55, 0x1E, 0xFF) : new Color32(0x8A, 0x2E, 0x24, 0xFF));
            }
        }
        _shownLevel = level;
    }

    // ---- retour d'achat -----------------------------------------------------------------------------------------
    private void StartFx(int pipIndex)
    {
        if (_fx != null) StopCoroutine(_fx);
        _fx = StartCoroutine(UpgradeFx(pipIndex));
    }

    private IEnumerator UpgradeFx(int pipIndex)
    {
        Image pip = _pips != null && pipIndex < _pips.Length ? _pips[pipIndex] : null;
        float t = 0f;
        while (t < 0.55f)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / 0.55f);
            if (_flash != null) _flash.color = new Color(1f, 0.95f, 0.8f, 0.5f * (1f - u) * (1f - u));
            if (pip != null)
            {
                float pu = Mathf.Clamp01(t / 0.38f);
                float s = 1f + 0.9f * (1f - pu) * (1f - pu) + 0.12f * Mathf.Sin(pu * Mathf.PI);   // gros puis retombe en rebondissant
                pip.rectTransform.localScale = new Vector3(s, s, 1f);
            }
            if (_value != null)
            {
                float vu = Mathf.Clamp01(t / 0.42f);
                float vs = 1f + 0.28f * Mathf.Sin(vu * Mathf.PI);
                _value.rectTransform.localScale = new Vector3(vs, vs, 1f);
            }
            yield return null;
        }
        if (_flash != null) _flash.color = new Color(1f, 1f, 1f, 0f);
        if (pip != null) pip.rectTransform.localScale = Vector3.one;
        if (_value != null) _value.rectTransform.localScale = Vector3.one;
        _fx = null;
    }
}
