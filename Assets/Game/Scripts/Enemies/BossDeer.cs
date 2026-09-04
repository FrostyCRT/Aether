using UnityEngine;
using System.Collections;

public class BossDeer : BossBase
{
    [Header("Cerf — Saut d'attaque")]
    [SerializeField] private float _jumpCooldown = 8f;
    [SerializeField] private float _jumpWindupDuration = 0.8f;
    [SerializeField] private float _jumpAirTime = 0.6f;
    [SerializeField] private float _jumpHopHeight = 4f;
    // MODIFIE - x10, cf. rescale global des degats/PV
    [SerializeField] private float _jumpDamage = 800f;
    [SerializeField] private float _jumpRadius = 4f;
    [SerializeField] private Color _telegraphColor = new Color(1f, 0.3f, 0.15f, 0.6f);
    [SerializeField] private float _summonedScaleFactor = 0.75f;

    [Header("Cerf — Restriction de saut par angle")]
    [SerializeField] private float _jumpForbiddenAngleMin = -5f * Mathf.PI / 6f;
    [SerializeField] private float _jumpForbiddenAngleMax = -1f * Mathf.PI / 6f;

    [Header("Cerf — Spirale")]
    [SerializeField] private float _spiralFireRate = 0.08f;
    [SerializeField] private int _spiralBurstCount = 36;
    [SerializeField] private bool _doubleSpiral = true;

    [Header("Cerf — Régénération")]
    // MODIFIE - x10, cf. rescale global des degats/PV
    [SerializeField] private float _regenAmount = 1000f;
    [SerializeField] private float _regenCooldown = 30f;

    [Header("Cerf — Phases")]
    [SerializeField] private float _phase2Threshold = 0.5f;
    [SerializeField] private float _rageThreshold = 0.3f;
    [SerializeField] private float _phase2SpeedMultiplier = 1.4f;

    [Header("Cerf — Morsure")]
    [SerializeField] private float _biteRange = 2f;

    [Header("Cerf — Mort")]
    [SerializeField] private float _deathAnimDuration = 2f;

    private bool _isPhase2 = false;
    private bool _isRaging = false;
    private bool _isDead = false;
    private bool _isBiting = false;

    private float _jumpTimer = 0f;
    private float _regenTimer = 0f;
    private float _spiralAngle = 0f;
    private float _spiralTimer = 0f;

    private enum JumpState { None, WindingUp, Airborne }
    private JumpState _jumpState = JumpState.None;
    private float _jumpStateTimer = 0f;
    private Vector3 _jumpTakeoffPosition;
    private Vector3 _jumpLandingPosition;
    private GameObject _telegraphObject;

    private Animator _deerAnimator;

    protected override void Start()
    {
        _bossName = "Le Cerf Ancestral";
        // MODIFIE - x10, cf. rescale global des degats/PV
        _maxHealth = 50000f;
        _moveSpeed = 4f;

        base.Start();

        _deerAnimator = GetComponentInChildren<Animator>();
    }

    protected override void Update()
    {
        if (_playerTransform == null) return;
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused) return;
        if (_isDead) return;

        if (_jumpState == JumpState.None)
        {
            Vector3 pos = transform.position;
            if (Mathf.Abs(pos.y) > 0.001f)
            {
                pos.y = 0f;
                transform.position = pos;
            }
        }

