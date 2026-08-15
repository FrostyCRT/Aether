using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Boss final — La Source Corrompue.
/// Design : un colossal serpent de mana corrompue, ANCRÉ à son rift (ne "marche" jamais
/// classiquement). Toutes ses attaques suivent une machine à séquence (une seule à la fois,
/// avec windup + fenêtre "safe" entre chaque) au lieu de timers indépendants qui se chevauchaient
/// dans la version précédente. Chaque attaque a un tell visuel, cohérent avec la règle établie
/// sur Golem/Sanglier/Cerf.
///
/// Contrainte animation (Mesh2Motion) : seulement 6 clips disponibles — Idle, SideWinding,
/// Death, Coiled, Bite, Dance. Chacun est réutilisé pour plusieurs attaques distinctes plutôt
/// que d'avoir un clip dédié par attaque (budget anim limité, cf. deadline 1 mois).
/// </summary>
public class BossCorruptedSource : BossBase
{
    private enum AttackType { CrystalPulse, RearingStrike, CorruptionWave, Summon, Implosion }

    // NOTE : Le champ _animator est déjà déclaré et géré dans BossBase. 
    // Le redéclarer ici provoquait une erreur de sérialisation (CS0108 / champ dupliqué).

    [Header("Ancrage / Rift")]
    [SerializeField] private float _anchorLeashRadius = 6f; // distance max avant de devoir se repositionner

    [Header("Cristaux orbitaux — Pulse")]
    [SerializeField] private GameObject _crystalPrefab;
    [SerializeField] private int _crystalCount = 6;
    [SerializeField] private float _crystalOrbitRadius = 4f;
    [SerializeField] private float _crystalOrbitSpeedIdle = 20f;
    [SerializeField] private float _crystalOrbitSpeedCharging = 180f;
    [SerializeField] private float _pulseWindupDuration = 1.2f;
    [SerializeField] private int _pulseVolleyCount = 4;
    [SerializeField] private float _pulseVolleyInterval = 0.25f;

    [Header("Frappe (Coiled -> Slither -> Bite)")]
    [SerializeField] private float _rearWindupDuration = 1.4f;
    [SerializeField] private float _strikeLungeDistance = 9f;
    [SerializeField] private float _strikeLungeSpeed = 22f;
    [SerializeField] private float _strikeImpactRadius = 2.5f;
    [SerializeField] private float _strikeDamage = 30f;
    [SerializeField] private GameObject _strikeTelegraphPrefab; // cercle rouge au sol, position d'impact

    [Header("Vague de Corruption (ex Slow Wave)")]
    [SerializeField] private float _waveExpandSpeed = 6f;
    [SerializeField] private float _waveMaxRadius = 9f;
    [SerializeField] private float _slowDuration = 3f;
    [SerializeField] private float _slowMultiplier = 0.4f;
    [SerializeField] private GameObject _waveRingPrefab;

    [Header("Repositionnement Slither — Phase 2 uniquement")]
    [SerializeField] private float _diveCheckInterval = 4f;
    [SerializeField] private float _repositionTelegraphDuration = 0.6f; // fissures avant le départ
    [SerializeField] private float _repositionTravelDuration = 1f;
    [SerializeField] private GameObject _crackTrailPrefab; // fissures qui suivent le trajet, purement visuel
    [SerializeField] private GameObject _resurfaceBurstPrefab;
    [SerializeField] private float _resurfaceBurstRadius = 3f;
    [SerializeField] private float _resurfaceBurstDamage = 15f;

    [Header("Invocation d'échos corrompus")]
    [SerializeField] private GameObject _miniBoss1Prefab;
    [SerializeField] private GameObject _miniBoss2Prefab;
    [SerializeField] private float _summonWindupDuration = 2f;
    [SerializeField] private GameObject _riftPortalPrefab;
    [SerializeField] private float _summonedVisualScale = 0.75f; // MODIFIÉ — remplace _miniBossVisualChildName, était 0.6f codé en dur

    [Header("Implosion — signature Phase 2 (Dance)")]
    [SerializeField] private float _implosionPullDuration = 1.5f;
    [SerializeField] private float _implosionPullForce = 15f;
    [SerializeField] private float _implosionRadius = 10f;
    [SerializeField] private float _implosionDamage = 40f;
    [SerializeField] private GameObject _implosionWarningPrefab;

