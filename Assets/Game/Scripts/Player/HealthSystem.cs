using UnityEngine;

public class HealthSystem : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _maxHealth = 2000f; // MODIFIE - x10, cf. rescale global des degats/PV

    [Header("Invincibilité")]
    [SerializeField] private float _invincibilityDuration = 1f;

    private float _currentHealth;
    private bool _isInvincible = false;
    private int _externalInvincibilitySources = 0;
    private float _invincibilityTimer = 0f;

    private float _damageTimer = 0f;

    // AJOUTE - le corps reste fige a la hauteur du bassin (Y=1.5, pivot du rig)
    // pendant l'animation de mort au lieu de descendre au sol : rien ne fait
    // jamais bouger transform.position.y a ce moment-la, la position racine ne
    // suit pas la pose couchee de l'animation.
    [Header("Chute a la mort")]
    [SerializeField] private float _deathFallTargetY = 0.4f;
    [SerializeField] private float _deathFallDuration = 1.2f;

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
        _regenPerSecond = MetaProgressionManager.Instance.GetReputationBonusRegen();
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

    public void AddExternalInvincibility()
    {
        _externalInvincibilitySources++;
    }

    public void RemoveExternalInvincibility()
    {
        _externalInvincibilitySources = Mathf.Max(0, _externalInvincibilitySources - 1);
    }

    // MODIFIE - retire gameObject.SetActive(false), qui desactivait TOUT le joueur
    // (modele, Animator, tout) sur la meme frame que la mort, avant que la moindre
    // animation ne puisse s'afficher. Le joueur reste maintenant visible, immobile
    // (PlayerController.Update()/FixedUpdate() s'arretent deja via IsGameOver),
    // le temps que GameManager.TriggerGameOver() affiche l'ecran de Game Over
    // 1.5s plus tard - fenetre pendant laquelle l'animation de mort peut jouer.
    // AJOUTE - declenche l'animation de mort via PlayerAnimatorController.TriggerDeath()
    // (deja prete, SetTrigger("IsDead") sur l'Animator), qui n'etait jamais appelee
    // nulle part avant ce correctif.
    // MODIFIE - cache aussi le baton en plus de declencher l'animation. Rien ne
    // le desactivait avant, il restait visible et genait visuellement l'animation
    // de mort.
    private void Die()
    {
        PlayerAnimatorController animatorController = GetComponent<PlayerAnimatorController>();
        if (animatorController != null)
            animatorController.TriggerDeath();

        PlayerController playerController = GetComponent<PlayerController>();
        if (playerController != null)
            playerController.HideStaff();

        // AJOUTE - fait descendre la racine du personnage jusqu'au sol en meme
        // temps que l'animation de mort joue, plutot que de rester fige a hauteur
        // du bassin. _deathFallDuration (1.2s) tient dans les 1.5s avant que
        // GameManager.ShowGameOver() n'affiche l'ecran de Game Over par-dessus.
        StartCoroutine(LowerBodyOnDeath());

        if (GameManager.Instance != null)
            GameManager.Instance.TriggerGameOver();
    }

    private System.Collections.IEnumerator LowerBodyOnDeath()
    {
        float startY = transform.position.y;
        float elapsed = 0f;

        while (elapsed < _deathFallDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _deathFallDuration);
            Vector3 pos = transform.position;
            pos.y = Mathf.Lerp(startY, _deathFallTargetY, t);
            transform.position = pos;
            yield return null;
        }

        Vector3 finalPos = transform.position;
        finalPos.y = _deathFallTargetY;
        transform.position = finalPos;
    }
}