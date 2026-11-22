using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// AJOUTÉ (2026-11-16, didacticiel) - VISITE GUIDÉE DU MENU, au tout premier lancement (ou depuis le bouton « ? » de l'Accueil).
// REFAIT (2026-11-16, retours utilisateur : « il manque d'interactif », « évite de parler de trop d'éléments d'un coup ») :
// le joueur clique lui-même sur les onglets, les flèches, « Sélectionner », le Grimoire et un nœud de l'arbre ; chaque étape
// ne dit que l'essentiel. Le décor de l'Accueil est vide jusqu'au choix du héros, puis Aether arrive en fondu.
// REFAIT (2026-11-16, retours 3) : visites PAR PAGE (lancées à la 1re ouverture de chaque onglet), Jouer grisé tant qu'aucun héros
// n'est choisi, plus d'étapes « ouvrez puis fermez ». À la fin de la visite principale : une partie d'ENTRAÎNEMENT guidée (les conseils de partie n'existent que là) ou « Je me débrouille ».
public class MenuTutorial : MonoBehaviour
{
    private MainMenuManager _menu;
    private Transform _canvas;
    private readonly List<TutorialStep> _steps = new List<TutorialStep>();
    private int _index;
    private bool _running;

    public static bool Running { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Running = false; }

    /// <summary>Appelé par MainMenuManager.Start : lance la visite au 1er lancement, ou le conseil « après la 1re partie ».</summary>
    public static void Install(MainMenuManager menu)
    {
        MenuTutorial t = menu.gameObject.AddComponent<MenuTutorial>();
        t._menu = menu;
        t._canvas = menu.CanvasRoot;
        t.BuildHelpButton();
        if (Tutorial.ShouldRunMenuTour) { t._running = true; Running = true; t.StartCoroutine(t.BeginAfterFrame()); }
        else t.StartCoroutine(t.AfterFirstRunTip());
    }

    /// <summary>Bouton « ? » de l'Accueil : revoir le didacticiel.</summary>
    public void Replay()
    {
        if (_running) return;
        // CORRIGÉ (2026-11-18) - pendant les ~0,6 s d'attente avant Begin(), Update() (hors visite) voyait « page d'accueil pas encore
        // vue » (Replay vient de tout remettre à zéro) et lançait tout de suite la visite de la page : une fenêtre apparaissait 1 s,
        // puis le choix de langue la remplaçait. La visite est marquée « en cours » dès le clic.
        _running = true;
        Running = true;
        Tutorial.Replay();
        StartCoroutine(BeginAfterFrame());
    }

    private IEnumerator BeginAfterFrame()
    {
        yield return null;   // laisse l'interface finir de s'installer (carrousel, polices)
        yield return new WaitForSecondsRealtime(0.6f);
        if (_menu == null || _canvas == null) { _running = false; Running = false; yield break; }
        if (TutorialOverlay.IsShowing) TutorialOverlay.Get().Dismiss();
        Begin();
    }

    // ---- déroulement ----------------------------------------------------------------------------------------------------
    // Visite principale (1er lancement / « ? ») : langue -> onglet Personnages (visite de la page : choix du héros) -> retour
    // automatique à l'Accueil (visite de la page) -> entraînement. Les autres pages (Compétences, Réputation) ont leur
    // propre visite, lancée à leur première ouverture (Update), une seule fois.
    private Action<bool> _onTourEnd;
    private GameObject _lastPanel;
    private float _panelSince;

    private void Begin()
    {
        _running = true;
        Running = true;
        Tutorial.BeginMenuTour();   // 1er lancement : décor sans personnage jusqu'au choix du héros (rejeu : héros déjà choisi)
        _menu.ShowMenu();
        RunTour(WelcomeSteps(), ok => { if (ok) AfterWelcome(); else SkipAll(); });
    }

    private void AfterWelcome()
    {
        if (!Tutorial.HeroChosen)
            RunTour(new List<TutorialStep> { PointToCharacters() }, ok => { if (ok) StartCharacters(); else SkipAll(); });
        else
            StartHome();
    }

    private void StartCharacters()
    {
        Tutorial.MarkPageSeen(Tutorial.Page.Characters);
        RunTour(CharacterSteps(), ok =>
        {
            if (!ok) { SkipAll(); return; }
            _menu.ShowMenu();   // retour à l'Accueil : Aether apparaît en fondu
            StartHome();
        });
    }

    private void StartHome()
    {
        Tutorial.MarkPageSeen(Tutorial.Page.Home);
        RunTour(HomeSteps(true), ok => End(ok));
    }

