using UnityEngine;

public class PlayerAnimatorController : MonoBehaviour
{
    private Animator _animator;

    void Awake()
    {
        // Cherche d'abord sur cet objet, puis dans les enfants
        // pour trouver celui qui est lié au modèle 3D Tripo
        _animator = GetComponentInChildren<Animator>();

        if (_animator == null)
            Debug.LogError($"[PlayerAnimatorController] Aucun Animator trouvé sur {gameObject.name} ou ses enfants !");
    }

    public void SetWalking(bool isMoving)
    {
        if (_animator == null) return;
        _animator.SetBool("IsWalking", isMoving);
    }

    public void TriggerDeath()
    {
        if (_animator == null) return;
        _animator.SetTrigger("IsDead");
    }
}