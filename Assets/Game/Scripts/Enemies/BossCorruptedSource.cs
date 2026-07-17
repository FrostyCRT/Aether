using UnityEngine;
using System.Collections;

public class BossCorruptedSource : BossBase
{
    [Header("Source — Cristaux Orbitaux")]
    [SerializeField] private GameObject _crystalPrefab;
    [SerializeField] private int _crystalCount = 6;
    [SerializeField] private float _crystalOrbitRadius = 4f;
    [SerializeField] private float _crystalOrbitSpeed = 90f;
    [SerializeField] private float _crystalFireRate = 0.5f;

    [Header("Source — Vague de Ralentissement")]
    [SerializeField] private float _slowWaveCooldown = 10f;
    [SerializeField] private float _slowDuration = 3f;
    [SerializeField] private float _slowMultiplier = 0.4f;
    [SerializeField] private float _slowWaveRadius = 8f;

    [Header("Source — Invocation")]
    [SerializeField] private GameObject _miniBoss1Prefab;
    [SerializeField] private GameObject _miniBoss2Prefab;
    [SerializeField] private float _summonCooldown = 25f;

    [Header("Source — Implosion")]
    [SerializeField] private float _implosionCooldown = 20f;
    [SerializeField] private float _implosionPullForce = 15f;
    [SerializeField] private float _implosionRadius = 10f;
    [SerializeField] private float _implosionDamage = 40f;
    [SerializeField] private GameObject _implosionWarningPrefab;

    [Header("Phase 2")]
    [SerializeField] private float _phase2Threshold = 0.5f;

    private float _slowWaveTimer = 0f;
    private float _summonTimer = 0f;
    private float _implosionTimer = 0f;
    private float _crystalAngle = 0f;
    private float _crystalFireTimer = 0f;

    private bool _isPhase2 = false;
    private bool _isImplosionActive = false;
    private bool _lastSummonWasBoss1 = false;

    private Vector3 _wanderDirection = Vector3.zero;
    private float _wanderTimer = 0f;
    private float _wanderChangeCooldown = 2f;

    private GameObject[] _crystals;
    private GameObject _currentWarning = null;

    // Caching des composants du joueur pour optimiser l'implosion
    private PlayerController _cachedPlayerController;
    private Rigidbody _cachedPlayerRigidbody;

    protected override void Start()
    {
        base.Start();
        _bossName = "La Source Corrompue";
        _maxHealth = 5000f;
        _moveSpeed = 0f;
        _currentHealth = _maxHealth;

        // Cache des composants du joueur dès le départ
        if (_playerTransform != null)
        {
            _cachedPlayerController = _playerTransform.GetComponent<PlayerController>();
            _cachedPlayerRigidbody = _playerTransform.GetComponent<Rigidbody>();
        }

        SpawnCrystals();
    }

    protected override void Update()
    {
        if (_playerTransform == null) return;
        if (GameManager.Instance == null) return;

        // CORRECTION BUG : Gestion de la pause et de fin de partie
        if (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused) return;

        HandleMovement();
        UpdateWander();
        HandleCrystalOrbit();
        HandleCrystalShooting();
        HandleSlowWave();
        HandleSummon();
        HandleImplosion();
        CheckPhase2();
    }

    protected override void HandleMovement()
    {
        if (!_isPhase2) return;
        if (_isImplosionActive) return;
        transform.position += _wanderDirection * _moveSpeed * Time.deltaTime;
    }

    private void SpawnCrystals()
    {
        if (_crystalPrefab == null) return;
        _crystals = new GameObject[_crystalCount];
        for (int i = 0; i < _crystalCount; i++)
        {
            float angle = (360f / _crystalCount) * i * Mathf.Deg2Rad;
            Vector3 pos = transform.position + new Vector3(
                Mathf.Cos(angle) * _crystalOrbitRadius, 0f,
                Mathf.Sin(angle) * _crystalOrbitRadius);
            _crystals[i] = Instantiate(_crystalPrefab, pos, Quaternion.identity);
            _crystals[i].transform.SetParent(transform);
        }
    }

    private void HandleCrystalOrbit()
    {
        if (_crystals == null) return;
        _crystalAngle += _crystalOrbitSpeed * Time.deltaTime;
        float angleStep = 360f / _crystalCount;
        for (int i = 0; i < _crystals.Length; i++)
        {
            if (_crystals[i] == null) continue;
            float angle = (_crystalAngle + angleStep * i) * Mathf.Deg2Rad;
            _crystals[i].transform.localPosition = new Vector3(
                Mathf.Cos(angle) * _crystalOrbitRadius, 0f,
                Mathf.Sin(angle) * _crystalOrbitRadius);
        }
    }

    private void HandleCrystalShooting()
    {
        if (_crystals == null) return;
        _crystalFireTimer += Time.deltaTime;
        if (_crystalFireTimer < 1f / _crystalFireRate) return;
        _crystalFireTimer = 0f;

        foreach (GameObject crystal in _crystals)
        {
            if (crystal == null) continue;

            Vector3 dirToPlayer = (_playerTransform.position - crystal.transform.position).normalized;
            dirToPlayer.y = 0f;

            // CORRECTION PERFORMANCE : Remplacement de l'Instantiate par le Pool global
            GameObject projectileGO = ObjectPool.Instance.Get("EnemyProjectile", crystal.transform.position, Quaternion.identity);
            if (projectileGO == null) continue;

            EnemyProjectile projectile = projectileGO.GetComponent<EnemyProjectile>();
            if (projectile != null)
                projectile.Init(dirToPlayer);
        }
    }

