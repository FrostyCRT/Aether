using UnityEngine;
using System.Collections;
public class WeaponShurikenBarrage : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _damage = 12f;
    [SerializeField] private float _fireRate = 0.4f;
    [SerializeField] private float _detectionRange = 15f;
    [SerializeField] private int _maxPierceCount = 3; // traverse jusqu'à 3 ennemis de base

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
        GameObject projectileGO = ObjectPool.Instance.Get("ProjectileShuriken", transform.position, Quaternion.identity);
        if (projectileGO == null) return;
        ProjectileBasic projectile = projectileGO.GetComponent<ProjectileBasic>();
        if (projectile != null)
        {
            projectile.Init(direction, _damage);
            projectile.SetPiercing(true, _maxPierceCount);
        }
    }
    public void AddDamage(float value) => _damage += _damage * value;
    public void AddFireRate(float value) => _fireRate += _fireRate * value;

    // AJOUTÉ — appelé par le palier 2 de la carte Couteaux
    public void AddPierce(int amount) => _maxPierceCount += Mathf.Max(1, amount);

    // AJOUTÉ — API double tir, remplace WeaponBase.UnlockDoubleShot()/IsDoubleShotUnlocked()
    public void UnlockDoubleShot() => _doubleShot = true;
    public bool IsDoubleShotUnlocked() => _doubleShot;
}