    [Header("Rythme du pattern")]
    [SerializeField] private float _minSafeWindow = 1.5f;
    [SerializeField] private float _maxSafeWindow = 2.5f;

    [Header("Rotation")]
    [SerializeField] private float _bodyRotationSpeed = 150f; // degrés/seconde

    [Header("Phase 2")]
    [SerializeField] private float _phase2Threshold = 0.5f;

    private bool _isPhase2 = false;
    private bool _isAttacking = false;
    private bool _isRepositioning = false;
    private bool _lockRotation = false; // true pendant les mouvements directionnels (Frappe, repositionnement)

    private GameObject[] _crystals;
    private float _crystalAngle = 0f;

    private PlayerController _cachedPlayerController;
    private Rigidbody _cachedPlayerRigidbody;

    private List<AttackType> _phase1Pool;
    private List<AttackType> _phase2Pool;
    private AttackType _lastAttack = AttackType.CrystalPulse;

    private GameObject _activeMiniBossInstance = null;
    private bool MiniBossAlive => _activeMiniBossInstance != null;

    protected override void Start()
    {
        base.Start();
        _bossName = "La Source Corrompue";
        _maxHealth = 12500f;
        _moveSpeed = 0f; // ancrée au rift — ne "marche" jamais au sens classique
        _currentHealth = _maxHealth;

        if (_playerTransform != null)
        {
            _cachedPlayerController = _playerTransform.GetComponent<PlayerController>();
            _cachedPlayerRigidbody = _playerTransform.GetComponent<Rigidbody>();
        }

        _phase1Pool = new List<AttackType> { AttackType.CrystalPulse, AttackType.RearingStrike, AttackType.CorruptionWave };
        _phase2Pool = new List<AttackType> { AttackType.CrystalPulse, AttackType.RearingStrike, AttackType.CorruptionWave, AttackType.Summon, AttackType.Implosion };

        SpawnCrystals();
        StartCoroutine(AttackPatternLoop());
        StartCoroutine(LeashWatcher());
    }

    protected override void Update()
    {
        if (_playerTransform == null) return;
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused) return;

        HandleCrystalOrbitVisual();
        CheckPhase2();
        RotateTowardsPlayer();

