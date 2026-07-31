using UnityEngine;

public class EnemyShooter : EnemyBase
{
    [Header("Shooter")]
    [SerializeField] private float _fireRate = 1.5f;
    [SerializeField] private float _preferredRange = 8f;
    [SerializeField] private float _fleeRange = 4f;
    [SerializeField] private float _rotationSpeed = 720f;

    [Header("Visuel")]
    [SerializeField] private GameObject _blowgunObject;
    [SerializeField] private Transform _projectileSpawnPoint; // AJOUTÉ — glisser ProjectileSpawnPoint ici

    private float _fireTimer = 0f;
    private EnemyAnimatorController _shooterAnimatorController;

    private static readonly Collider[] _shooterNeighbourBuffer = new Collider[8]; // REMIS
    private static int _shooterEnemyLayerMask = -1; // REMIS

    protected override void UpdateBehaviour(Transform target)
    {
        if (target == null) return;

        if (_shooterAnimatorController == null)
            _shooterAnimatorController = GetComponentInChildren<EnemyAnimatorController>();

        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        Vector3 separationForce = GetSeparationForce(); // REMIS
        _fireTimer += Time.deltaTime;

        Vector3 moveDirection = Vector3.zero;
        bool inFiringStance;

        if (distanceToTarget < _fleeRange)
        {
            Vector3 fleeDirection = transform.position - target.position;
            fleeDirection.y = 0f; // AJOUTÉ — empêche toute dérive verticale par accumulation
            fleeDirection = fleeDirection.normalized;
            moveDirection = (fleeDirection + separationForce).normalized;
            transform.position += moveDirection * MoveSpeed * _speedMultiplier * Time.deltaTime;
            inFiringStance = false;
        }
        else if (distanceToTarget > _preferredRange)
        {
            Vector3 chaseDirection = target.position - transform.position;
            chaseDirection.y = 0f; // AJOUTÉ — même précaution, cohérence avec la branche fuite
            chaseDirection = chaseDirection.normalized;
            moveDirection = (chaseDirection + separationForce).normalized;
            transform.position += moveDirection * MoveSpeed * _speedMultiplier * Time.deltaTime;
            inFiringStance = false;
        }
        else
        {
            
            inFiringStance = true;

            if (_fireTimer >= 1f / _fireRate)
            {
                Shoot(target);
                _fireTimer = 0f;
            }
        }

        if (_shooterAnimatorController != null)
            _shooterAnimatorController.SetAttacking(inFiringStance);

        if (_blowgunObject != null)
            _blowgunObject.SetActive(inFiringStance);

        Vector3 facingDirection = moveDirection.sqrMagnitude > 0.01f
            ? moveDirection
            : (target.position - transform.position);
        facingDirection.y = 0f;

        if (facingDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(facingDirection.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }
    }

    private Vector3 GetSeparationForce() // REMIS, avec le fix layer mask qu'on avait déjà validé sur EnemyBase
    {
        if (_shooterEnemyLayerMask == -1)
            _shooterEnemyLayerMask = 1 << LayerMask.NameToLayer("Enemy");

        Vector3 force = Vector3.zero;
        float separationRadius = 3f;

        int count = Physics.OverlapSphereNonAlloc(transform.position, separationRadius, _shooterNeighbourBuffer, _shooterEnemyLayerMask);

        for (int i = 0; i < count; i++)
        {
            Collider neighbour = _shooterNeighbourBuffer[i];
            if (neighbour.gameObject == gameObject) continue;

            Vector3 pushDirection = transform.position - neighbour.transform.position;
            force += pushDirection.normalized;
        }

        return force.normalized;
    }

    private void Shoot(Transform target)
    {
        if (ObjectPool.Instance == null) return;

        // MODIFIÉ — origine du tir et direction basées sur le point de spawn, pas le centre du gobelin
        Vector3 origin = _projectileSpawnPoint != null ? _projectileSpawnPoint.position : transform.position;
        Vector3 direction = (target.position - origin).normalized;

        GameObject projectileGO = ObjectPool.Instance.Get("EnemyProjectile", origin, Quaternion.identity);
        if (projectileGO == null) return;

        EnemyProjectile projectile = projectileGO.GetComponent<EnemyProjectile>();
        if (projectile != null)
        {
            projectile.Init(direction);
        }
    }
}