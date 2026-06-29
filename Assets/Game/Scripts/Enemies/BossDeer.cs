using UnityEngine;

public class BossDeer : BossBase
{
    [Header("Cerf — Téléportation")]
    [SerializeField] private float _teleportCooldown = 8f;
    [SerializeField] private float _teleportDistance = 3f;

    [Header("Cerf — Spirale")]
    [SerializeField] private float _spiralFireRate = 0.15f;
    [SerializeField] private int _spiralBurstCount = 24;

    [Header("Cerf — Régénération")]
    [SerializeField] private float _regenAmount = 100f;
    [SerializeField] private float _regenCooldown = 30f;

    [Header("Cerf — Rage")]
    [SerializeField] private float _rageThreshold = 0.3f; // 30% HP
    private bool _isRaging = false;

    private float _teleportTimer = 0f;
    private float _regenTimer = 0f;
    private float _spiralAngle = 0f;
    private float _spiralTimer = 0f;
    private bool _isShooting = false;
    private int _spiralCount = 0;

    private bool _isTeleporting = false;
    private float _teleportFreezeTimer = 0f; // Remplacement de l'Invoke

    protected override void Start()
    {
        // On configure d'abord les variables d'identité AVANT le base.Start() 
        // pour que _currentHealth = _maxHealth s'initialise correctement.
        _bossName = "Le Cerf Ancestral";
        _maxHealth = 800f;
        _moveSpeed = 4f;

        base.Start();
    }

    protected override void Update()
    {
        if (_playerTransform == null) return;
        if (GameManager.Instance == null) return;

        // CORRECTION PAUSE : Sécurisation complète de l'état du boss
        if (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused) return;

        HandleMovement();
        HandleTeleport();
        HandleSpiral();
        HandleRegen();
        CheckRage();
    }

    // Le Cerf ne charge pas — Désactivation de l'attaque de charge de la classe mère
    protected override void HandleCharge() { }

    // Désactivation du tir radial classique de la classe mère pour utiliser la spirale
    protected override void HandleShooting() { }

    protected override void HandleMovement()
    {
        if (_isTeleporting)
        {
            // CORRECTION LOGIQUE : Gestion du freeze de téléportation sans Invoke
            _teleportFreezeTimer -= Time.deltaTime;
            if (_teleportFreezeTimer <= 0f)
            {
                _isTeleporting = false;
            }
            return;
        }

        Vector3 direction = (_playerTransform.position - transform.position).normalized;
        transform.position += direction * _moveSpeed * Time.deltaTime;
    }

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

        _isTeleporting = true;
        _teleportFreezeTimer = 0.5f;
    }

    private void HandleSpiral()
    {
        if (!_isShooting)
        {
            _spiralTimer += Time.deltaTime;
            if (_spiralTimer >= 1f / _fireRate)
            {
                _isShooting = true;
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
        float angleStep = 360f / _spiralBurstCount;
        Vector3 direction = Quaternion.Euler(0, _spiralAngle, 0) * Vector3.forward;

        // CORRECTION PERFORMANCE : Remplacement de l'Instantiate par le Pool global
        GameObject projectileGO = ObjectPool.Instance.Get("EnemyProjectile", transform.position, Quaternion.identity);
        if (projectileGO == null) return;

        EnemyProjectile projectile = projectileGO.GetComponent<EnemyProjectile>();
        if (projectile != null)
            projectile.Init(direction);

        _spiralAngle += angleStep;
    }

    private void HandleRegen()
    {
        _regenTimer += Time.deltaTime;
        if (_regenTimer >= _regenCooldown)
        {
            _regenTimer = 0f;
            _currentHealth = Mathf.Min(_currentHealth + _regenAmount, _maxHealth);

            if (GameUI.Instance != null)
                GameUI.Instance.UpdateBossHP(_currentHealth, _maxHealth);
        }
    }

    private void CheckRage()
    {
        if (_isRaging) return;
        if (RageDisabled) return;
        if (_currentHealth / _maxHealth > _rageThreshold) return;

        _isRaging = true;
        _fireRate *= 1.5f;
        _moveSpeed *= 1.5f;
        _teleportCooldown *= 0.5f;
    }
}