using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// AJOUTE (2026-09-20) - fait défiler une liste HORIZONTALE avec la molette verticale de la souris (un ScrollRect
// horizontal ne réagit normalement qu'au défilement horizontal). Posé sur les rangées de skins de l'onglet Réputation :
// inactif tant que la rangée tient en entier dans son cadre.
[RequireComponent(typeof(ScrollRect))]
public class WheelToHorizontal : MonoBehaviour, IScrollHandler
{
    [SerializeField] private float _speed = 0.35f;
    private ScrollRect _scroll;

    private void Awake() => _scroll = GetComponent<ScrollRect>();

    public void OnScroll(PointerEventData eventData)
    {
        if (_scroll == null || _scroll.content == null || _scroll.viewport == null) return;
        float overflow = _scroll.content.rect.width - _scroll.viewport.rect.width;
        if (overflow <= 1f) return;
        float delta = (eventData.scrollDelta.y != 0f ? eventData.scrollDelta.y : -eventData.scrollDelta.x) * _speed * 100f / overflow;
        _scroll.horizontalNormalizedPosition = Mathf.Clamp01(_scroll.horizontalNormalizedPosition - delta);
    }
}
