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

    // AJOUTÉ (2026-10-18, diagnostic complet - audit §6) - numéro de version du format de sauvegarde, pour pouvoir
    // migrer proprement un jour sans deviner si un save vient d'avant ou après un changement de structure.
    public const int CurrentVersion = 1;

    public static void Save(SaveData data)
    {
        if (SuppressWrites) return;
        try
        {
            data.version = CurrentVersion;
            string json = JsonUtility.ToJson(data, true);
            string tempPath = _savePath + ".tmp";
            File.WriteAllText(tempPath, json);

            // CORRIGÉ (2026-10-18, diagnostic complet - audit §6) - écriture ATOMIQUE : avant, File.WriteAllText
            // écrivait DIRECTEMENT sur save.json ; une coupure de courant pile à ce moment-là laissait un fichier
            // tronqué (JSON invalide) - récupérable via le backup, mais save.json restait cassé jusqu'à la
            // sauvegarde suivante. File.Replace() écrit d'abord sur un fichier à part, puis ne remplace save.json
            // (et ne met à jour save.bak avec l'ancien contenu) qu'en UNE SEULE opération atomique du système de
            // fichiers : plus aucune fenêtre où save.json peut se retrouver à moitié écrit.
            if (File.Exists(_savePath))
                File.Replace(tempPath, _savePath, _backupPath);
            else
                File.Move(tempPath, _savePath);
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
        // CORRIGÉ (2026-10-18, diagnostic complet - audit §6) - avant, un save ET un backup tous les deux illisibles
        // faisaient directement repartir sur une SaveData vierge, qui écrasait le fichier corrompu dès la prochaine
        // sauvegarde automatique - aucune chance de l'inspecter après coup. Copié en save.corrupt d'abord.
        try
        {
            if (File.Exists(_savePath))
                File.Copy(_savePath, Application.persistentDataPath + "/save.corrupt", true);
        }
        catch (Exception e)
        {
            Debug.LogError($"Impossible de conserver le fichier corrompu : {e.Message}");
        }
        Debug.LogError("Création d'une nouvelle sauvegarde vierge.");
        return new SaveData();
    }
}
[System.Serializable]
public class SaveData
{
    // AJOUTÉ (2026-10-18, diagnostic complet - audit §6) - permet de savoir, à la lecture, si un save vient d'avant
    // ou après un futur changement de structure (migration). 0 = sauvegarde créée avant l'ajout de ce champ.
    public int version = 0;

    // Statistiques
    public int totalGold = 0;
    public int totalRuns = 0;
    public float bestTime = 0f;
    // AJOUTÉ (2026-10-20, carrousel de difficulté - "il faut aussi adapter le 'meilleur temps' par difficulté,
    // parce que c'est pas le même pour tous") - bestTime ci-dessus devient le record du palier MODÉRÉ uniquement
    // (migration dans MetaProgressionManager.EnsureBestTimeByTierArray, même technique que bestTimeByCharacter) ;
    // Violent/Apocalyptique démarrent à 0 pour chaque joueur. Null sur une sauvegarde pré-existante (JsonUtility) -
    // toujours passer par EnsureBestTimeByTierArray()/MetaProgressionManager.GetBestTime(), jamais lire ce tableau
    // directement avant qu'il ait été redimensionné.
    public float[] bestTimeByTier;
    public int bestKills = 0;
    // AJOUTE - 2 records supplementaires (le systeme ne couvrait que temps/kills
    // jusqu'ici), pour enrichir les messages de record du Game Over.
    public int bestLevel = 0;
    public int bestGoldInRun = 0;
    // AJOUTE (2026-09-26) - records du MODE SANS FIN, séparés du classique (sinon une seule partie sans fin de 40 min
    // écraserait à jamais les records de la partie de 15 min). Valeur par défaut 0 : anciens saves compatibles.
    public float bestEndlessTime = 0f;
    public int bestEndlessBossKills = 0;
    public int bestEndlessKills = 0;

    // AJOUTE (2026-09-26) - modes de jeu. bestClassicBossKills (0-3) = plus grand nombre de boss battus en une partie
    // CLASSIQUE : donne accès à la Ruée de boss et en fixe l'ampleur (voir GameModes). Les saves d'avant sont migrés
    // dans MetaProgressionManager.LoadData (Lyra débloquée = victoire = 3, Kael débloqué = au moins 1).
    // endlessRuns / rushRuns : compteurs de "Tentative n°X" par mode (le classique = totalRuns - les deux autres).
    public int bestClassicBossKills = 0;
    public int endlessRuns = 0;
    public int rushRuns = 0;
    public int rushWins = 0;
    public int rushBestBossKills = 0;
    // Choc des titans : parties jouées, victoires, meilleur temps de victoire (0 = jamais gagné), max de boss tués.
    public int titansRuns = 0;
    public int titansWins = 0;
    public float titansBestTime = 0f;
    public int titansBestBossKills = 0;
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
    // AJOUTÉ (2026-10-27, refonte de l'économie) - victoires du Classique par personnage ET par palier de difficulté,
    // index = personnage * 3 + palier (DifficultyTier). Sert aux conditions de l'arbre (nœuds du milieu : une victoire
    // en Modéré, nœud du haut : une victoire en Violent) et aux primes d'éclats de première victoire. Null sur une
    // sauvegarde pré-existante : toujours passer par MetaProgressionManager.EnsureWinsByTierArray().
    public int[] winsByCharacterTier;
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