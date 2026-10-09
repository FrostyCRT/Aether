using UnityEngine;

// MODIFIÉ (2026-10-05) - "LA solution" perf (texture d'animation GPU, voir NOTES.md) remplace Animator par
// VATAnimatedRenderer sur les ennemis courants. Plutôt que de modifier chaque appelant (EnemyBase, EnemyShooter,
// EnemyTank, EnemyCorruptedWeaver, EnemyKaiju...), ce pont route SetAttacking/SetAnimatorPaused vers l'un OU
// l'autre selon ce qui est présent sur le prefab - zéro ligne changée ailleurs. Les boss gardent un Animator
// classique (un seul à la fois, aucun enjeu de perf), donc _animator reste le chemin normal pour eux.
public class EnemyAnimatorController : MonoBehaviour
{
    private Animator _animator;
    private VATAnimatedRenderer _vatRenderer;

    // MODIFIÉ (2026-10-05) - le mesh VAT vit maintenant comme ENFANT DIRECT DE LA RACINE de l'ennemi (sibling de
    // l'ancienne hiérarchie d'os, pas un descendant - nécessaire pour que sa texture d'animation, cuite en espace
    // racine, s'affiche à la bonne taille - voir VATBaker.ApplyToPrefab). Comme EnemyAnimatorController vit lui
    // sur un noeud intermédiaire (même niveau que l'Armature, pas la racine), GetComponentInChildren ne verrait
    // plus ce sibling - recherche désormais depuis transform.root (la racine du prefab entier), qui reste
    // correcte aussi bien pour les ennemis déjà convertis que pour un Animator classique (boss).
    void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
        _vatRenderer = transform.root.GetComponentInChildren<VATAnimatedRenderer>(true);
    }

    public void SetAttacking(bool isAttacking)
    {
        if (_animator != null)
            _animator.SetBool("IsAttacking", isAttacking);
        else if (_vatRenderer != null)
            _vatRenderer.SetAttacking(isAttacking);
    }

    // AJOUTE - fige l'Animator sur l'image actuelle (speed = 0) plutot que de le
    // laisser boucler dans le vide. Solution retenue en l'absence de clip idle
    // dedie : forcer une transition vers un etat "Idle" qui n'existe pas ne ferait
    // que planter ou ignorer la demande. Geler l'anim la ou elle en est reste
    // largement preferable a une boucle de marche qui continue de tourner alors
    // que l'ennemi ne bouge plus du tout (ex: partie terminee).
    public void SetAnimatorPaused(bool paused)
    {
        if (_animator != null)
            _animator.speed = paused ? 0f : 1f;
        else if (_vatRenderer != null)
            _vatRenderer.SetPaused(paused);
    }
}