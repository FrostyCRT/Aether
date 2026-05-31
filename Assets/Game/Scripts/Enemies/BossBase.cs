using UnityEngine;

public class BossBase : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _maxHealth    = 500f;
    [SerializeField] private float _moveSpeed    = 3f;
    [SerializeField] private float _xpValue      = 200f;
    [SerializeField] private int _goldValue = 50;

    [Header("Attaque")]
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private float      _fireRate        = 1f;
    [SerializeField] private int        _projectileCount = 8; // Projectiles en éventail
    [SerializeField] private float      _chargeCooldown  = 5f; // Secondes entre chaque charge

    [Header("Identité")]
    [SerializeField] private string _bossName = "BOSS";

    private float     _currentHealth;
    private Transform _playerTransform;
    private float     _fireTimer   = 0f;
    private float     _chargeTimer = 0f;
    private bool      _isCharging  = false;
    private Vector3   _chargeDirection;

    private void Start()
    {
        GameUI.Instance.ShowBossHP(_bossName);
        
        _currentHealth = _maxHealth;

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
            _playerTransform = player.transform;
    }

    private void Update()
    {
        if (_playerTransform == null) return;
        if (GameManager.Instance.IsGameOver) return;

        HandleMovement();
        HandleShooting();
        HandleCharge();
    }

    private void HandleMovement()
    {
        if (_isCharging) return; // Pendant la charge c'est HandleCharge qui bouge

        Vector3 direction = (_playerTransform.position - transform.position).normalized;
        transform.position += direction * _moveSpeed * Time.deltaTime;
    }

    private void HandleShooting()
    {
        _fireTimer += Time.deltaTime;
        if (_fireTimer >= 1f / _fireRate)
        {
            ShootRadial();
            _fireTimer = 0f;
        }
    }

    private void ShootRadial()
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

    private void HandleCharge()
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
        {
            // On déplace via transform, pas via physique
            transform.position += _chargeDirection * _moveSpeed * 4f * Time.deltaTime;
        }
    }

    private void StopCharge()
    {
        _isCharging = false;
    }

    public void TakeDamage(float damage, Color color = default)
    {
        _currentHealth -= damage;

        // Chiffre flottant au dessus du boss
        if (DamageNumberSpawner.Instance != null)
        {
            Color c = color == default ? DamageNumberSpawner.ColorCritical : color;
            DamageNumberSpawner.Instance.Spawn(transform.position, damage, c, true); // isCritical = true pour les dégâts boss
        }

        GameUI.Instance.UpdateBossHP(_currentHealth, _maxHealth);

        if (_currentHealth <= 0)
            Die();
    }

    private void Die()
    {
        XPSystem.Instance.AddXP(_xpValue);
        GameManager.Instance.AddKill();
        MetaProgressionManager.Instance.AddRunGold(_goldValue);
        GameUI.Instance.HideBossHP();
        WaveManager.Instance.OnBossDied();
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            HealthSystem health = other.GetComponent<HealthSystem>();
            if (health != null)
                health.TakeDamage(30f);
        }
    }
}