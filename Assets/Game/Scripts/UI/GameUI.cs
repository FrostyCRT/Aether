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

    [Header("Boss")]
    [SerializeField] private GameObject _bossHPBar;
    [SerializeField] private Slider _bossHPSlider;
    [SerializeField] private TextMeshProUGUI _bossNameText;
    [SerializeField] private GameObject _bossIcon;

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
    }

    private void OnDestroy()
    {
        GameSettings.Changed -= OnUserSettingChanged;
        SettingsPage.InGameClosed -= OnSettingsClosed;
        InputBindings.Changed -= OnBindingChanged;
    }

    private void Start()
    {
        if (MetaProgressionManager.Instance != null)
            UpdateGold(MetaProgressionManager.Instance.RunGold);

        UpdateKillCount(0);
        ApplyUserSettings();
        RefreshCloneKey();
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
            _challengeText.text = $"<s>{challengeName}</s> — Échoué";
            _challengeText.color = new Color(0.55f, 0.55f, 0.55f);
        }
        else
        {
            _challengeText.text = $"{challengeName} — {progressText}";
            _challengeText.color = Color.white;
        }
    }

    // AJOUTE (2026-09-15) - masque le rappel de defi du HUD (voir
    // ChallengeManager.RefreshDisplay/IsRewardAlreadyClaimedThisHour).
    public void HideChallengeDisplay()
    {
        if (_challengeGroup != null) _challengeGroup.SetActive(false);
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

        if (duration <= 0f || from == to)
        {
            text.text = to.ToString();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            int value = Mathf.RoundToInt(Mathf.Lerp(from, to, eased));
            text.text = value.ToString();
            yield return null;
        }

        text.text = to.ToString();
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
        GameObject chipRoot = null;
        if (challengeText != null)
        {
            Image parentImage = challengeText.transform.parent != null ? challengeText.transform.parent.GetComponent<Image>() : null;
            chipRoot = parentImage != null ? challengeText.transform.parent.gameObject : challengeText.gameObject;
        }

        if (chipRoot != null)
            chipRoot.SetActive(false);

        yield return StartCoroutine(CountUpNumber(goldText, 0, baseGold, _goldCountUpDuration));

        if (challengeCompleted && totalGold > baseGold)
        {
            yield return new WaitForSecondsRealtime(_challengeRevealDelay);

            if (chipRoot != null)
            {
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
                challengeText.text = $"Défi réussi ! {ChallengeManager.FormatRewardMultiplier(rewardPercent)}";
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
                    new RecordCheck { current = Mathf.Floor(runTime), best = Mathf.Floor(data.bestTime), newRecordMessage = $"Nouveau record de survie : {FormatTime(runTime)} !", nearRecordMessage = "À deux doigts de ton record de survie..." },
                    new RecordCheck { current = killCount, best = data.bestKills, newRecordMessage = $"Nouveau record d'éliminations : {killCount} !", nearRecordMessage = "Si proche de ton record d'éliminations..." },
                    new RecordCheck { current = levelReached, best = data.bestLevel, newRecordMessage = $"Nouveau record de niveau : {levelReached} !", nearRecordMessage = "Un cheveu de ton meilleur niveau..." },
                    new RecordCheck { current = goldThisRun, best = data.bestGoldInRun, newRecordMessage = $"Nouveau record d'Or en une partie : {goldThisRun} !", nearRecordMessage = "Tout près de ton record d'Or..." },
                };

                foreach (RecordCheck check in checks)
                {
                    if (check.best > 0f && check.current >= check.best)
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
        return PopulateBuildGrid(gridContent, _buildGridSlotPrefab, _spawnedBuildGridSlots, _arsenalCellSize, _arsenalSpacing, false, null, null, null, null);
    }

    // Version générique (cellule/espacement/style paramétrables) : réutilisée telle quelle par PauseMenuUI.PopulateGrid()
    // pour la même grille en plus grand, avec son propre look (parchemin), sans dupliquer la logique de
    // disposition/fusion. parchmentSober, s'il est fourni (non-null), active le style "parchemin" du menu Pause
    // (même sprite sobre pour toutes les cartes) ; laissé à null pour garder le style "tuile teintée" par défaut
    // de cet écran (Victoire/Défaite).
    public int PopulateBuildGrid(Transform gridContent, GameObject slotPrefab, List<GameObject> spawnedSlots, Vector2 cellSize, Vector2 spacing, bool showNames,
        Sprite parchmentSober, Color? dotEmpty, Color? dotFilled, Color? dotMax, System.Action<UpgradeData> onSlotClicked = null)
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
            PositionArsenalSlot(slotGO, row, col, colSpan, cellSize, spacing, originX);
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
    private static void PositionArsenalSlot(GameObject slotGO, int row, int col, int colSpan, Vector2 cellSize, Vector2 spacing, float originX)
    {
        RectTransform rt = (RectTransform)slotGO.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(originX + col * (cellSize.x + spacing.x), -(row * (cellSize.y + spacing.y)));
        rt.sizeDelta = new Vector2(cellSize.x * colSpan + spacing.x * (colSpan - 1), cellSize.y);
    }

    private static readonly Vector2 _arsenalCellSize = new Vector2(125f, 125f);
    private static readonly Vector2 _arsenalSpacing = new Vector2(20f, 20f);

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
        int count = 0, col = 0, row = 0;
        foreach (GridItem item in items)
        {
            if (col + item.colSpan > columns) { col = 0; row++; }

            GameObject slotGO = Instantiate(slotPrefab, gridContent);
            PositionArsenalSlot(slotGO, row, col, item.colSpan, cellSize, spacing, originX);
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
            emptyText.text = pool[UnityEngine.Random.Range(0, pool.Length)];
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
                iconRt.anchoredPosition = parchmentSober != null ? new Vector2(-47f, -18f) : new Vector2(iconRt.anchoredPosition.x, 26f);
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

        // Une tuile de fusion n'affiche jamais de pastilles de palier, sur AUCUN écran (une fusion n'a qu'un
        // seul palier - les pastilles n'y ont jamais rien apporté) : basé sur isFusion, pas showFusionVisual, pour
        // rester vrai sur Victoire/Défaite même sans le visuel dédié (icône simple + fond violet) ci-dessus.
        if (refs.tierDotsRow != null)
            refs.tierDotsRow.gameObject.SetActive(!isFusion);

        if (refs.nameText != null)
        {
            refs.nameText.gameObject.SetActive(showNames);
            if (showNames)
            {
                refs.nameText.text = upgrade.upgradeName;
                refs.nameText.color = owned ? Color.white : new Color(0.75f, 0.75f, 0.75f, 0.8f);
                ApplyReadableOutline(refs.nameText);
            }
        }
        if (refs.stackCountText != null)
            refs.stackCountText.gameObject.SetActive(false);

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

    private UpgradeData FindUpgradeByType(UpgradeType type)
    {
        if (LevelUpManager.Instance == null || LevelUpManager.Instance.AllUpgrades == null) return null;
        foreach (UpgradeData u in LevelUpManager.Instance.AllUpgrades)
            if (u != null && u.upgradeType == type) return u;
        return null;
    }

    // AJOUTE (2026-09-27) - retour utilisateur : "le texte blanc sur ce genre de fond pour les cartes est pas très
    // visible". Contour sombre via les propriétés natives de TMP (matériau SDF, aucun assombrissement du fond
    // requis) - garantit la lisibilité du texte blanc quel que soit l'endroit du parchemin où il tombe.
    public static void ApplyReadableOutline(TextMeshProUGUI text)
    {
        if (text == null) return;
        text.fontMaterial.EnableKeyword("OUTLINE_ON");
        text.outlineWidth = 0.22f;
        text.outlineColor = new Color32(30, 18, 10, 255);
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
        "Tu as tout donné à cette branche. Littéralement.",
        "Plus rien à débloquer ici — change de personnage pour la suite.",
        "Cette branche n'a plus de secret pour toi.",
        "Arbre et Réputation à fond. Change de perso si tu veux du neuf.",
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
    private IEnumerator PlayNextUnlockPreview()
    {
        if (_gameOverNextUnlockText == null) yield break;

        string unlockedCharacter = MetaProgressionManager.Instance != null
            ? MetaProgressionManager.Instance.ConsumePendingUnlockNotification()
            : null;

        string text;
        bool highlight = false;

        if (unlockedCharacter != null)
        {
            text = $"Nouveau personnage débloqué : {unlockedCharacter} !";
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
                text = _gameOverMaxedOutLines[UnityEngine.Random.Range(0, _gameOverMaxedOutLines.Length)];
            }
            else
            {
                string currencyLabel = preview.IsGoldCurrency ? "Or" : "Éclats";
                text = preview.AmountStillNeeded > 0
                    ? $"Encore {preview.AmountStillNeeded} {currencyLabel} pour débloquer {preview.NodeName}"
                    : $"De quoi débloquer {preview.NodeName} dès maintenant !";
            }
        }

        // MODIFIE - #33271A (encre foncée, même teinte que StatStrip - déjà
        // confirmée lisible sur ce fond) plutôt que l'ink-soft #5A4834 d'origine :
        // retour utilisateur avec capture à l'appui, ce texte était presque
        // illisible sur la carte assombrie du Game Over. Le fond étant plus
        // sombre que celui de la Victoire, le texte doit compenser en contraste,
        // pas juste reprendre la même nuance "discrète" qui marchait sur un fond
        // plus clair.
        _gameOverNextUnlockText.text = text;
        _gameOverNextUnlockText.color = highlight ? _challengeSuccessColor : new Color32(0x33, 0x27, 0x1A, 0xFF);

        CanvasGroup cg = _gameOverNextUnlockText.GetComponent<CanvasGroup>();
        if (cg == null) cg = _gameOverNextUnlockText.gameObject.AddComponent<CanvasGroup>();

        cg.alpha = 0f;
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
        "La Source Corrompue n'a pas tenu la distance.",
        "Elle ne se relèvera pas.",
        "La corruption s'éteint ici.",
    };

    // AJOUTE - pool pour PopulateEmptyBuildState côté Victoire (0 upgrade
    // obtenue mais victoire quand même - rare mais possible en théorie sur un
    // run très rapide/chanceux avant le boss 3).
    private static readonly string[] _victoryEmptyBuildLines =
    {
        "Une victoire sans la moindre compétence. Chapeau.",
        "Aucune compétence récupérée. Le style, ça compte double.",
        "Rien pris en cours de route, et pourtant tu es là.",
    };

    // AJOUTE - pool pour PopulateEmptyBuildState côté Game Over (mort très
    // précoce, avant le premier level-up - confirmé en conditions réelles,
    // pas juste théorique).
    private static readonly string[] _gameOverEmptyBuildLines =
    {
        "Pas eu le temps de récupérer la moindre compétence.",
        "Aucune compétence récupérée cette fois.",
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
        "Aucun exploit cette fois — juste une victoire de plus.",
    };

    // AJOUTE - récap avec du ton (temps 1), remplace l'ancien SubtitleText statique.
    // ---- Adaptation des écrans de fin au mode de jeu (2026-09-26) ------------------------------------------------------------

    // "Tentative n°X" propre à chaque mode ; le nom du mode précède le numéro hors Classique.
    private string BuildAttemptLabel()
    {
        int n = MetaProgressionManager.Instance != null ? MetaProgressionManager.Instance.GetModeAttemptNumber(GameModes.Current) : 1;
        return GameModes.IsClassic
            ? $"Tentative n°{n}"
            : $"{GameModes.ShortName(GameModes.Current)}\nTentative n°{n}";
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
        if (GameModes.IsTitans) return $"Survie {mins:00}:{secs:00}    ·    Titans {bossKills}/3";

        string boss;
        if (GameModes.IsEndless) boss = $"Boss {bossKills}";
        else if (GameModes.IsTitans) boss = $"Titans {bossKills}/3";
        else if (GameModes.IsBossRush)
        {
            int total = GameModes.RushBossCount(MetaProgressionManager.Instance != null ? MetaProgressionManager.Instance.Data : null);
            boss = $"Boss {bossKills}/{total}";
        }
        else boss = $"Boss {bossKills}/3";

        return $"Survie {mins:00}:{secs:00}    ·    Éliminations {killCount}    ·    Niveau {level}    ·    {boss}";
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
        "Tu as tenu {t} face à des boss de plus en plus féroces.",
        "{b} boss vaincus avant la chute. Le prochain record est à portée.",
    };

    private string BuildModeGameOverMessage(float runTime, int bossKillCount)
    {
        if (GameModes.IsBossRush)
        {
            int total = GameModes.RushBossCount(MetaProgressionManager.Instance != null ? MetaProgressionManager.Instance.Data : null);
            return $"{bossKillCount} boss sur {total}. La ruée ne pardonne pas — reviens plus fort.";
        }
        if (GameModes.IsTitans)
        {
            string killed = bossKillCount <= 0 ? "Aucun titan abattu"
                : bossKillCount == 1 ? "1 titan abattu"
                : $"{bossKillCount} titans abattus";
            return $"{killed} sur 3. Les trois ensemble ne pardonnent pas — reviens plus fort.";
        }

        string line = _endlessEndLines[UnityEngine.Random.Range(0, _endlessEndLines.Length)];
        return line.Replace("{t}", FormatTime(runTime)).Replace("{b}", bossKillCount.ToString());
    }

    private void PopulateVictoryRecap(float runTime, int killCount)
    {
        if (_victoryRecapText == null) return;

        if (GameModes.IsBossRush)
        {
            int total = GameModes.RushBossCount(MetaProgressionManager.Instance != null ? MetaProgressionManager.Instance.Data : null);
            _victoryRecapText.text = $"{total} boss abattus en {FormatTime(runTime)}. La ruée est à toi.";
            return;
        }
        if (GameModes.IsTitans)
        {
            _victoryRecapText.text = $"Les 3 titans terrassés en {FormatTime(runTime)}. Rien ne t'arrête.";
            return;
        }

        string closer = _victoryClosers[UnityEngine.Random.Range(0, _victoryClosers.Length)];
        _victoryRecapText.text = $"{FormatTime(runTime)} au compteur, {killCount} ennemis au tapis. {closer}";
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
                    new RecordCheck { current = runTime, best = data.bestTime, newRecordMessage = $"Nouveau record de survie : {FormatTime(runTime)} !" },
                    new RecordCheck { current = killCount, best = data.bestKills, newRecordMessage = $"Nouveau record d'éliminations : {killCount} !" },
                    new RecordCheck { current = levelReached, best = data.bestLevel, newRecordMessage = $"Nouveau record de niveau : {levelReached} !" },
                    new RecordCheck { current = goldThisRun, best = data.bestGoldInRun, newRecordMessage = $"Nouveau record d'Or en une partie : {goldThisRun} !" },
                };

                foreach (RecordCheck check in checks)
                {
                    if (check.best > 0f && check.current >= check.best)
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
            _victoryQuietLineText.text = _victoryQuietLines[UnityEngine.Random.Range(0, _victoryQuietLines.Length)];
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

        SetEndScreenTitle(_victoryPanel, "VICTOIRE !");
        PopulateVictoryRecap(runTimer, killCount);
        FitSeparatorToText(_victoryRecapText);
        bool recordShown = PopulateVictoryRecordHighlight(runTimer, killCount, level, totalGold);
        PopulateVictoryQuietLine(recordShown, challengeCompleted);
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

    public void SetHUDVisible(bool visible)
    {
        if (_hudPanel != null)
            _hudPanel.SetActive(visible);
    }

    public void ShowUltEffect(bool show)
    {
        Debug.Log(show ? "ULT ACTIF — ennemis ralentis !" : "ULT terminé");
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
        }
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

    public void UpdateGold(int amount)
    {
        if (_goldText != null)
            _goldText.text = $"Or : {amount}";
    }

    public void UpdateKillCount(int kills)
    {
        if (_killCountText != null)
            _killCountText.text = $"Éliminations : {kills}";
    }

    public void UpdateXPBar(float currentXP, float xpToNextLevel, int level)
    {
        if (_xpBar != null)
        {
            _xpBar.value = (xpToNextLevel > 0) ? currentXP / xpToNextLevel : 0f;
        }

        if (_levelText != null)
            _levelText.text = $"Niv. {level}";
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

    public void UpdateTimer(float seconds)
    {
        if (_timerText == null) return;
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
            _bossNameText.text = bossName;
        }
        if (_bossHPSlider != null) _bossHPSlider.value = 1f;
        if (_bossIcon != null) _bossIcon.SetActive(true);
        if (_bossHPBar != null) { HudBar hb = _bossHPBar.GetComponent<HudBar>(); if (hb != null) hb.PlayIntro(); }   // la barre se remplit à l'apparition
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
        SetEndScreenTitle(_gameOverPanel, GameModes.IsEndless ? "FIN DE PARTIE" : "DÉFAITE");

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
        if (_pausePanel != null) _pausePanel.SetActive(show);
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
                _pauseStatsText.text = $"Temps : {mins:00}:{secs:00}\nEnnemis tués : {kills}\nGold : {gold}";
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