using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

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

    private void Start()
    {
        _canvas = GetComponentInParent<Canvas>();
        _detailPanel.gameObject.SetActive(false);

        if (_outsideClickCatcher != null)
        {
            _outsideClickCatcher.SetActive(false);
            Button catcherButton = _outsideClickCatcher.GetComponent<Button>();
            if (catcherButton != null)
                catcherButton.onClick.AddListener(ClosePanel);
        }

        RefreshGoldDisplay();
    }

    // ─── Clic sur un médaillon ───────────────────────────────────────────────

    public void OnNodeClicked(string nodeId, RectTransform nodeRect)
    {
        // Re-clic sur le même = ferme le panel
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
            // Première ouverture : le panel démarre depuis le nœud cliqué (effet "sortie du médaillon")
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

        // Affichage des niveaux
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

        // Coût et bouton
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
    }

    private string FormatLevel(int level, int currentLevel, string desc)
    {
        if (level <= currentLevel)
            return $"<color=#00C853>● Niv {level} : {desc}</color>";
        if (level == currentLevel + 1)
            return $"<color=#FFFFFF>● Niv {level} : {desc}</color>";
        return $"<color=#555555>○ Niv {level} : {desc}</color>";
    }

    // ─── Achat ──────────────────────────────────────────────────────────────

    public void OnBuyClicked()
    {
        if (string.IsNullOrEmpty(_selectedNodeId)) return;

        bool success = MetaProgressionManager.Instance.TryBuyNode(_selectedNodeId);
        if (success)
        {
            PopulateDetail(_selectedNodeId);
            RefreshAllNodes();
            RefreshGoldDisplay();
        }
    }

    // ─── Refresh visuel ──────────────────────────────────────────────────────

    public void RefreshGoldDisplay()
    {
        if (_goldText != null)
            _goldText.text = $"Gold : {MetaProgressionManager.Instance.Data.totalGold}";
    }

    public void RefreshAllNodes()
    {
        foreach (SkillNode node in FindObjectsOfType<SkillNode>())
            node.RefreshVisual();
    }

    private Vector2 WorldToPanelLocalPosition(RectTransform sourceRect)
    {
        Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, sourceRect.position);
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _detailPanel.parent as RectTransform, screenPoint, cam, out localPoint);
        return localPoint;
    }
    // ─── Positionnement du panel ─────────────────────────────────────────────

    private Vector2 ComputeTargetPosition(RectTransform nodeRect)
    {
        Vector2 nodePos = WorldToPanelLocalPosition(nodeRect);
        float panelW = _detailPanel.rect.width;
        float panelH = _detailPanel.rect.height;
        float offsetX = 150f;
        float halfScreenW = 960f;
        float halfScreenH = 540f;
        float margin = 20f;

        // Essai à droite du nœud
        float targetX = nodePos.x + offsetX + panelW * 0.5f;

        // Si ça dépasse le bord droit, on retourne le panel à gauche du nœud
        if (targetX + panelW * 0.5f > halfScreenW - margin)
            targetX = nodePos.x - offsetX - panelW * 0.5f;

        // Sécurité finale : on clamp toujours dans l'écran, même pour un nœud collé au bord
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
        MetaProgressionManager.Instance.ResetSkillTree();
        RefreshAllNodes();
        RefreshGoldDisplay();
        ClosePanel();
    }
}