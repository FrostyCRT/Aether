using UnityEngine;
using System.Collections;

public class BossBase : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] protected float _maxHealth = 5000f; // MODIFIE - x10, cf. rescale global des degats/PV
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

    // AJOUTE - zone de telegraphe au sol pendant la charge : grandit
    // progressivement pendant le windup, disparait exactement au moment ou
    // _isCharging devient vrai. Procedural (Cube aplati), faute d'asset dedie
    // pour l'instant - meme logique que les autres visuels temporaires du projet.
    [Header("Telegraphe de charge (zone rouge au sol)")]
    [SerializeField] protected Color _chargeTelegraphColor = new Color(1f, 0f, 0f, 0.4f);
    [SerializeField] protected float _chargeTelegraphHeightOffset = 0.05f;
    private GameObject _chargeTelegraphInstance;

    [Header("Corps à corps")]
    [SerializeField] protected float _contactDamage = 300f; // MODIFIE - x10, cf. rescale global des degats/PV
    [SerializeField] protected float _contactDamageCooldown = 0.6f;

    [Header("Charge")]
    [SerializeField] protected float _chargeDamage = 450f; // MODIFIE - x10, cf. rescale global des degats/PV
    [SerializeField] protected float _chargeHitRadius = 2.5f;
    [SerializeField] protected float _chargeDistance = 16f;
    [SerializeField] protected float _chargeDuration = 0.55f;
    [SerializeField] protected float _minChargeDistance = 4f;
    [SerializeField] protected float _postChargeRecoveryDuration = 1.3f;

    private Vector3 _chargeStartPosition;
    private Vector3 _chargeEndPosition;
    private bool _isRecovering = false;
    private float _recoveryTimer = 0f;

    [Header("Caméra")]
    [SerializeField] protected float _cameraZoomMargin = 0f;

    [Header("Animator")]
    [SerializeField] protected string _isChargingParam = "IsCharging";
    [SerializeField] protected string _isWindingUpParam = "IsWindingUp";
    [SerializeField] protected string _isCruisingParam = "IsCruising";
    [SerializeField] protected string _isRecoveringParam = "IsRecovering";

    protected float _currentHealth;
    protected Transform _playerTransform;
    protected float _fireTimer = 0f;
    protected float _chargeTimer = 0f;
    protected bool _isCharging = false;
    protected Vector3 _chargeDirection;
    protected float _chargeDurationTimer = 0f;
    private bool _hasDealtChargeDamage = false;
    public float CameraZoomMargin => _cameraZoomMargin;
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

        Vector3 pos = transform.position;
        if (Mathf.Abs(pos.y) > 0.001f)
        {
            pos.y = 0f;
            transform.position = pos;
        }

        _isWindingUp = !_isCharging && !_isRecovering && (_chargeCooldown - _chargeTimer) <= _chargeWindupDuration;

        HandleMovement();
        HandleShooting();
        HandleCharge();
        UpdateChargeTelegraph();
        UpdateAnimatorState();
    }

    protected virtual void UpdateAnimatorState()
    {
        if (_animator == null) return;

        _animator.SetBool(_isChargingParam, _isCharging);
        _animator.SetBool(_isWindingUpParam, _isWindingUp);
        _animator.SetBool(_isRecoveringParam, _isRecovering);
        _animator.SetBool(_isCruisingParam, !_isCharging && !_isWindingUp && !_isRecovering);
    }

    protected virtual void HandleMovement()
    {
        if (_isCharging || _isRecovering) return;
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
        if (_isCharging || _isRecovering) return;

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
        if (_isRecovering)
        {
            _recoveryTimer -= Time.deltaTime;
            if (_recoveryTimer <= 0f)
                _isRecovering = false;
            return;
        }

        _chargeTimer += Time.deltaTime;

        if (_chargeTimer >= _chargeCooldown && !_isCharging)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
            if (distanceToPlayer >= _minChargeDistance)
            {
                _isCharging = true;
                _isWindingUp = false;
                _chargeDirection = (_playerTransform.position - transform.position).normalized;
                _chargeTimer = 0f;
                _chargeDurationTimer = _chargeDuration;
                _hasDealtChargeDamage = false;

                _chargeStartPosition = transform.position;
                _chargeEndPosition = MapBoundaryUtils.ClampToZone(transform.position + _chargeDirection * _chargeDistance);

                _fireTimer = 0f;

                if (_animator != null) _animator.speed = _chargeAnimSpeed;
            }
        }

        if (_isCharging)
        {
            float progress = 1f - Mathf.Clamp01(_chargeDurationTimer / _chargeDuration);
            transform.position = Vector3.Lerp(_chargeStartPosition, _chargeEndPosition, progress);
            RotateTowards(_chargeDirection);

            if (!_hasDealtChargeDamage)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
                if (distanceToPlayer <= _chargeHitRadius)
                {
                    HealthSystem playerHealth = _playerTransform.GetComponent<HealthSystem>();
                    if (playerHealth != null)
                        playerHealth.TryTakeContactDamage(_chargeDamage, _contactDamageCooldown);
                    _hasDealtChargeDamage = true;
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
        _isRecovering = true;
        _recoveryTimer = _postChargeRecoveryDuration;
        if (_animator != null) _animator.speed = 1f;
    }

    protected virtual void UpdateChargeTelegraph()
    {
        // MODIFIE - la zone au sol doit disparaitre EXACTEMENT au moment ou la
        // charge demarre, donc verifiee ici avant le "return" existant.
        if (_isCharging)
        {
            DestroyTelegraphZone();
            return;
        }

        if (_isRecovering)
        {
            if (_animator != null) _animator.speed = 1f;
            UpdateGlowEffect(0f);
            DestroyTelegraphZone();
            return;
        }

        if (_isWindingUp)
        {
            if (_animator != null) _animator.speed = _windupAnimSpeed;

            float progress = 1f - Mathf.Clamp01((_chargeCooldown - _chargeTimer) / _chargeWindupDuration);
            UpdateGlowEffect(progress);
            UpdateTelegraphZone(progress);
        }
        else
        {
            if (_animator != null) _animator.speed = 1f;
            UpdateGlowEffect(0f);
            DestroyTelegraphZone();
        }
    }

    // AJOUTE - cree/redimensionne la zone rouge au sol, orientee vers le joueur,
    // longueur = _chargeDistance * progress (grandit avec le windup), largeur =
    // _chargeHitRadius * 2 (correspond exactement au rayon d'impact reel de la
    // charge, pas une valeur arbitraire - le joueur apprend a lire la vraie zone
    // de danger, pas une approximation).
    protected virtual void UpdateTelegraphZone(float progress)
    {
        if (_playerTransform == null) return;

        if (_chargeTelegraphInstance == null)
            CreateTelegraphZone();

        Vector3 dir = _playerTransform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
        dir.Normalize();

        Quaternion rot = Quaternion.LookRotation(dir);

        float length = Mathf.Max(0.05f, _chargeDistance * progress);
        float width = _chargeHitRadius * 2f;
        _chargeTelegraphInstance.transform.rotation = rot;
        _chargeTelegraphInstance.transform.localScale = new Vector3(width, 0.05f, length);

        Vector3 basePos = transform.position + Vector3.up * _chargeTelegraphHeightOffset;
        _chargeTelegraphInstance.transform.position = basePos + rot * new Vector3(0f, 0f, length * 0.5f);
    }

    protected virtual void CreateTelegraphZone()
    {
        _chargeTelegraphInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _chargeTelegraphInstance.name = "ChargeTelegraphZone";

        Collider col = _chargeTelegraphInstance.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = _chargeTelegraphInstance.GetComponent<Renderer>();
        if (rend != null)
        {
            Material mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = _chargeTelegraphColor;
            rend.material = mat;
        }
    }

    protected virtual void DestroyTelegraphZone()
    {
        if (_chargeTelegraphInstance != null)
        {
            Destroy(_chargeTelegraphInstance);
            _chargeTelegraphInstance = null;
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
        // AJOUTE - le boss peut mourir en plein milieu du windup (zone en train de
        // grandir). Comme la zone est un objet totalement separe du boss (jamais
        // mis en enfant), Destroy(gameObject) plus bas ne la detruit pas avec lui -
        // elle restait orpheline sur la map indefiniment.
        DestroyTelegraphZone();

        if (XPGemSpawner.Instance != null)
            XPGemSpawner.Instance.SpawnGems(transform.position, _xpValue);

        GameManager.Instance.AddKill();
        MetaProgressionManager.Instance.AddRunGold(_goldValue);

        // AJOUTE - comptabilise ce boss pour le calcul des Eclats en fin de run
        // (niveau atteint + boss vaincus + bonus de victoire). Seuls les VRAIS
        // boss comptent, pas les invocations (IsSummoned) - coherent avec le
        // reste du fichier qui traite deja les invocations differemment
        // (ShowBossHP/HideBossHP, WaveManager.OnBossDied ne se declenchent pas
        // non plus pour elles).
        if (!IsSummoned && GameManager.Instance != null)
            GameManager.Instance.AddBossKill();

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

    // AJOUTE - filet de securite : quel que soit le chemin par lequel ce boss est
    // detruit (pas seulement via Die()), la zone de warning ne doit jamais rester
    // orpheline sur la map.
    protected virtual void OnDestroy()
    {
        DestroyTelegraphZone();
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            HealthSystem health = other.GetComponent<HealthSystem>();
            if (health != null)
                health.TryTakeContactDamage(_contactDamage, _contactDamageCooldown);
        }
    }

    protected virtual void OnTriggerStay(Collider other)
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