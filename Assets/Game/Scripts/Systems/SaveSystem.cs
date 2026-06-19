using System.Collections.Generic;
using System.IO;
using UnityEngine;

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
            return new SaveData();
        string json = File.ReadAllText(_savePath);
        return JsonUtility.FromJson<SaveData>(json);
    }
}

[System.Serializable]
public class SaveData
{
    // Statistiques
    public int totalGold = 0;
    public int totalRuns = 0;
    public float bestTime = 0f;
    public int bestKills = 0;

    // Branche Guerrier
    public int damageLevel = 0;
    public int cadenceLevel = 0;
    public int crystalDamageLevel = 0;
    public bool fragmentationUnlocked = false;
    public bool overpowerUnlocked = false;

    // Branche Gardien
    public int vitalityLevel = 0;
    public int regenLevel = 0;
    public int armorLevel = 0;
    public bool secondWindUnlocked = false;
    public bool manaShieldUnlocked = false;

    // Branche Fantôme
    public int agilityLevel = 0;
    public int dashLevel = 0;
    public int novaRadiusLevel = 0;
    public bool crystalMasteryUnlocked = false;
    public bool phantomDashUnlocked = false;

    // ← Dictionary et HashSet supprimés : pas sérialisables en JSON
}