    private void HandleSlowWave()
    {
        _slowWaveTimer += Time.deltaTime;
        if (_slowWaveTimer < _slowWaveCooldown) return;
        _slowWaveTimer = 0f;

        Collider[] hits = Physics.OverlapSphere(transform.position, _slowWaveRadius);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                if (_cachedPlayerController != null)
                    StartCoroutine(SlowPlayer(_cachedPlayerController));
            }
        }
    }

    private IEnumerator SlowPlayer(PlayerController player)
    {
        player.SetSpeedMultiplier(_slowMultiplier);
        yield return new WaitForSeconds(_slowDuration);
        player.SetSpeedMultiplier(1f);
    }

    private void HandleSummon()
    {
        _summonTimer += Time.deltaTime;
        if (_summonTimer < _summonCooldown) return;
        _summonTimer = 0f;

        bool spawnBoss1 = !_lastSummonWasBoss1;
        _lastSummonWasBoss1 = spawnBoss1;

        GameObject prefabToSpawn = spawnBoss1 ? _miniBoss1Prefab : _miniBoss2Prefab;
        if (prefabToSpawn == null) return;

        Vector3 playerPos = _playerTransform.position;
        Vector3 awayFromPlayer = (transform.position - playerPos).normalized;
        Vector3 spawnPos = playerPos + awayFromPlayer * 10f;

        GameObject mini = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
        BossBase boss = mini.GetComponent<BossBase>();
        if (boss != null)
        {
            boss.IsSummoned = true;
            StartCoroutine(InitSummonedBoss(boss, 0.3f));
        }
    }

    private IEnumerator InitSummonedBoss(BossBase boss, float percent)
    {
        yield return null;
        if (boss != null)
        {
            boss.InitWithReducedHP(percent);
            boss.transform.localScale = Vector3.one * 0.6f;
            boss.SetXPValue(boss.MaxHealth * 0.3f);
            boss.RageDisabled = true;
        }
    }

    private void HandleImplosion()
    {
        if (_isImplosionActive) return;
        _implosionTimer += Time.deltaTime;
        if (_implosionTimer < _implosionCooldown) return;
        _implosionTimer = 0f;
        StartCoroutine(ImplosionSequence());
    }

    private IEnumerator ImplosionSequence()
    {
        _isImplosionActive = true;

        if (_implosionWarningPrefab != null)
        {
            _currentWarning = Instantiate(_implosionWarningPrefab, transform.position, Quaternion.identity);
            _currentWarning.transform.localScale = Vector3.zero;
        }

        float pullDuration = 1.5f;
        float elapsed = 0f;
        float minDistance = 3f;

        while (elapsed < pullDuration)
        {
            // Sécurité si le jeu se met en pause pendant la coroutine
            if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            {
                yield return null;
                continue;
            }

            elapsed += Time.deltaTime;

            if (_currentWarning != null)
            {
                _currentWarning.transform.position = transform.position;
                float scale = Mathf.Lerp(0f, _implosionRadius * 2f, elapsed / pullDuration);
                _currentWarning.transform.localScale = new Vector3(scale, 0.05f, scale);
            }

            // CORRECTION PERFORMANCE : Utilisation des composants mis en cache (Plus aucun GetComponent en boucle !)
            if (_playerTransform != null && _cachedPlayerController != null && _cachedPlayerRigidbody != null)
            {
                float dist = Vector3.Distance(_playerTransform.position, transform.position);
                if (dist > minDistance)
                {
                    if (!_cachedPlayerController.IsDashing)
                    {
                        Vector3 pullDir = (transform.position - _playerTransform.position).normalized;
                        _cachedPlayerRigidbody.MovePosition(
                            _playerTransform.position + pullDir * _implosionPullForce * Time.deltaTime);
                    }
                }
            }
            yield return null;
        }

        if (_currentWarning != null) Destroy(_currentWarning);

        Collider[] hits = Physics.OverlapSphere(transform.position, _implosionRadius);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                HealthSystem health = hit.GetComponent<HealthSystem>();
                if (health != null) health.TakeDamage(_implosionDamage);
            }
        }

        _isImplosionActive = false;
    }

    private void CheckPhase2()
    {
        if (_isPhase2) return;
        if (_currentHealth / _maxHealth > _phase2Threshold) return;

        _isPhase2 = true;
        _moveSpeed = 3f;
        _crystalFireRate *= 2f;
        _slowWaveCooldown *= 0.5f;
    }

    private void UpdateWander()
    {
        if (!_isPhase2) return;
        _wanderTimer += Time.deltaTime;
        if (_wanderTimer >= _wanderChangeCooldown)
        {
            _wanderTimer = 0f;
            float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            _wanderDirection = new Vector3(Mathf.Cos(randomAngle), 0f, Mathf.Sin(randomAngle));
        }
    }

    protected override void Die()
    {
        if (_currentWarning != null) Destroy(_currentWarning);
        if (_crystals != null)
            foreach (GameObject crystal in _crystals)
                if (crystal != null) Destroy(crystal);
        base.Die();
    }
}