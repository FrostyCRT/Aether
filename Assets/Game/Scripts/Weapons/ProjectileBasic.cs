using UnityEngine;
public class ProjectileBasic : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _speed = 10f;
    [SerializeField] private float _maxRange = 15f;
    private float _damage;
    private Vector3 _startPosition;
    private Vector3 _direction;
    private float _maxRangeSqr;
    private string _poolKey;
    // MODIFIÉ — Fragmentation : bool → chance cumulative (nœud meta + carte in-run peuvent
    // s'additionner désormais, voir WeaponFireball.GetTotalFragmentationChance())
    private float _fragmentChance = 0f;
    private float _fragmentDamage = 0f;
    private float _fragmentRadius = 2f;
    private static readonly Collider[] _overlapBuffer = new Collider[32];
    // Piercing (Shuriken)
    private bool _hasPiercing = false;
    private int _maxPierceCount = 1;
    private int _currentPierceHits = 0;
    private void Awake()
    {
        _maxRangeSqr = _maxRange * _maxRange;
        _poolKey = name.Replace("(Clone)", "").Trim();
    }
    public void Init(Vector3 direction, float damage)
    {
        _direction = direction.normalized;
        _startPosition = transform.position;
        _damage = damage;
        _fragmentChance = 0f;         // MODIFIÉ — reset systématique à chaque Init
        _hasPiercing = false;
        _currentPierceHits = 0;
    }

    // MODIFIÉ — signature : bool active → float chance (0-1). Un appelant qui veut désactiver
    // la fragmentation passe simplement 0f, plus besoin d'un flag séparé.
    public void SetFragmentation(float chance, float damage, float radius)
    {
        _fragmentChance = Mathf.Clamp01(chance);
        _fragmentDamage = damage;
        _fragmentRadius = radius;
    }

    public void SetPiercing(bool active, int maxPierceCount)
    {
        _hasPiercing = active;
        _maxPierceCount = Mathf.Max(1, maxPierceCount);
    }
    private void Update()
    {
        transform.position += _direction * _speed * Time.deltaTime;
        float distanceTravelledSqr = (transform.position - _startPosition).sqrMagnitude;
        if (distanceTravelledSqr >= _maxRangeSqr)
        {
            ReturnToPool();
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Enemy")) return;
        ApplyDamage(other, _damage, DamageNumberSpawner.ColorProjectile, false);

        // MODIFIÉ — teste directement contre la chance plutôt qu'un flag bool + constante 20%
        // en dur. La chance vient maintenant de l'appelant (WeaponFireball), qui combine
        // nœud meta + carte in-run.
        if (_fragmentChance > 0f && Random.value < _fragmentChance)
        {
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _fragmentRadius, _overlapBuffer);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _overlapBuffer[i];
                if (hit != null && hit.CompareTag("Enemy") && hit != other)
                {
                    ApplyDamage(hit, _fragmentDamage, DamageNumberSpawner.ColorCritical, true);
                }
            }
        }
        if (_hasPiercing)
        {
            _currentPierceHits++;
            if (_currentPierceHits < _maxPierceCount)
                return; // continue sa trajectoire, ne retourne PAS au pool
        }
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
            boss.TakeDamage(damage);
        }
    }
    private void ReturnToPool()
    {
        if (ObjectPool.Instance != null)
        {
            ObjectPool.Instance.ReturnToPool(_poolKey, gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}