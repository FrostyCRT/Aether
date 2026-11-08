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
    // {0} = objectif qui dépend de la difficulté (défi Fortune, voir ChallengeManager.FortuneGoldGoal).
    public string Description => Fill(description);
    public string LongDescription => Fill(string.IsNullOrEmpty(longDescription) ? description : longDescription);
    private static string Fill(string key) => key.Contains("{0}") ? Loc.F(key, Loc.Int(ChallengeManager.FortuneGoldGoal)) : Loc.T(key);
}

public static class ChallengeDatabase
{
    private static readonly List<ChallengeDefinition> _all = new List<ChallengeDefinition>
    {
        new ChallengeDefinition { id = "kill150", displayName = "Chasseur", description = "Tuer 500 ennemis", difficulty = ChallengeDifficulty.Easy },
        new ChallengeDefinition { id = "level10", displayName = "Montée en Puissance", description = "Atteindre le niveau 10", difficulty = ChallengeDifficulty.Easy },
        new ChallengeDefinition { id = "boss1", displayName = "Premier Sang", description = "Vaincre 1 boss", difficulty = ChallengeDifficulty.Easy },
        new ChallengeDefinition { id = "dash20", displayName = "Toujours en Mouvement", description = "Utiliser le Dash 50 fois", difficulty = ChallengeDifficulty.Easy },

        // MODIFIE (2026-09-13) - descriptions raccourcies a 35 caracteres max
        // (espaces inclus), retour utilisateur : plusieurs debordaient de
        // l'espace dedie du menu pause (jusqu'a 48 caracteres avant, ex.
        // "Terminer la partie avec au moins 3000 Or ramasse"). Le prefixe
        // "Terminer la partie"/"Atteindre le" est retire quand redondant (une
        // description de defi porte deja implicitement sur la partie entiere).
        new ChallengeDefinition { id = "hp30never", displayName = "Sang-Froid", description = "Garder plus de 30% de vie jusqu'au boss 2", longDescription = "Ne jamais descendre sous 30% de vie, du début de la partie jusqu'à la mort du 2ème boss.", difficulty = ChallengeDifficulty.Medium },
        new ChallengeDefinition { id = "boss2", displayName = "Double Chasse", description = "Vaincre 2 boss dans la même partie", difficulty = ChallengeDifficulty.Medium },
        new ChallengeDefinition { id = "level20in10min", displayName = "Ascension Rapide", description = "Atteindre le niveau 18 en 10 minutes", difficulty = ChallengeDifficulty.Medium },

        // MODIFIÉ (2026-11-07, retour utilisateur : "10 min sans ultime ça casse le but du gameplay fluide") - classé
        // Difficile jusqu'au boss 2 depuis le 2026-10-21, ce défi retirait la capacité la plus forte du joueur pendant
        // 10 minutes. Il suffit maintenant d'ATTEINDRE le boss 1 sans Ultime (validé à son apparition, voir
        // ChallengeManager.NotifyBossSpawned) et il repasse Moyen.
        new ChallengeDefinition { id = "noUltimate", displayName = "Sans Cristal", description = "Ne pas utiliser l'Ultime avant le boss 1", longDescription = "Ne pas utiliser l'Ultime (touche F) avant l'arrivée du 1er boss", difficulty = ChallengeDifficulty.Medium },

        new ChallengeDefinition { id = "hp50boss1", displayName = "Résilience", description = "Garder plus de 50% de vie jusqu'au boss 1", longDescription = "Ne jamais descendre sous 50% de vie, du début de la partie jusqu'à la mort du 1er boss.", difficulty = ChallengeDifficulty.Hard },
        new ChallengeDefinition { id = "bossUnder30s", displayName = "Éclair", description = "Vaincre un boss en moins de 25s", longDescription = "Vaincre n'importe quel boss en moins de 25 secondes après son apparition", difficulty = ChallengeDifficulty.Hard },
        new ChallengeDefinition { id = "gold3000", displayName = "Fortune", description = "Ramasser {0} Or", longDescription = "Ramasser au moins {0} Or au total durant la partie", difficulty = ChallengeDifficulty.Hard },
        new ChallengeDefinition { id = "level30", displayName = "Puissance Absolue", description = "Atteindre le niveau 25", difficulty = ChallengeDifficulty.Hard },
    };

    public static ChallengeDefinition Get(string id) => _all.Find(c => c.id == id);
    public static List<ChallengeDefinition> GetByDifficulty(ChallengeDifficulty difficulty) => _all.FindAll(c => c.difficulty == difficulty);
    public static List<ChallengeDefinition> All => _all;
}
