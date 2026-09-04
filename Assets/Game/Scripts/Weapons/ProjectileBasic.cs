using UnityEngine;
public class ProjectileBasic : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _speed = 10f;
    [SerializeField] private float _maxRange = 15f;
    // AJOUTE - expose la vitesse du projectile en lecture seule, pour que WeaponBase
    // puisse calculer un temps d'impact estime et anticiper la position d'une
    // cible en mouvement (voir EnemyBase.CurrentVelocity).
    public float Speed => _speed;

    private float _damage;
    private Vector3 _startPosition;
    private Vector3 _direction;
    private float _maxRangeSqr;
    private string _poolKey;
    private float _fragmentChance = 0f;
    private float _fragmentDamage = 0f;
    private float _fragmentRadius = 2f;
    private static readonly Collider[] _overlapBuffer = new Collider[32];
    // Piercing (Shuriken)
    private bool _hasPiercing = false;
    private int _maxPierceCount = 1;
    private int _currentPierceHits = 0;

    // AJOUTE - Brulure (palier 3 Fireball). 0 = pas de brulure sur ce tir.
    private float _burnDamagePerSecond = 0f;
    private float _burnDuration = 0f;

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
        _fragmentChance = 0f;
        _hasPiercing = false;
        _currentPierceHits = 0;
        // AJOUTE - reset systematique, meme raison que _fragmentChance juste au-dessus :
        // sans ca, un tir sans brulure pourrait heriter de la brulure du tir precedent
        // si l'objet vient d'etre recycle depuis le pool.
        _burnDamagePerSecond = 0f;
        _burnDuration = 0f;
    }

    public void SetFragmentation(float chance, float damage, float radius)
    {
        _fragmentChance = Mathf.Clamp01(chance);
        _fragmentDamage = damage;
        _fragmentRadius = radius;
    }

    // AJOUTE - appele par WeaponFireball quand le palier 3 (Brulure) est debloque.
    public void SetBurn(float damagePerSecond, float duration)
    {
        _burnDamagePerSecond = damagePerSecond;
        _burnDuration = duration;
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
        ApplyBurnIfNeeded(other);

        if (_fragmentChance > 0f && Random.value < _fragmentChance)
        {
            // AJOUTE - anneau d'impact visuel (procedural, aucun asset requis) pour
            // que le joueur voie concretement la zone d'explosion au moment ou elle
            // se declenche - sans ca, l'explosion n'a jamais eu la moindre existence
            // visuelle, seuls les chiffres de degats des cibles secondaires trahissaient
            // qu'il s'etait passe quelque chose.
            ExpandingRingVFX.Spawn(transform.position, _fragmentRadius, new Color(1f, 0.5f, 0.15f, 0.85f), 0.35f);

            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _fragmentRadius, _overlapBuffer);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _overlapBuffer[i];
                if (hit != null && hit.CompareTag("Enemy") && hit != other)
                {
                    ApplyDamage(hit, _fragmentDamage, DamageNumberSpawner.ColorCritical, true);
                    ApplyBurnIfNeeded(hit);
                }
            }
        }
        if (_hasPiercing)
        {
            _currentPierceHits++;
            if (_currentPierceHits < _maxPierceCount)
                return;
        }
        ReturnToPool();
    }

    // AJOUTE - applique la Brulure sur une cible touchee (directe ou via l'explosion),
    // uniquement sur les EnemyBase pour l'instant (BossBase n'a pas encore cette API -
    // a etendre plus tard si besoin d'appliquer la Brulure sur les boss).
    private void ApplyBurnIfNeeded(Collider target)
    {
        if (_burnDamagePerSecond <= 0f) return;
        EnemyBase enemy = target.GetComponent<EnemyBase>();
        if (enemy != null) enemy.ApplyBurn(_burnDamagePerSecond, _burnDuration);
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