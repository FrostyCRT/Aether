using UnityEngine;

// AJOUTÉ (2026-10-18, diagnostic complet - audit §3 "À retirer absolument avant la sortie") - avant, chaque bouton de
// debug (réinitialiser la Réputation, réinitialiser les déblocages de personnages) gérait sa propre visibilité à la
// main, et 2 des 3 boutons avaient été oubliés : ils restaient VISIBLES et CLIQUABLES en version release, donnant à
// n'importe quel joueur 20 000 Éclats gratuits ou le déblocage de Kael/Lyra sans rien faire. Seul l'arbre de
// compétences (SkillTreeUI._debugResetButton) avait le bon réflexe (Debug.isDebugBuild). Point d'appel UNIQUE
// désormais pour les 3 : un oubli futur se limite à ne pas appeler Show(), jamais à mal recopier la condition.
public static class DevOnly
{
    public static void Show(GameObject go)
    {
        if (go == null) return;
        go.SetActive(Application.isEditor || Debug.isDebugBuild);
    }
}
