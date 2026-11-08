using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// AJOUTÉ (2026-11-08, idée de l'utilisateur : "un bouton Oeil en bas à droite d'un écran dans le menu, pour cacher les
// boutons et les panels pour laisser que le fond d'écran, juste pour l'admirer [...] ça rentabilise le choix précis de
// ces fonds d'écran qui font partie de la même histoire") - « mode contemplation » des menus.
//
// Un petit bouton rond (œil) en bas à droite, sur toutes les pages du menu. Au clic, tout ce qui n'est pas le FOND de la
// page affichée s'efface en fondu : barre d'onglets, cartes, boutons, textes, héros, panneau Dernière partie... Le fond
// et ses effets d'ambiance (lueurs, poussières, feuilles, brumes) restent. Un clic n'importe où, une touche, ou le
// bouton (œil barré) ramène l'interface ; chaque élément retrouve exactement son état d'avant (alpha, clics).
//
// Ce qui reste visible, par page : les enfants nommés comme un fond (MenuBackground, SettingsBackground, PanelBackground,
// Background) ou une ambiance (Ambient), et dans l'arbre des Compétences les calques d'effets FX_* de TreeContainer.
// Les voiles d'assombrissement (Dim) sont retirés eux aussi : le fond s'admire en pleine lumière.
public class MenuViewMode : MonoBehaviour
{
    private static readonly HashSet<string> KeepNames = new HashSet<string>
        { "MenuBackground", "SettingsBackground", "PanelBackground", "Background", "Ambient" };

    private const float Fade = 0.3f;
    private const float ButtonSize = 48f, Margin = 14f;

    private MainMenuManager _menu;
    private RectTransform _canvasRect;
    private Transform _tabBarRoot;
    private RectTransform _button;
    private Image _icon, _buttonBack;
    private GameObject _catcher;
    private Sprite _eyeSprite, _eyeOffSprite;
    private bool _active;
    private Coroutine _fade;

    // CORRIGÉ (2026-11-08, retour utilisateur : "si on appuie plusieurs fois sur le bouton [...] ça peut altérer les
    // éléments cachés, ils paraissent mi-transparents") - on n'écrit plus jamais dans le CanvasGroup d'un élément. Avant, son
    // alpha était mémorisé puis restauré : mémorisé en plein fondu (double clic sur l'œil, clic pendant le fondu d'ouverture
    // de la page ou le repli de Dernière partie), il devenait son état « normal ». Désormais chaque élément reçoit un groupe
    // À NOUS ; s'il a déjà le sien (Unity n'en accepte qu'un par objet), il passe le temps de la contemplation dans un
    // conteneur vide de la taille de la page, et ses propres animations continuent sans interférence.
    private struct Hidden { public CanvasGroup group; public Transform wrapped; }
    private readonly List<Hidden> _hidden = new List<Hidden>();
    private GameObject _hiddenPanel;

    // Arbre des Compétences : ses médaillons ont leur propre CanvasGroup, remis à 1 par leur script à chaque image -> on
    // ne peut pas les estomper un par un. Les calques d'effets FX_* sont donc sortis provisoirement de TreeContainer
    // (même rectangle, même ordre d'affichage), puis TreeContainer est masqué d'un bloc ; tout est remis en place à la sortie.
    private struct Moved { public Transform t; public Transform parent; public int index; }
    private readonly List<Moved> _moved = new List<Moved>();

    public bool Active => _active;

    public void Init(MainMenuManager menu, RectTransform canvasRect, Transform tabBar)
    {
        _menu = menu;
        _canvasRect = canvasRect;
        _tabBarRoot = tabBar;
        while (_tabBarRoot != null && _tabBarRoot.parent != null && _tabBarRoot.parent != canvasRect) _tabBarRoot = _tabBarRoot.parent;
        _eyeSprite = MakeEye(false);
        _eyeOffSprite = MakeEye(true);
        BuildCatcher();
        BuildButton();
    }

