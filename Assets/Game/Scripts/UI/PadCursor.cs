using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

// AJOUTÉ (2026-10-28, retour utilisateur après essai à la manette : "la case dorée [...] pas optimisée pour ce genre de
// jeu dans le menu. On devrait plutôt passer à un curseur en forme de rond, qu'on dirige avec le joystick") - curseur
// de la manette dans les menus, à la place de la navigation bouton par bouton.
//
// Le stick gauche (et, plus lentement, la croix) déplace un rond à l'écran ; A clique. Le curseur pilote une SOURIS
// VIRTUELLE (périphérique Input System) : l'interface la traite exactement comme la vraie souris - survol, clic, glisser
// des curseurs de réglage, sans rien câbler écran par écran. Il ralentit au-dessus d'un bouton (visée plus facile).
// Actif seulement quand le jeu montre un curseur (menus, pause, cartes de niveau, écrans de fin : voir
// GameManager.NeedsCursor) et que le joueur joue à la manette ; en pleine partie, A reste le Dash.
//
// Zones qui défilent (liste des Paramètres, longues listes déroulantes) : A sur le fond de la zone (pas sur un bouton)
// passe en mode défilement - le stick haut / bas fait défiler, le curseur reste figé ; la touche retour de la manette
// (B, Rond sur PlayStation) en sort. Indicateurs : bulle « A Faire défiler » au survol d'une telle zone, puis liseré doré
// autour de la zone et bulle « B Quitter le défilement » pendant le défilement (icônes au style de la manette branchée).
[DefaultExecutionOrder(-950)]
public class PadCursor : MonoBehaviour
{
    private static PadCursor _instance;

    public static bool Active { get; private set; }
    public static Vector2 Position { get; private set; }
    public static bool ClickedThisFrame => _instance != null && _instance._clickFrame == Time.frameCount;
    public static int BackConsumedFrame { get; private set; } = -1;   // le B qui quitte le défilement (voir GameInput.PadBackDown)
    public static Mouse VirtualMouse => _instance != null ? _instance._mouse : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (_instance != null) return;
        GameObject go = new GameObject("[PadCursor]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<PadCursor>();
    }

    private const float MaxSpeed = 1500f;        // pixels / s à 1080p, stick à fond
    private const float DpadSpeed = 420f;        // croix : déplacement lent et précis
    private const float OverControlSlow = 0.55f; // vitesse au-dessus d'un bouton
    private const float ScrollSpeed = 1300f;     // unités du canevas / s, stick à fond
    private const float CursorSize = 44f;

    private Mouse _mouse;
    private bool _buttonDown;
    private bool _holdUntilRelease;   // A qui vient d'ouvrir le défilement : pas de clic tant qu'il n'est pas relâché
    private Vector2 _sentPos;
    private int _clickFrame = -1;
    private ScrollRect _scroll;       // zone en cours de défilement
    private ScrollRect _hoverScroll;  // zone qui défilerait sous le curseur
    private bool _overControl;

    private RectTransform _cursor, _frame, _hint;
    private Image _cursorImage, _frameImage;
    private PadGlyph _hintGlyph;
    private TextMeshProUGUI _hintText;
    private PadButton _hintButton = PadButton.None;
    private string _hintLabel;

    private readonly List<RaycastResult> _hits = new List<RaycastResult>();
    private PointerEventData _probe;
    private EventSystem _probeOwner;

    private void Awake()
    {
        BuildVisuals();
    }

    private void Update()
    {
        Gamepad g = Gamepad.current;
        bool want = GameInput.UsingGamepad && g != null && EventSystem.current != null
                    && (GameManager.Instance == null || GameManager.Instance.NeedsCursor);
        if (!want)
        {
            if (Active) Deactivate();
            return;
        }
        if (!Active) Activate();

        // Un bouton cliqué devient « sélectionné » pour l'interface : A (Submit) le revaliderait en plus du clic du
        // curseur, où qu'il soit. À la manette, rien ne reste sélectionné (tout passe par le curseur).
        if (EventSystem.current.currentSelectedGameObject != null) EventSystem.current.SetSelectedGameObject(null);

        float dt = Time.unscaledDeltaTime;
        Probe();

        if (_scroll != null && (!_scroll.isActiveAndEnabled || !Scrollable(_scroll))) ExitScroll();
        if (_scroll != null)
        {
            if (GameInput.PadDown(PadButton.East)) { BackConsumedFrame = Time.frameCount; ExitScroll(); }
            else ScrollBy(_scroll, Direction(g).y + g.dpad.ReadValue().y * 0.5f, dt);
        }
        else Move(g, dt);

        bool held = g.buttonSouth.isPressed;
        if (g.buttonSouth.wasPressedThisFrame && _scroll == null)
        {
            if (_hoverScroll != null) { EnterScroll(_hoverScroll); _holdUntilRelease = true; }
            else _clickFrame = Time.frameCount;
        }
        if (_holdUntilRelease) { if (!held) _holdUntilRelease = false; held = false; }
        if (_scroll != null) held = false;

        Send(held);
        UpdateVisuals();
    }

