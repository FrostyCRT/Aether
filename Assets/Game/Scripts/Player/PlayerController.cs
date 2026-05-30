using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Déplacement")]
    [SerializeField] private float _moveSpeed = 5f;

    [Header("Dash")]
    [SerializeField] private float _dashSpeed        = 15f;
    [SerializeField] private float _dashDuration     = 0.15f;
    [SerializeField] private float _dashCooldown     = 2f;
    [SerializeField] private float _absorptionWindow = 0.3f;

    private Rigidbody    _rb;
    private HealthSystem _healthSystem;
    private Vector3      _moveDirection;

    // Dash
    private bool    _isDashing         = false;
    private bool    _isInvincible      = false;
    private float   _dashTimer         = 0f;
    private float   _dashCooldownTimer = 0f;
    private bool    _canAbsorb         = false;
    private float   _absorptionTimer   = 0f;
    private Vector3 _dashDirection;  // Direction verrouillée au moment du dash

    private CrystalSystem _crystalSystem;

    public bool  IsDashing           => _isDashing;
    public bool  IsInvincible        => _isInvincible;
    public bool  CanAbsorb           => _canAbsorb;
    public float DashCooldownPercent => _dashCooldownTimer / _dashCooldown;

    private void Awake()
    {
        _rb           = GetComponent<Rigidbody>();
        _healthSystem = GetComponent<HealthSystem>();
        _crystalSystem = GetComponent<CrystalSystem>();
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;

        HandleMovementInput();
        HandleDash();
        HandleAbsorptionWindow();
        UpdateDashCooldown();
    }

    private void HandleMovementInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical   = Input.GetAxisRaw("Vertical");
        _moveDirection   = new Vector3(horizontal, 0f, vertical).normalized;
    }

    private void HandleDash()
    {
        // Déclenche le dash
        if (Input.GetKeyDown(KeyCode.LeftShift) && !_isDashing && _dashCooldownTimer <= 0f)
        {
            // Si immobile on dash vers l'avant par défaut
            Vector3 direction = _moveDirection != Vector3.zero ? _moveDirection : transform.forward;
            StartDash(direction);
        }

        // Pendant le dash — on décrémente le timer
        if (_isDashing)
        {
            _dashTimer -= Time.deltaTime;
            if (_dashTimer <= 0f)
                StopDash();
        }
    }

    private void StartDash(Vector3 direction)
    {
        // On verrouille la direction au moment du dash
        _dashDirection     = direction.normalized;
        _isDashing         = true;
        _isInvincible      = true;
        _dashTimer         = _dashDuration;
        _dashCooldownTimer = _dashCooldown;
        _canAbsorb         = true;
        _absorptionTimer   = _absorptionWindow;

        if (_healthSystem != null) _healthSystem.SetInvincibleExternal(true);

        GameUI.Instance.UpdateDashCooldown(0f);
    }

    private void StopDash()
    {
        _isDashing    = false;
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
            GameUI.Instance.UpdateDashCooldown(1f - (_dashCooldownTimer / _dashCooldown));
        }
    }

    private void FixedUpdate()
    {
        if (_isDashing)
        {
            // On utilise _dashDirection verrouillée — pas _moveDirection
            _rb.MovePosition(_rb.position + _dashDirection * _dashSpeed * Time.fixedDeltaTime);
        }
        else
        {
            _rb.MovePosition(_rb.position + _moveDirection * _moveSpeed * Time.fixedDeltaTime);
        }
    }

    public void AddMoveSpeed(float value)
    {
        _moveSpeed += _moveSpeed * value;
    }

    public void ReduceDashCooldown(float value)
    {
        _dashCooldown = Mathf.Max(_dashCooldown - value, 0.5f);
    }
}