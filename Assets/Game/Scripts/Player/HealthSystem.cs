using UnityEngine;

public class HealthSystem : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _maxHealth = 100f;

    [Header("Invincibilité")]
    [SerializeField] private float _invincibilityDuration = 1f;

    private float _currentHealth;
    private bool  _isInvincible         = false;
    private bool  _isInvincibleExternal = false;
    private float _invincibilityTimer   = 0f;
    private float _damageCooldown       = 0.5f;
    private float _damageTimer          = 0f;

    public bool IsInvincible => _isInvincible || _isInvincibleExternal;

    private void Awake()
    {
        float bonusHP  = MetaProgressionManager.Instance.GetBonusMaxHP();
        _maxHealth    += _maxHealth * bonusHP;
        _currentHealth = _maxHealth;
    }

    private void Start()
    {
        if (GameUI.Instance != null)
            GameUI.Instance.UpdateHPBar(_currentHealth, _maxHealth);
    }

    private void Update()
    {
        if (_isInvincible)
        {
            _invincibilityTimer -= Time.deltaTime;
            if (_invincibilityTimer <= 0f)
                _isInvincible = false;
        }

        if (_damageTimer > 0f)
            _damageTimer -= Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsInvincible) return;
        if (_damageTimer > 0f) return;

        if (other.CompareTag("Enemy"))
        {
            TakeDamage(10f);
            _damageTimer = _damageCooldown;
        }

        if (other.CompareTag("EnemyProjectile"))
        {
            TakeDamage(15f);
            _damageTimer = _damageCooldown;
            
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (IsInvincible) return;
        if (_damageTimer > 0f) return;

        if (other.CompareTag("Enemy"))
        {
            TakeDamage(10f);
            _damageTimer = _damageCooldown;
        }
    }

    public void TakeDamage(float damage)
    {
        _currentHealth -= damage;
        _currentHealth = Mathf.Max(_currentHealth, 0f);

        // Chiffre rouge au dessus du joueur
        if (DamageNumberSpawner.Instance != null)
            DamageNumberSpawner.Instance.Spawn(
                transform.position,
                damage,
                DamageNumberSpawner.ColorPlayer
            );

        GameUI.Instance.UpdateHPBar(_currentHealth, _maxHealth);
        if (_currentHealth <= 0)
            Die();
    }

    public void TakeDamageFromProjectile(float damage)
    {
        if (IsInvincible) return;
        TakeDamage(damage);
    }

    public void Heal(float percent)
    {
        _currentHealth += _maxHealth * percent;
        _currentHealth  = Mathf.Min(_currentHealth, _maxHealth);
        GameUI.Instance.UpdateHPBar(_currentHealth, _maxHealth);
    }

    public void SetInvincible()
    {
        _isInvincible       = true;
        _invincibilityTimer = _invincibilityDuration;
    }

    public void SetInvincibleExternal(bool value)
    {
        _isInvincibleExternal = value;
    }

    private void Die()
    {
        GameManager.Instance.TriggerGameOver();
        Destroy(gameObject);
    }
}