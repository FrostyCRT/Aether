using UnityEngine;
using System.Collections.Generic;

// Instance poolée spawnée par WeaponMudPuddle. Reste statique au sol pendant _duration,
// applique un ralentissement + DPS léger aux ennemis dans son rayon, puis retourne au pool.
public class MudPuddleZone : MonoBehaviour
{
    private float _duration;
    private float _radius;
    private float _slowMultiplier;
    private float _damagePerSecond;

    private float _lifeTimer;
    private float _tickTimer;
    private const float TickRate = 0.25f;

    private static readonly Collider[] _overlapBuffer = new Collider[32];
    // Réutilisé d'un cycle de vie à l'autre (l'objet est poolé, jamais détruit) — vidé
    // explicitement dans Init() pour ne pas hériter de l'état du cycle de vie précédent.
    private readonly Dictionary<int, EnemyBase> _currentlySlowed = new Dictionary<int, EnemyBase>();
    private readonly HashSet<int> _inRangeThisTick = new HashSet<int>();

    private string _poolKey;
    private bool _isActive = false;

    private void Awake()
    {
        _poolKey = name.Replace("(Clone)", "").Trim();
    }

    public void Init(float duration, float radius, float slowMultiplier, float damagePerSecond)
    {
        _duration = duration;
        _radius = radius;
        _slowMultiplier = slowMultiplier;
        _damagePerSecond = damagePerSecond;

        _lifeTimer = 0f;
        _tickTimer = 0f;
        _currentlySlowed.Clear();
        _isActive = true;

        // HYPOTHÈSE — suppose que le prefab visuel est modélisé pour un diamètre de 1 unité
        // à l'échelle (1,1,1). Si ton prefab a une taille de base différente, ajuste le facteur
        // ci-dessous (ou mieux : ajuste directement le mesh/prefab pour matcher cette convention,
        // plus simple à maintenir si tu ajoutes d'autres zones circulaires plus tard).
        transform.localScale = new Vector3(radius * 2f, transform.localScale.y, radius * 2f);
    }

    private void Update()
    {
        if (!_isActive) return;
        if (GameManager.Instance != null && (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused)) return;

        _lifeTimer += Time.deltaTime;
        if (_lifeTimer >= _duration)
        {
            ReturnToPool();
            return;
        }

        _tickTimer += Time.deltaTime;
        if (_tickTimer >= TickRate)
        {
            Tick();
            _tickTimer = 0f;
        }
    }

    private void Tick()
    {
        _inRangeThisTick.Clear();
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _radius, _overlapBuffer);
        float tickDamage = _damagePerSecond * TickRate;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _overlapBuffer[i];
            if (hit == null || !hit.CompareTag("Enemy")) continue;

            int enemyId = hit.GetInstanceID();
            if (_inRangeThisTick.Contains(enemyId)) continue;
            _inRangeThisTick.Add(enemyId);

            EnemyBase enemy = hit.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                if (_damagePerSecond > 0f)
                    enemy.TakeDamage(tickDamage, DamageNumberSpawner.ColorAOE);

                if (!_currentlySlowed.ContainsKey(enemyId))
                {
                    enemy.SetSpeedMultiplier(_slowMultiplier);
                    _currentlySlowed[enemyId] = enemy;
                }
                continue;
            }

            // Les boss subissent les dégâts mais pas le ralentissement (cohérent avec WeaponAura)
            BossBase boss = hit.GetComponent<BossBase>();
            if (boss != null && _damagePerSecond > 0f)
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

    private void ReturnToPool()
    {
        _isActive = false;

        // IMPORTANT — restaure la vitesse de TOUS les ennemis encore ralentis avant de
        // disparaître. Sans ça, un ennemi qui reste dans la flaque jusqu'à son expiration
        // garderait son SetSpeedMultiplier(_slowMultiplier) appliqué indéfiniment (bug de
        // ralentissement permanent, silencieux, difficile à repérer en playtest).
        foreach (var kvp in _currentlySlowed)
        {
            if (kvp.Value != null) kvp.Value.SetSpeedMultiplier(1f);
        }
        _currentlySlowed.Clear();

        if (ObjectPool.Instance != null)
            ObjectPool.Instance.ReturnToPool(_poolKey, gameObject);
        else
            Destroy(gameObject);
    }

    // AJOUTÉ — appelé par WeaponMudPuddle quand une nouvelle vague démarre alors que
    // cette flaque n'a pas encore atteint sa durée de vie naturelle. Réutilise exactement
    // la même logique de nettoyage que l'expiration normale (restauration de vitesse incluse),
    // juste déclenchée en avance plutôt qu'au bout du timer.
    public void ForceExpire()
    {
        if (!_isActive) return;
        ReturnToPool();
    }

    // Sécurité supplémentaire : si l'objet est désactivé par un autre chemin que ReturnToPool()
    // (ex: ClearAllEnemies-like cleanup, changement de scène), on ne laisse jamais un ennemi
    // ralenti orphelin.
    private void OnDisable()
    {
        if (_currentlySlowed.Count == 0) return;
        foreach (var kvp in _currentlySlowed)
        {
            if (kvp.Value != null) kvp.Value.SetSpeedMultiplier(1f);
        }
        _currentlySlowed.Clear();
    }
}