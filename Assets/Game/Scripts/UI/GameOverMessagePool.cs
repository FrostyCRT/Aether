using UnityEngine;

// Messages d'ambiance affiches sur le Game Over quand aucun record (nouveau ou
// presque battu) n'a la priorite - voir GameUI.PopulateGameOverMessage(). Le
// choix se fait par cause de mort ("boss", "horde", ou tout autre futur id non
// reconnu ici) pour rester plus percutant qu'un pool 100% generique, sans
// bloquer sur les causes qu'on n'a pas encore cablees ailleurs dans le jeu.
//
// MODIFIE (2026-09-12) - reecriture complete du ton. L'ancien pool etait de
// l'humour noir/familier (blagues, deadpan, "GG. Enfin non, pas GG.") - retour
// utilisateur direct : "je ne sais pas si ça a sa place, ça fait pas jeu
// serieux". Rejoint un point deja identifie dans NOTES.md ("Grands chantiers"
// #5 : messages sarcastiques vs lore epique-sincere, incoherent, une seule
// voie a choisir) jamais tranche jusqu'ici. Nouvelle direction validee par
// l'utilisateur parmi 3 propositions : "dur mais motivant" - garde de la
// personnalite/chaleur, zero blague/meme, parle au joueur comme un vrai
// adversaire respecte, pousse a rejouer plutot qu'a rire du echec.
public static class GameOverMessagePool
{
    private static readonly string[] _genericMessages =
    {
        "Tu tenais bon. Pas assez longtemps.",
        "Chaque run t'apprend où tu as flanché.",
        "La prochaine sera la bonne.",
        "Une défaite de plus vers la victoire.",
        "Retour au combat. C'est comme ça qu'on progresse.",
        "T'étais proche. Refais-en une.",
        "Relève-toi, c'est pas fini.",
        "T'as le niveau pour la suivante.",
        "Tu sais déjà ce qui a coincé. Corrige-le.",
        "Ça ne s'est pas joué à grand-chose.",
        "La détermination compte plus que la chance, ici.",
        "Debout. On recommence.",
    };

    private static readonly string[] _bossMessages =
    {
        "T'as tenu, mais pas assez.",
        "Le boss reste invaincu. Toi non — pour l'instant.",
        "Il ne t'a laissé aucune ouverture, cette fois.",
        "Un adversaire coriace. Il faudra revenir plus fort.",
        "Le boss a gagné ce round. Pas la partie.",
    };

    private static readonly string[] _hordeMessages =
    {
        "La horde t'a eu à l'usure.",
        "Un par un, ça passe. Tous ensemble, non.",
        "Ils étaient trop nombreux — tiens plus longtemps, la prochaine fois.",
        "T'as tenu tête à vingt loups. Ils ont gagné.",
        "La meute a fini le travail. Elle ne le refera pas deux fois.",
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
