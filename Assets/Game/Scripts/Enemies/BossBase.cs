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
    [SerializeField] protected GameObject _projectilePrefab;
    [SerializeField] protected float _fireRate = 1f;
    [SerializeField] protected int _projectileCount = 8;
    [SerializeField] protected float _chargeCooldown = 5f;
    [SerializeField] protected float _chargeWindupDuration = 1f;

    [Header("Identité")]
    [SerializeField] protected string _bossName = "BOSS";

    [Header("Visuel Charge")]
    [SerializeField] protected Renderer _bodyRenderer;
    [SerializeField] protected string _emissionColorProperty = "_EmissionColor";
    [SerializeField] protected Color _manaGlowColor = new Color(0.3f, 0.6f, 1f);
    [SerializeField] protected float _maxEmissionIntensity = 3f;
    [SerializeField] protected float _windupAnimSpeed = 0.35f;
    [SerializeField] protected float _chargeAnimSpeed = 3f;

    [Header("Rotation")]
    [SerializeField] protected float _rotationSpeed = 500f;

    [Header("Corps à corps")] // AJOUTÉ
    [SerializeField] protected float _contactDamage = 30f; // MODIFIÉ — remplace le 30f codé en dur dans OnTriggerEnter
    [SerializeField] protected float _contactDamageCooldown = 0.6f; // AJOUTÉ

    [Header("Charge")] // AJOUTÉ
    [SerializeField] protected float _chargeDamage = 45f; // AJOUTÉ — valeur de départ, à ajuster en playtest (la charge ne faisait jamais mal avant)
    [SerializeField] protected float _chargeHitRadius = 2.5f; // AJOUTÉ

    [Header("Caméra")]
    [SerializeField] protected float _cameraZoomMargin = 0f; // AJOUTÉ — 0 par défaut = pas de zoom (Sanglier au sol n'en a pas besoin)

    protected float _currentHealth;
    protected Transform _playerTransform;
    protected float _fireTimer = 0f;
    protected float _chargeTimer = 0f;
    protected bool _isCharging = false;
    protected Vector3 _chargeDirection;
    protected float _chargeDurationTimer = 0f;
    private bool _hasDealtChargeDamage = false; // AJOUTÉ
    public float CameraZoomMargin => _cameraZoomMargin; // AJOUTÉ
    public float MaxHealth => _maxHealth;
    public bool IsSummoned { get; set; } = false;
    public bool RageDisabled { get; set; } = false;

    protected float _speedMultiplier = 1f;

    protected Animator _animator;
    protected MaterialPropertyBlock _propBlock;
    protected bool _isWindingUp = false;

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

        _animator = GetComponentInChildren<Animator>();

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
        if (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused) return;

        _isWindingUp = !_isCharging && (_chargeCooldown - _chargeTimer) <= _chargeWindupDuration;

        HandleMovement();
        HandleShooting();
        HandleCharge();
        UpdateChargeTelegraph();
    }

    protected virtual void HandleMovement()
    {
        if (_isCharging) return;
        if (_isWindingUp)
        {
            RotateTowards(_playerTransform.position - transform.position);
            return;
        }

        Vector3 direction = (_playerTransform.position - transform.position).normalized;
        Vector3 nextPosition = transform.position + direction * _moveSpeed * _speedMultiplier * Time.deltaTime;
        transform.position = MapBoundaryUtils.ClampToZone(nextPosition);
        RotateTowards(direction);
    }

    protected virtual void HandleShooting()
    {
        if (_isCharging) return;

        float timeUntilCharge = _chargeCooldown - _chargeTimer;
        if (timeUntilCharge <= _chargeWindupDuration) return;

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
            _isWindingUp = false;
            _chargeDirection = (_playerTransform.position - transform.position).normalized;
            _chargeTimer = 0f;
            _chargeDurationTimer = 0.8f;
            _hasDealtChargeDamage = false; // AJOUTÉ — reset à chaque nouvelle charge

            _fireTimer = 0f;

            if (_animator != null) _animator.speed = _chargeAnimSpeed;
        }

        if (_isCharging)
        {
            Vector3 nextPosition = transform.position + _chargeDirection * _moveSpeed * 4f * _speedMultiplier * Time.deltaTime;
            transform.position = MapBoundaryUtils.ClampToZone(nextPosition);
            RotateTowards(_chargeDirection);

            // AJOUTÉ — dégâts actifs par distance, indépendants du trigger physique
            // (la charge va assez vite pour potentiellement "sauter" par-dessus le joueur
            // entre deux frames sans jamais déclencher OnTriggerEnter — tunneling classique)
            if (!_hasDealtChargeDamage)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
                if (distanceToPlayer <= _chargeHitRadius)
                {
                    HealthSystem playerHealth = _playerTransform.GetComponent<HealthSystem>();
                    if (playerHealth != null)
                        playerHealth.TryTakeContactDamage(_chargeDamage, _contactDamageCooldown);
                    _hasDealtChargeDamage = true; // un seul coup par charge, pas de spam sur tout le trajet
                }
            }

            _chargeDurationTimer -= Time.deltaTime;
            if (_chargeDurationTimer <= 0f)
            {
                StopCharge();
            }
        }
    }

    protected void RotateTowards(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
    }

    protected virtual void StopCharge()
    {
        _isCharging = false;
        if (_animator != null) _animator.speed = 1f;
    }

    protected virtual void UpdateChargeTelegraph()
    {
        if (_isCharging) return;

        if (_isWindingUp)
        {
            if (_animator != null) _animator.speed = _windupAnimSpeed;

            float progress = 1f - Mathf.Clamp01((_chargeCooldown - _chargeTimer) / _chargeWindupDuration);
            UpdateGlowEffect(progress);
        }
        else
        {
            if (_animator != null) _animator.speed = 1f;
            UpdateGlowEffect(0f);
        }
    }

    protected void UpdateGlowEffect(float progress)
    {
        if (_bodyRenderer == null) return;

        if (_propBlock == null)
            _propBlock = new MaterialPropertyBlock();

        _bodyRenderer.GetPropertyBlock(_propBlock);
        Color emission = _manaGlowColor * Mathf.Lerp(0f, _maxEmissionIntensity, progress);
        _propBlock.SetColor(_emissionColorProperty, emission);
        _bodyRenderer.SetPropertyBlock(_propBlock);
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
                health.TryTakeContactDamage(_contactDamage, _contactDamageCooldown); // MODIFIÉ
        }
    }

    protected virtual void OnTriggerStay(Collider other) // AJOUTÉ — absente avant, c'était la cause du "un coup et plus rien" au corps-à-corps
    {
        if (other.CompareTag("Player"))
        {
            HealthSystem health = other.GetComponent<HealthSystem>();
            if (health != null)
                health.TryTakeContactDamage(_contactDamage, _contactDamageCooldown);
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