using UnityEngine;
using System.Collections;

public class BossBase : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] protected float _maxHealth = 500f;
    [SerializeField] protected float _moveSpeed = 3f;
    [SerializeField] protected float _xpValue = 200f;
    [SerializeField] protected int _goldValue = 50;

    [Header("Attaque")]
    [SerializeField] protected GameObject _projectilePrefab; // Conservé pour la référence de l'inspecteur
    [SerializeField] protected float _fireRate = 1f;
    [SerializeField] protected int _projectileCount = 8;
    [SerializeField] protected float _chargeCooldown = 5f;

    [Header("Identité")]
    [SerializeField] protected string _bossName = "BOSS";

    protected float _currentHealth;
    protected Transform _playerTransform;
    protected float _fireTimer = 0f;
    protected float _chargeTimer = 0f;
    protected bool _isCharging = false;
    protected Vector3 _chargeDirection;
    protected float _chargeDurationTimer = 0f; // Remplacement de l'Invoke

    public float MaxHealth => _maxHealth;
    public bool IsSummoned { get; set; } = false;
    public bool RageDisabled { get; set; } = false;

    protected float _speedMultiplier = 1f; // NOUVEAU

    public void SetSpeedMultiplier(float multiplier)
    {
        _speedMultiplier = multiplier;
    }
    protected virtual void Start()
    {
        _currentHealth = _maxHealth;

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
            _playerTransform = player.transform;

        StartCoroutine(InitUI());
    }

    private IEnumerator InitUI()
    {
        yield return null;
        if (!IsSummoned)
            GameUI.Instance.ShowBossHP(_bossName);
    }

    protected virtual void Update()
    {
        if (_playerTransform == null) return;
        if (GameManager.Instance == null) return;

        // CORRECTION : Bloquer le boss si le jeu est en pause ou fini
        if (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused) return;

        HandleMovement();
        HandleShooting();
        HandleCharge();
    }

    protected virtual void HandleMovement()
    {
        if (_isCharging) return;
        Vector3 direction = (_playerTransform.position - transform.position).normalized;
        transform.position += direction * _moveSpeed * _speedMultiplier * Time.deltaTime;
    }

    protected virtual void HandleShooting()
    {
        // Ne pas tirer si le boss est déjà en train de charger
        if (_isCharging) return;

        // CORRECTION : Détection de la seconde d'attente avant le dash
        // Si le cooldown est à 5s, il s'arrête de tirer dès que le timer atteint 4s
        float timeUntilCharge = _chargeCooldown - _chargeTimer;
        if (timeUntilCharge <= 1f) return;

        _fireTimer += Time.deltaTime;
        if (_fireTimer >= 1f / _fireRate)
        {
            ShootRadial();
            _fireTimer = 0f;
        }
    }

    protected virtual void ShootRadial()
    {
        float angleStep = 360f / _projectileCount;

        for (int i = 0; i < _projectileCount; i++)
        {
            float angle = angleStep * i;
            Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;

            // CORRECTION PERFORMANCE : Utilisation de l'ObjectPool globale
            GameObject projectileGO = ObjectPool.Instance.Get("EnemyProjectile", transform.position, Quaternion.identity);
            if (projectileGO == null) continue;

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
            _isCharging = true;
            _chargeDirection = (_playerTransform.position - transform.position).normalized;
            _chargeTimer = 0f;
            _chargeDurationTimer = 0.8f;

            // Optionnel : Tu peux reset le _fireTimer ici pour que le boss ne tire pas 
            // instantanément une frame après la fin de son dash.
            _fireTimer = 0f;
        }

        if (_isCharging)
        {
            transform.position += _chargeDirection * _moveSpeed * 4f * _speedMultiplier * Time.deltaTime;

            _chargeDurationTimer -= Time.deltaTime;
            if (_chargeDurationTimer <= 0f)
            {
                StopCharge();
            }
        }
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
            DamageNumberSpawner.Instance.Spawn(transform.position, damage, c, transform, true);
        }

        if (!IsSummoned)
            GameUI.Instance.UpdateBossHP(_currentHealth, _maxHealth);

        if (_currentHealth <= 0)
            Die();
    }

    protected virtual void Die()
    {
        if (XPGemSpawner.Instance != null)
            XPGemSpawner.Instance.SpawnGems(transform.position, _xpValue);

        GameManager.Instance.AddKill();
        MetaProgressionManager.Instance.AddRunGold(_goldValue);

        if (!IsSummoned)
        {
            GameUI.Instance.HideBossHP();
            WaveManager.Instance.OnBossDied();

            HealthSystem playerHP = GameObject.FindWithTag("Player")?.GetComponent<HealthSystem>();
            if (playerHP != null)
            {
                // CORRECTION BUG LOGIQUE : Appliquer la bonne valeur calculée !
                float healAmount = playerHP.MaxHealth * 0.5f;
                playerHP.Heal(healAmount);

                if (DamageNumberSpawner.Instance != null)
                {
                    DamageNumberSpawner.Instance.Spawn(
                        playerHP.transform.position,
                        healAmount,
                        Color.green
                    );
                }
            }
        }

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

    public void InitWithReducedHP(float percent)
    {
        _currentHealth = _maxHealth * percent;
    }

    public void SetXPValue(float value)
    {
        _xpValue = value;
    }
}