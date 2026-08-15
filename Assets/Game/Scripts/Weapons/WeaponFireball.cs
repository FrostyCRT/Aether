using UnityEngine;
using System.Collections;
public class WeaponFireball : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _damage = 20f;
    [SerializeField] private float _fireRate = 0.5f; // 1 tir toutes les 2 secondes
    [SerializeField] private float _detectionRange = 15f;

    // AJOUTÉ — Fragmentation (palier 3 de la carte Boule de Feu + nœud meta "Fragmentation")
    [Header("Fragmentation")]
    [Tooltip("Dégâts de l'explosion en fraction des dégâts du tir principal (0.5 = 50%).")]
    [SerializeField] private float _fragmentDamageRatio = 0.5f;
    [SerializeField] private float _fragmentRadius = 2f;
    private float _fragmentChanceFromCard = 0f;

    // AJOUTÉ — Double Tir, migré depuis WeaponBase (qui tirait en double en parallèle de
    // cette arme, bug non voulu). Tir x2 est maintenant directement porté par l'arme
    // exclusive concernée, plus par un composant générique séparé.
    [Header("Double Tir")]
    [SerializeField] private bool _doubleShot = false;
    [SerializeField] private float _doubleShotDelay = 0.1f;
    private WaitForSeconds _doubleShotWait;

    private float _cooldownTimer = 0f;
    private static readonly Collider[] _detectionBuffer = new Collider[50];

    private void Awake()
    {
        _doubleShotWait = new WaitForSeconds(_doubleShotDelay);
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        if (GameManager.Instance.IsPaused) return;
        _cooldownTimer += Time.deltaTime;
        float cooldownDuration = _fireRate > 0f ? (1f / _fireRate) : 9999f;
        if (_cooldownTimer >= cooldownDuration)
        {
            Transform target = FindNearestEnemy();
            if (target != null)
            {
                Vector3 direction = target.position - transform.position;
                direction.y = 0f;
                direction.Normalize();
                Shoot(direction);
                _cooldownTimer = 0f;
            }
        }
    }

    // AJOUTÉ — point d'entrée unique du tir, gère le double tir en plus du tir simple
    private void Shoot(Vector3 direction)
    {
        FireProjectile(direction);
        if (_doubleShot)
            StartCoroutine(FireDelayed(direction));
    }

    private IEnumerator FireDelayed(Vector3 direction)
    {
        yield return _doubleShotWait;
        FireProjectile(direction);
    }

    private Transform FindNearestEnemy()
    {
        // Détection par Tag, pas par LayerMask — voir note plus bas sur pourquoi c'est important ici
        int count = Physics.OverlapSphereNonAlloc(transform.position, _detectionRange, _detectionBuffer);
        Transform nearest = null;
        float minDistSqr = _detectionRange * _detectionRange;
        for (int i = 0; i < count; i++)
        {
            Collider col = _detectionBuffer[i];
            if (col == null || !col.CompareTag("Enemy")) continue;
            float distSqr = (col.transform.position - transform.position).sqrMagnitude;
            if (distSqr < minDistSqr)
            {
                minDistSqr = distSqr;
                nearest = col.transform;
            }
        }
        return nearest;
    }
    private void FireProjectile(Vector3 direction)
    {
        if (ObjectPool.Instance == null) return;
        GameObject projectileGO = ObjectPool.Instance.Get("Projectile", transform.position, Quaternion.identity);
        if (projectileGO == null) return;
        ProjectileBasic projectile = projectileGO.GetComponent<ProjectileBasic>();
        if (projectile != null)
        {
            projectile.Init(direction, _damage);

            // AJOUTÉ — combine la chance de fragmentation du nœud meta (long terme) et de la
            // carte in-run (palier 3), plutôt que de les traiter comme deux systèmes séparés.
            float totalChance = GetTotalFragmentationChance();
            if (totalChance > 0f)
            {
                projectile.SetFragmentation(totalChance, _damage * _fragmentDamageRatio, _fragmentRadius);
            }
        }
    }

    // AJOUTÉ
    private float GetTotalFragmentationChance()
    {
        float metaChance = MetaProgressionManager.Instance != null
            ? MetaProgressionManager.Instance.GetFragmentationChance()
            : 0f;
        return metaChance + _fragmentChanceFromCard;
    }

    // AJOUTÉ — appelé par le palier 3 de la carte Boule de Feu
    public void AddFragmentationChance(float value)
    {
        _fragmentChanceFromCard += value;
    }

    // AJOUTÉ — API double tir, remplace WeaponBase.UnlockDoubleShot()/IsDoubleShotUnlocked()
    public void UnlockDoubleShot() => _doubleShot = true;
    public bool IsDoubleShotUnlocked() => _doubleShot;

    public void AddDamage(float value) => _damage += _damage * value;
    public void AddFireRate(float value) => _fireRate += _fireRate * value;
}