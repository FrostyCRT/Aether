using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Déplacement")]
    [SerializeField] private float _moveSpeed = 5f;

    [Header("Dash")]
    [SerializeField] private float _dashSpeed = 15f;
    [SerializeField] private float _dashDuration = 0.15f;
    [SerializeField] private float _dashCooldown = 2f;
    [SerializeField] private float _absorptionWindow = 0.3f;

    [Header("Rotation")]
    [SerializeField] private float _rotationSpeed = 700f;

    private Rigidbody _rb;
    private HealthSystem _healthSystem;
    private Vector3 _moveDirection;
    private float _speedMultiplier = 1f;

    private bool _isDashing = false;
    private bool _isInvincible = false;
    private float _dashTimer = 0f;
    private float _dashCooldownTimer = 0f;
    private bool _canAbsorb = false;
    private float _absorptionTimer = 0f;
    private Vector3 _dashDirection;

    private CrystalSystem _crystalSystem;
    private PlayerAnimatorController _animatorController;
    public static Transform ActivePhantomClone { get; private set; }
    public static System.Action OnPhantomDestroyed;
    public bool IsDashing => _isDashing;
    public bool IsInvincible => _isInvincible;
    public bool CanAbsorb => _canAbsorb;
    public float DashCooldownPercent => _dashCooldownTimer / _dashCooldown;

    [Header("Effets Second Souffle")]
    private bool _isInvisible = false;
    private float _invisibilityTimer = 0f;
    private float _blinkTimer = 0f;
    [SerializeField] private float _blinkInterval = 0.1f; // Vitesse du clignotement
    private Renderer[] _playerRenderers;
    private bool _renderersEnabled = true;

    // Cette méthode est appelée par le HealthSystem lors du Second Souffle
    public void ActivateInvisibility(float duration)
    {
        _isInvisible = true;
        _invisibilityTimer = duration;
        _blinkTimer = 0f;

        // Récupère les composants visuels
        _playerRenderers = GetComponentsInChildren<Renderer>();

        // Désactive la collision entre le calque Player et le calque Enemy
        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");

        if (playerLayer != -1 && enemyLayer != -1)
        {
            Physics.IgnoreLayerCollision(playerLayer, enemyLayer, true);
        }
    }

    private void HandleInvisibilityTimer()
    {
        if (!_isInvisible) return;

        _invisibilityTimer -= Time.deltaTime;

        _blinkTimer += Time.deltaTime;
        if (_blinkTimer >= _blinkInterval)
        {
            _blinkTimer = 0f;
            _renderersEnabled = !_renderersEnabled;

            if (_playerRenderers != null)
            {
                for (int i = 0; i < _playerRenderers.Length; i++)
                {
                    if (_playerRenderers[i] != null)
                        _playerRenderers[i].enabled = _renderersEnabled;
                }
            }
        }

        if (_invisibilityTimer <= 0f)
        {
            _isInvisible = false;

            if (_playerRenderers != null)
            {
                for (int i = 0; i < _playerRenderers.Length; i++)
                {
                    if (_playerRenderers[i] != null)
                        _playerRenderers[i].enabled = true;
                }
            }

            int playerLayer = LayerMask.NameToLayer("Player");
            int enemyLayer = LayerMask.NameToLayer("Enemy");

            if (playerLayer != -1 && enemyLayer != -1)
            {
                Physics.IgnoreLayerCollision(playerLayer, enemyLayer, false);
            }
        }
    }

    public void ResetDashCooldown()
    {
        _dashCooldownTimer = 0f;
        if (GameUI.Instance != null) GameUI.Instance.UpdateDashCooldown(1f);
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _healthSystem = GetComponent<HealthSystem>();
        _crystalSystem = GetComponent<CrystalSystem>();
        _animatorController = GetComponent<PlayerAnimatorController>();

        _moveSpeed += _moveSpeed * MetaProgressionManager.Instance.GetBonusAgility();
        _dashCooldown -= MetaProgressionManager.Instance.GetBonusDashCooldown();
        _dashCooldown = Mathf.Max(_dashCooldown, 1f);
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused) return;

        HandleMovementInput();
        HandleDash();
        HandleAbsorptionWindow();
        UpdateDashCooldown();
        HandleInvisibilityTimer();
    }

    private void HandleMovementInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        _moveDirection = new Vector3(horizontal, 0f, vertical).normalized;

        if (_animatorController != null)
        {
            bool isMoving = _moveDirection.sqrMagnitude > 0.01f;
            _animatorController.SetWalking(isMoving);
        }
    }

    private void HandleDash()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift) && !_isDashing && _dashCooldownTimer <= 0f)
        {
            Vector3 direction = _moveDirection != Vector3.zero ? _moveDirection : transform.forward;
            StartDash(direction);
        }

        if (_isDashing)
        {
            _dashTimer -= Time.deltaTime;
            if (_dashTimer <= 0f)
                StopDash();
        }
    }

    private void StartDash(Vector3 direction)
    {
        _dashDirection = direction.normalized;
        _isDashing = true;
        _isInvincible = true;
        _dashTimer = _dashDuration;
        _dashCooldownTimer = _dashCooldown;
        _canAbsorb = true;
        _absorptionTimer = _absorptionWindow;

        if (_healthSystem != null) _healthSystem.SetInvincibleExternal(true);
        if (GameUI.Instance != null) GameUI.Instance.UpdateDashCooldown(0f);

        if (MetaProgressionManager.Instance.HasPhantomDash())
            StartCoroutine(SpawnPhantomClone());
    }

    private IEnumerator SpawnPhantomClone()
    {
        GameObject clone = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        clone.transform.position = transform.position;
        clone.transform.localScale = transform.localScale;
        clone.layer = LayerMask.NameToLayer("PhantomClone");

        ActivePhantomClone = clone.transform;

        Renderer rend = clone.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.3f, 0.6f, 1f, 0.4f);
        rend.material = mat;

        float attractRadius = 8f;
        float duration = 2f;

        Collider[] targets = new Collider[20];
        int count = Physics.OverlapSphereNonAlloc(clone.transform.position, attractRadius, targets);

        List<EnemyBase> validEnemies = new List<EnemyBase>();

        for (int i = 0; i < count; i++)
        {
            if (targets[i] != null && targets[i].CompareTag("Enemy"))
            {
                EnemyBase enemy = targets[i].GetComponent<EnemyBase>();
                if (enemy != null) validEnemies.Add(enemy);
            }
        }

        validEnemies.Sort((a, b) =>
            Vector3.SqrMagnitude(a.transform.position - clone.transform.position)
            .CompareTo(Vector3.SqrMagnitude(b.transform.position - clone.transform.position))
        );

        int maxAttracted = Mathf.Min(2, validEnemies.Count);
        for (int i = 0; i < maxAttracted; i++)
        {
            validEnemies[i].SetTarget(clone.transform, 2f);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsPaused)
                elapsed += Time.deltaTime;
            yield return null;
        }

        OnPhantomDestroyed?.Invoke();

        ActivePhantomClone = null;

        Destroy(mat);
        Destroy(clone);
    }

    private void StopDash()
    {
        _isDashing = false;
        _isInvincible = false;
        if (_healthSystem != null) _healthSystem.SetInvincibleExternal(false);
    }

    private void HandleAbsorptionWindow()
    {
        if (!_canAbsorb) return;

        _absorptionTimer -= Time.deltaTime;
        if (_absorptionTimer <= 0f)
            _canAbsorb = false;
    }

    private void UpdateDashCooldown()
    {
        if (_dashCooldownTimer > 0f)
        {
            _dashCooldownTimer -= Time.deltaTime;
            if (GameUI.Instance != null)
                GameUI.Instance.UpdateDashCooldown(1f - (_dashCooldownTimer / _dashCooldown));
        }
    }

    private void FixedUpdate()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        // Rotation instantanée vers la direction du mouvement, dans le pas physique
        // Rotation progressive vers la direction du mouvement, dans le pas physique
        if (_moveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(_moveDirection);
            Quaternion smoothedRotation = Quaternion.RotateTowards(_rb.rotation, targetRotation, _rotationSpeed * Time.fixedDeltaTime);
            _rb.MoveRotation(smoothedRotation);
        }

        if (_isDashing)
            _rb.MovePosition(_rb.position + _dashDirection * _dashSpeed * Time.fixedDeltaTime);
        else
            _rb.MovePosition(_rb.position + _moveDirection * _moveSpeed * _speedMultiplier * Time.fixedDeltaTime);
    }

    public void AddMoveSpeed(float value)
    {
        _moveSpeed += _moveSpeed * value;
    }

    public void ReduceDashCooldown(float value)
    {
        _dashCooldown = Mathf.Max(_dashCooldown - value, 0.5f);
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        _speedMultiplier = multiplier;
    }
}