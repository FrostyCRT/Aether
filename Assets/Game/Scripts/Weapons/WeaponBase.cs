using System;
using UnityEngine;

public class WeaponBase : MonoBehaviour
{


    [Header("Stats de Base")]
    [SerializeField] private float _baseFireRate = 1f;
    [SerializeField] private float _baseDetectionRange = 15f;
    [SerializeField] private float _baseDamage = 100f; // MODIFIE - x10, cf. rescale global des degats/PV

    // MODIFIÉ (2026-11-25, retours lot 1 groupe 5 : le tir de base « presque inutile en fin de partie », il n'avait qu'une
    // carte à un seul palier) - la carte Double Tir devient une arme à 3 paliers (décidé avec l'utilisateur) :
    //  1 = Double Tir : 2 gouttes à la suite (écart _burstDelay), sur la même ligne ;
    //  2 = Triple Tir : 3 gouttes à la suite ;
    //  3 = DÉLUGE : jet continu, une goutte toutes les _delugeInterval secondes, chacune visant l'ennemi le plus proche
    //      (une cible tuée ne fait pas perdre les gouttes suivantes). La Cadence (arbre + cartes) N'ACCÉLÈRE PAS le Déluge
    //      (choix de l'utilisateur : sinon trop fort) ; aux paliers 1-2 elle raccourcit l'attente entre deux salves,
    //      jamais l'écart entre les gouttes d'une même salve.
    // Le tir reste linéaire (pas d'éventail : c'est la signature des dagues de Lyra).
    [Header("Paliers (carte Double Tir)")]
    [SerializeField] private float _burstDelay = 0.1f;
    [SerializeField] private float _delugeInterval = 0.1f;
    [Tooltip("Dégâts d'une goutte du Déluge, en part d'un tir normal (réglé par la mesure).")]
    [SerializeField] private float _delugeDamageFactor = 0.85f;   // > Triple Tir avec Cadence max (7,5 gouttes/s) : 10 x 0,85 = 8,5
    private int _shotLevel;            // 0 = tir simple, 1 Double, 2 Triple, 3 Déluge
    public const int MaxShotLevel = 3;
    // Fragmentation (Aether) : 1 tir sur 4 en temps normal, 1 goutte sur 10 au Déluge (sinon 2,5 explosions par seconde).
    private const int DelugeFragmentInterval = 10;
    // Une éclaboussure d'impact sur 3 au Déluge.
    private const int DelugeSplashEvery = 3;

    [Header("Références")]
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private LayerMask _enemyLayer;
    [SerializeField] private Transform _projectileSpawnPoint;

    // Cache pour les modificateurs d'upgrades (Logique additive saine)
    private float _upgradeDamageModifier = 0f;
    private float _upgradeFireRateModifier = 0f;

    // Stats réelles calculées
    private float _currentDamage;
    private float _currentFireRate;
    private float _cooldownTimer = 0f;
    private float _delugeTimer = 0f;
    private int _delugeShots;

    // Multiplicateur temporaire (pour l'Ulti du CrystalSystem)
    private float _damageMultiplier = 1f;

    // Optimisation : Cache de la caméra principale et du WaitForSeconds des salves
    private Camera _mainCamera;
    private WaitForSeconds _burstWait;

    // Pour optimiser la recherche d'ennemis sans allouer de mémoire inutilement
    private readonly Collider[] _detectionBuffer = new Collider[50];

    public float baseDamage => _baseDamage;
    // AJOUTÉ (2026-10-27, équilibrage des PV ennemis : "une attaque de plus pour en tuer un") - dégâts d'un tir de base
    // à cet instant, hors buffs temporaires (Concentration, Surpuissance : PlayerBuffs.OutgoingDamageMultiplier). Lecture seule.
    public float CurrentShotDamage => _currentDamage * _damageMultiplier;

    private void Awake()
    {
        _mainCamera = Camera.main;
        _burstWait = new WaitForSeconds(_burstDelay);
    }

    private void Start()
    {
        float bonusDamage = MetaProgressionManager.Instance.GetReputationBonusDamage();
        float bonusCadence = MetaProgressionManager.Instance.GetBonusCadence();

        // Application initiale de la métaprogression
        _baseDamage += _baseDamage * bonusDamage;
        _baseFireRate += _baseFireRate * bonusCadence;

        UpdateCalculatedStats();
    }

