using UnityEngine;
using System.Collections;

public class BossBase : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] protected float _maxHealth = 5000f; // MODIFIE - x10, cf. rescale global des degats/PV
    [SerializeField] protected float _moveSpeed = 3f;
    [SerializeField] protected float _xpValue = 200f;
    [SerializeField] protected int _goldValue = 50;

    // AJOUTÉ (2026-10-01) - retour utilisateur : pas de soin à la mort du boss 3. Case à décocher dans l'Inspector du
    // prefab concerné (cochée par défaut : les boss 1 et 2 gardent leur soin de 50 % des PV max du joueur).
    [Header("Récompense de mort")]
    [Tooltip("Si coché, le joueur est soigné de 50 % de ses PV max quand ce boss meurt. À décocher sur le boss 3.")]
    [SerializeField] protected bool _healPlayerOnDeath = true;

    // AJOUTÉ (2026-10-03) - effets d'arrivée et de mort des boss (voir BossFX). La mort n'est plus un simple
    // Destroy() : le boss reste en place quelques instants, inoffensif, se débat, s'embrase et rétrécit, puis explose.
    [Header("Effets d'arrivée et de mort (BossFX)")]
    [Tooltip("Décoche pour revenir à l'ancienne mort instantanée (le boss disparaît d'un coup).")]
    [SerializeField] protected bool _deathSequenceEnabled = true;
    [Tooltip("Durée (secondes) entre le dernier coup et l'explosion finale. Pour le boss final, garde cette durée inférieure au Victory Delay du GameManager (2 s par défaut). Les mini-boss invoqués utilisent 60 % de cette durée.")]
    [SerializeField] protected float _deathSequenceDuration = 1.4f;
    [Tooltip("Couleur dominante des effets d'arrivée et de mort de ce boss.")]
    [SerializeField] protected Color _deathFxColor = new Color(1f, 0.72f, 0.3f, 1f);
    [Tooltip("Court ralenti de tout le jeu au dernier coup (jamais pour un mini-boss).")]
    [SerializeField] protected bool _deathSlowMotion = true;
    [Tooltip("Onde de choc et colonne de lumière à l'apparition du boss.")]
    [SerializeField] protected bool _arrivalFxEnabled = true;

    [Header("Attaque")]
    [SerializeField] protected GameObject _projectilePrefab;
    [SerializeField] protected float _fireRate = 1f;
    [SerializeField] protected int _projectileCount = 8;
    [SerializeField] protected float _chargeCooldown = 5f;
    [SerializeField] protected float _chargeWindupDuration = 1f;

    [Header("Identité")]
    [SerializeField] protected string _bossName = "BOSS";

    // AJOUTE - clip a jouer pendant ce combat de boss specifique. Laisse vide
    // si tu veux garder la musique normale de la scene pour ce boss.
    [Header("Musique")]
    [SerializeField] protected AudioClip _bossMusicClip;

    [Header("Visuel Charge")]
    [SerializeField] protected Renderer _bodyRenderer;
    [SerializeField] protected string _emissionColorProperty = "_EmissionColor";
    [SerializeField] protected Color _manaGlowColor = new Color(0.3f, 0.6f, 1f);
    [SerializeField] protected float _maxEmissionIntensity = 3f;
    [SerializeField] protected float _windupAnimSpeed = 0.35f;
    [SerializeField] protected float _chargeAnimSpeed = 3f;

    [Header("Rotation")]
    [SerializeField] protected float _rotationSpeed = 500f;

    // MODIFIÉ (2026-10-03) - le télégraphe de charge n'est plus un cube rouge uni mais un couloir animé (ChargeLaneFX) :
    // visible sur toute sa longueur dès le début, remplissage qui avance avec le windup, bords nets, chevrons qui
    // défilent, anneau d'arrivée. Seule la COULEUR de ce champ est utilisée (la transparence est gérée par l'effet).
    [Header("Telegraphe de charge (couloir au sol)")]
    [SerializeField] protected Color _chargeTelegraphColor = new Color(1f, 0f, 0f, 0.4f);
    [SerializeField] protected float _chargeTelegraphHeightOffset = 0.05f;
    private ChargeLaneFX _chargeLane;

    [Header("Corps à corps")]
    [SerializeField] protected float _contactDamage = 300f; // MODIFIE - x10, cf. rescale global des degats/PV
    [SerializeField] protected float _contactDamageCooldown = 0.6f;

    [Header("Charge")]
    [SerializeField] protected float _chargeDamage = 450f; // MODIFIE - x10, cf. rescale global des degats/PV
    [SerializeField] protected float _chargeHitRadius = 2.5f;
    [SerializeField] protected float _chargeDistance = 16f;
    [SerializeField] protected float _chargeDuration = 0.55f;
    [SerializeField] protected float _minChargeDistance = 4f;
    [SerializeField] protected float _postChargeRecoveryDuration = 1.3f;

    private Vector3 _chargeStartPosition;
    private Vector3 _chargeEndPosition;
    private bool _isRecovering = false;
    private float _recoveryTimer = 0f;

    [Header("Caméra")]
    [SerializeField] protected float _cameraZoomMargin = 0f;

    [Header("Animator")]
    [SerializeField] protected string _isChargingParam = "IsCharging";
    [SerializeField] protected string _isWindingUpParam = "IsWindingUp";
    [SerializeField] protected string _isCruisingParam = "IsCruising";
    [SerializeField] protected string _isRecoveringParam = "IsRecovering";

    protected float _currentHealth;
    protected Transform _playerTransform;
    protected float _fireTimer = 0f;
    protected float _chargeTimer = 0f;
    protected bool _isCharging = false;
    protected Vector3 _chargeDirection;
    protected float _chargeDurationTimer = 0f;
    private bool _hasDealtChargeDamage = false;
    public float CameraZoomMargin => _cameraZoomMargin;
    public float MaxHealth => _maxHealth;
    public float CurrentHealth => _currentHealth;
    // AJOUTE (2026-09-15) - necessaire pour corriger le calcul d'XP des
    // mini-boss invoques par BossCorruptedSource (voir InitSummonedBoss) :
    // avant, SetXPValue(boss.MaxHealth * percent) utilisait _maxHealth, qui
    // n'est JAMAIS reduit par InitWithReducedHP() (seul _currentHealth
    // l'est) - donnait donc un mini-boss "a 30% de vie" mais recompense en
    // XP calculee sur 30% des PV COMPLETS du boss original (ex. 30% de
    // 20000 = 6000 XP, contre 420 XP attendu pour 30% de son _xpValue reel
    // de 1400) - retour utilisateur, "les mini boss donnent bien trop d'xp".
    public float XPValue => _xpValue;
    public bool IsSummoned { get; set; } = false;
    public bool RageDisabled { get; set; } = false;

    protected float _speedMultiplier = 1f;

    protected Animator _animator;
    protected MaterialPropertyBlock _propBlock;
    protected bool _isWindingUp = false;

    public void SetSpeedMultiplier(float multiplier)
    {
        _speedMultiplier = multiplier;
    }

    // AJOUTE (2026-09-25) - cible de déplacement / de visée : le clone spectral de Lyra tant qu'il existe,
    // sinon le joueur. Les dégâts, eux, restent toujours vérifiés sur le vrai joueur (_playerTransform).
    // Le test `!= null` d'Unity gère aussi un clone détruit.
    protected Transform AimTarget =>
        PlayerController.ActivePhantomClone != null ? PlayerController.ActivePhantomClone : _playerTransform;

    // AJOUTÉ (2026-10-04) - retour utilisateur : "quand un boss spawn trop rapidement, des fois... il meurt
    // instantanément". Cause trouvée : `_currentHealth` n'était initialisé que dans Start(), qui ne s'exécute
    // qu'AVANT le premier Update() du boss - donc PAS synchrone avec Instantiate(). Si quoi que ce soit lui inflige
    // des dégâts dans la MÊME frame que son apparition (une zone d'Aura/Boue déjà active à cet endroit, un orbital
    // qui le touche au moment exact où son collider s'active...), `_currentHealth` valait encore 0 (défaut C#) ->
    // `TakeDamage` le passait directement en négatif -> mort instantanée, aléatoirement, sans rapport apparent avec
    // les dégâts réellement infligés. `Awake()` s'exécute lui de façon synchrone à Instantiate(), AVANT que quoi
    // que ce soit d'autre puisse interagir avec le nouvel objet - élimine la fenêtre de course entièrement.
    protected virtual void Awake()
    {
        _currentHealth = _maxHealth;
    }

    protected virtual void Start()
    {
        _currentHealth = _maxHealth;

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
            _playerTransform = player.transform;

        _animator = GetComponentInChildren<Animator>();

        StartCoroutine(InitUI());
    }

    // AJOUTE (2026-09-26) - mode sans fin : multiplicateur de PV pour les séries de boss suivantes. Posé par WaveManager
    // juste après l'instanciation ; appliqué ici après le Start() (qui, selon le boss, fixe déjà ses PV en dur).
    private float _endlessHealthMultiplier = 1f;
    public void SetEndlessHealthMultiplier(float multiplier) { _endlessHealthMultiplier = Mathf.Max(1f, multiplier); }

    // AJOUTE - Ruée de boss : PV absolus imposés par l'équilibrage du mode (GameModes.RushBossHealth), quelles que soient
    // les PV du boss en classique. Même mécanisme d'application que le multiplicateur ci-dessus.
    private float _healthOverride = 0f;
    public void SetHealthOverride(float hp) { _healthOverride = Mathf.Max(0f, hp); }

    private IEnumerator InitUI()
    {
        yield return null;

        if (!IsSummoned)
        {
            if (_healthOverride > 0f)
            {
                _maxHealth = _healthOverride;
                _currentHealth = _maxHealth;
            }
            else if (_endlessHealthMultiplier > 1f)
            {
                _maxHealth *= _endlessHealthMultiplier;
                _currentHealth = _maxHealth;
            }
        }

        if (!IsSummoned)
        {
            GameUI.Instance.ShowBossHP(_bossName);

            // AJOUTÉ (2026-10-03) - onde de choc + colonne de lumière à l'apparition du boss.
            if (_arrivalFxEnabled)
                BossFX.PlayArrival(transform.position, ComputeFxRadius(), _deathFxColor);

            // AJOUTE - bascule sur la musique de ce boss, seulement pour un vrai
            // boss (pas une invocation) et seulement si un clip est assigne.
            if (MusicStarter.Instance != null && _bossMusicClip != null)
                MusicStarter.Instance.SwitchTo(_bossMusicClip);

            // AJOUTE - demarre le chrono du defi "Eclair" (vaincre un boss en
            // moins de 30 secondes). Utilise Time.time cote ChallengeManager,
            // jamais GameManager.RunTimer (qui ne progresse pas tant qu'un boss
            // est en vie).
            if (ChallengeManager.Instance != null)
                ChallengeManager.Instance.NotifyBossSpawned();
        }
    }

    protected virtual void Update()
    {
        if (_playerTransform == null) return;
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused) return;

        Vector3 pos = transform.position;
        if (Mathf.Abs(pos.y) > 0.001f)
        {
            pos.y = 0f;
            transform.position = pos;
        }

        _isWindingUp = !_isCharging && !_isRecovering && (_chargeCooldown - _chargeTimer) <= _chargeWindupDuration;

        HandleMovement();
        HandleShooting();
        HandleCharge();
        UpdateChargeTelegraph();
        UpdateAnimatorState();
    }

    protected virtual void UpdateAnimatorState()
    {
        if (_animator == null) return;

        _animator.SetBool(_isChargingParam, _isCharging);
        _animator.SetBool(_isWindingUpParam, _isWindingUp);
        _animator.SetBool(_isRecoveringParam, _isRecovering);
        _animator.SetBool(_isCruisingParam, !_isCharging && !_isWindingUp && !_isRecovering);
    }

    protected virtual void HandleMovement()
    {
        if (_isCharging || _isRecovering) return;
        if (_isWindingUp)
        {
            RotateTowards(AimTarget.position - transform.position);
            return;
        }

        Vector3 direction = (AimTarget.position - transform.position).normalized;
        Vector3 nextPosition = transform.position + direction * _moveSpeed * _speedMultiplier * Time.deltaTime;
        transform.position = MapBoundaryUtils.ClampToZone(nextPosition);
        RotateTowards(direction);
    }

    protected virtual void HandleShooting()
    {
        if (_isCharging || _isRecovering) return;

        float timeUntilCharge = _chargeCooldown - _chargeTimer;
        if (timeUntilCharge <= _chargeWindupDuration) return;

        _fireTimer += Time.deltaTime;
        if (_fireTimer >= 1f / _fireRate)
        {
            ShootRadial();
            _fireTimer = 0f;
        }
    }

    protected virtual void ShootRadial()
    {
        float angleStep = 360f / _projectileCount;

        for (int i = 0; i < _projectileCount; i++)
        {
            float angle = angleStep * i;
            Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;

            GameObject projectileGO = ObjectPool.Instance.Get("EnemyProjectile", transform.position, Quaternion.identity);
            if (projectileGO == null) continue;

            EnemyProjectile projectile = projectileGO.GetComponent<EnemyProjectile>();
            if (projectile != null)
                projectile.Init(direction);
        }
    }

    protected virtual void HandleCharge()
    {
        if (_isRecovering)
        {
            _recoveryTimer -= Time.deltaTime;
            if (_recoveryTimer <= 0f)
                _isRecovering = false;
            return;
        }

        _chargeTimer += Time.deltaTime;

        if (_chargeTimer >= _chargeCooldown && !_isCharging)
        {
            // CORRIGE (2026-09-24) - avant, la charge ne partait que si le joueur était à _minChargeDistance ou plus :
            // collé au boss, la charge n'était jamais lancée, _isWindingUp restait vrai (le boss ne bougeait ni ne tirait plus)
            // et il restait figé indéfiniment. La charge part maintenant toujours ; si le joueur est pile dessus,
            // elle part dans la direction où regarde le boss.
            {
                Vector3 toPlayer = AimTarget.position - transform.position;
                toPlayer.y = 0f;
                _isCharging = true;
                _isWindingUp = false;
                _chargeDirection = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : transform.forward;
                _chargeTimer = 0f;
                _chargeDurationTimer = _chargeDuration;
                _hasDealtChargeDamage = false;

                _chargeStartPosition = transform.position;
                _chargeEndPosition = MapBoundaryUtils.ClampToZone(transform.position + _chargeDirection * _chargeDistance);

                _fireTimer = 0f;

                if (_animator != null) _animator.speed = _chargeAnimSpeed;
            }
        }

        if (_isCharging)
        {
            float progress = 1f - Mathf.Clamp01(_chargeDurationTimer / _chargeDuration);
            transform.position = Vector3.Lerp(_chargeStartPosition, _chargeEndPosition, progress);
            RotateTowards(_chargeDirection);

            if (!_hasDealtChargeDamage)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
                if (distanceToPlayer <= _chargeHitRadius)
                {
                    HealthSystem playerHealth = _playerTransform.GetComponent<HealthSystem>();
                    // MODIFIE - meme source "boss" pour les degats de charge.
                    if (playerHealth != null)
                        playerHealth.TryTakeContactDamage(_chargeDamage * GameModes.EnemyDamageScale, _contactDamageCooldown, "boss");
                    _hasDealtChargeDamage = true;
                }
            }

            _chargeDurationTimer -= Time.deltaTime;
            if (_chargeDurationTimer <= 0f)
            {
                StopCharge();
            }
        }
    }

    protected void RotateTowards(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
    }

    protected virtual void StopCharge()
    {
        _isCharging = false;
        _isRecovering = true;
        _recoveryTimer = _postChargeRecoveryDuration;
        if (_animator != null) _animator.speed = 1f;
    }

    protected virtual void UpdateChargeTelegraph()
    {
        if (_isCharging)
        {
            DestroyTelegraphZone();
            return;
        }

        if (_isRecovering)
        {
            if (_animator != null) _animator.speed = 1f;
            UpdateGlowEffect(0f);
            DestroyTelegraphZone();
            return;
        }

        if (_isWindingUp)
        {
            if (_animator != null) _animator.speed = _windupAnimSpeed;

            float progress = 1f - Mathf.Clamp01((_chargeCooldown - _chargeTimer) / _chargeWindupDuration);
            UpdateGlowEffect(progress);
            UpdateTelegraphZone(progress);
        }
        else
        {
            if (_animator != null) _animator.speed = 1f;
            UpdateGlowEffect(0f);
            DestroyTelegraphZone();
        }
    }

    // MODIFIÉ (2026-10-03) - même contrat qu'avant (créer / mettre à jour / détruire le télégraphe de charge, les trois
    // restent virtuelles pour les boss qui ont leur propre signal), mais le visuel est maintenant un ChargeLaneFX :
    // le couloir est tracé sur toute la longueur réelle de la charge (même largeur et même longueur que la zone de dégâts)
    // et son remplissage suit la progression du windup.
    protected virtual void UpdateTelegraphZone(float progress)
    {
        if (_playerTransform == null) return;

        if (_chargeLane == null)
            CreateTelegraphZone();

        Vector3 dir = AimTarget.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
        dir.Normalize();

        Vector3 origin = transform.position;
        origin.y = _chargeTelegraphHeightOffset;

        _chargeLane.UpdateLane(origin, dir, _chargeDistance, _chargeHitRadius * 2f, progress);
    }

    protected virtual void CreateTelegraphZone()
    {
        _chargeLane = ChargeLaneFX.Spawn(_chargeTelegraphColor);
    }

    protected virtual void DestroyTelegraphZone()
    {
        if (_chargeLane != null)
        {
            Destroy(_chargeLane.gameObject);
            _chargeLane = null;
        }
    }

    // CORRIGÉ (2026-10-03) - cette lueur ne s'affichait jamais en jeu : les modèles importés de Tripo utilisent un
    // Shader Graph glTF ("Shader Graphs/glTF-pbrMetallicRoughness") dont les propriétés s'appellent baseColorFactor/
    // emissiveFactor, PAS _BaseColor/_EmissionColor (noms standard Unity, uniquement valables sur Universal Render
    // Pipeline/Lit) - _emissionColorProperty ne faisait donc rien, sans la moindre erreur. En plus, même sur le bon
    // nom, l'émission dépend d'un mot-clé/poids d'exposition pas garanti actif. On pulse maintenant aussi la
    // COULEUR DE BASE (toujours active, sur n'importe quel shader) en plus de la tentative d'émission d'origine
    // (gardée, inoffensive si elle ne fait rien) - teinte vers _manaGlowColor au lieu d'un blanc neutre, pour rester
    // dans l'esprit "mana" de ce signal plutôt qu'un flash générique.
    protected void UpdateGlowEffect(float progress)
    {
        if (_bodyRenderer == null) return;

        if (_propBlock == null)
            _propBlock = new MaterialPropertyBlock();

        _bodyRenderer.GetPropertyBlock(_propBlock);
        Color emission = _manaGlowColor * Mathf.Lerp(0f, _maxEmissionIntensity, progress);
        _propBlock.SetColor(_emissionColorProperty, emission);
        _propBlock.SetColor("emissiveFactor", emission);
        Color baseTint = Color.Lerp(Color.white, _manaGlowColor, progress * 0.8f) * Mathf.Lerp(1f, 1.8f, progress);
        baseTint.a = 1f;
        _propBlock.SetColor("_BaseColor", baseTint);
        _propBlock.SetColor("baseColorFactor", baseTint);
        _bodyRenderer.SetPropertyBlock(_propBlock);
    }

    // AJOUTE (2026-09-16) - DEBUG/TEST uniquement (voir DebugCheats.cs, F8) :
    // invincibilite du boss actif, pour observer tranquillement un combat/
    // pattern sans qu'il ne meure avant d'avoir vu ce qu'on veut tester (ex.
    // l'alternance des mini-boss invoques par le Boss 3). Statique - un seul
    // interrupteur global, coherent avec le fait qu'un seul boss est actif a
    // la fois. Jamais actif en dehors de l'Editeur/d'un build de developpement
    // (DebugCheats.cs se neutralise lui-meme hors de ces contextes).
    public static bool DebugInvincible = false;

    // CORRIGE (2026-09-26) - Destroy() n'est effectif qu'en fin de frame : plusieurs coups (zones, éclairs, cristaux...) reçus
    // dans la même frame après la mort rappelaient Die() -> mort comptée plusieurs fois (BossKillCount, OnBossDied).
    // Invisible en classique, mais en Choc des titans un seul boss tué déclenchait la victoire.
    private bool _deathTriggered = false;

    // AJOUTÉ (2026-10-04) - retour utilisateur : "l'indicateur de dégât on ne le voit pas car le boss est gros, et
    // l'affichage est sous le boss, donc le modèle 3D du boss passe devant l'affichage". `DamageNumberSpawner.Spawn`
    // ajoute +1,5 unité à la position reçue (pensé pour des ennemis normaux, bien plus petits) - sur un boss de
    // plusieurs unités de haut, ça tombe en plein milieu du torse/des jambes, caché par le modèle vu de la caméra
    // top-down du jeu. Calculé UNE SEULE FOIS (pas de GetComponent/bounds à chaque coup) à partir du haut réel du
    // modèle (`_bodyRenderer.bounds`, déjà assigné pour la lueur de charge) : les nombres flottent maintenant
    // au-dessus de la tête du boss, jamais cachés derrière lui vu d'en haut.
    private float? _damageNumberHeight;
    private float DamageNumberHeight()
    {
        if (_damageNumberHeight.HasValue) return _damageNumberHeight.Value;
        float topY = _bodyRenderer != null ? _bodyRenderer.bounds.max.y - transform.position.y : 3.5f;
        _damageNumberHeight = Mathf.Max(1.5f, topY + 0.6f) - 1.5f; // -1,5 pour compenser le +1,5 déjà ajouté par Spawn()
        return _damageNumberHeight.Value;
    }

    // AJOUTÉ (2026-10-06, retour utilisateur "boss pas assez résistants, trop de sources de dégâts cumulables") -
    // mesuré : un build avec les 10 types d'améliorations au palier max tue le Boss 1 (20 000 PV) en ~6,5 s, le
    // Boss 2 (50 000) en ~11,5 s, le Boss 3 (125 000) en ~19,4 s - chaque arme ajoute son propre DPS sans aucune
    // limite de cumul. Plafond de dégâts par FENÊTRE GLISSANTE courte, en % des PV MAX (proportionnel aux 3
    // paliers 20k/50k/125k, aucun réglage par boss) : un build faible/partiel reste toujours sous ce plafond et
    // n'est jamais affecté ; seul le PIC (beaucoup de sources qui tapent dans la même fenêtre) est écrêté.
    // MODIFIE (2026-10-07, retour utilisateur : "la limite de dégâts qu'on peut leur infliger est trop faible...
    // il y aura le système de niveau de difficulté donc si dans la meta progression on a plein de moyen d'avoir
    // plus de dégâts, ils servent surtout pour les moyens de difficulté qu'on va bientôt mettre en place") - le
    // plancher de ~30s minimum (0,5%) ne laissait plus aucune marge à un futur build méta très investi pour tuer
    // un boss plus vite qu'un build de base : le plafond devenait le vrai facteur limitant, pas l'équipement du
    // joueur. Relevé ×5 (plancher ramené à ~6s minimum) : un build faible/partiel reste sous ce plafond comme
    // avant, mais un build très optimisé (actuel OU futur via la méta-progression) peut enfin aller nettement
    // plus vite - à réajuster une fois le système de difficulté en place si besoin.
    [Header("Boss — Anti-cumul (plafond de dégâts simultanés)")]
    [SerializeField] private float _burstWindowDuration = 0.15f;
    [SerializeField] private float _maxBurstPercentPerWindow = 0.025f; // 2,5 % des PV max / fenêtre ≈ plancher ~6 s même en cumul extrême

    private float _burstWindowStart = -999f;
    private float _burstWindowDamageDealt = 0f;

    // CORRIGE (2026-10-07, retour utilisateur : "la limite est aussi compté pour les mini boss, est-ce que c'est
    // voulu ?") - non : _maxBurstPercentPerWindow est un pourcentage des PV MAX de CETTE instance, donc un
    // mini-boss (PV déjà réduits à 30% via InitWithReducedHP, voir BossCorruptedSource) héritait du même PLANCHER
    // DE TEMPS minimum (~6 s) qu'un vrai boss avec ses PV complets, alors qu'il est censé être un objectif
    // secondaire rapide à abattre pendant le combat principal. Les mini-boss ne sont plus jamais plafonnés.
    private float ClampBurstDamage(float damage)
    {
        if (IsSummoned) return damage;

        float now = Time.time;
        if (now - _burstWindowStart > _burstWindowDuration)
        {
            _burstWindowStart = now;
            _burstWindowDamageDealt = 0f;
        }

        float budget = _maxHealth * _maxBurstPercentPerWindow;
        float allowed = Mathf.Max(0f, budget - _burstWindowDamageDealt);
        float applied = Mathf.Min(damage, allowed);
        _burstWindowDamageDealt += applied;
        return applied;
    }

    public virtual void TakeDamage(float damage, Color color = default)
    {
        if (DebugInvincible) return;
        if (_deathTriggered) return;

        float applied = ClampBurstDamage(damage);
        if (applied <= 0f) return;

        _currentHealth -= applied;

        if (DamageNumberSpawner.Instance != null)
        {
            Color c = color == default ? DamageNumberSpawner.ColorCritical : color;
            Vector3 spawnPos = transform.position + Vector3.up * DamageNumberHeight();
            DamageNumberSpawner.Instance.Spawn(spawnPos, applied, c, transform, true);
        }

        // MODIFIE (2026-10-07) - un mini-boss invoqué (IsSummoned=true) a sa propre barre, voir GameUI.UpdateMiniBossHP.
        if (!IsSummoned)
            GameUI.Instance.UpdateBossHP(_currentHealth, _maxHealth);
        else
            GameUI.Instance.UpdateMiniBossHP(_currentHealth, _maxHealth);

        if (_currentHealth <= 0)
        {
            _deathTriggered = true;
            Die();
        }
    }

    // AJOUTE (2026-10-07, retour utilisateur : "si on tue le boss 3 alors qu'un mini boss est encore en vie, il
    // ne faut pas lancer le Victory panel tout de suite, il faut abattre le mini boss même si la source est
    // morte") - mise à mort immédiate, SANS passer par TakeDamage()/ClampBurstDamage() (le plafond anti-cumul
    // plus haut écrêterait un gros dégât ponctuel à ~0,5 % des PV max, donc ne tuerait pas le mini-boss d'un
    // coup). Réservé à ce genre de mise à mort scriptée (voir BossCorruptedSource.Die()), jamais aux dégâts du
    // joueur.
    public void ForceKill()
    {
        if (_deathTriggered) return;
        _deathTriggered = true;
        _currentHealth = 0f;
        Die();
    }

    protected virtual void Die()
    {
        DestroyTelegraphZone();

        // Choc des titans : l'XP ne sert à rien (build complet, pas de niveaux) -> aucune gemme.
        if (XPGemSpawner.Instance != null && !GameModes.IsTitans)
        {
            // CORRIGE (2026-09-16) - le pivot des boss est au sol (Y=0),
            // contrairement aux ennemis normaux : les gemmes d'XP spawnaient
            // donc quasiment sous la map, hors de portée du joueur (retour
            // utilisateur). Force Y=1,5, une hauteur ramassable normale.
            Vector3 gemSpawnPos = transform.position;
            gemSpawnPos.y = 1.5f;
            float xpReward = _xpValue;
            // Mini-boss invoqué : récompense = fraction de la barre d'XP du niveau ACTUEL, lue à la mort
            if (_xpBarFraction > 0f && XPSystem.Instance != null)
                xpReward = XPSystem.Instance.XPToNextLevel * _xpBarFraction;

            // AJOUTÉ (2026-10-04, suite 3) - retour utilisateur : "quand on tue un boss... je veux que ce soit
            // impressionnant et gratifiant visuellement" au lieu d'un simple tas de gemmes superposées. Un VRAI
            // boss (pas un mini-boss invoqué) a droit à la fontaine en cascade ; un mini-boss invoqué reste sur le
            // comportement normal (discret, cohérent avec un ennemi un peu plus gros).
            if (!IsSummoned)
                XPGemSpawner.Instance.SpawnBossGemBurst(gemSpawnPos, xpReward);
            else
                XPGemSpawner.Instance.SpawnGems(gemSpawnPos, xpReward);
        }

        GameManager.Instance.AddKill();
        MetaProgressionManager.Instance.AddRunGold(_goldValue);

        if (!IsSummoned && GameManager.Instance != null)
            GameManager.Instance.AddBossKill();

        if (!IsSummoned)
        {
            GameUI.Instance.HideBossHP();   // (Choc des titans : ne masque la barre qu'à la mort du dernier titan, voir GameUI)
            WaveManager.Instance.OnBossDied();

            // AJOUTE - revient a la musique normale de la scene a la mort du
            // boss, meme filtre !IsSummoned que le reste de ce bloc.
            // Choc des titans : seulement quand le dernier des 3 boss tombe.
            if (MusicStarter.Instance != null && (!GameModes.IsTitans || WaveManager.Instance.TitansRemaining <= 0))
                MusicStarter.Instance.RevertToDefault();

            // AJOUTE - notifie le systeme de defis (chrono "Eclair" + compteur
            // de boss vaincus pour "Double Chasse").
            if (ChallengeManager.Instance != null)
                ChallengeManager.Instance.NotifyBossDefeated();

            // MODIFIÉ (2026-10-01) - le soin n'est donné que si _healPlayerOnDeath est coché sur ce boss
            // (décoché sur le boss 3 : inutile de soigner le joueur juste avant l'écran de Victoire).
            if (_healPlayerOnDeath)
            {
                HealthSystem playerHP = GameObject.FindWithTag("Player")?.GetComponent<HealthSystem>();
                if (playerHP != null)
                {
                    float healAmount = playerHP.MaxHealth * 0.5f;
                    playerHP.Heal(healAmount);

                    if (DamageNumberSpawner.Instance != null)
                    {
                        DamageNumberSpawner.Instance.Spawn(
                            playerHP.transform.position,
                            healAmount,
                            Color.green
                        );
                    }
                }
            }
        }
        else
        {
            // AJOUTE (2026-10-07) - un mini-boss invoqué masque sa propre barre à sa mort (jamais celle du boss
            // principal, qui garde la sienne tant que la Source elle-même n'est pas morte).
            GameUI.Instance.HideMiniBossHP();
        }

        FinishDeath();
    }

    // AJOUTÉ (2026-10-03) - toute la logique de jeu de la mort (XP, or, victoire, musique, défis, soin) s'est déjà
    // exécutée dans Die() : ce qui suit ne concerne QUE le visuel. Le boss reste en place pendant la séquence mais
    // devient totalement inoffensif (script coupé, coroutines arrêtées, colliders désactivés, retiré de la liste des
    // cibles), puis BossFX le détruit au moment de l'explosion finale.
    private void FinishDeath()
    {
        if (!_deathSequenceEnabled)
        {
            Destroy(gameObject);
            return;
        }

        StopAllCoroutines();
        enabled = false;
        _contactDamageSuppressed = true;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.enabled = false;

        // Plus ciblable par les armes à visée automatique.
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t.CompareTag("Enemy")) t.gameObject.tag = "Untagged";

        float duration = IsSummoned ? _deathSequenceDuration * 0.6f : _deathSequenceDuration;
        BossFX.PlayDeath(
            transform,
            ComputeFxRadius(),
            _deathFxColor,
            duration,
            _deathSlowMotion && !IsSummoned,
            _bodyRenderer,
            _emissionColorProperty,
            Color.Lerp(_deathFxColor, Color.white, 0.5f));
    }

    // Rayon des effets (arrivée / mort) déduit automatiquement de la taille visible du boss : pas de réglage par boss.
    protected float ComputeFxRadius()
    {
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            bounds.Encapsulate(r.bounds);

        float radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z), 1.5f, 7f);
        return IsSummoned ? radius * 0.7f : radius;
    }

    protected virtual void OnDestroy()
    {
        DestroyTelegraphZone();
    }

    // AJOUTE (2026-09-16) - permet a une sous-classe de suspendre temporairement
    // les degats de CONTACT generiques (ci-dessous) pendant un deplacement
    // rapide deja telegraphie par ailleurs (ex. la Frappe du Boss 3, qui
    // affiche un cercle rouge au point d'ARRIVEE puis inflige ses propres
    // degats d'impact explicites une fois sur place) - retour utilisateur :
    // sans ca, le corps du boss inflige AUSSI des degats de contact tout le
    // long de sa trajectoire (avant meme d'arriver), donc une zone de degats
    // reelle plus large que ce que montre le seul cercle d'avertissement au
    // point B. Chaque attaque doit avoir un tell fiable (regle deja etablie
    // sur Golem/Sanglier/Cerf/ce boss meme, voir l'en-tete du fichier) - un
    // trajet non-telegraphie qui blesse casse cette regle.
    protected bool _contactDamageSuppressed = false;

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (_contactDamageSuppressed) return;
        if (other.CompareTag("Player"))
        {
            HealthSystem health = other.GetComponent<HealthSystem>();
            // MODIFIE - precise "boss" comme source du degat, pour le message
            // d'ambiance contextuel du Game Over.
            if (health != null)
                health.TryTakeContactDamage(_contactDamage * GameModes.EnemyDamageScale, _contactDamageCooldown, "boss");
        }
    }

    protected virtual void OnTriggerStay(Collider other)
    {
        if (_contactDamageSuppressed) return;
        if (other.CompareTag("Player"))
        {
            HealthSystem health = other.GetComponent<HealthSystem>();
            if (health != null)
                health.TryTakeContactDamage(_contactDamage * GameModes.EnemyDamageScale, _contactDamageCooldown, "boss");
        }
    }

    public void InitWithReducedHP(float percent)
    {
        _currentHealth = _maxHealth * percent;
    }

    // PV absolus (mini-boss invoqués en Choc des titans : leurs PV suivent ceux des titans, pas ceux du classique).
    public void InitWithHealth(float health)
    {
        _currentHealth = Mathf.Max(1f, health);
    }

    public void SetXPValue(float value)
    {
        _xpValue = value;
    }

    // AJOUTE (2026-09-24) - > 0 : l'XP donnée à la mort vaut cette fraction de la barre d'XP du niveau en cours
    // (au lieu de _xpValue). Utilisé par les mini-boss du boss 3 (0.5 = la moitié de la barre).
    private float _xpBarFraction = 0f;
    public void SetXPBarFraction(float fraction)
    {
        _xpBarFraction = fraction;
    }
}