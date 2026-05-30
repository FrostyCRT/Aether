using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    [SerializeField] private float _speed    = 6f;
    [SerializeField] private float _damage   = 15f;
    [SerializeField] private float _maxRange = 20f;

    private Vector3 _direction;
    private Vector3 _startPosition;

    public void Init(Vector3 direction)
    {
        _direction     = direction.normalized;
        _startPosition = transform.position;
    }

    private void Update()
    {
        transform.position += _direction * _speed * Time.deltaTime;

        float distanceTravelled = Vector3.Distance(_startPosition, transform.position);
        if (distanceTravelled >= _maxRange)
            Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();

            if (player != null && player.IsInvincible)
            {
                if (player.CanAbsorb)
                {
                    // Absorption — charge le cristal
                    CrystalSystem crystal = other.GetComponent<CrystalSystem>();
                    if (crystal != null) crystal.AbsorbProjectile();
                    Destroy(gameObject);
                }
                // Pas dans la fenêtre d'absorption → projectile passe à travers
                return;
            }

            // Joueur non invincible — dégâts normaux
            HealthSystem health = other.GetComponent<HealthSystem>();
            if (health != null)
                health.TakeDamageFromProjectile(_damage);

            Destroy(gameObject);
        }
    }
}