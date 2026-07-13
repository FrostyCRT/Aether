using UnityEngine;

public class PlayerAnimatorController : MonoBehaviour
{
    private Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void SetWalking(bool isMoving)
    {
        animator.SetBool("IsWalking", isMoving);
    }

    public void TriggerDeath()
    {
        animator.SetTrigger("IsDead");
    }
}