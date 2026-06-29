using UnityEngine;

public class ProjectileBasic : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _speed = 10f;
    [SerializeField] private float _maxRange = 15f;

    private float _damage;
    private Vector3 _startPosition;
    private Vector3 _direction;
    private float _maxRangeSqr; // Cache pour éliminer la racine carrée
    private string _poolKey;    // Cache dynamique pour l'Object Pool

    // Fragmentation
    private bool _hasFragmentation = false;
    private float _fragmentDamage = 0f;
    private float _fragmentRadius = 2f;

    // Tableau tampon statique partagé pour éviter toute allocation de Garbage Collector (max 32 cibles par explosion)
    private static readonly Collider[] _overlapBuffer = new Collider[32];

    private void Awake()
    {
        // On calcule la distance max au carré une fois pour toutes : 15 * 15 = 225
        _maxRangeSqr = _maxRange * _maxRange;
        // La clé du pool correspond exactement au nom du prefab (sans le "(Clone)")
        _poolKey = name.Replace("(Clone)", "").Trim();
    }

    public void Init(Vector3 direction, float damage)
    {
        _direction = direction.normalized;
        _startPosition = transform.position;
        _damage = damage;
        _hasFragmentation = false;
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

        // Optimisation mathématique : (Position - Départ).sqrMagnitude évite l'opération lourde de racine carrée
        float distanceTravelledSqr = (transform.position - _startPosition).sqrMagnitude;
        if (distanceTravelledSqr >= _maxRangeSqr)
        {
            ReturnToPool();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Enemy")) return;

        // 1. Dégâts directs à la cible principale
        ApplyDamage(other, _damage, DamageNumberSpawner.ColorProjectile, false);

        // 2. Gestion de l'explosion par fragmentation (20% de chance)
        if (_hasFragmentation && Random.value < 0.20f)
        {
            // Détection physique sans aucune allocation mémoire
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _fragmentRadius, _overlapBuffer);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _overlapBuffer[i];

                // On vérifie que c'est un ennemi et qu'on ne blesse pas deux fois la cible principale
                if (hit != null && hit.CompareTag("Enemy") && hit != other)
                {
                    ApplyDamage(hit, _fragmentDamage, DamageNumberSpawner.ColorCritical, true);
                }
            }
        }

        // 3. Retour au pool immédiat
        ReturnToPool();
    }

    private void ApplyDamage(Collider target, float damage, Color numberColor, bool isAOE)
    {
        EnemyBase enemy = target.GetComponent<EnemyBase>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage, numberColor);
            return;
        }

        BossBase boss = target.GetComponent<BossBase>();
        if (boss != null)
        {
            // Si c'est un boss, on peut aussi lui passer la couleur pour uniformiser
            boss.TakeDamage(damage);
        }
    }

    private void ReturnToPool()
    {
        if (ObjectPool.Instance != null)
        {
            
            ObjectPool.Instance.ReturnToPool("Projectile", gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}