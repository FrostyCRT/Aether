using UnityEngine;

public class BossDeer : BossBase
{
    [Header("Cerf — Saut d'attaque")]
    [SerializeField] private float _jumpCooldown = 8f;
    [SerializeField] private float _jumpWindupDuration = 0.8f;
    [SerializeField] private float _jumpAirTime = 0.6f;
    [SerializeField] private float _jumpHopHeight = 4f;
    // MODIFIE - x10, cf. rescale global des degats/PV
    [SerializeField] private float _jumpDamage = 800f;
    [SerializeField] private float _jumpRadius = 4f;
    [SerializeField] private Color _telegraphColor = new Color(1f, 0.3f, 0.15f, 0.6f);
    [SerializeField] private float _summonedScaleFactor = 0.75f;

    [Header("Cerf — Restriction de saut par angle")]
    [SerializeField] private float _jumpForbiddenAngleMin = -5f * Mathf.PI / 6f;
    [SerializeField] private float _jumpForbiddenAngleMax = -1f * Mathf.PI / 6f;

    [Header("Cerf — Spirale")]
    [SerializeField] private float _spiralFireRate = 0.08f;
    [SerializeField] private int _spiralBurstCount = 36;
    [SerializeField] private bool _doubleSpiral = true;

    [Header("Cerf — Phases")]
    [SerializeField] private float _phase2Threshold = 0.5f;
    [SerializeField] private float _rageThreshold = 0.3f;
    [SerializeField] private float _phase2SpeedMultiplier = 1.4f;

    [Header("Cerf — Morsure")]
    [SerializeField] private float _biteRange = 2f;

    [Header("Cerf — Mort")]
    [SerializeField] private float _deathAnimDuration = 2f;

    private bool _isPhase2 = false;
    private bool _isRaging = false;
    private bool _isDead = false;
    private bool _isBiting = false;

    private float _jumpTimer = 0f;
    private float _spiralAngle = 0f;
    private float _spiralTimer = 0f;

    private enum JumpState { None, WindingUp, Airborne }
    private JumpState _jumpState = JumpState.None;
    private float _jumpStateTimer = 0f;
    private Vector3 _jumpTakeoffPosition;
    private Vector3 _jumpLandingPosition;
    // MODIFIÉ (2026-10-03) - le télégraphe du saut n'est plus un cylindre rouge qui grossit mais une zone de danger
    // animée (GroundTelegraphFX) : contour complet visible dès le début, remplissage qui avance avec le windup.
    private GroundTelegraphFX _telegraphObject;

    private Animator _deerAnimator;

    protected override void Start()
    {
        _bossName = "Le Cerf Ancestral";
        // MODIFIE - 500 -> 50000. L'ancienne valeur (juste x10 depuis un 50
        // pre-rescale) etait restee figee ici et ecrasait silencieusement
        // toute valeur du prefab a chaque Start() - le commentaire dans
        // BossCorruptedSource.Start() documente explicitement "boss 2
        // (50000)" comme reference du vrai equilibrage (boss 1 20000 -> boss 2
        // 50000 -> boss 3 125000, progression croissante voulue), jamais
        // repercute ici. Le Cerf tournait donc reellement a 500 PV depuis ce
        // rescale, pas 50000 - largement sous les deux autres boss.
        // CORRIGE (2026-10-16, audit version finale) - la ligne `_maxHealth = 50000f` qui restait ici écrasait
        // encore le prefab (relevé à 90000 le 2026-10-12) : la hausse de PV du boss 2 n'avait jamais pris effet.
        // Les PV viennent désormais uniquement du prefab, comme pour BossCorruptedSource.
        _moveSpeed = 4f;

        base.Start();

        _deerAnimator = GetComponentInChildren<Animator>();
    }

    protected override void Update()
    {
        if (_playerTransform == null) return;
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused) return;
        if (_isDead) return;

        if (_jumpState == JumpState.None)
        {
            Vector3 pos = transform.position;
            if (Mathf.Abs(pos.y) > 0.001f)
            {
                pos.y = 0f;
                transform.position = pos;
            }
        }

