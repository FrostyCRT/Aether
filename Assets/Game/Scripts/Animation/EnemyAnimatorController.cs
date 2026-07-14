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
}