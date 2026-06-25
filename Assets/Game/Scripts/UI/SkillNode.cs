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

    [Header("Couleur état verrouillé")]
    [SerializeField] private Color _colorLocked = new Color(0.3f, 0.3f, 0.3f);

    [Header("Référence")]
    [SerializeField] private SkillTreeUI _skillTreeUI; // ← assigné dans l'Inspector

    private RectTransform _rectTransform;
    private Button _button;
    private Color _iconOriginalColor;
    private Color _medalOriginalColor;

    private void Awake()
    {
        if (_iconImage != null) _iconOriginalColor = _iconImage.color;
        if (_medalImage != null) _medalOriginalColor = _medalImage.color;

        _rectTransform = GetComponent<RectTransform>();
        _button = GetComponent<Button>();

        if (_button == null) { Debug.LogError($"[SkillNode] Pas de Button sur {_nodeId} !"); return; }
        if (_skillTreeUI == null) { Debug.LogError($"[SkillNode] SkillTreeUI non assigné sur {_nodeId} !"); return; }

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _skillTreeUI.OnNodeClicked(_nodeId, _rectTransform));
    }

    private void OnEnable()
    {
        StartCoroutine(RefreshAfterFrame());
    }

    private IEnumerator RefreshAfterFrame()
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

        bool hasProgress = _isUnique ? isPurchased : level > 0;
        bool locked = !hasProgress && !isUnlockable;

        ApplyState(locked, level);

        // Bouton toujours interactable — même au max, on peut cliquer pour voir les stats
        _button.interactable = true;
    }

    private void ApplyState(bool locked, int level)
    {
        float alpha = locked ? 0.45f : 1f;

        if (locked)
        {
            SetFullColor(_medalImage, _colorLocked, alpha);
            SetFullColor(_iconImage, _colorLocked, alpha);
        }
        else
        {
            SetFullColor(_medalImage, _medalOriginalColor, alpha);
            SetFullColor(_iconImage, _iconOriginalColor, alpha);
        }

        if (_isUnique)
        {
            bool purchased = MetaProgressionManager.Instance.IsNodePurchased(_nodeId);

            if (_dot2 != null) _dot2.gameObject.SetActive(false);
            if (_dot3 != null) _dot3.gameObject.SetActive(false);

            SetDot(_dot1, purchased);
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

    private void SetFullColor(Image img, Color color, float alpha)
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