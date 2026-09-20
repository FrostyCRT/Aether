using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// AJOUTE (2026-09-19) - PAGE PARAMÈTRES (refonte complète ; remplace SettingsManager).
//
// Principe : un CARROUSEL de catégories (Affichage, Graphismes, Audio, Jeu, Interface, Commandes) que l'on parcourt
// avec les flèches (comme la page Personnages), les pastilles de catégorie, PageHaut/PageBas ou les gâchettes
// LB/RB d'une manette. Chaque catégorie est une liste défilante de lignes construites à partir de SettingsSchema
// (données) : la page ne connaît aucun réglage en particulier.
//
// Tout s'applique EN DIRECT (SettingsApplier écoute GameSettings.Changed). Exceptions gérées ici :
//  - changement d'affichage (mode / résolution) : fenêtre "Conserver ces paramètres ?" avec retour automatique
//    au bout de 12 s, comme dans les grands jeux (si le nouvel affichage est illisible, le joueur n'a rien à faire) ;
//  - configuration d'une touche : fenêtre de capture (Échap = annuler, Retour arrière = effacer) et gestion des
//    conflits (une touche n'appartient jamais à deux actions) ;
//  - remise à zéro d'une page / de tout, quitter le jeu : fenêtre de confirmation.
//
// La hiérarchie est dans la scène (créée par l'outil d'éditeur Aether > Rebuild Settings Page, modifiable à la
// main) ; ce composant ne fait que la remplir.
public class SettingsPage : MonoBehaviour
{
    [Header("En-tête / carrousel")]
    [SerializeField] private TextMeshProUGUI _title;
    [SerializeField] private TextMeshProUGUI _subtitle;
    [SerializeField] private Button _arrowLeft;
    [SerializeField] private Button _arrowRight;
    [SerializeField] private RectTransform _tabsRoot;
    [SerializeField] private Button _tabTemplate;

    [Header("Liste")]
    [SerializeField] private ScrollRect _scroll;
    [SerializeField] private RectTransform _content;
    [SerializeField] private CanvasGroup _contentGroup;
    [SerializeField] private SettingsRow _rowTemplate;
    [SerializeField] private RectTransform _headerTemplate;
    [SerializeField] private RectTransform _noteTemplate;

    [Header("Pied de page")]
    [SerializeField] private Button _resetPageButton;
    [SerializeField] private Button _resetAllButton;
    [SerializeField] private Button _quitButton;

    [Header("Liste déroulante des lignes « choix »")]
    [SerializeField] private SettingsDropdown _dropdown;

    [Header("Message")]
    [SerializeField] private CanvasGroup _toastGroup;
    [SerializeField] private TextMeshProUGUI _toastText;

    [Header("Fenêtre : capture d'une touche")]
    [SerializeField] private GameObject _captureRoot;
    [SerializeField] private TextMeshProUGUI _captureTitle;
    [SerializeField] private TextMeshProUGUI _captureHint;

    [Header("Fenêtre : confirmation")]
    [SerializeField] private GameObject _confirmRoot;
    [SerializeField] private TextMeshProUGUI _confirmTitle;
    [SerializeField] private TextMeshProUGUI _confirmBody;
    [SerializeField] private Button _confirmYes;
    [SerializeField] private TextMeshProUGUI _confirmYesText;
    [SerializeField] private Button _confirmNo;

    [Header("Fenêtre : conserver l'affichage ?")]
    [SerializeField] private GameObject _revertRoot;
    [SerializeField] private TextMeshProUGUI _revertText;
    [SerializeField] private Button _revertKeep;
    [SerializeField] private Button _revertUndo;
    [SerializeField] private float _revertSeconds = 12f;

    [Header("Mode partie (menu pause)")]
    [SerializeField] private bool _inGame;

    // Vrai tant que la page est ouverte depuis le menu pause : GameManager ignore alors la touche Pause (Échap ferme
    // la page, pas la partie en pause). ClosedFrame évite que l'Échap qui ferme la page relance aussitôt la partie.
    public static bool InGameOpen { get; private set; }
    public static event Action InGameClosed;                // levé quand la page (mode partie) se ferme : le menu pause se réaffiche
    public static int ClosedFrame { get; private set; } = -1;

