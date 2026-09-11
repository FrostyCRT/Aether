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
    private static readonly Color _emptyTierColor = new Color32(0x59, 0x4C, 0x40, 150);
    private static readonly Color _filledTierColor = new Color32(0x2D, 0xD4, 0xCF, 255);
    private static readonly Color _maxTierColor = new Color32(0xFF, 0xC9, 0x4D, 255);

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

    [Header("Game Over — Records/Build (parité avec Victoire)")]
    // AJOUTE - le Game Over affiche desormais les memes informations que la
    // Victoire (records + build obtenu) : un joueur qui meurt apprend souvent
    // plus qu'un joueur qui gagne, pas de raison qu'il ait moins d'infos.
    [SerializeField] private TextMeshProUGUI _gameOverRecordsText;
    [SerializeField] private TextMeshProUGUI _gameOverBuildListText;
    [SerializeField] private TextMeshProUGUI _gameOverBuildListText2;

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
    // Teinte des pips quand le bouclier est cassé (recharge verrouillée) : rouge-gris désaturé.
    [SerializeField] private Color _manaShieldLockedColor = new Color32(0x8A, 0x5A, 0x5A, 200);

    [Header("Cristal")]
    [SerializeField] private UnityEngine.UI.Image[] _crystalIcons;
    [SerializeField] private GameObject _ultReadyEffect;
    [SerializeField] private TextMeshProUGUI _ultStackText;

    [Header("Pause")]
    [SerializeField] private GameObject _pausePanel;
    [SerializeField] private TextMeshProUGUI _pauseStatsText;
    [SerializeField] private TextMeshProUGUI _pauseUpgradesText;
    [SerializeField] private GameObject _abandonConfirmPanel;

    [Header("Victoire")]
    [SerializeField] private GameObject _victoryPanel;
    [SerializeField] private TextMeshProUGUI _victoryStatsText;
    [SerializeField] private TextMeshProUGUI _victoryRecordsText;
    [SerializeField] private TextMeshProUGUI _victoryBuildListText;
    [SerializeField] private TextMeshProUGUI _victoryBuildListText2; // NOUVEAU
    [SerializeField] private int _buildListMaxLinesPerColumn = 8;

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

    // Teintes de fond des tuiles, par branche d'upgrade (rouge/vert/bleu/doré,
    // mêmes familles que les parchemins des cartes de level-up).
    private static readonly Color _tileTintAether    = new Color32(0xC4, 0x5A, 0x3A, 0xE6);
    private static readonly Color _tileTintKael      = new Color32(0x4C, 0x8A, 0x4C, 0xE6);
    private static readonly Color _tileTintLyra      = new Color32(0x4C, 0x74, 0xC4, 0xE6);
    private static readonly Color _tileTintUniversal = new Color32(0xC4, 0xA0, 0x4D, 0xE6);

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
    }

    private void Start()
    {
        if (MetaProgressionManager.Instance != null)
            UpdateGold(MetaProgressionManager.Instance.RunGold);

        UpdateKillCount(0);
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
            ? "<color=#FFC94D>ULT x2</color>"
            : "<color=#2DD4CF>ULT x1</color>";
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
                challengeText.color = _challengeSuccessColor;
                challengeText.text = $"Défi réussi ! x{rewardPercent:0.00}";
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

    private void PopulateGameOverMessage(float runTime, int killCount, int levelReached, int goldThisRun, string deathCause)
    {
        if (_gameOverMessageText == null) return;

        bool isRecordHighlight = false;
        string message = null;

        if (MetaProgressionManager.Instance != null && MetaProgressionManager.Instance.Data != null)
        {
            SaveData data = MetaProgressionManager.Instance.Data;

            // totalRuns > 1 : au tout premier run, tous les "best" valent deja
            // les valeurs de CE run (SaveRunResults les a ecrasees juste avant),
            // donc sans cette garde on annoncerait un record battu sans aucune
            // reference anterieure a avoir battu.
            if (data.totalRuns > 1)
            {
                RecordCheck[] checks =
                {
                    new RecordCheck { current = runTime, best = data.bestTime, newRecordMessage = $"Nouveau record de survie : {FormatTime(runTime)} !", nearRecordMessage = "À deux doigts de ton record de survie..." },
                    new RecordCheck { current = killCount, best = data.bestKills, newRecordMessage = $"Nouveau record de kills : {killCount} ennemis !", nearRecordMessage = "Si proche de ton record de kills..." },
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
            message = GameOverMessagePool.GetMessage(deathCause);

        _gameOverMessageText.text = message;
        _gameOverMessageText.color = isRecordHighlight ? _challengeSuccessColor : _gameOverMessageDefaultColor;
    }

    // AJOUTE - factorise le calcul des records (deja identique entre Victoire et
    // Game Over) plutot que de dupliquer la meme logique 2 fois.
    private void PopulateRecordsText(TextMeshProUGUI recordsText)
    {
        if (recordsText == null) return;
        if (MetaProgressionManager.Instance == null || MetaProgressionManager.Instance.Data == null) return;

        SaveData data = MetaProgressionManager.Instance.Data;
        int bestMins = Mathf.FloorToInt(data.bestTime / 60f);
        int bestSecs = Mathf.FloorToInt(data.bestTime % 60f);

        // MODIFIE - "Runs totales" -> "Parties totales" (preference de
        // vocabulaire deja donnee : "run" evite dans les textes du jeu). Ajout
        // des 2 nouveaux records (niveau, or en une partie).
        recordsText.text = $"Meilleur temps : {bestMins:00}:{bestSecs:00}\nMeilleur kills : {data.bestKills}\nMeilleur niveau : {data.bestLevel}\nMeilleur or en une partie : {data.bestGoldInRun}\nParties totales : {data.totalRuns}";
    }

    // AJOUTE - meme principe pour la liste de build (decoupage en 2 colonnes
    // au-dela de _buildListMaxLinesPerColumn), partagee entre Victoire et Game Over.
    private void PopulateBuildList(TextMeshProUGUI listText, TextMeshProUGUI listText2)
    {
        if (listText == null || LevelUpManager.Instance == null) return;

        List<string> lines = LevelUpManager.Instance.GetUpgradesList();

        if (lines.Count <= _buildListMaxLinesPerColumn)
        {
            listText.text = string.Join("\n", lines);
            if (listText2 != null) listText2.text = "";
        }
        else
        {
            int splitIndex = Mathf.CeilToInt(lines.Count / 2f);
            listText.text = string.Join("\n", lines.GetRange(0, splitIndex));
            if (listText2 != null)
                listText2.text = string.Join("\n", lines.GetRange(splitIndex, lines.Count - splitIndex));
        }
    }

    // AJOUTE - grille d'icônes de la build obtenue cette partie. Suit
    // LevelUpManager.ObtainedOrder (ordre chronologique de pick). Chaque tuile :
    // fond teinté par branche + icône + pastilles de palier (ou "xN" pour les
    // upgrades à cap élevé, ou losange de déblocage pour les armes à pick séparé).
    // Pas de nom : sur un écran de résultats, l'icône suffit et reste compacte.
    private void PopulateBuildGrid(Transform gridContent)
    {
        foreach (GameObject old in _spawnedBuildGridSlots)
            if (old != null) Destroy(old);
        _spawnedBuildGridSlots.Clear();

        if (gridContent == null || _buildGridSlotPrefab == null || LevelUpManager.Instance == null)
            return;

        System.Collections.Generic.IReadOnlyList<UpgradeData> obtained = LevelUpManager.Instance.ObtainedOrder;
        if (obtained == null) return;

        foreach (UpgradeData upgrade in obtained)
        {
            if (upgrade == null) continue;
            GameObject slotGO = Instantiate(_buildGridSlotPrefab, gridContent);
            ConfigureBuildGridSlot(slotGO, upgrade);
            _spawnedBuildGridSlots.Add(slotGO);
        }
    }

    private void ConfigureBuildGridSlot(GameObject slotGO, UpgradeData upgrade)
    {
        UpgradeSlotRefs refs = slotGO.GetComponent<UpgradeSlotRefs>();
        if (refs == null)
        {
            Debug.LogWarning("[GameUI] Le prefab de slot de grille n'a pas de composant UpgradeSlotRefs.");
            return;
        }

        if (refs.background != null)
            refs.background.color = GetTileTint(upgrade.Branch);

        if (refs.icon != null)
            refs.icon.sprite = upgrade.icon;

        // Grille compacte : jamais de nom.
        if (refs.nameText != null)
            refs.nameText.gameObject.SetActive(false);

        int maxLevel = upgrade.MaxLevel;
        int currentLevel = upgrade.GetDisplayLevel();
        bool alreadyMaxed = currentLevel >= maxLevel;
        bool showDots = maxLevel > 1 && maxLevel <= 3 && refs.tierDots != null;

        if (refs.tierDots != null)
        {
            for (int d = 0; d < refs.tierDots.Length; d++)
            {
                if (refs.tierDots[d] == null) continue;

                bool dotExists = showDots && d < maxLevel;
                refs.tierDots[d].gameObject.SetActive(dotExists);
                if (!dotExists) continue;

                bool filled = d < currentLevel;
                if (!filled)
                    refs.tierDots[d].color = _emptyTierColor;
                else
                    refs.tierDots[d].color = alreadyMaxed ? _maxTierColor : _filledTierColor;
            }
        }

        // "xN" pour les upgrades à cap élevé (Dégâts/Cadence/Soin), pas de pastilles.
        bool showStackCount = !showDots && maxLevel > 1 && currentLevel >= 1;
        if (refs.stackCountText != null)
        {
            refs.stackCountText.gameObject.SetActive(showStackCount);
            if (showStackCount)
                refs.stackCountText.text = $"x{currentLevel}";
        }

        // Losange de déblocage (armes à pick séparé : Orbital, Foudre, Boue, etc.).
        if (refs.unlockDot != null)
        {
            bool requiresUnlock = upgrade.RequiresUnlockPick;
            refs.unlockDot.gameObject.SetActive(requiresUnlock);
            if (requiresUnlock)
                refs.unlockDot.color = upgrade.IsUnlocked() ? _filledTierColor : _emptyTierColor;
        }
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
    private IEnumerator PlayNextUnlockPreview()
    {
        if (_gameOverNextUnlockText == null) yield break;

        MetaProgressionManager.NextUnlockPreview preview = MetaProgressionManager.Instance != null
            ? MetaProgressionManager.Instance.GetNextUnlockPreview()
            : null;

        if (preview == null || !preview.HasPreview)
        {
            _gameOverNextUnlockText.gameObject.SetActive(false);
            yield break;
        }

        string currencyLabel = preview.IsGoldCurrency ? "Or" : "Éclats";

        _gameOverNextUnlockText.text = preview.AmountStillNeeded > 0
            ? $"Encore {preview.AmountStillNeeded} {currencyLabel} pour débloquer {preview.NodeName}"
            : $"De quoi débloquer {preview.NodeName} dès maintenant !";

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
    private static readonly string[] _victoryClosers =
    {
        "La Source Corrompue n'a pas tenu la distance.",
        "Le rift s'est refermé pour de bon.",
        "Elle ne se relèvera pas.",
        "Trois boss, zéro pitié.",
        "Une victoire nette, sans trembler.",
        "La corruption s'éteint ici.",
        "Le cristal a tenu bon jusqu'au bout.",
    };

    // AJOUTE - pool dédié à PopulateVictoryQuietLine (voir plus bas). Sujet
    // volontairement différent de _victoryClosers ci-dessus (qui parle déjà du
    // boss vaincu) : ici on reconnaît l'absence de record/défi sans plomber le
    // ton, même famille de voix que le reste (familier, direct, un peu de
    // personnalité, jamais ronflant).
    private static readonly string[] _victoryQuietLines =
    {
        "Pas de record aujourd'hui, mais la victoire est bien réelle.",
        "Rien d'exceptionnel à signaler, à part la victoire elle-même.",
        "Une run sans éclat particulier, mais qui compte double.",
        "Simple, efficace, sans trembler.",
        "Aucun exploit cette fois — juste une victoire de plus.",
        "Pas de nouveau sommet, mais toujours debout.",
        "La routine, version victorieuse.",
    };

    // AJOUTE - récap avec du ton (temps 1), remplace l'ancien SubtitleText statique.
    private void PopulateVictoryRecap(float runTime, int killCount)
    {
        if (_victoryRecapText == null) return;

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

        if (MetaProgressionManager.Instance != null && MetaProgressionManager.Instance.Data != null)
        {
            SaveData data = MetaProgressionManager.Instance.Data;

            // totalRuns > 1 : meme garde que PopulateGameOverMessage, jamais de
            // "nouveau record" sur la toute premiere partie (rien a battre).
            if (data.totalRuns > 1)
            {
                RecordCheck[] checks =
                {
                    new RecordCheck { current = runTime, best = data.bestTime, newRecordMessage = $"Nouveau record de survie : {FormatTime(runTime)} !" },
                    new RecordCheck { current = killCount, best = data.bestKills, newRecordMessage = $"Nouveau record de kills : {killCount} ennemis !" },
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
        if (_victoryStatsText != null)
            _victoryStatsText.text = $"Survie {mins:00}:{secs:00}    ·    Kills {killCount}    ·    Niveau {level}    ·    Boss 3/3";

        // SUPPRIME - l'ancien bloc Records (5 lignes) et la liste texte de build
        // n'existent plus dans la refonte 3-temps ; _victoryRecordsText et
        // _victoryBuildListText/2 ne sont désormais plus assignés dans la scène.
        // Remplacés par PopulateVictoryRecordHighlight (record battu CETTE partie
        // uniquement) et PopulateBuildGrid (grille d'icônes) ci-dessous.
        PopulateVictoryRecap(runTimer, killCount);
        bool recordShown = PopulateVictoryRecordHighlight(runTimer, killCount, level, totalGold);
        PopulateVictoryQuietLine(recordShown, challengeCompleted);
        PopulateVictoryPortrait();
        PopulateBuildGrid(_victoryBuildGridContent);

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

    public void UpdateConcentration(float bonus)
    {
        if (_concentrationText == null) return;

        int pct = Mathf.RoundToInt(bonus * 100f);
        _concentrationText.text = pct > 0 ? $"{pct}%" : "Concentration";

        // Réf. de teinte : plafond max du nœud (0.50). En dessous = interpolation
        // cyan -> doré, au plafond = doré plein.
        float t = Mathf.Clamp01(bonus / 0.5f);
        _concentrationText.color = Color.Lerp(_filledTierColor, _maxTierColor, t);
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
                _manaShieldPips[i].color = locked ? _manaShieldLockedColor : _filledTierColor;
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
            _killCountText.text = $"Kills : {kills}";
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
    }

    public void UpdateBossHP(float current, float max)
    {
        if (_bossHPSlider != null)
        {
            _bossHPSlider.value = (max > 0) ? current / max : 0f;
        }
    }

    public void HideBossHP()
    {
        if (_bossHPBar != null) _bossHPBar.SetActive(false);
        if (_bossNameText != null) _bossNameText.gameObject.SetActive(false);
        if (_bossIcon != null) _bossIcon.SetActive(false);
    }

    // MODIFIE - ajout de "deathCause" (transmis par HealthSystem -> GameManager)
    // pour le message d'ambiance contextuel.
    public void ShowGameOver(float runTimer, int killCount, int baseGold, int totalGold, int level, int eclatsEarned, bool challengeCompleted, float challengeRewardPercent, string deathCause)
    {
        if (_gameOverPanel != null) _gameOverPanel.SetActive(true);

        int mins = Mathf.FloorToInt(runTimer / 60f);
        int secs = Mathf.FloorToInt(runTimer % 60f);

        if (_statsText != null)
        {
            _statsText.text = $"Temps de survie : {mins:00}:{secs:00}\nEnnemis tués : {killCount}\nNiveau atteint : {level}";
        }

        PopulateRecordsText(_gameOverRecordsText);
        PopulateBuildList(_gameOverBuildListText, _gameOverBuildListText2);

        // MODIFIE - passe desormais le niveau atteint et l'Or total (bonus de
        // defi inclus) pour couvrir les 4 records au lieu de 2.
        PopulateGameOverMessage(runTimer, killCount, level, totalGold, deathCause);

        // AJOUTE - "Tentative n°X", Data.totalRuns a deja ete incremente par
        // SaveRunResults() avant l'appel a cette methode.
        if (_gameOverAttemptText != null && MetaProgressionManager.Instance != null && MetaProgressionManager.Instance.Data != null)
        {
            _gameOverAttemptText.text = $"Tentative n°{MetaProgressionManager.Instance.Data.totalRuns}";
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