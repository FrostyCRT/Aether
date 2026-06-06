using UnityEngine;

public class BossDeer : BossBase
{
    [Header("Cerf — Téléportation")]
    [SerializeField] private float _teleportCooldown  = 8f;
    [SerializeField] private float _teleportDistance  = 3f;

    [Header("Cerf — Spirale")]
    [SerializeField] private float _spiralFireRate    = 0.15f;
    [SerializeField] private int   _spiralBurstCount  = 24;

    [Header("Cerf — Régénération")]
    [SerializeField] private float _regenAmount       = 100f;
    [SerializeField] private float _regenCooldown     = 30f;

    [Header("Cerf — Rage")]
    [SerializeField] private float _rageThreshold     = 0.3f; // 30% HP
    private bool _isRaging = false;

    private float _teleportTimer = 0f;
    private float _regenTimer    = 0f;
    private float _spiralAngle   = 0f;
    private float _spiralTimer   = 0f;
    private bool  _isShooting    = false;
    private int   _spiralCount   = 0;

    protected override void Start()
    {
        base.Start();
        _bossName  = "Le Cerf Ancestral";
        _maxHealth = 800f;
        _moveSpeed = 4f;
    }

    protected override void Update()
    {
        if (_playerTransform == null) return;
        if (GameManager.Instance.IsGameOver) return;

        HandleMovement();
        HandleTeleport();
        HandleSpiral();
        HandleRegen();
        CheckRage();
    }

    // Le Cerf ne charge pas — il se déplace normalement
    protected override void HandleMovement()
    {
        if (_isTeleporting) return; // Pause après téléportation
        Vector3 direction = (_playerTransform.position - transform.position).normalized;
        transform.position += direction * _moveSpeed * Time.deltaTime;
    }

    // Téléportation derrière le joueur
    private bool _isTeleporting = false;

    private void HandleTeleport()
    {
        _teleportTimer += Time.deltaTime;
        if (_teleportTimer >= _teleportCooldown)
        {
            _teleportTimer = 0f;
            TeleportBehindPlayer();
        }
    }

    private void TeleportBehindPlayer()
    {
        if (_playerTransform == null) return;

        Vector3 directionTowardsBoss = (transform.position - _playerTransform.position).normalized;
        Vector3 behindPlayer = _playerTransform.position + directionTowardsBoss * _teleportDistance;

        transform.position = behindPlayer;

        // Freeze le mouvement pendant 0.5s après la téléportation
        _isTeleporting = true;
        Invoke(nameof(StopTeleport), 0.5f);
    }

    private void StopTeleport()
    {
        _isTeleporting = false;
    }

    // Tir en spirale
    private void HandleSpiral()
    {
        if (!_isShooting)
        {
            _spiralTimer += Time.deltaTime;
            if (_spiralTimer >= 1f / _fireRate)
            {
                _isShooting  = true;
                _spiralCount = 0;
                _spiralTimer = 0f;
            }
            return;
        }

        _spiralTimer += Time.deltaTime;
        if (_spiralTimer >= _spiralFireRate)
        {
            _spiralTimer = 0f;
            ShootSpiralProjectile();
            _spiralCount++;

            if (_spiralCount >= _spiralBurstCount)
                _isShooting = false;
        }
    }

    private void ShootSpiralProjectile()
    {
        if (_projectilePrefab == null) return;

        float   angleStep = 360f / _spiralBurstCount;
        Vector3 direction = Quaternion.Euler(0, _spiralAngle, 0) * Vector3.forward;

        GameObject projectileGO = Instantiate(
            _projectilePrefab,
            transform.position,
            Quaternion.identity
        );

        EnemyProjectile projectile = projectileGO.GetComponent<EnemyProjectile>();
        if (projectile != null)
            projectile.Init(direction);

        _spiralAngle += angleStep;
    }

    // Régénération toutes les 30 secondes
    private void HandleRegen()
    {
        _regenTimer += Time.deltaTime;
        if (_regenTimer >= _regenCooldown)
        {
            _regenTimer     = 0f;
            _currentHealth  = Mathf.Min(_currentHealth + _regenAmount, _maxHealth);
            GameUI.Instance.UpdateBossHP(_currentHealth, _maxHealth);
            Debug.Log("Cerf Ancestral se régénère !");
        }
    }

    // Rage à 30% HP
    private void CheckRage()
    {
        if (_isRaging) return;
        if (_currentHealth / _maxHealth <= _rageThreshold)
        {
            _isRaging  = true;
            _fireRate  *= 1.5f;
            _moveSpeed *= 1.5f;
            _teleportCooldown *= 0.5f;
            Debug.Log("Cerf Ancestral entre en RAGE !");
        }
    }
}