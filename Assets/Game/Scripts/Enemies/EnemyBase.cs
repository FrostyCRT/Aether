using UnityEngine;

public class EnemyBase : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _maxHealth = 30f;

    [Header("Drops")]
    [SerializeField] private float _xpValue = 10f;
    [SerializeField] private int _goldValue = 2;

    [Header("Pool")]
    [SerializeField] private string _poolTag = "Enemy";

    [Header("Distance Joueur")]
    [SerializeField] private float _playerContactRadius = 1.2f;


    protected float MoveSpeed => _moveSpeed;

    private float _currentHealth;
    private Transform _playerTransform;
    private Transform _currentTarget;

    protected float _speedMultiplier = 1f;
    private float _speedMultiplierTarget = 1f; // Multiplicateur temporaire pour l'attraction fantôme

    private void OnEnable()
    {
        _currentHealth = _maxHealth;
        _speedMultiplier = 1f;
        _speedMultiplierTarget = 1f;

        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) _playerTransform = player.transform;
        }

        _currentTarget = _playerTransform;


        // AJOUT : On s'abonne à l'événement de destruction du fantôme
        PlayerController.OnPhantomDestroyed += HandlePhantomDestroyed;
    }

    private void OnDisable()
    {
        // AJOUT CRITIQUE : Toujours se désabonner dans le OnDisable pour éviter les fuites de mémoire !
        PlayerController.OnPhantomDestroyed -= HandlePhantomDestroyed;
    }

    private void HandlePhantomDestroyed()
    {
        // Si l'ennemi suivait le fantôme, on le renvoie vers le joueur et on reset sa vitesse
        if (_currentTarget != null && _currentTarget == PlayerController.ActivePhantomClone)
        {
            SetTarget(_playerTransform); // Repasse la vitesse cible à 1f automatiquement
        }
    }

    private void Start()
    {
        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) _playerTransform = player.transform;
        }
        _currentTarget = _playerTransform;
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused) return;

        if (_currentTarget == null)
            SetTarget(_playerTransform);

        if (_currentTarget == null) return;

        // AJOUTÉ — vérifie l'attraction au clone à CHAQUE frame, pas juste au spawn
        if (PlayerController.ActivePhantomClone != null && _currentTarget == _playerTransform)
        {
            float sqrDistance = Vector3.SqrMagnitude(transform.position - PlayerController.ActivePhantomClone.position);
            if (sqrDistance <= PlayerController.PhantomAttractRadius * PlayerController.PhantomAttractRadius)
            {
                PlayerController.TryAttractToPhantom(this);
            }
        }

        UpdateBehaviour(_currentTarget);
    }



    private EnemyAnimatorController _animatorController;

    protected virtual void UpdateBehaviour(Transform target)
    {
        float distanceToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
        bool isInContactWithPlayer = target == _playerTransform && distanceToPlayer <= _playerContactRadius;

        if (_animatorController == null) _animatorController = GetComponentInChildren<EnemyAnimatorController>();

        if (isInContactWithPlayer)
        {
            if (_animatorController != null) _animatorController.SetAttacking(true);
            return;
        }

        if (_animatorController != null) _animatorController.SetAttacking(false);

        Vector3 direction = (target.position - transform.position).normalized;
        Vector3 separation = GetBaseSeparation();
        Vector3 final = (direction + separation * 0.3f).normalized;

        if (Vector3.Dot(final, direction) < 0.1f)
            final = direction;

        transform.position += final * MoveSpeed * _speedMultiplier * _speedMultiplierTarget * Time.deltaTime;

        if (final.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(final);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 720f * Time.deltaTime);
        }
    }

    public void SetTarget(Transform newTarget, float speedBoost = 1f)
    {
        _currentTarget = newTarget != null ? newTarget : _playerTransform;

        if (_currentTarget == _playerTransform)
        {
            _speedMultiplierTarget = 1f;
        }
        else
        {
            _speedMultiplierTarget = speedBoost;
            Debug.Log($"[PHANTOM DEBUG] {gameObject.name} boosté à {speedBoost}x, target={newTarget.name}", this); // AJOUTÉ TEMPORAIRE
        }
    }

    private static readonly Collider[] _neighbourBuffer = new Collider[16];
    private static int _enemyLayerMask = -1;

    private Vector3 GetBaseSeparation()
    {
        if (_enemyLayerMask == -1)
            _enemyLayerMask = 1 << LayerMask.NameToLayer("Enemy");

        Vector3 force = Vector3.zero;
        float separationRadius = 1.5f;

        int count = Physics.OverlapSphereNonAlloc(transform.position, separationRadius, _neighbourBuffer, _enemyLayerMask);

        for (int i = 0; i < count; i++)
        {
            Collider neighbour = _neighbourBuffer[i];
            if (neighbour.gameObject == gameObject) continue;

            Vector3 pushDirection = transform.position - neighbour.transform.position;
            force += pushDirection.normalized;
        }

        return force.normalized * 0.5f;
    }

    public void TakeDamage(float damage, Color color = default, bool fromNova = false)
    {
        _currentHealth -= damage;

        if (DamageNumberSpawner.Instance != null)
        {
            Color c = color == default ? DamageNumberSpawner.ColorProjectile : color;
            DamageNumberSpawner.Instance.Spawn(transform.position, damage, c, transform, false);
        }

        if (_currentHealth <= 0)
            Die(fromNova);
    }

    private void Die(bool fromNova = false)
    {
        if (XPGemSpawner.Instance != null)
            XPGemSpawner.Instance.SpawnGems(transform.position, _xpValue);

        GameManager.Instance.AddKill();
        MetaProgressionManager.Instance.AddRunGold(_goldValue);

        if (fromNova)
        {
            if (_playerTransform != null)
            {
                PlayerController pc = _playerTransform.GetComponent<PlayerController>();
                if (pc != null) pc.ResetDashCooldown();
            }
        }

        ObjectPool.Instance.ReturnToPool(_poolTag, gameObject);
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        _speedMultiplier = multiplier;
    }
}