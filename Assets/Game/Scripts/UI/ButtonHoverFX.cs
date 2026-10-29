using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// AJOUTÉ (2026-10-28, retour utilisateur : "fais au moins une animation au survol de chaque bouton du jeu, pour que le
// joueur comprenne qu'il peut appuyer dessus") - léger grossissement au survol (souris ou curseur de la manette, qui
// est une souris virtuelle), petit enfoncement à l'appui, en douceur et en temps réel (marche aussi en pause).
//
// Posé tout seul sur CHAQUE bouton du jeu, y compris ceux créés en cours de route (lignes des Paramètres, listes
// déroulantes...) : un balayage léger des boutons actifs, deux fois par seconde. Les boutons qui ont déjà leur propre
// effet de survol (onglets du menu, cartes de montée de niveau, médaillons de l'arbre, croix de fermeture...) - tout
// autre composant qui réagit à l'entrée du pointeur - sont laissés tels quels. Un bouton grisé ne réagit pas.
// Le grossissement se MULTIPLIE à l'échelle du bouton : si un autre script la change entre-temps (pastille choisie
// agrandie, apparition animée...), cette nouvelle échelle devient la base.
public class ButtonHoverFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private const float HoverScale = 1.06f;
    private const float PressScale = 0.95f;
    private const float Speed = 18f;            // vitesse de l'animation (lissage exponentiel)

    private Selectable _selectable;
    private Vector3 _base;
    private Vector3 _written;                   // dernière échelle posée ici (pour reconnaître un changement extérieur)
    private float _k = 1f;
    private bool _hover, _pressed, _animating;

    public void OnPointerEnter(PointerEventData e) { _hover = true; Wake(); }
    public void OnPointerExit(PointerEventData e) { _hover = false; _pressed = false; Wake(); }
    public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) { _pressed = true; Wake(); } }
    public void OnPointerUp(PointerEventData e) { _pressed = false; Wake(); }

    private void Awake() => _selectable = GetComponent<Selectable>();

    private void Wake()
    {
        if (_animating) return;
        _animating = true;
        _base = transform.localScale;
        _written = _base;
    }

    private void Update()
    {
        if (!_animating) return;
        // échelle changée par un autre script : c'est la nouvelle base
        if (transform.localScale != _written) _base = _k > 0f ? transform.localScale / _k : transform.localScale;

        bool usable = _selectable == null || _selectable.IsInteractable();
        float target = !usable ? 1f : _pressed ? PressScale : _hover ? HoverScale : 1f;
        _k = Mathf.Lerp(_k, target, 1f - Mathf.Exp(-Speed * Time.unscaledDeltaTime));
        if (Mathf.Abs(_k - target) < 0.002f) _k = target;

        _written = _base * _k;
        transform.localScale = _written;
        if (_k == 1f && target == 1f) _animating = false;   // au repos : on ne touche plus à l'échelle
    }

    private void OnDisable()
    {
        if (_animating) transform.localScale = _base;
        _animating = _hover = _pressed = false;
        _k = 1f;
    }

    // Bouton qui ne doit PAS grossir au survol (grande zone cliquable qui déborderait, voir CharacterInfoPanel) : un
    // ButtonHoverFX désactivé suffit, le balayage ne lui en ajoute plus.
    public static void Exclude(Selectable s)
    {
        if (s == null) return;
        if (!s.TryGetComponent(out ButtonHoverFX fx)) fx = s.gameObject.AddComponent<ButtonHoverFX>();
        fx.enabled = false;
    }

    // ---- pose automatique ------------------------------------------------------------------------------------------
    private static Selectable[] _buffer = new Selectable[256];
    private static float _nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        GameObject go = new GameObject("[ButtonHoverFX]");
        DontDestroyOnLoad(go);
        go.AddComponent<Scanner>();
        SceneManager.sceneLoaded += (s, m) => _nextScan = 0f;
    }

    private class Scanner : MonoBehaviour
    {
        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 0.5f;
            if (_buffer.Length < Selectable.allSelectableCount) _buffer = new Selectable[Selectable.allSelectableCount * 2];
            int count = Selectable.AllSelectablesNoAlloc(_buffer);
            for (int i = 0; i < count; i++)
            {
                Selectable s = _buffer[i];
                if (s is Button && !s.TryGetComponent(out ButtonHoverFX _) && !HasOwnHover(s.gameObject))
                    s.gameObject.AddComponent<ButtonHoverFX>();
            }
        }

        // Un autre composant que le bouton lui-même réagit déjà au survol : il a son propre effet.
        private static readonly System.Collections.Generic.List<MonoBehaviour> _components = new System.Collections.Generic.List<MonoBehaviour>();

        private static bool HasOwnHover(GameObject go)
        {
            go.GetComponents(_components);
            foreach (MonoBehaviour mb in _components)
                if (mb is IPointerEnterHandler && !(mb is Selectable)) return true;
            return false;
        }
    }
}