        CheckPhaseTransitions();
        HandleMovement();
        HandleJumpAttack();
        HandleSpiral();
        HandleRegen();
        UpdateAnimatorState();
    }

    protected override void HandleCharge() { }
    protected override void HandleShooting() { }

    private void CheckPhaseTransitions()
    {
        float hpPercent = _currentHealth / _maxHealth;

        if (!_isPhase2 && hpPercent <= _phase2Threshold)
        {
            _isPhase2 = true;
        }

        if (!_isRaging && !RageDisabled && hpPercent <= _rageThreshold)
        {
            _isRaging = true;
            _spiralFireRate *= 0.7f;
            _jumpCooldown *= 0.6f;
            _regenCooldown *= 0.7f;
        }
    }

    protected override void HandleMovement()
    {
        if (_jumpState != JumpState.None) return;

        float distanceToPlayer = Vector3.Distance(transform.position, _playerTransform.position);

        if (distanceToPlayer <= _biteRange)
        {
            _isBiting = true;
            RotateTowards(_playerTransform.position - transform.position);
            return;
        }
        _isBiting = false;

        Vector3 direction = (_playerTransform.position - transform.position).normalized;
        float speed = _moveSpeed * (_isPhase2 ? _phase2SpeedMultiplier : 1f) * _speedMultiplier;
        Vector3 nextPosition = transform.position + direction * speed * Time.deltaTime;
        transform.position = MapBoundaryUtils.ClampToZone(nextPosition);
        RotateTowards(direction);
    }

    private bool IsInForbiddenJumpZone()
    {
        Vector3 toDeer = transform.position - _playerTransform.position;
        toDeer.y = 0f;
        if (toDeer.sqrMagnitude < 0.01f) return false;

        float angle = Mathf.Atan2(toDeer.z, toDeer.x);

        return angle >= _jumpForbiddenAngleMin && angle <= _jumpForbiddenAngleMax;
    }

    private void HandleJumpAttack()
    {
        if (_isBiting) return;

        if (_jumpState == JumpState.None)
        {
            _jumpTimer += Time.deltaTime;
            if (_jumpTimer >= _jumpCooldown && !IsInForbiddenJumpZone())
            {
                _jumpTimer = 0f;
                StartJumpWindup();
            }
            return;
        }

        _jumpStateTimer += Time.deltaTime;

        if (_jumpState == JumpState.WindingUp)
        {
            float progress = Mathf.Clamp01(_jumpStateTimer / _jumpWindupDuration);

            if (_telegraphObject != null)
            {
                float effectiveRadius = IsSummoned ? _jumpRadius * _summonedScaleFactor : _jumpRadius;
                float diameter = effectiveRadius * 2f * progress;
                _telegraphObject.transform.localScale = new Vector3(diameter, 0.02f, diameter);
            }

            RotateTowards(_jumpLandingPosition - transform.position);

            if (_jumpStateTimer >= _jumpWindupDuration)
            {
                _jumpState = JumpState.Airborne;
                _jumpStateTimer = 0f;
            }
        }
        else if (_jumpState == JumpState.Airborne)
        {
            float progress = Mathf.Clamp01(_jumpStateTimer / _jumpAirTime);

            Vector3 horizontalPos = Vector3.Lerp(_jumpTakeoffPosition, _jumpLandingPosition, progress);
            float height = Mathf.Sin(progress * Mathf.PI) * _jumpHopHeight;
            transform.position = new Vector3(horizontalPos.x, height, horizontalPos.z);

            if (_jumpStateTimer >= _jumpAirTime)
            {
                LandJumpAttack();
            }
        }
    }

    private void StartJumpWindup()
    {
        _jumpState = JumpState.WindingUp;
        _jumpStateTimer = 0f;
        _jumpTakeoffPosition = transform.position;
        _jumpLandingPosition = MapBoundaryUtils.ClampToZone(_playerTransform.position);

        float effectiveRadius = IsSummoned ? _jumpRadius * _summonedScaleFactor : _jumpRadius;
        _telegraphObject = CreateTelegraphReticle(_jumpLandingPosition, effectiveRadius);
    }

    private void LandJumpAttack()
    {
        transform.position = new Vector3(_jumpLandingPosition.x, 0f, _jumpLandingPosition.z);

        if (_telegraphObject != null)
        {
            Destroy(_telegraphObject);
            _telegraphObject = null;
        }

        float effectiveRadius = IsSummoned ? _jumpRadius * _summonedScaleFactor : _jumpRadius;
        float distanceToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
        if (distanceToPlayer <= effectiveRadius)
        {
            HealthSystem playerHealth = _playerTransform.GetComponent<HealthSystem>();
            if (playerHealth != null)
                playerHealth.TakeDamage(_jumpDamage);
        }

        _jumpState = JumpState.None;
        _jumpStateTimer = 0f;
    }

    private GameObject CreateTelegraphReticle(Vector3 position, float radius)
    {
        GameObject reticle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(reticle.GetComponent<Collider>());

        reticle.transform.position = new Vector3(position.x, 0.05f, position.z);
        reticle.transform.localEulerAngles = Vector3.zero;
        reticle.transform.localScale = new Vector3(0.01f, 0.02f, 0.01f);

        Renderer rend = reticle.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.color = _telegraphColor;
        mat.SetFloat("_Surface", 1f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetInt("_ZWrite", 0);
        rend.material = mat;

        reticle.name = $"JumpTelegraph_{radius:F1}";

        return reticle;
    }

    private void HandleSpiral()
    {
        if (_jumpState != JumpState.None) return;
        if (_isBiting) return;

        _spiralTimer += Time.deltaTime;
        if (_spiralTimer >= _spiralFireRate)
        {
            _spiralTimer = 0f;
            ShootSpiralProjectile();
        }
    }

    private void ShootSpiralProjectile()
    {
        float angleStep = 360f / _spiralBurstCount;
        Vector3 direction = Quaternion.Euler(0, _spiralAngle, 0) * Vector3.forward;

        GameObject projectileGO = ObjectPool.Instance.Get("EnemyProjectile", transform.position, Quaternion.identity);
        if (projectileGO != null)
        {
            EnemyProjectile projectile = projectileGO.GetComponent<EnemyProjectile>();
            if (projectile != null)
                projectile.Init(direction);
        }

        if (_doubleSpiral)
        {
            Vector3 direction2 = Quaternion.Euler(0, _spiralAngle + 180f, 0) * Vector3.forward;
            GameObject projectileGO2 = ObjectPool.Instance.Get("EnemyProjectile", transform.position, Quaternion.identity);
            if (projectileGO2 != null)
            {
                EnemyProjectile projectile2 = projectileGO2.GetComponent<EnemyProjectile>();
                if (projectile2 != null)
                    projectile2.Init(direction2);
            }
        }

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

    protected override void Die()
    {
        if (_isDead) return;
        _isDead = true;

        if (_telegraphObject != null)
        {
            Destroy(_telegraphObject);
            _telegraphObject = null;
        }

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        if (_deerAnimator != null)
        {
            _deerAnimator.SetBool("IsJumping", false);
            _deerAnimator.SetBool("IsBiting", false);
            _deerAnimator.SetBool("IsMoving", false);
            _deerAnimator.SetTrigger("Death");
        }

        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        yield return new WaitForSeconds(_deathAnimDuration);
        base.Die();
    }

    protected override void UpdateAnimatorState()
    {
        if (_deerAnimator == null) return;

        bool isJumping = _jumpState != JumpState.None;
        bool isMoving = !isJumping && !_isBiting && (_playerTransform.position - transform.position).sqrMagnitude > 0.04f;

        _deerAnimator.SetBool("IsJumping", isJumping);
        _deerAnimator.SetBool("IsBiting", _isBiting);
        _deerAnimator.SetBool("IsMoving", isMoving);
        _deerAnimator.SetBool("IsPhase2", _isPhase2);
    }

    private void OnDrawGizmos()
    {
        if (_playerTransform == null) return;

        Gizmos.color = Color.red;
        int segments = 40;
        float range = _jumpForbiddenAngleMax - _jumpForbiddenAngleMin;

        Vector3 prevPoint = _playerTransform.position;
        for (int i = 0; i <= segments; i++)
        {
            float a = _jumpForbiddenAngleMin + (range * i / segments);
            Vector3 point = _playerTransform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 8f;
            Gizmos.DrawLine(i == 0 ? _playerTransform.position : prevPoint, point);
            prevPoint = point;
        }
    }
}