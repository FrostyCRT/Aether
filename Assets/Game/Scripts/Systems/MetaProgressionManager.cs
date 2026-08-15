using UnityEngine;

public class MetaProgressionManager : MonoBehaviour
{
    public static MetaProgressionManager Instance { get; private set; }

    public SaveData Data { get; private set; }
    public int RunGold { get; private set; } = 0;

    [Header("Personnages jouables")]
    [SerializeField] private GameObject[] _characterPrefabs;

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

    public void SaveRunResults(float runTime, int kills)
    {
        if (Data == null) LoadData();
        Data.totalRuns++;
        Data.totalGold += RunGold;
        if (runTime > Data.bestTime) Data.bestTime = runTime;
        if (kills > Data.bestKills) Data.bestKills = kills;
        SaveSystem.Save(Data);
        RunGold = 0;
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
            case 1:  return SkillTreeData.CharacterBranch.Gardien;
            case 2:  return SkillTreeData.CharacterBranch.Fantome;
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

    // AJOUTÉ — remplace le bool inerte HasFragmentation() côté gameplay. Jusqu'ici, rien
    // n'appelait HasFragmentation() nulle part : le nœud meta "Fragmentation" était achetable
    // et sauvegardé, mais n'avait aucun effet en jeu. Désormais WeaponFireball combine cette
    // chance avec celle du palier 3 de la carte Boule de Feu (voir UpgradeData.Apply()).
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

    public float GetReputationBonusDamage()
    {
        if (Data == null) return 0f;
        float[] values = { 0f, 0.05f, 0.12f, 0.20f, 0.30f, 0.42f };
        return values[Mathf.Clamp(Data.reputationDamageLevel, 0, values.Length - 1)];
    }

    public float GetReputationBonusSpeed()
    {
        if (Data == null) return 0f;
        float[] values = { 0f, 0.05f, 0.10f, 0.16f, 0.23f, 0.30f };
        return values[Mathf.Clamp(Data.reputationSpeedLevel, 0, values.Length - 1)];
    }

    public float GetReputationBonusRegen()
    {
        if (Data == null) return 0f;
        float[] values = { 0f, 1f, 2f, 3f, 5f, 7f };
        return values[Mathf.Clamp(Data.reputationRegenLevel, 0, values.Length - 1)];
    }

    // =====================
    // ACHAT DES NOEUDS
    // =====================

    public bool TryBuyNode(string nodeId)
    {
        if (Data == null) return false;
        int cost = GetNodeCost(nodeId);
        if (cost == -1) return false;
        if (!IsNodeUnlockable(nodeId)) return false;
        if (Data.totalGold < cost) return false;
        Data.totalGold -= cost;
        ApplyNodePurchase(nodeId);
        SaveSystem.Save(Data);
        return true;
    }

    public int GetNodeCost(string nodeId)
    {
        if (Data == null) return -1;

        switch (nodeId)
        {
            case "concentration":    return GetLevelCost(Data.concentrationLevel);
            case "cadence":          return GetLevelCost(Data.cadenceLevel);
            case "crystalDamage":    return GetLevelCost(Data.crystalDamageLevel);
            case "fragmentation":    return Data.fragmentationUnlocked ? -1 : 500;
            case "overpower":        return Data.overpowerUnlocked ? -1 : 1000;
            case "vitality":         return GetLevelCost(Data.vitalityLevel);
            case "recuperation":     return GetLevelCost(Data.recuperationLevel);
            case "armor":            return GetLevelCost(Data.armorLevel);
            case "secondWind":       return Data.secondWindUnlocked ? -1 : 500;
            case "manaShield":       return Data.manaShieldUnlocked ? -1 : 900;
            case "impulsionNova":    return Data.impulsionNovaUnlocked ? -1 : 500;
            case "dash":             return GetLevelCost(Data.dashLevel);
            case "novaRadius":       return GetLevelCost(Data.novaRadiusLevel);
            case "crystalMastery":   return Data.crystalMasteryUnlocked ? -1 : 600;
            case "phantomDash":      return Data.phantomDashUnlocked ? -1 : 1200;
            case "reputationDamage": return GetLevelCost(Data.reputationDamageLevel);
            case "reputationSpeed":  return GetLevelCost(Data.reputationSpeedLevel);
            case "reputationRegen":  return GetLevelCost(Data.reputationRegenLevel);
            default: return -1;
        }
    }

    private int GetLevelCost(int currentLevel)
    {
        int[] costs = { 100, 300, 700 };
        if (currentLevel >= costs.Length) return -1;
        return costs[currentLevel];
    }

    public bool IsNodeUnlockable(string nodeId)
    {
        if (Data == null) return false;

        switch (nodeId)
        {
            case "concentration":    return Data.concentrationLevel < 3;
            case "cadence":          return Data.cadenceLevel < 3;
            case "crystalDamage":    return Data.cadenceLevel >= 1 && Data.crystalDamageLevel < 3;
            case "fragmentation":    return Data.concentrationLevel >= 1 && !Data.fragmentationUnlocked;
            case "overpower":        return Data.fragmentationUnlocked && Data.crystalDamageLevel >= 1 && !Data.overpowerUnlocked;
            case "vitality":         return Data.vitalityLevel < 3;
            case "recuperation":     return Data.recuperationLevel < 3;
            case "armor":            return Data.recuperationLevel >= 1 && Data.armorLevel < 3;
            case "secondWind":       return Data.vitalityLevel >= 1 && !Data.secondWindUnlocked;
            case "manaShield":       return Data.secondWindUnlocked && Data.armorLevel >= 1 && !Data.manaShieldUnlocked;
            case "impulsionNova":    return !Data.impulsionNovaUnlocked;
            case "dash":             return Data.dashLevel < 3;
            case "novaRadius":       return Data.dashLevel >= 1 && Data.novaRadiusLevel < 3;
            case "crystalMastery":   return Data.impulsionNovaUnlocked && !Data.crystalMasteryUnlocked;
            case "phantomDash":      return Data.crystalMasteryUnlocked && Data.novaRadiusLevel >= 1 && !Data.phantomDashUnlocked;
            case "reputationDamage": return Data.reputationDamageLevel < 5;
            case "reputationSpeed":  return Data.reputationSpeedLevel < 5;
            case "reputationRegen":  return Data.reputationRegenLevel < 5;
            default: return false;
        }
    }

    private void ApplyNodePurchase(string nodeId)
    {
        switch (nodeId)
        {
            case "concentration":    Data.concentrationLevel++; break;
            case "cadence":          Data.cadenceLevel++; break;
            case "crystalDamage":    Data.crystalDamageLevel++; break;
            case "fragmentation":    Data.fragmentationUnlocked = true; break;
            case "overpower":        Data.overpowerUnlocked = true; break;
            case "vitality":         Data.vitalityLevel++; break;
            case "recuperation":     Data.recuperationLevel++; break;
            case "armor":            Data.armorLevel++; break;
            case "secondWind":       Data.secondWindUnlocked = true; break;
            case "manaShield":       Data.manaShieldUnlocked = true; break;
            case "impulsionNova":    Data.impulsionNovaUnlocked = true; break;
            case "dash":             Data.dashLevel++; break;
            case "novaRadius":       Data.novaRadiusLevel++; break;
            case "crystalMastery":   Data.crystalMasteryUnlocked = true; break;
            case "phantomDash":      Data.phantomDashUnlocked = true; break;
            case "reputationDamage": Data.reputationDamageLevel++; break;
            case "reputationSpeed":  Data.reputationSpeedLevel++; break;
            case "reputationRegen":  Data.reputationRegenLevel++; break;
        }
    }

    public int GetNodeLevel(string nodeId)
    {
        if (Data == null) return 0;

        switch (nodeId)
        {
            case "concentration":    return Data.concentrationLevel;
            case "cadence":          return Data.cadenceLevel;
            case "crystalDamage":    return Data.crystalDamageLevel;
            case "vitality":         return Data.vitalityLevel;
            case "recuperation":     return Data.recuperationLevel;
            case "armor":            return Data.armorLevel;
            case "dash":             return Data.dashLevel;
            case "novaRadius":       return Data.novaRadiusLevel;
            case "reputationDamage": return Data.reputationDamageLevel;
            case "reputationSpeed":  return Data.reputationSpeedLevel;
            case "reputationRegen":  return Data.reputationRegenLevel;
            default:                 return 0;
        }
    }

    public bool IsNodePurchased(string nodeId)
    {
        if (Data == null) return false;

        switch (nodeId)
        {
            case "fragmentation":  return Data.fragmentationUnlocked;
            case "overpower":      return Data.overpowerUnlocked;
            case "secondWind":     return Data.secondWindUnlocked;
            case "manaShield":     return Data.manaShieldUnlocked;
            case "impulsionNova":  return Data.impulsionNovaUnlocked;
            case "crystalMastery": return Data.crystalMasteryUnlocked;
            case "phantomDash":    return Data.phantomDashUnlocked;
            default:               return false;
        }
    }

    public void ResetSkillTree()
    {
        if (Data == null) Data = new SaveData();

        Data.concentrationLevel    = 0;
        Data.cadenceLevel          = 0;
        Data.crystalDamageLevel    = 0;
        Data.fragmentationUnlocked = false;
        Data.overpowerUnlocked     = false;
        Data.vitalityLevel         = 0;
        Data.recuperationLevel     = 0;
        Data.armorLevel            = 0;
        Data.secondWindUnlocked    = false;
        Data.manaShieldUnlocked    = false;
        Data.impulsionNovaUnlocked = false;
        Data.dashLevel             = 0;
        Data.novaRadiusLevel       = 0;
        Data.crystalMasteryUnlocked = false;
        Data.phantomDashUnlocked   = false;

        Data.totalGold = 10000; // remettre à 0 pour la release
        SaveSystem.Save(Data);
    }

    public float GetBonusXP() => 0f;
}