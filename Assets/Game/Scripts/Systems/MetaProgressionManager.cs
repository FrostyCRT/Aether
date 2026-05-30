using UnityEngine;

public class MetaProgressionManager : MonoBehaviour
{
    public static MetaProgressionManager Instance { get; private set; }

    // Données sauvegardées
    public SaveData Data { get; private set; }

    // Gold gagné pendant la run actuelle
    public int RunGold { get; private set; } = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Persiste entre les scènes

        Data = SaveSystem.Load();
    }

    public void AddRunGold(int amount)
    {
        RunGold += amount;
        GameUI.Instance.UpdateGold(RunGold);
    }

    public void SaveRunResults(float runTime, int kills, int wave)
    {
        Data.totalRuns++;
        Data.totalGold += RunGold;

        // On met à jour les records
        if (runTime > Data.bestTime)  Data.bestTime  = runTime;
        if (wave    > Data.bestWave)  Data.bestWave  = wave;
        if (kills   > Data.bestKills) Data.bestKills = kills;

        SaveSystem.Save(Data);
    }

    // Bonus permanents appliqués au début de chaque run
    public float GetBonusMaxHP()     => Data.hpUpgradeLevel     * 0.1f;  // +10% par niveau
    public float GetBonusDamage()    => Data.damageUpgradeLevel  * 0.05f; // +5% par niveau
    public float GetBonusXP()        => Data.xpUpgradeLevel      * 0.1f;  // +10% par niveau

    public bool BuyUpgrade(string upgradeType, int cost)
    {
        if (Data.totalGold < cost) return false;

        Data.totalGold -= cost;

        switch (upgradeType)
        {
            case "hp":     Data.hpUpgradeLevel++;     break;
            case "damage": Data.damageUpgradeLevel++;  break;
            case "xp":     Data.xpUpgradeLevel++;      break;
        }

        SaveSystem.Save(Data);
        return true;
    }
}