    // ---- construction ------------------------------------------------------------------------------------------------
    private void BuildCatcher()
    {
        _catcher = new GameObject("ViewModeCatcher", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rt = (RectTransform)_catcher.transform;
        rt.SetParent(_canvasRect, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = _catcher.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);       // invisible, mais attrape les clics
        var b = _catcher.GetComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        b.onClick.AddListener(Exit);
        ButtonHoverFX.Exclude(b);
        _catcher.SetActive(false);
    }

    private void BuildButton()
    {
        var go = new GameObject("ViewModeButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        _button = (RectTransform)go.transform;
        _button.SetParent(_canvasRect, false);
        _button.anchorMin = _button.anchorMax = _button.pivot = new Vector2(1f, 0f);
        _button.anchoredPosition = new Vector2(-Margin, Margin);
        _button.sizeDelta = new Vector2(ButtonSize, ButtonSize);
        _buttonBack = go.GetComponent<Image>();
        _buttonBack.sprite = HudSprites.Disc;
        _buttonBack.color = new Color(0.06f, 0.05f, 0.08f, 0.55f);

        var ring = new GameObject("Ring", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        ring.transform.SetParent(_button, false);
        Stretch(ring.rectTransform, 0f);
        ring.sprite = HudSprites.Ring;
        ring.color = new Color(0.86f, 0.72f, 0.44f, 0.75f);
        ring.raycastTarget = false;

        _icon = new GameObject("Eye", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        _icon.transform.SetParent(_button, false);
        Stretch(_icon.rectTransform, 10f);
        _icon.sprite = _eyeSprite;
        _icon.preserveAspect = true;
        _icon.color = new Color(0.95f, 0.88f, 0.72f, 1f);
        _icon.raycastTarget = false;

        var btn = go.GetComponent<Button>();
        var cb = btn.colors;
        cb.normalColor = Color.white; cb.highlightedColor = new Color(1.25f, 1.15f, 1f, 1f); cb.pressedColor = new Color(0.8f, 0.75f, 0.7f, 1f);
        cb.selectedColor = Color.white; cb.colorMultiplier = 1.2f; cb.fadeDuration = 0.1f;
        btn.colors = cb;
        btn.targetGraphic = ring;
        btn.onClick.AddListener(Toggle);
        _button.SetAsLastSibling();
    }

    private static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
    }

    // Œil dessiné en code (amande + iris + pupille), variante barrée pour « revenir ».
    private static Sprite MakeEye(bool crossed)
    {
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                // amande : deux arcs de cercle (rayon 1,25) décalés verticalement
                float r = 1.25f, cy = r - 0.5f;
                float dTop = Mathf.Sqrt(u * u + (v + cy) * (v + cy)) - r;      // arc supérieur
                float dBot = Mathf.Sqrt(u * u + (v - cy) * (v - cy)) - r;      // arc inférieur
                float almond = Mathf.Max(dTop, dBot);                          // < 0 à l'intérieur
                float outline = 1f - Mathf.Clamp01((Mathf.Abs(almond) - 0.035f) / 0.025f);
                float iris = 1f - Mathf.Clamp01((Mathf.Abs(Mathf.Sqrt(u * u + v * v) - 0.27f) - 0.04f) / 0.025f);
                float pupil = 1f - Mathf.Clamp01((Mathf.Sqrt(u * u + v * v) - 0.12f) / 0.025f);
                float a = Mathf.Max(outline, Mathf.Max(iris, pupil)) * (Mathf.Abs(u) < 0.98f ? 1f : 0f);
                if (crossed)
                {
                    float line = Mathf.Abs(u + v) / 1.4142f;                   // diagonale bas-gauche -> haut-droite
                    float cut = 1f - Mathf.Clamp01((line - 0.10f) / 0.025f);   // espace autour du trait
                    float bar = (1f - Mathf.Clamp01((line - 0.045f) / 0.025f)) * (Mathf.Abs(u - v) < 1.5f ? 1f : 0f);
                    a = Mathf.Max(a * (1f - cut), bar);
                }
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    // ---- bascule -------------------------------------------------------------------------------------------------------
    public void Toggle() { if (_active) Exit(); else Enter(); }

    public void Enter()
    {
        if (_active || _menu == null) return;
        GameObject panel = _menu.CurrentPanel;
        if (panel == null) return;
        _active = true;

        // Retour encore en cours sur cette page (clics rapides) : les éléments sont déjà pris en charge, on repart vers 0.
        if (_hidden.Count > 0 && _hiddenPanel == panel)
        {
            ShowCatcher();
            StartFade(0f);
            return;
        }
        Reveal();
        _hiddenPanel = panel;

        // Copie de la liste : Hide peut déplacer l'élément dans un conteneur. Les éléments inactifs sont pris aussi : une
        // animation peut les allumer pendant la contemplation.
        var children = new List<Transform>();
        foreach (Transform child in panel.transform) children.Add(child);
        foreach (Transform child in children)
        {
            if (KeepNames.Contains(child.name)) continue;
            if (child.name == "TreeContainer") continue;   // traité juste après
            Hide(child);
        }
        Transform tree = panel.transform.Find("TreeContainer");
        if (tree != null && tree.gameObject.activeSelf)
        {
            int at = tree.GetSiblingIndex();
            var fx = new List<Transform>();
            foreach (Transform g in tree) if (g.name.StartsWith("FX_")) fx.Add(g);
            foreach (Transform g in fx)
            {
                _moved.Add(new Moved { t = g, parent = tree, index = g.GetSiblingIndex() });
                g.SetParent(panel.transform, true);
                g.SetSiblingIndex(at++);
            }
            Hide(tree);
        }
        // barre d'onglets : le bandeau de bois reste (le fond des pages ne s'étend pas dessous), seuls les onglets s'effacent
        if (_tabBarRoot != null)
            foreach (Transform tab in _tabBarRoot)
                if (tab.name.StartsWith("Tab") || tab.name.EndsWith("Tab")) Hide(tab);

        ShowCatcher();
        StartFade(0f);
    }

    private void ShowCatcher()
    {
        _catcher.SetActive(true);
        _catcher.transform.SetAsLastSibling();
        _button.SetAsLastSibling();
        _icon.sprite = _eyeOffSprite;
    }

    public void Exit()
    {
        if (!_active) return;
        _active = false;
        _catcher.SetActive(false);
        _icon.sprite = _eyeSprite;
        StartFade(1f);
    }

    private void Hide(Transform t)
    {
        bool ownGroup = t.GetComponent<CanvasGroup>() != null;
        CanvasGroup g = ownGroup ? Wrap(t) : t.gameObject.AddComponent<CanvasGroup>();
        g.interactable = false;
        g.blocksRaycasts = false;
        _hidden.Add(new Hidden { group = g, wrapped = ownGroup ? t : null });
    }

    // Conteneur vide, à la même place dans la page et de même rectangle : l'élément garde exactement sa position.
    private static CanvasGroup Wrap(Transform t)
    {
        var parent = (RectTransform)t.parent;
        var wrap = (RectTransform)new GameObject("ViewModeWrap", typeof(RectTransform), typeof(CanvasGroup)).transform;
        wrap.SetParent(parent, false);
        wrap.anchorMin = Vector2.zero; wrap.anchorMax = Vector2.one; wrap.pivot = parent.pivot;
        wrap.offsetMin = wrap.offsetMax = Vector2.zero;
        wrap.SetSiblingIndex(t.GetSiblingIndex());
        t.SetParent(wrap, false);
        return wrap.GetComponent<CanvasGroup>();
    }

    // Rend l'interface d'un coup : retire nos groupes et conteneurs (les groupes des éléments n'ont jamais été touchés).
    private void Reveal()
    {
        foreach (var h in _hidden)
        {
            if (h.group == null) continue;
            h.group.alpha = 1f;
            if (h.wrapped != null)
            {
                Transform wrap = h.group.transform;
                h.wrapped.SetParent(wrap.parent, false);
                h.wrapped.SetSiblingIndex(wrap.GetSiblingIndex());
                Destroy(wrap.gameObject);
            }
            else Destroy(h.group);
        }
        _hidden.Clear();
        _hiddenPanel = null;
        RestoreMoved();
    }

    private void StartFade(float target)
    {
        if (_fade != null) StopCoroutine(_fade);
        _fade = StartCoroutine(FadeRoutine(target));
    }

    private IEnumerator FadeRoutine(float target)
    {
        var start = new float[_hidden.Count];
        for (int i = 0; i < _hidden.Count; i++) start[i] = _hidden[i].group != null ? _hidden[i].group.alpha : 0f;
        for (float t = 0f; t < Fade; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / Fade);
            for (int i = 0; i < _hidden.Count; i++)
                if (_hidden[i].group != null) _hidden[i].group.alpha = Mathf.Lerp(start[i], target, k);
            // le bouton lui-même se fait discret en contemplation
            _buttonBack.color = new Color(0.06f, 0.05f, 0.08f, Mathf.Lerp(_buttonBack.color.a, target <= 0f ? 0.25f : 0.55f, k));
            yield return null;
        }
        if (target <= 0f) { foreach (var h in _hidden) if (h.group != null) h.group.alpha = 0f; }
        else Reveal();
        _fade = null;
    }

    private void Update()
    {
        if (!_active) return;
        // une touche (clavier ou manette) ramène l'interface ; les clics passent par le capteur plein écran
        if (Input.anyKeyDown && !Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2)) Exit();
        else if (Input.GetMouseButtonDown(1)) Exit();
    }

    private void OnDisable()
    {
        // sécurité : ne jamais laisser l'interface cachée si le menu se ferme
        if (_active) { _active = false; if (_catcher != null) _catcher.SetActive(false); }
        _fade = null;   // Unity a arrêté la coroutine en désactivant le composant
        if (!gameObject.scene.isLoaded) { _hidden.Clear(); _moved.Clear(); return; }   // scène déchargée : tout part avec elle
        Reveal();
    }

    private void RestoreMoved()
    {
        // ordre inverse : chaque calque retrouve son indice d'origine
        for (int i = _moved.Count - 1; i >= 0; i--)
        {
            var m = _moved[i];
            if (m.t == null || m.parent == null) continue;
            m.t.SetParent(m.parent, true);
        }
        _moved.Sort((a, b) => a.index.CompareTo(b.index));
        foreach (var m in _moved) if (m.t != null && m.parent != null) m.t.SetSiblingIndex(m.index);
        _moved.Clear();
    }
}