    // Mettre à vrai pour tester la fenêtre "conserver ?" dans l'éditeur (où l'affichage ne change pas vraiment).
    public static bool AlwaysConfirmDisplay;

    private List<CategoryDef> _cats;
    private readonly List<SettingsRow> _rows = new List<SettingsRow>();
    private readonly List<Button> _tabs = new List<Button>();
    private int _index;
    private Coroutine _transition, _toast;

    // capture d'une touche
    private bool _capturing;
    private GameAction _capAction;
    private int _capSlot;
    private SettingsRow _capRow;
    private int _capFrame;

    // confirmation d'affichage
    private bool _hasSnapshot;
    private int _snapMode;
    private string _snapResolution;
    private float _revertTimer;
    private bool _revertOpen;

    private Action _confirmAction;

    // ---- cycle de vie --------------------------------------------------------------------------------------
    private void Awake()
    {
        if (_arrowLeft != null) _arrowLeft.onClick.AddListener(() => Step(-1));
        if (_arrowRight != null) _arrowRight.onClick.AddListener(() => Step(+1));
        if (_resetPageButton != null) _resetPageButton.onClick.AddListener(AskResetPage);
        if (_resetAllButton != null) _resetAllButton.onClick.AddListener(AskResetAll);
        if (_quitButton != null) _quitButton.onClick.AddListener(AskQuit);
        if (_inGame) { MakeQuitButtonBackButton(); ApplyInGameLook(); }
        if (_confirmNo != null) _confirmNo.onClick.AddListener(CloseConfirm);
        if (_confirmYes != null) _confirmYes.onClick.AddListener(() => { Action a = _confirmAction; CloseConfirm(); a?.Invoke(); });
        if (_revertKeep != null) _revertKeep.onClick.AddListener(KeepDisplay);
        if (_revertUndo != null) _revertUndo.onClick.AddListener(UndoDisplay);
    }

    private void OnEnable()
    {
        if (_cats == null) { _cats = SettingsSchema.Build(); BuildTabs(); }
        GameSettings.Changed += OnAnySettingChanged;
        InputBindings.Changed += OnBindingChanged;

        if (_inGame) InGameOpen = true;

        CloseDialogs();
        ShowCategory(Mathf.Clamp(_index, 0, _cats.Count - 1), 0, true);
    }

    private void OnDisable()
    {
        GameSettings.Changed -= OnAnySettingChanged;
        InputBindings.Changed -= OnBindingChanged;
        // fenêtre d'affichage encore ouverte quand on quitte la page : on garde ce que le joueur voit (pas de retour surprise)
        _hasSnapshot = false;
        CancelCapture();
        CloseDialogs();
        GameSettings.Flush();
        InputBindings.Flush();
        if (_inGame) { InGameOpen = false; ClosedFrame = Time.frameCount; InGameClosed?.Invoke(); }
    }

    // En partie, la page ne doit pas ressembler au menu principal : plus de bibliothèque ni de lueurs, la partie en pause
    // reste visible derrière un voile sombre, et une mention « PARTIE EN PAUSE » coiffe le titre.
    private void ApplyInGameLook()
    {
        Image rootImage = GetComponent<Image>();                    // fond plein de la page (gris) : caché pour laisser voir la partie
        if (rootImage != null) rootImage.color = new Color(0f, 0f, 0f, 0f);
        Transform bg = transform.Find("SettingsBackground");
        if (bg != null) bg.gameObject.SetActive(false);
        Transform amb = transform.Find("Ambient");
        if (amb != null) amb.gameObject.SetActive(false);
        Transform dim = transform.Find("Dim");
        if (dim != null)
        {
            Image img = dim.GetComponent<Image>();
            if (img != null) { img.color = new Color(0.02f, 0.015f, 0.04f, 0.80f); img.raycastTarget = true; }   // bloque aussi les clics vers le menu pause
        }
        if (_subtitle != null)
        {
            TextMeshProUGUI badge = Instantiate(_subtitle, _subtitle.transform.parent);
            badge.name = "InGameBadge";
            badge.text = "PARTIE EN PAUSE";
            badge.fontSize = 28;
            badge.color = SettingsStyle.Gold;
            badge.characterSpacing = 10f;
            RectTransform rt = badge.rectTransform;
            rt.anchoredPosition = new Vector2(0f, 494f);
        }
    }

