using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// AJOUTÉ (2026-10-25, retour utilisateur : "une animation au survol quand on met la souris au dessus de la croix pour
// quitter le panel de description d'une compétence [...] la croix avec le fond rond rouge et ça se grossit") - posé
// sur NodeDetailPanel/CloseButton (MainMenu). Au survol : le disque de fond passe au rouge et le bouton grossit, en
// fondu (temps non mis à l'échelle, le menu peut tourner avec timeScale = 0) ; léger écrasement au clic. Remplace la
// transition ColorTint du Button (mise à None dans la scène), qui aurait sinon re-teinté la couleur par-dessus.
[RequireComponent(typeof(Image))]
public class CloseButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private Color _hoverColor = new Color32(0xD6, 0x3A, 0x3A, 0xFF);
    [SerializeField] private float _hoverScale = 1.18f;
    [SerializeField] private float _pressScale = 1.05f;
    [SerializeField] private float _speed = 14f;

    [SerializeField] private Color _hoverGlyphColor = Color.white;   // la croix passe au blanc sur le rouge (lisibilité)

    private Image _image;
    private Color _baseColor;
    private TMPro.TMP_Text _glyph;
    private Color _baseGlyphColor;
    private bool _hovered;
    private bool _pressed;
    private float _t; // 0 = repos, 1 = survol complet

    private void Awake()
    {
        _image = GetComponent<Image>();
        _baseColor = _image.color;
        _glyph = GetComponentInChildren<TMPro.TMP_Text>(true);
        if (_glyph != null) _baseGlyphColor = _glyph.color;
    }

    private void OnDisable()
    {
        // Le panneau se ferme justement par ce bouton : repartir de l'état de repos à sa prochaine ouverture.
        _hovered = _pressed = false;
        _t = 0f;
        Apply();
    }

    public void OnPointerEnter(PointerEventData eventData) => _hovered = true;
    public void OnPointerExit(PointerEventData eventData) { _hovered = false; _pressed = false; }
    public void OnPointerDown(PointerEventData eventData) => _pressed = true;
    public void OnPointerUp(PointerEventData eventData) => _pressed = false;

    private void Update()
    {
        _t = Mathf.MoveTowards(_t, _hovered ? 1f : 0f, _speed * 0.5f * Time.unscaledDeltaTime);
        Apply();
    }

    private float ScaleFor(float t)
    {
        float eased = t * t * (3f - 2f * t);
        return Mathf.Lerp(1f, _pressed ? _pressScale : _hoverScale, eased);
    }

    private void Apply()
    {
        if (_image == null) return;
        float eased = _t * _t * (3f - 2f * _t);
        _image.color = Color.Lerp(_baseColor, _hoverColor, eased);
        if (_glyph != null) _glyph.color = Color.Lerp(_baseGlyphColor, _hoverGlyphColor, eased);
        transform.localScale = Vector3.one * ScaleFor(_t);
    }
}
