using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; private set; }

    // Palette a 3 paliers reprise telle quelle des pastilles d'upgrade
    // (UpgradeSlot/Réputation), pour une cohérence visuelle totale entre tous
    // les indicateurs à paliers du jeu.
    // Couleurs des paliers (cristaux d'ultime, points de compétences, Concentration) : réglables par HudStyler (palette du HUD).
    private static Color _emptyTierColor = new Color32(0x59, 0x4C, 0x40, 150);
    private static Color _filledTierColor = new Color32(0x2D, 0xD4, 0xCF, 255);
    private static Color _maxTierColor = new Color32(0xFF, 0xC9, 0x4D, 255);

    public static void SetTierColors(Color empty, Color filled, Color max)
    {
        _emptyTierColor = empty; _filledTierColor = filled; _maxTierColor = max;
    }

    public void SetShieldColors(Color filled, Color locked)
    {
        _manaShieldFilledColor = filled; _manaShieldLockedColor = locked;
    }

    [Header("XP")]
    [SerializeField] private Slider _xpBar;
    [SerializeField] private TextMeshProUGUI _levelText;

    // AJOUTÉ (2026-10-04) - retour utilisateur sur le ramassage d'XP : "pas le côté satisfaisant". La barre
    // sautait instantanément à la nouvelle valeur à chaque gemme ramassée - aucun lien visuel entre "une gemme
    // vient de voler jusqu'au joueur" et "la barre bouge". Remplit maintenant en douceur vers la cible (lissage
    // exponentiel, indépendant du framerate) + un petit "punch" d'échelle à chaque gain, pour que le ramassage se
    // ressente jusque dans la barre elle-même.
    private float _xpBarTargetValue;
    private float _xpBarDisplayedValue;
    private float _xpBarPunchTimer = -1f;
    private const float XPBarFillLerpRate = 9f;
    private const float XPBarPunchDuration = 0.16f;

    [Header("HP")]
    [SerializeField] private Slider _hpBar;
    [SerializeField] private TextMeshProUGUI _hpText;
    [SerializeField] private Image _hpFillImage;

    [Header("Timer")]
    [SerializeField] private TextMeshProUGUI _timerText;

    [Header("Défi")]
    [SerializeField] private TextMeshProUGUI _challengeText;
    // AJOUTE (2026-09-15) - conteneur (icone + texte) du rappel de defi dans le
    // HUD : masque entierement quand le defi de cette heure a deja ete reussi
    // (voir HideChallengeDisplay, appele par ChallengeManager.RefreshDisplay)
    // plutot que de laisser un texte "a faire" trompeur puisqu'il ne rapporte
    // plus rien.
    [SerializeField] private GameObject _challengeGroup;

    // AJOUTÉ (2026-10-13, retour utilisateur : "quand on lance une partie, on a le message de défi qui vient en
    // premier avec une animation en gros au milieu de l'écran et dit ce qu'il faut faire, avec l'icône de défi à
    // côté, et après ce délai, l'animation remet fluidement les éléments en haut à droite du HUD à leur position
    // d'origine") - réglages de timing/taille de cette annonce de lancement, voir PlayChallengeIntroRoutine plus
    // bas. Ne se joue JAMAIS si le défi de l'heure est déjà réussi : dans ce cas ChallengeManager.RefreshDisplay()
    // appelle HideChallengeDisplay() et jamais UpdateChallengeDisplay(), donc ce chemin n'est simplement jamais
    // atteint - "la partie se lance directement", comme demandé, sans rien y changer de spécial.
    //
    // MODIFIÉ (2026-10-13, retour utilisateur : "tu peux mettre plus d'animation [...] qui part d'en haut à
    // droite, vient au milieu, et repart à son endroit d'origine [...] il faudrait que tu mette un fond sur le
    // panel [...] autant d'informations qu'on peut voir dans le pause panel [...] une couleur qui va avec le
    // parchemin pour le fond [...] pendant tout le long [...] le player ne peut pas bouger et la game n'a pas
    // commencé, le timer est à 0") - 3 changements : (1) le "pop-in au centre" devient un vrai aller-retour,
    // la carte part de la VRAIE position du widget HUD, vole jusqu'au centre, puis en repart à l'identique à la
    // fin (voir _challengeIntroOriginScale, PlayChallengeIntroRoutine) ; (2) le fond sombre neutre devient un
    // parchemin (couleur chaude façon papier vieilli, encre brune pour le texte - mêmes teintes que les cartes
    // d'upgrade, voir GameUI.ParchmentInkColor) et la carte affiche désormais nom + PALIER DE DIFFICULTÉ +
    // description + récompense chiffrée, soit tout ce que montre déjà le menu Pause (PauseMenuUI.PullChallengeInfo)
    // sauf le Statut (toujours "En cours" à l'instant 0 d'une run, aucune valeur informative) ; (3)
    // GameManager.SetIntroLock(true/false) gèle tout le gameplay (joueur, ennemis, spawner, chrono - tous déjà
    // basés sur IsPaused) pendant TOUTE la séquence, du tout premier instant du vol aller jusqu'à l'arrivée
    // complète du vol retour.
    // MODIFIÉ (2026-10-18, retour utilisateur : "le fond beige est de la même couleur que le parchemin [...] on
    // enlève le cadre, et on met le texte directement sur l'icône du parchemin, et donc l'icône de parchemin on
    // l'agrandit beaucoup et on la met au milieu") - plus de cadre/fond séparé (voir l'historique de
    // EnsureChallengeIntroBuilt juste en dessous) : la "carte" EST l'icône de parchemin (parchemin_quest_transparent),
    // très agrandie, dimensionnée à son vrai ratio (1395x1086) pour ne jamais la déformer.
    // MODIFIÉ (2026-10-18, retour utilisateur : "il faut aussi qu'il soit un peu moins grand") - 1000 -> 760.
    [Header("Défi — annonce de lancement (grand format, centrée)")]
    [SerializeField] private float _challengeIntroCardWidth = 760f;
    [SerializeField] private float _challengeIntroTitleFontSize = 64f;
    [SerializeField] private float _challengeIntroDescFontSize = 34f;
    [Tooltip("Échelle de la carte à son point de départ/arrivée (position réelle du widget HUD, bien plus petit que la carte centrale).")]
    [SerializeField] private float _challengeIntroOriginScale = 0.22f;
    [SerializeField] private float _challengeIntroFlyInDuration = 0.6f;
    // MODIFIÉ (2026-10-25, retour utilisateur : "rallonge le temps d'affichage du début de quelques secondes") - 2,6 -> 5,5 s
    // (valeur aussi réécrite dans Game.unity, le champ y est sérialisé). Le joueur pressé passe d'une touche (voir
    // ChallengeIntroSkipRequested), l'invite en bas à droite le lui dit (voir EnsureSkipPromptBuilt).
    [SerializeField] private float _challengeIntroHoldDuration = 5.5f;
    [SerializeField] private float _challengeIntroFlyOutDuration = 0.6f;

    private bool _challengeIntroPlayed = false;
    private RectTransform _skipPrompt;            // invite "Appuie sur une touche pour passer", sœur de _challengeIntroRoot
    private CanvasGroup _skipPromptGroup;
    private RectTransform _skipPromptKey;
    private RectTransform _skipPromptProgress;
    private RectTransform _challengeIntroRoot;     // racine plein écran (toujours visible, c'est la Card ci-dessous qui bouge/zoome)
    private CanvasGroup _challengeIntroCanvasGroup;
    private RectTransform _challengeIntroCard;
    private Image _challengeIntroIconImage;
    private TextMeshProUGUI _challengeIntroTitleText;
    private TextMeshProUGUI _challengeIntroDescText;

    [Header("Boss")]
    [SerializeField] private GameObject _bossHPBar;
    [SerializeField] private Slider _bossHPSlider;
    [SerializeField] private TextMeshProUGUI _bossNameText;
    [SerializeField] private GameObject _bossIcon;

    // AJOUTE (2026-10-07, retour utilisateur : "pendant le boss 3 il faudrait ajouter une petite barre de vie
    // pour les mini boss qui arrive uniquement quand le mini boss spawn, et disparait quand le mini boss est
    // mort, et reviens si un autre mini boss spawn") - clone de _bossHPBar (MiniBossHPBar dans la scène), taille
    // divisée par 2, positionné à gauche de la barre du boss principal. Voir BossBase.TakeDamage/Die (branche
    // IsSummoned) et BossCorruptedSource.InitSummonedBoss pour les appels.
    [SerializeField] private GameObject _miniBossHPBar;
    [SerializeField] private Slider _miniBossHPSlider;

    [Header("Game Over")]
    [SerializeField] private GameObject _gameOverPanel;
    [SerializeField] private TextMeshProUGUI _statsText;

    [Header("Game Over — Or/Éclats/Défi (animés)")]
    // AJOUTE - champs dedies aux nombres animes, separes du bloc _statsText
    // pour pouvoir les faire defiler independamment du reste (Temps/Kills).
    [SerializeField] private TextMeshProUGUI _gameOverGoldText;
    [SerializeField] private TextMeshProUGUI _gameOverEclatsText;
    [SerializeField] private TextMeshProUGUI _gameOverChallengeText;

    [Header("Game Over — Portrait désaturé + grille (parité Victoire, refonte 3 temps)")]
    // MODIFIE - remplace l'ancien bloc "Records 5 lignes + liste texte de
    // build" (retiré, même logique que la refonte Victoire : ça "dégonfle le
    // moment", voir NOTES.md) par le même traitement moderne que Victoire :
    // portrait du perso (désaturé — c'est une défaite, pas un triomphe) +
    // grille d'icônes teintées par branche. Réutilise _buildGridSlotPrefab et
    // _characterPortraits, déjà câblés pour Victoire.
    [SerializeField] private Image _gameOverPortraitImage;
    [SerializeField] private Transform _gameOverBuildGridContent;
    // AJOUTE - teinte multipliée sur le portrait (grisé/désaturé). Un simple
    // multiply ne désature pas au sens strict (pas de vraie conversion
    // niveaux de gris) mais assombrit et neutralise assez la couleur pour lire
    // clairement "défaite" sans dépendre d'un shader dédié ni d'un nouvel asset.
    [SerializeField] private Color _gameOverPortraitTint = new Color32(0x8A, 0x85, 0x80, 0xFF);

    [Header("Game Over — Message d'ambiance")]
    // AJOUTE - message contextuel : record battu/presque battu en priorite
    // (couleur _challengeSuccessColor, deja utilisee ailleurs pour "reussite"),
    // sinon un message d'humour noir tire de GameOverMessagePool selon la
    // cause de la mort (couleur _gameOverMessageDefaultColor).
    [SerializeField] private TextMeshProUGUI _gameOverMessageText;
    [SerializeField] private Color _gameOverMessageDefaultColor = new Color32(0xE8, 0xDD, 0xC0, 255);

    [Header("Game Over — Aperçu du prochain palier")]
    // AJOUTE - "Encore X Or pour débloquer [Noeud]", affiche APRES la sequence
    // de comptage de l'Or (voir PlayGoldSequenceThenPreview) pour ne pas faire
    // concurrence visuelle au comptage - un dernier beat qui tourne le regard
    // du joueur vers l'avant plutot que vers la run qu'il vient de perdre.
    // Volontairement reserve au Game Over (pas a la Victoire), qui n'a pas
    // besoin de ce rattrapage moral.
    [SerializeField] private TextMeshProUGUI _gameOverNextUnlockText;
    [SerializeField] private float _nextUnlockFadeInDuration = 0.4f;

    [Header("Game Over — Numéro de tentative")]
    // AJOUTE - "Tentative n°X" (Data.totalRuns), pour materialiser une
    // progression dans le temps plutot que juste dans une partie - meme esprit
    // que le systeme de Reputation/Eclats, applique ici a l'ecran de fin.
    [SerializeField] private TextMeshProUGUI _gameOverAttemptText;
    // AJOUTE (2026-09-12) - meme compteur cote Victoire, demande explicitement
    // par l'utilisateur pour la coherence entre les deux ecrans de fin.
    [SerializeField] private TextMeshProUGUI _victoryAttemptText;

    [Header("Verrouillage anti-skip (Rejouer)")]
    // AJOUTE - le bouton Rejouer reste desactive tant que la sequence de
    // reveal (comptage Or + defi + apercu de palier) n'est pas terminee, pour
    // qu'un simple clic ne puisse pas sauter tout ce qu'on vient de construire
    // - meme logique que le verrou du raccourci clavier, cote GameManager.
    [SerializeField] private Button _gameOverRetryButton;
    [SerializeField] private Button _victoryRetryButton;

    // AJOUTE - signale a GameManager (qui debloque alors le raccourci clavier
    // Entree/Espace) que la sequence de reveal est terminee. Statique et
    // reinitialise a chaque chargement de scene puisque GameUI est propre a
    // la scene (pas de DontDestroyOnLoad ici).
    public static System.Action OnEndScreenRevealComplete;

    [Header("Gold")]
    [SerializeField] private TextMeshProUGUI _goldText;

    [Header("Kill Counter")]
    [SerializeField] private TextMeshProUGUI _killCountText;

    [Header("Dash")]
    [SerializeField] private Slider _dashCooldownBar;

    [Header("Clone (Lyra)")]
    [SerializeField] private GameObject _cloneCooldownContainer;
    [SerializeField] private Image _cloneCooldownFill;
    [Tooltip("Texte de l'icône du clone : affiche la touche assignée dans Paramètres > Commandes (trouvé tout seul dans le conteneur s'il est vide).")]
    [SerializeField] private TextMeshProUGUI _cloneKeyText;
    [Tooltip("Taille max de la touche dans la pastille du clone, en proportion de la taille d'origine du texte (appliquée au lancement).")]
    [Range(0.3f, 1.2f)] [SerializeField] private float _cloneKeyFontScale = 0.72f;

    [Header("Concentration (Aether)")]
    // AJOUTE - lecture du bonus dynamique du nœud Guerrier "Concentration".
    // Le conteneur est masqué pour Kael/Lyra via SetConcentrationAvailable(false).
    // Un indicateur visible est ESSENTIEL pour une mécanique de momentum : le
    // joueur doit voir le bonus grimper et sentir la perte quand il est touché.
    [SerializeField] private GameObject _concentrationContainer;
    [SerializeField] private TextMeshProUGUI _concentrationText;

    [Header("Bouclier de Mana (Kael)")]
    // AJOUTE - pips de charge du nœud capstone Gardien "Bouclier de Mana".
    // Même principe que _crystalIcons : un tableau d'Images, i < charges = rempli.
    // Le conteneur est masqué pour Aether/Lyra via SetManaShieldAvailable(false).
    [SerializeField] private GameObject _manaShieldContainer;
    [SerializeField] private Image[] _manaShieldPips;
    // MODIFIE (2026-09-16) - vert plutôt que le cyan générique _filledTierColor
    // (retour utilisateur : "ce serait mieux de changer la couleur de ces dots
    // en Vert plutôt qu'en bleu, parce que c'est représentatif de Kael") -
    // champ dédié pour ne pas changer _filledTierColor, partagé par les pips
    // Cristal/tier dots/etc. ailleurs dans le HUD. Même famille de teinte que
    // _tileTintKael (vert des tuiles d'upgrade Kael) mais plus vive/saturée,
    // pensée pour un pip HUD lumineux plutôt qu'une teinte de fond discrète.
    [SerializeField] private Color _manaShieldFilledColor = new Color32(0x36, 0xD9, 0x36, 255);
    // Teinte des pips quand le bouclier est cassé (recharge verrouillée) : rouge-gris désaturé.
    [SerializeField] private Color _manaShieldLockedColor = new Color32(0x8A, 0x5A, 0x5A, 200);

    [Header("Cristal")]
    [SerializeField] private UnityEngine.UI.Image[] _crystalIcons;
    [SerializeField] private GameObject _ultReadyEffect;
    [SerializeField] private TextMeshProUGUI _ultStackText;

    [Header("Pause")]
    [SerializeField] private GameObject _pausePanel;
    [SerializeField] private Button _pauseSettingsButton;      // menu pause : ouvre la page Paramètres
    [SerializeField] private GameObject _settingsPage;
    [SerializeField] private TextMeshProUGUI _pauseStatsText;
    [SerializeField] private TextMeshProUGUI _pauseUpgradesText;
    [SerializeField] private GameObject _abandonConfirmPanel;

    [Header("Victoire")]
    [SerializeField] private GameObject _victoryPanel;
    [SerializeField] private TextMeshProUGUI _victoryStatsText;

    [Header("Victoire — refonte 3 temps")]
    // AJOUTE - ligne de récap avec du ton (voix des messages de Game Over).
    [SerializeField] private TextMeshProUGUI _victoryRecapText;
    // AJOUTE - highlight d'un record BATTU cette partie (masqué sinon).
    [SerializeField] private GameObject _victoryRecordHighlight;
    [SerializeField] private TextMeshProUGUI _victoryRecordHighlightText;
    // AJOUTE - portrait du perso (réutilise les illustrations de CharacterSelectUI).
    [SerializeField] private Image _victoryPortraitImage;
    [Tooltip("Index 0 = Aether, 1 = Kael, 2 = Lyra. Mêmes sprites que la page de sélection.")]
    [SerializeField] private Sprite[] _characterPortraits;
    // AJOUTE - phrase de texture affichée UNIQUEMENT quand ni le highlight de
    // record ni le chip de défi ne sont actifs (victoire "normale", sans rien
    // d'exceptionnel à mettre en avant) - le panel de gauche a sensiblement
    // moins de contenu dans ce cas et se sent vide autrement. Jamais affichée
    // en même temps que l'un des deux autres (l'un ou l'autre suffit déjà à
    // remplir l'espace).
    [SerializeField] private TextMeshProUGUI _victoryQuietLineText;

    [Header("Grille d'upgrades (fin de partie)")]
    // AJOUTE - remplace la liste de texte par une grille d'icônes compacte
    // (icône + pastilles de palier, sans nom), façon écran de fin de Vampire
    // Survivors / Brotato. Conteneur = un objet avec un GridLayoutGroup ; prefab
    // = un slot compact (réutiliser UpgradeSlot ou une copie allégée, il suffit
    // qu'il porte un UpgradeSlotRefs). Laisser les champs de texte ci-dessus
    // non assignés si on ne garde que la grille.
    [SerializeField] private Transform _victoryBuildGridContent;
    [SerializeField] private GameObject _buildGridSlotPrefab;
    // AJOUTE - repli affiché à la place de la grille quand elle est vide (0
    // upgrade obtenue cette partie) - voir PopulateEmptyBuildState.
    [SerializeField] private TextMeshProUGUI _victoryEmptyBuildText;
    [SerializeField] private TextMeshProUGUI _gameOverEmptyBuildText;
    // AJOUTE (2026-09-12) - "BackgroundBuild" : cadre permanent ajoute a la
    // main par l'utilisateur derriere la grille du Game Over, pour que les
    // tuiles ne semblent plus "sortir de nulle part". Ne doit etre visible que
    // quand la grille contient au moins une upgrade - sinon il reste affiche
    // en meme temps que le petit cadre "vide" (EmptyBuildBg), ce qui fait
    // doublon (retour utilisateur). Uniquement Game Over, la Victoire n'a pas
    // cet objet.
    [SerializeField] private GameObject _gameOverArsenalFrame;

    // Teintes de fond des tuiles, par branche d'upgrade (rouge/vert/bleu/doré,
    // mêmes familles que les parchemins des cartes de level-up).
    private static readonly Color _tileTintAether    = new Color32(0xC4, 0x5A, 0x3A, 0xE6);
    private static readonly Color _tileTintKael      = new Color32(0x4C, 0x8A, 0x4C, 0xE6);
    private static readonly Color _tileTintLyra      = new Color32(0x4C, 0x74, 0xC4, 0xE6);
    private static readonly Color _tileTintUniversal = new Color32(0xC4, 0xA0, 0x4D, 0xE6);
    // AJOUTE (2026-09-30, retour utilisateur : "le fond de carte d'une fusion doit être d'une couleur
    // spéciale, je pensais au violet") - même famille de saturation/alpha que les teintes ci-dessus, teinte
    // violette pour bien distinguer une fusion de la couleur de branche habituelle.
    private static readonly Color _tileTintFusion    = new Color32(0x8A, 0x4C, 0xC4, 0xE6);

    private readonly List<GameObject> _spawnedBuildGridSlots = new List<GameObject>();

    [Header("Victoire — Or/Éclats/Défi (animés)")]
    [SerializeField] private TextMeshProUGUI _victoryGoldText;
    [SerializeField] private TextMeshProUGUI _victoryEclatsText;
    [SerializeField] private TextMeshProUGUI _victoryChallengeText;

    [Header("Animation des nombres (fin de run)")]
    // AJOUTE - reglages communs aux deux panels (Victoire / Game Over) pour
    // l'effet de comptage satisfaisant. _challengeRevealDelay = les "2 secondes"
    // demandees entre la fin du 1er comptage et l'apparition du message de defi.
    [SerializeField] private float _goldCountUpDuration = 1.2f;
    [SerializeField] private float _bonusCountUpDuration = 0.8f;
    [SerializeField] private float _eclatsCountUpDuration = 1.2f;
    [SerializeField] private float _challengeRevealDelay = 2f;
    [SerializeField] private Color _challengeSuccessColor = new Color32(0xFF, 0xC9, 0x4D, 255);

    [Header("HUD")]
    [SerializeField] private GameObject _hudPanel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        GameSettings.Changed += OnUserSettingChanged;
        SettingsPage.InGameClosed += OnSettingsClosed;
        InputBindings.Changed += OnBindingChanged;
        Loc.Changed += OnLanguageChanged;
    }

    private void OnDestroy()
    {
        GameSettings.Changed -= OnUserSettingChanged;
        SettingsPage.InGameClosed -= OnSettingsClosed;
        InputBindings.Changed -= OnBindingChanged;
        Loc.Changed -= OnLanguageChanged;
    }

    private void Start()
    {
        if (MetaProgressionManager.Instance != null)
            UpdateGold(MetaProgressionManager.Instance.RunGold);

        UpdateKillCount(0);
        ApplyUserSettings();
        RefreshCloneKey();
        StyleEndScreenTexts();
        FitStatStrips();

        if (_pauseSettingsButton != null) _pauseSettingsButton.onClick.AddListener(OpenSettings);
    }

    // Menu pause > Paramètres : la page (même prefab que le menu principal) s'affiche par-dessus, la partie reste en pause.
    public void OpenSettings()
    {
        if (_pausePanel != null) _pausePanel.SetActive(false);      // la page Paramètres prend tout l'écran (la partie reste visible en fond)
        if (_settingsPage != null) _settingsPage.SetActive(true);
    }

    // La page Paramètres vient de se fermer : retour au menu pause (si la partie est toujours en pause).
    private void OnSettingsClosed()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) ShowPausePanel(true);
    }

    // ---- réglages du joueur (Paramètres > Interface) -------------------------------------------------------
    // Échelle du HUD = échelle du seul panneau HUD (jamais du Canvas entier : les menus pause / montée de niveau /
    // fin de partie sont dessinés plein cadre et déborderaient). Le panneau est centré, réduit à (écran ÷ échelle)
    // puis agrandi de l'échelle : il couvre toujours exactement l'écran, donc les éléments restent collés à leurs
    // bords. Opacité = CanvasGroup posé sur le HUD. Chronomètre / or / kills = simple activation de leur texte.
    private CanvasGroup _hudGroup;
    private RectTransform _hudRect;
    private Vector2 _hudParentSize;
    private float _hudAppliedScale = -1f;

    private void LateUpdate()
    {
        // la fenêtre a changé de taille : le panneau HUD doit suivre
        if (_hudRect != null && _hudRect.parent is RectTransform p && (p.rect.size - _hudParentSize).sqrMagnitude > 0.25f)
            ApplyHudScale();

        UpdateXPBarSmoothing();
    }

    private void ApplyHudScale()
    {
        if (_hudPanel == null) return;
        if (_hudRect == null) _hudRect = _hudPanel.GetComponent<RectTransform>();
        if (_hudRect == null || !(_hudRect.parent is RectTransform parent)) return;

        float s = Mathf.Clamp(GameSettings.GetFloat(GameSettings.HudScale), 0.5f, 1.5f);
        _hudParentSize = parent.rect.size;
        _hudAppliedScale = s;

        _hudRect.pivot = new Vector2(0.5f, 0.5f);
        if (Mathf.Abs(s - 1f) < 0.001f)
        {
            _hudRect.anchorMin = Vector2.zero;
            _hudRect.anchorMax = Vector2.one;
            _hudRect.offsetMin = _hudRect.offsetMax = Vector2.zero;
            _hudRect.localScale = Vector3.one;
            return;
        }
        _hudRect.anchorMin = _hudRect.anchorMax = new Vector2(0.5f, 0.5f);
        _hudRect.anchoredPosition = Vector2.zero;
        _hudRect.sizeDelta = _hudParentSize / s;
        _hudRect.localScale = new Vector3(s, s, 1f);
    }

    private void OnUserSettingChanged(string key)
    {
        switch (key)
        {
            case GameSettings.HudScale:
            case GameSettings.HudOpacity:
            case GameSettings.ShowTimer:
            case GameSettings.ShowGold:
            case GameSettings.ShowKills:
                ApplyUserSettings(); break;
        }
    }

    public void ApplyUserSettings()
    {
        ApplyHudScale();

        if (_hudPanel != null)
        {
            if (_hudGroup == null)
            {
                _hudGroup = _hudPanel.GetComponent<CanvasGroup>();
                if (_hudGroup == null) _hudGroup = _hudPanel.AddComponent<CanvasGroup>();
                _hudGroup.interactable = false;
                _hudGroup.blocksRaycasts = false;
            }
            _hudGroup.alpha = GameSettings.GetFloat(GameSettings.HudOpacity);
        }

        if (_timerText != null) _timerText.gameObject.SetActive(GameSettings.GetBool(GameSettings.ShowTimer));
        if (_goldText != null) _goldText.gameObject.SetActive(GameSettings.GetBool(GameSettings.ShowGold));
        if (_killCountText != null) _killCountText.gameObject.SetActive(GameSettings.GetBool(GameSettings.ShowKills));
    }

    public void UpdateUltStack(int stacks)
    {
        if (_ultStackText == null) return;

        if (stacks <= 0)
        {
            _ultStackText.gameObject.SetActive(false);
            return;
        }

        _ultStackText.gameObject.SetActive(true);
        _ultStackText.text = stacks == 2
            ? "<color=#" + ColorUtility.ToHtmlStringRGB(_maxTierColor) + ">ULT x2</color>"
            : "<color=#" + ColorUtility.ToHtmlStringRGB(_filledTierColor) + ">ULT x1</color>";
    }

    public void UpdateCrystalCharge(int current, int max)
    {
        if (_crystalIcons == null) return;
        for (int i = 0; i < _crystalIcons.Length; i++)
        {
            if (_crystalIcons[i] == null) continue;

            bool isWithinMax = i < max;
            _crystalIcons[i].gameObject.SetActive(isWithinMax);

            if (isWithinMax)
                _crystalIcons[i].color = (i < current) ? _filledTierColor : _emptyTierColor;
        }
    }

    public void UpdateChallengeDisplay(string challengeName, string progressText, bool failed)
    {
        if (_challengeGroup != null) _challengeGroup.SetActive(true);
        if (_challengeText == null) return;

        if (failed)
        {
            _challengeText.text = $"<s>{challengeName}</s> — " + Loc.T("Échoué");
            _challengeText.color = new Color(0.55f, 0.55f, 0.55f);
        }
        else
        {
            _challengeText.text = $"{challengeName} — {progressText}";
            _challengeText.color = Color.white;
        }

        // AJOUTÉ (2026-10-13) - première fois que le défi s'affiche dans CETTE run (jamais rejoué aux mises à jour
        // de progression suivantes, qui repassent par ce même appel à chaque kill/or/niveau) : joue l'annonce en
        // grand avant de laisser le texte ci-dessus tranquille dans le HUD.
        if (!_challengeIntroPlayed)
        {
            _challengeIntroPlayed = true;
            ChallengeDefinition def = ChallengeManager.Instance != null ? ChallengeManager.Instance.CurrentChallenge : null;

            // MODIFIÉ (2026-10-13, retour utilisateur : "autant d'informations qu'on peut voir dans le pause
            // panel") - nom + palier de difficulté colorié dans le titre, description + récompense chiffrée dans
            // le sous-texte : même contenu que PauseMenuUI.PullChallengeInfo (sans le Statut, toujours "En cours"
            // à l'instant 0 d'une run fraîche).
            string introTitle = challengeName;
            string introDesc = progressText;
            if (def != null)
            {
                string difficultyHex = ColorUtility.ToHtmlStringRGB(GetDifficultyColor(def.difficulty));
                string rewardText = ChallengeManager.Instance != null
                    ? ChallengeManager.FormatRewardMultiplier(ChallengeManager.Instance.GetCurrentRewardPercent())
                    : "";
                introTitle = Loc.F("{0} : Niv. {1}", challengeName, $"<color=#{difficultyHex}>{GetDifficultyLabel(def.difficulty)}</color>");
                // MODIFIÉ (2026-10-16, retour utilisateur : "il y a de la place, [...] pour comprendre exactement
                // ce qu'il faut faire") - LongDescription (phrase complète) au lieu de Description (version
                // courte à 35 car. pensée pour l'espace réduit du menu Pause, voir PauseMenuUI.PullChallengeInfo).
                introDesc = string.IsNullOrEmpty(rewardText) ? def.LongDescription : def.LongDescription + "\n" + Loc.F("Récompense : {0}", rewardText);
            }

            StartCoroutine(PlayChallengeIntroRoutine(introTitle, introDesc, GetChallengeIconSprite()));
        }
    }

    // AJOUTÉ (2026-10-13) - mêmes paliers/couleurs/libellés que PauseMenuUI (_difficultyEasyColor/MediumColor/
    // HardColor, GetDifficultyLabel) : petite duplication assumée, chaque script UI reste autonome (convention
    // déjà suivie partout ailleurs dans ce fichier), mais la palette DOIT rester identique à l'œil entre l'annonce
    // de lancement et le détail du menu Pause.
    // MODIFIÉ (2026-10-25, analyse de lisibilité) - l'annonce s'affiche sur le parchemin clair (#ECDDBB), pas sur le
    // fond sombre du menu Pause : le vert/ambre d'origine y tombaient à 1,7:1 et 1,6:1 de contraste (mesuré sur
    // capture). Mêmes teintes, assombries pour l'encre sur parchemin (4,7 à 5,4:1) ; le menu Pause garde les siennes.
    private static readonly Color DifficultyEasyColor = new Color32(0x2F, 0x6B, 0x1E, 0xFF);    // vert (était #6FBF54)
    private static readonly Color DifficultyMediumColor = new Color32(0x8E, 0x52, 0x08, 0xFF);  // ambre (était #E8A33D)
    private static readonly Color DifficultyHardColor = new Color32(0x9A, 0x33, 0x22, 0xFF);    // rouge (était #B5533E)

    private static Color GetDifficultyColor(ChallengeDifficulty difficulty)
    {
        switch (difficulty)
        {
            case ChallengeDifficulty.Easy: return DifficultyEasyColor;
            case ChallengeDifficulty.Medium: return DifficultyMediumColor;
            case ChallengeDifficulty.Hard: return DifficultyHardColor;
            default: return Color.white;
        }
    }

    private static string GetDifficultyLabel(ChallengeDifficulty difficulty)
    {
        switch (difficulty)
        {
            case ChallengeDifficulty.Easy: return Loc.T("FACILE");
            case ChallengeDifficulty.Medium: return Loc.T("MOYEN");
            case ChallengeDifficulty.Hard: return Loc.T("DIFFICILE");
            default: return "";
        }
    }

    // AJOUTE (2026-09-15) - masque le rappel de defi du HUD (voir
    // ChallengeManager.RefreshDisplay/IsRewardAlreadyClaimedThisHour).
    public void HideChallengeDisplay()
    {
        if (_challengeGroup != null) _challengeGroup.SetActive(false);
    }

    // =====================
    // ANNONCE DE DÉFI EN GRAND AU LANCEMENT (voir UpdateChallengeDisplay ci-dessus)
    // =====================

    private Sprite GetChallengeIconSprite()
    {
        if (_challengeGroup == null) return null;
        Transform icon = _challengeGroup.transform.Find("ChallengeIcon");
        if (icon == null) return null;
        Image img = icon.GetComponent<Image>();
        return img != null ? img.sprite : null;
    }

    private static CanvasGroup GetOrAddCanvasGroup(GameObject go)
    {
        if (go == null) return null;
        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }

    // Construite une seule fois, en code (comme HudSprites/CustomCursor - zéro asset ni objet de scène
    // supplémentaire à préparer à la main). Sœur du panneau HUD (PAS un enfant) : ne doit jamais être affectée
    // par l'échelle HUD réglable dans Paramètres > Interface (ApplyHudScale ne touche QUE _hudPanel) - cette
    // annonce doit rester à une taille confortable et constante quel que soit ce réglage.
    private void EnsureChallengeIntroBuilt()
    {
        if (_challengeIntroRoot != null) return;
        if (_hudPanel == null) return;
        RectTransform hudRect = _hudPanel.GetComponent<RectTransform>();
        if (hudRect == null || !(hudRect.parent is RectTransform canvasRoot)) return;

        GameObject rootGO = new GameObject("ChallengeIntro", typeof(RectTransform), typeof(CanvasGroup));
        _challengeIntroRoot = (RectTransform)rootGO.transform;
        _challengeIntroRoot.SetParent(canvasRoot, false);
        _challengeIntroRoot.anchorMin = Vector2.zero;
        _challengeIntroRoot.anchorMax = Vector2.one;
        _challengeIntroRoot.offsetMin = _challengeIntroRoot.offsetMax = Vector2.zero;
        _challengeIntroRoot.SetAsLastSibling();   // toujours au-dessus du reste du HUD pendant l'annonce
        _challengeIntroCanvasGroup = rootGO.GetComponent<CanvasGroup>();
        _challengeIntroCanvasGroup.alpha = 0f;
        _challengeIntroCanvasGroup.interactable = false;
        _challengeIntroCanvasGroup.blocksRaycasts = false;

        GameObject cardGO = new GameObject("Card", typeof(RectTransform));
        _challengeIntroCard = (RectTransform)cardGO.transform;
        _challengeIntroCard.SetParent(_challengeIntroRoot, false);
        _challengeIntroCard.anchorMin = _challengeIntroCard.anchorMax = new Vector2(0.5f, 0.5f);
        _challengeIntroCard.pivot = new Vector2(0.5f, 0.5f);
        _challengeIntroCard.anchoredPosition = Vector2.zero;
        // MODIFIÉ (2026-10-18, retour utilisateur : "le fond beige est de la même couleur que le parchemin, donc la
        // couleur ne va pas [...] on enlève le cadre, et on met le texte directement sur l'icône du parchemin [...]
        // on l'agrandit beaucoup et on la met au milieu") - plus de cadre/fond séparé (l'ancien duo Frame+Inset
        // beige, voir historique Git) : la "carte" EST désormais l'icône de parchemin elle-même
        // (parchemin_quest_transparent), très agrandie. Dimensionnée à son vrai ratio (sprite 1395x1086) pour ne
        // jamais la déformer.
        float cardHeight = _challengeIntroCardWidth * (1086f / 1395f);
        _challengeIntroCard.sizeDelta = new Vector2(_challengeIntroCardWidth, cardHeight);

        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var iconRt = (RectTransform)iconGO.transform;
        iconRt.SetParent(_challengeIntroCard, false);
        iconRt.anchorMin = Vector2.zero; iconRt.anchorMax = Vector2.one;
        iconRt.offsetMin = iconRt.offsetMax = Vector2.zero;
        _challengeIntroIconImage = iconGO.GetComponent<Image>();

        // AJOUTÉ (2026-10-18, retour utilisateur : "comme sur l'icône du parchemin il y a un cercle noir au milieu,
        // il faudrait le cacher [...] avec un carré de la même couleur que le parchemin") - le médaillon
        // étoile/couronne au centre du sprite (visible une fois agrandi) est caché par un carré plein, échantillonné
        // sur la texture (zone crème plate, loin du médaillon et du sceau du rouleau : #ECDDBB). UNIQUEMENT pour cet
        // affichage - ce cache n'existe que dans cette hiérarchie, jamais sur la petite icône du HUD ni sur les
        // cartes du menu Pause, qui réutilisent le même sprite intact ailleurs (GetChallengeIconSprite/ConfigureBuildGridSlot).
        // Zone choisie généreusement (couvre aussi la couronne de laurier autour de l'étoile, pas juste l'étoile) :
        // mesurée en échantillonnant la texture (médaillon centré pile au centre du sprite, ~265x280px sur 1395x1086).
        // MODIFIÉ (2026-10-18, retour utilisateur : "déplace le texte un peu sur la droite") - cache + texte décalés
        // ensemble de +0.05 en X (le cache doit suivre le texte, sinon le médaillon sombre dépasserait derrière).
        GameObject coverGO = new GameObject("MedallionCover", typeof(RectTransform), typeof(Image));
        var coverRt = (RectTransform)coverGO.transform;
        coverRt.SetParent(_challengeIntroCard, false);
        coverRt.anchorMin = new Vector2(0.14f, 0.24f);
        coverRt.anchorMax = new Vector2(0.77f, 0.76f);
        coverRt.offsetMin = coverRt.offsetMax = Vector2.zero;
        Image cover = coverGO.GetComponent<Image>();
        cover.color = new Color32(0xEC, 0xDD, 0xBB, 0xFF);

        // MODIFIÉ (2026-10-18, retour utilisateur, capture à l'appui : "quand il y a un texte long, le texte n'est
        // plus centré en Y, donc remonte un peu") - ANCIEN : titre et description vivaient chacun dans leur propre
        // moitié FIXE (haut 0.50-0.76 / bas 0.24-0.50), alignés Bottom/Top pour se rejoindre pile sur la ligne du
        // milieu. Ça ne marchait que quand la description tenait sur ses 2 lignes prévues (description + récompense)
        // : dès qu'un défi a une description plus longue qui s'enroule sur une 3e ligne, elle grandit uniquement
        // vers le BAS depuis son ancrage Top fixe - le bloc titre+description entier n'est plus centré dans la zone
        // du parchemin, son centre de masse réel descend sans que le titre ne bouge, donnant l'impression que tout
        // "remonte" par rapport au vrai centre visuel. NOUVEAU : titre et description sont désormais 2 enfants d'un
        // groupe unique (TextGroup) avec un VerticalLayoutGroup qui les empile et les centre comme UN SEUL bloc dans
        // toute la zone (même rectangle que MedallionCover) - peu importe le nombre de lignes de la description, le
        // bloc entier reste centré verticalement, exactement comme un ContentSizeFitter centré le ferait.
        GameObject groupGO = new GameObject("TextGroup", typeof(RectTransform), typeof(VerticalLayoutGroup));
        var groupRt = (RectTransform)groupGO.transform;
        groupRt.SetParent(_challengeIntroCard, false);
        groupRt.anchorMin = new Vector2(0.14f, 0.24f);
        groupRt.anchorMax = new Vector2(0.77f, 0.76f);
        groupRt.offsetMin = new Vector2(24f, 8f);
        groupRt.offsetMax = new Vector2(-24f, -8f);
        VerticalLayoutGroup textGroupLayout = groupGO.GetComponent<VerticalLayoutGroup>();
        textGroupLayout.childAlignment = TextAnchor.MiddleCenter;
        textGroupLayout.childControlWidth = true;
        textGroupLayout.childControlHeight = true;
        textGroupLayout.childForceExpandWidth = true;
        textGroupLayout.childForceExpandHeight = false;
        textGroupLayout.spacing = 6f;

        GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        var titleRt = (RectTransform)titleGO.transform;
        titleRt.SetParent(groupRt, false);
        _challengeIntroTitleText = titleGO.GetComponent<TextMeshProUGUI>();
        _challengeIntroTitleText.fontSize = _challengeIntroTitleFontSize;
        _challengeIntroTitleText.alignment = TextAlignmentOptions.Center;
        // MODIFIÉ (2026-10-13) - encre brune sur parchemin (GameUI.ParchmentInkColor) au lieu de blanc, qui ne lit
        // plus du tout sur le nouveau fond clair - même couleur que les noms d'upgrade sur parchemin (ConfigureBuildGridSlot).
        _challengeIntroTitleText.color = ParchmentInkColor;
        _challengeIntroTitleText.textWrappingMode = TextWrappingModes.NoWrap;
        // auto-tassement : les 12 défis ont des noms/descriptions de longueurs très différentes, jamais garanti de
        // tenir sur une ligne à taille fixe - plutôt que de clipper ou déborder du cadre, le texte rapetisse au besoin.
        _challengeIntroTitleText.enableAutoSizing = true;
        _challengeIntroTitleText.fontSizeMin = 32f;
        _challengeIntroTitleText.fontSizeMax = _challengeIntroTitleFontSize;
        if (_challengeText != null) _challengeIntroTitleText.font = _challengeText.font;

        GameObject descGO = new GameObject("Desc", typeof(RectTransform), typeof(TextMeshProUGUI));
        var descRt = (RectTransform)descGO.transform;
        descRt.SetParent(groupRt, false);
        _challengeIntroDescText = descGO.GetComponent<TextMeshProUGUI>();
        _challengeIntroDescText.fontSize = _challengeIntroDescFontSize;
        _challengeIntroDescText.alignment = TextAlignmentOptions.Center;
        // MODIFIÉ (2026-10-18, retour utilisateur : "je veux que tu change de couleur le texte descriptif [...] une
        // couleur un peu plus faite [...] donc une couleur plus clair") - ParchmentInkColorLight (variante plus
        // claire de l'encre) au lieu de ParchmentInkColor plein, pour distinguer visuellement le petit texte
        // descriptif du titre, qui garde l'encre pleine.
        _challengeIntroDescText.color = ParchmentInkColorLight;
        // MODIFIÉ (2026-10-13) - le sous-texte a désormais 2 lignes (description + récompense, voir
        // UpdateChallengeDisplay) : NoWrap devient Normal (retour à la ligne autorisé) pour que chacune des 2
        // lignes puisse s'enrouler si besoin au lieu de déborder du cadre.
        _challengeIntroDescText.textWrappingMode = TextWrappingModes.Normal;
        _challengeIntroDescText.enableAutoSizing = true;
        _challengeIntroDescText.fontSizeMin = 18f;
        _challengeIntroDescText.fontSizeMax = _challengeIntroDescFontSize;
        GameFonts.ToBody(_challengeIntroDescText);   // MODIFIÉ (2026-10-25) - phrase : police de lecture (le titre reste en Bangers)
        // Pas de LayoutElement : TextMeshProUGUI fournit déjà sa propre hauteur préférée (ILayoutElement), recalculée
        // automatiquement par le VerticalLayoutGroup à chaque fois que PlayChallengeIntroRoutine change .text plus
        // bas - c'est ce qui permet au bloc de rester centré que la description tienne sur 1, 2 ou 3 lignes.
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float x1 = x - 1f;
        return 1f + c3 * x1 * x1 * x1 + c1 * x1 * x1;
    }

    private static float EaseInOutCubic(float x)
    {
        return x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
    }

    // MODIFIÉ (2026-10-13, retour utilisateur : "tu peux mettre plus d'animation [...] qui part d'en haut à
    // droite, vient au milieu, et repart à son endroit d'origine") - remplace l'ancien pop-in fixe au centre par
    // un vrai aller-retour : (1) vol depuis la VRAIE position du widget HUD jusqu'au centre de l'écran (grossit en
    // approchant), (2) maintien immobile le temps de lire, (3) vol de retour vers cette même position (rétrécit en
    // s'éloignant), avec un fondu croisé vers le widget réel pile pendant la seconde moitié du vol retour - aucun
    // "pop" brutal, le widget réel est déjà bien visible quand la carte d'annonce finit de disparaître.
    // Temps NON à l'échelle partout (WaitForSecondsRealtime / Time.unscaledDeltaTime) : cohérent avec le reste des
    // animations HUD de ce fichier, et nécessaire puisque Time.timeScale=0 pendant tout le verrou (voir
    // GameManager.SetIntroLock) - geler le jeu ne doit pas geler l'annonce elle-même.
    //
    // AJOUTÉ (2026-10-13, retour utilisateur : "pendant l'animation [...] le player ne peut pas bouger et la game
    // n'a pas commencé, le timer est à 0") - GameManager.SetIntroLock(true) dès le tout premier instant (avant même
    // le vol aller) et SetIntroLock(false) seulement une fois le vol retour terminé et le widget réel repris la
    // main : le gameplay reste gelé du début à la toute fin de la séquence, jamais entre les deux.
    private IEnumerator PlayChallengeIntroRoutine(string title, string description, Sprite icon)
    {
        if (GameManager.Instance != null) GameManager.Instance.SetIntroLock(true);

        EnsureChallengeIntroBuilt();
        if (_challengeIntroRoot == null || _challengeIntroCard == null)
        {
            if (GameManager.Instance != null) GameManager.Instance.SetIntroLock(false);
            yield break;
        }

        _challengeIntroTitleText.text = title;
        _challengeIntroDescText.text = description;
        _challengeIntroIconImage.sprite = icon;
        _challengeIntroIconImage.enabled = icon != null;

        CanvasGroup realGroupFade = GetOrAddCanvasGroup(_challengeGroup);
        if (realGroupFade != null) realGroupFade.alpha = 0f;   // invisible tant que la carte d'annonce ne l'a pas "rejoint"

        // AJOUTÉ (2026-10-18, retour utilisateur : "au lancement de la game, il est déjà là") - EnsureChallengeIntroBuilt
        // vient de créer toute la hiérarchie à l'instant : tant qu'aucune passe de layout n'a eu lieu, la position
        // monde de _challengeGroup (ancrée dans le HUD) peut encore refléter un état transitoire du tout premier
        // frame. Forcer la mise à jour ici garantit que originWorldPos/centerWorldPos sont bien les positions
        // RÉELLES avant de poser la carte à son point de départ - sinon le "vol aller" pouvait démarrer d'un point
        // quasi confondu avec l'arrivée, rendant l'animation imperceptible.
        Canvas.ForceUpdateCanvases();

        Vector3 originWorldPos = _challengeGroup != null
            ? ((RectTransform)_challengeGroup.transform).position
            : _challengeIntroRoot.position;
        Vector3 centerWorldPos = _challengeIntroRoot.position;

        _challengeIntroCanvasGroup.alpha = 0f;
        _challengeIntroCard.position = originWorldPos;
        _challengeIntroCard.localScale = Vector3.one * _challengeIntroOriginScale;
        yield return null;   // un frame posé (petit, transparent, à l'origine) avant d'animer - jamais de flash

        // 1) Vol aller : de la place réelle dans le HUD jusqu'au centre de l'écran, fondu-enchaîné en plus du
        // déplacement/zoom (retour utilisateur : l'arrivée doit se VOIR, pas juste être déjà là).
        float t = 0f;
        while (t < _challengeIntroFlyInDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / _challengeIntroFlyInDuration);
            _challengeIntroCard.position = Vector3.Lerp(originWorldPos, centerWorldPos, EaseInOutCubic(p));
            // EaseOutBack peut dépasser 1 puis revenir (léger rebond à l'arrivée) - uniquement sur l'échelle, la
            // position suit son propre easing (EaseInOutCubic) pour un déplacement qui reste bien fluide.
            float scaleEased = EaseOutBack(p);
            _challengeIntroCard.localScale = Vector3.LerpUnclamped(Vector3.one * _challengeIntroOriginScale, Vector3.one, scaleEased);
            _challengeIntroCanvasGroup.alpha = Mathf.Clamp01(p / 0.5f);   // apparu aux 2/3 du trajet, jamais un pop brutal
            yield return null;
        }
        _challengeIntroCard.position = centerWorldPos;
        _challengeIntroCard.localScale = Vector3.one;
        _challengeIntroCanvasGroup.alpha = 1f;

        // 2) Maintien immobile, le temps de lire - interrompu si le joueur presse Échap/Entrée/Espace/clic gauche.
        // AJOUTÉ (2026-10-18, retour utilisateur : "je veux aussi que tu mette des raccourcis clavier si le joueur
        // veut skip cette affichage [...] ça raccourcit juste le délai [...] donc on garde quand même les
        // animations") - seul CE délai de lecture est écourté ; le vol aller déjà joué et le vol retour ci-dessous
        // restent intégralement joués dans les 2 cas.
        // MODIFIÉ (2026-10-25) - invite de passage en bas à droite pendant ce maintien (voir EnsureSkipPromptBuilt), avec
        // une jauge qui se vide sur la durée restante : le joueur voit à la fois qu'il peut passer et quand ça
        // repartira tout seul.
        EnsureSkipPromptBuilt();
        float holdT = 0f;
        while (holdT < _challengeIntroHoldDuration && !ChallengeIntroSkipRequested())
        {
            holdT += Time.unscaledDeltaTime;
            UpdateSkipPrompt(holdT, Mathf.Clamp01(holdT / 0.35f));
            yield return null;
        }
        float promptAlphaAtExit = _skipPromptGroup != null ? _skipPromptGroup.alpha : 0f;

        // 3) Vol retour : du centre vers la place réelle dans le HUD, avec fondu croisé dans la 2e moitié
        t = 0f;
        while (t < _challengeIntroFlyOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / _challengeIntroFlyOutDuration);
            if (_skipPromptGroup != null) _skipPromptGroup.alpha = promptAlphaAtExit * (1f - Mathf.Clamp01(p / 0.35f));
            float eased = EaseInOutCubic(p);
            _challengeIntroCard.position = Vector3.Lerp(centerWorldPos, originWorldPos, eased);
            _challengeIntroCard.localScale = Vector3.Lerp(Vector3.one, Vector3.one * _challengeIntroOriginScale, eased);
            _challengeIntroCanvasGroup.alpha = 1f - Mathf.Clamp01((p - 0.55f) / 0.45f);     // ne commence à s'effacer que dans la 2e moitié du vol
            if (realGroupFade != null) realGroupFade.alpha = Mathf.Clamp01((p - 0.5f) / 0.5f);   // apparition en fondu croisé du vrai widget
            yield return null;
        }

        _challengeIntroCanvasGroup.alpha = 0f;
        if (_skipPrompt != null) _skipPrompt.gameObject.SetActive(false);
        if (realGroupFade != null) realGroupFade.alpha = 1f;
        _challengeIntroCard.localScale = Vector3.one;
        _challengeIntroCard.position = originWorldPos;

        // Le panel est "reparti" à son endroit d'origine - le jeu peut enfin commencer.
        if (GameManager.Instance != null) GameManager.Instance.SetIntroLock(false);
    }

    // AJOUTÉ (2026-10-18, retour utilisateur : "touche Echap, entrer ou espace, ou clique gauche") - Échap est
    // déjà avalée par GameManager pendant SetIntroLock (voir _introLockActive, GameManager.cs) : aucun conflit
    // avec la Pause normale, qui ne redevient disponible qu'une fois cette coroutine terminée.
    // MODIFIÉ (2026-10-25) - n'importe quelle touche (clavier, souris, manette) au lieu de 4 touches précises : c'est
    // ce que promet l'invite affichée en bas à droite.
    private static bool ChallengeIntroSkipRequested()
    {
        return Input.anyKeyDown;
    }

    // AJOUTÉ (2026-10-25, retour utilisateur : "en bas à droite pendant cette animation tu mets un message qui dit
    // d'appuyer sur une touche pour passer, ne mets pas du simple texte dans le vide, fais quelque chose de propre") -
    // pastille sombre à liseré doré (mêmes sprites HudSprites.Pill/PillOutline que les pastilles du HUD) : une
    // touche stylisée « » » qui pulse doucement, le texte en police de lecture, et une fine jauge dorée en pied de
    // pastille qui se vide sur le temps de lecture restant. Construite en code comme le reste de l'annonce, sœur de
    // _challengeIntroRoot (pas un enfant : son fondu reste indépendant de celui de la carte) et hors du HUD (taille
    // constante, insensible à l'échelle HUD des Paramètres).
    private void EnsureSkipPromptBuilt()
    {
        if (_skipPrompt != null) { _skipPrompt.gameObject.SetActive(true); return; }
        if (_challengeIntroRoot == null || !(_challengeIntroRoot.parent is RectTransform canvasRoot)) return;

        // MODIFIÉ (2026-10-26, retour utilisateur : "la barre en dessous est trop proche du texte et de l'icone en Y, donc
        // agrandis un peu le tout en Y") - pastille 54 -> 64 de haut, touche et texte remontés de contentLift : la jauge
        // garde sa place en pied de pastille, avec un vrai espace au-dessus d'elle.
        const float height = 64f, padLeft = 12f, padRight = 22f, keyW = 44f, keyH = 32f, gap = 12f, contentLift = 5f;
        Color gold = new Color32(0xE8, 0xC7, 0x7A, 0xFF);

        GameObject root = new GameObject("SkipPrompt", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        _skipPrompt = (RectTransform)root.transform;
        _skipPrompt.SetParent(canvasRoot, false);
        _skipPrompt.anchorMin = _skipPrompt.anchorMax = _skipPrompt.pivot = new Vector2(1f, 0f);
        _skipPrompt.SetAsLastSibling();
        _skipPromptGroup = root.GetComponent<CanvasGroup>();
        _skipPromptGroup.alpha = 0f;
        _skipPromptGroup.interactable = false;
        _skipPromptGroup.blocksRaycasts = false;
        Image bg = root.GetComponent<Image>();
        bg.sprite = HudSprites.Pill;
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.06f, 0.05f, 0.08f, 0.85f);
        bg.raycastTarget = false;

        Image outline = NewPromptImage("Outline", _skipPrompt, HudSprites.PillOutline, new Color(gold.r, gold.g, gold.b, 0.55f));
        outline.type = Image.Type.Sliced;
        RectTransform outlineRt = outline.rectTransform;
        outlineRt.anchorMin = Vector2.zero; outlineRt.anchorMax = Vector2.one;
        outlineRt.offsetMin = outlineRt.offsetMax = Vector2.zero;

        // Touche stylisée : petite pastille claire, liseré doré, « » » en encre sombre.
        Image key = NewPromptImage("Key", _skipPrompt, HudSprites.Pill, new Color32(0xEC, 0xDD, 0xBB, 0xFF));
        key.type = Image.Type.Sliced;
        _skipPromptKey = key.rectTransform;
        _skipPromptKey.anchorMin = _skipPromptKey.anchorMax = new Vector2(0f, 0.5f);
        _skipPromptKey.pivot = new Vector2(0.5f, 0.5f);
        _skipPromptKey.sizeDelta = new Vector2(keyW, keyH);
        _skipPromptKey.anchoredPosition = new Vector2(padLeft + keyW * 0.5f, contentLift);
        Image keyRim = NewPromptImage("Rim", _skipPromptKey, HudSprites.PillOutline, gold);
        keyRim.type = Image.Type.Sliced;
        keyRim.rectTransform.anchorMin = Vector2.zero; keyRim.rectTransform.anchorMax = Vector2.one;
        keyRim.rectTransform.offsetMin = keyRim.rectTransform.offsetMax = Vector2.zero;
        TextMeshProUGUI keyText = NewPromptText("Glyph", _skipPromptKey, "»", 28f, ParchmentInkColor);
        keyText.rectTransform.anchorMin = Vector2.zero; keyText.rectTransform.anchorMax = Vector2.one;
        keyText.rectTransform.offsetMin = Vector2.zero; keyText.rectTransform.offsetMax = new Vector2(0f, 3f);   // « » » est dessiné bas dans la police

        TextMeshProUGUI label = NewPromptText("Label", _skipPrompt, Loc.T("Appuie sur une touche pour passer"), 22f, new Color32(0xEE, 0xE6, 0xD6, 0xFF));
        label.alignment = TextAlignmentOptions.MidlineLeft;
        float labelW = Mathf.Ceil(label.GetPreferredValues(label.text).x);
        RectTransform labelRt = label.rectTransform;
        labelRt.anchorMin = new Vector2(0f, 0f); labelRt.anchorMax = new Vector2(0f, 1f);
        labelRt.pivot = new Vector2(0f, 0.5f);
        labelRt.sizeDelta = new Vector2(labelW, 0f);
        labelRt.anchoredPosition = new Vector2(padLeft + keyW + gap, contentLift);

        float width = padLeft + keyW + gap + labelW + padRight;
        _skipPrompt.sizeDelta = new Vector2(width, height);

        // Jauge : un rail sombre et sa partie dorée, dont l'ancre droite suit le temps restant (UpdateSkipPrompt).
        Image rail = NewPromptImage("ProgressRail", _skipPrompt, null, new Color(1f, 1f, 1f, 0.10f));
        RectTransform railRt = rail.rectTransform;
        railRt.anchorMin = new Vector2(0f, 0f); railRt.anchorMax = new Vector2(1f, 0f);
        railRt.pivot = new Vector2(0.5f, 0f);
        railRt.offsetMin = new Vector2(height * 0.5f, 7f); railRt.offsetMax = new Vector2(-height * 0.5f, 10f);
        Image fill = NewPromptImage("Fill", railRt, null, gold);
        _skipPromptProgress = fill.rectTransform;
        _skipPromptProgress.anchorMin = Vector2.zero; _skipPromptProgress.anchorMax = Vector2.one;
        _skipPromptProgress.offsetMin = _skipPromptProgress.offsetMax = Vector2.zero;
    }

    private static Image NewPromptImage(string name, RectTransform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private TextMeshProUGUI NewPromptText(string name, RectTransform parent, string text, float size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        GameFonts.ToBody(tmp);
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    // fadeIn : 0 -> 1 au début du maintien (apparition en fondu + légère montée), puis jauge et pulsation de la touche.
    private void UpdateSkipPrompt(float holdT, float fadeIn)
    {
        if (_skipPrompt == null) return;
        float eased = 1f - (1f - fadeIn) * (1f - fadeIn);
        _skipPromptGroup.alpha = eased;
        _skipPrompt.anchoredPosition = new Vector2(-36f, 32f - 14f * (1f - eased));
        float remaining = 1f - Mathf.Clamp01(holdT / Mathf.Max(0.01f, _challengeIntroHoldDuration));
        _skipPromptProgress.anchorMax = new Vector2(remaining, 1f);
        _skipPromptKey.localScale = Vector3.one * (1f + 0.06f * (0.5f + 0.5f * Mathf.Sin(holdT * 4.2f)));
    }

    // =====================
    // ANIMATION DES NOMBRES (Victoire / Game Over)
    // =====================

    // AJOUTE - compteur generique reutilisable : fait defiler un TextMeshProUGUI
    // de "from" a "to" sur "duration" secondes, avec un ease-out cubique (rapide
    // au debut, ralentit en approchant la cible) pour un rendu satisfaisant
    // plutot qu'un defilement lineaire mecanique. Unscaled : continue de tourner
    // meme si Time.timeScale est a 0 (le jeu est deja en pause visuelle sur ces
    // ecrans de fin de run).
    private IEnumerator CountUpNumber(TextMeshProUGUI text, int from, int to, float duration)
    {
        if (text == null) yield break;

        // MODIFIÉ (2026-10-18, diagnostic complet - audit §9 "les grands nombres (or, Éclats) s'affichent sans
        // séparateur de milliers") - Loc.Int() au lieu d'un ToString() brut : sans effet sur les petits nombres
        // (kills, boss), ajoute le bon séparateur selon la langue sur l'Or/les Éclats en fin de partie.
        if (duration <= 0f || from == to)
        {
            text.text = Loc.Int(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            int value = Mathf.RoundToInt(Mathf.Lerp(from, to, eased));
            text.text = Loc.Int(value);
            yield return null;
        }

        text.text = Loc.Int(to);
    }

    // AJOUTE - orchestre la sequence complete pour l'Or : compte jusqu'au
    // montant de base, puis si le defi est reussi, attend _challengeRevealDelay,
    // affiche le message de reussite (couleur vive), et reprend le comptage
    // jusqu'au montant final (base + bonus).
    private IEnumerator PlayGoldSequence(TextMeshProUGUI goldText, TextMeshProUGUI challengeText, int baseGold, int totalGold, bool challengeCompleted, float rewardPercent)
    {
        // MODIFIE - le chip Victoire a un fond dedie (ChallengeChipBg, une Image
        // ajoutee en parent du texte lors de la refonte 3-temps) separe du texte
        // lui-meme. Desactiver seulement challengeText.gameObject laissait ce fond
        // visible en permanence (bandeau orange vide quand aucun defi n'est
        // reussi). On cache/affiche desormais le PARENT quand il porte une Image
        // (le chip complet), avec repli sur le texte seul si ce parent n'existe
        // pas (cas Game Over, chip pas encore mis en place).
        // MODIFIÉ (2026-10-26) - le chip est désormais préparé (actif, transparent, texte posé) par PrepareChallengeChip à
        // l'ouverture de l'écran, dans le cadre d'infos : il n'est plus que révélé ici, sans faire bouger la mise en page.
        GameObject chipRoot = ChallengeChipRoot(challengeText);

        yield return StartCoroutine(CountUpNumber(goldText, 0, baseGold, _goldCountUpDuration));

        if (challengeCompleted && totalGold > baseGold)
        {
            yield return new WaitForSecondsRealtime(_challengeRevealDelay);

            if (chipRoot != null)
            {
                CanvasGroup chipGroup = chipRoot.GetComponent<CanvasGroup>();
                if (chipGroup != null) chipGroup.alpha = 1f;
                chipRoot.SetActive(true);
                // MODIFIE - challengeText.color = _challengeSuccessColor (#FFC94D)
                // retire : c'est EXACTEMENT la meme teinte que le fond du chip
                // (ChallengeChipBg, egalement #FFC94D) - le texte devenait
                // invisible en jeu, contraste nul, alors qu'il restait lisible
                // dans l'Editeur puisque la couleur authoree du texte (#4A3410,
                // deja correcte sur les deux panels) n'etait jamais touchee la-bas
                // (retour utilisateur, bug trouve en jeu). On ne touche plus a
                // cette couleur ici : #4A3410 est deja pensee pour ce fond.
                // MODIFIE - "x{rewardPercent:0.00}" affichait litteralement
                // "x0.10" pour un bonus de +10% (rewardPercent est une
                // fraction 0-1, pas un multiplicateur) - se lisait comme une
                // PERTE de 90% plutot qu'un bonus.
                // MODIFIE (2026-09-13) - "Or xN" au lieu de "+X%" (retour
                // utilisateur : plus parlant dans le vocabulaire jeu video).
                // Voir ChallengeManager.FormatRewardMultiplier, partage avec
                // PauseMenuUI.PullChallengeInfo().
                challengeText.text = Loc.F("Défi réussi ! {0}", ChallengeManager.FormatRewardMultiplier(rewardPercent));
            }

            yield return StartCoroutine(CountUpNumber(goldText, baseGold, totalGold, _bonusCountUpDuration));
        }
    }

    // AJOUTE - formatage MM:SS partage, deja duplique par ailleurs dans ce
    // fichier mais garde tel quel a chaque endroit existant pour ne rien casser -
    // celui-ci est reserve aux nouveaux appels (message d'ambiance).
    private string FormatTime(float seconds)
    {
        int mins = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        return $"{mins:00}:{secs:00}";
    }

    // AJOUTE - determine le message affiche sur le Game Over : priorite a un
    // record battu ou presque battu (survie ou kills), sinon un message
    // d'humour noir selon la cause de la mort. totalRuns > 1 evite d'annoncer
    // "nouveau record" des le tout premier run (bestTime/bestKills valent deja
    // les valeurs de CE run a ce stade, SaveRunResults les ayant ecrasees juste
    // avant l'appel a ShowGameOver - sans cette garde, un run sans aucune
    // reference anterieure se ferait passer pour un record battu).
    // MODIFIE - couvre desormais 4 stats (temps/kills/niveau/or en une partie)
    // au lieu de 2, via une petite structure locale pour eviter de repeter la
    // meme logique de comparaison 4 fois. La 1ere boucle cherche un record
    // BATTU (priorite max, quelle que soit la stat), la 2e ne s'execute que si
    // aucun record n'a ete battu et cherche le PLUS PROCHE d'etre battu.
    private struct RecordCheck
    {
        public float current;
        public float best;
        public string newRecordMessage;
        public string nearRecordMessage;
        // AJOUTÉ (2026-10-21, retour utilisateur : "j'étais à deux doigts de mon record de survie, ba en fait non,
        // parce que j'ai pas de record vu que j'ai déjà réussi une partie de 15min, donc 15 min n'est plus un
        // record si on ne peut pas faire plus") - le temps de survie en Classique a un plafond dur : le chrono se
        // FIGE dès l'apparition du boss 3 (BossSpawnInterval × 3 = 900s, voir WaveManager), donc 900s EST le
        // maximum humainement atteignable, pas juste "la meilleure valeur vue jusqu'ici". Une fois `best` déjà à
        // ce plafond, aucun run futur ne peut JAMAIS le dépasser (seulement l'égaler) - annoncer "à deux doigts de
        // ton record" est donc une promesse qui ne pourra jamais se réaliser. 0 = pas de plafond (kills/niveau/or
        // n'en ont pas, ils continuent de monter indéfiniment).
        public float hardCap;
    }

    private void PopulateGameOverMessage(float runTime, int killCount, int levelReached, int goldThisRun, string deathCause, int bossKillCount = 0)
    {
        if (_gameOverMessageText == null) return;

        bool isRecordHighlight = false;
        string message = null;

        // Sans fin / Ruée de boss : leurs propres records et leur propre ton (les records du classique ne sont pas touchés).
        if (!GameModes.IsClassic)
        {
            string modeRecord = MetaProgressionManager.Instance != null ? MetaProgressionManager.Instance.LastRunRecordMessage : null;
            isRecordHighlight = modeRecord != null;
            message = modeRecord ?? BuildModeGameOverMessage(runTime, bossKillCount);
            _gameOverMessageText.text = message;
            _gameOverMessageText.color = isRecordHighlight ? _challengeSuccessColor : _gameOverMessageDefaultColor;
            return;
        }

        if (MetaProgressionManager.Instance != null && MetaProgressionManager.Instance.Data != null)
        {
            SaveData data = MetaProgressionManager.Instance.Data;

            // totalRuns > 1 : au tout premier run, tous les "best" valent deja
            // les valeurs de CE run (SaveRunResults les a ecrasees juste avant),
            // donc sans cette garde on annoncerait un record battu sans aucune
            // reference anterieure a avoir battu.
            if (data.totalRuns > 1 && GameModes.IsClassic)
            {
                // CORRIGE (2026-09-30, retour utilisateur, capture d'écran : "à deux doigts de ton record" à
                // 15:00 pile alors que 15:00 est le max possible avant le combat du boss 3 - "c'est pas
                // possible") - le chrono du run se FIGE dès l'apparition d'un boss (voir WaveManager) : mourir
                // PENDANT le combat du boss 3 donne systématiquement le même runTime au flottant près (~900s),
                // quel que soit le run. `data.bestTime` peut différer de quelques centièmes (bruit d'une image à
                // l'autre au moment exact où le chrono s'est figé) sans que ça se voie sur l'affichage
                // (FormatTime tronque à la seconde) : ratio 0,9994 par exemple déclenchait "si proche" pour un
                // temps affiché IDENTIQUE. Comparaison à la seconde tronquée (comme l'affichage) pour ce check
                // uniquement : un temps qui s'affiche pareil que le record compte comme égalé, pas "presque".
                RecordCheck[] checks =
                {
                    new RecordCheck { current = Mathf.Floor(runTime), best = Mathf.Floor(MetaProgressionManager.Instance.PreviousBestTime), newRecordMessage = Loc.F("Nouveau record de survie : {0} !", FormatTime(runTime)), nearRecordMessage = Loc.T("Ton record de survie était à portée"), hardCap = WaveManager.Instance != null ? WaveManager.Instance.BossSpawnInterval * 3f : 900f },
                    new RecordCheck { current = killCount, best = MetaProgressionManager.Instance.PreviousBestKills, newRecordMessage = Loc.F("Nouveau record d'éliminations : {0} !", killCount), nearRecordMessage = Loc.T("Ton record d'éliminations était à portée") },
                    new RecordCheck { current = levelReached, best = MetaProgressionManager.Instance.PreviousBestLevel, newRecordMessage = Loc.F("Nouveau record de niveau : {0} !", levelReached), nearRecordMessage = Loc.T("Ton meilleur niveau était à portée") },
                    new RecordCheck { current = goldThisRun, best = MetaProgressionManager.Instance.PreviousBestGoldInRun, newRecordMessage = Loc.F("Nouveau record d'Or en une partie : {0} !", goldThisRun), nearRecordMessage = Loc.T("Ton record d'Or était à portée") },
                };

                foreach (RecordCheck check in checks)
                {
                    if (check.best > 0f && check.current > check.best)   // égalité : pas un record (voir PreviousBestTime)
                    {
                        message = check.newRecordMessage;
                        isRecordHighlight = true;
                        break;
                    }
                }

                if (message == null)
                {
                    foreach (RecordCheck check in checks)
                    {
                        // AJOUTÉ (2026-10-21) - voir le commentaire sur RecordCheck.hardCap : si le record est déjà
                        // au plafond dur (survie = 900s, le chrono se fige au boss 3), "presque un nouveau record"
                        // serait une promesse qui ne pourra jamais se réaliser - on l'exclut de cette 2e boucle.
                        if (check.hardCap > 0f && check.best >= check.hardCap) continue;

                        if (check.best > 0f && check.current >= check.best * 0.9f)
                        {
                            message = check.nearRecordMessage;
                            isRecordHighlight = true;
                            break;
                        }
                    }
                }
            }
        }

        if (message == null)
            message = GameOverMessagePool.GetMessage(deathCause, runTime);

        _gameOverMessageText.text = message;
        _gameOverMessageText.color = isRecordHighlight ? _challengeSuccessColor : _gameOverMessageDefaultColor;
    }

    // AJOUTE - portrait désaturé du perso (temps 2 du Game Over). Même source
    // que PopulateVictoryPortrait (illustrations de CharacterSelectUI), teinté
    // pour signaler visuellement "défaite" sans nouvel asset.
    private void PopulateGameOverPortrait()
    {
        if (_gameOverPortraitImage == null || _characterPortraits == null || _characterPortraits.Length == 0) return;
        if (MetaProgressionManager.Instance == null) return;

        int index = Mathf.Clamp(MetaProgressionManager.Instance.GetSelectedCharacterIndex(), 0, _characterPortraits.Length - 1);
        Sprite portrait = _characterPortraits[index];
        if (portrait != null)
            _gameOverPortraitImage.sprite = portrait;

        _gameOverPortraitImage.color = _gameOverPortraitTint;
    }

    // AJOUTE - grille d'icônes de la build obtenue cette partie. Suit
    // LevelUpManager.ObtainedOrder (ordre chronologique de pick). Chaque tuile :
    // fond teinté par branche + icône + pastilles de palier (ou "xN" pour les
    // upgrades à cap élevé, ou losange de déblocage pour les armes à pick séparé).
    // Pas de nom : sur un écran de résultats, l'icône suffit et reste compacte.
    // MODIFIE - renvoie desormais le nombre de tuiles reellement posees, pour
    // que l'appelant puisse afficher un etat "vide" explicite (voir
    // PopulateEmptyBuildState) au lieu de laisser un grand trou a cote du
    // portrait quand le joueur meurt/gagne sans avoir pris une seule upgrade
    // (mort tres precoce - trouve en conditions reelles sur le Game Over,
    // theoriquement possible aussi sur la Victoire meme si beaucoup plus rare).
    // (2026-09-27) Grille 3x3 pilotée par les fusions : ligne = les 2 armes qui fusionnent ensemble (celle du
    // personnage d'abord, puis les paires universelles), colonne 3 = toujours une passive (Dégâts/Cadence/Soin).
    // Une fusion DÉJÀ complétée prend la place de ses 2 armes en une seule tuile large. Voir GetArsenalRows().
    private struct ArsenalRow
    {
        public UpgradeData weapon1, weapon2, fusion, passive;
    }

    private int PopulateBuildGrid(Transform gridContent)
    {
        // MODIFIÉ (2026-10-26, retour utilisateur : "je t'avais dis d'agrandir les cases, ça doit presque remplir tout le
        // fond, il faut qu'il y ait le même écart entre le bord, et l'écart qu'il y a entre chaques cases") - la taille
        // des cases se déduit du cadre gris de l'arsenal (BackgroundBuild, frère de la grille) : 3 cases et 4 écarts
        // _arsenalSpacing (bord, entre les cases, bord) remplissent sa largeur, et la grille est posée à un écart de son
        // coin haut-gauche. Le contenu des cases grandit d'autant (_endTileScale, voir ConfigureBuildGridSlot), comme
        // l'agrandissement proportionnel du menu Pause ; pastilles et losanges gardent leur taille.
        Vector2 cellSize = _arsenalCellSize;
        Vector2? origin = null;
        _endTileScale = 1f;
        RectTransform content = gridContent as RectTransform;
        RectTransform frame = content != null && content.parent != null ? content.parent.Find("BackgroundBuild") as RectTransform : null;
        if (frame != null)
        {
            if (content.parent.parent is RectTransform column) LayoutRebuilder.ForceRebuildLayoutImmediate(column);
            Vector3[] corners = new Vector3[4];
            frame.GetWorldCorners(corners);
            Vector3 topLeft = content.InverseTransformPoint(corners[1]);
            Vector3 bottomRight = content.InverseTransformPoint(corners[3]);
            float side = Mathf.Min(bottomRight.x - topLeft.x, topLeft.y - bottomRight.y);
            float cell = (side - 4f * _arsenalSpacing.x) / 3f;
            cellSize = new Vector2(cell, cell);
            origin = new Vector2(topLeft.x - content.rect.xMin + _arsenalSpacing.x, content.rect.yMax - topLeft.y + _arsenalSpacing.y);
            _endTileScale = cell / _arsenalCellSize.x;
        }
        return PopulateBuildGrid(gridContent, _buildGridSlotPrefab, _spawnedBuildGridSlots, cellSize, _arsenalSpacing, false, null, null, null, null, null, origin);
    }

    // Version générique (cellule/espacement/style paramétrables) : réutilisée telle quelle par PauseMenuUI.PopulateGrid()
    // pour la même grille en plus grand, avec son propre look (parchemin), sans dupliquer la logique de
    // disposition/fusion. parchmentSober, s'il est fourni (non-null), active le style "parchemin" du menu Pause
    // (même sprite sobre pour toutes les cartes) ; laissé à null pour garder le style "tuile teintée" par défaut
    // de cet écran (Victoire/Défaite).
    public int PopulateBuildGrid(Transform gridContent, GameObject slotPrefab, List<GameObject> spawnedSlots, Vector2 cellSize, Vector2 spacing, bool showNames,
        Sprite parchmentSober, Color? dotEmpty, Color? dotFilled, Color? dotMax, System.Action<UpgradeData> onSlotClicked = null, Vector2? gridOrigin = null)
    {
        foreach (GameObject old in spawnedSlots)
            if (old != null) Destroy(old);
        spawnedSlots.Clear();

        if (gridContent == null || slotPrefab == null || LevelUpManager.Instance == null)
            return 0;

        // Positionnement manuel ci-dessous (nécessaire pour les tuiles de fusion, larges de 2 colonnes) : toute mise
        // en page automatique du conteneur doit être désactivée, sinon elle écrase ou re-clippe nos positions.
        GridLayoutGroup layoutGroup = gridContent.GetComponent<GridLayoutGroup>();
        if (layoutGroup != null) layoutGroup.enabled = false;
        ContentSizeFitter fitter = gridContent.GetComponent<ContentSizeFitter>();
        if (fitter != null) fitter.enabled = false;

        // Centre la grille dans le conteneur si celui-ci est plus large qu'elle (ex: menu Pause, dont le contenu
        // n'a plus besoin de remplir toute la largeur d'un ancien ScrollView une fois la grille fixée à 3x3).
        float gridWidth = cellSize.x * 3f + spacing.x * 2f;
        float originX = Mathf.Max(0f, (((RectTransform)gridContent).rect.width - gridWidth) * 0.5f);

        int count = 0;
        List<ArsenalRow> rows = GetArsenalRows();

        void PlaceSlot(UpgradeData upgrade, int row, int col, int colSpan)
        {
            GameObject slotGO = Instantiate(slotPrefab, gridContent);
            ConfigureBuildGridSlot(slotGO, upgrade, showNames, parchmentSober, dotEmpty, dotFilled, dotMax, cellSize, onSlotClicked);
            if (gridOrigin.HasValue) PositionArsenalSlot(slotGO, row, col, colSpan, cellSize, spacing, gridOrigin.Value.x, gridOrigin.Value.y);
            else PositionArsenalSlot(slotGO, row, col, colSpan, cellSize, spacing, originX);
            spawnedSlots.Add(slotGO);
            count++;
        }

        for (int row = 0; row < rows.Count; row++)
        {
            ArsenalRow r = rows[row];
            bool fused = r.fusion != null && r.fusion.GetCurrentLevel() > 0;

            if (fused)
            {
                PlaceSlot(r.fusion, row, 0, 2);
            }
            else
            {
                if (r.weapon1 != null) PlaceSlot(r.weapon1, row, 0, 1);
                if (r.weapon2 != null) PlaceSlot(r.weapon2, row, 1, 1);
            }

            if (r.passive != null) PlaceSlot(r.passive, row, 2, 1);
        }

        // Hauteur réelle du contenu (nécessaire au ScrollRect du menu Pause pour savoir jusqu'où défiler - sans ça,
        // avec la mise en page automatique désactivée plus haut, Content garde une hauteur figée qui ne correspond
        // plus aux rangées réellement positionnées).
        // CORRIGE (2026-09-27) - ne le fait QUE si un ScrollRect existe au-dessus (menu Pause) : sur Victoire/Défaite,
        // BuildGridContent a un pivot CENTRÉ (0.5) et non haut-gauche comme le Content du Pause - toucher sa
        // sizeDelta déplaçait tout le contenu vers le haut (la moitié de la hauteur ajoutée), faisant déborder la
        // 1re rangée au-dessus du cadre (retour utilisateur, capture d'écran : grille "décalée" sur l'écran Défaite).
        if (rows.Count > 0 && gridContent.GetComponentInParent<ScrollRect>() != null)
        {
            RectTransform contentRt = (RectTransform)gridContent;
            float totalHeight = rows.Count * cellSize.y + (rows.Count - 1) * spacing.y;
            contentRt.sizeDelta = new Vector2(contentRt.sizeDelta.x, totalHeight);
        }

        return count;
    }

    // Convention identique à GridLayoutGroup (UpperLeft / Horizontal) : ancre et pivot en haut-gauche, position
    // calculée à partir de la colonne/ligne et du nombre de colonnes occupées (2 pour une tuile de fusion).
    // originY (2026-10-07, retour utilisateur : "les cartes ne sont pas bien centré en Y par rapport à leur cadre
    // gris derrière") : décalage vertical additionnel, 0 par défaut (comportement inchangé pour PopulateBuildGrid).
    private static void PositionArsenalSlot(GameObject slotGO, int row, int col, int colSpan, Vector2 cellSize, Vector2 spacing, float originX, float originY = 0f)
    {
        RectTransform rt = (RectTransform)slotGO.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(originX + col * (cellSize.x + spacing.x), -originY - (row * (cellSize.y + spacing.y)));
        rt.sizeDelta = new Vector2(cellSize.x * colSpan + spacing.x * (colSpan - 1), cellSize.y);
    }

    private static readonly Vector2 _arsenalCellSize = new Vector2(125f, 125f);
    private static readonly Vector2 _arsenalSpacing = new Vector2(20f, 20f);

    // Compteur "×N" de la tuile Soin (Victoire/Défaite) : écart avec le bas et la droite de la tuile (le même que celui du
    // losange de déblocage avec son coin), et corrections mesurées sur capture pour que cet écart soit celui des chiffres
    // visibles : TextMeshPro laisse un vide sous les chiffres (place des jambages) et Bangers, penchée, dépasse à droite.
    private const float HealCountMargin = 10f;
    private const float HealCountSize = 24f;
    private const float HealCountBaselineInset = -2.8f;
    private const float HealCountSlantInset = 3.7f;

    // Agrandissement des cases de Victoire/Défaite par rapport au prefab (125 px), calculé par PopulateBuildGrid : leur
    // contenu (icônes) grandit d'autant, les pastilles et losanges gardent leur taille.
    private float _endTileScale = 1f;

    // Double Tir (Victoire/Défaite) : descendue sous le centre de sa case, pour que ses gouttes (dessinées en diagonale
    // vers le haut à droite) ne touchent pas le losange du coin (retour utilisateur 2026-10-26 : "baisse un peu l'icone").
    // MODIFIÉ (2026-10-26, même retour : "un tout petit peu vers le bas et la gauche, mais vraiment peu") : 7 -> 10 vers
    // le bas, 3 vers la gauche.
    private const float DoubleShotEndDrop = 10f;
    private const float DoubleShotEndShiftLeft = 3f;

    // Les 6 armes disponibles CETTE partie (la carte spéciale du perso + les 5 universelles), groupées par paire de
    // fusion quand une paire a une fusion définie et que les 2 armes sont encore libres — la fusion du personnage
    // passe toujours en premier (ligne 1). Les armes sans partenaire de fusion applicable (ex: Double tir pour Lyra,
    // dont le seul partenaire — l'Orbe rebondissant — est déjà pris par sa fusion perso) sont simplement regroupées
    // 2 par 2 dans l'ordre restant : elles cohabitent sur la même ligne sans jamais pouvoir fusionner.
    private List<ArsenalRow> GetArsenalRows()
    {
        List<ArsenalRow> rows = new List<ArsenalRow>();
        List<(UpgradeData, UpgradeData, UpgradeData)> pairs = GetWeaponFusionPairs();
        if (pairs == null) return rows;

        UpgradeData[] all = LevelUpManager.Instance.AllUpgrades;
        UpgradeData dmg = null, fr = null, heal = null;
        foreach (UpgradeData u in all)
        {
            if (u == null) continue;
            if (u.upgradeType == UpgradeType.Damage) dmg = u;
            else if (u.upgradeType == UpgradeType.FireRate) fr = u;
            else if (u.upgradeType == UpgradeType.Heal) heal = u;
        }
        UpgradeData[] passives = { dmg, fr, heal };

        for (int i = 0; i < 3 && i < pairs.Count; i++)
            rows.Add(new ArsenalRow { weapon1 = pairs[i].Item1, weapon2 = pairs[i].Item2, fusion = pairs[i].Item3, passive = i < passives.Length ? passives[i] : null });

        return rows;
    }

    // Extrait de GetArsenalRows() (2026-09-27) - les 3 paires d'armes (fusion du perso d'abord, puis fusions/paires
    // restantes dans l'ordre stable de LevelUpManager) sont réutilisées telles quelles par la grille compacte du
    // menu Pause (GetPauseArsenalItems), qui a juste une mise en page différente (4 colonnes au lieu de 3).
    private List<(UpgradeData, UpgradeData, UpgradeData)> GetWeaponFusionPairs()
    {
        UpgradeData[] all = LevelUpManager.Instance != null ? LevelUpManager.Instance.AllUpgrades : null;
        if (all == null) return null;

        int character = MetaProgressionManager.Instance != null ? MetaProgressionManager.Instance.GetSelectedCharacterIndex() : 0;
        UpgradeBranch mine = character == 1 ? UpgradeBranch.Kael : character == 2 ? UpgradeBranch.Lyra : UpgradeBranch.Aether;
        UpgradeType myWeapon = character == 1 ? UpgradeType.AuraUpgrade : character == 2 ? UpgradeType.Knives : UpgradeType.Fireball;

        UpgradeType[] weaponTypes = { myWeapon, UpgradeType.DoubleShot, UpgradeType.Orbital, UpgradeType.Lightning, UpgradeType.MudPuddle, UpgradeType.BouncingOrb };
        List<UpgradeData> weapons = new List<UpgradeData>();
        foreach (UpgradeType t in weaponTypes)
            foreach (UpgradeData u in all)
                if (u != null && u.upgradeType == t) { weapons.Add(u); break; }

        UpgradeData Get(UpgradeType t) { foreach (UpgradeData w in weapons) if (w.upgradeType == t) return w; return null; }

        // La fusion du personnage d'abord, puis les autres dans l'ordre de LevelUpManager (ordre stable, pas de tri).
        List<UpgradeData> fusionsMineFirst = new List<UpgradeData>();
        foreach (UpgradeData u in all)
            if (u != null && u.upgradeType == UpgradeType.Fusion && u.Branch == mine) fusionsMineFirst.Add(u);
        foreach (UpgradeData u in all)
            if (u != null && u.upgradeType == UpgradeType.Fusion && u.Branch != mine && (u.Branch == UpgradeBranch.Universal || true)) fusionsMineFirst.Add(u);

        HashSet<UpgradeData> used = new HashSet<UpgradeData>();
        List<(UpgradeData, UpgradeData, UpgradeData)> pairs = new List<(UpgradeData, UpgradeData, UpgradeData)>();

        // CORRIGE (2026-09-28, retour utilisateur : "je prends Marécage Maudit en première fusion, elle ne se
        // met pas dans le pause panel") - certaines armes universelles sont sources de PLUSIEURS fusions (ex: la
        // Foudre pour "Orbes Foudroyants" ET "Marécage Maudit"). L'ancienne boucle unique réclamait les sources
        // dans l'ordre FIXE de fusionsMineFirst (branche du perso puis ordre des assets), sans regarder si la
        // fusion avait réellement été prise - si "Orbes Foudroyants" arrivait avant "Marécage Maudit" dans cet
        // ordre, elle réservait la Foudre EN PREMIER même si le joueur n'avait jamais pris cette fusion,
        // laissant "Marécage Maudit" (réellement complétée) sans Foudre à associer - jamais ajoutée à pairs,
        // donc jamais affichée en tuile fusionnée. Corrigé en 2 passes : les fusions RÉELLEMENT complétées
        // (GetCurrentLevel() > 0 - état de jeu réel) réclament leurs sources en premier, quel que soit l'ordre de
        // la liste ; les fusions pas encore prises ne réclament ensuite que ce qu'il reste, uniquement pour
        // regrouper visuellement 2 sources encore libres côte à côte.
        foreach (UpgradeData f in fusionsMineFirst)
        {
            if (f.GetCurrentLevel() <= 0) continue;
            UpgradeData s1 = Get(f.FusionSource1);
            UpgradeData s2 = Get(f.FusionSource2);
            if (s1 == null || s2 == null || used.Contains(s1) || used.Contains(s2)) continue;
            pairs.Add((s1, s2, f));
            used.Add(s1); used.Add(s2);
        }
        foreach (UpgradeData f in fusionsMineFirst)
        {
            if (f.GetCurrentLevel() > 0) continue; // déjà traitée ci-dessus
            UpgradeData s1 = Get(f.FusionSource1);
            UpgradeData s2 = Get(f.FusionSource2);
            if (s1 == null || s2 == null || used.Contains(s1) || used.Contains(s2)) continue;
            pairs.Add((s1, s2, f));
            used.Add(s1); used.Add(s2);
        }

        List<UpgradeData> leftovers = new List<UpgradeData>();
        foreach (UpgradeData w in weapons) if (!used.Contains(w)) leftovers.Add(w);
        for (int i = 0; i < leftovers.Count; i += 2)
            pairs.Add((leftovers[i], i + 1 < leftovers.Count ? leftovers[i + 1] : null, null));

        return pairs;
    }

    // AJOUTE (2026-09-27) - grille compacte du menu Pause (retour utilisateur : "on revient sur un modèle un peu
    // plus compact... 2 lignes, 4 cartes par ligne", Soin exclu car "pas informatif sur du build"). Chaque paire
    // d'armes (fusionnée ou non) occupe TOUJOURS exactement 2 cases (une tuile large de colSpan=2, ou 2 tuiles
    // simples) : avec 3 paires + Dégâts + Cadence, ça tombe pile sur 8 cases = 2 rangées de 4, sans jamais qu'une
    // tuile large ne puisse chevaucher une fin de rangée (chaque paire commence toujours sur une colonne paire).
    private struct GridItem { public UpgradeData upgrade; public int colSpan; }

    private List<GridItem> GetPauseArsenalItems()
    {
        List<GridItem> items = new List<GridItem>();
        List<(UpgradeData, UpgradeData, UpgradeData)> pairs = GetWeaponFusionPairs();
        if (pairs == null) return items;

        for (int i = 0; i < 3 && i < pairs.Count; i++)
        {
            var (w1, w2, fusion) = pairs[i];
            bool fused = fusion != null && fusion.GetCurrentLevel() > 0;
            if (fused)
            {
                items.Add(new GridItem { upgrade = fusion, colSpan = 2 });
            }
            else
            {
                if (w1 != null) items.Add(new GridItem { upgrade = w1, colSpan = 1 });
                if (w2 != null) items.Add(new GridItem { upgrade = w2, colSpan = 1 });
            }
        }

        UpgradeData[] all = LevelUpManager.Instance.AllUpgrades;
        UpgradeData dmg = null, fr = null;
        foreach (UpgradeData u in all)
        {
            if (u == null) continue;
            if (u.upgradeType == UpgradeType.Damage) dmg = u;
            else if (u.upgradeType == UpgradeType.FireRate) fr = u;
        }
        if (dmg != null) items.Add(new GridItem { upgrade = dmg, colSpan = 1 });
        if (fr != null) items.Add(new GridItem { upgrade = fr, colSpan = 1 });

        return items;
    }

    // Version compacte de PopulateBuildGrid, réservée au menu Pause : N colonnes (4) au lieu de 3, pas de logique de
    // "passive en 3e colonne par ligne" - un simple flot gauche→droite/haut→bas des items de GetPauseArsenalItems(),
    // avec retour à la ligne automatique. onSlotClicked : callback clic-pour-décrire (voir PauseMenuUI).
    public int PopulatePauseGrid(Transform gridContent, GameObject slotPrefab, List<GameObject> spawnedSlots,
        Vector2 cellSize, Vector2 spacing, int columns, Sprite parchmentSober, Color dotEmpty, Color dotFilled, Color dotMax,
        System.Action<UpgradeData> onSlotClicked, System.Action<UpgradeData> onSlotHoverEnter = null, System.Action<UpgradeData> onSlotHoverExit = null,
        float dotSizeFew = 17f, float dotSizeMany = 12f, float dotSpacing = 6f, float unlockDotSize = 20f, float unlockDotGap = 12f)
    {
        foreach (GameObject old in spawnedSlots)
            if (old != null) Destroy(old);
        spawnedSlots.Clear();

        if (gridContent == null || slotPrefab == null || LevelUpManager.Instance == null)
            return 0;

        GridLayoutGroup layoutGroup = gridContent.GetComponent<GridLayoutGroup>();
        if (layoutGroup != null) layoutGroup.enabled = false;
        ContentSizeFitter fitter = gridContent.GetComponent<ContentSizeFitter>();
        if (fitter != null) fitter.enabled = false;

        float gridWidth = cellSize.x * columns + spacing.x * (columns - 1);
        float originX = Mathf.Max(0f, (((RectTransform)gridContent).rect.width - gridWidth) * 0.5f);

        List<GridItem> items = GetPauseArsenalItems();

        // AJOUTE (2026-10-07, retour utilisateur : "les cartes ne sont pas bien centré en Y par rapport à leur
        // cadre gris derrière, il doit y avoir le même écart entre le haut et le bas") - mesuré en jeu : la grille
        // (2 rangées de 265 + 16 d'espacement = 546) était plaquée en haut du cadre gris du ScrollView (573.75 de
        // haut), laissant tout l'écart (27.75) en bas et aucun en haut. Centrée verticalement dans le Viewport
        // (son parent direct, dont .rect.height donne la hauteur réelle du cadre indépendamment du scale du
        // Canvas) en pré-calculant d'abord le nombre de rangées via la même logique col/row que la boucle de
        // placement ci-dessous, sans encore instancier quoi que ce soit.
        int totalRows;
        {
            int c = 0, r = 0;
            foreach (GridItem item in items)
            {
                if (c + item.colSpan > columns) { c = 0; r++; }
                c += item.colSpan;
                if (c >= columns) { c = 0; r++; }
            }
            totalRows = c > 0 ? r + 1 : r;
        }
        float totalGridHeight = totalRows > 0 ? totalRows * cellSize.y + (totalRows - 1) * spacing.y : 0f;
        RectTransform viewportRt = gridContent.parent as RectTransform;
        float availableHeight = viewportRt != null ? viewportRt.rect.height : totalGridHeight;
        float originY = Mathf.Max(0f, (availableHeight - totalGridHeight) * 0.5f);

        int count = 0, col = 0, row = 0;
        foreach (GridItem item in items)
        {
            if (col + item.colSpan > columns) { col = 0; row++; }

            GameObject slotGO = Instantiate(slotPrefab, gridContent);
            PositionArsenalSlot(slotGO, row, col, item.colSpan, cellSize, spacing, originX, originY);
            ConfigureBuildGridSlot(slotGO, item.upgrade, true, parchmentSober, dotEmpty, dotFilled, dotMax, cellSize, onSlotClicked, onSlotHoverEnter, onSlotHoverExit,
                dotSizeFew, dotSizeMany, dotSpacing, unlockDotSize, unlockDotGap);
            spawnedSlots.Add(slotGO);
            count++;

            col += item.colSpan;
            if (col >= columns) { col = 0; row++; }
        }

        return count;
    }

    // AJOUTE - repli quand la grille est vide (0 upgrade obtenue) : sans ça,
    // le portrait se retrouve seul dans une grande carte à moitié vide (retour
    // utilisateur, capture à l'appui). Même famille de correctif que QuietLine
    // / le repli "tout maxé" du Game Over - ne jamais laisser un bloc de
    // contenu totalement vide sans une phrase dédiée à la place.
    //
    // MODIFIE (2026-09-12) - un temps passe par un parametre alwaysShowFrame
    // pour garder "EmptyBuildBg" actif en permanence comme fond de l'arsenal,
    // retire : l'utilisateur a construit a la main un objet dedie
    // ("BackgroundBuild", cadre noir semi-transparent toujours visible,
    // separe d'EmptyBuildBg) directement dans la scene pour ce role. Ce script
    // ne touche pas a BackgroundBuild - EmptyBuildBg reprend donc son
    // comportement d'origine : actif seulement quand la grille est vide.
    private void PopulateEmptyBuildState(TextMeshProUGUI emptyText, int tileCount, string[] pool)
    {
        if (emptyText == null) return;

        bool isEmpty = tileCount == 0;

        // Image et TextMeshProUGUI ne peuvent pas coexister sur le meme
        // GameObject (verifie - AddComponent echoue silencieusement dans les
        // deux sens), donc le fond vit sur le PARENT ("EmptyBuildBg") - meme
        // pattern deja utilise pour le chip de defi (voir PlayGoldSequence).
        Image parentImage = emptyText.transform.parent != null ? emptyText.transform.parent.GetComponent<Image>() : null;
        GameObject toggleRoot = parentImage != null ? emptyText.transform.parent.gameObject : emptyText.gameObject;
        toggleRoot.SetActive(isEmpty);

        if (isEmpty)
            emptyText.text = Loc.T(pool[UnityEngine.Random.Range(0, pool.Length)]);
    }

    // showNames : false pour la grille compacte des écrans de fin (jamais de nom), true pour la grande grille du
    // menu Pause (assez de place pour afficher le nom de chaque carte, y compris les fusions).
    // parchmentSober non-null → style "parchemin" du menu Pause (même sprite sobre pour toutes les cartes) ;
    // laissé à null → style "tuile teintée" compact des écrans de fin (même sprite pour tous, couleur = teinte de
    // branche, assombrie si pas obtenue).
    // dotEmpty/Filled/Max : couleurs des pastilles, par défaut celles de cet écran si non fournies (permet à Pause
    // de réutiliser les siennes).
    private void ConfigureBuildGridSlot(GameObject slotGO, UpgradeData upgrade, bool showNames = false,
        Sprite parchmentSober = null, Color? dotEmpty = null, Color? dotFilled = null, Color? dotMax = null,
        Vector2 cellSize = default, System.Action<UpgradeData> onSlotClicked = null,
        System.Action<UpgradeData> onSlotHoverEnter = null, System.Action<UpgradeData> onSlotHoverExit = null,
        float dotSizeFew = 17f, float dotSizeMany = 12f, float dotSpacing = 6f, float unlockDotSize = 20f, float unlockDotGap = 12f)
    {
        UpgradeSlotRefs refs = slotGO.GetComponent<UpgradeSlotRefs>();
        if (refs == null)
        {
            Debug.LogWarning("[GameUI] Le prefab de slot de grille n'a pas de composant UpgradeSlotRefs.");
            return;
        }

        Color colEmpty = dotEmpty ?? _emptyTierColor;
        Color colFilled = dotFilled ?? _filledTierColor;
        Color colMax = dotMax ?? _maxTierColor;

        int maxLevel = upgrade.MaxLevel;
        int currentLevel = upgrade.GetDisplayLevel();
        bool owned = upgrade.GetCurrentLevel() > 0;          // au moins un pick (le déblocage compte)
        bool alreadyMaxed = currentLevel >= maxLevel && (owned);
        bool isFusion = upgrade.upgradeType == UpgradeType.Fusion;

        if (refs.background != null)
        {
            if (parchmentSober != null)
            {
                refs.background.sprite = parchmentSober;
                refs.background.color = owned ? Color.white : new Color(0.55f, 0.55f, 0.55f, 0.75f);
            }
            else
            {
                // MODIFIE (2026-09-30, retour utilisateur : "le fond de carte d'une fusion doit être d'une
                // couleur spéciale, violet") - une fusion garde son identité visuelle propre plutôt que la
                // couleur de la branche de son arme (qui n'a plus de sens une fois les 2 armes fusionnées).
                Color tint = isFusion ? _tileTintFusion : GetTileTint(upgrade.Branch);
                refs.background.color = owned ? tint : new Color(tint.r * 0.55f, tint.g * 0.55f, tint.b * 0.55f, tint.a * 0.55f);
            }
        }

        // MODIFIE (2026-09-28, retour utilisateur : "je veux faire les réglages manuellement, comment je peux
        // faire ?") - jusqu'ici la position/taille de CHAQUE élément (icône, pastilles, icônes sources de
        // fusion, séparateur) était recalculée EN CODE à chaque peuplement de grille, écrasant systématiquement
        // tout ajustement manuel fait dans l'Inspector. fusionIcon/sourceIconTop/sourceIconBottom/fusionDivider
        // sont maintenant de VRAIS enfants du prefab UpgradeSlot (voir UpgradeSlotRefs) : le code ne touche plus
        // QUE sprite/couleur/visibilité ci-dessous, JAMAIS leur RectTransform (position/taille/rotation) - ces
        // valeurs sont éditables à la main directement dans le prefab et persistent réellement. Pour ajuster :
        // ouvrir Assets/Game/Prefabs/UI/UpgradeSlot.prefab, sélectionner l'enfant voulu (icon/nameText/
        // TierDotsRow/FusionIcon/SourceIconTop/SourceIconBottom/FusionDivider), modifier son RectTransform dans
        // l'Inspector, puis Ctrl+S pour sauvegarder le prefab (Play Mode pour prévisualiser en direct).
        //
        // MODIFIE (2026-09-30, retour utilisateur : "pas assez de place pour mettre 3 icônes [sur Victoire/
        // Défaite], on revient à la disposition d'avant, met que l'icône de fusion au milieu") - le visuel dédié
        // (icône de fusion + 2 icônes sources + séparateur + "+") reste réservé au menu Pause (carte 716px de
        // large pour une fusion, largement assez de place) ; Victoire/Défaite (tuile 270px) retrouve son icône
        // normale (upgrade.icon), simplement centrée - toujours identifiable par son fond violet (voir plus
        // haut) et l'absence de pastilles de palier (variable séparée juste en dessous, PAS celle-ci : une
        // fusion n'a qu'un seul palier sur AUCUN des 2 écrans, indépendamment de ce visuel dédié).
        bool showFusionVisual = isFusion && parchmentSober != null;

        if (refs.icon != null)
        {
            refs.icon.gameObject.SetActive(!showFusionVisual);
            refs.icon.sprite = upgrade.icon;
            refs.icon.color = owned ? Color.white : new Color(0.42f, 0.42f, 0.42f, 0.7f);   // carte pas obtenue : icône éteinte
            // AJOUTE (2026-09-27, retour utilisateur : "certaines icones sont déformés") - les icônes de fusion
            // n'ont pas toutes le même ratio W/H (contrairement aux icônes d'upgrade de base, plus uniformes) ;
            // sans preserveAspect, Image les étire pour remplir tout le cadre carré/rectangulaire de la tuile.
            refs.icon.preserveAspect = true;

            // AJOUTE (2026-09-30, retour utilisateur : "baisse légèrement en Y l'icône uniquement pour Double
            // Tir") - une seule et même Icon (RectTransform du prefab) sert à toutes les upgrades ; ce cas
            // particulier ne peut donc pas se régler dans le prefab sans décaler tout le monde.
            // MODIFIE (2026-09-30, retour utilisateur : "baisse les icônes des cartes normales, SAUF Double
            // Tir") - un décalage RELATIF à la position du prefab aurait fait descendre Double Tir un peu plus
            // à chaque fois que la position par défaut (partagée par toutes les cartes) est elle-même baissée.
            // Valeur ABSOLUE ici : Double Tir garde sa position déjà validée, indépendamment des réglages des
            // autres cartes.
            // CORRIGE (2026-09-30, retour utilisateur : "l'icône de tir x2 est totalement décalée" sur
            // Victoire/Défaite) - cette valeur absolue (-18) est calibrée pour le repère du prefab UpgradeSlot
            // (carte 350×265, icône centrée verticalement) : appliquée telle quelle sur UpgradeGridSlot (tuile
            // 125×125, icône ANCRÉE EN HAUT, par défaut à +37), elle envoyait l'icône loin en dehors de sa
            // position normale. Réservé au menu Pause (seul écran où -18 a du sens).
            // MODIFIE (2026-09-30, retour utilisateur : "décale légèrement l'icône des cartes à droite, sauf
            // pour Double Tir") - le X par défaut du prefab (partagé par toutes les cartes) a bougé légèrement
            // à droite ; fixe ici en X ABSOLU (comme en Y) pour que Double Tir garde sa position déjà validée
            // au lieu de suivre ce décalage commun.
            if (upgrade.upgradeType == UpgradeType.DoubleShot)
            {
                RectTransform iconRt = (RectTransform)refs.icon.transform;
                // MODIFIE (2026-09-30, retour utilisateur : "sur Victoire/Défaite, l'icône de Double Tir est
                // trop haute, baisse-la") - même icône (les 2 gouttes) visuellement plus haute que les autres
                // dans son cadre sur CETTE tuile aussi (repère différent du menu Pause, valeur absolue distincte).
                // Victoire/Défaite : placée par le bloc d'agrandissement juste en dessous.
                if (parchmentSober != null) iconRt.anchoredPosition = new Vector2(-47f, -18f);
            }

            // AJOUTÉ (2026-10-26, retour utilisateur : "agrandis un peu toutes les icones, comme avec la méthode où tu as
            // agrandi le pause panel, donc après l'avoir fait, recentre l'icone de double tir, ne change pas la taille des
            // dots") puis (précisé : "je t'avais dis d'agrandir les cases") - Victoire/Défaite : l'icône grandit comme sa
            // case (_endTileScale, taille et position depuis le centre), les pastilles gardent leur taille. Double Tir
            // (sans pastilles) : centrée dans sa case. Remplace le X absolu -8 de Double Tir (même jour) : recentrée.
            if (parchmentSober == null && !isFusion)
            {
                RectTransform iconRt = (RectTransform)refs.icon.transform;
                Vector2 size = iconRt.sizeDelta * _endTileScale;
                iconRt.sizeDelta = size;
                iconRt.anchoredPosition = upgrade.upgradeType == UpgradeType.DoubleShot
                    ? new Vector2(-DoubleShotEndShiftLeft, size.y * 0.5f - DoubleShotEndDrop)  // pivot en haut : un peu plus bas et à gauche
                    : iconRt.anchoredPosition * _endTileScale;
            }
        }

        if (refs.fusionIcon != null)
        {
            refs.fusionIcon.gameObject.SetActive(showFusionVisual);
            if (showFusionVisual)
            {
                refs.fusionIcon.sprite = upgrade.icon;
                refs.fusionIcon.color = owned ? Color.white : new Color(0.42f, 0.42f, 0.42f, 0.7f);
            }
        }

        if (showFusionVisual)
        {
            UpgradeData source1 = FindUpgradeByType(upgrade.FusionSource1);
            UpgradeData source2 = FindUpgradeByType(upgrade.FusionSource2);
            Color sourceIconColor = owned ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.75f);

            if (refs.sourceIconTop != null)
            {
                refs.sourceIconTop.gameObject.SetActive(true);
                refs.sourceIconTop.sprite = source1 != null ? source1.icon : null;
                refs.sourceIconTop.color = refs.sourceIconTop.sprite != null ? sourceIconColor : new Color(0f, 0f, 0f, 0f);
            }
            if (refs.sourceIconBottom != null)
            {
                refs.sourceIconBottom.gameObject.SetActive(true);
                refs.sourceIconBottom.sprite = source2 != null ? source2.icon : null;
                refs.sourceIconBottom.color = refs.sourceIconBottom.sprite != null ? sourceIconColor : new Color(0f, 0f, 0f, 0f);
            }
            if (refs.fusionDivider != null)
                refs.fusionDivider.gameObject.SetActive(true);
            // AJOUTE (2026-09-30, retour utilisateur : "mets un petit + entre les 2 icônes de droite pour
            // montrer que c'est une fusion de 2 items") - purement décoratif, toujours "+" (pas de sprite/texte
            // par arme), juste activé/désactivé avec le reste du visuel de fusion.
            if (refs.fusionPlusText != null)
                refs.fusionPlusText.gameObject.SetActive(true);
        }
        else
        {
            if (refs.sourceIconTop != null) refs.sourceIconTop.gameObject.SetActive(false);
            if (refs.sourceIconBottom != null) refs.sourceIconBottom.gameObject.SetActive(false);
            if (refs.fusionDivider != null) refs.fusionDivider.gameObject.SetActive(false);
            if (refs.fusionPlusText != null) refs.fusionPlusText.gameObject.SetActive(false);
        }

        if (isFusion && parchmentSober == null)
            LayoutEndScreenFusionTile(refs, upgrade, owned);

        // Une tuile de fusion n'affiche jamais de pastilles de palier, sur AUCUN écran (une fusion n'a qu'un
        // seul palier - les pastilles n'y ont jamais rien apporté) : basé sur isFusion, pas showFusionVisual, pour
        // rester vrai sur Victoire/Défaite même sans le visuel dédié (icône simple + fond violet) ci-dessus.
        // (2026-10-26 : une pastille dorée a été essayée sous la fusion de Victoire/Défaite, puis retirée à la demande de
        // l'utilisateur, la tuile montrant désormais l'icône de fusion et ses 2 armes sources.)
        // Soin (2026-10-26, même retour : "enlève les dots [...] et tu met à la place un indicateur chiffré en bas à
        // droite") : un compteur "×N" (nombre de Soins pris) remplace ses pastilles, voir plus bas.
        bool healCount = upgrade.upgradeType == UpgradeType.Heal && parchmentSober == null && refs.stackCountText != null;
        if (refs.tierDotsRow != null)
            refs.tierDotsRow.gameObject.SetActive(!healCount && !isFusion);

        if (refs.nameText != null)
        {
            refs.nameText.gameObject.SetActive(showNames);
            if (showNames)
            {
                refs.nameText.text = upgrade.Name;
                // CORRIGÉ (2026-10-18, retour utilisateur, capture à l'appui : "les titres n'ont pas la même
                // couleur [...] moi je veux qu'ils aient la même couleur") - le titre reste dans la même encre
                // (ParchmentInkColor) qu'une carte possédée, qu'elle soit débloquée ou non ; seuls l'icône et le
                // fond de la carte (assombris ailleurs) marquent la différence verrouillé/débloqué.
                // MODIFIÉ (2026-10-18, retour utilisateur : "remet quand même un contour, mais plus léger, je veux
                // que ça puisse se voir nettement") - contour remis mais nettement plus fin que le défaut (0,16,
                // pensé pour de grands titres) : 0,08 reste visible à la taille de ces cartes (~30pt) sans fusionner
                // les lettres entre elles comme l'ancien réglage (voir ApplyReadableOutline).
                refs.nameText.color = ParchmentInkColor;
                ApplyReadableOutline(refs.nameText, 0.08f);
            }
        }
        if (refs.stackCountText != null)
        {
            refs.stackCountText.gameObject.SetActive(healCount);
            if (healCount)
            {
                // Coin bas-droit, même écart (HealCountMargin) avec le bas et la droite de la tuile.
                TextMeshProUGUI count = refs.stackCountText;
                RectTransform countRt = count.rectTransform;
                countRt.anchorMin = countRt.anchorMax = countRt.pivot = new Vector2(1f, 0f);
                countRt.sizeDelta = new Vector2(80f, 30f);
                countRt.anchoredPosition = new Vector2(-HealCountMargin, HealCountMargin);
                count.alignment = TextAlignmentOptions.BottomRight;
                count.enableAutoSizing = false;
                count.fontSize = HealCountSize;
                count.margin = new Vector4(0f, 0f, HealCountSlantInset, HealCountBaselineInset);
                count.text = "×" + currentLevel;
                // MODIFIÉ (2026-10-26, retour utilisateur : "met le en couleur blanc, et rapetisse le un peu") - blanc au
                // contour sombre, comme les montants Or / Éclats.
                TextMeshProUGUI amountStyle = _gameOverGoldText != null ? _gameOverGoldText : _victoryGoldText;
                if (amountStyle != null) count.fontSharedMaterial = amountStyle.fontSharedMaterial;
                count.color = owned ? Color.white : new Color(1f, 1f, 1f, 0.55f);
            }
        }

        // Pastilles : une par palier (1 pour Double tir/Fusion, 3 pour les armes, 5 pour Dégâts / Cadence / Soin).
        int dotCount = Mathf.Clamp(maxLevel, 1, 5);

        bool requiresUnlock = upgrade.RequiresUnlockPick;
        bool showUnlockDot = refs.unlockDot != null && requiresUnlock;

        // AJOUTE (2026-09-30, retour utilisateur : "le losange bleu est mal positionné, remets-le bien sur le
        // coin en haut à droite" + "pour les fusions, le losange en haut à gauche" + "pour tir x2, la même
        // taille que les autres") - sur Victoire/Défaite (parchmentSober == null), le losange n'est plus
        // positionné via le calcul de curseur partagé avec les pastilles ci-dessous (celui-ci suppose que le
        // losange est un ENFANT de TierDotsRow, comme dans le menu Pause - ce n'est PAS le cas sur
        // UpgradeGridSlot, où le losange est un enfant direct de la tuile : la même formule y produisait un
        // résultat sans rapport). Position fixe dans un coin, taille uniforme (jamais agrandie, contrairement au
        // menu Pause où le losange seul de Double Tir est volontairement plus gros) : un coin différent pour
        // une fusion (haut-gauche, libère la place à droite pour son visuel dédié) qu'une upgrade normale
        // (haut-droite).
        // CORRIGE (2026-09-30, retour utilisateur : "le losange n'est pas bien dans l'angle, c'est parce que tu
        // mets déjà le carré puis tu fais la rotation, sauf que la rotation ça fait bouger") - le losange est un
        // carré tourné à 45° (rotation héritée du prefab). Une rotation RectTransform tourne autour du PIVOT :
        // avec pivot=(1,1) (le coin), le carré pivotait autour de son propre coin au lieu de son centre, donc le
        // "losange" résultant n'était PAS centré sur le point d'ancrage - ses 4 pointes n'étaient pas à égale
        // distance des bords. Pivot=(0.5,0.5) (rotation autour du VRAI centre du carré) corrige ça ; il faut
        // alors décaler ce centre plus loin du coin que pour un simple carré, pour que la POINTE (pas le centre)
        // se retrouve à `gap` du bord : un carré de côté W tourné à 45° a ses pointes à W/√2 de son centre.
        if (parchmentSober == null && refs.unlockDot != null)
        {
            RectTransform cornerRt = (RectTransform)refs.unlockDot.transform;
            float gap = 10f;
            float offset = gap + unlockDotSize * 0.70710678f;
            cornerRt.pivot = new Vector2(0.5f, 0.5f);
            if (isFusion)
            {
                cornerRt.anchorMin = cornerRt.anchorMax = new Vector2(0f, 1f);
                cornerRt.anchoredPosition = new Vector2(offset, -offset);
            }
            else
            {
                cornerRt.anchorMin = cornerRt.anchorMax = new Vector2(1f, 1f);
                cornerRt.anchoredPosition = new Vector2(-offset, -offset);
            }
            cornerRt.sizeDelta = new Vector2(unlockDotSize, unlockDotSize);
        }

        // AJOUTE (2026-09-30, retour utilisateur : "pour Double Tir, la dot qui s'affiche c'est celle de
        // déblocage uniquement, le losange doit être centré") - une arme à un seul palier (maxLevel<=1, donc
        // jamais réellement "graduée") gagne à afficher un simple losange (obtenue/pas obtenue) plutôt qu'une
        // pastille de palier qui n'aurait jamais de sens intermédiaire. Règle générale par maxLevel (pas un cas
        // spécial "Double Tir" codé en dur) plutôt que basée sur RequiresUnlockPick : Double Tir n'a PAS ce flag
        // (son déblocage se fait en 1 seul pick, sans étape de "déblocage" séparée d'un palier - voir
        // TotalAllowedPicks) - passer RequiresUnlockPick à true aurait changé cette mécanique de jeu (2 picks
        // nécessaires au lieu d'1) juste pour un besoin visuel, donc pas touché ici. Exclut les fusions (isFusion,
        // elles aussi maxLevel=1) : leur tierDotsRow est déjà masquée dans le menu Pause, et sur Victoire/Défaite
        // elles gardent leur pastille unique existante, jamais demandée à changer.
        bool unlockOnly = maxLevel <= 1 && !isFusion && refs.unlockDot != null;
        int effectiveDotCount = unlockOnly ? 0 : dotCount;

        System.Collections.Generic.List<Image> dots = new System.Collections.Generic.List<Image>();
        if (refs.tierDots != null)
            foreach (Image d in refs.tierDots) if (d != null) dots.Add(d);

        // CORRIGE (2026-09-30, retour utilisateur : "centré en Y avec un vertical layout") - refs.tierDots ne
        // référence QUE les 3 pastilles d'origine du prefab : un clone créé par la boucle ci-dessous lors d'un
        // appel PRÉCÉDENT (grille repeuplée plusieurs fois pour le même slot, ex. Dégâts+/Cadence+ à 5 paliers)
        // n'y est jamais ajouté - il restait donc orphelin dans TierDotsRow, actif, jamais désactivé par la
        // boucle de visibilité plus bas, et faussait le centrage du VerticalLayoutGroup (compté par le layout
        // sans qu'aucun code ne le gère). On récupère ici tout clone déjà présent dans la hiérarchie (nommé
        // "DotN", au-delà des 3 d'origine) pour qu'il soit repris par la logique existante (activé/désactivé/
        // redimensionné comme les autres) au lieu de créer un doublon en plus de lui.
        if (refs.tierDotsRow != null)
        {
            for (int i = 0; i < refs.tierDotsRow.childCount; i++)
            {
                Image existing = refs.tierDotsRow.GetChild(i).GetComponent<Image>();
                if (existing == null || dots.Contains(existing) || existing.gameObject == (refs.unlockDot != null ? refs.unlockDot.gameObject : null))
                    continue;
                if (existing.name.StartsWith("Dot"))
                    dots.Add(existing);
            }
        }

        // Le prefab n'a que 3 pastilles : on clone la dernière pour les cartes à 4-5 paliers.
        while (dots.Count < effectiveDotCount && dots.Count > 0)
        {
            Image last = dots[dots.Count - 1];
            Image clone = Instantiate(last, last.transform.parent);
            clone.name = "Dot" + (dots.Count + 1);
            dots.Add(clone);
        }

        // CORRIGE (2026-09-27, retour utilisateur : "on voit que 3 dots sur 5" + "pas bien centré") - le prefab
        // n'a PAS de mise en page automatique fonctionnelle sur TierDotsRow : chaque pastille clonée héritait
        // simplement de la position EXACTE de la dernière pastille existante (toutes empilées au même endroit,
        // invisibles les unes derrière les autres). Repositionnement explicite de chaque pastille (+ le losange de
        // déblocage s'il est visible), toute la rangée centrée sur TierDotsRow, quel que soit le nombre de paliers.
        //
        // MODIFIE (2026-09-27, retour utilisateur : "l'icône à gauche, les dots à droite, du haut vers le bas, pas
        // de gauche à droite") - sur une carte normale (pas fusion) du menu Pause, les pastilles s'empilent
        // maintenant en COLONNE verticale (à droite de l'icône, voir LayoutParchmentCard) plutôt qu'en ligne
        // horizontale en dessous - gagne de la place en hauteur. Les tuiles de fusion et Victoire/Défaite gardent
        // la ligne horizontale d'origine (aucun changement pour elles).
        bool verticalDots = parchmentSober != null && !isFusion;

        if (refs.tierDotsRow != null)
        {
            VerticalLayoutGroup vlg = refs.tierDotsRow.GetComponent<VerticalLayoutGroup>();

            float dotSize = dotCount > 3 ? dotSizeMany : dotSizeFew;

            float dotsSpan = effectiveDotCount * dotSize + Mathf.Max(0, effectiveDotCount - 1) * dotSpacing;
            // unlockOnly : pas de marge (unlockDotGap) après le losange, il n'y a rien à espacer de lui.
            // MODIFIE (2026-09-30) - sur Victoire/Défaite (parchmentSober == null), le losange est désormais fixe
            // dans un coin (voir plus haut) au lieu de partager la ligne avec les pastilles : ne plus lui
            // réserver de place ici, sinon les pastilles restent décalées pour un emplacement qui n'existe plus.
            float totalSpan = dotsSpan + (showUnlockDot && parchmentSober != null ? unlockDotSize + (effectiveDotCount > 0 ? unlockDotGap : 0f) : 0f);

            if (unlockOnly)
            {
                if (vlg != null) vlg.enabled = false;
                if (refs.unlockDot != null && parchmentSober != null)
                {
                    // CORRIGE (2026-09-30, retour utilisateur : "le losange doit être centré") - le losange est
                    // un ENFANT de TierDotsRow, donc son anchoredPosition est relatif au centre de TierDotsRow,
                    // pas au centre de la carte : (0,0) ici plaçait le losange au centre de TierDotsRow, qui
                    // lui-même n'est PAS au centre de la carte (il est décalé à droite pour la colonne de
                    // pastilles). Annuler ce décalage (-anchoredPosition de TierDotsRow) recentre bien le
                    // losange sur le centre RÉEL de la carte, quelle que soit la position de TierDotsRow.
                    Vector2 centerOfCard = -refs.tierDotsRow.anchoredPosition;
                    RectTransform unlockOnlyRt = (RectTransform)refs.unlockDot.transform;
                    unlockOnlyRt.anchoredPosition = centerOfCard;
                    // CORRIGE (2026-09-30, retour utilisateur : "c'est l'inverse qu'il fallait faire, c'est pour
                    // Double Tir qu'il doit être plus gros") - ce losange est le SEUL indicateur de la carte
                    // (pas de pastilles à côté) : plus grand que le losange normal (qui, lui, n'est qu'un élément
                    // parmi d'autres dans la colonne) pour ne pas paraître chétif seul au centre de la carte.
                    unlockOnlyRt.sizeDelta = new Vector2(unlockDotSize, unlockDotSize) * 1.6f;
                }
            }
            else if (verticalDots)
            {
                // MODIFIE (2026-09-30, retour utilisateur : "dots à droite de l'icône, centrés en Y avec un
                // vertical layout, pour rester centrés quel que soit le nombre de dots") - l'ancien calcul
                // manuel de cursorY supposait à tort que l'ancre des pastilles était le CENTRE de TierDotsRow
                // (elle est en haut : anchorMin/Max = (0,1)), donc le cluster ressortait décalé vers le haut de
                // la carte au lieu d'être centré sur l'icône. Remplacé par un vrai VerticalLayoutGroup
                // (MiddleCenter, posé sur TierDotsRow) : il centre lui-même le cluster sur toute la hauteur de
                // la rangée à chaque activation/désactivation de pastille, sans recalcul de position à la main.
                // Désactivé ici : le nombre d'enfants ACTIFS n'est fixé que plus bas (boucle des dots +
                // visibilité du losange) - le layout est (ré)activé juste après, une fois ce nombre connu (voir
                // plus bas, "vlg.enabled = false; vlg.enabled = true;").
                if (vlg != null) vlg.enabled = false;

                if (refs.unlockDot != null && showUnlockDot)
                    ((RectTransform)refs.unlockDot.transform).sizeDelta = new Vector2(unlockDotSize, unlockDotSize);
                for (int d = 0; d < dots.Count && d < effectiveDotCount; d++)
                    ((RectTransform)dots[d].transform).sizeDelta = new Vector2(dotSize, dotSize);
            }
            else
            {
                if (vlg != null) vlg.enabled = false;
                // CORRIGE (2026-09-27, retour utilisateur + capture d'écran : "les dots ne sont pas centrés") - le
                // losange/les pastilles ont leur ancre sur le bord GAUCHE de TierDotsRow (anchorMin.x=0), pas son
                // centre : démarrer le curseur à -totalSpan/2 centrait donc la grappe sur le bord gauche de la
                // ligne au lieu de son centre. Uniquement pour le menu Pause (parchmentSober != null, où la largeur
                // de ligne est un point fixe non-étiré) : sur Victoire/Défaite, TierDotsRow est étiré sur toute la
                // largeur de la tuile et ce calcul restait correct tel quel.
                float rowLeftEdgeOffset = parchmentSober != null ? refs.tierDotsRow.sizeDelta.x / 2f : 0f;
                float cursorX = rowLeftEdgeOffset - totalSpan / 2f;

                // Le losange/les pastilles ont leur ancre en HAUT de TierDotsRow : recentre aussi verticalement
                // dans la hauteur réelle de la ligne (Pause uniquement) plutôt que l'ancien offset figé -30.
                bool recenterY = parchmentSober != null;
                float dotY = recenterY ? -refs.tierDotsRow.sizeDelta.y / 2f : 0f;

                if (refs.unlockDot != null && showUnlockDot && parchmentSober != null)
                {
                    RectTransform unlockRt = (RectTransform)refs.unlockDot.transform;
                    float y = recenterY ? dotY : unlockRt.anchoredPosition.y;
                    unlockRt.anchoredPosition = new Vector2(cursorX + unlockDotSize / 2f, y);
                    cursorX += unlockDotSize + unlockDotGap;
                }
                for (int d = 0; d < dots.Count && d < effectiveDotCount; d++)
                {
                    RectTransform dotRt = (RectTransform)dots[d].transform;
                    dotRt.sizeDelta = new Vector2(dotSize, dotSize);
                    float y = recenterY ? dotY : dotRt.anchoredPosition.y;
                    dotRt.anchoredPosition = new Vector2(cursorX + dotSize / 2f, y);
                    cursorX += dotSize + dotSpacing;
                }
            }
        }

        for (int d = 0; d < dots.Count; d++)
        {
            bool dotExists = d < effectiveDotCount;
            dots[d].gameObject.SetActive(dotExists);
            if (!dotExists) continue;

            bool filled = d < currentLevel;
            dots[d].color = !filled ? colEmpty : (alreadyMaxed ? colMax : colFilled);
        }

        // Losange de déblocage (armes à pick séparé : Orbital, Foudre, Boue, etc. - OU une arme à un seul
        // palier comme Double Tir, voir unlockOnly plus haut : même losange, mais coloré selon "obtenue" plutôt
        // que "déjà débloquée avant ce pick", puisqu'il n'y a pas d'étape de déblocage séparée à distinguer).
        bool showDiamond = showUnlockDot || unlockOnly;
        if (refs.unlockDot != null)
        {
            refs.unlockDot.gameObject.SetActive(showDiamond);
            if (showDiamond)
                refs.unlockDot.color = unlockOnly ? (owned ? colFilled : colEmpty) : (upgrade.IsUnlocked() ? colFilled : colEmpty);
        }

        // AJOUTE (2026-09-30) - le VerticalLayoutGroup de TierDotsRow ne reconstruit sa liste d'enfants actifs
        // qu'à son propre OnEnable (pas simplement quand un enfant change d'état actif/inactif au-dessus) : le
        // rebasculer OFF puis ON ICI, une fois le nombre RÉEL de pastilles/losange actifs connu (boucles
        // ci-dessus), force cette liste à jour avant le calcul de centrage - sinon un enfant resté "vu" par le
        // groupe depuis un état précédent (ex. losange compté alors qu'il vient d'être désactivé) décale tout le
        // cluster au lieu de le centrer sur l'icône.
        if (verticalDots && refs.tierDotsRow != null)
        {
            VerticalLayoutGroup vlg = refs.tierDotsRow.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                vlg.enabled = false;
                vlg.spacing = dotSpacing;
                vlg.enabled = true;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(refs.tierDotsRow);
        }

        // AJOUTE (2026-09-27) - clic sur une carte (menu Pause uniquement, onSlotClicked fourni par
        // PauseMenuUI) : affiche sa description. Aucun effet visuel de survol/pression (Transition.None) pour ne
        // pas casser le style "parchemin" existant des tuiles.
        Button btn = slotGO.GetComponent<Button>();
        if (onSlotClicked != null)
        {
            if (btn == null) btn = slotGO.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => onSlotClicked(upgrade));
        }
        else if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
        }

        // AJOUTE (2026-09-27) - survol (entrée/sortie souris) de la carte, transmis à PauseMenuUI pour fermer le
        // panneau de description en fondu quand la souris quitte la carte active (retour utilisateur).
        UpgradeCardHover hover = slotGO.GetComponent<UpgradeCardHover>();
        if (onSlotHoverEnter != null || onSlotHoverExit != null)
        {
            if (hover == null) hover = slotGO.AddComponent<UpgradeCardHover>();
            hover.Upgrade = upgrade;
            hover.OnEnter = onSlotHoverEnter;
            hover.OnExit = onSlotHoverExit;
        }
        else if (hover != null)
        {
            hover.OnEnter = null;
            hover.OnExit = null;
        }
    }

    // AJOUTÉ (2026-10-26, retour utilisateur : "je trouve ça dommage d'utiliser la place de carte pour juste mettre
    // l'icone de la fusion au milieu avec une dot en dessous") - tuile de fusion de Victoire/Défaite (2 colonnes) :
    // MODIFIÉ (2026-10-26, même jour : "ne met pas leur noms à côté, parce que c'est bizarre c'est les seul qui ont du
    // texte [...] laisse l'icone de fusion à gauche, et à droite, tu met l'icones des 2 sous fusions cote à cote en
    // légèrement plus petit, avec la barre verticale entre les 2 parties. Il doit y avoir le même écart entre le bord
    // droit du cadre, le côté droit de l'icone le plus à droite, par rapport au bord gauche de la case, et du bord gauche
    // de l'icone de la fusion") - icône de fusion à gauche (taille et hauteur des icônes des autres tuiles, pastille dorée
    // dessous), les 2 armes sources côte à côte à droite, un filet vertical à mi-chemin entre les deux parties, et le
    // même écart FusionTileInset avec le bord gauche et le bord droit de la tuile.
    // Positions relatives aux bords de la tuile : elle n'a pas encore sa taille finale ici (PositionArsenalSlot après).
    // MODIFIÉ (2026-10-26, retour utilisateur : "enlève la dot qu'il y a sous l'icone de fusion, et je veux que tu
    // recentre l'icone de fusion pour qu'il y ait le même écart entre la barre de séparation et le bord gauche de la
    // case. Aussi, tu peux donc agrandir l'icone de fusion") - plus de pastille : tout est centré en hauteur dans la
    // tuile ; l'icône de fusion, plus grande (FusionTileIconGrow), est centrée entre le bord gauche et le filet.
    // Tout est placé depuis le bord droit (armes sources, filet) : la tuile n'a pas encore sa largeur finale ici.
    private const float FusionTileInset = 14f;          // bord droit de la tuile -> bord droit de la dernière arme source
    private const float FusionTileSourceScale = 0.8f;   // armes sources un peu plus petites qu'une icône de case simple
    private const float FusionTileSourceGap = 8f;
    private const float FusionTileRuleGap = 16f;        // 1re arme source -> filet
    private const float FusionTileIconGrow = 1.3f;      // icône de fusion par rapport à une icône de case simple
    private const float FusionTileRuleHeight = 70f;

    private void LayoutEndScreenFusionTile(UpgradeSlotRefs refs, UpgradeData upgrade, bool owned)
    {
        RectTransform iconRt = refs.icon != null ? refs.icon.rectTransform : null;
        if (iconRt == null) return;

        float baseIcon = iconRt.sizeDelta.y * _endTileScale;      // icône d'une case simple, à l'échelle des cases
        float sourceSize = baseIcon * FusionTileSourceScale;
        float ruleFromRight = FusionTileInset + 2f * sourceSize + FusionTileSourceGap + FusionTileRuleGap;

        // Icône de fusion : centrée entre le bord gauche et le filet (ancrée au centre : -ruleFromRight / 2).
        float iconSize = baseIcon * FusionTileIconGrow;
        iconRt.anchorMin = iconRt.anchorMax = iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.sizeDelta = new Vector2(iconSize, iconSize);
        iconRt.anchoredPosition = new Vector2(-ruleFromRight * 0.5f, 0f);

        // Armes sources : côte à côte, calées à droite. Leur rangée (pile horizontale du prefab) couvre toute la tuile,
        // positions posées ici.
        Image[] sources = { refs.sourceIconTop, refs.sourceIconBottom };
        UpgradeData[] sourceData = { FindUpgradeByType(upgrade.FusionSource1), FindUpgradeByType(upgrade.FusionSource2) };
        Color sourceColor = owned ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.75f);
        RectTransform row = refs.sourceIconTop != null ? refs.sourceIconTop.rectTransform.parent as RectTransform : null;
        if (row != null)
        {
            HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            if (rowLayout != null) rowLayout.enabled = false;
            row.anchorMin = Vector2.zero;
            row.anchorMax = Vector2.one;
            row.offsetMin = row.offsetMax = Vector2.zero;
        }
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] == null) continue;
            sources[i].gameObject.SetActive(true);
            sources[i].sprite = sourceData[i] != null ? sourceData[i].icon : null;
            sources[i].color = sources[i].sprite != null ? sourceColor : new Color(0f, 0f, 0f, 0f);
            sources[i].preserveAspect = true;
            RectTransform rt = sources[i].rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(sourceSize, sourceSize);
            float fromRight = FusionTileInset + sourceSize * 0.5f + (i == 0 ? sourceSize + FusionTileSourceGap : 0f);
            rt.anchoredPosition = new Vector2(-fromRight, 0f);
        }
        if (refs.fusionPlusText != null) refs.fusionPlusText.gameObject.SetActive(false);

        if (refs.fusionDivider != null)
        {
            refs.fusionDivider.gameObject.SetActive(true);
            RectTransform rule = refs.fusionDivider.rectTransform;
            rule.anchorMin = rule.anchorMax = new Vector2(1f, 0.5f);
            rule.pivot = new Vector2(0.5f, 0.5f);
            rule.sizeDelta = new Vector2(FusionTileRuleHeight, rule.sizeDelta.y);   // filet couché tourné de 90° : x = longueur
            rule.anchoredPosition = new Vector2(-ruleFromRight, 0f);
            if (rule.GetComponent<PixelSnapRule>() == null) rule.gameObject.AddComponent<PixelSnapRule>();
        }
    }

    private UpgradeData FindUpgradeByType(UpgradeType type)
    {
        if (LevelUpManager.Instance == null || LevelUpManager.Instance.AllUpgrades == null) return null;
        foreach (UpgradeData u in LevelUpManager.Instance.AllUpgrades)
            if (u != null && u.upgradeType == type) return u;
        return null;
    }

    // MODIFIE (2026-10-07, retour utilisateur : "c'est toujours aussi clair, et c'est difficile de lire de loin")
    // - le texte blanc + contour sombre (voir ancien commentaire ci-dessous) reste trop peu contrasté sur un
    // parchemin clair, vu de loin ou compressé. Remplacé par une couleur "encre" (comme le reste du dessin du
    // parchemin, bordure végétale incluse - cohérent avec le style plutôt qu'un ajout) + un fin halo clair à la
    // place du contour sombre, pour que le texte se détache sans jamais disparaître dans le papier.
    // ANCIEN (2026-09-27) : "le texte blanc sur ce genre de fond pour les cartes est pas très visible" - ce
    // correctif-ci avait déjà tenté de régler le même retour avec un contour sombre sur texte BLANC ; insuffisant.
    // RETOUCHÉ (2026-10-07, retour utilisateur : 1er essai en brun quasi-noir "je ne sais pas trop quoi en
    // penser, c'est très différent d'avant" → "trouve un entre deux, essaie de faire de la même couleur que les
    // dessins sur les côtés des parchemins") - éclairci pour reprendre la teinte sépia/brun réelle du TRAIT de la
    // bordure végétale dessinée (mesurée à l'œil sur parchment_sobre_final.jpg/ParcheminDoré.png : un brun sienna
    // moyen, pas un brun-noir) plutôt qu'une encre plus sombre inventée.
    public static readonly Color ParchmentInkColor = new Color32(0x6B, 0x42, 0x26, 0xFF);

    // AJOUTÉ (2026-10-18, retour utilisateur : "pour la nouvelle fusion [...] je veux que ce qu'il y a en dessous tu
    // mette une couleur un peu plus faite pour voir des petits textes descriptifs, donc une couleur plus clair")
    // - variante plus claire/douce de ParchmentInkColor (même teinte sépia, luminosité remontée) réservée aux petits
    // textes descriptifs secondaires (ex: description de défi sous le titre) pour les distinguer visuellement des
    // titres/noms, qui gardent l'encre pleine ParchmentInkColor.
    // MODIFIÉ (2026-10-25, retour utilisateur : "la description du défi n'est pas de la bonne couleur par rapport à
    // la couleur du fond") - l'ancienne encre claire #9C7A5E ne donnait que 2,9:1 de contraste sur le parchemin
    // (#ECDDBB), délavée sous son contour sombre. 4 teintes comparées sur capture en jeu : brun rouille #7A3B1C retenu
    // (6,3:1, aussi lisible que le titre), qui reste distinct du titre par la teinte plutôt que par la clarté et
    // s'accorde au sceau de cire du parchemin.
    public static readonly Color ParchmentInkColorLight = new Color32(0x7A, 0x3B, 0x1C, 0xFF);

    // MODIFIÉ (2026-10-18, retour utilisateur, capture à l'appui : "le texte est trop gros, et j'ai l'impression
    // qu'il est en gras, donc les lettres se chevauchent en gros blanc") - `outlineWidth` est une fraction FIXE de
    // la taille de police (pas des pixels absolus) : 0,16 reste fin sur un grand titre (44-64pt) mais devient un
    // contour énorme, qui déborde d'une lettre à l'autre, sur un petit texte (ex. 22pt, l'infobulle de description
    // du menu Pause) - zoom x3 à l'appui, les lettres fusionnaient littéralement entre elles. `outlineWidth`
    // paramétrable (défaut 0,16 inchangé pour tous les appels existants), l'infobulle Pause passe désormais 0,06.
    // AJOUTÉ (2026-10-18, diagnostic complet - audit §2.8) - `text.fontMaterial` (au lieu de `fontSharedMaterial`)
    // fait cloner à TMP un NOUVEAU Material à chaque appel sur un texte qui n'en a pas encore un à lui ; si les
    // tuiles de la grille du menu Pause/Victoire/Défaite sont réinstanciées (pas réutilisées) à chaque ouverture,
    // ces clones s'accumulent sans jamais être libérés avant le prochain chargement de scène (petite fuite mémoire
    // sur une longue session). Seules 2 combinaisons (police, largeur de contour) existent réellement dans tout le
    // jeu (0,16 par défaut, 0,06 pour l'infobulle du menu Pause) : un seul Material partagé est créé par combinaison
    // la première fois qu'elle est demandée, puis réutilisé par TOUS les textes suivants via `fontSharedMaterial`
    // (jamais `fontMaterial`, qui redéclencherait un clone individuel).
    private static readonly Dictionary<(TMP_FontAsset font, float width), Material> _outlineMaterialCache = new Dictionary<(TMP_FontAsset, float), Material>();
    private static readonly Color32 OutlineColor = new Color32(0xF5, 0xEC, 0xD6, 0xFF);

    public static void ApplyReadableOutline(TextMeshProUGUI text, float outlineWidth = 0.16f)
    {
        if (text == null || text.font == null) return;

        var key = (text.font, outlineWidth);
        if (!_outlineMaterialCache.TryGetValue(key, out Material shared) || shared == null)
        {
            shared = new Material(text.fontSharedMaterial);
            shared.EnableKeyword("OUTLINE_ON");
            shared.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);
            shared.SetColor(ShaderUtilities.ID_OutlineColor, OutlineColor);
            _outlineMaterialCache[key] = shared;
        }
        text.fontSharedMaterial = shared;
    }

    private Color GetTileTint(UpgradeBranch branch)
    {
        switch (branch)
        {
            case UpgradeBranch.Aether: return _tileTintAether;
            case UpgradeBranch.Kael: return _tileTintKael;
            case UpgradeBranch.Lyra: return _tileTintLyra;
            default: return _tileTintUniversal;
        }
    }

    // AJOUTE - repli de PlayNextUnlockPreview() quand HasPreview est faux (la
    // branche active ET les 3 nœuds de Réputation sont entièrement maxés à la
    // fois - un joueur très avancé peut vraiment l'atteindre, observé en
    // conditions réelles pendant cette passe). Ton positif/fier plutôt qu'un
    // trou vide : le joueur a littéralement tout fini côté progression pour
    // ce personnage, ça mérite d'être dit.
    // MODIFIE (2026-09-12, 3e passe) - "Il ne reste que la gloire, ici."
    // retiree sur demande utilisateur.
    private static readonly string[] _gameOverMaxedOutLines =
    {
        "Tu as tout débloqué dans cette branche",
        "Plus rien à débloquer ici — change de personnage pour la suite",
        "Cette branche n'a plus de secret pour toi",
        "Arbre de compétences et Réputation au maximum. Change de personnage pour découvrir autre chose.",
    };

    // AJOUTE - enchaine la sequence normale de l'Or (voir PlayGoldSequence) puis,
    // une fois terminee, revele l'apercu du prochain palier de progression -
    // le tout dans une seule coroutine pour garder ShowGameOver() simple.
    private IEnumerator PlayGoldSequenceThenPreview(int baseGold, int totalGold, bool challengeCompleted, float challengeRewardPercent)
    {
        yield return StartCoroutine(PlayGoldSequence(_gameOverGoldText, _gameOverChallengeText, baseGold, totalGold, challengeCompleted, challengeRewardPercent));
        yield return StartCoroutine(PlayNextUnlockPreview());

        // AJOUTE - deverrouille le bouton Rejouer et signale a GameManager que
        // le raccourci clavier peut s'activer, seulement maintenant que toute
        // la sequence de reveal est terminee.
        if (_gameOverRetryButton != null) _gameOverRetryButton.interactable = true;
        OnEndScreenRevealComplete?.Invoke();
    }

    // AJOUTE - calcule et affiche (en fondu doux) le message "Encore X Or pour
    // debloquer [Noeud]", ou le message positif si le joueur a deja de quoi se
    // l'offrir. Ne fait rien si aucun palier n'est trouvable (arbre + Reputation
    // entierement maxes - cas rare mais gere proprement plutot que d'afficher
    // un texte vide ou incoherent).
    // MODIFIE - verifie d'abord ConsumePendingUnlockNotification() : si un
    // personnage vient d'etre debloque pendant CETTE partie (ex. Kael au
    // spawn du Boss 2), l'annoncer ici prend le pas sur l'apercu du prochain
    // palier - montrer "encore X Or pour debloquer Y" juste apres avoir
    // debloque quelqu'un serait anticlimatique. Voir NOTES.md / V13 §"Systeme
    // de deblocage des personnages" - c'etait deja prevu, jamais cable.
    // MODIFIÉ (2026-10-26) - texte calculé et posé (transparent) dès l'ouverture par PrepareNextUnlockText : le cadre
    // d'infos connaît ainsi sa hauteur finale tout de suite, la coroutine ne fait plus que le fondu d'apparition.
    private IEnumerator PlayNextUnlockPreview()
    {
        if (_gameOverNextUnlockText == null) yield break;

        CanvasGroup cg = _gameOverNextUnlockText.GetComponent<CanvasGroup>();
        if (cg == null) cg = _gameOverNextUnlockText.gameObject.AddComponent<CanvasGroup>();
        _gameOverNextUnlockText.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < _nextUnlockFadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Clamp01(elapsed / _nextUnlockFadeInDuration);
            yield return null;
        }
        cg.alpha = 1f;
    }

    private void PrepareNextUnlockText()
    {
        if (_gameOverNextUnlockText == null) return;

        string unlockedCharacter = MetaProgressionManager.Instance != null
            ? MetaProgressionManager.Instance.ConsumePendingUnlockNotification()
            : null;

        string text;
        bool highlight = false;

        if (unlockedCharacter != null)
        {
            text = Loc.F("Nouveau personnage débloqué : {0} !", unlockedCharacter);
            highlight = true;
        }
        else
        {
            MetaProgressionManager.NextUnlockPreview preview = MetaProgressionManager.Instance != null
                ? MetaProgressionManager.Instance.GetNextUnlockPreview()
                : null;

            // MODIFIE - avant, masquait completement ce texte si HasPreview est
            // faux (branche + Reputation entierement maxees - un joueur tres
            // avance peut vraiment atteindre ce cas, verifie en conditions
            // reelles). Repli sur une phrase dediee plutot que de laisser un
            // trou : cette ligne comble sinon systematiquement l'espace du
            // panel de gauche (voir le meme souci deja traite cote Victoire
            // avec QuietLine), pas de raison que le "cas parfait" en soit prive.
            if (preview == null || !preview.HasPreview)
            {
                text = Loc.T(_gameOverMaxedOutLines[UnityEngine.Random.Range(0, _gameOverMaxedOutLines.Length)]);
            }
            else
            {
                // MODIFIÉ (2026-10-15) - "Éclats" accordé au nombre ("Encore 1 Éclat", russe "21 осколка")
                string currencyLabel = preview.IsGoldCurrency ? Loc.T("Or") : Loc.PluralWord(preview.AmountStillNeeded, "Éclat", "Éclats");
                text = preview.AmountStillNeeded > 0
                    ? Loc.F("Encore {0} {1} pour débloquer {2}", preview.AmountStillNeeded, currencyLabel, Loc.T(preview.NodeName))
                    : Loc.F("De quoi débloquer {0} dès maintenant !", Loc.T(preview.NodeName));
            }
        }

        // MODIFIE - #33271A (encre foncée, même teinte que StatStrip - déjà
        // confirmée lisible sur ce fond) plutôt que l'ink-soft #5A4834 d'origine :
        // retour utilisateur avec capture à l'appui, ce texte était presque
        // illisible sur la carte assombrie du Game Over. Le fond étant plus
        // sombre que celui de la Victoire, le texte doit compenser en contraste,
        // pas juste reprendre la même nuance "discrète" qui marchait sur un fond
        // plus clair.
        // MODIFIÉ (2026-10-26) - dans le cadre d'infos (fond clair des cartes Or / Éclats), le doré _challengeSuccessColor
        // ne se lirait plus : l'annonce d'un nouveau personnage passe à l'encre rouille ParchmentInkColorLight.
        _gameOverNextUnlockText.text = text;
        _gameOverNextUnlockText.color = highlight ? ParchmentInkColorLight : new Color32(0x33, 0x27, 0x1A, 0xFF);

        CanvasGroup cg = _gameOverNextUnlockText.GetComponent<CanvasGroup>();
        if (cg == null) cg = _gameOverNextUnlockText.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        _gameOverNextUnlockText.gameObject.SetActive(true);
    }

    // AJOUTE - phrases de clôture du récap de victoire (temps 3 de la refonte
    // 3-temps). Toujours à propos du boss final : la Victoire ne se déclenche
    // QUE quand "La Source Corrompue" (3ᵉ boss) est vaincue, donc pas besoin de
    // paramétrer quel boss - c'est toujours le même. Même famille de ton que
    // GameOverMessagePool (familier, direct, sec), version positive.
    // MODIFIE (2026-09-12) - 4 lignes retirees sur demande utilisateur
    // (relecture "jeu serieux / Steam") ; les 3 restantes sont volontairement
    // celles ancrees dans le lore ("La Source Corrompue", "la corruption") -
    // registre prefere a un ton plus generique/sportif ("zero pitie", etc.).
    private static readonly string[] _victoryClosers =
    {
        "La Source Corrompue n'a pas tenu la distance",
        "Elle ne se relèvera pas",
        "La corruption s'éteint ici",
    };

    // AJOUTE - pool pour PopulateEmptyBuildState côté Victoire (0 upgrade
    // obtenue mais victoire quand même - rare mais possible en théorie sur un
    // run très rapide/chanceux avant le boss 3).
    private static readonly string[] _victoryEmptyBuildLines =
    {
        "Une victoire sans la moindre compétence. Remarquable.",
        "Aucune compétence récupérée, et la victoire malgré tout.",
        "Rien pris en cours de route, et pourtant tu es là.",
    };

    // AJOUTE - pool pour PopulateEmptyBuildState côté Game Over (mort très
    // précoce, avant le premier level-up - confirmé en conditions réelles,
    // pas juste théorique).
    private static readonly string[] _gameOverEmptyBuildLines =
    {
        "Tu n'as pas eu le temps de récupérer la moindre compétence",
        "Aucune compétence récupérée cette fois",
    };

    // AJOUTE - pool dédié à PopulateVictoryQuietLine (voir plus bas). Sujet
    // volontairement différent de _victoryClosers ci-dessus (qui parle déjà du
    // boss vaincu) : ici on reconnaît l'absence de record/défi sans plomber le
    // ton, même famille de voix que le reste (familier, direct, un peu de
    // personnalité, jamais ronflant).
    // MODIFIE (2026-09-12) - 4 lignes retirees sur demande utilisateur
    // (relecture "jeu serieux / Steam").
    private static readonly string[] _victoryQuietLines =
    {
        "Pas de record aujourd'hui, mais la victoire est bien réelle.",
        "Rien d'exceptionnel à signaler, à part la victoire elle-même.",
        "Aucun exploit cette fois — juste une victoire de plus",
    };

    // AJOUTE - récap avec du ton (temps 1), remplace l'ancien SubtitleText statique.
    // ---- Adaptation des écrans de fin au mode de jeu (2026-09-26) ------------------------------------------------------------

    // "Tentative n°X" propre à chaque mode ; le nom du mode précède le numéro hors Classique.
    private string BuildAttemptLabel()
    {
        int n = MetaProgressionManager.Instance != null ? MetaProgressionManager.Instance.GetModeAttemptNumber(GameModes.Current) : 1;
        return GameModes.IsClassic
            ? Loc.F("Tentative n°{0}", n)
            : GameModes.ShortName(GameModes.Current) + "\n" + Loc.F("Tentative n°{0}", n);
    }

    // Écrit "Tentative n°X" (précédé du nom du mode hors Classique, sur 2 lignes) dans un cadre assez large pour ne jamais
    // couper ni décaler le texte.
    private void ApplyAttemptLabel(TextMeshProUGUI label)
    {
        if (label == null) return;
        label.text = BuildAttemptLabel();
        if (!GameModes.IsClassic)
        {
            label.enableWordWrapping = false;
            label.rectTransform.sizeDelta = new Vector2(460f, 60f);
        }
    }

    // Le liseré sous le message doit toujours dépasser le texte : on adapte sa largeur (et celle du cadre du message) à la
    // longueur réelle du texte affiché.
    private void FitSeparatorToText(TextMeshProUGUI text)
    {
        if (text == null) return;
        text.ForceMeshUpdate();
        float preferred = text.GetPreferredValues(text.text, 100000f, 0f).x;

        RectTransform box = text.rectTransform;
        box.sizeDelta = new Vector2(Mathf.Clamp(preferred + 40f, 1150f, 1560f), box.sizeDelta.y);

        Transform separator = text.transform.parent != null ? text.transform.parent.Find("Separator1") : null;
        if (separator != null)
        {
            RectTransform line = (RectTransform)separator;
            line.sizeDelta = new Vector2(Mathf.Clamp(preferred + 160f, 950f, 1600f), line.sizeDelta.y);
        }
    }

    // Modes sans XP (Choc des titans) : la barre d'XP et le niveau disparaissent du HUD, et la rangée de compétences
    // (Clone / Esquive / Ultime) descend à la place qu'ils occupaient.
    public void HideXpHud()
    {
        if (_xpBar == null) return;
        Transform group = _xpBar.transform.parent;
        if (group != null) group.gameObject.SetActive(false);

        Transform hud = group != null ? group.parent : null;
        HudStyler styler = hud != null ? hud.GetComponent<HudStyler>() : null;
        if (styler != null) styler.SetXpHidden(true);
    }

    // Ligne de stats : "Boss N/3" en classique, "Boss N/total" en ruée, "Boss N" en sans fin (pas de plafond).
    private string BuildStatLine(int mins, int secs, int killCount, int level, int bossKills)
    {
        // Choc des titans : ni ennemis ni niveaux, seulement le temps et les titans abattus.
        if (GameModes.IsTitans) return Loc.F("Survie {0:00}:{1:00}    ·    Titans {2}/3", mins, secs, bossKills);

        string boss;
        if (GameModes.IsEndless) boss = Loc.F("Boss {0}", bossKills);
        else if (GameModes.IsTitans) boss = Loc.F("Titans {0}/3", bossKills);
        else if (GameModes.IsBossRush)
        {
            int total = GameModes.RushBossCount(MetaProgressionManager.Instance != null ? MetaProgressionManager.Instance.Data : null);
            boss = Loc.F("Boss {0}/{1}", bossKills, total);
        }
        else boss = Loc.F("Boss {0}/{1}", bossKills, 3);

        return Loc.F("Survie {0:00}:{1:00}    ·    Éliminations {2}    ·    Niveau {3}    ·    {4}", mins, secs, killCount, level, boss);
    }

    // Le titre est du texte de scène (CenterCard/Crown/TitleText) : réécrit à CHAQUE affichage pour ne jamais garder
    // le titre d'un autre mode.
    private void SetEndScreenTitle(GameObject panel, string text)
    {
        if (panel == null) return;
        Transform t = panel.transform.Find("CenterCard/Crown/TitleText");
        TextMeshProUGUI tmp = t != null ? t.GetComponent<TextMeshProUGUI>() : null;
        if (tmp != null) tmp.text = text;
    }

    private static readonly string[] _endlessEndLines =
    {
        "{t} de survie, {b} boss abattus. Tu peux tenir plus longtemps.",
        "Tu as tenu {t} face à des boss de plus en plus féroces",
        "{b} boss vaincus avant la chute. Le prochain record est à portée.",
    };

    private string BuildModeGameOverMessage(float runTime, int bossKillCount)
    {
        if (GameModes.IsBossRush)
        {
            int total = GameModes.RushBossCount(MetaProgressionManager.Instance != null ? MetaProgressionManager.Instance.Data : null);
            return Loc.F("{0} boss sur {1}. La ruée ne pardonne pas — reviens plus fort.", bossKillCount, total);
        }
        if (GameModes.IsTitans)
        {
            string killed = bossKillCount <= 0 ? Loc.T("Aucun titan abattu")
                : bossKillCount == 1 ? Loc.T("1 titan abattu")
                : Loc.F("{0} titans abattus", bossKillCount);
            return Loc.F("{0} sur 3. Les trois ensemble ne pardonnent pas — reviens plus fort.", killed);
        }

        string line = Loc.T(_endlessEndLines[UnityEngine.Random.Range(0, _endlessEndLines.Length)]);
        return line.Replace("{t}", FormatTime(runTime)).Replace("{b}", bossKillCount.ToString());
    }

    private void PopulateVictoryRecap(float runTime, int killCount)
    {
        if (_victoryRecapText == null) return;

        if (GameModes.IsBossRush)
        {
            int total = GameModes.RushBossCount(MetaProgressionManager.Instance != null ? MetaProgressionManager.Instance.Data : null);
            _victoryRecapText.text = Loc.F("{0} boss abattus en {1}. La ruée est à toi", total, FormatTime(runTime));
            return;
        }
        if (GameModes.IsTitans)
        {
            _victoryRecapText.text = Loc.F("Les 3 titans terrassés en {0}. Rien ne t'arrête.", FormatTime(runTime));
            return;
        }

        string closer = Loc.T(_victoryClosers[UnityEngine.Random.Range(0, _victoryClosers.Length)]);
        _victoryRecapText.text = Loc.F("{0} de combat, {1} ennemis vaincus. {2}", FormatTime(runTime), killCount, closer);
    }

    // AJOUTE - highlight d'un record BATTU cette partie (temps 1). Volontairement
    // séparé de PopulateGameOverMessage() (qui a sa propre variante avec un
    // fallback "record presque battu") pour ne rien risquer sur l'écran de Game
    // Over déjà en place - même logique de détection dupliquée ici sciemment.
    // Masqué si aucun record n'est battu (pas de highlight "presque" côté Victoire,
    // la victoire elle-même porte déjà la note positive).
    // MODIFIE - renvoie désormais un bool (record affiché ou non) pour que
    // ShowVictory puisse décider d'afficher PopulateVictoryQuietLine à la place
    // quand ni le record ni le défi ne sont là.
    private bool PopulateVictoryRecordHighlight(float runTime, int killCount, int levelReached, int goldThisRun)
    {
        if (_victoryRecordHighlight == null) return false;

        string message = null;

        if (!GameModes.IsClassic)
        {
            message = MetaProgressionManager.Instance != null ? MetaProgressionManager.Instance.LastRunRecordMessage : null;
            _victoryRecordHighlight.SetActive(message != null);
            if (message != null && _victoryRecordHighlightText != null)
                _victoryRecordHighlightText.text = message;
            return message != null;
        }

        if (MetaProgressionManager.Instance != null && MetaProgressionManager.Instance.Data != null)
        {
            SaveData data = MetaProgressionManager.Instance.Data;

            // totalRuns > 1 : meme garde que PopulateGameOverMessage, jamais de
            // "nouveau record" sur la toute premiere partie (rien a battre).
            if (data.totalRuns > 1 && GameModes.IsClassic)
            {
                RecordCheck[] checks =
                {
                    new RecordCheck { current = Mathf.Floor(runTime), best = Mathf.Floor(MetaProgressionManager.Instance.PreviousBestTime), newRecordMessage = Loc.F("Nouveau record de survie : {0} !", FormatTime(runTime)) },
                    new RecordCheck { current = killCount, best = MetaProgressionManager.Instance.PreviousBestKills, newRecordMessage = Loc.F("Nouveau record d'éliminations : {0} !", killCount) },
                    new RecordCheck { current = levelReached, best = MetaProgressionManager.Instance.PreviousBestLevel, newRecordMessage = Loc.F("Nouveau record de niveau : {0} !", levelReached) },
                    new RecordCheck { current = goldThisRun, best = MetaProgressionManager.Instance.PreviousBestGoldInRun, newRecordMessage = Loc.F("Nouveau record d'Or en une partie : {0} !", goldThisRun) },
                };

                foreach (RecordCheck check in checks)
                {
                    if (check.best > 0f && check.current > check.best)   // égalité : pas un record (voir PreviousBestTime)
                    {
                        message = check.newRecordMessage;
                        break;
                    }
                }
            }
        }

        _victoryRecordHighlight.SetActive(message != null);
        if (message != null && _victoryRecordHighlightText != null)
            _victoryRecordHighlightText.text = message;

        return message != null;
    }

    // AJOUTE - phrase de texture (temps 1) affichée UNIQUEMENT quand ni le
    // highlight de record ni le chip de défi ne sont présents cette partie -
    // ces deux blocs suffisent déjà à remplir le panel de gauche quand l'un
    // d'eux est là ; sans les deux, le contenu restant (titre + cartes Or/
    // Éclats + stat strip) laisse un vide sensible. Une phrase choisie au
    // hasard comble cet espace sans réintroduire de stats permanentes (voir
    // maquette validée : les records à vie n'ont volontairement pas leur place
    // ici, "ça dégonflerait le moment").
    private void PopulateVictoryQuietLine(bool recordShown, bool challengeCompleted)
    {
        if (_victoryQuietLineText == null) return;

        bool showQuietLine = !recordShown && !challengeCompleted;
        _victoryQuietLineText.gameObject.SetActive(showQuietLine);
        if (showQuietLine)
            _victoryQuietLineText.text = Loc.T(_victoryQuietLines[UnityEngine.Random.Range(0, _victoryQuietLines.Length)]);
    }

    // AJOUTE - portrait du perso victorieux (temps 2). Réutilise les illustrations
    // de la page de sélection (CharacterSelectUI._spriteAether/Kael/sprayteLyra),
    // copiées dans _characterPortraits au moment du câblage de la scène - les
    // modèles 3D ne sont pas assez détaillés vus de près/de face pour un portrait.
    private void PopulateVictoryPortrait()
    {
        if (_victoryPortraitImage == null || _characterPortraits == null || _characterPortraits.Length == 0) return;
        if (MetaProgressionManager.Instance == null) return;

        int index = Mathf.Clamp(MetaProgressionManager.Instance.GetSelectedCharacterIndex(), 0, _characterPortraits.Length - 1);
        Sprite portrait = _characterPortraits[index];
        if (portrait != null)
            _victoryPortraitImage.sprite = portrait;
    }

    public void ShowVictory(float runTimer, int killCount, int baseGold, int totalGold, int level, int eclatsEarned, bool challengeCompleted, float challengeRewardPercent)
    {
        Debug.Log($"[GameUI] ShowVictory sur {gameObject.name} (id {GetInstanceID()}) — GoldText null ? {_victoryGoldText == null} — EclatsText null ? {_victoryEclatsText == null}");
        if (_victoryPanel != null) _victoryPanel.SetActive(true);

        int mins = Mathf.FloorToInt(runTimer / 60f);
        int secs = Mathf.FloorToInt(runTimer % 60f);

        // MODIFIE - refonte 3-temps : une seule ligne compacte au lieu du pavé de
        // 3 lignes. "Boss 3/3" est fixe : la Victoire ne se déclenche QUE quand
        // les 3 boss sont vaincus (invariant WaveManager.OnBossDied -> GameManager).
        int victoryBossKills = GameManager.Instance != null ? GameManager.Instance.BossKillCount : 3;
        if (_victoryStatsText != null)
            _victoryStatsText.text = BuildStatLine(mins, secs, killCount, level, victoryBossKills);

        SetEndScreenTitle(_victoryPanel, Loc.T("VICTOIRE !"));
        PopulateVictoryRecap(runTimer, killCount);
        FitSeparatorToText(_victoryRecapText);
        bool recordShown = PopulateVictoryRecordHighlight(runTimer, killCount, level, totalGold);
        // MODIFIÉ (2026-10-26) - le chip ne s'affiche qu'avec un vrai bonus d'Or (même condition que PlayGoldSequence) :
        // défi réussi sans bonus = phrase de repli, sinon le cadre d'infos n'aurait aucun message.
        bool chipShown = challengeCompleted && totalGold > baseGold;
        PopulateVictoryQuietLine(recordShown, chipShown);
        EndInfoBox victoryInfo = EnsureEndInfoBox(_victoryPanel, _victoryStatsText, _victoryChallengeText, _victoryRecordHighlightText, _victoryQuietLineText);
        FillEndInfoStats(victoryInfo, mins, secs, killCount, level, victoryBossKills);
        PrepareChallengeChip(_victoryChallengeText, chipShown, challengeRewardPercent);
        LayoutEndInfoBox(_victoryPanel, victoryInfo);
        PopulateVictoryPortrait();
        int victoryTileCount = PopulateBuildGrid(_victoryBuildGridContent);
        PopulateEmptyBuildState(_victoryEmptyBuildText, victoryTileCount, _victoryEmptyBuildLines);

        // AJOUTE - "Tentative n°X", meme logique que Game Over (voir
        // _gameOverAttemptText). Data.totalRuns a deja ete incremente par
        // SaveRunResults() avant l'appel a cette methode.
        if (_victoryAttemptText != null && MetaProgressionManager.Instance != null && MetaProgressionManager.Instance.Data != null)
        {
            ApplyAttemptLabel(_victoryAttemptText);
        }

        // MODIFIE - le bouton Rejouer reste verrouille jusqu'a la fin de la
        // sequence de reveal (voir PlayVictoryGoldSequenceThenReveal), meme
        // logique anti-skip que le Game Over.
        if (_victoryRetryButton != null) _victoryRetryButton.interactable = false;

        StartCoroutine(PlayVictoryGoldSequenceThenReveal(baseGold, totalGold, challengeCompleted, challengeRewardPercent));
        StartCoroutine(CountUpNumber(_victoryEclatsText, 0, eclatsEarned, _eclatsCountUpDuration));
    }

    // AJOUTE - equivalent de PlayGoldSequenceThenPreview mais pour la Victoire
    // (pas d'apercu de palier ici, volontairement) : deverrouille le bouton
    // Rejouer et signale a GameManager que le raccourci clavier peut s'activer,
    // uniquement une fois la sequence de comptage/reveal terminee.
    private IEnumerator PlayVictoryGoldSequenceThenReveal(int baseGold, int totalGold, bool challengeCompleted, float challengeRewardPercent)
    {
        yield return StartCoroutine(PlayGoldSequence(_victoryGoldText, _victoryChallengeText, baseGold, totalGold, challengeCompleted, challengeRewardPercent));

        if (_victoryRetryButton != null) _victoryRetryButton.interactable = true;
        OnEndScreenRevealComplete?.Invoke();
    }

    public void SetCrystalReady(int readyStacks)
    {
        if (_ultReadyEffect != null)
            _ultReadyEffect.SetActive(readyStacks > 0);

        if (_crystalIcons == null) return;

        Color targetColor = _emptyTierColor;
        if (readyStacks == 1) targetColor = _filledTierColor;
        else if (readyStacks >= 2) targetColor = _maxTierColor;

        foreach (var icon in _crystalIcons)
        {
            if (icon != null) icon.color = targetColor;
        }
    }

    // AJOUTÉ (2026-10-19, retour utilisateur : "quand on peut choisir une upgrade dans le level up panel, je veux
    // que tu affiche quand même la barre de vie qu'on a... parce que si il y a l'upgrade Soin qui tombe, le joueur
    // ne peut pas savoir à l'avance combien de vie il a") - keepHealthBar ne cache PAS tout _hudPanel (ce qui
    // cacherait aussi "Health", son enfant direct - voir HudStyler.ApplyBars) : il garde le panneau actif mais
    // désactive individuellement chaque enfant SAUF "Health". Utilisé uniquement par LevelUpManager ; les écrans de
    // fin de partie / le menu pause (autres appelants, visible=false sans ce paramètre) continuent de tout cacher.
    private GameObject _healthHudGroup;
    private bool _hudHealthOnlyMode;

    public void SetHUDVisible(bool visible, bool keepHealthBar = false)
    {
        if (_hudPanel == null) return;

        if (visible)
        {
            if (_hudHealthOnlyMode)
            {
                foreach (Transform child in _hudPanel.transform)
                    child.gameObject.SetActive(true);
                _hudHealthOnlyMode = false;
            }
            _hudPanel.SetActive(true);
            return;
        }

        if (!keepHealthBar)
        {
            _hudPanel.SetActive(false);
            return;
        }

        if (_healthHudGroup == null)
        {
            Transform t = _hudPanel.transform.Find("Health");
            _healthHudGroup = t != null ? t.gameObject : null;
        }

        _hudPanel.SetActive(true);
        _hudHealthOnlyMode = true;
        foreach (Transform child in _hudPanel.transform)
            child.gameObject.SetActive(child.gameObject == _healthHudGroup);
    }

    // AJOUTÉ (2026-10-18, diagnostic complet - audit §1.11 "Ultime sans aucun retour visuel à l'écran") - avant,
    // cette méthode ne faisait qu'un Debug.Log("ULT ACTIF") : rien ne distinguait à l'écran les 3 secondes où les
    // ennemis sont ralentis (CrystalSystem._slowDuration) du reste du jeu. Teinte plein écran bleu-cristal (couleur
    // de la branche Ultime/Cristal dans tout le reste du jeu), construite en code une seule fois (même principe que
    // EnsureChallengeIntroBuilt juste au-dessus : zéro asset/objet de scène à préparer à la main) : un flash net à
    // l'activation puis un palier doux qui respire légèrement tant que l'effet dure, et un fondu de sortie à la fin.
    private RectTransform _ultVfxRoot;
    private Image _ultVfxImage;
    private Coroutine _ultVfxRoutine;
    private static readonly Color UltVfxColor = new Color(0.55f, 0.85f, 1f);

    private void EnsureUltimateVFXBuilt()
    {
        if (_ultVfxRoot != null) return;
        if (_hudPanel == null) return;
        RectTransform hudRect = _hudPanel.GetComponent<RectTransform>();
        if (hudRect == null || !(hudRect.parent is RectTransform canvasRoot)) return;

        GameObject rootGO = new GameObject("UltimateVFX", typeof(RectTransform), typeof(Image));
        _ultVfxRoot = (RectTransform)rootGO.transform;
        _ultVfxRoot.SetParent(canvasRoot, false);
        _ultVfxRoot.anchorMin = Vector2.zero;
        _ultVfxRoot.anchorMax = Vector2.one;
        _ultVfxRoot.offsetMin = _ultVfxRoot.offsetMax = Vector2.zero;
        _ultVfxRoot.SetAsLastSibling();

        _ultVfxImage = rootGO.GetComponent<Image>();
        _ultVfxImage.color = new Color(UltVfxColor.r, UltVfxColor.g, UltVfxColor.b, 0f);
        _ultVfxImage.raycastTarget = false;
        rootGO.SetActive(false);
    }

    public void ShowUltEffect(bool show)
    {
        EnsureUltimateVFXBuilt();
        if (_ultVfxRoot == null) return;

        if (_ultVfxRoutine != null) StopCoroutine(_ultVfxRoutine);
        _ultVfxRoutine = StartCoroutine(show ? UltimateVFXInRoutine() : UltimateVFXOutRoutine());
    }

    // MODIFIÉ (2026-10-19, retour utilisateur : "l'effet dure trop longtemps, et quand j'ai level up en même temps
    // que cet effet de couleur bleu, l'effet de couleur est toujours là alors qu'il y a le panel") - 2 corrections :
    // (1) la respiration ne boucle plus indéfiniment jusqu'à ce que CrystalSystem appelle ShowUltEffect(false) à la
    // fin des 3s du ralenti (_slowDuration) - elle se limite désormais à une courte tenue puis s'éteint TOUTE SEULE
    // (~1,3s au total, flash+palier+tenue+fondu compris), bien avant la fin du ralenti ; (2) tout le minutage utilise
    // désormais Time.unscaledDeltaTime/unscaledTime au lieu de Time.deltaTime/Time.time - le LevelUpManager pose
    // Time.timeScale=0 pendant le choix de carte, ce qui gelait l'ancienne boucle (Time.deltaTime=0) et laissait la
    // teinte bleue visible à l'écran tant que le joueur n'avait pas choisi, PAR-DESSUS/DERRIÈRE le panel - en temps
    // non affecté par la pause, l'effet termine son propre cycle et s'éteint même si un level-up survient en même
    // temps, sans dépendre d'aucun couplage direct avec LevelUpManager.
    private IEnumerator UltimateVFXInRoutine()
    {
        _ultVfxRoot.gameObject.SetActive(true);
        // Flash net (0 -> 0.32) en 0.12s, puis retombe sur un palier doux (0.14) en 0.25s de plus - évite un
        // aplat plat pendant toute la durée du ralenti, sans jamais gêner la lisibilité des ennemis/projectiles.
        const float FlashPeak = 0.32f, RestingAlpha = 0.14f, HoldDuration = 0.6f;
        float t = 0f;
        while (t < 0.12f)
        {
            t += Time.unscaledDeltaTime;
            SetUltVfxAlpha(Mathf.Lerp(0f, FlashPeak, t / 0.12f));
            yield return null;
        }
        t = 0f;
        while (t < 0.25f)
        {
            t += Time.unscaledDeltaTime;
            SetUltVfxAlpha(Mathf.Lerp(FlashPeak, RestingAlpha, t / 0.25f));
            yield return null;
        }
        // Courte respiration (tenue fixe, plus une boucle infinie) puis extinction automatique.
        t = 0f;
        while (t < HoldDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = RestingAlpha + 0.03f * Mathf.Sin(Time.unscaledTime * 3f);
            SetUltVfxAlpha(k);
            yield return null;
        }
        yield return UltimateVFXOutRoutine();
        _ultVfxRoutine = null;
    }

    private IEnumerator UltimateVFXOutRoutine()
    {
        float start = _ultVfxImage != null ? _ultVfxImage.color.a : 0f;
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            SetUltVfxAlpha(Mathf.Lerp(start, 0f, t / 0.3f));
            yield return null;
        }
        SetUltVfxAlpha(0f);
        if (_ultVfxRoot != null) _ultVfxRoot.gameObject.SetActive(false);
        _ultVfxRoutine = null;
    }

    private void SetUltVfxAlpha(float a)
    {
        if (_ultVfxImage != null) _ultVfxImage.color = new Color(UltVfxColor.r, UltVfxColor.g, UltVfxColor.b, a);
    }

    // AJOUTÉ (2026-10-20, chantier satisfaction - comparaison aux autres survivors) - avant ce correctif, se faire
    // toucher ne produisait RIEN à l'écran à part le chiffre de dégâts et la barre de vie qui bouge : aucun retour
    // visuel immédiat sur le coup lui-même (contrairement aux ennemis, qui ont déjà un flash de teinte - voir
    // EnemyBase.TriggerHitFlash). Un flash rouge plein écran au moment de l'impact est un standard quasi universel
    // du genre (Vampire Survivors, Brotato, Halls of Torment le font tous) - MÊME construction que l'effet de
    // l'Ultime juste au-dessus (flash net + fondu de sortie, construit en code, un seul Image réutilisé), mais
    // l'intensité et la durée du fondu de sortie sont PROPORTIONNELLES à la gravité du coup (`severity01` = dégâts
    // / PV max) : un chip de dégâts reste discret, un coup qui retire une grosse part de la vie se voit nettement -
    // sans jamais aller jusqu'à gêner la lisibilité (plafond de _peakAlpha ci-dessous). Volontairement SANS secousse
    // de caméra (voir CameraShakeFX, header : réservée aux gros moments, jamais aux dégâts courants reçus en combat
    // normal - un choix déjà fait et documenté, pas remis en cause ici).
    private RectTransform _damageVfxRoot;
    private Image _damageVfxImage;
    private Coroutine _damageVfxRoutine;
    private static readonly Color DamageVfxColor = new Color(0.75f, 0.05f, 0.05f);

    private void EnsureDamageVFXBuilt()
    {
        if (_damageVfxRoot != null) return;
        if (_hudPanel == null) return;
        RectTransform hudRect = _hudPanel.GetComponent<RectTransform>();
        if (hudRect == null || !(hudRect.parent is RectTransform canvasRoot)) return;

        GameObject rootGO = new GameObject("DamageVFX", typeof(RectTransform), typeof(Image));
        _damageVfxRoot = (RectTransform)rootGO.transform;
        _damageVfxRoot.SetParent(canvasRoot, false);
        _damageVfxRoot.anchorMin = Vector2.zero;
        _damageVfxRoot.anchorMax = Vector2.one;
        _damageVfxRoot.offsetMin = _damageVfxRoot.offsetMax = Vector2.zero;
        _damageVfxRoot.SetAsLastSibling();

        _damageVfxImage = rootGO.GetComponent<Image>();
        _damageVfxImage.color = new Color(DamageVfxColor.r, DamageVfxColor.g, DamageVfxColor.b, 0f);
        _damageVfxImage.raycastTarget = false;
        rootGO.SetActive(false);
    }

    // severity01 : dégâts de ce coup / PV max du joueur (0 = chip minime, 1 = perte totale/quasi-totale en un coup).
    public void FlashDamageVignette(float severity01)
    {
        EnsureDamageVFXBuilt();
        if (_damageVfxRoot == null) return;

        severity01 = Mathf.Clamp01(severity01);
        if (_damageVfxRoutine != null) StopCoroutine(_damageVfxRoutine);
        _damageVfxRoutine = StartCoroutine(DamageVFXRoutine(severity01));
    }

    private IEnumerator DamageVFXRoutine(float severity01)
    {
        _damageVfxRoot.gameObject.SetActive(true);

        // Plafonné à 0,5 même pour le pire coup possible : un flash plus fort commencerait à gêner la lecture du
        // champ de bataille juste après un gros coup, l'instant où le joueur a justement le plus besoin de voir.
        float peakAlpha = Mathf.Lerp(0.1f, 0.5f, severity01);
        float outDuration = Mathf.Lerp(0.15f, 0.4f, severity01);
        const float InDuration = 0.05f;

        float t = 0f;
        while (t < InDuration)
        {
            t += Time.unscaledDeltaTime;
            SetDamageVfxAlpha(Mathf.Lerp(0f, peakAlpha, t / InDuration));
            yield return null;
        }
        t = 0f;
        while (t < outDuration)
        {
            t += Time.unscaledDeltaTime;
            SetDamageVfxAlpha(Mathf.Lerp(peakAlpha, 0f, t / outDuration));
            yield return null;
        }

        SetDamageVfxAlpha(0f);
        _damageVfxRoot.gameObject.SetActive(false);
        _damageVfxRoutine = null;
    }

    private void SetDamageVfxAlpha(float a)
    {
        if (_damageVfxImage != null) _damageVfxImage.color = new Color(DamageVfxColor.r, DamageVfxColor.g, DamageVfxColor.b, a);
    }

    // AJOUTÉ (2026-10-21, retour utilisateur : "il faut mettre un indicateur de puissance pour le nœud
    // Surpuissance de Aether, genre un effet rouge aura autour de l'écran") - avant ce correctif, le x2 de dégâts
    // de 5s après l'Ultime (nœud "Surpuissance") n'avait AUCUN retour visuel.
    // REFONTE (2026-10-22, retour utilisateur : "cette couleur est mélangée au bleu de l'ult, et en plus c'est du
    // rouge, donc ça met en confusion avec l'indicateur où le player se prend des dégâts [...] tu pourrais faire
    // quelque chose d'aussi précis graphiquement [que FireExplosionVFX] ? [...] l'entièreté de l'écran doit rester
    // visible, et qu'on doit reconnaître ce que c'est, sans que le joueur pourrait penser que c'est un bug") -
    // l'aplat rouge uniforme (0.85, 0.08, 0.08) entrait en collision avec 2 couleurs déjà prises : quasi identique
    // à DamageVfxColor (flash de dégâts, 0.75, 0.05, 0.05) et visuellement mélangée au bleu de UltVfxColor pendant
    // les 3 premières secondes (les 2 effets tournent en même temps à l'activation de l'Ultime). Remplacé par une
    // identité OR/CRISTAL (même or canonique utilisé partout ailleurs dans le jeu pour "palier max"/puissance -
    // UpgradeUI._dotColorMax, GameUI._maxTierColor - jamais pris par un autre effet plein écran), construite comme
    // FireExplosionVFX : des formes procédurales en couches (FXSprites.CrystalShard, pointes de cristal
    // irrégulières à facettes, pas un disque/aplat) plutôt qu'une simple teinte. 4 foyers d'éclats aux COINS de
    // l'écran (2 couches qui tournent chacune à sa propre vitesse/sens, pulsent en léger déphasage par coin) + un
    // halo ambré très discret en bordure (même Vignette transparente au centre qu'avant, mais peak alpha divisé
    // par ~2,7 - simple renfort d'ambiance, plus le porteur principal du message). Le CENTRE de l'écran reste
    // intégralement dégagé (demande explicite), et la forme aux coins + la couleur dorée (jamais utilisée pour les
    // dégâts ni l'Ultime) rendent l'effet reconnaissable comme un buff de puissance, pas un bug ni une alerte.
    private RectTransform _overpowerVfxRoot;
    private Image _overpowerGlowImage;
    private readonly Image[] _overpowerShardImages = new Image[8]; // 2 couches (intérieure/extérieure) x 4 coins
    private Coroutine _overpowerVfxRoutine;
    private float _overpowerShardAlphaScale;

    private static readonly Color OverpowerGlowColor = new Color(1f, 0.79f, 0.30f); // or canonique du jeu
    private static readonly Color OverpowerShardMidColor = new Color(1f, 0.79f, 0.30f);
    private static readonly Color OverpowerShardHotColor = new Color(1f, 0.92f, 0.62f);
    private static readonly Color OverpowerShardEdgeColor = new Color(0.68f, 0.40f, 0.07f);

    private const float OverpowerGlowPeakAlpha = 0.20f;
    private const float OverpowerShardPeakAlpha = 0.62f;
    private const float OverpowerShardBaseSize = 280f;

    private static readonly Vector2[] OverpowerCornerAnchors =
    {
        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f),
    };
    // Direction DEPUIS chaque coin VERS le centre de l'écran - décale chaque foyer d'éclats légèrement à
    // l'intérieur du coin plutôt que pile sur le bord coupé par l'écran.
    private static readonly Vector2[] OverpowerCornerInward =
    {
        new Vector2(1f, -1f), new Vector2(-1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f),
    };

    private void EnsureOverpowerVFXBuilt()
    {
        if (_overpowerVfxRoot != null) return;
        if (_hudPanel == null) return;
        RectTransform hudRect = _hudPanel.GetComponent<RectTransform>();
        if (hudRect == null || !(hudRect.parent is RectTransform canvasRoot)) return;

        GameObject rootGO = new GameObject("OverpowerVFX", typeof(RectTransform));
        _overpowerVfxRoot = (RectTransform)rootGO.transform;
        _overpowerVfxRoot.SetParent(canvasRoot, false);
        _overpowerVfxRoot.anchorMin = Vector2.zero;
        _overpowerVfxRoot.anchorMax = Vector2.one;
        _overpowerVfxRoot.offsetMin = _overpowerVfxRoot.offsetMax = Vector2.zero;
        _overpowerVfxRoot.SetAsLastSibling();

        GameObject glowGO = new GameObject("Glow", typeof(RectTransform), typeof(Image));
        RectTransform glowRect = (RectTransform)glowGO.transform;
        glowRect.SetParent(_overpowerVfxRoot, false);
        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.offsetMin = glowRect.offsetMax = Vector2.zero;
        _overpowerGlowImage = glowGO.GetComponent<Image>();
        _overpowerGlowImage.sprite = HudSprites.Vignette;
        _overpowerGlowImage.type = Image.Type.Simple;
        _overpowerGlowImage.color = FXSprites.A(OverpowerGlowColor, 0f);
        _overpowerGlowImage.raycastTarget = false;

        for (int corner = 0; corner < 4; corner++)
        {
            for (int layer = 0; layer < 2; layer++)
            {
                GameObject shardGO = new GameObject($"Shard_{corner}_{layer}", typeof(RectTransform), typeof(Image));
                RectTransform shardRect = (RectTransform)shardGO.transform;
                shardRect.SetParent(_overpowerVfxRoot, false);
                shardRect.anchorMin = shardRect.anchorMax = OverpowerCornerAnchors[corner];
                shardRect.pivot = new Vector2(0.5f, 0.5f);
                float size = OverpowerShardBaseSize * (layer == 0 ? 1f : 1.4f);
                shardRect.sizeDelta = new Vector2(size, size);
                shardRect.anchoredPosition = OverpowerCornerInward[corner] * (layer == 0 ? 90f : 70f);

                Image shardImage = shardGO.GetComponent<Image>();
                shardImage.sprite = FXSprites.CrystalShard();
                shardImage.type = Image.Type.Simple;
                shardImage.color = FXSprites.A(OverpowerShardMidColor, 0f);
                shardImage.raycastTarget = false;

                _overpowerShardImages[corner * 2 + layer] = shardImage;
            }
        }

        rootGO.SetActive(false);
    }

    public void SetOverpowerVisualActive(bool active)
    {
        EnsureOverpowerVFXBuilt();
        if (_overpowerVfxRoot == null) return;

        if (_overpowerVfxRoutine != null) StopCoroutine(_overpowerVfxRoutine);
        _overpowerVfxRoutine = StartCoroutine(active ? OverpowerVFXInRoutine() : OverpowerVFXOutRoutine());
    }

    private IEnumerator OverpowerVFXInRoutine()
    {
        _overpowerVfxRoot.gameObject.SetActive(true);
        const float InDuration = 0.18f;
        float t = 0f;
        while (t < InDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = t / InDuration;
            SetOverpowerGlowAlpha(Mathf.Lerp(0f, OverpowerGlowPeakAlpha, p));
            _overpowerShardAlphaScale = Mathf.Lerp(0f, 1f, p);
            AnimateOverpowerShards();
            yield return null;
        }
        // Tenue + rotation/pulsation continues (fin pilotée par SetOverpowerVisualActive(false), jamais par ce
        // minutage interne - voir commentaire plus haut).
        while (true)
        {
            SetOverpowerGlowAlpha(OverpowerGlowPeakAlpha + 0.04f * Mathf.Sin(Time.unscaledTime * 2.2f));
            _overpowerShardAlphaScale = 1f;
            AnimateOverpowerShards();
            yield return null;
        }
    }

    private IEnumerator OverpowerVFXOutRoutine()
    {
        const float OutDuration = 0.4f;
        float startGlow = _overpowerGlowImage != null ? _overpowerGlowImage.color.a : 0f;
        float startShardScale = _overpowerShardAlphaScale;
        float t = 0f;
        while (t < OutDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = t / OutDuration;
            SetOverpowerGlowAlpha(Mathf.Lerp(startGlow, 0f, p));
            _overpowerShardAlphaScale = Mathf.Lerp(startShardScale, 0f, p);
            AnimateOverpowerShards();
            yield return null;
        }
        SetOverpowerGlowAlpha(0f);
        _overpowerShardAlphaScale = 0f;
        AnimateOverpowerShards();
        _overpowerVfxRoot.gameObject.SetActive(false);
        _overpowerVfxRoutine = null;
    }

    private void SetOverpowerGlowAlpha(float a)
    {
        if (_overpowerGlowImage != null) _overpowerGlowImage.color = FXSprites.A(OverpowerGlowColor, a);
    }

    // Rotation lente continue (sens opposé entre couche intérieure/extérieure - effet de parallaxe), pulsation
    // d'alpha déphasée par coin (jamais synchronisée - lecture "énergie vivante", pas un clignotement uniforme) et
    // dégradé chaud/or/ambré entre couches (même principe que FireExplosionVFX.UpdateBurst).
    private void AnimateOverpowerShards()
    {
        float time = Time.unscaledTime;
        for (int corner = 0; corner < 4; corner++)
        {
            float phase = corner * 1.7f; // déphasage fixe par coin, pas aléatoire (forme stable, lecture cohérente)
            for (int layer = 0; layer < 2; layer++)
            {
                Image img = _overpowerShardImages[corner * 2 + layer];
                if (img == null) continue;

                float spinSpeed = layer == 0 ? 9f : -6f;
                img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, time * spinSpeed + corner * 37f);

                float pulse = 0.5f + 0.5f * Mathf.Sin(time * 2.6f + phase + layer * 0.8f);
                Color edgeTint = layer == 0 ? OverpowerShardHotColor : OverpowerShardEdgeColor;
                Color tinted = Color.Lerp(OverpowerShardMidColor, edgeTint, 0.5f + 0.5f * pulse);
                float alpha = Mathf.Lerp(OverpowerShardPeakAlpha * 0.5f, OverpowerShardPeakAlpha, pulse) * _overpowerShardAlphaScale;
                img.color = FXSprites.A(tinted, alpha);
            }
        }
    }

    public void UpdateDashCooldown(float percent)
    {
        if (_dashCooldownBar != null)
            _dashCooldownBar.value = Mathf.Clamp01(percent);
    }

    // L'icône du clone (Lyra) montre la touche que le joueur a réellement assignée : « C » par défaut, mais plus
    // jamais une lettre fausse après un changement dans Paramètres > Commandes (mise à jour immédiate, même en pause).
    private void OnBindingChanged(GameAction action)
    {
        if (action == GameAction.PhantomClone) RefreshCloneKey();
    }

    // AJOUTÉ (2026-10-25, audit de lisibilité) - plusieurs textes des écrans de fin étaient en LiberationSans (police type
    // Arial, étrangère au reste du jeu) et se lisaient mal : Or et Éclats gagnés en jaune / cyan sur les cases beige (1,5:1),
    // « Tentative n°X » à 60 % d'opacité. Ils passent en Bangers comme le reste de l'écran, avec un contour sombre (même
    // rendu que l'Or du HUD).
    private static readonly Color32 EndScreenOutlineColor = new Color32(0x2A, 0x1A, 0x08, 0xFF);

    private void StyleEndScreenTexts()
    {
        TextMeshProUGUI reference = _gameOverMessageText;              // titre du Game Over, déjà en Bangers
        if (reference == null) return;

        foreach (TextMeshProUGUI t in new[] { _gameOverGoldText, _gameOverEclatsText, _victoryGoldText, _victoryEclatsText })
            ToEndScreenFont(t, reference, 1.15f, 0.24f);

        foreach (TextMeshProUGUI t in new[] { _gameOverAttemptText, _victoryAttemptText })
        {
            if (!ToEndScreenFont(t, reference, 1.15f, 0.2f)) continue;
            Color c = t.color;
            t.color = new Color(c.r, c.g, c.b, 1f);
        }

        // Les lignes d'informations du bas (bandeau du défi, statistiques, prochain palier, phrase calme) restent dans leur
        // police d'origine : essayées en Bangers, refusées par l'utilisateur (moins belles).

        // Phrase sous « Défaite » (beige clair sur parchemin, 3:1) : déjà en Bangers, elle prend le contour sombre de
        // « Tentative n°X ». En dernier : elle sert de référence aux textes ci-dessus.
        Material outlinedMessage = HudBar.OutlinedMaterial(reference.fontSharedMaterial, 0.2f, EndScreenOutlineColor);
        if (outlinedMessage != null) reference.fontSharedMaterial = outlinedMessage;
    }

    // Passe un texte en Bangers (avec ou sans contour sombre) et agrandit sa police : Bangers est plus serrée que
    // LiberationSans à taille égale. Sans effet si le texte est déjà en Bangers.
    private static bool ToEndScreenFont(TextMeshProUGUI t, TextMeshProUGUI reference, float sizeScale, float outline)
    {
        if (t == null || t.font == reference.font) return false;
        t.font = reference.font;
        t.fontSharedMaterial = outline > 0f
            ? HudBar.OutlinedMaterial(reference.fontSharedMaterial, outline, EndScreenOutlineColor)
            : reference.fontSharedMaterial;
        t.fontStyle &= ~FontStyles.Italic;                            // Bangers est déjà penchée
        t.fontSize *= sizeScale;
        return true;
    }

    // « Éliminations » est plus long que « Kills » : les bandeaux de stats (Game Over / Victoire) réduisent leur police
    // au besoin plutôt que de déborder de leur cadre.
    private void FitStatStrips()
    {
        foreach (TextMeshProUGUI t in new[] { _statsText, _victoryStatsText })
        {
            if (t == null || t.enableAutoSizing) continue;
            float max = t.fontSize;
            t.enableAutoSizing = true;
            t.fontSizeMax = max;
            t.fontSizeMin = Mathf.Max(10f, max * 0.65f);
            // Une seule ligne : sans ça TMP coupe la ligne au lieu de réduire la police (italien, espagnol...).
            t.textWrappingMode = TextWrappingModes.NoWrap;
        }
    }

    // ---- Cadre d'infos des écrans de fin (Victoire / Défaite) ------------------------------------------------------------
    // AJOUTÉ (2026-10-26, retour utilisateur : "les informations qu'on voit en bas à gauche, c'est écrit petit, et c'est
    // espacé beaucoup en Y entre les 2 textes [...] Tu pourrait faire un petit cadre [...] il y a des messages qui ne sont
    // pas tout le temps ici [...] faire en sorte que ça ne soit jamais vide et tout petit") - la ligne de stats et les
    // messages variables (défi, aperçu du prochain déblocage, record, phrase de repli) quittent la pile verticale, qui
    // les étirait sur toute la hauteur restante, pour un cadre du même style que les cartes Or / Éclats : une rangée de
    // stats (gros chiffre + intitulé, une case par stat), un filet, puis les messages présents. La colonne de gauche suit
    // la grille de l'arsenal : titres alignés, cartes Or / Éclats calées sur le haut du cadre gris de l'arsenal, cadre
    // d'infos calé sur son bas. Le cadre garde au moins sa hauteur de base (jamais un petit bandeau) et grandit, au
    // détriment des cartes Or / Éclats, quand les messages prennent plus de place.
    // Essai écarté : la même ligne de stats en plus gros dans le cadre (beaucoup de vide, et trop juste en largeur pour
    // les langues longues).
    private sealed class EndInfoBox
    {
        public RectTransform root;
        public TextMeshProUGUI[] values = new TextMeshProUGUI[4];
        public TextMeshProUGUI[] labels = new TextMeshProUGUI[4];
        public GameObject[] dividers = new GameObject[3];
    }

    private readonly Dictionary<GameObject, EndInfoBox> _endInfoBoxes = new Dictionary<GameObject, EndInfoBox>();

    private const float EndInfoKeepHeight = 250f;      // cartes Or / Éclats quand les messages tiennent dans la hauteur de base
    private const float EndInfoKeepMinHeight = 200f;
    private const float EndInfoGap = 16f;              // même écart que entre les cartes Or et Éclats
    private const float EndInfoStatsHeight = 80f;
    private static readonly Color32 EndInfoLabelColor = new Color32(0x6B, 0x55, 0x3C, 0xFF);
    private static readonly Color32 EndInfoRuleColor = new Color32(0x33, 0x27, 0x1A, 0x40);

    // Construit le cadre une fois par écran et y range les textes variables (dans l'ordre d'affichage).
    private EndInfoBox EnsureEndInfoBox(GameObject panel, TextMeshProUGUI oldStatLine, params TextMeshProUGUI[] messages)
    {
        if (panel == null) return null;
        if (_endInfoBoxes.TryGetValue(panel, out EndInfoBox existing) && existing.root != null) return existing;

        RectTransform leftCol = panel.transform.Find("CenterCard/Body/LeftCol") as RectTransform;
        Transform goldRow = leftCol != null ? leftCol.Find("KeepSection/GoldRow") : null;
        TextMeshProUGUI goldText = goldRow != null ? goldRow.Find("GoldText")?.GetComponent<TextMeshProUGUI>() : null;
        if (leftCol == null || goldRow == null || goldText == null) return null;

        // Positions posées à la main par LayoutEndInfoBox (voir plus haut) : la pile verticale ne les recalcule plus.
        VerticalLayoutGroup column = leftCol.GetComponent<VerticalLayoutGroup>();
        if (column != null) column.enabled = false;

        EndInfoBox box = new EndInfoBox();
        GameObject rootGo = new GameObject("RunInfoBox", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        box.root = (RectTransform)rootGo.transform;
        box.root.SetParent(leftCol, false);
        box.root.anchorMin = box.root.anchorMax = box.root.pivot = new Vector2(0.5f, 0.5f);
        Image frame = rootGo.GetComponent<Image>();
        Image goldFrame = goldRow.GetComponent<Image>();
        frame.sprite = goldFrame.sprite;
        frame.type = goldFrame.type;
        frame.color = goldFrame.color;
        frame.raycastTarget = false;
        VerticalLayoutGroup stack = rootGo.GetComponent<VerticalLayoutGroup>();
        stack.childAlignment = TextAnchor.MiddleCenter;
        stack.childControlWidth = stack.childControlHeight = true;
        stack.childForceExpandWidth = true;
        stack.childForceExpandHeight = false;
        stack.padding = new RectOffset(24, 24, 14, 16);
        stack.spacing = 10f;

        // Rangée de stats : 4 cases de même largeur séparées par un filet vertical.
        GameObject row = new GameObject("Stats", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(box.root, false);
        row.GetComponent<LayoutElement>().preferredHeight = EndInfoStatsHeight;
        HorizontalLayoutGroup cells = row.GetComponent<HorizontalLayoutGroup>();
        cells.childControlWidth = cells.childControlHeight = true;
        cells.childForceExpandWidth = false;
        cells.childForceExpandHeight = true;
        for (int i = 0; i < 4; i++)
        {
            if (i > 0)
            {
                GameObject divider = NewInfoRule("Divider", row.transform);
                LayoutElement dividerLe = divider.AddComponent<LayoutElement>();
                dividerLe.minWidth = dividerLe.preferredWidth = 2f;
                box.dividers[i - 1] = divider;
            }
            GameObject cell = new GameObject("Cell", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            cell.transform.SetParent(row.transform, false);
            LayoutElement cellLe = cell.GetComponent<LayoutElement>();
            cellLe.minWidth = cellLe.preferredWidth = 0f;                  // cases de même largeur, quel que soit le texte
            cellLe.flexibleWidth = 1f;
            VerticalLayoutGroup cellStack = cell.GetComponent<VerticalLayoutGroup>();
            cellStack.childAlignment = TextAnchor.MiddleCenter;
            cellStack.childControlWidth = cellStack.childControlHeight = true;
            cellStack.childForceExpandHeight = false;
            cellStack.padding = new RectOffset(6, 6, 0, 0);
            cellStack.spacing = -2f;

            // MODIFIÉ (2026-10-26, retour utilisateur : "pourquoi le marron en couleur ? je pense que c'est mieux en blanc") -
            // chiffre blanc au contour sombre, même matériau que les montants Or / Éclats juste au-dessus.
            TextMeshProUGUI value = NewInfoText("Value", cell.transform, goldText.font, 44f, Color.white);
            value.fontSharedMaterial = goldText.fontSharedMaterial;
            value.enableAutoSizing = true;
            value.fontSizeMin = 28f;
            value.fontSizeMax = 44f;
            TextMeshProUGUI label = NewInfoText("Label", cell.transform, GameFonts.Body, 17f, EndInfoLabelColor);
            label.fontSharedMaterial = GameFonts.Body.material;
            label.fontStyle = FontStyles.UpperCase;
            label.characterSpacing = 3f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = 17f;
            box.values[i] = value;
            box.labels[i] = label;
        }

        GameObject rule = NewInfoRule("Rule", box.root);
        rule.AddComponent<LayoutElement>().preferredHeight = 2f;

        // Textes variables : rangés dans le cadre (le chip de défi y entre avec son fond doré).
        foreach (TextMeshProUGUI message in messages)
        {
            if (message == null) continue;
            Transform item = message.transform.parent != null && message.transform.parent.GetComponent<Image>() != null && message.transform.parent != leftCol
                ? message.transform.parent : message.transform;
            item.SetParent(box.root, false);
            LayoutElement le = item.GetComponent<LayoutElement>();
            if (item == message.transform)
            {
                // Phrase : sa vraie hauteur (1 ou 2 lignes), plus lisible qu'avant.
                if (le != null) le.preferredHeight = -1f;
                message.enableAutoSizing = false;
                message.fontSize = 24f;
                message.textWrappingMode = TextWrappingModes.Normal;
                message.alignment = TextAlignmentOptions.Center;
            }
            else if (le != null)
            {
                le.preferredHeight = 46f;   // chip de défi : hauteur de son fond
            }
        }

        if (oldStatLine != null) oldStatLine.gameObject.SetActive(false);   // remplacée par la rangée de stats

        _endInfoBoxes[panel] = box;
        return box;
    }

    private static GameObject NewInfoRule(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = EndInfoRuleColor;
        img.raycastTarget = false;
        go.AddComponent<PixelSnapRule>();   // même épaisseur à l'écran pour tous les filets
        return go;
    }

    private static TextMeshProUGUI NewInfoText(string name, Transform parent, TMP_FontAsset font, float size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    // Stats de la partie, une case chacune (mêmes règles par mode que BuildStatLine).
    private void FillEndInfoStats(EndInfoBox box, int mins, int secs, int killCount, int level, int bossKills)
    {
        if (box == null) return;
        // Temps : un petit espace de chaque côté des deux-points (retour utilisateur 2026-10-26 : "un peu trop collé vu la
        // taille des chiffres").
        List<(string value, string label)> stats = new List<(string, string)> { ($"{mins:00}<space=0.08em>:<space=0.08em>{secs:00}", Loc.T("Survie")) };
        if (GameModes.IsTitans)
        {
            stats.Add(($"{bossKills}/3", Loc.T("Titans")));
        }
        else
        {
            stats.Add((Loc.Int(killCount), Loc.T("Éliminations")));
            stats.Add((level.ToString(), Loc.T("Niveau")));
            string boss;
            if (GameModes.IsEndless) boss = bossKills.ToString();
            else if (GameModes.IsBossRush) boss = $"{bossKills}/{GameModes.RushBossCount(MetaProgressionManager.Instance != null ? MetaProgressionManager.Instance.Data : null)}";
            else boss = $"{bossKills}/3";
            stats.Add((boss, Loc.T("Boss")));
        }

        for (int i = 0; i < box.values.Length; i++)
        {
            bool used = i < stats.Count;
            box.values[i].transform.parent.gameObject.SetActive(used);
            if (i > 0) box.dividers[i - 1].SetActive(used);
            if (!used) continue;
            box.values[i].text = stats[i].value;
            box.labels[i].text = stats[i].label;
        }
    }

    // Place titre, cartes Or / Éclats et cadre d'infos sur la grille de la colonne de droite (titre, cadre de l'arsenal).
    // Appelée à chaque affichage, une fois les textes du cadre posés (leur hauteur décide de celle du cadre).
    private void LayoutEndInfoBox(GameObject panel, EndInfoBox box)
    {
        if (panel == null || box == null) return;
        RectTransform body = panel.transform.Find("CenterCard/Body") as RectTransform;
        RectTransform leftCol = body != null ? body.Find("LeftCol") as RectTransform : null;
        RectTransform title = leftCol != null ? leftCol.Find("KeepSectionTitle") as RectTransform : null;
        RectTransform keep = leftCol != null ? leftCol.Find("KeepSection") as RectTransform : null;
        RectTransform rightTitle = body != null ? body.Find("RightCol/BuildTitleText") as RectTransform : null;
        RectTransform arsenal = body != null ? body.Find("RightCol/ArsenalRow/BackgroundBuild") as RectTransform : null;
        if (title == null || keep == null || rightTitle == null || arsenal == null) return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(body);   // colonne de droite (pile verticale) en place avant de s'y caler

        Vector3[] corners = new Vector3[4];
        // Les deux titres alignés sur leur zone de texte (cadre moins marges haut/bas), pas sur leur cadre : celui de
        // droite a une marge basse sur la Victoire, qui remonte son texte.
        rightTitle.GetWorldCorners(corners);
        float titleY = leftCol.InverseTransformPoint((corners[0] + corners[2]) * 0.5f).y
            + TextAreaOffset(rightTitle.GetComponent<TextMeshProUGUI>()) - TextAreaOffset(title.GetComponent<TextMeshProUGUI>());
        arsenal.GetWorldCorners(corners);
        float top = leftCol.InverseTransformPoint(corners[1]).y;
        float bottom = leftCol.InverseTransformPoint(corners[0]).y;
        float width = leftCol.rect.width;

        box.root.sizeDelta = new Vector2(width, 0f);
        LayoutRebuilder.ForceRebuildLayoutImmediate(box.root);
        float available = top - bottom - EndInfoGap;
        float boxHeight = Mathf.Max(available - EndInfoKeepHeight, LayoutUtility.GetPreferredHeight(box.root));
        float keepHeight = Mathf.Max(EndInfoKeepMinHeight, available - boxHeight);

        Place(title, titleY, title.rect.height);
        Place(keep, top - keepHeight * 0.5f, keepHeight);
        Place(box.root, bottom + boxHeight * 0.5f, boxHeight);

        void Place(RectTransform rt, float centerY, float height)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(0f, centerY);
        }
    }

    // Décalage vertical du centre de la zone de texte par rapport au centre du cadre (marges basse - haute).
    private static float TextAreaOffset(TextMeshProUGUI text)
    {
        return text != null ? (text.margin.w - text.margin.y) * 0.5f : 0f;
    }

    // Chip "Défi réussi" : présent dès l'ouverture (transparent) pour que le cadre ait déjà sa hauteur finale, puis
    // révélé par PlayGoldSequence au moment du bonus.
    private static void PrepareChallengeChip(TextMeshProUGUI challengeText, bool show, float rewardPercent)
    {
        GameObject chipRoot = ChallengeChipRoot(challengeText);
        if (chipRoot == null) return;
        chipRoot.SetActive(show);
        if (!show) return;
        challengeText.text = Loc.F("Défi réussi ! {0}", ChallengeManager.FormatRewardMultiplier(rewardPercent));
        CanvasGroup cg = chipRoot.GetComponent<CanvasGroup>();
        if (cg == null) cg = chipRoot.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
    }

    private static GameObject ChallengeChipRoot(TextMeshProUGUI challengeText)
    {
        if (challengeText == null) return null;
        Image parentImage = challengeText.transform.parent != null ? challengeText.transform.parent.GetComponent<Image>() : null;
        return parentImage != null ? challengeText.transform.parent.gameObject : challengeText.gameObject;
    }

    private void RefreshCloneKey()
    {
        if (_cloneKeyText == null && _cloneCooldownContainer != null)
        {
            foreach (TextMeshProUGUI t in _cloneCooldownContainer.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (t.text.Trim().Length <= 4) { _cloneKeyText = t; break; }
        }
        if (_cloneKeyText == null) return;
        if (!_cloneKeyText.enableAutoSizing)                      // « Maj », « Ctrl »... doivent tenir dans la pastille
        {
            float max = _cloneKeyText.fontSize * _cloneKeyFontScale;      // la touche reste discrète dans la pastille
            _cloneKeyText.enableAutoSizing = true;
            _cloneKeyText.fontSizeMax = max;
            _cloneKeyText.fontSizeMin = Mathf.Max(8f, max * 0.4f);
            _cloneKeyText.textWrappingMode = TextWrappingModes.NoWrap;
        }
        _cloneKeyText.text = GameInput.ShortBindingLabel(GameAction.PhantomClone);
    }

    public void UpdateCloneCooldown(float percent)
    {
        if (_cloneCooldownFill != null)
            _cloneCooldownFill.fillAmount = Mathf.Clamp01(percent);
    }

    public void SetCloneAvailable(bool available)
    {
        if (_cloneCooldownContainer != null)
            _cloneCooldownContainer.SetActive(available);
    }

    // AJOUTE - Concentration (Aether). Texte qui affiche le bonus courant et
    // vire du cyan vers le doré à mesure qu'il approche du plafond.
    public void SetConcentrationAvailable(bool available)
    {
        if (_concentrationContainer != null)
            _concentrationContainer.SetActive(available);
    }

    // MODIFIE (2026-09-16) - retour utilisateur : "il y a écrit Concentration au
    // lieu de 0%... l'espace donné est fait pour des pourcentages, c'est pas
    // assez grand pour Concentration". Le texte affiche désormais toujours un
    // pourcentage, y compris "0%".
    public void UpdateConcentration(float bonus)
    {
        if (_concentrationText == null) return;

        // Une descente animée (PlayConcentrationHitDrop) est en cours : elle a la
        // main sur le texte/la couleur jusqu'à sa fin, ne pas l'interrompre par un
        // rafraîchissement normal qui écraserait l'animation par un saut.
        if (_concentrationDropCoroutine != null) return;

        SetConcentrationDisplay(bonus);
    }

    private void SetConcentrationDisplay(float bonus)
    {
        int pct = Mathf.RoundToInt(bonus * 100f);
        _concentrationText.text = $"{pct}%";

        // Réf. de teinte : plafond max du nœud (0.50). En dessous = interpolation
        // cyan -> doré, au plafond = doré plein.
        float t = Mathf.Clamp01(bonus / 0.5f);
        _concentrationText.color = Color.Lerp(_filledTierColor, _maxTierColor, t);
    }

    // AJOUTE (2026-09-16) - descente animée rapide du pourcentage affiché jusqu'à
    // 0% quand le joueur se fait toucher (voir PlayerBuffs.NotifyDamaged), plutôt
    // qu'un saut instantané ou le mot "Concentration" - la perte doit se
    // ressentir. La valeur de jeu réelle (dégâts) est déjà retombée à 0 avant
    // l'appel ; seule l'animation d'affichage est décalée dans le temps.
    private Coroutine _concentrationDropCoroutine;
    private const float ConcentrationDropDuration = 0.25f;

    public void PlayConcentrationHitDrop(float fromBonus)
    {
        if (_concentrationText == null) return;
        if (_concentrationDropCoroutine != null) StopCoroutine(_concentrationDropCoroutine);
        _concentrationDropCoroutine = StartCoroutine(ConcentrationDropRoutine(fromBonus));
    }

    private IEnumerator ConcentrationDropRoutine(float fromBonus)
    {
        float elapsed = 0f;
        while (elapsed < ConcentrationDropDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float bonus = Mathf.Lerp(fromBonus, 0f, elapsed / ConcentrationDropDuration);
            SetConcentrationDisplay(bonus);
            yield return null;
        }

        SetConcentrationDisplay(0f);
        _concentrationDropCoroutine = null;
    }

    // AJOUTE - Bouclier de Mana (Kael). SetManaShieldAvailable masque tout le
    // groupe pour les personnages qui n'ont pas le nœud ; UpdateManaShield
    // rafraîchit les pips (rempli / vide / cassé).
    public void SetManaShieldAvailable(bool available)
    {
        if (_manaShieldContainer != null)
            _manaShieldContainer.SetActive(available);
    }

    public void UpdateManaShield(int charges, int maxCharges, bool locked)
    {
        if (_manaShieldPips == null) return;

        for (int i = 0; i < _manaShieldPips.Length; i++)
        {
            if (_manaShieldPips[i] == null) continue;

            bool exists = i < maxCharges;
            _manaShieldPips[i].gameObject.SetActive(exists);
            if (!exists) continue;

            bool filled = i < charges;
            if (!filled)
                _manaShieldPips[i].color = _emptyTierColor;
            else
                _manaShieldPips[i].color = locked ? _manaShieldLockedColor : _manaShieldFilledColor;
        }
    }

    // AJOUTÉ (2026-10-14) - langue changée en pleine partie (Pause > Paramètres) : les compteurs du HUD ne se
    // réécrivent qu'à chaque variation, on les réécrit donc tout de suite avec leurs dernières valeurs.
    private int _hudGold, _hudKills, _hudLevel = 1;

    private void OnLanguageChanged()
    {
        UpdateGold(_hudGold);
        UpdateKillCount(_hudKills);
        if (_levelText != null) _levelText.text = Loc.F("Niv. {0}", _hudLevel);
        if (ChallengeManager.Instance != null && GameModes.IsClassic) ChallengeManager.Instance.RefreshDisplay();
        if (_bossNameText != null && _bossNameFr != null) _bossNameText.text = Loc.T(_bossNameFr);
    }

    private string _bossNameFr;

    public void UpdateGold(int amount)
    {
        _hudGold = amount;
        if (_goldText != null)
            _goldText.text = Loc.F("Or : {0}", Loc.Int(amount));
    }

    public void UpdateKillCount(int kills)
    {
        _hudKills = kills;
        if (_killCountText != null)
            _killCountText.text = Loc.F("Éliminations : {0}", kills);
    }

    public void UpdateXPBar(float currentXP, float xpToNextLevel, int level)
    {
        if (_xpBar != null)
        {
            _xpBarTargetValue = (xpToNextLevel > 0) ? currentXP / xpToNextLevel : 0f;
            _xpBarPunchTimer = 0f; // relance le petit "punch" a chaque gain - voir LateUpdate
        }

        _hudLevel = level;
        if (_levelText != null)
            _levelText.text = Loc.F("Niv. {0}", level);
    }

    // AJOUTÉ (2026-10-04) - voir le commentaire sur _xpBarTargetValue. Lissage exponentiel (jamais de
    // Time.deltaTime brut multiplié - indépendant du framerate) + petit punch d'échelle synchronisé.
    private void UpdateXPBarSmoothing()
    {
        if (_xpBar == null) return;

        _xpBarDisplayedValue = Mathf.Lerp(_xpBarDisplayedValue, _xpBarTargetValue, 1f - Mathf.Exp(-XPBarFillLerpRate * Time.deltaTime));
        if (Mathf.Abs(_xpBarDisplayedValue - _xpBarTargetValue) < 0.0005f) _xpBarDisplayedValue = _xpBarTargetValue;
        _xpBar.value = _xpBarDisplayedValue;

        if (_xpBarPunchTimer >= 0f)
        {
            _xpBarPunchTimer += Time.deltaTime;
            float p = Mathf.Clamp01(_xpBarPunchTimer / XPBarPunchDuration);
            float punch = 1f + 0.1f * (1f - p) * Mathf.Sin(p * Mathf.PI); // part a 1, bombe, revient a 1
            _xpBar.transform.localScale = new Vector3(1f, punch, 1f);
            if (_xpBarPunchTimer >= XPBarPunchDuration)
            {
                _xpBarPunchTimer = -1f;
                _xpBar.transform.localScale = Vector3.one;
            }
        }
    }

    public void UpdateHPBar(float currentHP, float maxHP)
    {
        if (_hpBar == null) return;

        float percent = (maxHP > 0) ? currentHP / maxHP : 0f;
        _hpBar.value = percent;

        if (_hpText != null)
        {
            _hpText.text = $"{Mathf.CeilToInt(Mathf.Max(0, currentHP))} / {Mathf.CeilToInt(maxHP)}";
        }

        if (_hpFillImage == null) return;

        if (percent > 0.6f)
            _hpFillImage.color = new Color(0f, 0.7f, 0f);
        else if (percent > 0.4f)
            _hpFillImage.color = new Color(0.4f, 0.8f, 0f);
        else if (percent > 0.2f)
            _hpFillImage.color = new Color(1f, 0.5f, 0f);
        else
            _hpFillImage.color = new Color(0.85f, 0.1f, 0.1f);
    }

    // MODIFIÉ (2026-10-24, session FPS) - appelé CHAQUE frame par WaveManager : reformatait une nouvelle chaîne
    // "mm:ss" à chaque frame (allocation mémoire + reconstruction du texte TMP) alors que l'affichage ne change
    // qu'une fois par seconde. Ne réécrit plus le texte que quand la seconde affichée change.
    private int _lastTimerSecond = -1;

    public void UpdateTimer(float seconds)
    {
        if (_timerText == null) return;
        int total = Mathf.FloorToInt(seconds);
        if (total == _lastTimerSecond) return;
        _lastTimerSecond = total;
        int mins = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        _timerText.text = $"{mins:00}:{secs:00}";
    }

    public void ShowBossHP(string bossName)
    {
        if (_bossHPBar != null) _bossHPBar.SetActive(true);
        if (_bossNameText != null)
        {
            _bossNameText.gameObject.SetActive(true);
            _bossNameFr = bossName;
            _bossNameText.text = Loc.T(bossName);
        }
        if (_bossHPSlider != null) _bossHPSlider.value = 1f;
        if (_bossIcon != null) _bossIcon.SetActive(true);
        if (_bossHPBar != null) { HudBar hb = _bossHPBar.GetComponent<HudBar>(); if (hb != null) hb.PlayIntro(); }   // la barre se remplit à l'apparition

        PlayBossRevealMask();
    }

    // CORRIGÉ (2026-10-20, retour utilisateur : "tu as mis tout en haut de l'écran, je ne t'ai jamais dis de
    // changer la disposition, remet à droite et de haut en bas") - 1re version : un `BossRevealMask` posé EN DUR
    // DANS LA SCÈNE, parent intercalé entre BossGroup et BossNameText/BossIcon/BossHPBar. Cause du bug : HudStyler
    // retrouve et positionne ces 3 éléments par un CHEMIN EXACT (`Find<Slider>("BossGroup/BossHPBar")`, etc. - voir
    // HudStyler.ApplyBoss/ApplyMiniBoss) qui suppose qu'ils sont des enfants DIRECTS de BossGroup ; les avoir
    // intercalés sous un parent intermédiaire a cassé ce chemin, donc HudStyler ne les a plus jamais stylés
    // (barre verticale sur le bord droit, icône en haut à droite) - ils sont restés à leurs valeurs d'ÉDITION
    // (mortes, voir la mémoire du projet "hud-bars-repositioned-at-runtime-by-hudstyler"), qui se trouvaient en
    // plus être des valeurs PÉRIMÉES d'une session antérieure, d'où l'atterrissage en haut de l'écran.
    //
    // Corrigé en construisant le masque ENTIÈREMENT EN CODE, à la toute première apparition d'un boss - donc
    // BIEN APRÈS que HudStyler ait déjà positionné BossIcon/BossHPBar à leur vraie place (HudStyler tourne une
    // fois au lancement de la scène, dans Start() ; un boss n'apparaît que des dizaines de secondes plus tard).
    // `RectTransformUtility.CalculateRelativeRectTransformBounds` lit les bornes RÉELLES de ces 2 éléments tels
    // que HudStyler les a posés - zéro valeur recopiée à la main, donc zéro risque de désynchronisation si
    // HudStyler change un jour ses réglages. `SetParent(mask, true)` (worldPositionStays=true) laisse Unity
    // recalculer lui-même le nouvel ancrage pour une position ÉCRAN inchangée - aucune compensation manuelle.
    // BossNameText (en haut-CENTRE de l'écran, donc spatialement séparé de l'icône/la barre à droite) n'est PAS
    // inclus dans ce masque : un rectangle couvrant les 2 zones à la fois engloberait une bonne partie du HUD du
    // haut (Or, Éliminations, chip de défi) et risquerait de les faire disparaître pendant la révélation.
    private RectTransform _bossRevealMask;
    private float _bossRevealFullHeight;
    private bool _bossRevealMaskBuildAttempted;
    private Coroutine _bossRevealRoutine;
    private const float BossRevealDuration = 0.9f;   // "2s max" demandé - volontairement plus court, net sans traîner
    private const float BossRevealStartFraction = 0.14f; // hauteur déjà visible au tout 1er instant ("on voit que le haut")

    private void EnsureBossRevealMaskBuilt()
    {
        if (_bossRevealMaskBuildAttempted) return;
        if (_bossIcon == null || _bossHPBar == null) return;

        RectTransform iconRt = _bossIcon.GetComponent<RectTransform>();
        RectTransform barRt = _bossHPBar.GetComponent<RectTransform>();
        if (iconRt == null || barRt == null) return;
        RectTransform parent = iconRt.parent as RectTransform;
        if (parent == null || barRt.parent != parent) return; // les 2 doivent partager le même parent direct (BossGroup)

        _bossRevealMaskBuildAttempted = true;

        Bounds union = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, iconRt);
        union.Encapsulate(RectTransformUtility.CalculateRelativeRectTransformBounds(parent, barRt));

        const float Margin = 6f;
        float left = union.min.x - Margin, right = union.max.x + Margin;
        float bottom = union.min.y - Margin, top = union.max.y + Margin;
        float width = right - left;
        float height = top - bottom;
        if (width <= 0f || height <= 0f) return;

        GameObject maskGO = new GameObject("BossRevealMask", typeof(RectTransform));
        RectTransform maskRt = (RectTransform)maskGO.transform;
        maskRt.SetParent(parent, false);
        maskRt.anchorMin = maskRt.anchorMax = new Vector2(0.5f, 0.5f); // même pivot que BossGroup (son centre = centre écran)
        maskRt.pivot = new Vector2(0.5f, 1f);                         // piloté depuis le HAUT : le bas bouge, le haut jamais
        maskRt.sizeDelta = new Vector2(width, height);
        maskRt.anchoredPosition = new Vector2((left + right) * 0.5f, top);
        maskGO.AddComponent<RectMask2D>();

        // worldPositionStays=true : Unity recalcule lui-même l'ancrage de chaque enfant pour que sa position à
        // L'ÉCRAN ne bouge pas d'un pixel - aucune compensation manuelle, contrairement à la 1re version de ce correctif.
        iconRt.SetParent(maskRt, true);
        barRt.SetParent(maskRt, true);

        _bossRevealMask = maskRt;
        _bossRevealFullHeight = height;
    }

    private void PlayBossRevealMask()
    {
        EnsureBossRevealMaskBuilt();
        if (_bossRevealMask == null) return;
        if (_bossRevealRoutine != null) StopCoroutine(_bossRevealRoutine);
        _bossRevealRoutine = StartCoroutine(BossRevealMaskRoutine());
    }

    // "Rideau qui se lève" : la hauteur du masque grandit depuis un mince filet jusqu'à sa hauteur complète - le
    // pivot HAUT (voir EnsureBossRevealMaskBuilt) fait que le bord du HAUT ne bouge jamais, seul le bord du BAS
    // révélé descend. EaseOutCubic (déjà utilisé ailleurs dans ce fichier pour les mêmes raisons) : net au départ,
    // se pose en douceur à l'arrivée.
    private System.Collections.IEnumerator BossRevealMaskRoutine()
    {
        SetBossRevealFraction(BossRevealStartFraction);

        float t = 0f;
        while (t < BossRevealDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / BossRevealDuration);
            float eased = 1f - Mathf.Pow(1f - p, 3f);
            SetBossRevealFraction(Mathf.Lerp(BossRevealStartFraction, 1f, eased));
            yield return null;
        }

        SetBossRevealFraction(1f);
        _bossRevealRoutine = null;
    }

    private void SetBossRevealFraction(float visibleFraction)
    {
        if (_bossRevealMask == null) return;
        visibleFraction = Mathf.Clamp01(visibleFraction);
        Vector2 size = _bossRevealMask.sizeDelta;
        size.y = _bossRevealFullHeight * visibleFraction;
        _bossRevealMask.sizeDelta = size;
    }

    public void UpdateBossHP(float current, float max)
    {
        // Choc des titans : une seule barre pour les 3 boss = somme de leurs PV.
        if (GameModes.IsTitans && WaveManager.Instance != null)
            WaveManager.Instance.GetTitansHealth(out current, out max);

        if (_bossHPSlider != null)
        {
            _bossHPSlider.value = (max > 0) ? current / max : 0f;
        }
    }

    public void HideBossHP()
    {
        // Choc des titans : la barre reste tant qu'il reste au moins 2 boss (celui qui meurt est encore compté ici).
        if (GameModes.IsTitans && WaveManager.Instance != null && WaveManager.Instance.TitansRemaining > 1) return;
        if (_bossHPBar != null) _bossHPBar.SetActive(false);
        if (_bossNameText != null) _bossNameText.gameObject.SetActive(false);
        if (_bossIcon != null) _bossIcon.SetActive(false);
    }

    // AJOUTE (2026-10-07, retour utilisateur : barre de vie dédiée aux mini-boss du Boss 3) - même trio
    // Show/Update/Hide que la barre du boss principal, en parallèle.
    public void ShowMiniBossHP()
    {
        if (_miniBossHPBar != null) _miniBossHPBar.SetActive(true);
        if (_miniBossHPSlider != null) _miniBossHPSlider.value = 1f;
    }

    public void UpdateMiniBossHP(float current, float max)
    {
        if (_miniBossHPSlider != null)
            _miniBossHPSlider.value = (max > 0) ? current / max : 0f;
    }

    public void HideMiniBossHP()
    {
        if (_miniBossHPBar != null) _miniBossHPBar.SetActive(false);
    }

    // MODIFIE - ajout de "deathCause" (transmis par HealthSystem -> GameManager)
    // pour le message d'ambiance contextuel.
    // MODIFIE - ajout de "bossKillCount" (deja suivi par GameManager, jamais
    // transmis jusqu'ici) pour que le StatStrip affiche la vraie progression
    // de la run ("Boss 1/3", "Boss 2/3"...) au lieu d'un "Boss 3/3" fige qui
    // n'aurait jamais eu de sens ici (contrairement a la Victoire, qui ne se
    // declenche QUE quand les 3 boss sont vaincus).
    public void ShowGameOver(float runTimer, int killCount, int baseGold, int totalGold, int level, int eclatsEarned, bool challengeCompleted, float challengeRewardPercent, string deathCause, int bossKillCount)
    {
        if (_gameOverPanel != null) _gameOverPanel.SetActive(true);

        int mins = Mathf.FloorToInt(runTimer / 60f);
        int secs = Mathf.FloorToInt(runTimer % 60f);

        // MODIFIE - refonte 3-temps, meme format compact que la Victoire (une
        // seule ligne) plutot que le pave de 3 lignes d'avant.
        if (_statsText != null)
            _statsText.text = BuildStatLine(mins, secs, killCount, level, bossKillCount);

        // Sans fin : on ne "perd" pas, la partie se termine. Classique et Ruée gardent DÉFAITE.
        SetEndScreenTitle(_gameOverPanel, GameModes.IsEndless ? Loc.T("FIN DE PARTIE") : Loc.T("DÉFAITE"));

        PopulateGameOverPortrait();
        int gameOverTileCount = PopulateBuildGrid(_gameOverBuildGridContent);
        PopulateEmptyBuildState(_gameOverEmptyBuildText, gameOverTileCount, _gameOverEmptyBuildLines);
        // AJOUTE - le cadre "BackgroundBuild" (ajoute a la main par
        // l'utilisateur) n'a de sens que derriere des tuiles reelles ; a vide,
        // seul le petit cadre EmptyBuildBg (gere ci-dessus) doit rester.
        if (_gameOverArsenalFrame != null)
            _gameOverArsenalFrame.SetActive(gameOverTileCount > 0);

        // MODIFIE - passe desormais le niveau atteint et l'Or total (bonus de
        // defi inclus) pour couvrir les 4 records au lieu de 2.
        PopulateGameOverMessage(runTimer, killCount, level, totalGold, deathCause, bossKillCount);
        FitSeparatorToText(_gameOverMessageText);

        // AJOUTÉ (2026-10-26) - cadre d'infos : stats, chip de défi, aperçu du prochain déblocage (voir EnsureEndInfoBox).
        EndInfoBox gameOverInfo = EnsureEndInfoBox(_gameOverPanel, _statsText, _gameOverChallengeText, _gameOverNextUnlockText);
        FillEndInfoStats(gameOverInfo, mins, secs, killCount, level, bossKillCount);
        PrepareChallengeChip(_gameOverChallengeText, challengeCompleted && totalGold > baseGold, challengeRewardPercent);
        PrepareNextUnlockText();
        LayoutEndInfoBox(_gameOverPanel, gameOverInfo);

        // AJOUTE - "Tentative n°X", Data.totalRuns a deja ete incremente par
        // SaveRunResults() avant l'appel a cette methode.
        if (_gameOverAttemptText != null && MetaProgressionManager.Instance != null && MetaProgressionManager.Instance.Data != null)
        {
            ApplyAttemptLabel(_gameOverAttemptText);
        }

        // MODIFIE - le bouton Rejouer reste verrouille jusqu'a la fin complete
        // de la sequence de reveal (voir PlayGoldSequenceThenPreview).
        if (_gameOverRetryButton != null) _gameOverRetryButton.interactable = false;

        StartCoroutine(PlayGoldSequenceThenPreview(baseGold, totalGold, challengeCompleted, challengeRewardPercent));
        StartCoroutine(CountUpNumber(_gameOverEclatsText, 0, eclatsEarned, _eclatsCountUpDuration));
    }

    public void ShowPausePanel(bool show)
    {
        if (_pausePanel != null)
        {
            _pausePanel.SetActive(show);
            // AJOUTÉ (2026-10-20, retour utilisateur : "même réflexion pour le pause panel, le [bleu de l'Ultime]
            // passe dessus, je veux que ce soit en dessous") - même correctif que LevelUpManager.DisplayLevelUp()
            // (voir son commentaire) : remonte le panel en DERNIER enfant du Canvas à chaque ouverture, pour qu'il
            // reste toujours au-dessus de tout effet plein écran déjà présent (dont GameUI._ultVfxRoot, qui se
            // place lui-même en dernier enfant à sa création) - le bleu, s'il traîne encore, reste caché derrière.
            if (show) _pausePanel.transform.SetAsLastSibling();
        }
        SettingsApplier.SetPauseDuck(show);          // le son du jeu baisse légèrement pendant la pause

        if (show)
        {
            float runTime = (GameManager.Instance != null) ? GameManager.Instance.RunTimer : 0f;
            int kills = (GameManager.Instance != null) ? GameManager.Instance.KillCount : 0;
            int gold = (MetaProgressionManager.Instance != null) ? MetaProgressionManager.Instance.RunGold : 0;

            int mins = Mathf.FloorToInt(runTime / 60f);
            int secs = Mathf.FloorToInt(runTime % 60f);

            if (_pauseStatsText != null)
            {
                // CORRIGÉ (2026-10-18, diagnostic complet - audit §9 "la pause affiche Gold : dans une clé
                // française, un anglicisme") - "Gold" -> "Or", et Loc.Int pour le séparateur de milliers.
                _pauseStatsText.text = Loc.F("Temps : {0:00}:{1:00}\nEnnemis tués : {2}\nOr : {3}", mins, secs, kills, Loc.Int(gold));
            }

            if (_pauseUpgradesText != null && LevelUpManager.Instance != null)
            {
                _pauseUpgradesText.text = LevelUpManager.Instance.GetUpgradesSummary();
            }

            if (_abandonConfirmPanel != null)
                _abandonConfirmPanel.SetActive(false);
        }
    }

    public void ShowAbandonConfirm(bool show)
    {
        if (_abandonConfirmPanel != null)
            _abandonConfirmPanel.SetActive(show);
    }
}