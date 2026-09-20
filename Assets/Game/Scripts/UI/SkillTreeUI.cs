using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Contrôleur de l'onglet Compétences : sélection d'un nœud, fiche de détail, achat, or,
// en-tête et légendes des personnages.
//
// REFONTE (2026-09-19) - la logique d'achat (MetaProgressionManager.TryBuyNode) et l'API
// publique (RegisterNode, OnNodeClicked, ClosePanel, OnBuyClicked, RefreshGoldDisplay,
// RefreshAllNodes, OnResetClicked) sont inchangées ; ce qui a changé :
//  - la fiche est un SkillDetailPanel (charte du jeu) et non plus 9 champs TMP éparpillés ;
//  - ouvrir une fiche ATTÉNUE les autres nœuds : le joueur voit d'un coup d'œil de quoi on parle,
//    même si la fiche recouvre une partie de l'arbre ;
//  - un achat se SENT : rebond du médaillon, gerbe d'étincelles, compteur d'or qui décompte ;
//  - l'en-tête dit ce qu'il y a à faire (nombre de compétences achetables) et chaque logo
//    porte la progression de son personnage (x / 11) + "actif" pour celui qui est sélectionné ;
//  - Échap ferme la fiche ; le bouton "Réinitialiser" (debug) n'existe plus hors build de dev.
public class SkillTreeUI : MonoBehaviour
{
    [System.Serializable]
    public class BranchCaption
    {
        public SkillTreeData.CharacterBranch branch;
        public RectTransform root;
        public Image border;
        public TextMeshProUGUI text;
    }

    [Header("Panneau de l'onglet")]
    [Tooltip("UpgradesPanel : sert à détecter l'ouverture / la fermeture de l'onglet.")]
    [SerializeField] private RectTransform _panelRoot;

    [Header("Fiche de détail")]
    [SerializeField] private RectTransform _detailPanel;
    [SerializeField] private SkillDetailPanel _detail;
    [SerializeField] private CanvasGroup _detailGroup;

    [Header("En-tête")]
    [SerializeField] private TextMeshProUGUI _goldText;
    [SerializeField] private RectTransform _goldPill;
    [SerializeField] private TextMeshProUGUI _hintText;
    [SerializeField] private BranchCaption[] _captions;

    [Header("Décor")]
    [SerializeField] private SkillTreeLinks _links;
    [SerializeField] private TreeAmbientFX _fx;

    [Header("Debug (masqué hors build de développement)")]
    [SerializeField] private GameObject _debugResetButton;

    [Header("Fermeture au clic extérieur")]
    [SerializeField] private GameObject _outsideClickCatcher;

    [Header("Animation de la fiche")]
    [SerializeField] private float _panelMoveDuration = 0.18f;

    private string _selectedNodeId = "";
    private bool _isPanelOpen;
    private bool _tabWasVisible;
    private Coroutine _moveCoroutine;
    private Canvas _canvas;
    private RectTransform _canvasRect;
    private float _openTime = -10f;
    private float _goldShown = -1f;      // valeur affichée (décompte animé)
    private int _goldTarget;
    private int _goldTextValue = int.MinValue;
    private float _goldPunchTime = -10f;

    public static SkillTreeUI Instance { get; private set; }