    // Lance une suite d'étapes ; onEnd(true) à la dernière étape validée, onEnd(false) si le joueur passe.
    private void RunTour(List<TutorialStep> steps, Action<bool> onEnd)
    {
        _steps.Clear();
        _steps.AddRange(steps);
        _onTourEnd = onEnd;
        Show(0);
    }

    private void Show(int i)
    {
        _index = Mathf.Clamp(i, 0, _steps.Count - 1);
        TutorialStep s = _steps[_index];
        TutorialOverlay.Get().Present(s, _index, _steps.Count, Next, Skip);
    }

    private void Next()
    {
        TutorialOverlay.Get().Dismiss();
        if (_index >= _steps.Count - 1) { Action<bool> cb = _onTourEnd; _onTourEnd = null; cb?.Invoke(true); return; }
        Show(_index + 1);
    }

    private void Skip()
    {
        TutorialOverlay.Get().Dismiss();
        Action<bool> cb = _onTourEnd;
        _onTourEnd = null;
        cb?.Invoke(false);
    }

    // « Passer le didacticiel » : plus aucune visite de page ni conseil de partie.
    private void SkipAll()
    {
        Tutorial.DisableHints();
        End(false);
    }

    private void End(bool practice)
    {
        _running = false;
        Running = false;
        Tutorial.FinishMenuTour();
        _menu.ShowMenu();
        if (!practice) return;
        // Partie d'entraînement : Classique, Modéré (les conseils de partie ne s'y déclenchent que dans ce cas).
        SaveData data = MetaProgressionManager.Instance != null ? MetaProgressionManager.Instance.Data : null;
        GameModes.Current = GameMode.Classic;
        DifficultyTiers.Current = DifficultyTier.Moderate;
        if (!GameModes.CanPlay(GameModes.Current, DifficultyTiers.Current, data)) return;
        Tutorial.RequestPractice();
        _menu.PlayGame();
    }

    // ---- visites de page ------------------------------------------------------------------------------------------------
    private RectTransform Find(string path) => _canvas != null ? _canvas.Find(path) as RectTransform : null;

    private bool OnPanel(GameObject panel) => panel != null && _menu.CurrentPanel == panel;

    // Nœud de départ de la branche du héros choisi (celui qu'on peut regarder sans rien acheter).
    private string FirstNodePath()
    {
        int hero = MetaProgressionManager.Instance != null ? MetaProgressionManager.Instance.GetSelectedCharacterIndex() : 0;
        return "UpgradesPanel/TreeContainer/" + (hero == 1 ? "Node_Vitality" : hero == 2 ? "Node_Dash" : "Node_Concentration");
    }

    private bool NodePanelOpen()
    {
        RectTransform p = Find("UpgradesPanel/NodeDetailPanel");
        return p != null && p.gameObject.activeInHierarchy;
    }

    private List<TutorialStep> WelcomeSteps()
    {
        return new List<TutorialStep>
        {
            new TutorialStep
            {
                Title = () => Loc.T("BIENVENUE"),
                Body = () => Loc.T("Aether Storm Survivor est un jeu de survie : affrontez des hordes de monstres, renforcez votre héros et survivez. Choisissez votre langue pour commencer."),
                LanguagePicker = true,
                NextLabel = () => Loc.T("Commencer"),
            }
        };
    }

    // Aucun héros choisi : Jouer est grisé ; on désigne l'onglet Personnages (le joueur l'ouvre lui-même).
    private TutorialStep PointToCharacters()
    {
        return new TutorialStep
        {
            Title = () => Loc.T("PERSONNAGES"),
            Body = () => Loc.T("Aucun héros n'est sélectionné. Ouvrez l'onglet Personnages pour en choisir un."),
            Target = () => Find("TabBar/TabPlayer"),
            Tight = true,
            RequireAction = true,
            PassClicks = true,
            DoneWhen = () => OnPanel(_menu.CharacterSelectPanelObject),
        };
    }

    private List<TutorialStep> CharacterSteps()
    {
        bool first = !Tutorial.HeroChosen;
        return new List<TutorialStep>
        {
            new TutorialStep
            {
                Title = () => Loc.T("HÉROS"),
                Body = first
                    ? (Func<string>)(() => Loc.T("Chaque héros possède son style de combat et son arbre de compétences. Sélectionnez Aether pour commencer."))
                    : (() => Loc.T("Chaque héros possède son style de combat et son arbre de compétences. Les autres héros se débloquent en progressant.")),
                Target = first ? (Func<RectTransform>)(() => CharacterSelectUI.SelectButtonRect) : null,
                RequireAction = first,
                PassClicks = true,
                DoneWhen = first ? (Func<bool>)(() => Tutorial.HeroChosen) : null,
                Position = TutorialStep.Place.BottomLeft,   // le héros (en haut à gauche) et la fiche (à droite) restent visibles
                Panel = () => _menu.CharacterSelectPanelObject,
            }
        };
    }

