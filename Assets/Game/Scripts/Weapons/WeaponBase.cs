using System;
using UnityEngine;

public class WeaponBase : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private LayerMask _enemyLayer; // À configurer dans l'inspecteur sur "Enemy"

    [Header("Stats de Base")]
    [SerializeField] private float _baseFireRate = 1f;
    [SerializeField] private float _baseDetectionRange = 15f;
    [SerializeField] private float _baseDamage = 10f;

    [Header("Double tir")]
    [SerializeField] private bool _doubleShot = false;
    [SerializeField] private float _doubleShotDelay = 0.1f;

    // Stats réelles calculées
    private float _currentDamage;
    private float _currentFireRate;
    private float _cooldownTimer = 0f;

    // Multiplicateur temporaire (pour l'Ulti du CrystalSystem)
    private float _damageMultiplier = 1f;

    // Pour optimiser la recherche d'ennemis sans allouer de mémoire inutilement
    private Collider[] _detectionBuffer = new Collider[50];

    // Propriété utile pour lire les dégâts actuels (utilisée par ton système d'upgrade au besoin)
    public float baseDamage => _baseDamage;

    private void Start()
    {
        float bonusDamage = MetaProgressionManager.Instance.GetBonusDamage();
        float bonusCadence = MetaProgressionManager.Instance.GetBonusCadence();

        // Application de la métaprogression sur les stats de base
        _baseDamage += _baseDamage * bonusDamage;
        _baseFireRate += _baseFireRate * bonusCadence;

        UpdateCalculatedStats();
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        if (GameManager.Instance.IsPaused) return;

        _cooldownTimer += Time.deltaTime;
        if (_cooldownTimer >= 1f / _currentFireRate)
        {
            Vector3 fireDirection = Vector3.zero;
            bool canShoot = false;

            if (SettingsManager.IsAutoFireEnabled())
            {
                Transform nearest = FindNearestEnemy();
                if (nearest != null)
                {
                    fireDirection = (nearest.position - transform.position).normalized;
                    canShoot = true;
                }
            }
            else
            {
                Vector3 mouseWorldPosition = GetMouseWorldPosition();
                fireDirection = (mouseWorldPosition - transform.position).normalized;
                canShoot = true;
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
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0, transform.position.y, 0));

        if (groundPlane.Raycast(ray, out float enter))
        {
            return ray.GetPoint(enter);
        }
        return transform.position;
    }

    // CORRECTION PERFORMANCE : Utilisation de Physics.OverlapSphereNonAlloc (Gros gain de FPS)
    private Transform FindNearestEnemy()
    {
        int numColliders = Physics.OverlapSphereNonAlloc(transform.position, _baseDetectionRange, _detectionBuffer, _enemyLayer);

        Transform nearest = null;
        float minDist = _baseDetectionRange;

        for (int i = 0; i < numColliders; i++)
        {
            float dist = Vector3.Distance(transform.position, _detectionBuffer[i].transform.position);
            if (dist < minDist)
            {
                minDist = dist;
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

    private void FireProjectile(Vector3 direction)
    {
        GameObject projectileGO = ObjectPool.Instance.Get("Projectile", transform.position, Quaternion.identity);
        if (projectileGO == null) return;

        ProjectileBasic projectile = projectileGO.GetComponent<ProjectileBasic>();
        if (projectile != null)
        {
            // Prend en compte le multiplicateur de l'Ulti
            float finalDamage = _currentDamage * _damageMultiplier;
            projectile.Init(direction, finalDamage);
        }

        if (MetaProgressionManager.Instance.HasFragmentation() && projectile != null)
        {
            float fragDamage = (_currentDamage * _damageMultiplier) * 0.5f;
            projectile.SetFragmentation(true, fragDamage, 2f);
        }
    }

    private System.Collections.IEnumerator FireDelayed(Vector3 direction)
    {
        yield return new WaitForSeconds(_doubleShotDelay);
        FireProjectile(direction);
    }

    // Recalcule proprement les stats pour éviter les dérives mathématiques
    private void UpdateCalculatedStats()
    {
        _currentDamage = _baseDamage;
        _currentFireRate = _baseFireRate;
    }

    // --- API PUBLIQUE POUR LES AUTRES SCRIPTS ---

    public void UnlockDoubleShot() => _doubleShot = true;
    public bool IsDoubleShotUnlocked() => _doubleShot;

    // Correction de la logique additive des Upgrades (+10% se basera toujours sur les dégâts de base initials)
    public void AddDamage(float value)
    {
        _baseDamage += _baseDamage * value;
        UpdateCalculatedStats();
    }

    public void AddFireRate(float value)
    {
        _baseFireRate += _baseFireRate * value;
        UpdateCalculatedStats();
    }

    // CORRECTION DU BUG : La méthode réclamée par ton CrystalSystem !
    public void SetDamageMultiplier(float multiplier)
    {
        _damageMultiplier = multiplier;
    }
}