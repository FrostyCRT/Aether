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

    // AJOUTE (2026-09-24) - force une pose Idle immédiate, sans blend ni délai, PUIS applique cette pose au maillage
    // sur-le-champ (Update(0) évalue l'Animator tout de suite au lieu d'attendre la prochaine image du moteur).
    // Sert à PlayerController.SpawnPhantomClone : le clone est un instantané figé du maillage (BakeMesh) - le
    // prendre pendant une image de marche donnait un clone "gelé en pleine foulée" au lieu d'un clone immobile.
    // Tout se passe dans la même image (avant tout rendu) : invisible pour le joueur, qui garde sa vraie pose juste
    // après (voir l'appel à SetWalking() qui suit, dans le même bloc de code).
    public void SnapToIdlePose()
    {
        if (_animator == null) return;
        _animator.Play("Idle", 0, 0f);
        _animator.Update(0f);
    }
}