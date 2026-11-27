using UnityEngine;

// AJOUTÉ (2026-11-27, retour utilisateur : « les projectiles ennemis doivent être la priorité, et après les ennemis ; les sorts
// doivent être en dessous, et l'affichage des dégâts ne doit pas gêner ») - ORDRE D'AFFICHAGE à l'écran, de bas en haut :
//   1. sol, décor, mare du Lotus, sorts du joueur (rendu normal) ;
//   2. chiffres de dégâts (transparents, rendu normal) ;
//   3. ENNEMIS et boss : calque « EnemyVisual », redessinés APRÈS les transparents par le renderer URP (Render Objects,
//      test de profondeur normal) -> au-dessus des sorts et des chiffres ;
//   4. PROJECTILES ENNEMIS : calque « EnemyFX », dessinés en dernier SANS test de profondeur -> toujours visibles, même
//      sous les pétales d'un boss.
// Seuls les objets de RENDU changent de calque : un objet portant un Collider garde le sien (physique inchangée).
public static class RenderPriority
{
    public static readonly int EnemyVisualLayer = LayerMask.NameToLayer("EnemyVisual");
    public static readonly int EnemyFxLayer = LayerMask.NameToLayer("EnemyFX");

    public static void ApplyEnemy(GameObject root) => Apply(root, EnemyVisualLayer);
    public static void ApplyEnemyProjectile(GameObject root) => Apply(root, EnemyFxLayer);

    private static void Apply(GameObject root, int layer)
    {
        if (root == null || layer < 0) return;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            GameObject g = r.gameObject;
            if (g.GetComponent<Collider>() != null) continue;               // physique : ne jamais toucher
            if (IsGroundLevel(g.transform, root.transform)) continue;       // mare du Lotus : reste au niveau du sol
            g.layer = layer;
        }
    }

    // éléments « au sol » d'un ennemi (mare de la Source) : restent sous les sorts
    private static bool IsGroundLevel(Transform t, Transform root)
    {
        for (; t != null && t != root; t = t.parent)
            if (t.name == "SourcePond") return true;
        return false;
    }
}
