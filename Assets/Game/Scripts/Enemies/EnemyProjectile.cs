using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    [SerializeField] private float _speed = 6f;
    [SerializeField] private float _maxRange = 20f;
    [SerializeField] private float _damage = 10f; // Ajout de la stat de dégâts manquante

    private Vector3 _direction;
    private Vector3 _startPosition;
    private bool _hasHit = false;

    public void Init(Vector3 direction)
    {
        _direction = direction.normalized;
        _startPosition = transform.position;
        _hasHit = false;
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        // CORRECTION PAUSE
        if (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused) return;

        transform.position += _direction * _speed * Time.deltaTime;

        // OPTIMISATION : SqrMagnitude est plus performant que Vector3.Distance pour les vérifications de portée
        if (Vector3.SqrMagnitude(transform.position - _startPosition) >= _maxRange * _maxRange)
        {
            Despawn();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasHit) return;

        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null && player.IsInvincible)
            {
                if (player.CanAbsorb)
                {
                    CrystalSystem crystal = other.GetComponent<CrystalSystem>();
                    if (crystal != null) crystal.AbsorbProjectile();

                    _hasHit = true;
                    Despawn();
                }
                return;
            }

            // CORRECTION LOGIQUE : Application des dégâts si le joueur n'est pas invincible
            HealthSystem health = other.GetComponent<HealthSystem>();
            if (health != null)
            {
                health.TakeDamage(_damage);
            }

            _hasHit = true;
            Despawn();
        }
    }

    // CORRECTION POOLING : Centralisation de la désactivation pour éviter de détruire l'entité
    private void Despawn()
    {
        if (ObjectPool.Instance != null)
        {
            ObjectPool.Instance.ReturnToPool("EnemyProjectile", gameObject);
        }
        else
        {
            Destroy(gameObject); // Sécurité de secours
        }
    }
}