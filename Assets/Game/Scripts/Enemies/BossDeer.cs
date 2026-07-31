using UnityEngine;
using System.Collections;

public class BossDeer : BossBase
{
    [Header("Cerf — Saut d'attaque")]
    [SerializeField] private float _jumpCooldown = 8f;
    [SerializeField] private float _jumpWindupDuration = 0.8f;
    [SerializeField] private float _jumpAirTime = 0.6f;
    [SerializeField] private float _jumpHopHeight = 4f;
    [SerializeField] private float _jumpDamage = 80f;
    [SerializeField] private float _jumpRadius = 4f;
    [SerializeField] private Color _telegraphColor = new Color(1f, 0.3f, 0.15f, 0.6f);

    [Header("Cerf — Spirale")]
    [SerializeField] private float _spiralFireRate = 0.08f; // MODIFIÉ — était 0.15
    [SerializeField] private int _spiralBurstCount = 36;    // MODIFIÉ — était 24
    [SerializeField] private bool _doubleSpiral = true;     // AJOUTÉ — 2ème couche décalée à 180°

    [Header("Cerf — Régénération")]
    [SerializeField] private float _regenAmount = 100f;
    [SerializeField] private float _regenCooldown = 30f;

    [Header("Cerf — Phases")]
    [SerializeField] private float _phase2Threshold = 0.5f;
    [SerializeField] private float _rageThreshold = 0.3f;
    [SerializeField] private float _phase2SpeedMultiplier = 1.4f;

    [Header("Cerf — Morsure")] // AJOUTÉ
    [SerializeField] private float _biteRange = 2f;

    [Header("Cerf — Mort")] // AJOUTÉ
    [SerializeField] private float _deathAnimDuration = 2f;

    private bool _isPhase2 = false;
    private bool _isRaging = false;
    private bool _isDead = false; // AJOUTÉ
    private bool _isBiting = false; // AJOUTÉ

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
        _maxHealth = 5000f;
        _moveSpeed = 4f;

        base.Start();

        _deerAnimator = GetComponentInChildren<Animator>();
    }

    protected override void Update()
    {
        if (_playerTransform == null) return;
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused) return;
        if (_isDead) return; // AJOUTÉ — plus aucune logique pendant la mort

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

        // AJOUTÉ — s'arrête net à distance de morsure au lieu de marcher à travers le joueur
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

    private void HandleJumpAttack()
    {
        if (_isBiting) return; // AJOUTÉ — pas de saut pendant qu'il mord, évite le chevauchement d'états

        if (_jumpState == JumpState.None)
        {
            _jumpTimer += Time.deltaTime;
            if (_jumpTimer >= _jumpCooldown)
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

            // MODIFIÉ — le vrai fix : seulement X et Z grandissent, Y reste un disque fin
            if (_telegraphObject != null)
            {
                float diameter = _jumpRadius * 2f * progress;
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

        _telegraphObject = CreateTelegraphReticle(_jumpLandingPosition);
    }

    private void LandJumpAttack()
    {
        transform.position = new Vector3(_jumpLandingPosition.x, 0f, _jumpLandingPosition.z);

        if (_telegraphObject != null)
        {
            Destroy(_telegraphObject);
            _telegraphObject = null;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
        if (distanceToPlayer <= _jumpRadius)
        {
            HealthSystem playerHealth = _playerTransform.GetComponent<HealthSystem>();
            if (playerHealth != null)
                playerHealth.TakeDamage(_jumpDamage);
        }

        _jumpState = JumpState.None;
        _jumpStateTimer = 0f;
    }

    private GameObject CreateTelegraphReticle(Vector3 position)
    {
        GameObject reticle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(reticle.GetComponent<Collider>());

        reticle.transform.position = new Vector3(position.x, 0.05f, position.z);
        reticle.transform.localEulerAngles = Vector3.zero;
        reticle.transform.localScale = new Vector3(0.01f, 0.02f, 0.01f); // MODIFIÉ — Y fixe dès le départ

        Renderer rend = reticle.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.color = _telegraphColor;
        mat.SetFloat("_Surface", 1f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetInt("_ZWrite", 0);
        rend.material = mat;

        return reticle;
    }

    private void HandleSpiral()
    {
        if (_jumpState != JumpState.None) return;
        if (_isBiting) return;

        // MODIFIÉ — plus de logique de salve/pause, tir continu tant qu'aucune autre action ne bloque
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

        // AJOUTÉ — 2ème spirale simultanée, décalée à 180°, pour un motif double plus dense
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

    // AJOUTÉ — override complet : joue Death, gèle tout, puis appelle base.Die() après coup pour les drops/heal/notify existants
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
        if (col != null) col.enabled = false; // plus de dégâts de contact pendant l'anim de mort

        if (_deerAnimator != null)
        {
            _deerAnimator.SetBool("IsJumping", false);
            _deerAnimator.SetBool("IsBiting", false);
            _deerAnimator.SetBool("IsMoving", false);
            _deerAnimator.SetTrigger("Death"); // AJOUTÉ — Trigger, pas Bool, une seule occurrence nécessaire
        }

        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        yield return new WaitForSeconds(_deathAnimDuration);
        base.Die(); // drops XP/gold, heal joueur, notify WaveManager, Destroy(gameObject) — logique déjà existante, réutilisée telle quelle
    }

    private void UpdateAnimatorState()
    {
        if (_deerAnimator == null) return;

        bool isJumping = _jumpState != JumpState.None;
        bool isMoving = !isJumping && !_isBiting && (_playerTransform.position - transform.position).sqrMagnitude > 0.04f;

        _deerAnimator.SetBool("IsJumping", isJumping);
        _deerAnimator.SetBool("IsBiting", _isBiting); // AJOUTÉ
        _deerAnimator.SetBool("IsMoving", isMoving);
        _deerAnimator.SetBool("IsPhase2", _isPhase2);
    }
}