    private List<TutorialStep> HomeSteps(bool withPractice)
    {
        List<TutorialStep> l = new List<TutorialStep>
        {
            new TutorialStep
            {
                Title = () => Loc.T("PARTIE"),
                Body = () => Loc.T("Choisissez un mode de jeu et une difficulté, puis lancez la partie avec Jouer. Les difficultés supérieures se débloquent en progressant."),
                Target = () => Find("MenuPanel/HomeCommandPanel"),
                Tight = true,
                Panel = () => _menu.MenuPanelObject,
            },
            new TutorialStep
            {
                Title = () => Loc.T("GRIMOIRE"),
                Body = () => Loc.T("Le Grimoire garde la trace de vos découvertes. Chaque nouvelle découverte vous rapporte une récompense."),
                Target = () => Find("MenuPanel/Grimoire/GrimoireButton"),
                Tight = false,   // (2026-11-22) le rectangle du bouton (livre + mot) : « ajusté » englobait le grand halo et sortait de l'écran
                Padding = 10f,
                Arrow = true,   // le cadre est petit et la carte grande : une flèche animée désigne le bouton
                Panel = () => _menu.MenuPanelObject,
            },
        };
        if (withPractice)
            l.Add(new TutorialStep
            {
                Title = () => Loc.T("ENTRAÎNEMENT"),
                Body = () => Loc.T("Une partie d'entraînement guidée présente les commandes. Vous pouvez aussi lancer directement une partie sans assistance."),
                NextLabel = () => Loc.T("Entraînement"),
                SkipLabel = () => Loc.T("Passer"),
                SkipConfirm = false,
                Panel = () => _menu.MenuPanelObject,
            });
        return l;
    }

    private List<TutorialStep> SkillSteps()
    {
        return new List<TutorialStep>
        {
            new TutorialStep
            {
                Title = () => Loc.T("ARBRE DE COMPÉTENCES"),
                Body = () => Loc.T("Les compétences sont des bonus permanents achetés avec l'Or gagné en partie. Cliquez sur un nœud pour consulter son détail."),
                Target = () => Find(FirstNodePath()),
                Tight = true,
                RequireAction = true,
                PassClicks = true,
                DoneWhen = NodePanelOpen,
                Panel = () => _menu.UpgradesPanelObject,
                SkipLabel = () => Loc.T("Passer"),
                SkipConfirm = false,
                OnExit = () =>
                {
                    RectTransform close = Find("UpgradesPanel/NodeDetailPanel/CloseButton");
                    if (close != null && close.TryGetComponent(out Button b)) b.onClick.Invoke();
                },
            }
        };
    }

    private List<TutorialStep> ReputationSteps()
    {
        return new List<TutorialStep>
        {
            new TutorialStep
            {
                Title = () => Loc.T("RÉPUTATION"),
                Body = () => Loc.T("Ici, vous pouvez acheter des bonus permanents avec vos Éclats."),
                NextLabel = () => Loc.T("Compris"),
                ShowSkip = false,
                Panel = () => _menu.ReputationPanelObject,
            }
        };
    }

    private void Update()
    {
        if (_running)
        {
            // Le joueur ne peut pas quitter la page de l'étape (clavier, manette) : elle est réaffichée.
            if (!TutorialOverlay.IsShowing || _index >= _steps.Count) return;
            TutorialStep s = _steps[_index];
            if (s.Panel == null) return;
            GameObject p = s.Panel();
            if (p != null && _menu.CurrentPanel != p) _menu.ShowPanel(p);
            return;
        }
        // Hors visite principale : à la 1re ouverture d'une page qui a sa visite, elle se lance (une seule fois).
        if (_menu == null || TutorialOverlay.IsShowing || SettingsPage.ModalOpen || QuitConfirm.IsOpen || GrimoireUI.IsOpen) return;
        GameObject cur = _menu.CurrentPanel;
        if (cur != _lastPanel) { _lastPanel = cur; _panelSince = Time.unscaledTime; }
        if (cur == null || Time.unscaledTime - _panelSince < 0.55f) return;

        if (cur == _menu.UpgradesPanelObject && !Tutorial.PageSeen(Tutorial.Page.Skills)) StartPage(Tutorial.Page.Skills, SkillSteps());
        else if (cur == _menu.ReputationPanelObject && !Tutorial.PageSeen(Tutorial.Page.Reputation)) StartPage(Tutorial.Page.Reputation, ReputationSteps());
        else if (cur == _menu.CharacterSelectPanelObject && !Tutorial.PageSeen(Tutorial.Page.Characters)) StartPage(Tutorial.Page.Characters, CharacterSteps());
        else if (cur == _menu.MenuPanelObject && !Tutorial.PageSeen(Tutorial.Page.Home) && Tutorial.HeroChosen) StartPage(Tutorial.Page.Home, HomeSteps(false));
    }

