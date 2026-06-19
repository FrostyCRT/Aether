using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    private string _selectedNodeId = "";
    private bool _isPanelOpen = false;

    private void Start()
    {
        _detailPanel.gameObject.SetActive(false);
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

        _selectedNodeId = nodeId;
        PopulateDetail(nodeId);
        PositionPanel(nodeRect);

        _detailPanel.gameObject.SetActive(true);
        _isPanelOpen = true;
    }

    public void ClosePanel()
    {
        _detailPanel.gameObject.SetActive(false);
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
            _level1Text.text = isPurchased
                ? "<color=#00C853>✓ Débloqué</color>"
                : "<color=#AAAAAA>○ Non débloqué</color>";
            if (_level2Text != null) _level2Text.gameObject.SetActive(false);
            if (_level3Text != null) _level3Text.gameObject.SetActive(false);
        }
        else
        {
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
            return $"<color=#00C853>✓ Niv {level} : {desc}</color>";
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

    // ─── Positionnement du panel ─────────────────────────────────────────────

    private void PositionPanel(RectTransform nodeRect)
    {
        _detailPanel.anchorMin = new Vector2(0.5f, 0.5f);
        _detailPanel.anchorMax = new Vector2(0.5f, 0.5f);

        Vector2 nodePos = nodeRect.anchoredPosition;
        float panelW = _detailPanel.rect.width;
        float panelH = _detailPanel.rect.height;
        float offsetX = 150f;
        float targetX = nodePos.x + offsetX;
        float targetY = nodePos.y;

        // Si ça dépasse à droite → place à gauche
        if (targetX + panelW * 0.5f > 940f)
            targetX = nodePos.x - offsetX - panelW + 80f;

        // Clamp vertical
        targetY = Mathf.Clamp(targetY, -540f + panelH * 0.5f + 20f, 540f - panelH * 0.5f - 20f);

        _detailPanel.anchoredPosition = new Vector2(targetX, targetY);
    }
    public void OnResetClicked()
    {
        MetaProgressionManager.Instance.ResetSkillTree();
        RefreshAllNodes();
        RefreshGoldDisplay();
        ClosePanel();
    }
}