        CheckPhaseTransitions();
        HandleMovement();
        HandleJumpAttack();
        HandleSpiral();
        // RETIRÉ (2026-10-07, retour utilisateur : "il faut enlever la méthode de heal que se met le boss 2, parce
        // que ça rend juste le combat plus long") - régénération périodique (HandleRegen(), +1000 PV/30s) supprimée.
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
        }
    }

    protected override void HandleMovement()
    {
        if (_jumpState != JumpState.None) return;

        float distanceToPlayer = Vector3.Distance(transform.position, AimTarget.position);

        if (distanceToPlayer <= _biteRange)
        {
            _isBiting = true;
            RotateTowards(AimTarget.position - transform.position);
            return;
        }
        _isBiting = false;

        Vector3 direction = (AimTarget.position - transform.position).normalized;
        // MODIFIÉ (2026-10-21, retour utilisateur : "rendre les boss plus fort aussi dans leur vitesse de
        // déplacement") - _difficultySpeedMultiplier (BossBase), séparé de _speedMultiplier (ralentissements
        // temporaires du joueur) pour la même raison que BossBase.HandleMovement.
        float speed = _moveSpeed * (_isPhase2 ? _phase2SpeedMultiplier : 1f) * _speedMultiplier * _difficultySpeedMultiplier;
        Vector3 nextPosition = transform.position + direction * speed * Time.deltaTime;
        transform.position = MapBoundaryUtils.ClampToZone(nextPosition);
        RotateTowards(direction);
    }

    private bool IsInForbiddenJumpZone()
    {
        Vector3 toDeer = transform.position - AimTarget.position;
        toDeer.y = 0f;
        if (toDeer.sqrMagnitude < 0.01f) return false;

        float angle = Mathf.Atan2(toDeer.z, toDeer.x);

        return angle >= _jumpForbiddenAngleMin && angle <= _jumpForbiddenAngleMax;
    }

    private void HandleJumpAttack()
    {
        if (_isBiting) return;

        if (_jumpState == JumpState.None)
        {
            _jumpTimer += Time.deltaTime;
            // MODIFIÉ (2026-10-21, retour utilisateur : "le délai entre chaque attaques") - cooldown du saut
            // raccourci par _difficultyAttackSpeedMultiplier (BossBase), même principe que EffectiveChargeCooldown
            // sur le Boss 1 - le Cerf n'utilise pas _chargeCooldown (HandleCharge est vidée, voir plus haut), donc
            // le même calcul est refait ici localement sur sa propre notion de cooldown (_jumpCooldown).
            float effectiveJumpCooldown = _jumpCooldown / _difficultyAttackSpeedMultiplier;
            if (_jumpTimer >= effectiveJumpCooldown && !IsInForbiddenJumpZone())
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

            if (_telegraphObject != null)
                _telegraphObject.SetProgress(progress);

            RotateTowards(_jumpLandingPosition - transform.position);

            if (_jumpStateTimer >= _jumpWindupDuration)
            {
                _jumpState = JumpState.Airborne;
                _jumpStateTimer = 0f;
                // CORRIGE (2026-10-11, retour utilisateur : "le boss 1 one shot quand on est collé à lui, il faut
                // lui mettre le même délai de dégâts qu'on a mis sur le boss 2") - même correctif que la Frappe
                // du Boss 2 (BossCorruptedSource.StrikeAttack, voir _contactDamageSuppressed) : pendant le saut,
                // transform.position est téléporté en Lerp (pas un déplacement physique classique), donc si
                // l'atterrissage retombe sur/près du joueur, le collider générique du boss (BossBase.OnTriggerEnter/
                // Stay) déclenchait AUSSI les dégâts de contact normaux en plus des 800 dégâts explicites de
                // LandJumpAttack - un joueur collé au boss au moment de l'atterrissage encaissait donc les deux à
                // la fois. Suspendu pendant toute la phase aérienne, réactivé juste après l'impact explicite.
                _contactDamageSuppressed = true;
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
        _jumpLandingPosition = MapBoundaryUtils.ClampToZone(AimTarget.position);

        float effectiveRadius = IsSummoned ? _jumpRadius * _summonedScaleFactor : _jumpRadius;
        _telegraphObject = GroundTelegraphFX.Spawn(_jumpLandingPosition, effectiveRadius, _telegraphColor, GroundTelegraphFX.Style.Zone);
    }

    private void LandJumpAttack()
    {
        transform.position = new Vector3(_jumpLandingPosition.x, 0f, _jumpLandingPosition.z);

        // L'atterrissage touche : éclat blanc puis fondu (le télégraphe se détruit tout seul).
        if (_telegraphObject != null)
        {
            _telegraphObject.Impact();
            _telegraphObject = null;
        }

        float effectiveRadius = IsSummoned ? _jumpRadius * _summonedScaleFactor : _jumpRadius;
        float distanceToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
        if (distanceToPlayer <= effectiveRadius)
        {
            HealthSystem playerHealth = _playerTransform.GetComponent<HealthSystem>();
            if (playerHealth != null)
                playerHealth.TakeDamage(_jumpDamage * GameModes.EnemyDamageScale, "boss"); // cause "boss" (2026-10-16, audit)
        }

        _contactDamageSuppressed = false;
        _jumpState = JumpState.None;
        _jumpStateTimer = 0f;
    }

    // AJOUTÉ (2026-10-21, retour utilisateur : "pour le boss 2, mettre plus de projectiles dans ce même scale lié
    // à la difficulté") - bras supplémentaires de la spirale (au-delà des 2 bras opposés de _doubleSpiral),
    // posés par WaveManager.SpawnBoss comme les autres multiplicateurs de difficulté. 0 par défaut = comportement
    // inchangé (2 bras, comme avant ce correctif).
    private int _difficultyExtraSpiralArms = 0;
    public void SetDifficultyExtraSpiralArms(int extraArms) { _difficultyExtraSpiralArms = Mathf.Max(0, extraArms); }

    private void HandleSpiral()
    {
        if (_jumpState != JumpState.None) return;
        if (_isBiting) return;

        _spiralTimer += Time.deltaTime;
        // MODIFIÉ (2026-10-21) - cadence de la spirale accélérée par _difficultyAttackSpeedMultiplier (BossBase),
        // même principe que le cooldown du saut ci-dessus.
        float effectiveSpiralFireRate = _spiralFireRate / _difficultyAttackSpeedMultiplier;
        if (_spiralTimer >= effectiveSpiralFireRate)
        {
            _spiralTimer = 0f;
            ShootSpiralProjectile();
        }
    }

    private void FireSpiralArm(float angleDeg)
    {
        Vector3 direction = Quaternion.Euler(0, angleDeg, 0) * Vector3.forward;
        GameObject projectileGO = ObjectPool.Instance.Get("EnemyProjectile", transform.position, Quaternion.identity);
        if (projectileGO == null) return;
        EnemyProjectile projectile = projectileGO.GetComponent<EnemyProjectile>();
        if (projectile != null) projectile.Init(direction, "boss");
    }

    private void ShootSpiralProjectile()
    {
        float angleStep = 360f / _spiralBurstCount;

        // MODIFIÉ (2026-10-21) - _doubleSpiral (2 bras fixes, opposés à 180°) remplacé par un nombre de bras
        // RÉPARTIS UNIFORMÉMENT (toujours 2 à la base, +1 bras par palier de difficulté au-dessus de Modéré) -
        // refactorisé en boucle (voir FireSpiralArm ci-dessus) plutôt que de dupliquer le bloc Get/Init une 3e/4e
        // fois à la main.
        int totalArms = (_doubleSpiral ? 2 : 1) + _difficultyExtraSpiralArms;
        float armAngleStep = 360f / totalArms;
        for (int arm = 0; arm < totalArms; arm++)
            FireSpiralArm(_spiralAngle + armAngleStep * arm);

        _spiralAngle += angleStep;
    }

    // MODIFIÉ (2026-10-03) - la mort passe par BossBase.Die() IMMÉDIATEMENT (récompenses, victoire, musique) puis par
    // la séquence BossFX (le boss se débat, s'embrase, rétrécit, explose). L'animation "Death" du cerf joue pendant cette
    // séquence, dont la durée est alignée sur _deathAnimDuration. Avant, on attendait l'animation PUIS la mort :
    // le joueur patientait 2 s sans récompense, et la séquence BossFX serait venue s'ajouter après.
    protected override void Die()
    {
        if (_isDead) return;
        _isDead = true;

        // Mort en plein saut : on repose le cerf au sol pour que les effets partent du bon endroit.
        if (_jumpState != JumpState.None)
        {
            _jumpState = JumpState.None;
            Vector3 pos = transform.position;
            pos.y = 0f;
            transform.position = pos;
        }

        if (_telegraphObject != null)
        {
            _telegraphObject.Dismiss();
            _telegraphObject = null;
        }

        if (_deerAnimator != null)
        {
            _deerAnimator.SetBool("IsJumping", false);
            _deerAnimator.SetBool("IsBiting", false);
            _deerAnimator.SetBool("IsMoving", false);
            _deerAnimator.SetTrigger("Death");
        }

        _deathSequenceDuration = _deathAnimDuration;
        base.Die();
    }

    protected override void OnDestroy()
    {
        if (_telegraphObject != null)
            Destroy(_telegraphObject.gameObject);
        base.OnDestroy();
    }

    protected override void UpdateAnimatorState()
    {
        if (_deerAnimator == null) return;

        bool isJumping = _jumpState != JumpState.None;
        bool isMoving = !isJumping && !_isBiting && (AimTarget.position - transform.position).sqrMagnitude > 0.04f;

        _deerAnimator.SetBool("IsJumping", isJumping);
        _deerAnimator.SetBool("IsBiting", _isBiting);
        _deerAnimator.SetBool("IsMoving", isMoving);
        _deerAnimator.SetBool("IsPhase2", _isPhase2);
    }

    private void OnDrawGizmos()
    {
        if (_playerTransform == null) return;

        Gizmos.color = Color.red;
        int segments = 40;
        float range = _jumpForbiddenAngleMax - _jumpForbiddenAngleMin;

        Vector3 prevPoint = _playerTransform.position;
        for (int i = 0; i <= segments; i++)
        {
            float a = _jumpForbiddenAngleMin + (range * i / segments);
            Vector3 point = _playerTransform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 8f;
            Gizmos.DrawLine(i == 0 ? _playerTransform.position : prevPoint, point);
            prevPoint = point;
        }
    }
}