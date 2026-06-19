using UnityEngine;

public class ProjectileBasic : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _speed = 10f;
    [SerializeField] private float _maxRange = 15f;

    private float _damage;
    private Vector3 _startPosition;
    private Vector3 _direction;

    // Fragmentation
    private bool _hasFragmentation = false;
    private float _fragmentDamage = 0f;
    private float _fragmentRadius = 2f;

    public void Init(Vector3 direction, float damage)
    {
        _direction = direction.normalized;
        _startPosition = transform.position;
        _damage = damage;
        _hasFragmentation = false; // Reset à chaque réutilisation depuis le pool
    }

    public void SetFragmentation(bool active, float damage, float radius)
    {
        _hasFragmentation = active;
        _fragmentDamage = damage;
        _fragmentRadius = radius;
    }

    private void Update()
    {
        transform.position += _direction * _speed * Time.deltaTime;
        float distanceTravelled = Vector3.Distance(_startPosition, transform.position);
        if (distanceTravelled >= _maxRange)
            ObjectPool.Instance.ReturnToPool("Projectile", gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            EnemyBase enemy = other.GetComponent<EnemyBase>();
            if (enemy != null)
                enemy.TakeDamage(_damage, DamageNumberSpawner.ColorProjectile);
            else
            {
                BossBase boss = other.GetComponent<BossBase>();
                if (boss != null)
                    boss.TakeDamage(_damage);
            }

            // Fragmentation — explosion en AOE à l'impact
            if (_hasFragmentation && Random.value < 0.20f)
            {
                Collider[] hits = Physics.OverlapSphere(transform.position, _fragmentRadius);
                foreach (Collider hit in hits)
                {
                    if (hit.CompareTag("Enemy") && hit != other)
                    {
                        EnemyBase nearbyEnemy = hit.GetComponent<EnemyBase>();
                        if (nearbyEnemy != null)
                            nearbyEnemy.TakeDamage(_fragmentDamage, DamageNumberSpawner.ColorCritical);

                        BossBase nearbyBoss = hit.GetComponent<BossBase>();
                        if (nearbyBoss != null)
                            nearbyBoss.TakeDamage(_fragmentDamage);
                    }
                }
            }

            ObjectPool.Instance.ReturnToPool("Projectile", gameObject);
        }
    }
}