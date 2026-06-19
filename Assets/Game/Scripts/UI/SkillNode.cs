using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SkillNode : MonoBehaviour
{
    [Header("Identité")]
    [SerializeField] private string _nodeId;
    [SerializeField] private bool _isUnique;

    [Header("Visuels")]
    [SerializeField] private Image _medalImage;
    [SerializeField] private Image _iconImage;
    [SerializeField] private Image _dot1;
    [SerializeField] private Image _dot2;
    [SerializeField] private Image _dot3;

    [Header("Couleurs par état")]
    [SerializeField] private Color _colorUnlocked = new Color(1f, 0.85f, 0f);    // Doré
    [SerializeField] private Color _colorAvailable = Color.white;                   // Blanc naturel
    [SerializeField] private Color _colorLocked = new Color(0.3f, 0.3f, 0.3f); // Grisé

    private RectTransform _rectTransform;
    private Button _button;

    [Header("Référence")]
    [SerializeField] private SkillTreeUI _skillTreeUI; // ← assigné dans l'Inspector

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _button = GetComponent<Button>();

        Debug.Log($"[SkillNode] Awake sur {_nodeId} — button: {_button != null} — skillTreeUI: {_skillTreeUI != null}");

        if (_button == null) { Debug.LogError($"[SkillNode] PAS DE BUTTON sur {_nodeId} !"); return; }
        if (_skillTreeUI == null) { Debug.LogError($"[SkillNode] SkillTreeUI NULL sur {_nodeId} !"); return; }

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() =>
        {
            Debug.Log($"[SkillNode] CLIC sur {_nodeId}");
            _skillTreeUI.OnNodeClicked(_nodeId, _rectTransform);
        });
    }

    private void OnEnable()
    {
        StartCoroutine(RefreshAfterFrame());
    }

    private System.Collections.IEnumerator RefreshAfterFrame()
    {
        yield return null;
        RefreshVisual();
    }

    public void RefreshVisual()
    {
        if (MetaProgressionManager.Instance == null) return;

        bool isUnlockable = MetaProgressionManager.Instance.IsNodeUnlockable(_nodeId);
        bool isPurchased = MetaProgressionManager.Instance.IsNodePurchased(_nodeId);
        int level = MetaProgressionManager.Instance.GetNodeLevel(_nodeId);
        int gold = MetaProgressionManager.Instance.Data.totalGold;
        int cost = MetaProgressionManager.Instance.GetNodeCost(_nodeId);
        bool canAfford = gold >= cost && cost != -1;

        if (_isUnique)
        {
            if (isPurchased)
                ApplyState(_colorUnlocked, 1f, -1);       // Doré = acheté
            else if (isUnlockable && canAfford)
                ApplyState(_colorAvailable, 1f, -1);      // Blanc = disponible
            else
                ApplyState(_colorLocked, 0.45f, -1);      // Grisé = verrouillé
        }
        else
        {
            if (level >= 3)
                ApplyState(_colorUnlocked, 1f, level);    // Doré = max
            else if (level > 0)
                ApplyState(_colorAvailable, 1f, level);   // Blanc = en cours
            else if (isUnlockable && canAfford)
                ApplyState(_colorAvailable, 1f, level);   // Blanc = disponible
            else
                ApplyState(_colorLocked, 0.45f, level);   // Grisé = verrouillé
        }

        // Bouton interactable si on peut encore acheter
        bool maxed = _isUnique ? isPurchased : (level >= 3);
        // Bouton toujours interactable — même au max, on peut cliquer pour voir les stats
        _button.interactable = true;
    }

    // Applique couleur + alpha sur médaillon et icône, et met à jour les dots
    private void ApplyState(Color color, float alpha, int level)
    {
        SetImageColor(_medalImage, color, alpha);
        SetImageColor(_iconImage, color, alpha);

        if (_isUnique)
        {
            bool purchased = MetaProgressionManager.Instance.IsNodePurchased(_nodeId);
            SetDot(_dot1, purchased);
            if (_dot2 != null) _dot2.gameObject.SetActive(false);
            if (_dot3 != null) _dot3.gameObject.SetActive(false);
        }
        else
        {
            if (_dot2 != null) _dot2.gameObject.SetActive(true);
            if (_dot3 != null) _dot3.gameObject.SetActive(true);
            SetDot(_dot1, level >= 1);
            SetDot(_dot2, level >= 2);
            SetDot(_dot3, level >= 3);
        }
    }

    private void SetImageColor(Image img, Color color, float alpha)
    {
        if (img == null) return;
        Color c = color;
        c.a = alpha;
        img.color = c;
    }

    private void SetDot(Image dot, bool active)
    {
        if (dot == null) return;
        Color c = dot.color;
        c.a = active ? 1f : 0.25f;
        dot.color = c;
    }
}