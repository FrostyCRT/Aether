using System.Collections.Generic;
using UnityEngine;

// Messages d'ambiance affiches sur le Game Over quand aucun record (nouveau ou
// presque battu) n'a la priorite - voir GameUI.PopulateGameOverMessage().
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
//
// MODIFIE (2026-09-12, 2e passe) - relecture "jeu serieux / Steam" demandee
// par l'utilisateur :
// - Pool "horde" retire entierement (l'utilisateur ne l'aimait pas) - toute
//   mort non-boss utilise desormais _genericMessages.
// - _genericMessages : 4 lignes retirees sur demande, et "Ça ne s'est pas joue
//   a grand-chose." n'est plus tire au hasard sans condition - elle n'a de
//   sens que si le joueur est mort tout pres de la fin du run (3 boss en 15
//   min, cf V13). Restreinte a une mort entre 12 et 15 minutes de survie.
// - _bossMessages : 2 lignes retirees, virgule en trop corrigee sur une 3e.
public static class GameOverMessagePool
{
    private static readonly string[] _genericMessages =
    {
        "La prochaine sera la bonne.",
        "Une défaite de plus vers la victoire.",
        "Retour au combat. C'est comme ça qu'on progresse.",
        "Relève-toi, c'est pas fini.",
        "Tu as le niveau pour la suivante.",
        "Tu sais déjà ce qui a coincé. Corrige-le.",
    };

    // MODIFIE (2026-09-12, 3e passe) - "Tu étais proche. Refais-en une."
    // rejoint ce groupe conditionnel sur demande utilisateur : meme probleme
    // que "Ça ne s'est pas joue a grand-chose" ci-dessous, ca n'a de sens que
    // si le joueur est mort pres de la fin du run (15 min, 3e boss) - pas a la
    // 3e minute. Ne s'affichent que si le run a dure entre 12 et 15 minutes.
    private static readonly string[] _closeToTheEndMessages =
    {
        "Ça ne s'est pas joué à grand-chose.",
        "Tu étais proche. Refais-en une.",
    };
    private const float _closeToTheEndMinSeconds = 12f * 60f;
    private const float _closeToTheEndMaxSeconds = 15f * 60f;

    // MODIFIE (2026-09-12, 3e passe) - "Pas la partie." retire : dans ce jeu
    // une "partie" est justement une run entiere, et le joueur vient de la
    // perdre (mort = fin de partie) - l'opposition round/partie n'avait donc
    // pas de sens (releve par l'utilisateur). Remplace par l'expression
    // consacree "gagner une bataille, pas la guerre" (demande utilisateur).
    private static readonly string[] _bossMessages =
    {
        "Il ne t'a laissé aucune ouverture cette fois.",
        "Un adversaire coriace. Il faudra revenir plus fort.",
        "Le boss a gagné cette bataille. Il n'a pas gagné la guerre.",
    };

    public static string GetMessage(string deathCause, float runTime)
    {
        if (deathCause == "boss")
            return _bossMessages[Random.Range(0, _bossMessages.Length)];

        List<string> pool = new List<string>(_genericMessages);
        if (runTime >= _closeToTheEndMinSeconds && runTime <= _closeToTheEndMaxSeconds)
            pool.AddRange(_closeToTheEndMessages);

        return pool[Random.Range(0, pool.Count)];
    }
}
