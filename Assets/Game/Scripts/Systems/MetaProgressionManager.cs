using UnityEngine;

public class MetaProgressionManager : MonoBehaviour
{
    // AJOUTE - resultat de GetNextUnlockPreview(), utilise par l'ecran de Game
    // Over pour afficher "Encore X Or pour debloquer [Noeud]".
    public class NextUnlockPreview
    {
        public bool HasPreview;
        public string NodeName;
        public int Cost;
        public int CurrentAmount;
        public bool IsGoldCurrency; // true = Or, false = Eclats
        public int AmountStillNeeded => Mathf.Max(0, Cost - CurrentAmount);
    }

    public static MetaProgressionManager Instance { get; private set; }

    public SaveData Data { get; private set; }
    public int RunGold { get; private set; } = 0;

    [Header("Personnages jouables")]
    [SerializeField] private GameObject[] _characterPrefabs;

    // =====================
    // BARÈME DES RÉCOMPENSES
    // =====================
    // REFONTE (2026-10-27, retour utilisateur : "en une partie complète je gagne beaucoup trop d'or, dans les 40 000, et
    // éclats c'est pareil [...] je veux que les joueurs restent, qu'ils aient un intérêt de revenir sur le jeu") - l'or
    // venait presque entièrement des ennemis tués (1 à 5 or chacun) : plus le joueur était fort, plus il tuait, plus il
    // gagnait. Mesuré sur 6 parties automatiques (joueur immobile, cartes au hasard) : victoire Modéré 5 953 or pour un
    // nouveau joueur, 23 510 pour un vétéran ; Apocalyptique 42 917 / 47 546 - alors que tout l'arbre (3 branches)
    // coûtait 14 000 or. Les éclats (15 par niveau + boss + victoire) payaient toute la Réputation en ~12 victoires.
    // Désormais l'or vient surtout de la PROGRESSION (minutes survécues, boss, victoire), les ennemis n'en donnent plus
    // qu'une petite part (KillGoldFactor) : victoire ~1 000 or en Modéré (1 044 nouveau / 1 396 vétéran), ~1 700 /
    // 2 200 en Violent, ~3 000 / 3 100 en Apocalyptique. Les éclats ne viennent plus que des boss et des victoires, plus
    // une prime unique pour la première victoire de chaque personnage dans chaque palier. Objectifs : une branche
    // maxée en ~30 parties, toute la Réputation en ~150. Coûts de l'arbre : SkillTreeData.
    // Constantes (et non [SerializeField]) : l'ancienne version sérialisait ses valeurs dans la scène MainMenu, qui
    // auraient écrasé ce barème sans qu'on le voie dans le code.
    public const int GoldPerMinute = 20;                       // par minute de chrono (Classique, Sans fin, Ruée)
    public const float KillGoldFactor = 0.02f;                 // part de EnemyBase._goldValue réellement versée
    public const int ClassicVictoryGold = 250;
    public const int RushVictoryGold = 125;
    public const int TitansVictoryGold = 50;
    public const float RushBossRewardScale = 0.5f;             // 9 boss rapprochés : la moitié d'un boss du Classique
    public const float TitansBossRewardScale = 0.2f;           // ~1 à 2 min de partie : pas de ferme à or
    private static readonly int[] EclatsPerBoss = { 10, 20, 30 };   // boss 1, 2, 3
    public const int EclatsVictoryBonus = 40;
    public const int TitansVictoryEclats = 10;
    // Prime unique de la PREMIÈRE victoire du Classique dans un palier, pour chaque personnage (Modéré, Violent, Apocalyptique).
    private static readonly int[] FirstWinEclats = { 300, 600, 1000 };

    [Header("Déblocage des personnages (filet Éclats)")]
    [SerializeField] private int _kaelUnlockEclatsCost = 500;
    [SerializeField] private int _lyraUnlockEclatsCost = 1200;

    public int TotalEclats => Data?.totalEclats ?? 0;

    // AJOUTE - nom du dernier personnage débloqué cette session (condition en jeu
    // OU achat Éclats). Lu et remis à null par l'écran de fin de partie pour
    // afficher "Nouveau personnage débloqué : X !". null si rien à annoncer.
    public string PendingUnlockNotification { get; private set; }

