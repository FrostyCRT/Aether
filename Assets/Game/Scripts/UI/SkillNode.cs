using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Médaillon d'une compétence dans l'arbre.
//
// REFONTE (2026-09-19) - l'ancien nœud n'avait que deux états visuels (icône assombrie à 45 %
// ou pleine), des pastilles "plates" sur un fond noir et aucune information de progression
// hors des pastilles. Le joueur devait cliquer sur chaque nœud pour savoir où il en était.
// Désormais, sans rien cliquer, un nœud dit :
//  - QUOI : le nom sous le médaillon ;
//  - OÙ J'EN SUIS : pastilles remplies (3 paliers) ou cartouche (talent unique) ;
//  - CE QUE JE PEUX FAIRE : halo doré qui respire = "achetable maintenant" ; halo de la
//    couleur du personnage = en cours ; halo doré fixe + pastilles dorées = maîtrisé ;
//  - CE QUI EST FERMÉ : icône désaturée + cadenas = prérequis manquant (les liaisons
//    entre nœuds, voir SkillTreeLinks, disent lequel).
// Un nœud d'un personnage non sélectionné est un cran plus terne : on peut le consulter,
// mais on ne peut acheter que pour le personnage actif.
//
// Toute la hiérarchie est dans la scène ; ce composant ne fait que la colorer/l'animer.
public class SkillNode : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public enum State { Locked, Available, InProgress, Maxed }

    [Header("Identité")]
    [SerializeField] private string _nodeId;
    [SerializeField] private bool _isUnique;

    [Header("Visuels")]
    [SerializeField] private Image _glow;          // halo additif derrière le médaillon
    [SerializeField] private Image _ringImage;     // cadre orné (Cadre / Cadre Vert / Cadre Bleu)
    [SerializeField] private Image _discImage;     // fond du médaillon
    [SerializeField] private Image _iconImage;
    [SerializeField] private GameObject _lockBadge;
    [SerializeField] private TextMeshProUGUI _nameLabel;
    [SerializeField] private RectTransform _plate;      // cartouche sombre : nom + pastilles (couvre les liaisons qui passent dessous)
    [SerializeField] private Image _plateBorder;
    [SerializeField] private Image _dot1;
    [SerializeField] private Image _dot2;
    [SerializeField] private Image _dot3;
    [SerializeField] private CanvasGroup _group;

    [Header("Matériau de désaturation (Aether/UI/Desaturate)")]
    [SerializeField] private Material _desaturateMaterial;

    [Header("Taille")]
    [Tooltip("Échelle de base du médaillon entier (cadre, nom, pastilles). Les nœuds d'origine étaient jugés trop petits.")]
    [SerializeField] private float _baseScale = 1.5f;

    [Header("Référence")]
    [SerializeField] private SkillTreeUI _skillTreeUI;

    public string NodeId => _nodeId;
    public RectTransform Rect => _rectTransform != null ? _rectTransform : (RectTransform)transform;
    public Sprite IconSprite => _iconImage != null ? _iconImage.sprite : null;
    public State CurrentState { get; private set; } = State.Locked;
    public bool IsBuyableNow { get; private set; }
    public SkillTreeData.CharacterBranch Branch { get; private set; }

    private RectTransform _rectTransform;
    private Button _button;
    private Material _iconMaterial;
    private bool _iconGrey;
    private CanvasRenderer _glowRenderer;

    private Color _accent = Color.white;
    private bool _activeBranch = true;
    private bool _hovered, _selected, _dimmed;
    private float _hover, _dim, _phase;
    private CanvasGroup _plateGroup;
    private float _plateT;                 // 0 = cartouche caché, 1 = affiché (survol / sélection)
    private float _popStart = -10f;
    private float _appearTime;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _button = GetComponent<Button>();
        _phase = Random.Range(0f, 6.28f);

        // Le cartouche (nom + pastilles) n'apparaît qu'au survol ou quand le nœud est sélectionné :
        // 15 cartouches affichés en permanence mangeaient la place des liaisons. Caché, il ne capte
        // plus les clics (blocksRaycasts) : seul le disque du médaillon reste cliquable.
        if (_plate != null)
        {
            _plateGroup = _plate.GetComponent<CanvasGroup>();
            if (_plateGroup == null) _plateGroup = _plate.gameObject.AddComponent<CanvasGroup>();
            _plateGroup.alpha = 0f;
            _plateGroup.blocksRaycasts = false;
            _plateGroup.interactable = false;
        }

        // Zone cliquable : l'Image racine est transparente (alpha 0). Par défaut le CanvasRenderer
        // ne dessine pas (donc ne "raycaste" pas) un maillage transparent : sans ceci, plus aucun
        // clic ne touche le nœud. On la garde invisible mais cliquable.
        Image hit = GetComponent<Image>();
        if (hit != null)
        {
            hit.raycastTarget = true;
            hit.canvasRenderer.cullTransparentMesh = false;
        }

        if (_glow != null)
        {
            // sprite généré en code (TreeSprites) ; en scène l'image est transparente (pas de carré blanc dans l'éditeur)
            _glow.sprite = TreeSprites.Ring;
            _glow.color = Color.white;
            _glow.raycastTarget = false;
            _glowRenderer = _glow.GetComponent<CanvasRenderer>();
            _glowRenderer.SetColor(new Color(1, 1, 1, 0));
        }
        if (_iconImage != null && _desaturateMaterial != null)
        {
            // Matériau "gris" créé UNE fois, saturation fixée AVANT toute affectation, jamais modifié
            // ensuite : sous un Mask, Unity fabrique une copie "stencil" du matériau au premier rendu et
            // ne la met PAS à jour quand on change une propriété du matériau d'origine (le nœud restait
            // gris après achat du palier précédent). On change donc de matériau, pas de propriété.
            _iconMaterial = new Material(_desaturateMaterial);
            _iconMaterial.SetFloat("_Saturation", 0f);
        }

        if (_button == null) { Debug.LogError($"[SkillNode] Pas de Button sur {_nodeId} !"); return; }
        if (_skillTreeUI == null) { Debug.LogError($"[SkillNode] SkillTreeUI non assigné sur {_nodeId} !"); return; }

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(HandleClick);
        _skillTreeUI.RegisterNode(this);

        SkillTreeData.NodeData data = SkillTreeData.Get(_nodeId);
        if (data != null)
        {
            Branch = data.branch;
            _accent = SkillTreeStyle.Accent(data.branch);
            if (_nameLabel != null) _nameLabel.text = data.displayName;
        }
    }

    // Le cartouche épouse la longueur du nom. En Start (pas Awake) : TextMeshPro n'a pas encore
    // initialisé le texte des enfants quand Awake du parent s'exécute.
    private void Start()
    {
        if (_plate == null || _nameLabel == null) return;
        float w = _nameLabel.GetPreferredValues(_nameLabel.text).x + 24f;
        _plate.sizeDelta = new Vector2(Mathf.Max(_isUnique ? 72f : 80f, w), _plate.sizeDelta.y);
    }

    private void OnDestroy()
    {
        if (_skillTreeUI != null) _skillTreeUI.UnregisterNode(this);
        if (_iconMaterial != null) Destroy(_iconMaterial);
    }

    private void OnEnable()
    {
        _appearTime = Time.unscaledTime;
        _hovered = false;
        _hover = 0f;
        _plateT = 0f;
        RefreshVisual();
    }

    private void HandleClick()
    {
        if (_skillTreeUI != null) _skillTreeUI.OnNodeClicked(_nodeId, _rectTransform);
    }

    public void OnPointerEnter(PointerEventData e) => _hovered = true;
    public void OnPointerExit(PointerEventData e) => _hovered = false;

    // --- API appelée par SkillTreeUI ------------------------------------

    public void SetSelected(bool selected) => _selected = selected;
    public void SetDimmed(bool dimmed) => _dimmed = dimmed;
    public void Pop() => _popStart = Time.unscaledTime;

    public void RefreshVisual()
    {
        MetaProgressionManager meta = MetaProgressionManager.Instance;
        SkillTreeData.NodeData data = SkillTreeData.Get(_nodeId);
        if (meta == null || data == null || _button == null) return;

        int max = SkillTreeStyle.MaxLevel(data);
        int level = SkillTreeStyle.CurrentLevel(_nodeId);
        bool unlockable = meta.IsNodeUnlockable(_nodeId);

        _activeBranch = data.branch == meta.GetActiveBranch();
        CurrentState = level >= max ? State.Maxed
                     : level > 0 ? State.InProgress
                     : unlockable ? State.Available
                     : State.Locked;

        int cost = meta.GetNodeCost(_nodeId);
        IsBuyableNow = _activeBranch && unlockable && cost > 0 && meta.Data.totalGold >= cost;

        ApplyColors(level, max);
        _button.interactable = true;
    }

    private void ApplyColors(int level, int max)
    {
        bool locked = CurrentState == State.Locked;
        bool maxed = CurrentState == State.Maxed;
        // Un nœud n'est gris que s'il est VERROUILLÉ (prérequis manquant). Le personnage sélectionné
        // ne change que ce qu'on peut ACHETER (halo, fiche) : un nœud d'un autre personnage garde ses
        // couleurs, et ce qui est acquis reste en couleur quel que soit le personnage.
        if (_iconImage != null)
        {
            if (_iconMaterial != null && _iconGrey != locked)
            {
                _iconImage.material = locked ? _iconMaterial : null;
                _iconGrey = locked;
            }
            float b = locked ? 0.66f : 1f;
            _iconImage.color = new Color(b, b, b * (locked ? 1.1f : 1f), locked ? 0.92f : 1f);
        }

        if (_ringImage != null)
        {
            float r = locked ? 0.5f : 1f;
            _ringImage.color = new Color(r, r, r * (locked ? 1.1f : 1f), 1f);
        }

        if (_discImage != null)
        {
            Color d = SkillTreeStyle.Disc;
            d.a = locked ? 0.9f : 0.94f;
            _discImage.color = d;
        }

        if (_lockBadge != null) _lockBadge.SetActive(locked);

        if (_nameLabel != null)
        {
            _nameLabel.color = locked ? new Color(0.78f, 0.76f, 0.85f, 0.92f) : Color.white;
        }

        // pastilles : couleur du perso, dorées quand le nœud est maîtrisé
        Color fill = maxed ? SkillTreeStyle.Gold : _accent;
        if (_isUnique)
        {
            if (_dot2 != null) _dot2.gameObject.SetActive(false);
            if (_dot3 != null) _dot3.gameObject.SetActive(false);
            SetPip(_dot1, level >= 1, fill);
        }
        else
        {
            if (_dot2 != null) _dot2.gameObject.SetActive(true);
            if (_dot3 != null) _dot3.gameObject.SetActive(true);
            SetPip(_dot1, level >= 1, fill);
            SetPip(_dot2, level >= 2, fill);
            SetPip(_dot3, level >= 3, fill);
        }
        // liseré du cartouche : éteint (verrouillé) / couleur du perso (acquis) / or (maîtrisé)
        if (_plateBorder != null)
        {
            Color b = locked ? SkillTreeStyle.EmptyRing : (maxed ? SkillTreeStyle.Gold : _accent);
            b.a = locked ? 0.9f : 0.85f;
            _plateBorder.color = b;
        }
    }

    private static void SetPip(Image pip, bool on, Color fill)
    {
        if (pip == null) return;
        pip.color = on ? fill : SkillTreeStyle.EmptyPip;
    }

    // --- animation --------------------------------------------------------

    private void Update()
    {
        float t = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;

        _hover = Mathf.MoveTowards(_hover, (_hovered || _selected) ? 1f : 0f, dt * 7f);
        _dim = Mathf.MoveTowards(_dim, _dimmed ? 1f : 0f, dt * 6f);

        // pop d'achat : petit rebond du médaillon + flash du halo
        float pop = 0f, flash = 0f;
        float pt = t - _popStart;
        if (pt >= 0f && pt < 0.7f)
        {
            float k = pt / 0.7f;
            pop = Mathf.Sin(k * Mathf.PI) * 0.16f * (1f - k * 0.4f);
            flash = 1f - k;
        }

        // apparition à l'ouverture de l'onglet : le médaillon "se pose" avec un léger décalage par nœud
        float appear = Mathf.SmoothStep(0f, 1f, (t - _appearTime - (_phase * 0.03f)) / 0.35f);

        float s = _baseScale * (0.94f + 0.06f * appear) * (1f + 0.06f * _hover + pop);
        transform.localScale = new Vector3(s, s, 1f);

        if (_group != null) _group.alpha = appear * Mathf.Lerp(1f, 0.38f, _dim * (_selected ? 0f : 1f));

        UpdateGlow(t, flash);
        UpdatePlate(dt);
    }

    private void UpdatePlate(float dt)
    {
        if (_plateGroup == null) return;
        _plateT = Mathf.MoveTowards(_plateT, (_hovered || _selected) ? 1f : 0f, dt * 9f);
        float e = _plateT * _plateT * (3f - 2f * _plateT);
        _plateGroup.alpha = e;
        _plateGroup.blocksRaycasts = _plateT > 0.5f;
        _plate.localScale = Vector3.one * (0.88f + 0.12f * e);   // légère "sortie" du cartouche
    }

    private void UpdateGlow(float t, float flash)
    {
        if (_glowRenderer == null) return;

        float a; Color c;
        switch (CurrentState)
        {
            case State.Maxed:
                c = SkillTreeStyle.Gold;
                a = 0.55f + 0.10f * Mathf.Sin(t * 0.9f + _phase);
                break;
            case State.InProgress:
                // en cours ET palier suivant achetable : même signal doré qu'un nœud neuf achetable
                c = IsBuyableNow ? SkillTreeStyle.Gold : _accent;
                a = IsBuyableNow ? 0.78f + 0.22f * Mathf.Sin(t * 2.4f + _phase)
                                 : 0.48f + 0.10f * Mathf.Sin(t * 0.9f + _phase);
                break;
            case State.Available:
                c = IsBuyableNow ? SkillTreeStyle.Gold : _accent;
                a = IsBuyableNow ? 0.78f + 0.22f * Mathf.Sin(t * 2.4f + _phase) : (_activeBranch ? 0.22f : 0f);
                break;
            default:
                c = _accent;
                a = 0f;
                break;
        }


        // survol/sélection : le halo s'ouvre et se réchauffe
        c = Color.Lerp(c, Color.Lerp(c, Color.white, 0.4f), _hover);
        a += 0.30f * _hover + 0.9f * flash;

        float size = 200f * (1f + 0.12f * _hover + 0.6f * flash);
        _glow.rectTransform.sizeDelta = new Vector2(size, size);
        _glowRenderer.SetColor(new Color(c.r, c.g, c.b, Mathf.Clamp01(a)));
    }
}
