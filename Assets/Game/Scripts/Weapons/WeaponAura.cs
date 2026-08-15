using UnityEngine;
using System.Collections.Generic;
public class WeaponAura : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _damagePerSecond = 8f;
    [SerializeField] private float _radius = 3.5f;
    [SerializeField] private float _tickRate = 0.25f;
    [Header("Ralentissement léger")]
    [SerializeField] private float _slowMultiplier = 0.85f; // -15%
    // AJOUTÉ — plancher de sécurité : empêche le multiplicateur de tomber à 0 ou négatif
    // si le palier 3 est cumulé avec d'autres sources de slow à l'avenir (immobilisation totale
    // non voulue, à moins que ce soit explicitement designé plus tard).
    [SerializeField] private float _minSlowMultiplier = 0.4f;
    private float _tickTimer = 0f;
    private static readonly Collider[] _auraOverlapBuffer = new Collider[64];
    private readonly Dictionary<int, EnemyBase> _currentlySlowed = new Dictionary<int, EnemyBase>();
    private readonly HashSet<int> _inRangeThisTick = new HashSet<int>();
    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        _tickTimer += Time.deltaTime;
        if (_tickTimer >= _tickRate)
        {
            ApplyAuraTick();
            _tickTimer = 0f;
        }
    }
    private void ApplyAuraTick()
    {
        _inRangeThisTick.Clear();
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _radius, _auraOverlapBuffer);
        float tickDamage = _damagePerSecond * _tickRate;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _auraOverlapBuffer[i];
            if (hit == null || !hit.CompareTag("Enemy")) continue;
            int enemyId = hit.GetInstanceID();
            if (_inRangeThisTick.Contains(enemyId)) continue;
            _inRangeThisTick.Add(enemyId);
            EnemyBase enemy = hit.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                enemy.TakeDamage(tickDamage, DamageNumberSpawner.ColorAOE);
                if (!_currentlySlowed.ContainsKey(enemyId))
                {
                    enemy.SetSpeedMultiplier(_slowMultiplier);
                    _currentlySlowed[enemyId] = enemy;
                }
                continue;
            }
            BossBase boss = hit.GetComponent<BossBase>();
            if (boss != null)
                boss.TakeDamage(tickDamage);
        }
        // Restaure la vitesse normale des ennemis sortis du rayon depuis le tick précédent
        List<int> toRemove = null;
        foreach (var kvp in _currentlySlowed)
        {
            if (_inRangeThisTick.Contains(kvp.Key)) continue;
            if (kvp.Value != null) kvp.Value.SetSpeedMultiplier(1f);
            (toRemove ??= new List<int>()).Add(kvp.Key);
        }
        if (toRemove != null)
            foreach (int id in toRemove) _currentlySlowed.Remove(id);
    }
    public void AddDamage(float value) => _damagePerSecond += _damagePerSecond * value;
    public void AddRadius(float value) => _radius += _radius * value;

    // AJOUTÉ — appelé par le palier 3 de la carte Aura. value = réduction additionnelle
    // du multiplicateur de vitesse (ex: 0.10 pour passer de -15% à -25%).
    // Applique aussi le nouveau multiplicateur aux ennemis déjà ralentis ce tick,
    // pour ne pas attendre le prochain tick avant que le renfort soit visible.
    public void AddSlowStrength(float value)
    {
        _slowMultiplier = Mathf.Max(_minSlowMultiplier, _slowMultiplier - value);
        foreach (var kvp in _currentlySlowed)
        {
            if (kvp.Value != null) kvp.Value.SetSpeedMultiplier(_slowMultiplier);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _radius);
    }
}