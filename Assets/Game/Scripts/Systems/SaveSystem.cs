using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
public static class SaveSystem
{
    private static string _savePath => Application.persistentDataPath + "/save.json";
    private static string _backupPath => Application.persistentDataPath + "/save.bak";
    // Garde-fou pour les tests en éditeur : à vrai, plus rien n'est écrit sur disque (la vraie sauvegarde du
    // joueur reste intacte, les changements ne vivent qu'en mémoire).
    public static bool SuppressWrites;

    public static void Save(SaveData data)
    {
        if (SuppressWrites) return;
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
    // AJOUTE - 2 records supplementaires (le systeme ne couvrait que temps/kills
    // jusqu'ici), pour enrichir les messages de record du Game Over.
    public int bestLevel = 0;
    public int bestGoldInRun = 0;
    public int selectedCharacterIndex = 0;

    // AJOUTE - déblocage des personnages. Aether est toujours disponible.
    // Kael : atteindre le Boss 2 en une partie (ou 500 Éclats).
    // Lyra : gagner une partie complète, Boss 3 (ou 1200 Éclats).
    // Un ancien save sans ces champs les charge à false : lancer
    // MetaProgressionManager.DebugUnlockAllCharacters() une fois en dev si besoin.
    public bool kaelUnlocked = false;
    public bool lyraUnlocked = false;

    // AJOUTE (2026-09-19) - parcours PAR personnage (index 0 Aether, 1 Kael, 2 Lyra),
    // affiché sur la fiche de l'onglet Personnage. Un ancien save sans ces champs les
    // charge avec ces valeurs par défaut (tableaux de 3 zéros) : le suivi démarre à
    // la première partie jouée après cette mise à jour, l'historique d'avant n'est pas
    // attribuable à un personnage.
    public int[] runsByCharacter = new int[3];
    public int[] winsByCharacter = new int[3];
    public float[] bestTimeByCharacter = new float[3];
    // Branche Guerrier
    public int cadenceLevel = 0;
    public int crystalDamageLevel = 0;
    public bool fragmentationUnlocked = false;
    public bool overpowerUnlocked = false;
    public int concentrationLevel = 0;
    // Branche Gardien
    public int vitalityLevel = 0;
    public int armorLevel = 0;
    public bool secondWindUnlocked = false;
    public bool manaShieldUnlocked = false;
    public int recuperationLevel = 0;
    // Branche Fantôme
    public int dashLevel = 0;
    public int novaRadiusLevel = 0;
    public bool crystalMasteryUnlocked = false;
    public bool phantomDashUnlocked = false;
    public bool impulsionNovaUnlocked = false;
    // Réputation (tronc commun meta, indépendant de l'arbre)
    public int reputationDamageLevel = 0;
    public int reputationSpeedLevel = 0;
    public int reputationRegenLevel = 0;
    public int totalEclats = 0;

    // AJOUTE - systeme de defis horaires. currentChallengeHourBucket identifie
    // une heure reelle precise (UTC) ; tant que ce nombre ne change pas, le
    // defi reste le meme quel que soit le nombre de runs tentees dans l'heure.
    public string currentChallengeId = "";
    public long currentChallengeHourBucket = -1;
    // AJOUTE - empeche de re-toucher le bonus plusieurs fois dans la meme heure
    // en reussissant le meme defi sur plusieurs runs courtes.
    public bool currentChallengeRewardClaimed = false;
    // AJOUTE - historique des 3 derniers defis pour eviter les repetitions,
    // qu'il s'agisse de runs consecutives ou d'heures consecutives.
    public List<string> recentChallengeIds = new List<string>();

    // AJOUTE (2026-09-20) - skins (onglet Réputation). ownedSkins = identifiants du SkinCatalog achetés (la tenue
    // d'origine est toujours possédée, elle n'y figure pas) ; equippedSkins = skin équipé par personnage
    // (index 0 Aether, 1 Kael, 2 Lyra ; vide = tenue d'origine). Un ancien save sans ces champs les charge vides.
    public List<string> ownedSkins = new List<string>();
    public string[] equippedSkins = new string[3];
}