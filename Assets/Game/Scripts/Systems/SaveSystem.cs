using UnityEngine;
using System.IO;

public static class SaveSystem
{
    private static string _savePath => Application.persistentDataPath + "/save.json";

    public static void Save(SaveData data)
    {
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(_savePath, json);
        Debug.Log($"Sauvegarde : {_savePath}");
    }

    public static SaveData Load()
    {
        if (!File.Exists(_savePath))
            return new SaveData(); // Première fois → données vierges

        string json = File.ReadAllText(_savePath);
        return JsonUtility.FromJson<SaveData>(json);
    }
}

[System.Serializable]
public class SaveData
{
    public int   totalGold       = 0;  // Gold disponible pour le shop
    public int   totalRuns       = 0;  // Nombre de parties jouées
    public float bestTime        = 0f; // Meilleur temps de survie
    public int   bestWave        = 0;  // Meilleure vague atteinte
    public int   bestKills       = 0;  // Meilleur nombre de kills
    public int   totalGems       = 0;  // Gemmes disponible pour le shop
    // Upgrades méta achetées
    public int hpUpgradeLevel      = 0;
    public int damageUpgradeLevel  = 0;
    public int xpUpgradeLevel      = 0;
}