        if (MiniBossAlive && _activeMiniBossInstance.activeInHierarchy == false)
            _activeMiniBossInstance = null;
    }

    // Pas de mouvement classique : le seul déplacement du corps passe par le repositionnement Slither.
    protected override void HandleMovement() { }

    // Rotation continue vers le joueur, sauf pendant les mouvements directionnels (Frappe, repositionnement)
    // où la direction est volontairement figée au moment du windup — cohérent avec le principe
    // d'attaque "committed" déjà établi sur le Cerf. Rotation en Slerp continu, jamais de bascule
    // binaire par seuil (cf. le bug de tremblement identifié sur les autres ennemis).
    private void RotateTowardsPlayer()
    {
        if (_lockRotation) return;
        if (_playerTransform == null) return;

        Vector3 dir = _playerTransform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, _bodyRotationSpeed * Time.deltaTime);
    }

    // ---------- MACHINE À SÉQUENCE ----------
    private IEnumerator AttackPatternLoop()
    {
        yield return new WaitForSeconds(2f); // laisse le temps au joueur de voir le boss émerger

        while (_currentHealth > 0f)
        {
            if (GameManager.Instance == null || GameManager.Instance.IsGameOver)
                yield break;

            if (GameManager.Instance.IsPaused || _isRepositioning)
            {
                yield return null;
                continue;
            }

            List<AttackType> pool = _isPhase2 ? _phase2Pool : _phase1Pool;
            AttackType next = ChooseNextAttack(pool);

            yield return StartCoroutine(RunAttack(next));

            float safeWindow = Random.Range(_minSafeWindow, _maxSafeWindow);
            yield return new WaitForSeconds(safeWindow);
        }
    }

    private AttackType ChooseNextAttack(List<AttackType> pool)
    {
        AttackType choice;
        int guard = 0;
        do
        {
            choice = pool[Random.Range(0, pool.Count)];
            guard++;
        } while (choice == _lastAttack && guard < 10); // évite deux fois la même attaque d'affilée

        if (choice == AttackType.Implosion && MiniBossAlive)
            choice = AttackType.CrystalPulse;
        if (choice == AttackType.Summon && MiniBossAlive)
            choice = AttackType.RearingStrike;

        _lastAttack = choice;
        return choice;
    }

    private IEnumerator RunAttack(AttackType type)
    {
        _isAttacking = true;
        switch (type)
        {
            case AttackType.CrystalPulse: yield return StartCoroutine(CrystalPulseAttack()); break;
            case AttackType.RearingStrike: yield return StartCoroutine(RearingStrikeAttack()); break;
            case AttackType.CorruptionWave: yield return StartCoroutine(CorruptionWaveAttack()); break;
            case AttackType.Summon: yield return StartCoroutine(SummonAttack()); break;
            case AttackType.Implosion: yield return StartCoroutine(ImplosionAttack()); break;
        }
        _isAttacking = false;
    }

    // ---------- CRYSTAL PULSE ----------
    // Coiled (charge) puis plusieurs salves synchronisées successives, au lieu d'un tir unique
    // trop faible. Chaque salve retire la position ACTUELLE du joueur (pas figée au windup),
    // donc rester mobile pendant l'attaque ne désamorce plus complètement les tirs suivants.
    private IEnumerator CrystalPulseAttack()
    {
        if (_animator != null) _animator.SetBool("IsCoiling", true);

        yield return new WaitForSeconds(_pulseWindupDuration);

        if (_animator != null) _animator.SetBool("IsCoiling", false);

        for (int volley = 0; volley < _pulseVolleyCount; volley++)
        {
            if (_crystals != null)
            {
                foreach (GameObject crystal in _crystals)
                {
                    if (crystal == null) continue;
                    Vector3 dirToPlayer = (_playerTransform.position - crystal.transform.position).normalized;
                    dirToPlayer.y = 0f;

                    GameObject projectileGO = ObjectPool.Instance.Get("EnemyProjectile", crystal.transform.position, Quaternion.identity);
                    if (projectileGO == null) continue;

                    EnemyProjectile projectile = projectileGO.GetComponent<EnemyProjectile>();
                    if (projectile != null) projectile.Init(dirToPlayer);
                }
            }

            yield return new WaitForSeconds(_pulseVolleyInterval);
        }

        yield return new WaitForSeconds(0.3f);
    }

    // ---------- FRAPPE (Coiled -> Slither -> Bite) ----------
    // Se love (tell), vise la position du joueur au moment du windup (comme le saut du Cerf),
    // glisse rapidement jusqu'à l'impact, puis mord.
    private IEnumerator RearingStrikeAttack()
    {
        _lockRotation = true;
        if (_animator != null) _animator.SetBool("IsCoiling", true);

        Vector3 targetPos = _playerTransform.position;
        Vector3 dir = (targetPos - transform.position).normalized;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
        Quaternion targetRot = Quaternion.LookRotation(dir);

        GameObject telegraph = null;
        Vector3 impactPos = transform.position + dir * _strikeLungeDistance;
        if (_strikeTelegraphPrefab != null)
        {
            telegraph = Instantiate(_strikeTelegraphPrefab, impactPos, Quaternion.identity);
            telegraph.transform.localScale = Vector3.zero;
        }

        float t = 0f;
        while (t < _rearWindupDuration)
        {
            t += Time.deltaTime;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, _bodyRotationSpeed * 2f * Time.deltaTime);
            if (telegraph != null)
            {
                float scale = Mathf.Lerp(0f, _strikeImpactRadius * 2f, t / _rearWindupDuration);
                telegraph.transform.localScale = new Vector3(scale, 0.02f, scale);
            }
            yield return null;
        }

        transform.rotation = targetRot; // aligne parfaitement avant le lunge, même si le windup était un peu court

        if (_animator != null)
        {
            _animator.SetBool("IsCoiling", false);
            _animator.SetTrigger("Slither");
        }

        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + dir * _strikeLungeDistance;
        float lungeElapsed = 0f;
        float lungeDuration = _strikeLungeDistance / _strikeLungeSpeed;

        while (lungeElapsed < lungeDuration)
        {
            lungeElapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, endPos, lungeElapsed / lungeDuration);
            yield return null;
        }

        if (_animator != null) _animator.SetTrigger("Bite");

        Collider[] hits = Physics.OverlapSphere(endPos, _strikeImpactRadius);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                HealthSystem health = hit.GetComponent<HealthSystem>();
                if (health != null) health.TakeDamage(_strikeDamage);
            }
        }

        if (telegraph != null) Destroy(telegraph);
        _lockRotation = false;
        yield return new WaitForSeconds(0.4f); // recovery avant la prochaine attaque
    }

    // ---------- CORRUPTION WAVE (ex Slow Wave) ----------
    // Anneau visible qui grandit au sol avant d'appliquer le ralentissement.
    private IEnumerator CorruptionWaveAttack()
    {
        if (_animator != null) _animator.SetBool("IsCoiling", true);

        GameObject ring = null;
        if (_waveRingPrefab != null)
        {
            ring = Instantiate(_waveRingPrefab, transform.position, Quaternion.identity);
            ring.transform.localScale = Vector3.zero;
        }

        float radius = 0f;
        while (radius < _waveMaxRadius)
        {
            radius += _waveExpandSpeed * Time.deltaTime;
            if (ring != null)
                ring.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);

            float distToPlayer = Vector3.Distance(_playerTransform.position, transform.position);
            if (distToPlayer <= radius && _cachedPlayerController != null)
            {
                StartCoroutine(SlowPlayer(_cachedPlayerController));
                break;
            }
            yield return null;
        }

        if (ring != null) Destroy(ring);
        if (_animator != null) _animator.SetBool("IsCoiling", false);
    }

    private IEnumerator SlowPlayer(PlayerController player)
    {
        player.SetSpeedMultiplier(_slowMultiplier);
        yield return new WaitForSeconds(_slowDuration);
        player.SetSpeedMultiplier(1f);
    }

    // ---------- REPOSITIONNEMENT SLITHER ----------
    // Remplace le wander aléatoire : si le joueur s'éloigne trop du rift en phase 2,
    // le serpent glisse rapidement (à vue, pas de téléportation/invisibilité) vers une
    // position proche du joueur. Télégraphié par des fissures qui suivent le trajet.
    private IEnumerator LeashWatcher()
    {
        while (_currentHealth > 0f)
        {
            yield return new WaitForSeconds(_diveCheckInterval);

            if (!_isPhase2 || _isRepositioning || _isAttacking) continue;
            if (GameManager.Instance != null && (GameManager.Instance.IsPaused || GameManager.Instance.IsGameOver)) continue;

            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            if (dist > _anchorLeashRadius)
                yield return StartCoroutine(RepositionSlither());
        }
    }

    private IEnumerator RepositionSlither()
    {
        _isRepositioning = true;
        _lockRotation = true;

        Vector3 offset = Random.insideUnitSphere * 3f;
        offset.y = 0f;
        Vector3 targetPos = MapBoundaryUtils.ClampToZone(_playerTransform.position + offset);
        Quaternion targetRot = Quaternion.LookRotation((targetPos - transform.position).normalized);

        GameObject crackTrail = null;
        if (_crackTrailPrefab != null)
            crackTrail = Instantiate(_crackTrailPrefab, transform.position, targetRot);

        float telegraphElapsed = 0f;
        while (telegraphElapsed < _repositionTelegraphDuration)
        {
            telegraphElapsed += Time.deltaTime;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, _bodyRotationSpeed * 2f * Time.deltaTime);
            yield return null;
        }

        if (_animator != null) _animator.SetTrigger("Slither");

        Vector3 startPos = transform.position;
        float elapsed = 0f;
        while (elapsed < _repositionTravelDuration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, targetPos, elapsed / _repositionTravelDuration);
            if (crackTrail != null) crackTrail.transform.position = transform.position;
            yield return null;
        }

        if (crackTrail != null) Destroy(crackTrail);

        if (_resurfaceBurstPrefab != null)
        {
            GameObject burst = Instantiate(_resurfaceBurstPrefab, transform.position, Quaternion.identity);
            Destroy(burst, 1f);
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, _resurfaceBurstRadius);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                HealthSystem health = hit.GetComponent<HealthSystem>();
                if (health != null) health.TakeDamage(_resurfaceBurstDamage);
            }
        }

        _isRepositioning = false;
        _lockRotation = false;
    }

    // ---------- SUMMON (Échos corrompus) ----------
    // Portail visible avant l'apparition du mini-boss, au lieu d'un pop instantané.
    private IEnumerator SummonAttack()
    {
        if (_animator != null) _animator.SetBool("IsCoiling", true);

        Vector3 playerPos = _playerTransform.position;
        Vector3 awayFromPlayer = (transform.position - playerPos).normalized;
        Vector3 spawnPos = playerPos + awayFromPlayer * 10f;

        GameObject portal = null;
        if (_riftPortalPrefab != null)
            portal = Instantiate(_riftPortalPrefab, spawnPos, Quaternion.identity);

        yield return new WaitForSeconds(_summonWindupDuration);

        if (portal != null) Destroy(portal);
        if (_animator != null) _animator.SetBool("IsCoiling", false);

        bool spawnBoss1 = Random.value > 0.5f;
        GameObject prefabToSpawn = spawnBoss1 ? _miniBoss1Prefab : _miniBoss2Prefab;
        if (prefabToSpawn == null) yield break;

        GameObject mini = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
        _activeMiniBossInstance = mini;

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

            // MODIFIÉ — recherche par composant plutôt que par nom d'enfant : trouve tout
            // SkinnedMeshRenderer peu importe sa profondeur/nom dans la hiérarchie (ex: "Skinned Mesh 0"),
            // évite de deviner un nom qui varie d'un prefab à l'autre (cause du bug précédent où
            // le fallback scalait toute la racine, y compris le Collider — d'où les tirs qui passaient au-dessus)
            SkinnedMeshRenderer[] renderers = boss.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (renderers.Length > 0)
            {
                foreach (SkinnedMeshRenderer smr in renderers)
                    smr.transform.localScale = Vector3.one * _summonedVisualScale;
            }
            else
            {
                boss.transform.localScale = Vector3.one * _summonedVisualScale; // fallback ultime, seulement si aucun SkinnedMeshRenderer trouvé du tout
            }

            boss.SetXPValue(boss.MaxHealth * 0.3f);
            boss.RageDisabled = true;
        }
    }

    // ---------- IMPLOSION — signature Phase 2 (Dance) ----------
    private IEnumerator ImplosionAttack()
    {
        if (_animator != null) _animator.SetTrigger("Dance");

        GameObject warning = null;
        if (_implosionWarningPrefab != null)
        {
            warning = Instantiate(_implosionWarningPrefab, transform.position, Quaternion.identity);
            warning.transform.localScale = Vector3.zero;
        }

        float elapsed = 0f;
        float minDistance = 3f;

        while (elapsed < _implosionPullDuration)
        {
            if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            {
                yield return null;
                continue;
            }

            elapsed += Time.deltaTime;

            if (warning != null)
            {
                float scale = Mathf.Lerp(0f, _implosionRadius * 2f, elapsed / _implosionPullDuration);
                warning.transform.localScale = new Vector3(scale, 0.05f, scale);
            }

            if (_playerTransform != null && _cachedPlayerController != null && _cachedPlayerRigidbody != null)
            {
                float dist = Vector3.Distance(_playerTransform.position, transform.position);
                if (dist > minDistance && !_cachedPlayerController.IsDashing)
                {
                    Vector3 pullDir = (transform.position - _playerTransform.position).normalized;
                    _cachedPlayerRigidbody.MovePosition(
                        _playerTransform.position + pullDir * _implosionPullForce * Time.deltaTime);
                }
            }
            yield return null;
        }

        if (warning != null) Destroy(warning);

        Collider[] hits = Physics.OverlapSphere(transform.position, _implosionRadius);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                HealthSystem health = hit.GetComponent<HealthSystem>();
                if (health != null) health.TakeDamage(_implosionDamage);
            }
        }

        yield return new WaitForSeconds(0.6f);
    }

    // ---------- Cristaux : orbite visuelle, vitesse liée à l'état de charge ----------
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

    private void HandleCrystalOrbitVisual()
    {
        if (_crystals == null) return;
        float speed = _isAttacking ? _crystalOrbitSpeedCharging : _crystalOrbitSpeedIdle;
        _crystalAngle += speed * Time.deltaTime;
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

    private void CheckPhase2()
    {
        if (_isPhase2) return;
        if (_currentHealth / _maxHealth > _phase2Threshold) return;
        _isPhase2 = true;
        // Pas de clip dédié à la transition de phase (budget anim limité) : le signal visuel
        // passe par le pipeline de glow générique (MaterialPropertyBlock) déjà utilisé sur
        // Golem/Sanglier/Cerf plutôt que par une animation supplémentaire.
    }

    protected override void Die()
    {
        StopAllCoroutines();
        if (_animator != null) _animator.SetTrigger("Death");
        if (_crystals != null)
            foreach (GameObject crystal in _crystals)
                if (crystal != null) Destroy(crystal);
        base.Die();
    }
}