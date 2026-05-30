using UnityEngine;

public class EnemyShooter : EnemyBase
{
    [Header("Shooter")]
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private float _fireRate       = 1.5f; // Tirs par seconde
    [SerializeField] private float _preferredRange = 8f;   // Distance idéale du joueur
    [SerializeField] private float _fleeRange      = 4f;   // Distance minimale avant de fuir

    
    private float _fireTimer = 0f;

    protected override void UpdateBehaviour(Transform playerTransform)
    {
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // Séparation entre Shooters
        Vector3 separationForce = GetSeparationForce();

        if (distanceToPlayer < _fleeRange)
        {
            Vector3 fleeDirection = (transform.position - playerTransform.position).normalized;
            transform.position += (fleeDirection + separationForce).normalized * MoveSpeed * _speedMultiplier *  Time.deltaTime;
        }
        else if (distanceToPlayer > _preferredRange)
        {
            Vector3 chaseDirection = (playerTransform.position - transform.position).normalized;
            transform.position += (chaseDirection + separationForce).normalized * MoveSpeed * _speedMultiplier *  Time.deltaTime;
        }
        else
        {
            transform.position += separationForce * MoveSpeed * _speedMultiplier * Time.deltaTime;
            HandleShooting(playerTransform);
        }
    }

    private Vector3 GetSeparationForce()
    {
        Vector3 force = Vector3.zero;
        float separationRadius = 3f; // Distance minimum entre Shooters

        Collider[] neighbours = Physics.OverlapSphere(transform.position, separationRadius);
        foreach (Collider neighbour in neighbours)
        {
            if (neighbour.gameObject == gameObject) continue;
            if (!neighbour.CompareTag("Enemy")) continue;
            if (neighbour.GetComponent<EnemyShooter>() == null) continue;

            Vector3 pushDirection = transform.position - neighbour.transform.position;
            force += pushDirection.normalized;
        }

        return force.normalized;
    }

    private void HandleShooting(Transform playerTransform)
    {
        _fireTimer += Time.deltaTime;

        if (_fireTimer >= 1f / _fireRate)
        {
            Shoot(playerTransform);
            _fireTimer = 0f;
        }
    }

    private void Shoot(Transform playerTransform)
    {
        if (_projectilePrefab == null) return;

        Vector3 direction = (playerTransform.position - transform.position).normalized;

        GameObject projectileGO = Instantiate(
            _projectilePrefab,
            transform.position,
            Quaternion.identity
        );

        EnemyProjectile projectile = projectileGO.GetComponent<EnemyProjectile>();
        if (projectile != null)
            projectile.Init(direction);
    }
}