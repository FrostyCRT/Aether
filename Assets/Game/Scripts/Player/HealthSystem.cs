using UnityEngine;

public class HealthSystem : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _maxHealth = 200f;

    [Header("Invincibilité")]
    [SerializeField] private float _invincibilityDuration = 1f;

    private float _currentHealth;
    private bool _isInvincible = false;
    private int _externalInvincibilitySources = 0;
    private float _invincibilityTimer = 0f;
    
    private float _damageTimer = 0f;

    public bool IsInvincible => _isInvincible || _externalInvincibilitySources > 0;
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

    // SUPPRIMÉ — OnTriggerEnter(Collider other) { ... }
    // SUPPRIMÉ — OnTriggerStay(Collider other) { ... }
    // Remplacés par cette méthode, appelée activement depuis EnemyBase.UpdateBehaviour()
    // sur une base de distance, plus fiable que la détection trigger sur de gros colliders
    // qui se font repousser par la résolution physique après le premier contact.
    public void TryTakeContactDamage(float damage, float cooldown)
    {
        if (IsInvincible) return;
        if (_damageTimer > 0f) return;

        TakeDamage(damage);
        _damageTimer = cooldown;
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

    public void AddExternalInvincibility() // MODIFIÉ — remplace SetInvincibleExternal(true)
    {
        _externalInvincibilitySources++;
    }

    public void RemoveExternalInvincibility() // MODIFIÉ — remplace SetInvincibleExternal(false)
    {
        _externalInvincibilitySources = Mathf.Max(0, _externalInvincibilitySources - 1); // sécurité anti-passage négatif
    }

    private void Die()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.TriggerGameOver();

        gameObject.SetActive(false);
    }
}