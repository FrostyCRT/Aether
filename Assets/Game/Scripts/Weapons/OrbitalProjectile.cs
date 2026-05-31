using UnityEngine;

public class OrbitalProjectile : MonoBehaviour
{
    [SerializeField] private float _damage = 15f;

    // Dictionnaire pour éviter de toucher le même ennemi en boucle
    private System.Collections.Generic.Dictionary<EnemyBase, float> _hitCooldowns
        = new System.Collections.Generic.Dictionary<EnemyBase, float>();

    private float _hitCooldown = 0.5f; // Délai entre deux hits sur le même ennemi

    private void Update()
    {
        // On réduit les cooldowns de hit
        var keys = new System.Collections.Generic.List<EnemyBase>(_hitCooldowns.Keys);
        foreach (EnemyBase enemy in keys)
        {
            _hitCooldowns[enemy] -= Time.deltaTime;
            if (_hitCooldowns[enemy] <= 0f)
                _hitCooldowns.Remove(enemy);
        }
    }

    public void SetDamage(float damage)
    {
        _damage = damage;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            EnemyBase enemy = other.GetComponent<EnemyBase>();
            if (enemy == null) return;

            // On vérifie que l'ennemi n'est pas en cooldown
            if (!_hitCooldowns.ContainsKey(enemy))
            {
                enemy.TakeDamage(_damage, DamageNumberSpawner.ColorOrbital);
                _hitCooldowns[enemy] = _hitCooldown;
            }
        }
    }
}