    // ---- déplacement ----------------------------------------------------------------------------------------------
    private static Vector2 Direction(Gamepad g)
    {
        Vector2 v = GameInput.Stick(g.leftStick);
        v *= Mathf.Pow(v.magnitude, 0.8f);              // courbe : petits mouvements précis, stick à fond rapide
        return v;
    }

    private void Move(Gamepad g, float dt)
    {
        float scale = Screen.height / 1080f;
        Vector2 step = Direction(g) * MaxSpeed + g.dpad.ReadValue() * DpadSpeed;
        if (_overControl) step *= OverControlSlow;
        Vector2 p = Position + step * scale * dt;
        Position = new Vector2(Mathf.Clamp(p.x, 0f, Screen.width - 1f), Mathf.Clamp(p.y, 0f, Screen.height - 1f));
    }

    // CORRIGÉ (2026-10-28, retour utilisateur : "quand on switch de la souris à la manette, ça reprend là où c'était avant
    // le switch [...] il faut que quand ça switch, peu importe le curseur, qu'il soit au même endroit") - passage à la
    // manette : le rond part de là où était la souris ; retour à la souris : la vraie souris est replacée sur le rond.
    // Le rond simplement masqué en restant à la manette (retour en partie) ne touche pas à la souris - la replacer
    // ferait croire au jeu qu'on vient de la bouger - et réapparaît là où il était.
    private bool _keepPosition;

    private void Activate()
    {
        Active = true;
        if (!_keepPosition)
        {
            Mouse real = RealMouse();
            Vector2 mouse = real != null ? real.position.ReadValue() : (Vector2)Input.mousePosition;
            Position = new Vector2(Mathf.Clamp(mouse.x, 0f, Screen.width - 1f), Mathf.Clamp(mouse.y, 0f, Screen.height - 1f));
        }
        if (_mouse == null || !_mouse.added) _mouse = InputSystem.AddDevice<Mouse>("PadCursorMouse");
        _sentPos = new Vector2(-1f, -1f);   // force l'envoi de la position
        _cursor.gameObject.SetActive(true);
    }

    private void Deactivate()
    {
        Send(false);
        ExitScroll();
        _keepPosition = GameInput.UsingGamepad;
        Mouse real = RealMouse();
        if (!_keepPosition && real != null) real.WarpCursorPosition(Position);
        Active = false;
        _hoverScroll = null;
        _cursor.gameObject.SetActive(false);
        ShowHint(PadButton.None, null, Vector2.zero, false);
    }

    private static Mouse RealMouse()
    {
        foreach (InputDevice d in InputSystem.devices)
            if (d is Mouse m && m != VirtualMouse) return m;
        return null;
    }

    // Position et bouton gauche de la souris virtuelle (seulement quand quelque chose change).
    private void Send(bool held)
    {
        if (_mouse == null || !_mouse.added) return;
        if (Position == _sentPos && held == _buttonDown) return;
        MouseState st = new MouseState { position = Position, delta = _sentPos.x < 0f ? Vector2.zero : Position - _sentPos };
        if (held) st = st.WithButton(MouseButton.Left);
        InputSystem.QueueStateEvent(_mouse, st);
        _sentPos = Position;
        _buttonDown = held;
    }

    // ---- ce qu'il y a sous le curseur -------------------------------------------------------------------------------
    private void Probe()
    {
        _overControl = false;
        _hoverScroll = null;
        if (_probe == null || _probeOwner != EventSystem.current)
        {
            _probeOwner = EventSystem.current;
            _probe = new PointerEventData(_probeOwner);
        }
        _probe.position = Position;
        _hits.Clear();
        EventSystem.current.RaycastAll(_probe, _hits);
        if (_hits.Count == 0) return;

        Transform top = _hits[0].gameObject.transform;
        ScrollRect scroll = top.GetComponentInParent<ScrollRect>();
        // un bouton (ou curseur, interrupteur...) entre le point visé et la zone qui défile : c'est lui qu'on vise
        for (Transform t = top; t != null && (scroll == null || t != scroll.transform); t = t.parent)
        {
            if (t.TryGetComponent(out Selectable s) && s.isActiveAndEnabled)
            {
                _overControl = s.interactable;
                return;
            }
        }
        if (scroll != null && Scrollable(scroll)) _hoverScroll = scroll;
    }

