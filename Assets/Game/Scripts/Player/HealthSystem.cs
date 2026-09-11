using UnityEngine;

public class HealthSystem : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _maxHealth = 2000f;

    [Header("Invincibilité")]
    [SerializeField] private float _invincibilityDuration = 1f;

    private float _currentHealth;
    private bool _isInvincible = false;
    private int _externalInvincibilitySources = 0;
    private float _invincibilityTimer = 0f;

    private float _damageTimer = 0f;

    [Header("Chute a la mort")]
    [SerializeField] private float _deathFallTargetY = 0.4f;
    [SerializeField] private float _deathFallDuration = 1.2f;

    public bool IsInvincible => _isInvincible || _externalInvincibilitySources > 0;
    public float MaxHealth => _maxHealth;

    private float _armorReduction = 0f;
    private float _regenPerSecond = 0f;
    private float _regenTimer = 0f;
    private bool _secondWindUsed = false;

    // AJOUTE - retient la source du DERNIER coup encaisse, pour remonter la
    // cause de la mort jusqu'au Game Over (messages d'ambiance contextuels).
    // Defaut "horde" : la plupart des degats du jeu viennent d'ennemis normaux
    // qui n'ont pas encore ete mis a jour pour preciser une source (EnemyBase/
    // EnemyProjectile, non vus) - ce defaut reste donc pertinent tant que ces
    // scripts n'auront pas ete etendus, sans rien casser en attendant.
    private string _lastDamageSource = "horde";

    // AJOUTE - cache pour réinitialiser la Concentration (nœud Guerrier) à chaque
    // coup reçu. Null si le composant n'est pas sur le prefab → no-op.
    private PlayerBuffs _playerBuffs;

    private void Awake()
    {
        float bonusHP = MetaProgressionManager.Instance.GetBonusMaxHP();
        _maxHealth += _maxHealth * bonusHP;
        _currentHealth = _maxHealth;
        _armorReduction = MetaProgressionManager.Instance.GetBonusArmor();
        _regenPerSecond = MetaProgressionManager.Instance.GetReputationBonusRegen();
        _secondWindUsed = false;
        _playerBuffs = GetComponent<PlayerBuffs>();
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

                // AJOUTE - la regen peut faire remonter le HP mais ne peut jamais
                // faire baisser le minimum deja enregistre (NotifyHealthChanged
                // ne garde que la plus basse valeur vue) - appel sans risque ici.
                if (ChallengeManager.Instance != null)
                    ChallengeManager.Instance.NotifyHealthChanged(_currentHealth, _maxHealth);
            }
        }
    }

    public void TryTakeContactDamage(float damage, float cooldown, string source = "horde")
    {
        if (IsInvincible) return;
        if (_damageTimer > 0f) return;

        TakeDamage(damage, source);
        _damageTimer = cooldown;
    }

    public void TakeDamage(float damage, string source = "horde")
    {
        if (IsInvincible) return;

        // AJOUTE - retient la source de CE coup ; si c'est le coup fatal, Die()
        // (plus bas, appele de facon synchrone dans la meme execution) lira
        // cette valeur exactement telle qu'elle est ici, au moment du coup qui
        // tue - jamais un coup anterieur ni un coup encaisse apres coup.
        _lastDamageSource = source;

        // A ce point, un vrai coup va etre encaisse (normal ou attenue
        // par Second Souffle plus bas) - notifie le defi "Sans-Faute" ici, avant
        // toute branche, pour ne jamais le manquer.
        if (ChallengeManager.Instance != null)
            ChallengeManager.Instance.NotifyDamageTaken();

        // AJOUTE - même point : réinitialise la Concentration (nœud Guerrier).
        if (_playerBuffs != null)
            _playerBuffs.NotifyDamaged();

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

        // AJOUTE - tracke le minimum de vie atteint pour le defi "Sang-Froid"
        // (ne jamais descendre sous 30%).
        if (ChallengeManager.Instance != null)
            ChallengeManager.Instance.NotifyHealthChanged(_currentHealth, _maxHealth);

        if (_currentHealth <= 0f)
            Die();
    }

    private void TriggerSecondWind()
    {
        _secondWindUsed = true;
        _currentHealth = 1f;

        if (GameUI.Instance != null)
            GameUI.Instance.UpdateHPBar(_currentHealth, _maxHealth);

        // AJOUTE - le Second Souffle ramene la vie a 1 PV, largement sous 30% -
        // doit compter pour "Sang-Froid" comme n'importe quelle autre chute de vie.
        if (ChallengeManager.Instance != null)
            ChallengeManager.Instance.NotifyHealthChanged(_currentHealth, _maxHealth);

        _isInvincible = true;
        _invincibilityTimer = 3f;

        PlayerController playerCtrl = GetComponent<PlayerController>();
        if (playerCtrl != null)
            playerCtrl.ActivateInvisibility(3f);
    }

    public void TakeDamageFromProjectile(float damage, string source = "horde")
    {
        TakeDamage(damage, source);
    }

    public void Heal(float percent)
    {
        _currentHealth += _maxHealth * percent;
        _currentHealth = Mathf.Min(_currentHealth, _maxHealth);

        if (GameUI.Instance != null)
            GameUI.Instance.UpdateHPBar(_currentHealth, _maxHealth);
    }

    // AJOUTE - soin d'une valeur PLATE (pas un pourcentage), utilisé par le nœud
    // "Récupération" (Gardien) qui restaure X PV fixes à chaque absorption de dash.
    // Cappe à _maxHealth (pas d'overheal). Ignoré si le joueur est déjà mort.
    public void HealFlat(float amount)
    {
        if (amount <= 0f || _currentHealth <= 0f) return;

        _currentHealth = Mathf.Min(_currentHealth + amount, _maxHealth);

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

    private void Die()
    {
        PlayerAnimatorController animatorController = GetComponent<PlayerAnimatorController>();
        if (animatorController != null)
            animatorController.TriggerDeath();

        PlayerController playerController = GetComponent<PlayerController>();
        if (playerController != null)
            playerController.HideStaff();

        StartCoroutine(LowerBodyOnDeath());

        // MODIFIE - transmet la cause du coup fatal, pour le message d'ambiance
        // contextuel du Game Over.
        if (GameManager.Instance != null)
            GameManager.Instance.TriggerGameOver(_lastDamageSource);
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