using UnityEngine;

// Messages d'ambiance affiches sur le Game Over quand aucun record (nouveau ou
// presque battu) n'a la priorite - voir GameUI.PopulateGameOverMessage(). Le
// choix se fait par cause de mort ("boss", "horde", ou tout autre futur id non
// reconnu ici) pour rester plus percutant qu'un pool 100% generique, sans
// bloquer sur les causes qu'on n'a pas encore cablees ailleurs dans le jeu.
public static class GameOverMessagePool
{
    private static readonly string[] _genericMessages =
    {
        "T'es mort. Voilà, c'est dit.",
        "Bon bah ça c'est fait.",
        "Les loups ont gagné cette manche.",
        "Ça pique un peu, avoue.",
        "Même pas cinq minutes.",
        "On va dire que c'était voulu.",
        "GG. Enfin non, pas GG.",
        "Le cristal a dit non.",
        "Retour à la case départ, comme d'hab.",
        "T'as fait pire, en vrai. Enfin j'espère.",
        "Allez, la prochaine tu la fais.",
        "T'étais proche. Refais-en une.",
        "Relève-toi, c'est pas fini.",
        "Chaque partie t'apprend un truc. Celle-là t'a appris à pas faire ça.",
        "T'as le niveau pour la suivante.",
        "C'est en mourant qu'on progresse. En théorie.",
        "Encore une tentative et c'est réglé."
    };

    private static readonly string[] _bossMessages =
    {
        "Le boss t'a explosé sans forcer.",
        "Il t'a écrasé, simple et efficace.",
        "T'as tenu, mais pas assez.",
        "Le boss reste invaincu. Toi non.",
        "Il t'a laissé aucune chance, ou presque."
    };

    private static readonly string[] _hordeMessages =
    {
        "La horde t'a eu à l'usure.",
        "Un par un ça passe. Tous ensemble non.",
        "Ils étaient trop nombreux, comme d'hab.",
        "T'as tenu tête à vingt loups. Ils ont gagné.",
        "La meute a fini le travail."
    };

    public static string GetMessage(string deathCause)
    {
        string[] pool;

        switch (deathCause)
        {
            case "boss":
                pool = _bossMessages;
                break;
            case "horde":
                pool = _hordeMessages;
                break;
            default:
                pool = _genericMessages;
                break;
        }

        return pool[Random.Range(0, pool.Length)];
    }
}