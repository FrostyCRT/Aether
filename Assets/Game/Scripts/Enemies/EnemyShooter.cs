using UnityEngine;

public class EnemyShooter : EnemyBase
{
    [Header("Shooter")]
    [SerializeField] private float _fireRate = 1.5f;
    [SerializeField] private float _preferredRange = 8f;
    [SerializeField] private float _fleeRange = 4f;

    private float _fireTimer = 0f;

    // CORRECTION : On renomme la variable en 'target' car le shooter peut viser le clone !
    protected override void UpdateBehaviour(Transform target)
    {
        if (target == null) return;

        // Calcul de la distance par rapport à sa cible ACTUELLE (Joueur OU Clone)
        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        Vector3 separationForce = GetSeparationForce();
        _fireTimer += Time.deltaTime;

        if (distanceToTarget < _fleeRange)
        {
            Vector3 fleeDirection = (transform.position - target.position).normalized;
            transform.position += (fleeDirection + separationForce).normalized * MoveSpeed * _speedMultiplier * Time.deltaTime;
        }
        else if (distanceToTarget > _preferredRange)
        {
            Vector3 chaseDirection = (target.position - transform.position).normalized;
            transform.position += (chaseDirection + separationForce).normalized * MoveSpeed * _speedMultiplier * Time.deltaTime;
        }
        else
        {
            transform.position += separationForce * MoveSpeed * _speedMultiplier * Time.deltaTime;

            if (_fireTimer >= 1f / _fireRate)
            {
                Shoot(target); // Tire sur sa cible actuelle
                _fireTimer = 0f;
            }
        }
    }

    private Vector3 GetSeparationForce()
    {
        Vector3 force = Vector3.zero;
        float separationRadius = 3f;

        Collider[] neighbours = new Collider[4];
        int count = Physics.OverlapSphereNonAlloc(transform.position, separationRadius, neighbours);

        for (int i = 0; i < count; i++)
        {
            Collider neighbour = neighbours[i];
            if (neighbour.gameObject == gameObject) continue;
            if (!neighbour.CompareTag("Enemy")) continue;

            Vector3 pushDirection = transform.position - neighbour.transform.position;
            force += pushDirection.normalized;
        }

        return force.normalized;
    }

    private void Shoot(Transform target)
    {
        if (ObjectPool.Instance == null) return;

        Vector3 direction = (target.position - transform.position).normalized;

        GameObject projectileGO = ObjectPool.Instance.Get("EnemyProjectile", transform.position, Quaternion.identity);
        if (projectileGO == null) return;

        EnemyProjectile projectile = projectileGO.GetComponent<EnemyProjectile>();
        if (projectile != null)
        {
            projectile.Init(direction);
        }
    }
}