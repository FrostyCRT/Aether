using UnityEngine;

public class BossBase : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] protected float _maxHealth = 500f;
    [SerializeField] protected float _moveSpeed = 3f;
    [SerializeField] protected float _xpValue   = 200f;
    [SerializeField] protected int   _goldValue = 50;

    [Header("Attaque")]
    [SerializeField] protected GameObject _projectilePrefab;
    [SerializeField] protected float      _fireRate        = 1f;
    [SerializeField] protected int        _projectileCount = 8;
    [SerializeField] protected float      _chargeCooldown  = 5f;

    [Header("Identité")]
    [SerializeField] protected string _bossName = "BOSS";

    protected float     _currentHealth;
    protected Transform _playerTransform;
    protected float     _fireTimer   = 0f;
    protected float     _chargeTimer = 0f;
    protected bool      _isCharging  = false;
    protected Vector3   _chargeDirection;

    protected virtual void Start()
    {
        GameUI.Instance.ShowBossHP(_bossName);
        _currentHealth = _maxHealth;

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
            _playerTransform = player.transform;
    }

    protected virtual void Update()
    {
        if (_playerTransform == null) return;
        if (GameManager.Instance.IsGameOver) return;

        HandleMovement();
        HandleShooting();
        HandleCharge();
    }

    protected virtual void HandleMovement()
    {
        if (_isCharging) return;
        Vector3 direction = (_playerTransform.position - transform.position).normalized;
        transform.position += direction * _moveSpeed * Time.deltaTime;
    }

    protected virtual void HandleShooting()
    {
        _fireTimer += Time.deltaTime;
        if (_fireTimer >= 1f / _fireRate)
        {
            ShootRadial();
            _fireTimer = 0f;
        }
    }

    protected virtual void ShootRadial()
    {
        if (_projectilePrefab == null) return;

        float angleStep = 360f / _projectileCount;

        for (int i = 0; i < _projectileCount; i++)
        {
            float   angle     = angleStep * i;
            Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;

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

    protected virtual void HandleCharge()
    {
        _chargeTimer += Time.deltaTime;

        if (_chargeTimer >= _chargeCooldown && !_isCharging)
        {
            _isCharging      = true;
            _chargeDirection = (_playerTransform.position - transform.position).normalized;
            _chargeTimer     = 0f;
            Invoke(nameof(StopCharge), 0.8f);
        }

        if (_isCharging)
            transform.position += _chargeDirection * _moveSpeed * 4f * Time.deltaTime;
    }

    protected virtual void StopCharge()
    {
        _isCharging = false;
    }

    public virtual void TakeDamage(float damage, Color color = default)
    {
        _currentHealth -= damage;

        if (DamageNumberSpawner.Instance != null)
        {
            Color c = color == default ? DamageNumberSpawner.ColorCritical : color;
            DamageNumberSpawner.Instance.Spawn(transform.position, damage, c, true);
        }

        GameUI.Instance.UpdateBossHP(_currentHealth, _maxHealth);

        if (_currentHealth <= 0)
            Die();
    }

    protected virtual void Die()
    {
        XPSystem.Instance.AddXP(_xpValue);
        GameManager.Instance.AddKill();
        MetaProgressionManager.Instance.AddRunGold(_goldValue);
        GameUI.Instance.HideBossHP();
        WaveManager.Instance.OnBossDied();
        // Régénération du joueur à la mort du boss
        HealthSystem playerHP = GameObject.FindWithTag("Player")?.GetComponent<HealthSystem>();
        if (playerHP != null) playerHP.Heal(0.5f);
        Destroy(gameObject);
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            HealthSystem health = other.GetComponent<HealthSystem>();
            if (health != null)
                health.TakeDamage(30f);
        }
    }
}