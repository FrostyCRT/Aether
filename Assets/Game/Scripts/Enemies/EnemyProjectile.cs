using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    [SerializeField] private float _speed    = 6f;
    
    [SerializeField] private float _maxRange = 20f;

    private Vector3 _direction;
    private Vector3 _startPosition;
    private bool    _hasHit = false; // ← garde fou

    public void Init(Vector3 direction)
    {
        _direction     = direction.normalized;
        _startPosition = transform.position;
        _hasHit        = false;
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
        if (_hasHit) return; // ← bloque le double déclenchement

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
                    Destroy(gameObject);
                }
                return;
            }

            _hasHit = true;
            Destroy(gameObject);
        }
    }
}