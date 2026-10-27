using System.Collections.Generic;

public enum ChallengeDifficulty
{
    Easy,
    Medium,
    Hard
}

public class ChallengeDefinition
{
    public string id;
    public string displayName;
    public string description;
    // AJOUTÉ (2026-10-16, retour utilisateur : "pour le défis qui s'affiche au milieu [au lancement], il faut
    // faire des phrases plus descriptives [...] pour comprendre exactement ce qu'il faut faire") - description
    // longue, UNIQUEMENT pour l'annonce de lancement (GameUI.PlayChallengeIntroRoutine), qui a la place pour une
    // vraie phrase. `description` reste la version courte (35 car. max) utilisée dans le menu Pause, à l'espace
    // compté (PauseMenuUI.PullChallengeInfo) - ne jamais l'allonger, ça ferait déborder. Laissé à null quand la
    // version courte était déjà sans ambiguïté (kill150, level10, boss1, dash20, boss2, level20in10min, level30) :
    // LongDescription retombe alors sur Description.
    public string longDescription;
    public ChallengeDifficulty difficulty;

    // AJOUTÉ (2026-10-14) - textes à AFFICHER (langue courante, voir Loc). displayName/description restent la
    // donnée française d'origine (clés de traduction).
    public string Name => Loc.T(displayName);
    public string Description => Loc.T(description);
    public string LongDescription => Loc.T(string.IsNullOrEmpty(longDescription) ? description : longDescription);
}

public static class ChallengeDatabase
{
    private static readonly List<ChallengeDefinition> _all = new List<ChallengeDefinition>
    {
        new ChallengeDefinition { id = "kill150", displayName = "Chasseur", description = "Tuer 150 ennemis", difficulty = ChallengeDifficulty.Easy },
        new ChallengeDefinition { id = "level10", displayName = "Montée en Puissance", description = "Atteindre le niveau 10", difficulty = ChallengeDifficulty.Easy },
        new ChallengeDefinition { id = "boss1", displayName = "Premier Sang", description = "Vaincre 1 boss", difficulty = ChallengeDifficulty.Easy },
        new ChallengeDefinition { id = "dash20", displayName = "Toujours en Mouvement", description = "Utiliser le Dash 20 fois", difficulty = ChallengeDifficulty.Easy },

        // MODIFIE (2026-09-13) - descriptions raccourcies a 35 caracteres max
        // (espaces inclus), retour utilisateur : plusieurs debordaient de
        // l'espace dedie du menu pause (jusqu'a 48 caracteres avant, ex.
        // "Terminer la partie avec au moins 3000 Or ramasse"). Le prefixe
        // "Terminer la partie"/"Atteindre le" est retire quand redondant (une
        // description de defi porte deja implicitement sur la partie entiere).
        new ChallengeDefinition { id = "hp30never", displayName = "Sang-Froid", description = "Garder 30% de vie jusqu'au boss 2", longDescription = "Ne jamais descendre sous 30% de vie, du début de la partie jusqu'à la mort du 2e boss.", difficulty = ChallengeDifficulty.Medium },
        new ChallengeDefinition { id = "boss2", displayName = "Double Chasse", description = "Vaincre 2 boss dans la même partie", difficulty = ChallengeDifficulty.Medium },
        new ChallengeDefinition { id = "level20in10min", displayName = "Ascension Rapide", description = "Niveau 18 avant la 10e minute", difficulty = ChallengeDifficulty.Medium },

        // CORRIGÉ (2026-10-21, retour utilisateur : "le défi sans ult jusqu'au boss 2 est presque impossible, ça
        // veut dire qu'il faut jamais ult pendant 10min c'est énorme") - BossSpawnInterval=300s (voir WaveManager) :
        // le boss 2 apparaît au plus tôt à la 10e minute, donc ce défi impose de tenir TOUT ce temps sans la
        // capacité la plus forte du joueur (dégâts de zone + ralentissement + absorption d'XP), pendant que les
        // ennemis montent déjà en puissance - nettement plus punitif que les autres défis "Medium" au même jalon
        // (ex. "Double Chasse"/"boss2", qui ne retire aucune capacité) et au moins aussi dur que les défis déjà
        // classés "Hard" ci-dessous. Reclassé Medium -> Hard, pas de changement du jalon lui-même (toujours boss 2).
        new ChallengeDefinition { id = "noUltimate", displayName = "Sans Cristal", description = "Sans Ultime jusqu'au boss 2", longDescription = "Ne pas utiliser l'Ultime (touche F) avant d'avoir vaincu le 2e boss", difficulty = ChallengeDifficulty.Hard },

        new ChallengeDefinition { id = "hp50boss1", displayName = "Résilience", description = "Garder plus de 50% de vie jusqu'au boss 1", longDescription = "Ne jamais descendre sous 50% de vie, du début de la partie jusqu'à la mort du 1er boss.", difficulty = ChallengeDifficulty.Hard },
        new ChallengeDefinition { id = "bossUnder30s", displayName = "Éclair", description = "Vaincre un boss en moins de 30s", longDescription = "Vaincre n'importe quel boss en moins de 30 secondes après son apparition", difficulty = ChallengeDifficulty.Hard },
        new ChallengeDefinition { id = "gold3000", displayName = "Fortune", description = "Avec au moins 3000 Or ramassé", longDescription = "Ramasser au moins 3000 Or au total durant la partie", difficulty = ChallengeDifficulty.Hard },
        new ChallengeDefinition { id = "level30", displayName = "Puissance Absolue", description = "Niveau 30 dans la même partie", difficulty = ChallengeDifficulty.Hard },
    };

    public static ChallengeDefinition Get(string id) => _all.Find(c => c.id == id);
    public static List<ChallengeDefinition> GetByDifficulty(ChallengeDifficulty difficulty) => _all.FindAll(c => c.difficulty == difficulty);
    public static List<ChallengeDefinition> All => _all;
}
