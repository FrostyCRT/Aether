using UnityEngine;
using System.Collections.Generic;

public class WeaponAOE : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _damage = 15f;
    [SerializeField] private float _radius = 3f;
    [SerializeField] private float _fireRate = 0.5f;

    [Header("Visuel")]
    [SerializeField] private GameObject _pulseVisual;

    private float _cooldownTimer = 0f;
    private float _animationTimer = -1f; // -1 = pas d'animation en cours
    private float _animDuration = 0.3f;

    [Header("Limites")]
    [SerializeField] private float _maxRadius = 8f;
    public bool IsMaxRadius() => _radius >= _maxRadius;

    // Tableau tampon partagé pour annuler complètement le Garbage Collector (capacité de 128 ennemis par pulsation)
    private static readonly Collider[] _aoeOverlapBuffer = new Collider[128];

    // Liste de cache pour mémoriser les ID des ennemis touchés DURANT la pulsation actuelle (évite les doubles dégâts)
    private readonly HashSet<int> _hitEnemiesThisPulse = new HashSet<int>();

    public void AddRadius(float value)
    {
        _radius = Mathf.Min(_radius + _radius * value, _maxRadius);
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

        _cooldownTimer += Time.deltaTime;

        // Sécurité anti-crash : on s'assure que le fireRate est supérieur à 0 avant la division
        float currentCooldownDuration = _fireRate > 0f ? (1f / _fireRate) : 9999f;

        if (_cooldownTimer >= currentCooldownDuration)
        {
            Pulse();
            _cooldownTimer = 0f;
        }

        // Gestion de l'animation d'extension visuelle du cercle
        if (_animationTimer >= 0f)
        {
            _animationTimer += Time.deltaTime;
            float scale = Mathf.Lerp(0f, _radius * 2f, _animationTimer / _animDuration);

            if (_pulseVisual != null)
            {
                _pulseVisual.transform.localScale = new Vector3(scale, 0.1f, scale);
            }

            if (_animationTimer >= _animDuration)
            {
                if (_pulseVisual != null) _pulseVisual.SetActive(false);
                _animationTimer = -1f;
            }
        }
    }

    private void Pulse()
    {
        // On vide le dictionnaire de suivi au début de chaque nouvelle pulsation autonome
        _hitEnemiesThisPulse.Clear();

        // Détection physique instantanée sur l'intégralité du rayon de l'onde
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _radius, _aoeOverlapBuffer);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _aoeOverlapBuffer[i];
            if (hit == null || !hit.CompareTag("Enemy")) continue;

            // Récupération de l'ID unique de l'entité physique pour le filtrage
            int enemyId = hit.GetInstanceID();

            // Si cet ennemi a DÉJÀ encaissé les dégâts de cette onde de choc précise, on l'ignore
            if (_hitEnemiesThisPulse.Contains(enemyId)) continue;

            // Tentative de récupération directe du composant de base
            EnemyBase enemy = hit.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                enemy.TakeDamage(_damage, DamageNumberSpawner.ColorAOE);
                _hitEnemiesThisPulse.Add(enemyId); // Marqué comme touché
                continue;
            }

            // Si ce n'est pas un ennemi standard, on vérifie si c'est un boss
            BossBase boss = hit.GetComponent<BossBase>();
            if (boss != null)
            {
                boss.TakeDamage(_damage, DamageNumberSpawner.ColorAOE);
                _hitEnemiesThisPulse.Add(enemyId); // Marqué comme touché
            }
        }

        // Déclenchement de l'animation visuelle
        if (_pulseVisual != null)
        {
            _pulseVisual.SetActive(true);
            _pulseVisual.transform.localScale = Vector3.zero;
            _animationTimer = 0f;
        }
    }

    public void AddDamage(float value) => _damage += _damage * value;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _radius);
    }

    public void Init(GameObject pulseVisualPrefab)
    {
        if (pulseVisualPrefab == null) return;

        GameObject visual = Instantiate(pulseVisualPrefab, transform.position, Quaternion.identity);
        visual.transform.SetParent(transform);
        _pulseVisual = visual;
        _pulseVisual.SetActive(false);
    }

    public void AddFireRate(float value) => _fireRate += _fireRate * value;
}