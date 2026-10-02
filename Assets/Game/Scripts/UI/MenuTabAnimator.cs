using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Animation d'un onglet de la barre de navigation du menu principal : UNIQUEMENT un grossissement doux au survol
// (peu importe où se trouve le curseur sur l'onglet), plus le passage à l'état actif (un peu plus grand, pleine couleur).
// Volontairement sobre : pas de penché, pas d'écrasement au clic, pas de rebond (retour utilisateur : les onglets
// paraissaient instables). Ne touche ni à la position ni à la taille (le HorizontalLayoutGroup de la barre les pilote) :
// uniquement échelle et couleur. Ajouté automatiquement par MenuTabBarFX, rien à poser dans la scène.
[RequireComponent(typeof(Image))]
public class MenuTabAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Échelle")]
    [SerializeField] private float _hoverScale = 1.07f;
    [SerializeField] private float _activeScale = 1.08f;

    [Header("Couleur (multiplie le ruban)")]
    [SerializeField] private Color _inactiveColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    [SerializeField] private Color _hoverColor = new Color(0.9f, 0.9f, 0.9f, 1f);
    [SerializeField] private Color _activeColor = Color.white;

    [Header("Lissage")]
    [Tooltip("Durée approximative du grossissement, en secondes. Un ressort critique : jamais de dépassement.")]
    [SerializeField] private float _smoothTime = 0.09f;

    private Image _image;
    private bool _hovering;
    private bool _active;
    private float _scale = 1f;
    private float _scaleVelocity;

    public bool IsActive => _active;

    private void Awake()
    {
        _image = GetComponent<Image>();
    }

    private void OnEnable()
    {
        _hovering = false;
        SnapToTarget();
    }

    // Appelé par MainMenuManager : l'onglet correspond-il au panneau affiché ?
    public void SetActiveState(bool active, bool instant = false)
    {
        _active = active;
        if (instant) SnapToTarget();
    }

    private float TargetScale()
    {
        if (_active) return _activeScale;
        return _hovering ? _hoverScale : 1f;
    }

    private Color TargetColor()
    {
        if (_active) return _activeColor;
        return _hovering ? _hoverColor : _inactiveColor;
    }

    private void SnapToTarget()
    {
        if (_image == null) _image = GetComponent<Image>();
        _scale = TargetScale();
        _scaleVelocity = 0f;
        transform.localScale = new Vector3(_scale, _scale, 1f);
        transform.localRotation = Quaternion.identity;
        _image.color = TargetColor();
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;   // le menu ne dépend jamais de Time.timeScale

        _scale = Mathf.SmoothDamp(_scale, TargetScale(), ref _scaleVelocity, _smoothTime, Mathf.Infinity, dt);
        transform.localScale = new Vector3(_scale, _scale, 1f);

        _image.color = Color.Lerp(_image.color, TargetColor(), 1f - Mathf.Exp(-16f * dt));
    }

    public void OnPointerEnter(PointerEventData e) { _hovering = true; }
    public void OnPointerExit(PointerEventData e) { _hovering = false; }
}
