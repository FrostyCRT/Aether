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
    public ChallengeDifficulty difficulty;
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
        new ChallengeDefinition { id = "hp30never", displayName = "Sang-Froid", description = "Jamais sous 30% de vie", difficulty = ChallengeDifficulty.Medium },
        new ChallengeDefinition { id = "boss2", displayName = "Double Chasse", description = "Vaincre 2 boss dans la même partie", difficulty = ChallengeDifficulty.Medium },
        new ChallengeDefinition { id = "level20in10min", displayName = "Ascension Rapide", description = "Niveau 20 avant la 10e minute", difficulty = ChallengeDifficulty.Medium },
        new ChallengeDefinition { id = "noUltimate", displayName = "Sans Cristal", description = "Sans jamais utiliser l'Ultime", difficulty = ChallengeDifficulty.Medium },

        new ChallengeDefinition { id = "noDamage", displayName = "Sans-Faute", description = "Sans prendre le moindre dégât", difficulty = ChallengeDifficulty.Hard },
        new ChallengeDefinition { id = "bossUnder30s", displayName = "Éclair", description = "Vaincre un boss en moins de 30s", difficulty = ChallengeDifficulty.Hard },
        new ChallengeDefinition { id = "gold3000", displayName = "Fortune", description = "Avec au moins 3000 Or ramassé", difficulty = ChallengeDifficulty.Hard },
        new ChallengeDefinition { id = "level30", displayName = "Puissance Absolue", description = "Niveau 30 dans la même partie", difficulty = ChallengeDifficulty.Hard },
    };

    public static ChallengeDefinition Get(string id) => _all.Find(c => c.id == id);
    public static List<ChallengeDefinition> GetByDifficulty(ChallengeDifficulty difficulty) => _all.FindAll(c => c.difficulty == difficulty);
    public static List<ChallengeDefinition> All => _all;
}