    public static bool AutoFireDisabled;   // didacticiel (partie d'entraînement)

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        if (GameManager.Instance.IsPaused) return;
        if (WeaponBase.AutoFireDisabled) return;   // didacticiel : tir coupé pendant les étapes de déplacement

        if (_shotLevel >= MaxShotLevel)
        {
            // Déluge : cadence fixe, indépendante de la Cadence ; rattrape au plus 2 gouttes après une image lente.
            _delugeTimer = Mathf.Min(_delugeTimer + Time.deltaTime, _delugeInterval * 2f);
            while (_delugeTimer >= _delugeInterval)
            {
                if (!TryGetFireDirection(out Vector3 dir)) { _delugeTimer = _delugeInterval; break; }   // rien à viser : prêt dès qu'une cible arrive
                _delugeTimer -= _delugeInterval;
                FireProjectile(dir, true);
            }
            return;
        }

        _cooldownTimer += Time.deltaTime;

        float currentCooldownDuration = _currentFireRate > 0f ? (1f / _currentFireRate) : 9999f;

        if (_cooldownTimer >= currentCooldownDuration && TryGetFireDirection(out Vector3 fireDirection))
        {
            Shoot(fireDirection);
            _cooldownTimer = 0f;
        }
    }

    // Direction de tir selon le mode de visée. AJOUTÉ (2026-10-21, retour utilisateur sur la Boule de feu : "si on est
    // collé dans un angle de 2 murs invisible [...] la boule de feu reste coincé dans l'angle") - dans un coin de carte,
    // un ennemi peut se retrouver à une distance quasi nulle du joueur : `.normalized` d'un vecteur (quasi) nul renvoie
    // (0,0,0) et le projectile resterait coincé pour toujours. Seuil minimal avant `.normalized` (visée auto ET souris,
    // cette dernière dégénère si le curseur est pile sur le joueur).
    private bool TryGetFireDirection(out Vector3 fireDirection)
    {
        fireDirection = Vector3.zero;
        Vector3 firingOrigin = _projectileSpawnPoint != null ? _projectileSpawnPoint.position : transform.position;

        if (GameSettings.AutoAimEnabled)
        {
            Transform nearest = FindNearestEnemy();
            if (nearest == null) return false;
            Vector3 toNearest = nearest.position - firingOrigin;
            if (toNearest.sqrMagnitude < 0.01f) return false;
            fireDirection = toNearest.normalized;
        }
        else if (GameInput.UsingGamepad)
        {
            // AJOUTÉ (2026-10-28, session manette) - visée manuelle au stick droit ; stick relâché = on continue
            // de tirer dans la dernière direction visée (au départ : devant le joueur).
            Vector2 aim = GameInput.PadAimDirection;
            fireDirection = aim.sqrMagnitude > 0f ? new Vector3(aim.x, 0f, aim.y) : transform.forward;
        }
        else
        {
            Vector3 toMouse = GetMouseWorldPosition() - firingOrigin;
            if (toMouse.sqrMagnitude < 0.01f) return false;
            fireDirection = toMouse.normalized;
        }
        fireDirection.y = 0f;
        return true;
    }

    private Vector3 GetMouseWorldPosition()
    {
        if (_mainCamera == null) _mainCamera = Camera.main;

        Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0, transform.position.y, 0));

        if (groundPlane.Raycast(ray, out float enter))
        {
            return ray.GetPoint(enter);
        }
        return transform.position;
    }

    private Transform FindNearestEnemy()
    {
        // Gobelin doré à portée : cible prioritaire (GoldenGoblinEvent.PriorityTarget).
        Transform golden = GoldenGoblinEvent.PriorityTarget(transform.position, _baseDetectionRange);
        if (golden != null) return golden;

        int numColliders = Physics.OverlapSphereNonAlloc(transform.position, _baseDetectionRange, _detectionBuffer, _enemyLayer);

        Transform nearest = null;
        // On compare avec la distance maximale au carré pour éviter le calcul de racine carrée
        float minDistSqr = _baseDetectionRange * _baseDetectionRange;

        for (int i = 0; i < numColliders; i++)
        {
            if (_detectionBuffer[i] == null) continue;

            // .sqrMagnitude au lieu de Vector3.Distance
            float distSqr = (_detectionBuffer[i].transform.position - transform.position).sqrMagnitude;
            if (distSqr < minDistSqr)
            {
                minDistSqr = distSqr;
                nearest = _detectionBuffer[i].transform;
            }
        }
        return nearest;
    }

    // Salve des paliers 0-2 : 1 + _shotLevel gouttes à la suite, sur la même ligne.
    private void Shoot(Vector3 direction)
    {
        FireProjectile(direction, false);
        if (_shotLevel > 0) StartCoroutine(FireBurst(direction, _shotLevel));
    }

    private System.Collections.IEnumerator FireBurst(Vector3 direction, int extraShots)
    {
        for (int i = 0; i < extraShots; i++)
        {
            yield return _burstWait;   // en cache : aucune allocation par salve
            FireProjectile(direction, false);
        }
    }

    // Nœud Réputation "Fragmentation" (voir FireProjectile).
    private const float FragmentDamageRatio = 1f;
    private const float FragmentRadius = 4f;
    private int _shotsSinceFragment;

    private void FireProjectile(Vector3 direction, bool deluge)
    {
        if (ObjectPool.Instance == null) return;

        Vector3 rawSpawnPosition = _projectileSpawnPoint != null ? _projectileSpawnPoint.position : transform.position;
        // On garde X/Z du bâton (origine visuelle), mais on verrouille Y au niveau de jeu du joueur
        Vector3 spawnPosition = new Vector3(rawSpawnPosition.x, transform.position.y, rawSpawnPosition.z);

        GameObject projectileGO = ObjectPool.Instance.Get("Projectile", spawnPosition, Quaternion.identity);
        if (projectileGO == null) return;

        projectileGO.TryGetComponent(out ProjectileBasic projectile);
        if (projectile != null)
        {
            // PlayerBuffs.OutgoingDamageMultiplier = buffs dynamiques du joueur
            // (Concentration). Vaut 1f si aucun buff actif → sans effet pour les
            // autres personnages.
            float finalDamage = _currentDamage * _damageMultiplier * PlayerBuffs.OutgoingDamageMultiplier;
            if (deluge) finalDamage *= _delugeDamageFactor;
            projectile.Init(direction, finalDamage);
            if (deluge && (++_delugeShots % DelugeSplashEvery) != 0) projectile.SkipImpactFX();

            // MODIFIÉ (2026-10-27, retour utilisateur : Fragmentation "très peu efficace en jeu", puis "le rayon
            // d'explosion [doit être] quand même assez large, et je veux que ce soit visible") - au lieu de 15 % de
            // chance (50 % des dégâts, rayon 2) : un projectile sur 4 (salves comprises) explose à coup sûr, avec
            // tous ses dégâts, dans un rayon de FragmentRadius. Le projectile chargé est marqué (traînée plus large et
            // plus claire) pour que le joueur le voie partir. Au Déluge : 1 goutte sur DelugeFragmentInterval.
            int interval = MetaProgressionManager.Instance != null
                ? MetaProgressionManager.Instance.GetFragmentationInterval()
                : 0;
            if (interval > 0 && deluge) interval = Mathf.Max(interval, DelugeFragmentInterval);

            if (interval > 0 && ++_shotsSinceFragment >= interval)
            {
                _shotsSinceFragment = 0;
                projectile.SetFragmentation(1f, finalDamage * FragmentDamageRatio, FragmentRadius);
                projectile.MarkFragmentCharged();
            }
        }
    }

    private void UpdateCalculatedStats()
    {
        // Formule additive saine : Dégâts = DégâtsDeBase * (1 + SommeDesUpgrades)
        _currentDamage = _baseDamage * (1f + _upgradeDamageModifier);
        _currentFireRate = _baseFireRate * (1f + _upgradeFireRateModifier);
    }

    // --- API PUBLIQUE ---

    // Palier de la carte Double Tir (0 = pas encore prise). Chaque prise monte d'un palier, jusqu'au Déluge.
    public int ShotLevel => _shotLevel;
    public void AddShotLevel() { if (_shotLevel < MaxShotLevel) _shotLevel++; }
    public bool IsDelugeActive => _shotLevel >= MaxShotLevel;
    // Gardés pour les appelants existants : « débloqué » = au moins le palier 1.
    public void UnlockDoubleShot() => AddShotLevel();
    public bool IsDoubleShotUnlocked() => _shotLevel >= 1;

    public void AddDamage(float value)
    {
        _upgradeDamageModifier += value; // On ajoute le pourcentage (+0.1f pour +10%)
        UpdateCalculatedStats();
    }

    public void AddFireRate(float value)
    {
        _upgradeFireRateModifier += value;
        UpdateCalculatedStats();
    }

    public void SetDamageMultiplier(float multiplier)
    {
        _damageMultiplier = multiplier;
    }
}