    // AJOUTE - expose le nombre d'Eclats gagnes lors du DERNIER appel a
    // SaveRunResults(), pour que GameManager puisse le transmettre a
    // GameUI.ShowVictory()/ShowGameOver() pour l'animation de comptage - avant
    // ça, seul le total cumule (Data.totalEclats) etait accessible, pas le
    // montant de cette run precise.
    public int LastRunEclatsEarned { get; private set; } = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadData();
    }

    public void LoadData()
    {
        Data = SaveSystem.Load();
        if (Data == null) Data = new SaveData();
        MigrateClassicBossProgress();
    }

    // Saves d'avant les modes de jeu : on retrouve la progression boss d'après les déblocages existants
    // (Lyra = victoire = 3 boss ; Kael = boss 2 atteint donc boss 1 battu = 1). Ne fait que monter, jamais descendre.
    private void MigrateClassicBossProgress()
    {
        if (Data.bestClassicBossKills >= 3) return;
        if (Data.lyraUnlocked) Data.bestClassicBossKills = 3;
        else if (Data.kaelUnlocked && Data.bestClassicBossKills < 1) Data.bestClassicBossKills = 1;
    }

    // Or des ennemis tués : seule une petite part de leur valeur (KillGoldFactor), cumulée en fractions pour que même
    // un ennemi à 1 or compte. Remis à zéro avec RunGold (SaveRunResults).
    private float _killGoldCarry;

    public void AddKillGold(int enemyGoldValue)
    {
        _killGoldCarry += enemyGoldValue * KillGoldFactor;
        if (_killGoldCarry < 1f) return;
        int whole = Mathf.FloorToInt(_killGoldCarry);
        _killGoldCarry -= whole;
        AddRunGold(whole);
    }

    // Or d'un boss, réduit dans les modes qui enchaînent les boss bien plus vite que le Classique.
    public void AddBossGold(int bossGoldValue)
    {
        float scale = GameModes.IsTitans ? TitansBossRewardScale : GameModes.IsBossRush ? RushBossRewardScale : 1f;
        AddRunGold(Mathf.RoundToInt(bossGoldValue * scale));
    }

    // Appelé par GameManager à chaque minute pleine du chrono.
    public void AddSurvivalMinuteGold()
    {
        if (GameModes.IsClassic || GameModes.IsEndless || GameModes.IsBossRush)
            AddRunGold(GoldPerMinute);
    }

    // Appelé par GameManager à la victoire, AVANT l'évaluation du défi (le défi "Fortune" compte cet or).
    public void AddVictoryGold()
    {
        if (GameModes.IsTitans) AddRunGold(TitansVictoryGold);
        else if (GameModes.IsBossRush) AddRunGold(RushVictoryGold);
        else if (GameModes.IsClassic) AddRunGold(ClassicVictoryGold);
    }

    public void AddRunGold(int amount) => AddRunGold(amount, true);

    public void AddRunGold(int amount, bool applyDifficulty)
    {
        // AJOUTÉ (2026-10-18, diagnostic complet - système de difficulté ; ÉTENDU 2026-10-20, carrousel de
        // difficulté - "les boss [...] dans les autres difficultés bénéficient du scale donc [...] un niveau de
        // difficulté pour ça aussi") - l'or encaissé pendant la partie est majoré au palier de difficulté, qui
        // s'applique maintenant aussi en Sans fin et Ruée de boss (plus seulement le Classique) ; x1 hors ces 3
        // modes ou au palier Modéré. Arrondi au plus proche pour ne jamais perdre le dernier point d'or sur un
        // petit montant.
        // MODIFIÉ (2026-10-27) - applyDifficulty = false pour la prime du défi de l'heure : elle était repassée dans ce
        // multiplicateur alors qu'elle était calculée sur un or DÉJÀ majoré (x2,5 au lieu de x2 en Apocalyptique).
        if (applyDifficulty && (GameModes.IsClassic || GameModes.IsEndless || GameModes.IsBossRush))
            amount = Mathf.RoundToInt(amount * DifficultyTiers.RewardMultiplier(DifficultyTiers.Current));

        RunGold += amount;
        if (GameUI.Instance != null && GameUI.Instance.gameObject.activeInHierarchy)
            GameUI.Instance.UpdateGold(RunGold);

        // AJOUTE - rafraichit la progression du defi "Fortune" (FortuneGoldGoal Or) en
        // temps reel, pas seulement en fin de run.
        if (ChallengeManager.Instance != null)
            ChallengeManager.Instance.RefreshDisplay();
    }

    // AJOUTÉ (2026-10-26, bug signalé par l'utilisateur : "quand on meurt au boss 3, c'est écrit Nouveau record de survie
    // 15 min ! Sauf que c'est pas record vu que j'ai déjà eu les 15 minutes plusieurs fois") - records du Classique tels
    // qu'ils étaient AVANT la partie qui vient de finir. SaveRunResults les remplace aussitôt par ceux de la partie, et les
    // écrans de fin comparaient la partie à ces records déjà mis à jour : une égalité (15:00, le chrono se fige au boss 3)
    // passait pour un record battu. Les écrans de fin comparent désormais à ces valeurs-ci.
    public float PreviousBestTime { get; private set; }
    public int PreviousBestKills { get; private set; }
    public int PreviousBestLevel { get; private set; }
    public int PreviousBestGoldInRun { get; private set; }

    public void SaveRunResults(float runTime, int kills, int levelReached, int bossKills, bool victory)
    {
        if (Data == null) LoadData();
        Data.totalRuns++;
        Data.totalGold += RunGold;
        GameMode mode = GameModes.Current;
        bool endless = mode == GameMode.Endless;
        bool rush = mode == GameMode.BossRush;
        bool classic = mode == GameMode.Classic;
        LastRunRecordMessage = null;

        switch (mode)
        {
            case GameMode.Endless:
            {
                // Mode sans fin : records à part, les records du classique (15 min) ne sont pas touchés.
                bool hadRecord = Data.bestEndlessTime > 0f;
                if (hadRecord && runTime > Data.bestEndlessTime)
                    LastRunRecordMessage = Loc.F("Nouveau record sans fin : {0} !", FormatClock(runTime));
                else if (hadRecord && bossKills > Data.bestEndlessBossKills)
                    LastRunRecordMessage = Loc.F("Nouveau record sans fin : {0} boss vaincus !", bossKills);
                if (runTime > Data.bestEndlessTime) Data.bestEndlessTime = runTime;
                if (kills > Data.bestEndlessKills) Data.bestEndlessKills = kills;
                if (bossKills > Data.bestEndlessBossKills) Data.bestEndlessBossKills = bossKills;
                Data.endlessRuns++;
                break;
            }
            case GameMode.BossRush:
            {
                if (Data.rushBestBossKills > 0 && bossKills > Data.rushBestBossKills)
                    LastRunRecordMessage = Loc.F("Nouveau record de ruée : {0} boss vaincus !", bossKills);
                if (bossKills > Data.rushBestBossKills) Data.rushBestBossKills = bossKills;
                Data.rushRuns++;
                if (victory) Data.rushWins++;
                break;
            }
            case GameMode.Titans:
            {
                bool hadWin = Data.titansBestTime > 0f;
                if (victory && (!hadWin || runTime < Data.titansBestTime))
                {
                    if (hadWin) LastRunRecordMessage = Loc.F("Nouveau record du Choc des titans : {0} !", FormatClock(runTime));
                    Data.titansBestTime = runTime;
                }
                else if (!victory && Data.titansBestBossKills > 0 && bossKills > Data.titansBestBossKills)
                    LastRunRecordMessage = bossKills > 1 ? Loc.F("Nouveau record : {0} titans abattus !", bossKills) : Loc.F("Nouveau record : {0} titan abattu !", bossKills);
                if (bossKills > Data.titansBestBossKills) Data.titansBestBossKills = bossKills;
                Data.titansRuns++;
                if (victory) Data.titansWins++;
                break;
            }
            default:
            {
                // MODIFIÉ (2026-10-20, carrousel de difficulté) - le record de survie du Classique se tient
                // maintenant PAR PALIER (voir EnsureBestTimeByTierArray) ; l'ancien champ bestTime n'est plus
                // écrit (gardé seulement pour la migration en lecture d'une sauvegarde pré-existante).
                EnsureBestTimeByTierArray();
                int tierIndex = (int)DifficultyTiers.Current;
                PreviousBestTime = Data.bestTimeByTier[tierIndex];
                PreviousBestKills = Data.bestKills;
                PreviousBestLevel = Data.bestLevel;
                PreviousBestGoldInRun = Data.bestGoldInRun;
                if (runTime > Data.bestTimeByTier[tierIndex]) Data.bestTimeByTier[tierIndex] = runTime;
                if (kills > Data.bestKills) Data.bestKills = kills;
                // AJOUTE - 2 records supplementaires. RunGold est lu AVANT d'etre remis a
                // 0 plus bas, et reflete deja le bonus de defi eventuel (deja applique
                // par ChallengeManager avant l'appel a SaveRunResults) - donc "meilleur
                // or en une partie" inclut logiquement le bonus, comme le montant reel
                // que le joueur repart avec.
                if (levelReached > Data.bestLevel) Data.bestLevel = levelReached;
                if (RunGold > Data.bestGoldInRun) Data.bestGoldInRun = RunGold;
                // Progression des modes : combien de boss ce joueur a déjà battus en classique (ouvre / dimensionne la Ruée).
                if (bossKills > Data.bestClassicBossKills) Data.bestClassicBossKills = Mathf.Min(bossKills, 3);
                break;
            }
        }

        RecordCharacterRun(runTime, victory && classic, !classic);

        int eclatsEarned = CalculateEclatsEarned(levelReached, bossKills, victory, mode);
        if (victory && classic)
        {
            int firstWinBonus = RecordClassicWin(GetSelectedCharacterIndex(), DifficultyTiers.Current);
            if (firstWinBonus > 0)
            {
                eclatsEarned += firstWinBonus;
                LastRunRecordMessage = FirstWinMessage(DifficultyTiers.Current, firstWinBonus);
            }
        }
        Data.totalEclats += eclatsEarned;
        LastRunEclatsEarned = eclatsEarned;

        SaveSystem.Save(Data);
        RunGold = 0;
        _killGoldCarry = 0f;

        // Déblocage de Lyra : gagner une partie complète (Boss 3) - EN CLASSIQUE uniquement. Idempotent,
        // se sauvegarde lui-même si c'est un nouveau déblocage.
        if (victory && classic)
            UnlockCharacter(2);
    }

    // Dernier record battu par la partie qui vient de se terminer (modes Sans fin / Ruée), null sinon. Lu par les écrans de fin.
    public string LastRunRecordMessage { get; private set; }

    // Numéro de la partie qui vient d'être enregistrée DANS SON MODE (pour "Tentative n°X" : chaque mode a son compteur).
    public int GetModeAttemptNumber(GameMode mode)
    {
        if (Data == null) return 1;
        switch (mode)
        {
            case GameMode.Endless: return Mathf.Max(1, Data.endlessRuns);
            case GameMode.BossRush: return Mathf.Max(1, Data.rushRuns);
            case GameMode.Titans: return Mathf.Max(1, Data.titansRuns);
            default: return Mathf.Max(1, Data.totalRuns - Data.endlessRuns - Data.rushRuns - Data.titansRuns);
        }
    }

    private static string FormatClock(float seconds)
    {
        int total = Mathf.FloorToInt(seconds);
        return TimeText.Clock(total / 60, total % 60);
    }

    // =====================
    // PARCOURS PAR PERSONNAGE (fiche de l'onglet Personnage)
    // =====================

    // AJOUTÉ (2026-10-20, carrousel de difficulté) - même technique que EnsureCharacterStatArrays ci-dessous :
    // garantit 3 cases (Modéré/Violent/Apocalyptique). Migration UNE SEULE FOIS (seulement quand le tableau vient
    // d'être créé, donc une vraie sauvegarde pré-existante) : l'ancien record unique (bestTime, toutes parties
    // Classique confondues avant l'existence des paliers) devient le record du palier Modéré, Violent/Apocalyptique
    // démarrent à 0.
    private void EnsureBestTimeByTierArray()
    {
        if (Data == null) return;
        if (Data.bestTimeByTier == null || Data.bestTimeByTier.Length < 3)
        {
            bool hadLegacyRecord = Data.bestTime > 0f;
            System.Array.Resize(ref Data.bestTimeByTier, 3);
            if (hadLegacyRecord) Data.bestTimeByTier[(int)DifficultyTier.Moderate] = Data.bestTime;
        }
    }

    // Record de survie du Classique pour UN palier donné (voir carrousel de difficulté, page Accueil).
    public float GetBestTime(DifficultyTier tier)
    {
        EnsureBestTimeByTierArray();
        return Data != null ? Data.bestTimeByTier[(int)tier] : 0f;
    }

    // Garantit 3 cases par tableau (save ancien/édité/tronqué) en conservant l'existant.
    private void EnsureCharacterStatArrays()
    {
        if (Data == null) return;
        if (Data.runsByCharacter == null || Data.runsByCharacter.Length < 3)
            System.Array.Resize(ref Data.runsByCharacter, 3);
        if (Data.winsByCharacter == null || Data.winsByCharacter.Length < 3)
            System.Array.Resize(ref Data.winsByCharacter, 3);
        if (Data.bestTimeByCharacter == null || Data.bestTimeByCharacter.Length < 3)
            System.Array.Resize(ref Data.bestTimeByCharacter, 3);
    }

    // Le personnage joué = celui sélectionné (la sélection ne change pas en cours de partie),
    // avec le même repli de sécurité que le spawn (perso verrouillé -> Aether).
    private void RecordCharacterRun(float runTime, bool victory, bool endless = false)
    {
        EnsureCharacterStatArrays();
        int c = Mathf.Clamp(GetSelectedCharacterIndex(), 0, 2);
        Data.runsByCharacter[c]++;
        if (victory) Data.winsByCharacter[c]++;
        // Le "meilleur temps" du parcours est celui du classique (comparable d'un perso à l'autre) : pas le sans fin.
        if (!endless && runTime > Data.bestTimeByCharacter[c]) Data.bestTimeByCharacter[c] = runTime;
    }

    // =====================
    // VICTOIRES PAR PALIER (conditions de l'arbre, primes de première victoire)
    // =====================

    // Garantit 9 cases (3 personnages x 3 paliers). Migration UNE SEULE FOIS, à la création du tableau : les victoires
    // du Classique déjà enregistrées par personnage (winsByCharacter, sans palier) comptent comme des victoires en
    // Modéré - sans prime d'éclats (elle n'est versée qu'en passant de 0 à 1 victoire, voir RecordClassicWin).
    public void EnsureWinsByTierArray()
    {
        if (Data == null) return;
        if (Data.winsByCharacterTier != null && Data.winsByCharacterTier.Length >= 9) return;
        bool created = Data.winsByCharacterTier == null || Data.winsByCharacterTier.Length == 0;
        System.Array.Resize(ref Data.winsByCharacterTier, 9);
        if (!created) return;
        EnsureCharacterStatArrays();
        for (int c = 0; c < 3; c++)
            Data.winsByCharacterTier[c * 3 + (int)DifficultyTier.Moderate] = Data.winsByCharacter[c];
    }

    // Vrai si ce personnage a gagné une partie du Classique dans ce palier OU dans un palier plus dur.
    public bool HasWonTier(int characterIndex, DifficultyTier tier)
    {
        EnsureWinsByTierArray();
        if (Data == null || characterIndex < 0 || characterIndex > 2) return false;
        for (int t = (int)tier; t < 3; t++)
            if (Data.winsByCharacterTier[characterIndex * 3 + t] > 0) return true;
        return false;
    }

    // Enregistre la victoire et renvoie la prime d'éclats de première victoire (0 si ce n'est pas la première).
    private int RecordClassicWin(int characterIndex, DifficultyTier tier)
    {
        EnsureWinsByTierArray();
        int c = Mathf.Clamp(characterIndex, 0, 2);
        int i = c * 3 + (int)tier;
        Data.winsByCharacterTier[i]++;
        return Data.winsByCharacterTier[i] == 1 ? FirstWinEclats[(int)tier] : 0;
    }

    private static string FirstWinMessage(DifficultyTier tier, int eclats)
    {
        switch (tier)
        {
            case DifficultyTier.Violent: return Loc.F("Première victoire en Violent : +{0} éclats", eclats);
            case DifficultyTier.Apocalyptic: return Loc.F("Première victoire en Apocalyptique : +{0} éclats", eclats);
            default: return Loc.F("Première victoire en Modéré : +{0} éclats", eclats);
        }
    }

    public static int CharacterIndexOf(SkillTreeData.CharacterBranch branch)
    {
        switch (branch)
        {
            case SkillTreeData.CharacterBranch.Gardien: return 1;
            case SkillTreeData.CharacterBranch.Fantome: return 2;
            default: return 0;
        }
    }

    // Condition de victoire d'un nœud de l'arbre (SkillTreeData.requiredWinTier) remplie par le personnage de sa branche.
    public bool IsNodeWinRequirementMet(string nodeId)
    {
        SkillTreeData.NodeData node = SkillTreeData.Get(nodeId);
        if (node == null || node.requiredWinTier < 0) return true;
        return HasWonTier(CharacterIndexOf(node.branch), (DifficultyTier)node.requiredWinTier);
    }

    public int GetCharacterRuns(int index)
    {
        EnsureCharacterStatArrays();
        return Data != null && index >= 0 && index < 3 ? Data.runsByCharacter[index] : 0;
    }

    public int GetCharacterWins(int index)
    {
        EnsureCharacterStatArrays();
        return Data != null && index >= 0 && index < 3 ? Data.winsByCharacter[index] : 0;
    }

    public float GetCharacterBestTime(int index)
    {
        EnsureCharacterStatArrays();
        return Data != null && index >= 0 && index < 3 ? Data.bestTimeByCharacter[index] : 0f;
    }

    // ÉCLATS PAR MODE - REFONTE (2026-10-27, voir le barème en tête de classe) : plus aucun éclat par niveau, seulement
    // les boss vaincus (10 / 20 / 30 selon le boss) et la victoire (+40), majorés par le palier de difficulté
    // (x1 / x1,5 / x2) - une victoire rapporte 100 / 150 / 200 éclats, plus la prime de première victoire (RecordClassicWin).
    //  - Sans fin : 9 boss comptés au plus (3 boucles), pas de victoire.
    //  - Ruée de boss : moitié moins par boss ; la victoire rapporte le bonus proportionnellement à la taille de la Ruée.
    //  - Choc des titans (~1 à 2 min) : seulement une petite prime de victoire, pas de ferme à éclats.
    private int CalculateEclatsEarned(int levelReached, int bossKills, bool victory, GameMode mode = GameMode.Classic)
    {
        float tierMultiplier = DifficultyTiers.RewardMultiplier(DifficultyTiers.Current);
        switch (mode)
        {
            case GameMode.Endless:
            {
                int total = 0;
                for (int i = 0; i < Mathf.Min(bossKills, 9); i++) total += EclatsPerBoss[i % 3];
                return Mathf.RoundToInt(total * tierMultiplier);
            }
            case GameMode.BossRush:
            {
                int total = 0;
                for (int i = 0; i < bossKills; i++) total += EclatsPerBoss[GameModes.RushBossNumber(i) - 1];
                total = Mathf.RoundToInt(total * RushBossRewardScale);
                if (victory)
                {
                    int progress = Mathf.Max(1, GameModes.ClassicBossProgress(Data));
                    total += (EclatsVictoryBonus * progress) / 3;
                }
                return Mathf.RoundToInt(total * tierMultiplier);
            }
            case GameMode.Titans:
                return victory ? TitansVictoryEclats : 0;
            default:
            {
                int total = 0;
                for (int i = 0; i < Mathf.Min(bossKills, 3); i++) total += EclatsPerBoss[i];
                if (victory) total += EclatsVictoryBonus;
                return Mathf.RoundToInt(total * tierMultiplier);
            }
        }
    }

    // =====================
    // PERSONNAGE SÉLECTIONNÉ
    // =====================

    // MODIFIE - retombe sur Aether (0) si l'index sauvegardé pointe vers un
    // personnage qui n'est pas / plus débloqué (save édité, ordre des conditions
    // changé...). Filet de sécurité : GetActiveBranch(), le spawn, etc. en dépendent.
    public int GetSelectedCharacterIndex()
    {
        int idx = Data?.selectedCharacterIndex ?? 0;
        return IsCharacterUnlocked(idx) ? idx : 0;
    }

    public void SetSelectedCharacter(int index)
    {
        if (Data == null) return;
        index = Mathf.Clamp(index, 0, 2);
        if (!IsCharacterUnlocked(index)) return; // sécurité : on ne sélectionne pas un perso verrouillé
        Data.selectedCharacterIndex = index;
        SaveSystem.Save(Data);
    }

    // =====================
    // DÉBLOCAGE DES PERSONNAGES
    // =====================

    public bool IsCharacterUnlocked(int index)
    {
        if (Data == null) return index == 0;
        switch (index)
        {
            case 1: return Data.kaelUnlocked;
            case 2: return Data.lyraUnlocked;
            default: return true; // Aether, toujours disponible
        }
    }

    public int GetCharacterUnlockEclatsCost(int index)
    {
        switch (index)
        {
            case 1: return _kaelUnlockEclatsCost;
            case 2: return _lyraUnlockEclatsCost;
            default: return 0;
        }
    }

    public static string GetCharacterDisplayName(int index)
    {
        switch (index)
        {
            case 1: return "Kael";
            case 2: return "Lyra";
            default: return "Aether";
        }
    }

    // Déblocage par condition en jeu (Boss 2 atteint, partie gagnée). Idempotent.
    // Retourne true seulement si c'est un NOUVEAU déblocage.
    public bool UnlockCharacter(int index)
    {
        if (Data == null || IsCharacterUnlocked(index)) return false;

        switch (index)
        {
            case 1: Data.kaelUnlocked = true; break;
            case 2: Data.lyraUnlocked = true; break;
            default: return false;
        }

        PendingUnlockNotification = GetCharacterDisplayName(index);
        SaveSystem.Save(Data);
        return true;
    }

    // Déblocage payé en Éclats depuis la page de sélection (filet anti-blocage).
    public bool TryUnlockCharacterWithEclats(int index)
    {
        if (Data == null || IsCharacterUnlocked(index)) return false;

        int cost = GetCharacterUnlockEclatsCost(index);
        if (cost <= 0 || Data.totalEclats < cost) return false;

        Data.totalEclats -= cost;
        switch (index)
        {
            case 1: Data.kaelUnlocked = true; break;
            case 2: Data.lyraUnlocked = true; break;
            default: return false;
        }

        SaveSystem.Save(Data);
        return true;
    }

    // Lu par l'écran de fin de partie : renvoie le nom du perso débloqué cette
    // session (une seule fois), null ensuite.
    public string ConsumePendingUnlockNotification()
    {
        string s = PendingUnlockNotification;
        PendingUnlockNotification = null;
        return s;
    }

    // DEBUG - outil de développement, jamais exposé en release (le bouton qui l'appelle est caché par DevOnly.Show,
    // et le corps lui-même est maintenant compilé uniquement hors build release - audit §3 "code mort en release").
    // Débloque tout, utile après avoir ajouté le système de déblocage sur un save existant.
    public void DebugUnlockAllCharacters()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Data == null) LoadData();
        Data.kaelUnlocked = true;
        Data.lyraUnlocked = true;
        SaveSystem.Save(Data);
