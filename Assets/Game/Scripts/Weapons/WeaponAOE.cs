using UnityEngine;

public class WeaponAOE : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _damage   = 15f;
    [SerializeField] private float _radius   = 3f;
    [SerializeField] private float _fireRate = 0.5f; // Réduit à 0.5 pulse/sec

    [Header("Visuel")]
    [SerializeField] private GameObject _pulseVisual;

    private float _cooldownTimer  = 0f;
    private float _animationTimer = -1f; // -1 = pas d'animation en cours
    private float _animDuration   = 0.3f;

    [Header("Limites")]
    [SerializeField] private float _maxRadius = 8f; // Rayon maximum
    public bool IsMaxRadius() => _radius >= _maxRadius;

    public void AddRadius(float value)
    {
        _radius = Mathf.Min(_radius + _radius * value, _maxRadius);
    }

    private void Update()
    {
        if (GameManager.Instance.IsGameOver) return;

        _cooldownTimer += Time.deltaTime;
        if (_cooldownTimer >= 1f / _fireRate)
        {
            Pulse();
            _cooldownTimer = 0f;
        }

        if (_animationTimer >= 0f)
        {
            _animationTimer += Time.deltaTime;
            float scale = Mathf.Lerp(0f, _radius * 2f, _animationTimer / _animDuration);
            _pulseVisual.transform.localScale = new Vector3(scale, 0.1f, scale);

            if (_animationTimer >= _animDuration)
            {
                _pulseVisual.SetActive(false);
                _animationTimer = -1f;
            }
        }
    }

    // Déplace le OverlapSphere dans FixedUpdate — plus adapté à la physique
    private void FixedUpdate()
    {
        // Rien ici, le pulse est géré dans Update
    }

    private void Pulse()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, _radius);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                // Ennemis normaux
                EnemyBase enemy = hit.GetComponent<EnemyBase>();
                if (enemy != null)
                {
                    enemy.TakeDamage(_damage, DamageNumberSpawner.ColorAOE);
                    continue;
                }

                // Boss
                BossBase boss = hit.GetComponent<BossBase>();
                if (boss != null)
                    boss.TakeDamage(_damage, DamageNumberSpawner.ColorAOE);
            }
        }

        // Lance l'animation
        if (_pulseVisual != null)
        {
            _pulseVisual.SetActive(true);
            _pulseVisual.transform.localScale = Vector3.zero;
            _animationTimer = 0f;
        }
    }

    public void AddDamage(float value) => _damage  += _damage * value;
    

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _radius);
    }

    public void Init(GameObject pulseVisualPrefab)
    {
        GameObject visual = Instantiate(pulseVisualPrefab, transform.position, Quaternion.identity);
        visual.transform.SetParent(transform);
        _pulseVisual = visual;
        _pulseVisual.SetActive(false);
    }

    public void AddFireRate(float value) => _fireRate += _fireRate * value;
}