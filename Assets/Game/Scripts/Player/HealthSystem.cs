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
    // AJOUTÉ (2026-10-03) - même pattern que EnemyBase.CurrentHealth : utile pour tout outil de test/debug qui doit
    // lire les PV du joueur sans réflexion (ex. le bot de playtest d'équilibrage, voir GameInput.DebugMoveOverride).
    public float CurrentHealth => _currentHealth;

    // AJOUTE (2026-10-07, retour utilisateur : "même si on a pas pris réputation régén, on récupère 5pv toutes
    // les secondes") - jusqu'ici _regenPerSecond venait UNIQUEMENT du nœud méta "Réputation - Régénération" :
    // sans lui, _regenPerSecond valait 0 et le bloc de régénération plus bas (Update) ne faisait jamais rien.
    // Ce socle s'additionne au bonus du nœud (voir Awake) - un joueur qui a investi dans le nœud cumule les 2.
    [Header("Régénération")]
    [SerializeField] private float _baseRegenPerSecond = 5f;

    private float _armorReduction = 0f;
    private float _regenPerSecond = 0f;
    private float _regenTimer = 0f;
    private bool _secondWindUsed = false;

    // AJOUTÉ (2026-10-01) - vrai dès que le joueur est mort. Le délai avant le panel de Game Over étant plus long
    // (pour laisser finir l'animation de mort), les ennemis peuvent encore frapper le joueur pendant ce temps :
    // sans ce garde-fou, chaque coup relancerait Die() (animation de mort rejouée + chute du corps relancée).
    private bool _isDead = false;

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
        // AJOUTÉ (2026-10-18, diagnostic complet - audit §2.14) - même garde que PlayerController.Awake() : lancer
        // la scène Game directement depuis l'éditeur (sans passer par MainMenu) laissait Instance à null ici.
        if (MetaProgressionManager.Instance != null)
        {
            float bonusHP = MetaProgressionManager.Instance.GetBonusMaxHP();
            _maxHealth += _maxHealth * bonusHP;
            _armorReduction = MetaProgressionManager.Instance.GetBonusArmor();
            _regenPerSecond = _baseRegenPerSecond + MetaProgressionManager.Instance.GetReputationBonusRegen();
        }
        else
        {
            _regenPerSecond = _baseRegenPerSecond;
        }
        _currentHealth = _maxHealth;
        _secondWindUsed = false;
        _isDead = false;
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

        if (_projectileDamageTimer > 0f)
            _projectileDamageTimer -= Time.deltaTime;

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

    // AJOUTÉ (2026-10-28) - coup de contact d'un ennemi de la horde : le délai entre deux coups est tenu par CHAQUE ennemi
    // (EnemyBase._nextContactHitTime), pas par le joueur - plusieurs ennemis au contact frappent chacun à leur rythme.
    // Renvoie false si le coup n'a pas porté (invincibilité : dash, bouclier...), pour que l'ennemi ne perde pas son tour.
    // Plafond : au plus ContactHitsPerWindow coups par fenêtre de ContactHitWindow s (mesure 2026-10-28 sans plafond :
    // ~2 750 dégâts/s immobile dans la horde à 3:20, Kael mort en ~1 s). Coup refusé par le plafond = false aussi.
    private const int ContactHitsPerWindow = 3;
    private const float ContactHitWindow = 0.5f;
    private readonly float[] _contactHitTimes = new float[ContactHitsPerWindow];
    private int _contactHitIndex;

    public bool TakeContactHit(float damage, string source = "horde")
    {
        if (_isDead || IsInvincible) return false;
        // Le plus ancien des N derniers coups doit être sorti de la fenêtre.
        if (Time.time - _contactHitTimes[_contactHitIndex] < ContactHitWindow && _contactHitTimes[_contactHitIndex] > 0f) return false;
        _contactHitTimes[_contactHitIndex] = Time.time;
        _contactHitIndex = (_contactHitIndex + 1) % ContactHitsPerWindow;
        TakeDamage(damage, source);
        return true;
    }

    // Boss : délai global côté joueur (un seul boss, plusieurs zones de contact qui ne doivent pas cumuler).
    public void TryTakeContactDamage(float damage, float cooldown, string source = "horde")
    {
        if (IsInvincible) return;
        if (_damageTimer > 0f) return;

        TakeDamage(damage, source);
        _damageTimer = cooldown;
    }

    public void TakeDamage(float damage, string source = "horde")
    {
        // MODIFIÉ (2026-10-01) - ignore aussi les coups reçus une fois mort (voir _isDead).
        if (_isDead) return;
        if (IsInvincible) return;

        // AJOUTE - retient la source de CE coup ; si c'est le coup fatal, Die()
        // (plus bas, appele de facon synchrone dans la meme execution) lira
        // cette valeur exactement telle qu'elle est ici, au moment du coup qui
        // tue - jamais un coup anterieur ni un coup encaisse apres coup.
        _lastDamageSource = source;

        // AJOUTE - même point : réinitialise la Concentration (nœud Guerrier).
        if (_playerBuffs != null)
            _playerBuffs.NotifyDamaged();

        damage *= (1f - _armorReduction);

        if (MetaProgressionManager.Instance != null && MetaProgressionManager.Instance.HasSecondWind() && !_secondWindUsed)
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
        {
            GameUI.Instance.UpdateHPBar(_currentHealth, _maxHealth);
            // AJOUTÉ (2026-10-20, chantier satisfaction) - voir GameUI.FlashDamageVignette : un coup reçu n'avait
            // jusqu'ici aucun retour visuel propre (juste le chiffre + la barre), contrairement aux ennemis qui
            // flashent déjà à l'impact. Intensité proportionnelle à la part des PV max perdue dans CE coup.
            GameUI.Instance.FlashDamageVignette(damage / Mathf.Max(1f, _maxHealth));
        }

        // AJOUTÉ (2026-10-28, session manette) - vibration proportionnelle à la part des PV perdue ; longue secousse au
        // coup fatal.
        float lost = damage / Mathf.Max(1f, _maxHealth);
        if (_currentHealth <= 0f) PadRumble.Pulse(1f, 0.8f, 0.7f);
        else PadRumble.Pulse(Mathf.Clamp(0.25f + lost * 4f, 0.25f, 0.8f), Mathf.Clamp(0.15f + lost * 3f, 0.15f, 0.6f),
                             Mathf.Clamp(0.1f + lost * 1.5f, 0.1f, 0.3f));

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
        {
            GameUI.Instance.UpdateHPBar(_currentHealth, _maxHealth);
            // Coup qui aurait dû être fatal sans Second Souffle : mérite le flash le plus fort (severity=1), pas
            // proportionnel aux PV réellement perdus comme TakeDamage() - sans ça ce "quasi-mort" paraîtrait anodin.
            GameUI.Instance.FlashDamageVignette(1f);
        }

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

    // CORRIGÉ (2026-10-11, retour utilisateur : "je me fais one shot parce que si je suis dans lui, et qu'il lance
    // ses projectiles, je les prends tous en même temps") - BossBase.ShootRadial() (et les autres tirs à plusieurs
    // projectiles : BossDeer.ShootSpiralProjectile x2, BossCorruptedSource CrystalPulse) spawnent TOUS leurs
    // projectiles à la position du boss la même frame ; si le joueur se tient collé au boss, chaque projectile
    // chevauche déjà son collider dès l'apparition et appelait TakeDamage() directement (EnemyProjectile.cs) - sans
    // AUCUNE protection entre projectiles différents (contrairement aux dégâts de contact, qui partagent déjà
    // _damageTimer via TryTakeContactDamage). 8 projectiles encaissés la même frame = mort instantanée. Cette
    // méthode existait déjà mais n'était jamais appelée (EnemyProjectile appelait TakeDamage directement) : lui
    // donne maintenant sa propre fenêtre anti-cumul très courte (bien plus courte que le cooldown de contact à
    // 0,6-0,8 s) - assez longue pour fondre une salve simultanée en 1 seul coup, assez courte pour ne jamais
    // bloquer 2 projectiles réellement distincts qui arrivent à des moments différents.
    // CORRIGÉ (2026-10-20, retour utilisateur : "quand je prends 2 projectiles en même temps, je prends des
    // dégâts que d'un au lieu de deux") - élargie à 0,22s plus tôt dans la même session (suite 8, pour le
    // problème "trop de tireurs") pour limiter le débit de dégâts d'un essaim de tireurs - ça a justement cassé la
    // garantie documentée ci-dessus : 220 ms est assez long pour que 2 vrais tirs DISTINCTS (2 ennemis différents,
    // pas la même salve) tombent dans la même fenêtre et que le 2e soit purement perdu. Le vrai problème "essaim de
    // tireurs" a depuis sa propre solution dédiée (`EnemySpawner._maxSimultaneousRanged`, voir EnemyBase.
    // ActiveRangedCount) - cette fenêtre n'a plus besoin de faire ce travail en double. Redescendue à 0,1s, plus
    // courte même que l'originale (0,15s) : une vraie salve simultanée (même frame, ~16 ms d'écart) reste fondue en
    // un coup, mais 2 tirs à 100 ms d'écart ou plus comptent maintenant chacun pour de vrai.
    private float _projectileDamageTimer = 0f;
    [SerializeField] private float _projectileDamageMergeWindow = 0.1f;

    public void TakeDamageFromProjectile(float damage, string source = "horde")
    {
        if (IsInvincible) return;
        if (_projectileDamageTimer > 0f) return;

        TakeDamage(damage, source);
        _projectileDamageTimer = _projectileDamageMergeWindow;
    }

    // AJOUTÉ (2026-10-27) - carte Soin : +percent des PV max de DÉPART de la partie (bonus de méta inclus, figés à la
    // première augmentation), additif d'un pick à l'autre comme Dégâts+ / Cadence+. Les PV courants gagnent la même
    // quantité : le pourcentage de vie affiché ne baisse jamais.
    private float _startMaxHealth;

    public void AddMaxHealthPercent(float percent)
    {
        if (_isDead || percent <= 0f) return;
        if (_startMaxHealth <= 0f) _startMaxHealth = _maxHealth;

        float gain = _startMaxHealth * percent;
        _maxHealth += gain;
        _currentHealth = Mathf.Min(_currentHealth + gain, _maxHealth);

        if (GameUI.Instance != null)
            GameUI.Instance.UpdateHPBar(_currentHealth, _maxHealth);
    }

    public void Heal(float percent)
    {
        // AJOUTÉ (2026-10-01) - pas de soin sur un joueur déjà mort.
        if (_isDead) return;

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
        if (_isDead || amount <= 0f || _currentHealth <= 0f) return;

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
        // AJOUTÉ (2026-10-01) - une seule mort possible (voir _isDead).
        if (_isDead) return;
        _isDead = true;

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