#endif
    }

    // DEBUG - même traitement que ci-dessus. Remet les déblocages à l'état "première
    // partie" : seul Aether disponible, sélection ramenée sur Aether. Ne touche
    // PAS aux arbres / à l'Or / aux Éclats (resets séparés).
    public void DebugResetCharacterUnlocks()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Data == null) LoadData();
        Data.kaelUnlocked = false;
        Data.lyraUnlocked = false;
        Data.selectedCharacterIndex = 0;
        PendingUnlockNotification = null;
        SaveSystem.Save(Data);
#endif
    }

    public GameObject GetSelectedCharacterPrefab()
    {
        if (_characterPrefabs == null || _characterPrefabs.Length == 0) return null;
        int index = GetSelectedCharacterIndex();
        if (index < 0 || index >= _characterPrefabs.Length) return null;
        return _characterPrefabs[index];
    }

    public SkillTreeData.CharacterBranch GetActiveBranch()
    {
        switch (GetSelectedCharacterIndex())
        {
            case 1: return SkillTreeData.CharacterBranch.Gardien;
            case 2: return SkillTreeData.CharacterBranch.Fantome;
            default: return SkillTreeData.CharacterBranch.Guerrier;
        }
    }

    private bool IsBranchActive(SkillTreeData.CharacterBranch branch)
    {
        return GetActiveBranch() == branch;
    }

    // =====================
    // BONUS ARBRE — GUERRIER
    // =====================

    // Plafond du bonus de dégâts de "Concentration" (branche Guerrier).
    // MODIFIE - 0.25/0.40 -> 0.30/0.50 aux paliers 2/3 : le bonus se réinitialise à
    // chaque coup reçu (fragile en fin de partie), le plafond doit donc être assez
    // gros pour donner envie de jouer proprement. Montée effective : voir
    // GetConcentrationRampPerSecond() ci-dessous.
    public float GetBonusConcentrationCap()
    {
        if (!IsBranchActive(SkillTreeData.CharacterBranch.Guerrier)) return 0f;
        float[] values = { 0f, 0.15f, 0.30f, 0.50f };
        return values[Mathf.Clamp(Data.concentrationLevel, 0, values.Length - 1)];
    }

    // AJOUTE (2026-09-16) - vitesse de montée du bonus de Concentration (%/s),
    // variable selon le palier plutôt qu'un taux plat de 8%/s pour tous - retour
    // utilisateur : "ça doit quand même être une récompense d'avoir des dégâts
    // en plus, pas un passif facile à avoir". Plus le palier est profond, plus
    // la montée est rapide ET le plafond est haut - cohérent avec l'investissement.
    public float GetConcentrationRampPerSecond()
    {
        if (!IsBranchActive(SkillTreeData.CharacterBranch.Guerrier)) return 0f;
        float[] values = { 0f, 0.02f, 0.03f, 0.05f };
        return values[Mathf.Clamp(Data.concentrationLevel, 0, values.Length - 1)];
    }

    public float GetBonusCadence()
    {
        if (!IsBranchActive(SkillTreeData.CharacterBranch.Guerrier)) return 0f;
        float[] values = { 0f, 0.10f, 0.20f, 0.35f };
        return values[Mathf.Clamp(Data.cadenceLevel, 0, values.Length - 1)];
    }

    public float GetBonusCrystalDamage()
    {
        if (!IsBranchActive(SkillTreeData.CharacterBranch.Guerrier)) return 0f;
        float[] values = { 0f, 0.25f, 0.50f, 1.00f };
        return values[Mathf.Clamp(Data.crystalDamageLevel, 0, values.Length - 1)];
    }

    public bool HasFragmentation()
    {
        if (!IsBranchActive(SkillTreeData.CharacterBranch.Guerrier)) return false;
        return Data != null && Data.fragmentationUnlocked;
    }

    // MODIFIÉ (2026-10-27, retour utilisateur : "très peu efficace en jeu") - plus de chance aléatoire (15 %) : un
    // projectile du tir de base sur FragmentationShotInterval explose à coup sûr (voir WeaponBase.FireProjectile).
    public const int FragmentationShotInterval = 4;

    // 0 = nœud non débloqué (aucune explosion).
    public int GetFragmentationInterval()
    {
        return HasFragmentation() ? FragmentationShotInterval : 0;
    }

    public bool HasOverpower()
    {
        if (!IsBranchActive(SkillTreeData.CharacterBranch.Guerrier)) return false;
        return Data != null && Data.overpowerUnlocked;
    }

    // =====================
    // BONUS ARBRE — GARDIEN
    // =====================

    public float GetBonusMaxHP()
    {
        if (!IsBranchActive(SkillTreeData.CharacterBranch.Gardien)) return 0f;
        float[] values = { 0f, 0.15f, 0.30f, 0.50f };
        return values[Mathf.Clamp(Data.vitalityLevel, 0, values.Length - 1)];
    }

    // PV plats restaurés à chaque projectile absorbé (dash) — nœud "Récupération".
    // MODIFIE - 2/5/8 -> 20/50/80 : avec le rescale ×10 des PV (joueur ~2000 PV),
    // 2/5/8 représentait 0,1-0,4 % de la vie, imperceptible en jeu.
    public float GetBonusRecuperation()
    {
        if (!IsBranchActive(SkillTreeData.CharacterBranch.Gardien)) return 0f;
        float[] values = { 0f, 20f, 50f, 80f };
        return values[Mathf.Clamp(Data.recuperationLevel, 0, values.Length - 1)];
    }

    public float GetBonusArmor()
    {
        if (!IsBranchActive(SkillTreeData.CharacterBranch.Gardien)) return 0f;
        float[] values = { 0f, 0.08f, 0.15f, 0.25f };
        return values[Mathf.Clamp(Data.armorLevel, 0, values.Length - 1)];
    }

    public bool HasSecondWind()
    {
        if (!IsBranchActive(SkillTreeData.CharacterBranch.Gardien)) return false;
        return Data != null && Data.secondWindUnlocked;
    }

    public bool HasManaShield()
    {
        if (!IsBranchActive(SkillTreeData.CharacterBranch.Gardien)) return false;
        return Data != null && Data.manaShieldUnlocked;
    }

    // =====================
    // BONUS ARBRE — FANTÔME
    // =====================

    public float GetBonusDashCooldown()
    {
        if (!IsBranchActive(SkillTreeData.CharacterBranch.Fantome)) return 0f;
        float[] values = { 0f, 0.3f, 0.6f, 1.0f };
        return values[Mathf.Clamp(Data.dashLevel, 0, values.Length - 1)];
    }

    public float GetBonusNovaRadius()
    {
        if (!IsBranchActive(SkillTreeData.CharacterBranch.Fantome)) return 0f;
        float[] values = { 0f, 0.30f, 0.60f, 1.00f };
        return values[Mathf.Clamp(Data.novaRadiusLevel, 0, values.Length - 1)];
    }

    public bool HasCrystalMastery()
    {
        if (!IsBranchActive(SkillTreeData.CharacterBranch.Fantome)) return false;
        return Data != null && Data.crystalMasteryUnlocked;
    }

    public bool HasPhantomDash()
    {
        if (!IsBranchActive(SkillTreeData.CharacterBranch.Fantome)) return false;
        return Data != null && Data.phantomDashUnlocked;
    }

    public bool HasImpulsionNova()
    {
        if (!IsBranchActive(SkillTreeData.CharacterBranch.Fantome)) return false;
        return Data != null && Data.impulsionNovaUnlocked;
    }

    // =====================
    // RÉPUTATION — tronc commun, aucun filtre de branche
    // =====================

    // Valeurs de chaque palier (index = niveau, 0 = aucun bonus). Publiques en lecture pour que l'onglet Réputation
    // puisse afficher le bonus du PROCHAIN palier sans dupliquer ces tableaux.
    public const int MaxReputationLevel = 5;
    // MODIFIÉ (2026-10-25, retour utilisateur : "42 % pour un dernier palier, c'est bizarre, c'est pas rond") - paliers
    // arrondis à 5 près, équilibre quasi inchangé : Dégâts 5/12/20/30/42 -> 5/10/20/30/40, Vitesse 5/10/16/23/25 ->
    // 5/10/15/20/25 (Régénération déjà ronde).
    // MODIFIÉ (2026-10-25, retour utilisateur : "il faut regarder si chaque achat procure une vraie avancée") - gain
    // RÉEL de chaque achat (stat totale avant -> après), avec les coûts 100/300/700/1500/3000 :
    //   ancien Dégâts 5/10/20/30/40 : +5 %, +4,8 %, +9,1 %, +8,3 %, +7,7 % -> le 2e achat (300 Or) ne rapportait
    //     presque rien, et les 2 derniers (les plus chers) rapportaient MOINS que le 3e.
    //   ancienne Vitesse 5/10/15/20/25 : +0,25 m/s à chaque achat (~+4 %), sous le seuil de ce qu'on ressent.
    //   ancienne Régénération 10/20/30/50/70 (+ 5 PV/s de base) : correcte, mais palier 3 creux (+10 entre +10 et +20).
    // Nouveaux paliers : chaque achat gagne au moins autant que le précédent (le coût monte toujours), et chaque
    // achat améliore réellement la stat (~+9 à +10 % de dégâts, +0,3 m/s de vitesse, +10 à +20 PV/s). La Vitesse
    // reste plafonnée à +30 % (5 -> 6,5 m/s) : c'est la stat de survie principale du genre, les ennemis courants
    // vont de 1,5 à 4,5 m/s - plus haut, plus rien ne rattraperait le joueur.
    private static readonly float[] ReputationDamageValues = { 0f, 0.10f, 0.20f, 0.30f, 0.45f, 0.60f };
    private static readonly float[] ReputationSpeedValues = { 0f, 0.06f, 0.12f, 0.18f, 0.24f, 0.30f };
    private static readonly float[] ReputationRegenValues = { 0f, 10f, 20f, 35f, 50f, 70f };

    // Bonus d'une stat de Réputation à un niveau donné (0..MaxReputationLevel) : Dégâts / Vitesse en fraction
    // (0,40 = +40 %), Régénération en PV/s. Renvoie 0 pour un identifiant inconnu.
    public float GetReputationValueAt(string nodeId, int level)
    {
        float[] values = null;
        switch (nodeId)
        {
            case "reputationDamage": values = ReputationDamageValues; break;
            case "reputationSpeed": values = ReputationSpeedValues; break;
            case "reputationRegen": values = ReputationRegenValues; break;
        }
        if (values == null) return 0f;
        return values[Mathf.Clamp(level, 0, values.Length - 1)];
    }

    public float GetReputationBonusDamage()
    {
        if (Data == null) return 0f;
        return GetReputationValueAt("reputationDamage", Data.reputationDamageLevel);
    }

    public float GetReputationBonusSpeed()
    {
        if (Data == null) return 0f;
        return GetReputationValueAt("reputationSpeed", Data.reputationSpeedLevel);
    }

    public float GetReputationBonusRegen()
    {
        if (Data == null) return 0f;
        return GetReputationValueAt("reputationRegen", Data.reputationRegenLevel);
    }

    // =====================
    // SKINS (onglet Réputation)
    // =====================

    public enum SkinBuyResult { Ok, Unknown, AlreadyOwned, CharacterLocked, NotEnoughCurrency }

    public bool IsSkinOwned(SkinEntry skin)
    {
        if (skin == null || Data == null) return false;
        return skin.isDefault || Data.ownedSkins.Contains(skin.id);
    }

    // Skin équipé d'un personnage ; la tenue d'origine si rien n'est équipé (ou si le skin n'existe plus / n'est plus possédé).
    public SkinEntry GetEquippedSkin(int characterIndex)
    {
        characterIndex = Mathf.Clamp(characterIndex, 0, SkinCatalog.CharacterCount - 1);
        SkinCatalog catalog = SkinCatalog.Instance;
        if (Data != null && Data.equippedSkins != null && characterIndex < Data.equippedSkins.Length)
        {
            SkinEntry equipped = catalog.Get(Data.equippedSkins[characterIndex]);
            if (equipped != null && equipped.characterIndex == characterIndex && IsSkinOwned(equipped)) return equipped;
        }
        return catalog.DefaultFor(characterIndex);
    }

    public SkinBuyResult TryBuySkin(string skinId)
    {
        SkinEntry skin = SkinCatalog.Instance.Get(skinId);
        if (skin == null || Data == null) return SkinBuyResult.Unknown;
        if (IsSkinOwned(skin)) return SkinBuyResult.AlreadyOwned;
        if (!IsCharacterUnlocked(skin.characterIndex)) return SkinBuyResult.CharacterLocked;

        if (skin.tier == SkinTier.Prestige)
        {
            if (Data.totalEclats < skin.cost) return SkinBuyResult.NotEnoughCurrency;
            Data.totalEclats -= skin.cost;
        }
        else
        {
            if (Data.totalGold < skin.cost) return SkinBuyResult.NotEnoughCurrency;
            Data.totalGold -= skin.cost;
        }
        Data.ownedSkins.Add(skin.id);
        SaveSystem.Save(Data);
        return SkinBuyResult.Ok;
    }

    public bool EquipSkin(string skinId)
    {
        SkinEntry skin = SkinCatalog.Instance.Get(skinId);
        if (skin == null || Data == null || !IsSkinOwned(skin)) return false;
        if (!IsCharacterUnlocked(skin.characterIndex)) return false;
        if (Data.equippedSkins == null || Data.equippedSkins.Length < SkinCatalog.CharacterCount)
        {
            string[] grown = new string[SkinCatalog.CharacterCount];
            if (Data.equippedSkins != null) System.Array.Copy(Data.equippedSkins, grown, Data.equippedSkins.Length);
            Data.equippedSkins = grown;
        }
        Data.equippedSkins[skin.characterIndex] = skin.isDefault ? "" : skin.id;
        SaveSystem.Save(Data);
        return true;
    }

    // =====================
    // ACHAT DES NOEUDS
    // =====================

    private bool IsReputationNode(string nodeId)
    {
        return nodeId == "reputationDamage" || nodeId == "reputationSpeed" || nodeId == "reputationRegen";
    }

    public bool TryBuyNode(string nodeId)
    {
        if (Data == null) return false;
        int cost = GetNodeCost(nodeId);
        if (cost == -1) return false;
        if (!IsNodeUnlockable(nodeId)) return false;

        if (IsReputationNode(nodeId))
        {
            if (Data.totalEclats < cost) return false;
            Data.totalEclats -= cost;
        }
        else
        {
            if (Data.totalGold < cost) return false;
            Data.totalGold -= cost;
        }

        ApplyNodePurchase(nodeId);
        SaveSystem.Save(Data);
        return true;
    }

    // MODIFIÉ (2026-10-27, refonte de l'économie) - les coûts de l'arbre ne sont plus dupliqués ici : seule source,
    // SkillTreeData (costLevel1/2/3, déjà affichés par la fiche de détail). -1 = plus rien à acheter.
    public int GetNodeCost(string nodeId)
    {
        if (Data == null) return -1;

        if (IsReputationNode(nodeId)) return GetReputationLevelCost(GetNodeLevel(nodeId));

        SkillTreeData.NodeData node = SkillTreeData.Get(nodeId);
        if (node == null) return -1;
        if (node.isUnique) return IsNodePurchased(nodeId) ? -1 : node.costLevel1;

        switch (GetNodeLevel(nodeId))
        {
            case 0: return node.costLevel1;
            case 1: return node.costLevel2;
            case 2: return node.costLevel3;
            default: return -1;
        }
    }

    // MODIFIÉ (2026-10-27, refonte de l'économie) - 100/300/700/1 500/3 000 -> 200/500/1 000/2 000/3 500 (7 200 éclats par
    // bonus, 21 600 pour les 3) : avec ~100 à 200 éclats par victoire et 5 700 de primes de première victoire, toute la
    // Réputation demande ~150 parties - l'objectif de très long terme, commun aux 3 personnages.
    private int GetReputationLevelCost(int currentLevel)
    {
        int[] costs = { 200, 500, 1000, 2000, 3500 };
        if (currentLevel >= costs.Length) return -1;
        return costs[currentLevel];
    }

    public bool IsNodeUnlockable(string nodeId)
    {
        if (Data == null) return false;
        // AJOUTÉ (2026-10-27) - nœuds du milieu : une victoire en Modéré, nœud du haut : une victoire en Violent, avec le
        // personnage de la branche (SkillTreeData.requiredWinTier).
        if (!IsNodeWinRequirementMet(nodeId)) return false;

        switch (nodeId)
        {
            case "concentration": return Data.concentrationLevel < 3;
            case "cadence": return Data.cadenceLevel < 3;
            case "crystalDamage": return Data.cadenceLevel >= 1 && Data.crystalDamageLevel < 3;
            case "fragmentation": return Data.concentrationLevel >= 1 && !Data.fragmentationUnlocked;
            case "overpower": return Data.fragmentationUnlocked && Data.crystalDamageLevel >= 1 && !Data.overpowerUnlocked;
            case "vitality": return Data.vitalityLevel < 3;
            case "recuperation": return Data.recuperationLevel < 3;
            case "armor": return Data.recuperationLevel >= 1 && Data.armorLevel < 3;
            case "secondWind": return Data.vitalityLevel >= 1 && !Data.secondWindUnlocked;
            case "manaShield": return Data.secondWindUnlocked && Data.armorLevel >= 1 && !Data.manaShieldUnlocked;
            case "impulsionNova": return !Data.impulsionNovaUnlocked;
            case "dash": return Data.dashLevel < 3;
            case "novaRadius": return Data.dashLevel >= 1 && Data.novaRadiusLevel < 3;
            case "crystalMastery": return Data.impulsionNovaUnlocked && !Data.crystalMasteryUnlocked;
            case "phantomDash": return Data.crystalMasteryUnlocked && Data.novaRadiusLevel >= 1 && !Data.phantomDashUnlocked;
            case "reputationDamage": return Data.reputationDamageLevel < 5;
            case "reputationSpeed": return Data.reputationSpeedLevel < 5;
            case "reputationRegen": return Data.reputationRegenLevel < 5;
            default: return false;
        }
    }

    private void ApplyNodePurchase(string nodeId)
    {
        switch (nodeId)
        {
            case "concentration": Data.concentrationLevel++; break;
            case "cadence": Data.cadenceLevel++; break;
            case "crystalDamage": Data.crystalDamageLevel++; break;
            case "fragmentation": Data.fragmentationUnlocked = true; break;
            case "overpower": Data.overpowerUnlocked = true; break;
            case "vitality": Data.vitalityLevel++; break;
            case "recuperation": Data.recuperationLevel++; break;
            case "armor": Data.armorLevel++; break;
            case "secondWind": Data.secondWindUnlocked = true; break;
            case "manaShield": Data.manaShieldUnlocked = true; break;
            case "impulsionNova": Data.impulsionNovaUnlocked = true; break;
            case "dash": Data.dashLevel++; break;
            case "novaRadius": Data.novaRadiusLevel++; break;
            case "crystalMastery": Data.crystalMasteryUnlocked = true; break;
            case "phantomDash": Data.phantomDashUnlocked = true; break;
            case "reputationDamage": Data.reputationDamageLevel++; break;
            case "reputationSpeed": Data.reputationSpeedLevel++; break;
            case "reputationRegen": Data.reputationRegenLevel++; break;
        }
    }

    public int GetNodeLevel(string nodeId)
    {
        if (Data == null) return 0;

        switch (nodeId)
        {
            case "concentration": return Data.concentrationLevel;
            case "cadence": return Data.cadenceLevel;
            case "crystalDamage": return Data.crystalDamageLevel;
            case "vitality": return Data.vitalityLevel;
            case "recuperation": return Data.recuperationLevel;
            case "armor": return Data.armorLevel;
            case "dash": return Data.dashLevel;
            case "novaRadius": return Data.novaRadiusLevel;
            case "reputationDamage": return Data.reputationDamageLevel;
            case "reputationSpeed": return Data.reputationSpeedLevel;
            case "reputationRegen": return Data.reputationRegenLevel;
            default: return 0;
        }
    }

    public bool IsNodePurchased(string nodeId)
    {
        if (Data == null) return false;

        switch (nodeId)
        {
            case "fragmentation": return Data.fragmentationUnlocked;
            case "overpower": return Data.overpowerUnlocked;
            case "secondWind": return Data.secondWindUnlocked;
            case "manaShield": return Data.manaShieldUnlocked;
            case "impulsionNova": return Data.impulsionNovaUnlocked;
            case "crystalMastery": return Data.crystalMasteryUnlocked;
            case "phantomDash": return Data.phantomDashUnlocked;
            default: return false;
        }
    }

    public void ResetSkillTree()
    {
        if (Data == null) Data = new SaveData();

        Data.concentrationLevel = 0;
        Data.cadenceLevel = 0;
        Data.crystalDamageLevel = 0;
        Data.fragmentationUnlocked = false;
        Data.overpowerUnlocked = false;
        Data.vitalityLevel = 0;
        Data.recuperationLevel = 0;
        Data.armorLevel = 0;
        Data.secondWindUnlocked = false;
        Data.manaShieldUnlocked = false;
        Data.impulsionNovaUnlocked = false;
        Data.dashLevel = 0;
        Data.novaRadiusLevel = 0;
        Data.crystalMasteryUnlocked = false;
        Data.phantomDashUnlocked = false;

        Data.totalGold = 100000; // DEBUG : de quoi tout acheter (~91 750 pour les 3 branches depuis la refonte du 2026-10-27) ; remettre à 0 pour la release

        SaveSystem.Save(Data);
    }

    // DEBUG - outil de développement, jamais exposé en release (voir DebugUnlockAllCharacters ci-dessus).
    public void DebugResetReputation()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Data == null) LoadData();
        Data.reputationDamageLevel = 0;
        Data.reputationSpeedLevel = 0;
        Data.reputationRegenLevel = 0;
        Data.totalEclats = 20000;
        SaveSystem.Save(Data);
