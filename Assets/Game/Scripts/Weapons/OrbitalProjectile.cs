using UnityEngine;
using System.Collections.Generic;

public class OrbitalProjectile : MonoBehaviour
{
    [SerializeField] private float _damage     = 15f;
    private float _hitCooldown = 0.5f;

    // Cooldowns par GameObject pour couvrir ennemis ET boss
    private Dictionary<GameObject, float> _hitCooldowns = new Dictionary<GameObject, float>();

    private void Update()
    {
        var keys = new List<GameObject>(_hitCooldowns.Keys);
        foreach (GameObject go in keys)
        {
            _hitCooldowns[go] -= Time.deltaTime;
            if (_hitCooldowns[go] <= 0f)
                _hitCooldowns.Remove(go);
        }
    }

    public void SetDamage(float damage)
    {
        _damage = damage;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Enemy")) return;
        if (_hitCooldowns.ContainsKey(other.gameObject)) return;

        // Ennemi normal
        EnemyBase enemy = other.GetComponent<EnemyBase>();
        if (enemy != null)
        {
            enemy.TakeDamage(_damage, DamageNumberSpawner.ColorOrbital);
            _hitCooldowns[other.gameObject] = _hitCooldown;
            return;
        }

        // Boss
        BossBase boss = other.GetComponent<BossBase>();
        if (boss != null)
        {
            boss.TakeDamage(_damage, DamageNumberSpawner.ColorOrbital);
            _hitCooldowns[other.gameObject] = _hitCooldown;
        }
    }
}