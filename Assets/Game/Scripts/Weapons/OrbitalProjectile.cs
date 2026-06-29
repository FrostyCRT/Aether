using UnityEngine;
using System.Collections.Generic;

public class OrbitalProjectile : MonoBehaviour
{
    [SerializeField] private float _damage = 15f;
    private readonly float _hitCooldown = 0.5f;

    // Cache des timestamps de dernière frappe : ZÉRO UPDATE requis pour nettoyer
    private readonly Dictionary<int, float> _lastHitTimestamps = new Dictionary<int, float>();

    public void SetDamage(float damage)
    {
        _damage = damage;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Enemy")) return;

        // Utilisation de l'InstanceID unique (int) au lieu du GameObject complet pour éviter les fuites de mémoire
        int enemyId = other.GetInstanceID();
        float currentTime = Time.time;

        if (_lastHitTimestamps.TryGetValue(enemyId, out float lastHitTime))
        {
            if (currentTime - lastHitTime < _hitCooldown)
            {
                return; // L'ennemi est encore sous le coup du cooldown (I-frame)
            }
        }

        // On cherche d'abord la base commune d'ennemi normal
        EnemyBase enemy = other.GetComponent<EnemyBase>();
        if (enemy != null)
        {
            enemy.TakeDamage(_damage, DamageNumberSpawner.ColorOrbital);
            _lastHitTimestamps[enemyId] = currentTime;
            return;
        }

        // Si ce n'est pas un ennemi basique, on check le Boss
        BossBase boss = other.GetComponent<BossBase>();
        if (boss != null)
        {
            boss.TakeDamage(_damage, DamageNumberSpawner.ColorOrbital);
            _lastHitTimestamps[enemyId] = currentTime;
        }
    }

    // Si le projectile est désactivé ou replacé dans un Object Pool, on nettoie son dictionnaire
    private void OnDisable()
    {
        _lastHitTimestamps.Clear();
    }
}