using System;
using UnityEngine;

public class WeaponBase : MonoBehaviour
{


    [Header("Stats de Base")]
    [SerializeField] private float _baseFireRate = 1f;
    [SerializeField] private float _baseDetectionRange = 15f;
    [SerializeField] private float _baseDamage = 100f; // MODIFIE - x10, cf. rescale global des degats/PV

    [Header("Double tir")]
    [SerializeField] private bool _doubleShot = false;
    [SerializeField] private float _doubleShotDelay = 0.1f;

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

    // Multiplicateur temporaire (pour l'Ulti du CrystalSystem)
    private float _damageMultiplier = 1f;

    // Optimisation : Cache de la caméra principale et du WaitForSeconds du double shot
    private Camera _mainCamera;
    private WaitForSeconds _doubleShotWait;

    // Pour optimiser la recherche d'ennemis sans allouer de mémoire inutilement
    private readonly Collider[] _detectionBuffer = new Collider[50];

    public float baseDamage => _baseDamage;

    private void Awake()
    {
        _mainCamera = Camera.main;
        _doubleShotWait = new WaitForSeconds(_doubleShotDelay);
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

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        if (GameManager.Instance.IsPaused) return;

        _cooldownTimer += Time.deltaTime;

        float currentCooldownDuration = _currentFireRate > 0f ? (1f / _currentFireRate) : 9999f;

        if (_cooldownTimer >= currentCooldownDuration)
        {
            Vector3 fireDirection = Vector3.zero;
            bool canShoot = false;

            Vector3 firingOrigin = _projectileSpawnPoint != null ? _projectileSpawnPoint.position : transform.position;

            // AJOUTÉ (2026-10-21, retour utilisateur sur la Boule de feu : "si on est collé dans un angle de 2 murs
            // invisible [...] la boule de feu reste coincé dans l'angle, et ne se casse pas" - même root cause ici,
            // voir WeaponFireball.Update()) - dans un coin de carte, un ennemi peut se retrouver à une distance
            // quasi nulle du joueur (il ne peut pas le contourner) : `.normalized` d'un vecteur (quasi) nul renvoie
            // silencieusement (0,0,0) sans erreur, et un projectile tiré avec cette direction ne bouge plus jamais
            // ni n'atteint sa portée max pour se désactiver tout seul - coincé pour toujours. Seuil minimal avant
            // `.normalized` dans les 2 branches (visée auto ET souris, cette dernière dégénère si le curseur est
            // pile sur le joueur).
            if (GameSettings.AutoAimEnabled)
            {
                Transform nearest = FindNearestEnemy();
                if (nearest != null)
                {
                    Vector3 toNearest = nearest.position - firingOrigin;
                    if (toNearest.sqrMagnitude >= 0.01f)
                    {
                        fireDirection = toNearest.normalized;
                        canShoot = true;
                    }
                }
            }
            else
            {
                Vector3 mouseWorldPosition = GetMouseWorldPosition();
                Vector3 toMouse = mouseWorldPosition - firingOrigin;
                if (toMouse.sqrMagnitude >= 0.01f)
                {
                    fireDirection = toMouse.normalized;
                    canShoot = true;
                }
            }

            if (canShoot)
            {
                fireDirection.y = 0f;
                Shoot(fireDirection);
                _cooldownTimer = 0f;
            }
        }
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

    private void Shoot(Vector3 direction)
    {
        FireProjectile(direction);
        if (_doubleShot)
            StartCoroutine(FireDelayed(direction));
    }

    // Nœud Réputation "Fragmentation" (voir FireProjectile).
    private const float FragmentDamageRatio = 1f;
    private const float FragmentRadius = 4f;
    private int _shotsSinceFragment;

    private void FireProjectile(Vector3 direction)
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
            projectile.Init(direction, finalDamage);

            // MODIFIÉ (2026-10-27, retour utilisateur : Fragmentation "très peu efficace en jeu", puis "le rayon
            // d'explosion [doit être] quand même assez large, et je veux que ce soit visible") - au lieu de 15 % de
            // chance (50 % des dégâts, rayon 2) : un projectile sur 4 (Double Tir compris) explose à coup sûr, avec
            // tous ses dégâts, dans un rayon de FragmentRadius. Le projectile chargé est marqué (traînée plus large et
            // plus claire) pour que le joueur le voie partir.
            int interval = MetaProgressionManager.Instance != null
                ? MetaProgressionManager.Instance.GetFragmentationInterval()
                : 0;

            if (interval > 0 && ++_shotsSinceFragment >= interval)
            {
                _shotsSinceFragment = 0;
                projectile.SetFragmentation(1f, finalDamage * FragmentDamageRatio, FragmentRadius);
                projectile.MarkFragmentCharged();
            }
        }
    }

    private System.Collections.IEnumerator FireDelayed(Vector3 direction)
    {
        // Utilisation du cache pour éviter d'allouer du GC à chaque double tir
        yield return _doubleShotWait;
        FireProjectile(direction);
    }

    private void UpdateCalculatedStats()
    {
        // Formule additive saine : Dégâts = DégâtsDeBase * (1 + SommeDesUpgrades)
        _currentDamage = _baseDamage * (1f + _upgradeDamageModifier);
        _currentFireRate = _baseFireRate * (1f + _upgradeFireRateModifier);
    }

    // --- API PUBLIQUE ---

    public void UnlockDoubleShot() => _doubleShot = true;
    public bool IsDoubleShotUnlocked() => _doubleShot;

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