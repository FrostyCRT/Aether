using UnityEngine;
using System.Collections;

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
    private float _speedMultiplier = 1f;

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

    public void ResetDashCooldown()
    {
        _dashCooldownTimer = 0f;
        GameUI.Instance.UpdateDashCooldown(1f);
    }
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _healthSystem = GetComponent<HealthSystem>();
        _crystalSystem = GetComponent<CrystalSystem>();

        // Bonus méta
        _moveSpeed += _moveSpeed * MetaProgressionManager.Instance.GetBonusAgility();
        _dashCooldown -= MetaProgressionManager.Instance.GetBonusDashCooldown();
        _dashCooldown = Mathf.Max(_dashCooldown, 1f); // Minimum 1s
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
        _dashDirection = direction.normalized;
        _isDashing = true;
        _isInvincible = true;
        _dashTimer = _dashDuration;
        _dashCooldownTimer = _dashCooldown;
        _canAbsorb = true;
        _absorptionTimer = _absorptionWindow;

        if (_healthSystem != null) _healthSystem.SetInvincibleExternal(true);
        GameUI.Instance.UpdateDashCooldown(0f);

        // Phantom Dash — clone qui attire les ennemis
        if (MetaProgressionManager.Instance.HasPhantomDash())
            StartCoroutine(SpawnPhantomClone());
    }

    private IEnumerator SpawnPhantomClone()
    {
        // Crée un clone visuel simple à la position du dash
        GameObject clone = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        clone.transform.position = transform.position;
        clone.transform.localScale = transform.localScale;
        Destroy(clone.GetComponent<Collider>());

        // Material semi-transparent bleu
        Renderer rend = clone.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.3f, 0.6f, 1f, 0.4f);
        rend.material = mat;

        // Attire les ennemis pendant 2s
        float duration = 2f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            Collider[] nearby = Physics.OverlapSphere(clone.transform.position, 5f);
            foreach (Collider col in nearby)
            {
                if (col.CompareTag("Enemy"))
                {
                    Vector3 dir = (clone.transform.position - col.transform.position).normalized;
                    col.transform.position += dir * 3f * Time.deltaTime;
                }
            }
            yield return null;
        }

        Destroy(clone);
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
            _rb.MovePosition(_rb.position + _moveDirection * _moveSpeed * _speedMultiplier * Time.fixedDeltaTime);
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
    public void SetSpeedMultiplier(float multiplier)
    {
        _speedMultiplier = multiplier;
    }
}