using UnityEngine;

public class WeaponBase : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private GameObject _projectilePrefab;

    [Header("Stats")]
    [SerializeField] private float _fireRate       = 1f;
    [SerializeField] private float _detectionRange = 15f;
    [SerializeField] private float _damage         = 10f;

    private float _cooldownTimer = 0f;

    private void Update()
    {
        _cooldownTimer += Time.deltaTime;

        if (_cooldownTimer >= 1f / _fireRate)
        {
            Transform nearestEnemy = FindNearestEnemy();

            if (nearestEnemy != null)
            {
                Shoot(nearestEnemy);
                _cooldownTimer = 0f;
            }
        }
    }

    private Transform FindNearestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

        Transform nearest = null;
        float     minDist = _detectionRange;

        foreach (GameObject enemy in enemies)
        {
            float dist = Vector3.Distance(transform.position, enemy.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = enemy.transform;
            }
        }

        return nearest;
    }

   private void Shoot(Transform target)
   {
        Vector3 direction = (target.position - transform.position).normalized;

        // On utilise le pool au lieu de Instantiate
        GameObject projectileGO = ObjectPool.Instance.Get(
            "Projectile",
            transform.position,
            Quaternion.identity
        );

        if (projectileGO == null) return;

        ProjectileBasic projectile = projectileGO.GetComponent<ProjectileBasic>();
        if (projectile != null)
            projectile.Init(direction, _damage);
   }


   public void AddDamage(float value)
   {
        _damage += _damage * value;
   }

   public void AddFireRate(float value)
   {
        _fireRate += _fireRate * value;
   }

   private void Start()
   {
        // Applique le bonus méta de dégâts
        float bonusDamage = MetaProgressionManager.Instance.GetBonusDamage();
        _damage += _damage * bonusDamage;
   }
}