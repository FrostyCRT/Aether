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

            // CORRECTION CRITIQUE : Si une ancienne sauvegarde valide existe, on la garde en backup temporaire
            if (File.Exists(_savePath))
            {
                File.Copy(_savePath, _backupPath, true);
            }

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
        // 1. Si le fichier principal n'existe pas, on tente de restaurer le backup
        if (!File.Exists(_savePath))
        {
            if (File.Exists(_backupPath))
            {
                Debug.LogWarning("Fichier de sauvegarde principal manquant. Restauration du backup...");
                File.Copy(_backupPath, _savePath, true);
            }
            else
            {
                return new SaveData(); // Première partie du joueur
            }
        }

        try
        {
            string json = File.ReadAllText(_savePath);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            // Sécurité si le JSON était vide ou corrompu (renvoie un objet null)
            if (data == null)
            {
                return AttemptBackupRecovery();
            }

            return data;
        }
        catch (Exception e)
        {
            Debug.LogError($"Fichier de sauvegarde principal corrompu : {e.Message}. Tentative de récupération...");
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
                    // On restaure le backup comme fichier principal
                    File.Copy(_backupPath, _savePath, true);
                    Debug.Log("Récupération de la sauvegarde réussie via le fichier Backup !");
                    return backupData;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Le fichier de backup est également corrompu : {e.Message}");
            }
        }

        Debug.LogError("Impossible de charger la progression. Création d'une nouvelle sauvegarde vierge.");
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