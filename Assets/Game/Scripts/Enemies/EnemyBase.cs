using UnityEngine;

public class EnemyBase : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _moveSpeed  = 2f;
    [SerializeField] private float _maxHealth  = 30f;

    [Header("Drops")]
    [SerializeField] private float _xpValue   = 10f;
    [SerializeField] private int   _goldValue = 2;
    // Gold droppé à la mort
    
    [Header("Pool")]
    [SerializeField] private string _poolTag = "Enemy";
    

    // Protected = accessible par les classes enfants
    protected float MoveSpeed => _moveSpeed;

    private float     _currentHealth;
    private Transform _playerTransform;

    private void Awake()
    {
        _currentHealth = _maxHealth;
    }

    private void Start()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
            _playerTransform = player.transform;
    }

    private void Update()
    {
        if (_playerTransform == null) return;
        if (GameManager.Instance.IsGameOver) return;

        UpdateBehaviour(_playerTransform);
    }

    // Virtual = peut être overridé par les classes enfants
    protected virtual void UpdateBehaviour(Transform playerTransform)
    {
        Vector3 direction  = (playerTransform.position - transform.position).normalized;
        Vector3 separation = GetBaseSeparation();
        transform.position += (direction + separation).normalized * MoveSpeed * _speedMultiplier * Time.deltaTime;
    }

    private Vector3 GetBaseSeparation()
    {
        Vector3 force = Vector3.zero;
        float separationRadius = 1.5f;

        Collider[] neighbours = Physics.OverlapSphere(transform.position, separationRadius);
        foreach (Collider neighbour in neighbours)
        {
            if (neighbour.gameObject == gameObject) continue;
            if (!neighbour.CompareTag("Enemy")) continue;

            Vector3 pushDirection = transform.position - neighbour.transform.position;
            force += pushDirection.normalized;
        }

        return force.normalized * 0.5f; // Force réduite pour ne pas trop perturber
    }

    public void TakeDamage(float damage)
    {
        _currentHealth -= damage;
        if (_currentHealth <= 0)
            Die();
    }

    private void Die()
    {
        XPSystem.Instance.AddXP(_xpValue);
        GameManager.Instance.AddKill();
        MetaProgressionManager.Instance.AddRunGold(_goldValue);
        ObjectPool.Instance.ReturnToPool(_poolTag, gameObject);
    }

    [System.NonSerialized] protected float _speedMultiplier = 1f;

    public void SetSpeedMultiplier(float multiplier)
    {
        _speedMultiplier = multiplier;
    }
}