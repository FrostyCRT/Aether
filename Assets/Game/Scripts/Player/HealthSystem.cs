using UnityEngine;

public class HealthSystem : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _maxHealth = 200f; // MODIFIÉ — était 100f

    [Header("Invincibilité")]
    [SerializeField] private float _invincibilityDuration = 1f;

    private float _currentHealth;
    private bool _isInvincible = false;
    private bool _isInvincibleExternal = false;
    private float _invincibilityTimer = 0f;
    private float _damageCooldown = 0.5f;
    private float _damageTimer = 0f;

    public bool IsInvincible => _isInvincible || _isInvincibleExternal;
    public float MaxHealth => _maxHealth;

    private float _armorReduction = 0f;
    private float _regenPerSecond = 0f;
    private float _regenTimer = 0f;
    private bool _secondWindUsed = false;

    private void Awake()
    {
        float bonusHP = MetaProgressionManager.Instance.GetBonusMaxHP();
        _maxHealth += _maxHealth * bonusHP;
        _currentHealth = _maxHealth;
        _armorReduction = MetaProgressionManager.Instance.GetBonusArmor();
        _regenPerSecond = MetaProgressionManager.Instance.GetBonusRegen();
        _secondWindUsed = false;
    }

    private void Start()
    {
        if (GameUI.Instance != null)
            GameUI.Instance.UpdateHPBar(_currentHealth, _maxHealth);
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused) return;

        if (_isInvincible)
        {
            _invincibilityTimer -= Time.deltaTime;
            if (_invincibilityTimer <= 0f)
                _isInvincible = false;
        }

        if (_damageTimer > 0f)
            _damageTimer -= Time.deltaTime;

        if (_regenPerSecond > 0f && _currentHealth < _maxHealth)
        {
            _regenTimer += Time.deltaTime;
            if (_regenTimer >= 1f)
            {
                _regenTimer = 0f;
                _currentHealth = Mathf.Min(_currentHealth + _regenPerSecond, _maxHealth);

                if (GameUI.Instance != null)
                    GameUI.Instance.UpdateHPBar(_currentHealth, _maxHealth);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsInvincible) return;
        if (_damageTimer > 0f) return;

        if (other.CompareTag("Enemy"))
        {
            TakeDamage(15f); // MODIFIÉ — était 10f
            _damageTimer = _damageCooldown;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (IsInvincible) return;
        if (_damageTimer > 0f) return;

        if (other.CompareTag("Enemy"))
        {
            TakeDamage(15f); // MODIFIÉ — était 10f
            _damageTimer = _damageCooldown;
        }
    }

    public void TakeDamage(float damage)
    {
        if (IsInvincible) return;

        damage *= (1f - _armorReduction);

        if (MetaProgressionManager.Instance.HasSecondWind() && !_secondWindUsed)
        {
            if (_currentHealth - damage <= 0f)
            {
                TriggerSecondWind();
                return;
            }
        }

        _currentHealth -= damage;
        _currentHealth = Mathf.Max(_currentHealth, 0f);

        if (DamageNumberSpawner.Instance != null)
            DamageNumberSpawner.Instance.Spawn(
                transform.position, damage, DamageNumberSpawner.ColorPlayer);

        if (GameUI.Instance != null)
            GameUI.Instance.UpdateHPBar(_currentHealth, _maxHealth);

        if (_currentHealth <= 0f)
            Die();
    }

    private void TriggerSecondWind()
    {
        _secondWindUsed = true;
        _currentHealth = 1f;

        if (GameUI.Instance != null)
            GameUI.Instance.UpdateHPBar(_currentHealth, _maxHealth);

        _isInvincible = true;
        _invincibilityTimer = 3f;

        PlayerController playerCtrl = GetComponent<PlayerController>();
        if (playerCtrl != null)
            playerCtrl.ActivateInvisibility(3f);
    }

    public void TakeDamageFromProjectile(float damage)
    {
        TakeDamage(damage);
    }

    public void Heal(float percent)
    {
        _currentHealth += _maxHealth * percent;
        _currentHealth = Mathf.Min(_currentHealth, _maxHealth);

        if (GameUI.Instance != null)
            GameUI.Instance.UpdateHPBar(_currentHealth, _maxHealth);
    }

    public void SetInvincible()
    {
        _isInvincible = true;
        _invincibilityTimer = _invincibilityDuration;
    }

    public void SetInvincibleExternal(bool value)
    {
        _isInvincibleExternal = value;
    }

    private void Die()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.TriggerGameOver();

        gameObject.SetActive(false);
    }
}