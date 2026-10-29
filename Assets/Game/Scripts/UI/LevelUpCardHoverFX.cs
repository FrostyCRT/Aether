using UnityEngine;
using UnityEngine.EventSystems;

// AJOUTÉ (2026-10-28) - survol d'une carte de montée de niveau.
// MODIFIÉ (2026-10-29, retour utilisateur : "enlève ce que tu as mis, je veux qu'on revienne à un petit survol simple") -
// la carte grossit simplement un peu au survol, en douceur (plus de soulèvement, d'inclinaison, de halo ni d'icône animée).
// Posé par UpgradeUI sur le bouton de chaque carte (il reçoit le pointeur, souris ou curseur de la manette) ; anime la
// carte entière. Temps réel : le jeu est en pause pendant le choix. Pendant l'apparition des cartes et l'animation de
// choix (UpgradeUI.CardsBusy), l'effet s'efface et ne touche plus à rien : ces animations pilotent elles-mêmes la carte.
public class LevelUpCardHoverFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const float HoverScale = 1.04f;
    private const float Speed = 14f;

    private UpgradeUI _owner;
    private RectTransform _card;

    private bool _hover, _active;
    private float _k = 1f;
    private Vector3 _cardScale;

    public void Init(UpgradeUI owner, RectTransform card)
    {
        _owner = owner;
        _card = card;
    }

    public void OnPointerEnter(PointerEventData e) { _hover = true; }
    public void OnPointerExit(PointerEventData e) { _hover = false; }

    // Remet la carte exactement dans son état de repos (avant une animation d'UpgradeUI qui lit son état de départ).
    public void ResetNow()
    {
        if (_active) Restore();
        _k = 1f;
        _hover = false;
    }

    private void Update()
    {
        if (_card == null) return;
        if (_owner != null && _owner.CardsBusy)
        {
            if (_active) ResetNow();
            return;
        }

        float target = _hover ? HoverScale : 1f;
        if (!_active)
        {
            if (!_hover) return;
            _active = true;
            _cardScale = _card.localScale;
        }
        _k = Mathf.Lerp(_k, target, 1f - Mathf.Exp(-Speed * Time.unscaledDeltaTime));
        if (Mathf.Abs(_k - target) < 0.001f) _k = target;
        _card.localScale = _cardScale * _k;
        if (!_hover && _k == 1f) Restore();
    }

    private void Restore()
    {
        _active = false;
        _card.localScale = _cardScale;
    }

    private void OnDisable() => ResetNow();
}
