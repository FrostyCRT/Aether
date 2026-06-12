using UnityEngine;

public class MetaProgressionManager : MonoBehaviour
{
    public static MetaProgressionManager Instance { get; private set; }

    public SaveData Data     { get; private set; }
    public int      RunGold  { get; private set; } = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
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

        if (runTime > Data.bestTime)  Data.bestTime  = runTime;
        if (kills   > Data.bestKills) Data.bestKills = kills;

        SaveSystem.Save(Data);
    }

    public float GetBonusMaxHP()  => Data.hpUpgradeLevel    * 0.1f;
    public float GetBonusDamage() => Data.damageUpgradeLevel * 0.05f;
    public float GetBonusXP()     => Data.xpUpgradeLevel     * 0.1f;

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