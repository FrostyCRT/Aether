using System;
using System.IO;
using UnityEngine;

public static class SaveSystem
{
    private static string _savePath => Application.persistentDataPath + "/save.json";
    private static string _backupPath => Application.persistentDataPath + "/save.bak";

    public static void Save(SaveData data)
    {
        try
        {
            string json = JsonUtility.ToJson(data, true);
            if (File.Exists(_savePath))
                File.Copy(_savePath, _backupPath, true);
            File.WriteAllText(_savePath, json);
            Debug.Log($"Sauvegarde réussie : {_savePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Erreur lors de la sauvegarde : {e.Message}");
        }
    }

    public static SaveData Load()
    {
        if (!File.Exists(_savePath))
        {
            if (File.Exists(_backupPath))
            {
                Debug.LogWarning("Fichier de sauvegarde principal manquant. Restauration du backup...");
                File.Copy(_backupPath, _savePath, true);
            }
            else
            {
                return new SaveData();
            }
        }
        try
        {
            string json = File.ReadAllText(_savePath);
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            if (data == null) return AttemptBackupRecovery();
            return data;
        }
        catch (Exception e)
        {
            Debug.LogError($"Fichier corrompu : {e.Message}");
            return AttemptBackupRecovery();
        }
    }

    private static SaveData AttemptBackupRecovery()
    {
        if (File.Exists(_backupPath))
        {
            try
            {
                string backupJson = File.ReadAllText(_backupPath);
                SaveData backupData = JsonUtility.FromJson<SaveData>(backupJson);
                if (backupData != null)
                {
                    File.Copy(_backupPath, _savePath, true);
                    Debug.Log("Récupération via backup réussie !");
                    return backupData;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Backup corrompu : {e.Message}");
            }
        }
        Debug.LogError("Création d'une nouvelle sauvegarde vierge.");
        return new SaveData();
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

    // AJOUTÉ — Personnage sélectionné (0=Aether, 1=Kael, 2=Lyra), Aether par défaut
    public int selectedCharacterIndex = 0;

    // Branche Guerrier
    public int cadenceLevel = 0;
    public int crystalDamageLevel = 0;
    public bool fragmentationUnlocked = false;
    public bool overpowerUnlocked = false;
    public int concentrationLevel = 0; // AJOUTÉ — remplace damageLevel

    // Branche Gardien
    public int vitalityLevel = 0;
    public int armorLevel = 0;
    public bool secondWindUnlocked = false;
    public bool manaShieldUnlocked = false;
    public int recuperationLevel = 0; // AJOUTÉ — remplace regenLevel

    // Branche Fantôme
    public int dashLevel = 0;
    public int novaRadiusLevel = 0;
    public bool crystalMasteryUnlocked = false;
    public bool phantomDashUnlocked = false;
    public bool impulsionNovaUnlocked = false; // AJOUTÉ — remplace agilityLevel

    // AJOUTÉ — Réputation (tronc commun meta, indépendant de l'arbre)
    public int reputationDamageLevel = 0;   // Dégâts+
    public int reputationSpeedLevel = 0;    // Vitesse+
    public int reputationRegenLevel = 0;    // Regen PV/sec
}