    // En partie, « Quitter le jeu » devient « Retour » (ferme la page, la partie reste en pause).
    private void MakeQuitButtonBackButton()
    {
        if (_quitButton == null) return;
        TextMeshProUGUI label = _quitButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) { label.text = "Retour"; label.color = SettingsStyle.Body; }
        Transform border = _quitButton.transform.Find("Border");
        if (border != null) border.GetComponent<Image>().color = SettingsStyle.AccentSoft;
        Image fill = _quitButton.targetGraphic as Image;
        if (fill != null) fill.color = new Color32(0x2C, 0x25, 0x40, 0xFF);
    }

    private void OnAnySettingChanged(string key) => RefreshRows();
    private void OnBindingChanged(GameAction a) => RefreshRows();

    private void RefreshRows()
    {
        foreach (SettingsRow r in _rows) if (r != null) r.Refresh();
    }

    // ---- carrousel -----------------------------------------------------------------------------------------
    private void BuildTabs()
    {
        if (_tabTemplate == null || _tabsRoot == null) return;
        _tabTemplate.gameObject.SetActive(false);
        for (int i = 0; i < _cats.Count; i++)
        {
            int idx = i;
            Button b = Instantiate(_tabTemplate, _tabsRoot);
            b.gameObject.SetActive(true);
            b.name = "Tab_" + _cats[i].id;
            b.GetComponentInChildren<TextMeshProUGUI>().text = _cats[i].title;
            b.onClick.AddListener(() => Go(idx));
            _tabs.Add(b);
        }
    }

    private void Step(int dir)
    {
        if (_cats == null || DialogOpen) return;
        int next = (_index + dir + _cats.Count) % _cats.Count;
        ShowCategory(next, dir, false);
    }

    private void Go(int index)
    {
        if (DialogOpen || index == _index) return;
        ShowCategory(index, index > _index ? 1 : -1, false);
    }

    private void ShowCategory(int index, int direction, bool instant)
    {
        _index = index;
        if (_dropdown != null) _dropdown.Close();
        CategoryDef cat = _cats[index];
        _title.text = cat.title;
        _subtitle.text = cat.subtitle;

        // pastilles : la catégorie courante est pleine
        for (int i = 0; i < _tabs.Count; i++)
        {
            bool sel = i == index;
            Image bg = _tabs[i].targetGraphic as Image;
            TextMeshProUGUI t = _tabs[i].GetComponentInChildren<TextMeshProUGUI>();
            if (bg != null) bg.color = sel ? SettingsStyle.Accent : SettingsStyle.Field;
            if (t != null) t.color = sel ? Color.white : SettingsStyle.Muted;
        }

        // lignes
        for (int i = _content.childCount - 1; i >= 0; i--)
        {
            Transform child = _content.GetChild(i);
            child.SetParent(null, false);           // sort tout de suite de la mise en page (Destroy n'agit qu'en fin d'image)
            Destroy(child.gameObject);
        }
        _rows.Clear();
        foreach (RowDef def in cat.rows)
        {
            if (def.kind == RowKind.Header)
            {
                RectTransform h = Instantiate(_headerTemplate, _content);
                h.gameObject.SetActive(true);
                h.GetComponentInChildren<TextMeshProUGUI>().text = def.label;
            }
            else if (def.kind == RowKind.Note)
            {
                RectTransform n = Instantiate(_noteTemplate, _content);
                n.gameObject.SetActive(true);
                n.GetComponentInChildren<TextMeshProUGUI>().text = def.label;
            }
            else
            {
                SettingsRow row = Instantiate(_rowTemplate, _content);
                row.gameObject.SetActive(true);
                row.Bind(def, this);
                _rows.Add(row);
                // une ligne sans description est plus basse (les autres gardent 2 lignes de texte)
                LayoutElement le = row.GetComponent<LayoutElement>();
                if (le != null) le.preferredHeight = string.IsNullOrEmpty(def.description) ? 66f : 88f;
            }
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        if (_scroll != null)
        {
            _scroll.verticalNormalizedPosition = 1f;
            UpdateScrollbarVisibility();
        }

        if (_transition != null) StopCoroutine(_transition);
        if (instant || !isActiveAndEnabled)
        {
            if (_contentGroup != null) _contentGroup.alpha = 1f;
            _content.anchoredPosition = new Vector2(0f, _content.anchoredPosition.y);
        }
        else
        {
            _transition = StartCoroutine(TransitionRoutine(direction));
        }
    }

    private IEnumerator TransitionRoutine(int direction)
    {
        const float dur = 0.24f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / dur);
            if (_contentGroup != null) _contentGroup.alpha = k;
            _content.anchoredPosition = new Vector2(Mathf.Lerp(70f * direction, 0f, k), _content.anchoredPosition.y);
            yield return null;
        }
        if (_contentGroup != null) _contentGroup.alpha = 1f;
        _content.anchoredPosition = new Vector2(0f, _content.anchoredPosition.y);
    }

    private bool DialogOpen => _capturing || (_confirmRoot != null && _confirmRoot.activeSelf) || _revertOpen;

    // Ouvre la liste des valeurs sous `anchor` ; un second clic sur le même bouton la referme.
    public void ToggleDropdown(RectTransform anchor, string[] options, int current, Action<int> onPick)
    {
        if (_dropdown == null || DialogOpen) return;
        if (_dropdown.IsOpen && _dropdown.Anchor == anchor) { _dropdown.Close(); return; }
        _dropdown.Open(anchor, options, current, onPick);
    }

    private void CloseDialogs()
    {
        if (_dropdown != null) _dropdown.Close();
        if (_captureRoot != null) _captureRoot.SetActive(false);
        if (_confirmRoot != null) _confirmRoot.SetActive(false);
        if (_revertRoot != null) _revertRoot.SetActive(false);
        _revertOpen = false;
        if (_toastGroup != null) _toastGroup.alpha = 0f;
    }

    // ---- boucle --------------------------------------------------------------------------------------------
    private void LateUpdate()
    {
        UpdateScrollbarVisibility();
    }

    // Barre de défilement visible seulement si la liste dépasse la zone visible. On joue sur la TRANSPARENCE (CanvasGroup),
    // pas sur SetActive : en mode « Permanent », le ScrollRect réactive lui-même la barre à chaque image (c'est ce qui la
    // laissait affichée sur les pages courtes).
    private CanvasGroup _scrollbarGroup;

    private void UpdateScrollbarVisibility()
    {
        if (_scroll == null || _scroll.verticalScrollbar == null || _scroll.viewport == null || _content == null) return;
        bool needed = _content.rect.height > _scroll.viewport.rect.height + 1f;
        if (_scrollbarGroup == null)
        {
            _scrollbarGroup = _scroll.verticalScrollbar.GetComponent<CanvasGroup>();
            if (_scrollbarGroup == null) _scrollbarGroup = _scroll.verticalScrollbar.gameObject.AddComponent<CanvasGroup>();
        }
        _scrollbarGroup.alpha = needed ? 1f : 0f;
        _scrollbarGroup.blocksRaycasts = needed;
        _scrollbarGroup.interactable = needed;
    }

    private void Update()
    {
        if (_capturing) { UpdateCapture(); return; }

        if (_revertOpen)
        {
            _revertTimer -= Time.unscaledDeltaTime;
            _revertText.text = "Retour automatique à l'ancien affichage dans " + Mathf.CeilToInt(Mathf.Max(0f, _revertTimer)) + " s";
            if (_revertTimer <= 0f) UndoDisplay();
            return;
        }

        if (DialogOpen) return;

        // en partie : Échap (ou la touche Pause) referme la page, sauf si une liste vient de se fermer avec cette même frappe
        if (_inGame && (Input.GetKeyDown(KeyCode.Escape) || GameInput.Down(GameAction.Pause))
            && !(_dropdown != null && (_dropdown.IsOpen || _dropdown.ClosedFrame == Time.frameCount)))
        {
            gameObject.SetActive(false);
            return;
        }

        // raccourcis : PageHaut / PageBas, gâchettes LB / RB
        if (Input.GetKeyDown(KeyCode.PageUp) || Input.GetKeyDown(KeyCode.JoystickButton4)) Step(-1);
        else if (Input.GetKeyDown(KeyCode.PageDown) || Input.GetKeyDown(KeyCode.JoystickButton5)) Step(+1);
    }

    // ---- notification d'une ligne ------------------------------------------------------------------------------------
    public void OnRowChanged(SettingsRow row)
    {
        RefreshRows();
        if (row != null && row.Def != null && row.Def.affectsDisplay) OpenRevertIfNeeded();
    }

    // Appelé AVANT qu'une ligne change un réglage d'affichage : mémorise l'état précédent (une seule fois).
    public void BeforeDisplayChange(RowDef def)
    {
        if (def == null || !def.affectsDisplay || _hasSnapshot) return;
        _hasSnapshot = true;
        _snapMode = GameSettings.GetInt(GameSettings.DisplayMode);
        _snapResolution = GameSettings.GetString(GameSettings.Resolution);
    }

    private void OpenRevertIfNeeded()
    {
        if (!_hasSnapshot) return;
        if (Application.isEditor && !AlwaysConfirmDisplay) { _hasSnapshot = false; return; }   // rien ne change dans l'éditeur
        _revertOpen = true;
        _revertTimer = _revertSeconds;
        _revertRoot.SetActive(true);
    }

    private void KeepDisplay()
    {
        _hasSnapshot = false;
        _revertOpen = false;
        _revertRoot.SetActive(false);
        Toast("Affichage conservé.");
    }

    private void UndoDisplay()
    {
        _revertOpen = false;
        _revertRoot.SetActive(false);
        if (_hasSnapshot)
        {
            GameSettings.SetInt(GameSettings.DisplayMode, _snapMode);
            GameSettings.SetString(GameSettings.Resolution, _snapResolution);
            _hasSnapshot = false;
            Toast("Ancien affichage rétabli.");
        }
        RefreshRows();
    }

    // ---- capture d'une touche -------------------------------------------------------------------------------------
    public void BeginCapture(GameAction action, int slot, SettingsRow row)
    {
        if (DialogOpen) return;
        if (_dropdown != null) _dropdown.Close();
        _capturing = true;
        _capAction = action; _capSlot = slot; _capRow = row;
        _capFrame = Time.frameCount;

        _captureTitle.text = GameInput.ActionLabel(action).ToUpperInvariant();
        _captureHint.text = "Appuie sur une touche, un bouton de souris ou de manette\n<color=#9C93AE>Échap : annuler     Retour arrière : effacer</color>";
        _captureRoot.SetActive(true);
        if (row != null) row.SetCaptureVisual(slot, true);
    }

    private void CancelCapture()
    {
        if (!_capturing) return;
        _capturing = false;
        if (_captureRoot != null) _captureRoot.SetActive(false);
        if (_capRow != null) _capRow.SetCaptureVisual(-1, false);
        _capRow = null;
    }

    private void UpdateCapture()
    {
        if (Time.frameCount <= _capFrame) return;            // la frappe qui a ouvert la capture ne compte pas
        if (Input.GetMouseButtonDown(0)) { CancelCapture(); return; }

        GameInput.Capture c = GameInput.PollCapture(out KeyCode key);
        if (c == GameInput.Capture.None) return;

        if (c == GameInput.Capture.Cancel) { CancelCapture(); return; }

        KeyCode assigned = c == GameInput.Capture.Clear ? KeyCode.None : key;
        KeyCode previous = InputBindings.Get(_capAction, _capSlot);
        InputBindings.RebindResult r = InputBindings.Rebind(_capAction, _capSlot, assigned);
        GameAction action = _capAction;
        CancelCapture();

        if (c == GameInput.Capture.Clear && !r.applied)
        {
            Toast("Impossible : « " + GameInput.ActionLabel(action) + " » doit garder au moins une touche.");
        }
        else if (r.blocked)
        {
            Toast("« " + GameInput.KeyLabel(key) + " » est la seule touche de « " + GameInput.ActionLabel(r.displaced) + " » : libère-la d'abord.");
        }
        else if (r.hasDisplaced)
        {
            if (previous != KeyCode.None)
                Toast("« " + GameInput.KeyLabel(key) + " » était utilisée par « " + GameInput.ActionLabel(r.displaced) + " » : elles échangent leurs touches (« " + GameInput.KeyLabel(previous) + " »).");
            else
                Toast("« " + GameInput.KeyLabel(key) + " » était utilisée par « " + GameInput.ActionLabel(r.displaced) + " » : elle lui est retirée.");
        }
        else if (r.applied && c == GameInput.Capture.Key)
        {
            Toast("« " + GameInput.ActionLabel(action) + " » : " + GameInput.KeyLabel(key));
        }
        RefreshRows();
    }

    // ---- pied de page ---------------------------------------------------------------------------------------------
    private void AskResetPage()
    {
        CategoryDef cat = _cats[_index];
        Confirm("RÉINITIALISER « " + cat.title + " » ?", "Les réglages de cette page reprennent leur valeur d'origine.", "Réinitialiser", () =>
        {
            SettingsSchema.ResetCategory(cat);
            RefreshRows();
            Toast("Page « " + cat.title + " » réinitialisée.");
        });
    }

    private void AskResetAll()
    {
        Confirm("TOUT RÉINITIALISER ?", "Tous les réglages et toutes les touches reprennent leur valeur d'origine.", "Tout réinitialiser", () =>
        {
            GameSettings.ResetAll();
            ShowCategory(_index, 0, true);
            Toast("Tous les paramètres ont été réinitialisés.");
        });
    }

    private void AskQuit()
    {
        if (_inGame) { gameObject.SetActive(false); return; }
        Confirm("QUITTER LE JEU ?", "La partie en cours de préparation sera fermée. Tes réglages sont enregistrés.", "Quitter", () =>
        {
            GameSettings.Flush();
            InputBindings.Flush();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });
    }

    private void Confirm(string title, string body, string yesLabel, Action onYes)
    {
        if (DialogOpen) return;
        if (_dropdown != null) _dropdown.Close();
        _confirmTitle.text = title;
        _confirmBody.text = body;
        _confirmYesText.text = yesLabel;
        _confirmAction = onYes;
        _confirmRoot.SetActive(true);
    }

    private void CloseConfirm()
    {
        _confirmAction = null;
        _confirmRoot.SetActive(false);
    }

    // ---- message court -------------------------------------------------------------------------------------------
    public void Toast(string message)
    {
        if (_toastGroup == null) return;
        _toastText.text = message;
        if (_toast != null) StopCoroutine(_toast);
        _toast = StartCoroutine(ToastRoutine());
    }

    private IEnumerator ToastRoutine()
    {
        _toastGroup.alpha = 0f;
        float t = 0f;
        while (t < 0.15f) { t += Time.unscaledDeltaTime; _toastGroup.alpha = t / 0.15f; yield return null; }
        _toastGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(3.2f);
        t = 0f;
        while (t < 0.5f) { t += Time.unscaledDeltaTime; _toastGroup.alpha = 1f - t / 0.5f; yield return null; }
        _toastGroup.alpha = 0f;
    }
}
