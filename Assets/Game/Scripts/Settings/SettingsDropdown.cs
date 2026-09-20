using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// AJOUTE (2026-09-19) - liste déroulante des lignes « choix » de la page Paramètres.
//
// Un clic sur le bouton d'une ligne ouvre ici la liste de toutes les valeurs possibles, sous le bouton (au-dessus s'il
// n'y a pas la place). Elle se ferme d'elle-même quand le curseur s'éloigne du bouton et de la liste, quand on clique
// ailleurs, ou avec Échap. Elle vit à la racine de la page (et non dans la ligne) : la zone de défilement des lignes
// la couperait. Au-delà de 8 valeurs (résolutions), la liste défile à la molette et s'ouvre sur la valeur courante.
public class SettingsDropdown : MonoBehaviour
{
    [SerializeField] private RectTransform _panel;
    [SerializeField] private RectTransform _content;
    [SerializeField] private ScrollRect _scroll;
    [SerializeField] private Button _itemTemplate;
    [SerializeField] private float _itemHeight = 50f;
    [SerializeField] private float _spacing = 3f;
    [SerializeField] private float _padding = 8f;
    [SerializeField] private int _maxVisible = 8;
    [SerializeField] private float _leaveMargin = 14f;

    private readonly List<Button> _items = new List<Button>();
    private RectTransform _anchor;
    private Action<int> _onPick;
    private float _openedAt;

    public bool IsOpen => gameObject.activeSelf;
    public int ClosedFrame { get; private set; } = -1;     // image de la dernière fermeture (l'Échap qui ferme la liste ne doit pas fermer la page)
    public RectTransform Anchor => IsOpen ? _anchor : null;

    private void Awake()
    {
        if (_itemTemplate != null) _itemTemplate.gameObject.SetActive(false);
    }

    public void Open(RectTransform anchor, string[] options, int current, Action<int> onPick)
    {
        Close();
        if (anchor == null || options == null || options.Length == 0) return;
        _anchor = anchor;
        _onPick = onPick;
        _openedAt = Time.unscaledTime;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();          // au-dessus des lignes et de la carte (les fenêtres restent au-dessus si elles sont rouvertes ensuite)

        for (int i = 0; i < options.Length; i++)
        {
            Button b = Instantiate(_itemTemplate, _content);
            b.gameObject.SetActive(true);
            b.name = "Item_" + i;
            bool selected = i == current;
            Transform border = b.transform.Find("Border");
            Transform fill = b.transform.Find("Fill");
            if (border != null) border.gameObject.SetActive(selected);
            if (fill != null) fill.GetComponent<Image>().color = selected ? SettingsStyle.AccentSoft : new Color32(0x1A, 0x15, 0x27, 0xFF);
            TextMeshProUGUI t = b.GetComponentInChildren<TextMeshProUGUI>(true);
            t.text = options[i];
            t.color = selected ? Color.white : SettingsStyle.Body;
            int index = i;
            b.onClick.AddListener(() => Pick(index));
            _items.Add(b);
        }

        // dimensions : largeur du bouton, hauteur selon le nombre de valeurs (8 visibles au plus)
        Canvas.ForceUpdateCanvases();
        RectTransform parent = (RectTransform)transform.parent;
        Vector3[] c = new Vector3[4];
        anchor.GetWorldCorners(c);
        Vector2 bl = parent.InverseTransformPoint(c[0]);
        Vector2 tr = parent.InverseTransformPoint(c[2]);
        float width = tr.x - bl.x;

        int shown = Mathf.Min(options.Length, _maxVisible);
        float full = options.Length * _itemHeight + (options.Length - 1) * _spacing + _padding * 2f;
        float height = shown * _itemHeight + (shown - 1) * _spacing + _padding * 2f;

        // en dessous du bouton ; au-dessus si le bas de l'écran est atteint
        Vector2 centerOffset = parent.rect.center;
        bool below = bl.y - 4f - height >= parent.rect.yMin + 8f;
        _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
        _panel.pivot = new Vector2(0.5f, below ? 1f : 0f);
        _panel.sizeDelta = new Vector2(width, height);
        Vector2 pos = new Vector2((bl.x + tr.x) * 0.5f, below ? bl.y - 4f : tr.y + 4f) - centerOffset;
        _panel.anchoredPosition = pos;

        _content.sizeDelta = new Vector2(0f, full - _padding * 2f);
        _scroll.vertical = options.Length > _maxVisible;
        LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);     // la zone visible doit avoir sa taille avant de placer le défilement
        _scroll.verticalNormalizedPosition = 1f;
        if (options.Length > _maxVisible)
        {
            // ouvre la liste sur la valeur courante
            float scrollable = full - height;
            float y = Mathf.Max(0f, current * (_itemHeight + _spacing) - (height - _padding * 2f) * 0.5f + _itemHeight * 0.5f);
            _scroll.verticalNormalizedPosition = scrollable > 0f ? 1f - Mathf.Clamp01(y / scrollable) : 1f;
        }
    }

    public void Close()
    {
        foreach (Button b in _items) if (b != null) Destroy(b.gameObject);
        _items.Clear();
        _anchor = null;
        _onPick = null;
        if (gameObject.activeSelf) { ClosedFrame = Time.frameCount; gameObject.SetActive(false); }
    }

    private void Pick(int index)
    {
        Action<int> cb = _onPick;
        Close();
        cb?.Invoke(index);
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (_anchor == null || !_anchor.gameObject.activeInHierarchy) { Close(); return; }
        if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
        if (Time.unscaledTime - _openedAt < 0.12f) return;

        // le curseur s'éloigne du bouton ET de la liste (marge de 14 px, qui couvre l'espace entre les deux)
        RectTransform parent = (RectTransform)transform.parent;
        Canvas canvas = parent.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, Input.mousePosition, cam, out Vector2 local)) { Close(); return; }

        if (!Contains(parent, _anchor, local) && !Contains(parent, _panel, local)) Close();
    }

    private bool Contains(RectTransform space, RectTransform target, Vector2 localPoint)
    {
        Vector3[] c = new Vector3[4];
        target.GetWorldCorners(c);
        Vector2 bl = space.InverseTransformPoint(c[0]);
        Vector2 tr = space.InverseTransformPoint(c[2]);
        return localPoint.x >= bl.x - _leaveMargin && localPoint.x <= tr.x + _leaveMargin
            && localPoint.y >= bl.y - _leaveMargin && localPoint.y <= tr.y + _leaveMargin;
    }

    private void OnDisable()
    {
        // désactivation par le parent (changement de page) : rien ne doit rester
        foreach (Button b in _items) if (b != null) Destroy(b.gameObject);
        _items.Clear();
    }
}
