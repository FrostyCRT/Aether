using UnityEngine;

public class MetaProgressionManager : MonoBehaviour
{
    public static MetaProgressionManager Instance { get; private set; }

    public SaveData Data { get; private set; }
    public int RunGold { get; private set; } = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Data = SaveSystem.Load();
    }

    public void AddRunGold(int amount)
    {
        RunGold += amount;
        if (GameUI.Instance != null)
            GameUI.Instance.UpdateGold(RunGold);
    }

    public void SaveRunResults(float runTime, int kills)
    {
        Data.totalRuns++;
        Data.totalGold += RunGold;
        if (runTime > Data.bestTime) Data.bestTime = runTime;
        if (kills > Data.bestKills) Data.bestKills = kills;
        SaveSystem.Save(Data);
        RunGold = 0;
    }

    // =====================
    // BONUS APPLIQUÉS EN JEU
    // =====================

    // Branche Guerrier
    public float GetBonusDamage()
    {
        float[] values = { 0f, 0.10f, 0.25f, 0.50f };
        return values[Mathf.Clamp(Data.damageLevel, 0, 3)];
    }

    public float GetBonusCadence()
    {
        float[] values = { 0f, 0.10f, 0.20f, 0.35f };
        return values[Mathf.Clamp(Data.cadenceLevel, 0, 3)];
    }

    public float GetBonusCrystalDamage()
    {
        float[] values = { 0f, 0.25f, 0.50f, 1.00f };
        return values[Mathf.Clamp(Data.crystalDamageLevel, 0, 3)];
    }

    public bool HasFragmentation() => Data.fragmentationUnlocked;
    public bool HasOverpower() => Data.overpowerUnlocked;

    // Branche Gardien
    public float GetBonusMaxHP()
    {
        float[] values = { 0f, 0.15f, 0.30f, 0.50f };
        return values[Mathf.Clamp(Data.vitalityLevel, 0, 3)];
    }

    public float GetBonusRegen()
    {
        float[] values = { 0f, 1f, 2f, 4f };
        return values[Mathf.Clamp(Data.regenLevel, 0, 3)];
    }

    public float GetBonusArmor()
    {
        float[] values = { 0f, 0.08f, 0.15f, 0.25f };
        return values[Mathf.Clamp(Data.armorLevel, 0, 3)];
    }

    public bool HasSecondWind() => Data.secondWindUnlocked;
    public bool HasManaShield() => Data.manaShieldUnlocked;

    // Branche Fantôme
    public float GetBonusAgility()
    {
        float[] values = { 0f, 0.08f, 0.18f, 0.30f };
        return values[Mathf.Clamp(Data.agilityLevel, 0, 3)];
    }

    public float GetBonusDashCooldown()
    {
        float[] values = { 0f, 0.3f, 0.6f, 1.0f };
        return values[Mathf.Clamp(Data.dashLevel, 0, 3)];
    }

    public float GetBonusNovaRadius()
    {
        float[] values = { 0f, 0.30f, 0.60f, 1.00f };
        return values[Mathf.Clamp(Data.novaRadiusLevel, 0, 3)];
    }

    public bool HasCrystalMastery() => Data.crystalMasteryUnlocked;
    public bool HasPhantomDash() => Data.phantomDashUnlocked;

    // =====================
    // ACHAT DES NŒUDS
    // =====================

    public bool TryBuyNode(string nodeId)
    {
        int cost = GetNodeCost(nodeId);
        if (cost == -1) return false;        // Nœud invalide
        if (!IsNodeUnlockable(nodeId)) return false; // Prérequis non remplis
        if (Data.totalGold < cost) return false;     // Pas assez de gold

        Data.totalGold -= cost;
        ApplyNodePurchase(nodeId);
        SaveSystem.Save(Data);
        return true;
    }

    public int GetNodeCost(string nodeId)
    {
        // Nœuds multi-niveaux — coût selon le niveau suivant
        switch (nodeId)
        {
            case "damage": return GetLevelCost(Data.damageLevel);
            case "cadence": return GetLevelCost(Data.cadenceLevel);
            case "crystalDamage": return GetLevelCost(Data.crystalDamageLevel);
            case "vitality": return GetLevelCost(Data.vitalityLevel);
            case "regen": return GetLevelCost(Data.regenLevel);
            case "armor": return GetLevelCost(Data.armorLevel);
            case "agility": return GetLevelCost(Data.agilityLevel);
            case "dash": return GetLevelCost(Data.dashLevel);
            case "novaRadius": return GetLevelCost(Data.novaRadiusLevel);

            // Nœuds uniques
            case "fragmentation": return 500;
            case "overpower": return 1000;
            case "secondWind": return 800;
            case "manaShield": return 900;
            case "crystalMastery": return 600;
            case "phantomDash": return 1200;

            default: return -1;
        }
    }

    private int GetLevelCost(int currentLevel)
    {
        int[] costs = { 100, 300, 700 };
        if (currentLevel >= 3) return -1; // Déjà au max
        return costs[currentLevel];
    }

    public bool IsNodeUnlockable(string nodeId)
    {
        switch (nodeId)
        {
            // ── Toujours disponibles (bas de chaque branche) ──
            case "damage": return Data.damageLevel < 3;
            case "cadence": return Data.cadenceLevel < 3;
            case "vitality": return Data.vitalityLevel < 3;
            case "regen": return Data.regenLevel < 3;
            case "agility": return Data.agilityLevel < 3;
            case "dash": return Data.dashLevel < 3;

            // ── Milieu droite (nécessite bas droite) ──
            case "fragmentation": return Data.damageLevel >= 1 && !Data.fragmentationUnlocked;
            case "secondWind": return Data.vitalityLevel >= 1 && !Data.secondWindUnlocked;
            case "crystalMastery": return Data.agilityLevel >= 1 && !Data.crystalMasteryUnlocked;

            // ── Milieu gauche (nécessite bas gauche) ──
            case "crystalDamage": return Data.cadenceLevel >= 1 && Data.crystalDamageLevel < 3;
            case "armor": return Data.regenLevel >= 1 && Data.armorLevel < 3;
            case "novaRadius": return Data.dashLevel >= 1 && Data.novaRadiusLevel < 3;

            // ── Sommet (nécessite milieu droite ET milieu gauche) ──
            case "overpower": return Data.fragmentationUnlocked && Data.crystalDamageLevel >= 1 && !Data.overpowerUnlocked;
            case "manaShield": return Data.secondWindUnlocked && Data.armorLevel >= 1 && !Data.manaShieldUnlocked;
            case "phantomDash": return Data.crystalMasteryUnlocked && Data.novaRadiusLevel >= 1 && !Data.phantomDashUnlocked;

            default: return false;
        }
    }

    private void ApplyNodePurchase(string nodeId)
    {
        switch (nodeId)
        {
            case "damage": Data.damageLevel++; break;
            case "cadence": Data.cadenceLevel++; break;
            case "crystalDamage": Data.crystalDamageLevel++; break;
            case "fragmentation": Data.fragmentationUnlocked = true; break;
            case "overpower": Data.overpowerUnlocked = true; break;
            case "vitality": Data.vitalityLevel++; break;
            case "regen": Data.regenLevel++; break;
            case "armor": Data.armorLevel++; break;
            case "secondWind": Data.secondWindUnlocked = true; break;
            case "manaShield": Data.manaShieldUnlocked = true; break;
            case "agility": Data.agilityLevel++; break;
            case "dash": Data.dashLevel++; break;
            case "novaRadius": Data.novaRadiusLevel++; break;
            case "crystalMastery": Data.crystalMasteryUnlocked = true; break;
            case "phantomDash": Data.phantomDashUnlocked = true; break;
        }
    }

    // Pour affichage dans le shop
    public int GetNodeLevel(string nodeId)
    {
        switch (nodeId)
        {
            case "damage": return Data.damageLevel;
            case "cadence": return Data.cadenceLevel;
            case "crystalDamage": return Data.crystalDamageLevel;
            case "vitality": return Data.vitalityLevel;
            case "regen": return Data.regenLevel;
            case "armor": return Data.armorLevel;
            case "agility": return Data.agilityLevel;
            case "dash": return Data.dashLevel;
            case "novaRadius": return Data.novaRadiusLevel;
            default: return 0;
        }
    }

    public bool IsNodePurchased(string nodeId)
    {
        switch (nodeId)
        {
            case "fragmentation": return Data.fragmentationUnlocked;
            case "overpower": return Data.overpowerUnlocked;
            case "secondWind": return Data.secondWindUnlocked;
            case "manaShield": return Data.manaShieldUnlocked;
            case "crystalMastery": return Data.crystalMasteryUnlocked;
            case "phantomDash": return Data.phantomDashUnlocked;
            default: return false;
        }
    }

    // Legacy — gardé pour compatibilité
    public float GetBonusXP() => 0f;
    public void ResetSkillTree()
    {
        Data.damageLevel = 0;
        Data.cadenceLevel = 0;
        Data.crystalDamageLevel = 0;
        Data.fragmentationUnlocked = false;
        Data.overpowerUnlocked = false;
        Data.vitalityLevel = 0;
        Data.regenLevel = 0;
        Data.armorLevel = 0;
        Data.secondWindUnlocked = false;
        Data.manaShieldUnlocked = false;
        Data.agilityLevel = 0;
        Data.dashLevel = 0;
        Data.novaRadiusLevel = 0;
        Data.crystalMasteryUnlocked = false;
        Data.phantomDashUnlocked = false;
        Data.totalGold = 9999; // Gold de test
        SaveSystem.Save(Data);
    }

}