    private void StartPage(Tutorial.Page page, List<TutorialStep> steps)
    {
        Tutorial.MarkPageSeen(page);
        _running = true;
        RunTour(steps, ok =>
        {
            _running = false;
            if (!ok) Tutorial.DisableHints();   // « Passer » : plus aucune visite
        });
    }

    // ---- bouton « ? » (revoir le didacticiel) ---------------------------------------------------------------------------
    private void BuildHelpButton()
    {
        // CHANGÉ (2026-11-22) - le bouton Grimoire est parti en haut à droite (grand bouton illustré) : le « ? » reste en bas à
        // gauche, seul, à sa taille d'avant (56 px, marge 14), en face du bouton œil.
        RectTransform grimoire = Find("MenuPanel/Grimoire");
        if (grimoire == null) return;
        RectTransform rt = new GameObject("TutorialHelpButton", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(grimoire, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
        const float size = 56f;
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = new Vector2(14f, 14f);

        Image ring = rt.gameObject.AddComponent<Image>();
        ring.sprite = HudSprites.Disc;
        ring.color = new Color(0.10f, 0.07f, 0.05f, 0.92f);
        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = ring;
        rt.gameObject.AddComponent<ButtonHoverFX>();

        Image edge = new GameObject("Edge", typeof(RectTransform)).AddComponent<Image>();
        edge.transform.SetParent(rt, false);
        edge.sprite = HudSprites.Ring;
        edge.color = new Color(1f, 0.80f, 0.36f, 1f);
        edge.raycastTarget = false;
        edge.rectTransform.anchorMin = Vector2.zero;
        edge.rectTransform.anchorMax = Vector2.one;
        edge.rectTransform.offsetMin = edge.rectTransform.offsetMax = Vector2.zero;

        TMPro.TextMeshProUGUI t = new GameObject("Mark", typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();
        t.transform.SetParent(rt, false);
        t.text = "?";
        t.fontSize = size * 0.62f;
        t.alignment = TMPro.TextAlignmentOptions.Center;
        t.color = new Color(1f, 0.80f, 0.36f, 1f);
        t.raycastTarget = false;
        t.rectTransform.anchorMin = Vector2.zero;
        t.rectTransform.anchorMax = Vector2.one;
        t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;

        b.onClick.AddListener(Replay);
    }

    // ---- après la 1re partie --------------------------------------------------------------------------------------------
    // L'Or d'une 1re partie ne suffit pas toujours pour un nœud : le conseil dit la vérité (prix du 1er nœud, Or actuel).
    private IEnumerator AfterFirstRunTip()
    {
        yield return new WaitForSecondsRealtime(1.4f);
        MetaProgressionManager meta = MetaProgressionManager.Instance;
        if (meta == null || _menu == null || _canvas == null) yield break;
        if (!Tutorial.HintsEnabled || Tutorial.Seen(Tutorial.Hint.AfterFirstRun) || meta.Data.totalRuns < 1) yield break;
        if (TutorialOverlay.IsShowing || SettingsPage.ModalOpen || QuitConfirm.IsOpen) yield break;
        _menu.ShowMenu();

        int hero = meta.GetSelectedCharacterIndex();
        string firstNode = hero == 1 ? "vitality" : hero == 2 ? "dash" : "concentration";
        int cost = meta.GetNodeCost(firstNode);
        int gold = meta.Data.totalGold;
        bool canBuy = cost > 0 && gold >= cost;

        TutorialStep s = new TutorialStep
        {
            Title = () => Loc.T("PREMIÈRE PARTIE TERMINÉE"),
            Body = canBuy
                ? (System.Func<string>)(() => Loc.T("L'Or accumulé suffit pour acheter un premier nœud dans Compétences."))
                : (() => Loc.F("Le premier nœud coûte {0} Or ; vous en avez {1}. Continuez à jouer pour l'obtenir.", Loc.Int(cost), Loc.Int(gold))),
            Target = () => Find("TabBar/TabUpgrades"),
            Tight = true,
            NextLabel = () => Loc.T("Compris"),
            ShowSkip = false,
            OnExit = () => Tutorial.MarkSeen(Tutorial.Hint.AfterFirstRun),
        };
        TutorialOverlay.Get().Present(s, 0, 1, () => { TutorialOverlay.Get().Dismiss(); if (canBuy) _menu.ShowUpgrades(); }, null);
    }
}
