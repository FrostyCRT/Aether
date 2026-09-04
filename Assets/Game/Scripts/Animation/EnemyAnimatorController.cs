using UnityEngine;
public class EnemyAnimatorController : MonoBehaviour
{
    private Animator _animator;
    void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
    }
    public void SetAttacking(bool isAttacking)
    {
        if (_animator != null)
            _animator.SetBool("IsAttacking", isAttacking);
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
    }
}