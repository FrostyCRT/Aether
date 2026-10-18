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
        ResetFx();
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
            // CORRIGÉ (2026-10-18, retour utilisateur, capture à l'appui : "les cadres ... sont toujours dorés") -
            // le halo gardait l'or même après le passage de la valeur/des paliers à la couleur de la stat ; il
            // suit maintenant _accent comme le reste de la carte, plus aucune bascule vers l'or sur cette carte.
            float g = _maxed ? 0.30f : 0.20f;
            _glow.color = new Color(_accent.r, _accent.g, _accent.b,
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
        // CORRIGÉ (2026-10-18) - même changement que le halo ci-dessus : le cadre garde la couleur de la stat
        // même maîtrisée, il ne bascule plus en or.
        Color c = _accent;
        float a = _maxed ? 0.85f : (_affordable ? 0.9f : 0.55f);
        a = Mathf.Lerp(a, 1f, hover);
        return new Color(c.r, c.g, c.b, a);
    }

    // ---- affichage --------------------------------------------------------------------------------------------
    public void Refresh()
    {
        // AJOUTÉ (2026-10-18, retour utilisateur, captures à l'appui : "ça reprend les couleurs qu'il y avait avant
        // qu'on règle le problème des couleurs [...] c'est juste un bug d'affichage") - _accent n'était calculé QUE
        // dans Awake(). Quand toute la page Réputation est réactivée d'un coup (SetActive(true) au retour dans le
        // menu après une partie), Unity NE GARANTIT PAS que l'Awake() de CETTE carte tourne avant l'OnEnable() du
        // parent (ReputationUI), qui appelle Refresh() sur toutes les cartes immédiatement - piège classique
        // d'activation de hiérarchie. Si Refresh() tombe sur cette fenêtre, _accent valait encore default(Color)
        // (0,0,0,0, transparent) : tout ce qui utilise _accent (texte de valeur, pastilles REMPLIES) devenait
        // invisible, alors que tout ce qui utilise une couleur fixe (pastilles VIDES, EmptyPip) restait visible -
        // exactement le symptôme des captures (nombre de pastilles visibles = 5 - niveau, soit uniquement les vides).
        // Recalculé ici à chaque appel : plus aucune dépendance à l'ordre Awake/OnEnable entre composants.
        _accent = ReputationStyle.StatAccent(_nodeId);
        // Titre réécrit à chaque rafraîchissement (et pas seulement à l'Awake) : suit les changements de langue.
        if (_name != null) _name.text = ReputationStyle.StatName(_nodeId);
        MetaProgressionManager meta = MetaProgressionManager.Instance;
        if (meta == null || meta.Data == null) return;

        int max = MetaProgressionManager.MaxReputationLevel;
        int level = Mathf.Clamp(meta.GetNodeLevel(_nodeId), 0, max);
        int cost = meta.GetNodeCost(_nodeId);
        _maxed = cost == -1 || level >= max;
        _affordable = !_maxed && meta.TotalEclats >= cost;
        // CORRIGÉ (2026-10-18, retour utilisateur : "beaucoup de jaune doré dans cette page, même un peu trop ...
        // ça vient surtout du fait que quand on améliore quelque chose au max, c'est doré, mais du coup si tout
        // est au max ça fait ça") - l'or ne bascule plus la valeur/les paliers/la barre d'accent : ils gardent la
        // couleur propre de la stat (orange/bleu/vert) même maîtrisée. L'or reste réservé au cadre + halo (voir
        // FrameColor et Update), seul signal de "palier maximum" sur la carte.
        Color accent = _accent;

        float now = meta.GetReputationValueAt(_nodeId, level);
        if (_value != null)
        {
            _value.text = ReputationStyle.FormatBonus(_nodeId, now);
            _value.color = level == 0 ? ReputationStyle.Muted : accent;
        }
        if (_level != null)
        {
            _level.text = _maxed ? Loc.T("NIVEAU MAXIMUM") : Loc.F("NIVEAU {0} / {1}", level, max);
            _level.color = _maxed ? ReputationStyle.Body : ReputationStyle.Muted;
        }

        if (_next != null)
        {
            // CORRIGÉ (2026-10-18, retour utilisateur, capture à l'appui : "les textes prochain niveau +x% qui sont
            // noir, donc on ne les voit pas") - le libellé "PROCHAIN NIVEAU" n'a jamais de balise <color>, donc il
            // s'affichait dans la couleur de base du composant TMP (jamais définie en code, restée au noir par
            // défaut) - invisible sur le fond sombre de la carte. _next.color fixé explicitement, même gris/blanc
            // que _level (ReputationStyle.Muted) juste au-dessus ; seule la valeur chiffrée garde sa couleur accent.
            _next.color = ReputationStyle.Muted;
            if (_maxed) _next.text = "<color=#" + ReputationStyle.Hex(ReputationStyle.Body) + ">" + Loc.T("PALIER MAXIMUM ATTEINT") + "</color>";
            else
            {
                float nextValue = meta.GetReputationValueAt(_nodeId, level + 1);
                string gain = ReputationStyle.FormatBonus(_nodeId, nextValue);
                _next.text = Loc.T("PROCHAIN NIVEAU") + "   <color=#" + ReputationStyle.Hex(_accent) + ">" + gain + "</color>";
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
                _buyLabel.text = _maxed ? "MAX" : (_affordable ? Loc.T("AMÉLIORER") : Loc.T("ÉCLATS INSUFFISANTS"));
                _buyLabel.rectTransform.offsetMax = new Vector2(_maxed ? -16f : -150f, 0f);
                // CORRIGÉ (2026-10-18, retour utilisateur, capture à l'appui : "le texte Max on le voit à peine") -
                // l'ancien brun (0x6B551E) se fondait dans le bouton teinté gris-brun (0.62, 0.56, 0.54) à 70%
                // d'opacité. Passé en blanc-crème neutre (Body) : l'or reste réservé au cadre/halo de la carte,
                // voir le commentaire plus haut sur "trop de doré".
                _buyLabel.color = _affordable ? ReputationStyle.ButtonText : (_maxed ? ReputationStyle.Body : new Color32(0x8A, 0x2E, 0x24, 0xFF));
            }
        }
        _shownLevel = level;
    }

    // ---- retour d'achat -----------------------------------------------------------------------------------------
    private void StartFx(int pipIndex)
    {
        if (_fx != null) StopCoroutine(_fx);
        // CORRIGE (2026-09-26) - en spammant l'achat, l'animation précédente était coupée en plein « pop » : son palier
        // restait agrandi pour toujours (les barres se collaient en un seul bloc). On remet TOUT à l'état normal avant
        // de relancer l'animation.
        ResetFx();
        _fx = StartCoroutine(UpgradeFx(pipIndex));
    }

    private void ResetFx()
    {
        if (_pips != null)
            foreach (Image pip in _pips)
                if (pip != null) pip.rectTransform.localScale = Vector3.one;
        if (_value != null) _value.rectTransform.localScale = Vector3.one;
        if (_flash != null) _flash.color = new Color(1f, 1f, 1f, 0f);
    }

    private void OnDisable()
    {
        // La page peut être fermée en plein « pop » (les coroutines s'arrêtent avec l'objet) : rien ne doit rester agrandi.
        if (_fx != null) { StopCoroutine(_fx); _fx = null; }
        ResetFx();
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