    private static bool Scrollable(ScrollRect s)
    {
        if (!s.vertical || s.content == null) return false;
        RectTransform view = s.viewport != null ? s.viewport : (RectTransform)s.transform;
        return s.content.rect.height > view.rect.height + 1f;
    }

    // ---- mode défilement ------------------------------------------------------------------------------------------
    private void EnterScroll(ScrollRect s)
    {
        _scroll = s;
        _frame.gameObject.SetActive(true);
    }

    private void ExitScroll()
    {
        _scroll = null;
        if (_frame != null) _frame.gameObject.SetActive(false);
    }

    private static void ScrollBy(ScrollRect s, float stickY, float dt)
    {
        if (Mathf.Abs(stickY) < 0.01f) return;
        RectTransform content = s.content;
        RectTransform view = s.viewport != null ? s.viewport : (RectTransform)s.transform;
        float max = Mathf.Max(0f, content.rect.height - view.rect.height);
        Vector2 p = content.anchoredPosition;
        p.y = Mathf.Clamp(p.y - stickY * ScrollSpeed * dt, 0f, max);   // stick vers le haut : on remonte la liste
        content.anchoredPosition = p;
        s.velocity = Vector2.zero;
    }

    // ---- visuels ---------------------------------------------------------------------------------------------------
    private void UpdateVisuals()
    {
        float scale = Screen.height / 1080f;
        _cursor.anchoredPosition = Position;
        _cursor.sizeDelta = Vector2.one * CursorSize * scale * (_buttonDown ? 0.82f : 1f);
        _cursorImage.color = new Color(1f, 1f, 1f, _scroll != null ? 0.35f : 1f);

        if (_scroll != null)
        {
            RectTransform view = _scroll.viewport != null ? _scroll.viewport : (RectTransform)_scroll.transform;
            if (ScreenRect(view, out Rect r))
            {
                float pad = 6f * scale;
                _frame.anchoredPosition = r.center;
                _frame.sizeDelta = r.size + Vector2.one * pad * 2f;
                _frameImage.color = new Color(1f, 0.79f, 0.24f, 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 5f));
                ShowHint(PadButton.East, Loc.T("Quitter le défilement"), new Vector2(r.center.x, r.yMin - pad), true);
            }
        }
        else if (_hoverScroll != null)
            ShowHint(PadButton.South, Loc.T("Faire défiler"), Position + new Vector2(30f, -30f) * scale, false);
        else
            ShowHint(PadButton.None, null, Vector2.zero, false);
    }

    // Bulle : icône du bouton + texte. centered = centrée sur `at` (sous la zone), sinon accrochée par son coin haut-gauche.
    private void ShowHint(PadButton button, string label, Vector2 at, bool centered)
    {
        bool on = button != PadButton.None;
        if (_hint.gameObject.activeSelf != on) _hint.gameObject.SetActive(on);
        if (!on) return;

        float scale = Screen.height / 1080f;
        float h = 46f * scale, pad = 10f * scale, gap = 8f * scale;
        if (button != _hintButton || label != _hintLabel || Mathf.Abs(_hintGlyph.Rect.sizeDelta.y - (h - 10f * scale)) > 0.5f)
        {
            _hintButton = button;
            _hintLabel = label;
            _hintText.text = label;
            _hintText.fontSize = 24f * scale;
            _hintGlyph.Rect.sizeDelta = new Vector2(h - 10f * scale, h - 10f * scale);
        }
        float gw = _hintGlyph.Show(button, GameInput.PadStyle);
        float tw = _hintText.GetPreferredValues(label).x;
        float w = pad + gw + gap + tw + pad * 1.6f;
        _hint.sizeDelta = new Vector2(w, h);
        _hintGlyph.Rect.anchoredPosition = new Vector2(pad, 0f);
        _hintText.rectTransform.anchoredPosition = new Vector2(pad + gw + gap, 0f);
        _hintText.rectTransform.sizeDelta = new Vector2(tw + 4f, h);

        Vector2 pos = centered ? at - new Vector2(w * 0.5f, h * 0.5f) : at - new Vector2(0f, h);
        pos.x = Mathf.Clamp(pos.x, 0f, Screen.width - w);
        pos.y = Mathf.Clamp(pos.y, 0f, Screen.height - h);
        _hint.anchoredPosition = pos;
    }

    private static readonly Vector3[] Corners = new Vector3[4];

    private static bool ScreenRect(RectTransform rt, out Rect rect)
    {
        rect = default;
        Canvas c = rt.GetComponentInParent<Canvas>();
        if (c == null) return false;
        Camera cam = c.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : c.rootCanvas.worldCamera;
        rt.GetWorldCorners(Corners);
        Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, Corners[0]);
        Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, Corners[2]);
        rect = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        return true;
    }

    private void BuildVisuals()
    {
        GameObject canvasGo = new GameObject("PadCursorCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32010;   // au-dessus de toute interface, y compris la fenêtre Quitter (32000)

        _frame = NewRect("ScrollFrame", canvasGo.transform, new Vector2(0.5f, 0.5f));
        _frameImage = _frame.gameObject.AddComponent<Image>();
        _frameImage.sprite = MakeSprite(64, true);
        _frameImage.type = Image.Type.Sliced;
        _frameImage.raycastTarget = false;
        _frame.gameObject.SetActive(false);

        _hint = NewRect("Hint", canvasGo.transform, Vector2.zero);
        Image bg = _hint.gameObject.AddComponent<Image>();
        bg.sprite = HudSprites.Pill;
        bg.type = Image.Type.Sliced;
        bg.pixelsPerUnitMultiplier = 1.5f;   // même réglage que les pastilles de la légende du HUD
        bg.color = new Color(0.05f, 0.06f, 0.08f, 0.92f);
        bg.raycastTarget = false;
        _hintGlyph = PadGlyph.Create(_hint, 36f);
        _hintGlyph.Rect.anchorMin = _hintGlyph.Rect.anchorMax = _hintGlyph.Rect.pivot = new Vector2(0f, 0.5f);
        _hintText = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        RectTransform trt = _hintText.rectTransform;
        trt.SetParent(_hint, false);
        trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0f, 0.5f);
        if (GameFonts.Body != null) { _hintText.font = GameFonts.Body; _hintText.fontSharedMaterial = GameFonts.Body.material; }
        _hintText.color = new Color(0.96f, 0.89f, 0.72f, 1f);
        _hintText.alignment = TextAlignmentOptions.MidlineLeft;
        _hintText.textWrappingMode = TextWrappingModes.NoWrap;
        _hintText.raycastTarget = false;
        _hint.gameObject.SetActive(false);

        _cursor = NewRect("Cursor", canvasGo.transform, new Vector2(0.5f, 0.5f));
        _cursorImage = _cursor.gameObject.AddComponent<Image>();
        _cursorImage.sprite = MakeSprite(96, false);
        _cursorImage.raycastTarget = false;
        _cursor.gameObject.SetActive(false);
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 pivot)
    {
        RectTransform rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = Vector2.zero;   // positions en pixels d'écran (le canevas n'a pas de CanvasScaler)
        rt.pivot = pivot;
        return rt;
    }

    // frame = cadre arrondi blanc (teinté, découpé en 9) ; sinon curseur : anneau blanc cerné de sombre + point central.
    private static Sprite MakeSprite(int size, bool frame)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Trilinear;
        Color32[] px = new Color32[size * size];
        float c = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f - c, y + 0.5f - c);
                Color col;
                if (frame)
                {
                    const float radius = 14f, stroke = 4f;
                    float qx = Mathf.Abs(p.x) - (c - radius - 1f), qy = Mathf.Abs(p.y) - (c - radius - 1f);
                    float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    col = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + stroke + 0.5f));
                }
                else
                {
                    float r = p.magnitude;
                    float ring = Mathf.Clamp01(0.5f - Mathf.Abs(r - size * 0.36f) + size * 0.045f);   // anneau blanc
                    float shadow = Mathf.Clamp01(0.5f - Mathf.Abs(r - size * 0.36f) + size * 0.085f); // contour sombre
                    float dot = Mathf.Clamp01(size * 0.07f + 0.5f - r);                              // point central
                    float white = Mathf.Max(ring, dot);
                    float alpha = Mathf.Max(white, shadow * 0.75f, Mathf.Clamp01(size * 0.1f + 0.5f - r) * 0.75f);
                    Color dark = new Color(0.08f, 0.07f, 0.1f, 1f);
                    col = Color.Lerp(dark, Color.white, alpha > 0f ? white / alpha : 0f);
                    col.a = alpha;
                }
                px[y * size + x] = col;
            }
        tex.SetPixels32(px);
        tex.Apply(true, true);
        tex.hideFlags = HideFlags.HideAndDontSave;
        Vector4 border = frame ? new Vector4(18, 18, 18, 18) : Vector4.zero;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    private void OnDestroy()
    {
        if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
    }
}
