using UnityEngine;
using UnityEngine.UI;

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
    [SerializeField] private SkillTreeUI _skillTreeUI;

    private RectTransform _rectTransform;
    private Button _button;
    private Color _iconOriginalColor = Color.white;
    private Color _medalOriginalColor = Color.white;
    private bool _colorsCaptured = false;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _button = GetComponent<Button>();

        if (_button == null) { Debug.LogError($"[SkillNode] Pas de Button sur {_nodeId} !"); return; }
        if (_skillTreeUI == null) { Debug.LogError($"[SkillNode] SkillTreeUI non assigné sur {_nodeId} !"); return; }

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(HandleClick);

        // AJOUT : Le nœud s'inscrit directement dans le cache de l'UI
        _skillTreeUI.RegisterNode(this);
    }

    private void OnDestroy()
    {
        // AJOUT : Nettoyage sécurisé à la destruction de la scène
        if (_skillTreeUI != null)
        {
            _skillTreeUI.UnregisterNode(this);
        }
    }

    private void OnEnable()
    {
        // On capture les couleurs d'origine au tout premier affichage réel pour éviter les erreurs de prefab
        if (!_colorsCaptured)
        {
            if (_iconImage != null) _iconOriginalColor = _iconImage.color;
            if (_medalImage != null) _medalOriginalColor = _medalImage.color;
            _colorsCaptured = true;
        }

        // Plus besoin de Coroutine d'attente d'une frame : on rafraîchit immédiatement à l'activation
        RefreshVisual();
    }

    private void HandleClick()
    {
        if (_skillTreeUI != null)
        {
            _skillTreeUI.OnNodeClicked(_nodeId, _rectTransform);
        }
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