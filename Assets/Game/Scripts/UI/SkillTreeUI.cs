using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillTreeUI : MonoBehaviour
{
    [Header("Panel de détail")]
    [SerializeField] private RectTransform _detailPanel;
    [SerializeField] private TextMeshProUGUI _detailTitle;
    [SerializeField] private TextMeshProUGUI _detailDescription;
    [SerializeField] private TextMeshProUGUI _level1Text;
    [SerializeField] private TextMeshProUGUI _level2Text;
    [SerializeField] private TextMeshProUGUI _level3Text;
    [SerializeField] private TextMeshProUGUI _costText;
    [SerializeField] private TextMeshProUGUI _playerGoldText;
    [SerializeField] private Button _buyButton;
    [SerializeField] private TextMeshProUGUI _buyButtonText;

    [Header("Gold affiché en haut à droite")]
    [SerializeField] private TextMeshProUGUI _goldText;

    [Header("Fermeture au clic extérieur")]
    [SerializeField] private GameObject _outsideClickCatcher;

    [Header("Animation du panel")]
    [SerializeField] private float _panelMoveDuration = 0.18f;

    private string _selectedNodeId = "";
    private bool _isPanelOpen = false;
    private Coroutine _moveCoroutine;
    private Canvas _canvas;
    private RectTransform _canvasRect;
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
        if (_canvas != null)
        {
            _canvasRect = _canvas.GetComponent<RectTransform>();
        }

        _detailPanel.gameObject.SetActive(false);

        // Liaison sécurisée des boutons par code
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

        if (_buyButton != null)
        {
            _buyButton.onClick.RemoveAllListeners();
            _buyButton.onClick.AddListener(OnBuyClicked);
        }

        RefreshGoldDisplay();
    }

    // Gestion du cache d'enregistrement automatique des nœuds de compétences
    public void RegisterNode(SkillNode node)
    {
        if (!_registeredNodes.Contains(node)) _registeredNodes.Add(node);
    }

    public void UnregisterNode(SkillNode node)
    {
        if (_registeredNodes.Contains(node)) _registeredNodes.Remove(node);
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
        PopulateDetail(nodeId);

        Vector2 targetPos = ComputeTargetPosition(nodeRect);

        if (!wasOpen)
        {
            _detailPanel.anchorMin = new Vector2(0.5f, 0.5f);
            _detailPanel.anchorMax = new Vector2(0.5f, 0.5f);
            _detailPanel.anchoredPosition = WorldToPanelLocalPosition(nodeRect);
            _detailPanel.gameObject.SetActive(true);

            if (_outsideClickCatcher != null)
                _outsideClickCatcher.SetActive(true);
        }

        _isPanelOpen = true;
        MovePanelTo(targetPos);
    }

    public void ClosePanel()
    {
        if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
        _detailPanel.gameObject.SetActive(false);
        if (_outsideClickCatcher != null) _outsideClickCatcher.SetActive(false);
        _isPanelOpen = false;
        _selectedNodeId = "";
    }

    // ─── Remplissage du panel de détail ─────────────────────────────────────

    private void PopulateDetail(string nodeId)
    {
        if (MetaProgressionManager.Instance == null) return;

        SkillTreeData.NodeData data = SkillTreeData.Get(nodeId);
        if (data == null)
        {
            Debug.LogWarning($"[SkillTreeUI] NodeData introuvable pour : {nodeId}");
            return;
        }

        _detailTitle.text = data.displayName;
        _detailDescription.text = data.description;

        int currentLevel = MetaProgressionManager.Instance.GetNodeLevel(nodeId);
        int gold = MetaProgressionManager.Instance.Data.totalGold;
        int cost = MetaProgressionManager.Instance.GetNodeCost(nodeId);
        bool isUnlockable = MetaProgressionManager.Instance.IsNodeUnlockable(nodeId);
        bool isPurchased = MetaProgressionManager.Instance.IsNodePurchased(nodeId);

        if (data.isUnique)
        {
            if (_level1Text != null) _level1Text.gameObject.SetActive(false);
            if (_level3Text != null) _level3Text.gameObject.SetActive(false);

            if (_level2Text != null)
            {
                _level2Text.gameObject.SetActive(true);
                _level2Text.text = isPurchased
                    ? "<color=#00C853>● Débloqué</color>"
                    : "<color=#AAAAAA>○ Non débloqué</color>";
            }
        }
        else
        {
            if (_level1Text != null) _level1Text.gameObject.SetActive(true);
            if (_level2Text != null) _level2Text.gameObject.SetActive(true);
            if (_level3Text != null) _level3Text.gameObject.SetActive(true);

            _level1Text.text = FormatLevel(1, currentLevel, data.level1Desc);
            _level2Text.text = FormatLevel(2, currentLevel, data.level2Desc);
            _level3Text.text = FormatLevel(3, currentLevel, data.level3Desc);
        }

        if (cost == -1)
        {
            _costText.text = "Niveau maximum";
            _buyButton.interactable = false;
            _buyButtonText.text = "MAX";
            _buyButtonText.color = new Color(0.5f, 0.5f, 0.5f);
        }
        else
        {
            _costText.text = $"Coût : {cost} gold";

            bool canBuy = isUnlockable && gold >= cost;
            _buyButton.interactable = canBuy;

            if (!isUnlockable)
            {
                _buyButtonText.text = "Verrouillé";
                _buyButtonText.color = new Color(0.5f, 0.5f, 0.5f);
            }
            else if (gold < cost)
            {
                _buyButtonText.text = "Gold insuffisant";
                _buyButtonText.color = new Color(0.8f, 0.3f, 0.3f);
            }
            else
            {
                _buyButtonText.text = "ACHETER";
                _buyButtonText.color = new Color(1f, 0.85f, 0f);
            }
        }

        _playerGoldText.text = $"Ton gold : {gold}";

        // AJOUTÉ — à placer à la fin de PopulateDetail(), juste avant la fermeture de la méthode
        SkillTreeData.NodeData nodeData = SkillTreeData.Get(nodeId);
        if (nodeData != null && MetaProgressionManager.Instance != null)
        {
            bool isInactiveBranch = nodeData.branch != MetaProgressionManager.Instance.GetActiveBranch();
            if (isInactiveBranch)
            {
                _buyButton.interactable = false;
                _buyButtonText.text = "Non disponible";
                _buyButtonText.color = new Color(0.5f, 0.5f, 0.5f);
                _costText.text = "Sélectionnez ce personnage pour débloquer cette branche";
            }
        }
    }

    private string FormatLevel(int level, int currentLevel, string desc)
    {
        if (level <= currentLevel)
            return $"<color=#00C853>● Niv {level} : {desc}</color>";
        if (level == currentLevel + 1)
            return $"<color=#FFFFFF>● Niv {level} : {desc}</color>";
        return $"<color=#555555>○ Niv {level} : {desc}</color>";
    }

    public void OnBuyClicked()
    {
        if (string.IsNullOrEmpty(_selectedNodeId) || MetaProgressionManager.Instance == null) return;

        if (MetaProgressionManager.Instance.TryBuyNode(_selectedNodeId))
        {
            PopulateDetail(_selectedNodeId);
            RefreshAllNodes();
            RefreshGoldDisplay();
        }
    }

    public void RefreshGoldDisplay()
    {
        if (_goldText != null && MetaProgressionManager.Instance != null)
            _goldText.text = $"Gold : {MetaProgressionManager.Instance.Data.totalGold}";
    }

    public void RefreshAllNodes()
    {
        // Remplacement du FindObjectsOfType par une lecture directe du cache O(N) ultra rapide
        for (int i = 0; i < _registeredNodes.Count; i++)
        {
            if (_registeredNodes[i] != null)
                _registeredNodes[i].RefreshVisual();
        }
    }

    private Vector2 WorldToPanelLocalPosition(RectTransform sourceRect)
    {
        Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, sourceRect.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _detailPanel.parent as RectTransform, screenPoint, cam, out Vector2 localPoint);
        return localPoint;
    }

    private Vector2 ComputeTargetPosition(RectTransform nodeRect)
    {
        Vector2 nodePos = WorldToPanelLocalPosition(nodeRect);
        float panelW = _detailPanel.rect.width;
        float panelH = _detailPanel.rect.height;
        float offsetX = 150f;
        float margin = 20f;

        // Récupération dynamique de la taille réelle du Canvas de l'UI
        float halfScreenW = _canvasRect != null ? _canvasRect.rect.width * 0.5f : 960f;
        float halfScreenH = _canvasRect != null ? _canvasRect.rect.height * 0.5f : 540f;

        float targetX = nodePos.x + offsetX + panelW * 0.5f;

        if (targetX + panelW * 0.5f > halfScreenW - margin)
            targetX = nodePos.x - offsetX - panelW * 0.5f;

        targetX = Mathf.Clamp(targetX, -halfScreenW + panelW * 0.5f + margin, halfScreenW - panelW * 0.5f - margin);
        float targetY = Mathf.Clamp(nodePos.y, -halfScreenH + panelH * 0.5f + margin, halfScreenH - panelH * 0.5f - margin);

        return new Vector2(targetX, targetY);
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
            t += Time.deltaTime;
            float ratio = Mathf.SmoothStep(0f, 1f, t / _panelMoveDuration);
            _detailPanel.anchoredPosition = Vector2.Lerp(start, target, ratio);
            yield return null;
        }
        _detailPanel.anchoredPosition = target;
    }

    public void OnResetClicked()
    {
        if (MetaProgressionManager.Instance == null) return;

        MetaProgressionManager.Instance.ResetSkillTree();
        RefreshAllNodes();
        RefreshGoldDisplay();
        ClosePanel();
    }
}