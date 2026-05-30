using UnityEngine;

public class ProjectileBasic : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _speed    = 10f;
    [SerializeField] private float _maxRange = 15f;

    private float   _damage;
    private Vector3 _startPosition;
    private Vector3 _direction;

    public void Init(Vector3 direction, float damage)
    {
        _direction     = direction.normalized;
        _startPosition = transform.position;
        _damage        = damage;
    }

    private void Update()
    {
        transform.position += _direction * _speed * Time.deltaTime;

        float distanceTravelled = Vector3.Distance(_startPosition, transform.position);
        if (distanceTravelled >= _maxRange)
            ObjectPool.Instance.ReturnToPool("Projectile", gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        
    
        if (other.CompareTag("Enemy"))
        {
            EnemyBase enemy = other.GetComponent<EnemyBase>();
            if (enemy != null)
                enemy.TakeDamage(_damage);
            else
            {
                // Essaie avec BossBase
                BossBase boss = other.GetComponent<BossBase>();
                if (boss != null)
                    boss.TakeDamage(_damage);
            }

            ObjectPool.Instance.ReturnToPool("Projectile", gameObject);
        }
    }
}