    // Cache local pour remplacer le FindObjectsOfType très gourmand
    private readonly List<SkillNode> _registeredNodes = new List<SkillNode>();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas != null) _canvasRect = _canvas.GetComponent<RectTransform>();

        _detailPanel.gameObject.SetActive(false);

        if (_outsideClickCatcher != null)
        {
            _outsideClickCatcher.SetActive(false);
            Button catcherButton = _outsideClickCatcher.GetComponent<Button>();
            if (catcherButton != null)
            {
                catcherButton.onClick.RemoveAllListeners();
                catcherButton.onClick.AddListener(ClosePanel);
            }
        }

        if (_detail != null)
        {
            if (_detail.BuyButton != null)
            {
                _detail.BuyButton.onClick.RemoveAllListeners();
                _detail.BuyButton.onClick.AddListener(OnBuyClicked);
            }
            if (_detail.CloseButton != null)
            {
                _detail.CloseButton.onClick.RemoveAllListeners();
                _detail.CloseButton.onClick.AddListener(ClosePanel);
            }
        }

        // Le bouton de remise à zéro est un outil de développement : il ne doit jamais
        // atteindre un joueur (Debug.isDebugBuild = vrai dans l'éditeur et les builds de dev).
        if (_debugResetButton != null) _debugResetButton.SetActive(Debug.isDebugBuild);

        RefreshAllNodes();
    }

    // Enregistrement automatique des nœuds (cache)
    public void RegisterNode(SkillNode node)
    {
        if (!_registeredNodes.Contains(node)) _registeredNodes.Add(node);
    }

    public void UnregisterNode(SkillNode node)
    {
        _registeredNodes.Remove(node);
    }

    // ─── Boucle ──────────────────────────────────────────────────────────────

    private void Update()
    {
        // l'onglet vient de s'ouvrir / de se fermer
        bool visible = _panelRoot != null && _panelRoot.gameObject.activeInHierarchy;
        if (visible && !_tabWasVisible)
        {
            _goldShown = -1f;            // pas de décompte au premier affichage
            RefreshAllNodes();
        }
        else if (!visible && _tabWasVisible && _isPanelOpen)
        {
            ClosePanel();
        }
        _tabWasVisible = visible;
        if (!visible) return;

        if (_isPanelOpen && Input.GetKeyDown(KeyCode.Escape)) ClosePanel();

        AnimateGold();
        AnimatePanelIn();
    }

    private void AnimatePanelIn()
    {
        if (!_isPanelOpen || _detailGroup == null) return;
        float k = Mathf.Clamp01((Time.unscaledTime - _openTime) / 0.16f);
        float e = 1f - (1f - k) * (1f - k);
        _detailGroup.alpha = e;
        _detailPanel.localScale = Vector3.one * (0.95f + 0.05f * e);
    }

    private void AnimateGold()
    {
        if (_goldText == null) return;

        if (_goldShown < 0f) _goldShown = _goldTarget;
        else if (!Mathf.Approximately(_goldShown, _goldTarget))
        {
            // décompte : ~0,6 s quel que soit l'écart
            float step = Mathf.Max(1f, Mathf.Abs(_goldTarget - _goldShown) * 6f * Time.unscaledDeltaTime);
            _goldShown = Mathf.MoveTowards(_goldShown, _goldTarget, step);
        }

        int shown = Mathf.RoundToInt(_goldShown);
        if (shown != _goldTextValue)
        {
            _goldTextValue = shown;
            _goldText.text = shown.ToString();
            if (_goldPill != null)
            {
                // la pastille épouse le nombre (pièce à gauche, chiffres à droite)
                float w = Mathf.Max(150f, 62f + _goldText.GetPreferredValues(_goldText.text).x + 26f);
                _goldPill.sizeDelta = new Vector2(w, _goldPill.sizeDelta.y);
            }
        }

        if (_goldPill != null)
        {
            float pt = Time.unscaledTime - _goldPunchTime;
            float punch = pt >= 0f && pt < 0.35f ? Mathf.Sin(pt / 0.35f * Mathf.PI) * 0.07f : 0f;
            _goldPill.localScale = Vector3.one * (1f + punch);
        }
    }

    // ─── Clic sur un médaillon ───────────────────────────────────────────────

    public void OnNodeClicked(string nodeId, RectTransform nodeRect)
    {
        if (_isPanelOpen && _selectedNodeId == nodeId)
        {
            ClosePanel();
            return;
        }

        bool wasOpen = _isPanelOpen;
        _selectedNodeId = nodeId;
        SkillNode node = FindNode(nodeId);

        // La fiche doit être ACTIVE avant d'être remplie : TextMeshPro ne mesure pas un texte
        // sur un objet inactif (les puces prendraient une largeur de 5 px).
        if (!wasOpen)
        {
            _detailPanel.anchorMin = new Vector2(0.5f, 0.5f);
            _detailPanel.anchorMax = new Vector2(0.5f, 0.5f);
            if (_detailGroup != null) _detailGroup.alpha = 0f;
            _detailPanel.gameObject.SetActive(true);
        }

        _detail.Populate(nodeId, node != null ? node.IconSprite : null);
        ApplySelection();

        if (!wasOpen)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_detailPanel);
            _detailPanel.anchoredPosition = ComputeTargetPosition(nodeRect);   // apparaît directement à sa place
            _openTime = Time.unscaledTime;

            if (_outsideClickCatcher != null) _outsideClickCatcher.SetActive(true);
            _isPanelOpen = true;
        }
        else
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_detailPanel);
            _isPanelOpen = true;
            MovePanelTo(ComputeTargetPosition(nodeRect));
        }
    }

    public void ClosePanel()
    {
        if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
        _detailPanel.gameObject.SetActive(false);
        if (_outsideClickCatcher != null) _outsideClickCatcher.SetActive(false);
        _isPanelOpen = false;
        _selectedNodeId = "";
        ApplySelection();
    }

    // Nœud choisi en pleine lumière ; tous les autres (et les liaisons) reculent.
    private void ApplySelection()
    {
        bool any = _isPanelOpen || !string.IsNullOrEmpty(_selectedNodeId);
        foreach (SkillNode n in _registeredNodes)
        {
            if (n == null) continue;
            bool isSel = any && n.NodeId == _selectedNodeId;
            n.SetSelected(isSel);
            n.SetDimmed(any && !isSel);
        }
        if (_links != null) _links.SetDimmed(any);
    }

    private SkillNode FindNode(string nodeId)
    {
        foreach (SkillNode n in _registeredNodes)
            if (n != null && n.NodeId == nodeId) return n;
        return null;
    }

    // ─── Achat ───────────────────────────────────────────────────────────────

    public void OnBuyClicked()
    {
        if (string.IsNullOrEmpty(_selectedNodeId) || MetaProgressionManager.Instance == null) return;

        if (MetaProgressionManager.Instance.TryBuyNode(_selectedNodeId))
        {
            SkillNode node = FindNode(_selectedNodeId);
            SkillTreeData.NodeData data = SkillTreeData.Get(_selectedNodeId);
            if (node != null)
            {
                node.Pop();
                if (_fx != null && data != null)
                {
                    bool maxed = SkillTreeStyle.CurrentLevel(_selectedNodeId) >= SkillTreeStyle.MaxLevel(data);
                    _fx.Burst(node.Rect, maxed ? SkillTreeStyle.Gold : SkillTreeStyle.Accent(data.branch), maxed ? 34 : 22);
                }
            }

            _goldPunchTime = Time.unscaledTime;
            RefreshAllNodes();
            _detail.Populate(_selectedNodeId, node != null ? node.IconSprite : null);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_detailPanel);
        }
    }

    // ─── Affichage ───────────────────────────────────────────────────────────

    public void RefreshGoldDisplay()
    {
        if (MetaProgressionManager.Instance == null || MetaProgressionManager.Instance.Data == null) return;
        _goldTarget = MetaProgressionManager.Instance.Data.totalGold;
    }

    public void RefreshAllNodes()
    {
        for (int i = 0; i < _registeredNodes.Count; i++)
            if (_registeredNodes[i] != null) _registeredNodes[i].RefreshVisual();

        if (_links != null) _links.RefreshStates();
        RefreshGoldDisplay();
        RefreshHeader();
    }

    private void RefreshHeader()
    {
        MetaProgressionManager meta = MetaProgressionManager.Instance;
        if (meta == null || meta.Data == null) return;

        // consigne de l'onglet : ce qu'il y a à faire MAINTENANT
        if (_hintText != null)
        {
            int buyable = 0;
            foreach (SkillNode n in _registeredNodes)
                if (n != null && n.IsBuyableNow) buyable++;

            string gold = SkillTreeStyle.Hex(SkillTreeStyle.Gold);
            string muted = SkillTreeStyle.Hex(SkillTreeStyle.Body);
            _hintText.text = buyable > 0
                ? $"<color=#{gold}>{buyable}</color> compétence{(buyable > 1 ? "s" : "")} achetable{(buyable > 1 ? "s" : "")}  ·  repère le halo doré"
                : $"<color=#{muted}>Clique sur un médaillon pour voir ses niveaux et ses prérequis</color>";

            // la pastille sombre derrière le texte épouse sa longueur
            RectTransform pill = _hintText.transform.parent as RectTransform;
            if (pill != null)
                pill.sizeDelta = new Vector2(_hintText.GetPreferredValues(_hintText.text).x + 56f, pill.sizeDelta.y);
        }

        // légendes sous les logos : progression de chaque personnage, actif mis en avant
        if (_captions != null)
        {
            SkillTreeData.CharacterBranch active = meta.GetActiveBranch();
            foreach (BranchCaption c in _captions)
            {
                if (c == null || c.text == null) continue;
                Color accent = SkillTreeStyle.Accent(c.branch);
                bool isActive = c.branch == active;
                string progress = $"{SkillTreeStyle.BranchProgress(c.branch)} / {SkillTreeStyle.BranchMax(c.branch)}";
                string label = isActive ? "PERSONNAGE ACTIF" : "COMPÉTENCES";
                string labelColor = SkillTreeStyle.Hex(isActive ? accent : SkillTreeStyle.Muted);
                string text = $"<color=#{labelColor}>{label}</color>   <color=#FFFFFF>{progress}</color>";
                c.text.text = text;
                if (c.border != null) c.border.color = isActive ? accent : SkillTreeStyle.EmptyRing;
                if (c.root != null)
                {
                    Vector2 pref = c.text.GetPreferredValues(text);
                    c.root.sizeDelta = new Vector2(pref.x + 60f, c.root.sizeDelta.y);
                }
            }
        }
    }

    // ─── Placement de la fiche ───────────────────────────────────────────────

    // Position (repère du parent de la fiche, relative à son centre) d'un point du monde.
    private Vector2 WorldToParentLocal(RectTransform source)
    {
        RectTransform parent = (RectTransform)_detailPanel.parent;
        Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, source.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, cam, out Vector2 local);
        return local - parent.rect.center;
    }

    private Vector2 ComputeTargetPosition(RectTransform nodeRect)
    {
        RectTransform parent = (RectTransform)_detailPanel.parent;
        Vector2 nodePos = WorldToParentLocal(nodeRect);
        float panelW = _detailPanel.rect.width;
        float panelH = _detailPanel.rect.height;
        float halfW = parent.rect.width * 0.5f;
        float halfH = parent.rect.height * 0.5f;
        float margin = 16f;
        float gap = 122f;                                 // rayon du médaillon (x1,5) + air

        // côté : la fiche s'ouvre vers le centre de l'écran (plus de place, moins de logos recouverts)
        float sign = nodePos.x > 0f ? -1f : 1f;
        float x = nodePos.x + sign * (gap + panelW * 0.5f);
        if (x + panelW * 0.5f > halfW - margin || x - panelW * 0.5f < -halfW + margin)
            x = nodePos.x - sign * (gap + panelW * 0.5f); // pas la place : de l'autre côté
        x = Mathf.Clamp(x, -halfW + panelW * 0.5f + margin, halfW - panelW * 0.5f - margin);

        // la fiche reste sous la consigne quand elle y tient (zone d'en-tête ~60 px)
        float top = halfH - (panelH <= parent.rect.height - 60f - margin ? 60f : 0f);
        float minY = -halfH + panelH * 0.5f + margin;
        float maxY = top - panelH * 0.5f - margin;
        float y = minY <= maxY ? Mathf.Clamp(nodePos.y, minY, maxY) : 0f;
        return new Vector2(x, y);
    }

    private void MovePanelTo(Vector2 target)
    {
        if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
        _moveCoroutine = StartCoroutine(MovePanelRoutine(target));
    }

    private IEnumerator MovePanelRoutine(Vector2 target)
    {
        Vector2 start = _detailPanel.anchoredPosition;
        float t = 0f;
        while (t < _panelMoveDuration)
        {
            t += Time.unscaledDeltaTime;
            float ratio = Mathf.SmoothStep(0f, 1f, t / _panelMoveDuration);
            _detailPanel.anchoredPosition = Vector2.Lerp(start, target, ratio);
            yield return null;
        }
        _detailPanel.anchoredPosition = target;
    }

    // ─── Debug ───────────────────────────────────────────────────────────────

    public void OnResetClicked()
    {
        if (MetaProgressionManager.Instance == null) return;

        MetaProgressionManager.Instance.ResetSkillTree();
        RefreshAllNodes();
        ClosePanel();
    }
}
