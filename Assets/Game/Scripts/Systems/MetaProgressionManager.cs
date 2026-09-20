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

    [Header("Eclats (calcul de fin de run)")]
    [SerializeField] private int _eclatsPerLevel = 15;
    [SerializeField] private int _eclatsPerBossKill = 60;
    [SerializeField] private int _eclatsVictoryBonus = 200;

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
    }

    public void AddRunGold(int amount)
    {
        RunGold += amount;
        if (GameUI.Instance != null && GameUI.Instance.gameObject.activeInHierarchy)
            GameUI.Instance.UpdateGold(RunGold);

        // AJOUTE - rafraichit la progression du defi "Fortune" (3000 Or) en
        // temps reel, pas seulement en fin de run.
        if (ChallengeManager.Instance != null)
            ChallengeManager.Instance.RefreshDisplay();
    }

    public void SaveRunResults(float runTime, int kills, int levelReached, int bossKills, bool victory)
    {
        if (Data == null) LoadData();
        Data.totalRuns++;
        Data.totalGold += RunGold;
        if (runTime > Data.bestTime) Data.bestTime = runTime;
        if (kills > Data.bestKills) Data.bestKills = kills;
        // AJOUTE - 2 records supplementaires. RunGold est lu AVANT d'etre remis a
        // 0 plus bas, et reflete deja le bonus de defi eventuel (deja applique
        // par ChallengeManager avant l'appel a SaveRunResults) - donc "meilleur
        // or en une partie" inclut logiquement le bonus, comme le montant reel
        // que le joueur repart avec.
        if (levelReached > Data.bestLevel) Data.bestLevel = levelReached;
        if (RunGold > Data.bestGoldInRun) Data.bestGoldInRun = RunGold;

        RecordCharacterRun(runTime, victory);

        int eclatsEarned = CalculateEclatsEarned(levelReached, bossKills, victory);
        Data.totalEclats += eclatsEarned;
        LastRunEclatsEarned = eclatsEarned;

        SaveSystem.Save(Data);
        RunGold = 0;

        // Déblocage de Lyra : gagner une partie complète (Boss 3). Idempotent,
        // se sauvegarde lui-même si c'est un nouveau déblocage.
        if (victory)
            UnlockCharacter(2);
    }

    // =====================
    // PARCOURS PAR PERSONNAGE (fiche de l'onglet Personnage)
    // =====================

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
    private void RecordCharacterRun(float runTime, bool victory)
    {
        EnsureCharacterStatArrays();
        int c = Mathf.Clamp(GetSelectedCharacterIndex(), 0, 2);
        Data.runsByCharacter[c]++;
        if (victory) Data.winsByCharacter[c]++;
        if (runTime > Data.bestTimeByCharacter[c]) Data.bestTimeByCharacter[c] = runTime;
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

    private int CalculateEclatsEarned(int levelReached, int bossKills, bool victory)
    {
        int total = (levelReached * _eclatsPerLevel) + (bossKills * _eclatsPerBossKill);
        if (victory) total += _eclatsVictoryBonus;
        return total;
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

    // DEBUG - à retirer avant release (cf. NOTES.md). Débloque tout, utile après
    // avoir ajouté le système de déblocage sur un save existant.
    public void DebugUnlockAllCharacters()
    {
        if (Data == null) LoadData();
        Data.kaelUnlocked = true;
        Data.lyraUnlocked = true;
        SaveSystem.Save(Data);
    }

    // DEBUG - à retirer avant release. Remet les déblocages à l'état "première
    // partie" : seul Aether disponible, sélection ramenée sur Aether. Ne touche
    // PAS aux arbres / à l'Or / aux Éclats (resets séparés).
    public void DebugResetCharacterUnlocks()
    {
        if (Data == null) LoadData();
        Data.kaelUnlocked = false;
        Data.lyraUnlocked = false;
        Data.selectedCharacterIndex = 0;
        PendingUnlockNotification = null;
        SaveSystem.Save(Data);
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

    [Tooltip("Chance de fragmentation accordée par le nœud meta, s'additionne à celle du palier 3 de la carte Boule de Feu.")]
    [SerializeField] private float _fragmentationNodeChance = 0.15f;

    public float GetFragmentationChance()
    {
        return HasFragmentation() ? _fragmentationNodeChance : 0f;
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
    private static readonly float[] ReputationDamageValues = { 0f, 0.05f, 0.12f, 0.20f, 0.30f, 0.42f };
    private static readonly float[] ReputationSpeedValues = { 0f, 0.05f, 0.10f, 0.16f, 0.23f, 0.25f };
    private static readonly float[] ReputationRegenValues = { 0f, 10f, 20f, 30f, 50f, 70f };

    // Bonus d'une stat de Réputation à un niveau donné (0..MaxReputationLevel) : Dégâts / Vitesse en fraction
    // (0,42 = +42 %), Régénération en PV/s. Renvoie 0 pour un identifiant inconnu.
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

    public int GetNodeCost(string nodeId)
    {
        if (Data == null) return -1;

        switch (nodeId)
        {
            case "concentration": return GetLevelCost(Data.concentrationLevel);
            case "cadence": return GetLevelCost(Data.cadenceLevel);
            case "crystalDamage": return GetLevelCost(Data.crystalDamageLevel);
            case "fragmentation": return Data.fragmentationUnlocked ? -1 : 500;
            case "overpower": return Data.overpowerUnlocked ? -1 : 1000;
            case "vitality": return GetLevelCost(Data.vitalityLevel);
            case "recuperation": return GetLevelCost(Data.recuperationLevel);
            case "armor": return GetLevelCost(Data.armorLevel);
            case "secondWind": return Data.secondWindUnlocked ? -1 : 500;
            case "manaShield": return Data.manaShieldUnlocked ? -1 : 900;
            case "impulsionNova": return Data.impulsionNovaUnlocked ? -1 : 500;
            case "dash": return GetLevelCost(Data.dashLevel);
            case "novaRadius": return GetLevelCost(Data.novaRadiusLevel);
            case "crystalMastery": return Data.crystalMasteryUnlocked ? -1 : 600;
            case "phantomDash": return Data.phantomDashUnlocked ? -1 : 1200;
            case "reputationDamage": return GetReputationLevelCost(Data.reputationDamageLevel);
            case "reputationSpeed": return GetReputationLevelCost(Data.reputationSpeedLevel);
            case "reputationRegen": return GetReputationLevelCost(Data.reputationRegenLevel);
            default: return -1;
        }
    }

    private int GetLevelCost(int currentLevel)
    {
        int[] costs = { 100, 300, 700 };
        if (currentLevel >= costs.Length) return -1;
        return costs[currentLevel];
    }

    private int GetReputationLevelCost(int currentLevel)
    {
        int[] costs = { 100, 300, 700, 1500, 3000 };
        if (currentLevel >= costs.Length) return -1;
        return costs[currentLevel];
    }

    public bool IsNodeUnlockable(string nodeId)
    {
        if (Data == null) return false;

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

        Data.totalGold = 50000; // DEBUG : 50000 = de quoi tout acheter (~15 100 pour les 3 branches) avec de la marge ; remettre à 0 pour la release

        SaveSystem.Save(Data);
    }

    public void DebugResetReputation()
    {
        if (Data == null) LoadData();
        Data.reputationDamageLevel = 0;
        Data.reputationSpeedLevel = 0;
        Data.reputationRegenLevel = 0;
        Data.totalEclats = 20000;
        SaveSystem.Save(Data);
    }

    public float GetBonusXP() => 0f;

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