#endif
    }

    // AJOUTE - cherche, parmi les noeuds de la branche du personnage actif (Or)
    // et les 3 noeuds de Reputation (Eclats), celui qui necessite le MOINS de
    // monnaie manquante pour etre debloque - tous types de monnaie confondus.
    // Sert au message "encore X pour debloquer..." de l'ecran de Game Over,
    // pense pour retourner le regard du joueur vers l'avant plutot que vers
    // l'echec qu'il vient de vivre.
    // MODIFIE - comparait auparavant un "manque d'Or" et un "manque d'Éclats"
    // sur la même échelle absolue (déjà signalé dans NOTES.md) : un nœud à
    // "50 Éclats manquants" gagnait toujours contre un nœud à "2000 Or
    // manquants" alors que les Éclats sont bien plus rares/lents à gagner
    // (~200-500 par run, contre des milliers d'Or) - le nœud "le plus proche"
    // affiché n'était donc pas forcément le plus proche en pratique. Compare
    // désormais une PROPORTION (part déjà payée du coût, 0 à 1), indépendante
    // de la devise : le nœud choisi est celui dont il manque le moins *en
    // pourcentage de son coût total*, peu importe si ce coût est en Or ou en
    // Éclats.
    public NextUnlockPreview GetNextUnlockPreview()
    {
        NextUnlockPreview best = new NextUnlockPreview { HasPreview = false };
        if (Data == null) return best;

        float bestRatio = -1f;
        SkillTreeData.CharacterBranch activeBranch = GetActiveBranch();

        foreach (SkillTreeData.NodeData node in SkillTreeData.All)
        {
            if (node.branch != activeBranch) continue;
            if (!IsNodeUnlockable(node.id)) continue;

            int cost = GetNodeCost(node.id);
            if (cost <= 0) continue;

            float ratio = Mathf.Clamp01((float)Data.totalGold / cost);
            if (ratio > bestRatio)
            {
                bestRatio = ratio;
                best = new NextUnlockPreview
                {
                    HasPreview = true,
                    NodeName = node.displayName,
                    Cost = cost,
                    CurrentAmount = Data.totalGold,
                    IsGoldCurrency = true
                };
            }
        }

        // Les 3 noeuds de Reputation ne vivent pas dans SkillTreeData (ils sont
        // geres entierement ici, cf. IsReputationNode) - traites a part, avec
        // des noms d'affichage a confirmer si ReputationUI.cs utilise deja un
        // intitule etabli different de celui-ci.
        string[] reputationIds = { "reputationDamage", "reputationSpeed", "reputationRegen" };
        string[] reputationNames = { "Réputation : Dégâts", "Réputation : Vitesse", "Réputation : Régénération" };

        for (int i = 0; i < reputationIds.Length; i++)
        {
            string id = reputationIds[i];
            if (!IsNodeUnlockable(id)) continue;

            int cost = GetNodeCost(id);
            if (cost <= 0) continue;

            float ratio = Mathf.Clamp01((float)Data.totalEclats / cost);
            if (ratio > bestRatio)
            {
                bestRatio = ratio;
                best = new NextUnlockPreview
                {
                    HasPreview = true,
                    NodeName = reputationNames[i],
                    Cost = cost,
                    CurrentAmount = Data.totalEclats,
                    IsGoldCurrency = false
                };
            }
        }

        return best;
    }
}