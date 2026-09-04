using UnityEngine;

public class MetaProgressionManager : MonoBehaviour
{
    public static MetaProgressionManager Instance { get; private set; }

    public SaveData Data { get; private set; }
    public int RunGold { get; private set; } = 0;

    [Header("Personnages jouables")]
    [SerializeField] private GameObject[] _characterPrefabs;

    // AJOUTE - calcul des Eclats gagnes en fin de run, independant de l'or
    // ramasse. Valeurs de depart calibrees pour ~35-40 runs solides afin de
    // maxer completement les 3 stats de Reputation (5 paliers chacune, cout
    // total 5600 x 3 = 16800 Eclats) - a ajuster une fois de vraies donnees de
    // partie disponibles (niveau moyen atteint, frequence des boss vaincus).
    [Header("Eclats (calcul de fin de run)")]
    [SerializeField] private int _eclatsPerLevel = 15;
    [SerializeField] private int _eclatsPerBossKill = 60;
    [SerializeField] private int _eclatsVictoryBonus = 200;

    public int TotalEclats => Data?.totalEclats ?? 0;

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
    }

    // MODIFIE - signature enrichie : level/bossKills/victory servent uniquement
    // au calcul des Eclats (voir CalculateEclatsEarned), separe de l'or (qui
    // continue de financer les arbres de competence par personnage, logique
    // inchangee ci-dessous).
    public void SaveRunResults(float runTime, int kills, int levelReached, int bossKills, bool victory)
    {
        if (Data == null) LoadData();
        Data.totalRuns++;
        Data.totalGold += RunGold;
        if (runTime > Data.bestTime) Data.bestTime = runTime;
        if (kills > Data.bestKills) Data.bestKills = kills;

        int eclatsEarned = CalculateEclatsEarned(levelReached, bossKills, victory);
        Data.totalEclats += eclatsEarned;

        SaveSystem.Save(Data);
        RunGold = 0;
    }

    // AJOUTE - formule des Eclats : niveau atteint + boss vaincus + bonus de
    // victoire. Volontairement independante de l'or/kills, pour recompenser la
    // PERFORMANCE de la run plutot que la collecte.
    private int CalculateEclatsEarned(int levelReached, int bossKills, bool victory)
    {
        int total = (levelReached * _eclatsPerLevel) + (bossKills * _eclatsPerBossKill);
        if (victory) total += _eclatsVictoryBonus;
        return total;
    }

    // =====================
    // PERSONNAGE SÉLECTIONNÉ
    // =====================

    public int GetSelectedCharacterIndex() => Data?.selectedCharacterIndex ?? 0;

    public void SetSelectedCharacter(int index)
    {
        if (Data == null) return;
        Data.selectedCharacterIndex = Mathf.Clamp(index, 0, 2);
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

    public float GetBonusConcentrationCap()
    {
        if (!IsBranchActive(SkillTreeData.CharacterBranch.Guerrier)) return 0f;
        float[] values = { 0f, 0.15f, 0.25f, 0.40f };
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

    public float GetBonusRecuperation()
    {
        if (!IsBranchActive(SkillTreeData.CharacterBranch.Gardien)) return 0f;
        float[] values = { 0f, 2f, 5f, 8f };
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
    // MODIFIE - Vitesse plafonnee a +25% au lieu de +30% au palier 5 : seul
    // stat de Reputation sans "plafond naturel" contrairement aux degats
    // (butent vite sur "l'ennemi meurt deja en 1 coup"), donc traite avec plus
    // de prudence a l'approche du palier maximal.

    public float GetReputationBonusDamage()
    {
        if (Data == null) return 0f;
        float[] values = { 0f, 0.05f, 0.12f, 0.20f, 0.30f, 0.42f };
        return values[Mathf.Clamp(Data.reputationDamageLevel, 0, values.Length - 1)];
    }

    public float GetReputationBonusSpeed()
    {
        if (Data == null) return 0f;
        float[] values = { 0f, 0.05f, 0.10f, 0.16f, 0.23f, 0.25f };
        return values[Mathf.Clamp(Data.reputationSpeedLevel, 0, values.Length - 1)];
    }

    // MODIFIE - x10, cf. rescale global des degats/PV. Contrairement a
    // Degats/Vitesse (des pourcentages, valides peu importe l'echelle des
    // nombres de base), la Regen est un montant FIXE en PV/sec - elle doit
    // suivre le rescale explicitement.
    public float GetReputationBonusRegen()
    {
        if (Data == null) return 0f;
        float[] values = { 0f, 10f, 20f, 30f, 50f, 70f };
        return values[Mathf.Clamp(Data.reputationRegenLevel, 0, values.Length - 1)];
    }

    // =====================
    // ACHAT DES NOEUDS
    // =====================

    // AJOUTE - les 3 noeuds de Reputation payent en Eclats (Data.totalEclats),
    // tous les autres noeuds (arbres de personnage) continuent de payer en Or
    // (Data.totalGold). Centralise ici pour eviter de dupliquer cette liste a
    // plusieurs endroits.
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
            // MODIFIE - les 3 noeuds de Reputation utilisent desormais leur propre
            // table de couts a 5 paliers (GetReputationLevelCost), plus l'ancienne
            // table a 3 paliers (GetLevelCost) qui bloquait silencieusement tout
            // achat au-dela du palier 3 (cout -1 des que currentLevel >= 3).
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

    // AJOUTE - table de couts dediee a la Reputation, 5 paliers au lieu de 3.
    // Progression volontairement plus agressive que la table generique (x2 a x3
    // par palier comme avant, mais etendue) pour que les 2 derniers paliers
    // restent un vrai objectif de fin de progression, pas un a-cote acquis sans
    // y penser.
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

        // NOTE (non modifiee) - reste un artefact de test a corriger avant la
        // release, deja signale precedemment : remettre a 0 (ou retirer cette
        // ligne) avant de sortir le jeu. Volontairement pas touche ici, ce
        // n'est pas dans le perimetre de la Reputation.
        Data.totalGold = 10000; // remettre à 0 pour la release

        // NOTE - ResetSkillTree() ne touche NI la Reputation NI les Eclats,
        // volontairement : ce sont deux systemes de progression distincts des
        // arbres de personnage, un reset de branche ne doit pas faire perdre
        // une progression de long terme separee.

        SaveSystem.Save(Data);
    }

    // AJOUTE - bouton reserve au developpement/debug, PAS destine a la version
    // finale (demande explicitement comme outil de test). Remet les 3 stats de
    // Reputation a 0 et donne largement de quoi tout re-maxer immediatement,
    // pour tester des valeurs sans devoir enchainer des dizaines de vraies runs.
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
}