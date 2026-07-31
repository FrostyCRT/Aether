using UnityEngine;

public class EnemyKaiju : EnemyBase
{
    private enum AttackPhase { Idle, Windup, Combo, Recovery }

    [Header("Attaque frontale — Attack")]
    [SerializeField] private float _attackRange = 2.8f;
    [SerializeField] private float _attackConeAngle = 75f; // cône devant lui, PAS omnidirectionnel
    [SerializeField] private float _attackDamage = 18f;
    [SerializeField] private float _attackWindup = 0.35f;   // rapide, punit le facetank immédiat
    [SerializeField] private float _attackRecovery = 0.4f;
    [SerializeField] private float _attackCooldown = 2.5f;

    [Header("Balayage de queue — Attack Queue (combo, pas indépendant)")]
    [SerializeField] private float _tailSweepRadius = 4.2f; // 360°, plus large que le cône
    [SerializeField] private float _tailSweepDamage = 26f;
    [SerializeField] private float _tailSweepWindup = 0.85f; // plus lent = plus lisible, en échange d'être plus fort
    [SerializeField] private float _comboWindow = 1.3f;      // fenêtre pour rester au contact et déclencher le combo
    [SerializeField] private float _tailSweepRecovery = 0.6f;

    [Header("Visuel de charge (tell rouge/orange, distinct du bleu du Golem)")]
    [SerializeField] private Renderer _bodyRenderer;
    [SerializeField] private string _emissionColorProperty = "_EmissionColor";
    [SerializeField] private Color _rageGlowColor = new Color(1f, 0.35f, 0.05f);
    [SerializeField] private float _maxEmissionIntensity = 3.5f;

    private Animator _animator;
    private Transform _bruteTarget;
    private MaterialPropertyBlock _propBlock;

    private AttackPhase _phase = AttackPhase.Idle;
    private float _phaseTimer = 0f;
    private float _attackCooldownTimer = 0f;
    private bool _comboAvailable = false;
    private float _comboTimer = 0f;

    // Même pattern que EnemyTank : override du hook, pas de 2e Update()
    protected override void OnEnemyUpdate()
    {
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();

        // _playerTransform est private dans EnemyBase (leçon connue) -> référence locale dupliquée
        if (_bruteTarget == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) _bruteTarget = player.transform;
        }
        if (_bruteTarget == null) return;

        if (_attackCooldownTimer > 0f)
            _attackCooldownTimer -= Time.deltaTime;

        float distance = Vector3.Distance(transform.position, _bruteTarget.position);

        switch (_phase)
        {
            case AttackPhase.Idle:
                HandleComboWindow(distance);

                if (distance <= _attackRange && _attackCooldownTimer <= 0f)
                    StartWindup();
                break;

            case AttackPhase.Windup:
                _phaseTimer += Time.deltaTime;
                UpdateGlow(_phaseTimer / _attackWindup);
                if (_phaseTimer >= _attackWindup)
                    ExecuteFrontalAttack();
                break;

            case AttackPhase.Combo:
                _phaseTimer += Time.deltaTime;
                UpdateGlow(_phaseTimer / _tailSweepWindup);
                if (_phaseTimer >= _tailSweepWindup)
                    ExecuteTailSweep(distance);
                break;

            case AttackPhase.Recovery:
                _phaseTimer -= Time.deltaTime;
                if (_phaseTimer <= 0f)
                    _phase = AttackPhase.Idle;
                break;
        }
    }

    private void HandleComboWindow(float distance)
    {
        if (!_comboAvailable) return;

        _comboTimer -= Time.deltaTime;

        // Le joueur est resté au contact après l'Attack -> le combo se déclenche
        if (distance <= _tailSweepRadius)
        {
            _comboAvailable = false;
            StartTailSweepWindup();
            return;
        }

        if (_comboTimer <= 0f)
            _comboAvailable = false; // le joueur a reculé à temps, pas de combo
    }

    private void StartWindup()
    {
        _phase = AttackPhase.Windup;
        _phaseTimer = 0f;
        if (_animator != null)
            _animator.SetTrigger("Attack");
    }

    private void ExecuteFrontalAttack()
    {
        UpdateGlow(0f);

        float distance = Vector3.Distance(transform.position, _bruteTarget.position);
        if (distance <= _attackRange)
        {
            Vector3 toPlayer = (_bruteTarget.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, toPlayer);

            if (angle <= _attackConeAngle * 0.5f)
            {
                HealthSystem playerHealth = _bruteTarget.GetComponent<HealthSystem>();
                if (playerHealth != null)
                    playerHealth.TakeDamage(_attackDamage);
            }
        }

        _attackCooldownTimer = _attackCooldown;
        _comboAvailable = true;
        _comboTimer = _comboWindow;
        _phase = AttackPhase.Recovery;
        _phaseTimer = _attackRecovery;
    }

    private void StartTailSweepWindup()
    {
        _phase = AttackPhase.Combo;
        _phaseTimer = 0f;
        if (_animator != null)
            _animator.SetTrigger("AttackQueue");
    }

    private void ExecuteTailSweep(float distance)
    {
        UpdateGlow(0f);

        // 360°, contrairement au cône de l'Attack — punit d'être resté au contact, peu importe l'angle
        if (distance <= _tailSweepRadius)
        {
            HealthSystem playerHealth = _bruteTarget.GetComponent<HealthSystem>();
            if (playerHealth != null)
                playerHealth.TakeDamage(_tailSweepDamage);
        }

        _phase = AttackPhase.Recovery;
        _phaseTimer = _tailSweepRecovery;
    }

    private void UpdateGlow(float progress)
    {
        if (_bodyRenderer == null) return;
        if (_propBlock == null)
            _propBlock = new MaterialPropertyBlock();

        _bodyRenderer.GetPropertyBlock(_propBlock);
        Color emission = _rageGlowColor * Mathf.Lerp(0f, _maxEmissionIntensity, progress);
        _propBlock.SetColor(_emissionColorProperty, emission);
        _bodyRenderer.SetPropertyBlock(_propBlock);
    }

    // Reset au niveau run, pas juste au niveau instance — TODO: appeler EnemyKaiju.ResetRunState()
    // depuis GameManager au début d'une run (je n'ai pas encore ce script)
    private static bool _hasRoaredThisRun = false;

    public static void ResetRunState()
    {
        _hasRoaredThisRun = false;
    }

    // Séparé du OnEnable() privé d'EnemyBase — Unity appelle les deux indépendamment
    // (ce ne sont pas des méthodes virtuelles, donc pas d'override classique possible ici).
    // À vérifier une fois en jeu avec un Debug.Log de chaque côté, je préfère te le dire
    // plutôt que de l'affirmer à 100% sans que tu l'aies vu tourner sur CE projet précis.
    protected override void OnEnable() // MODIFIÉ — était private void OnEnable()
    {
        base.OnEnable(); // AJOUTÉ — indispensable, sinon _currentHealth (et l'abonnement à OnPhantomDestroyed) ne sont plus jamais initialisés

        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();

        if (_animator == null) return;

        _animator.Rebind();
        _animator.Update(0f);

        if (!_hasRoaredThisRun)
        {
            _hasRoaredThisRun = true;
            _animator.Play("Roar", 0, 0f);
        }
        else
        {
            _animator.Play("Walk", 0, 0f);
        }

        _phase = AttackPhase.Idle;
        _phaseTimer = 0f;
        _attackCooldownTimer = 0f;
        _comboAvailable = false;
    }
}
