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
    private readonly Dictionary<int, EnemyBase> _currentlySlowed = new Dictionary<int, EnemyBase>();
    private readonly HashSet<int> _inRangeThisTick = new HashSet<int>();

    // AJOUTE - les boss n'etaient jamais ralentis, seulement endommages : la
    // branche BossBase de Tick() ne faisait que TakeDamage(), jamais
    // SetSpeedMultiplier() (que BossBase possede pourtant deja). Un seul boss actif
    // a la fois en pratique, donc une reference simple suffit plutot que
    // d'etendre le dictionnaire generique.
    private BossBase _currentlySlowedBoss = null;

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
                // AJOUTE - immunite totale des ennemis volants (Corbeau) : ni degats,
                // ni ralentissement. La boue est au sol, un volant ne la touche jamais.
                // Verifie avant tout le reste, y compris les degats.
                if (enemy.IsFlying) continue;

                if (_damagePerSecond > 0f)
                    enemy.TakeDamage(tickDamage, DamageNumberSpawner.ColorAOE);

                if (!_currentlySlowed.ContainsKey(enemyId))
                {
                    enemy.SetSpeedMultiplier(_slowMultiplier);
                    _currentlySlowed[enemyId] = enemy;
                }
                continue;
            }

            // MODIFIE - les boss subissent maintenant AUSSI le ralentissement, pas
            // seulement les degats. _inRangeThisTick.Add(enemyId) plus haut couvre
            // deja les boss (GetInstanceID fonctionne sur n'importe quel Collider),
            // donc la restauration de vitesse ci-dessous les detecte correctement
            // quand ils sortent du rayon.
            BossBase boss = hit.GetComponent<BossBase>();
            if (boss != null)
            {
                if (_damagePerSecond > 0f)
                    boss.TakeDamage(tickDamage);

                boss.SetSpeedMultiplier(_slowMultiplier);
                _currentlySlowedBoss = boss;
            }
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

        // AJOUTE - meme restauration pour le boss, s'il en avait un ralenti et
        // qu'il n'est plus dans le rayon ce tick (compare par instance ID, comme
        // les ennemis normaux, mais tracke a part faute de type commun EnemyBase/BossBase).
        if (_currentlySlowedBoss != null)
        {
            int bossId = _currentlySlowedBoss.GetInstanceID();
            if (!_inRangeThisTick.Contains(bossId))
            {
                _currentlySlowedBoss.SetSpeedMultiplier(1f);
                _currentlySlowedBoss = null;
            }
        }
    }

    private void ReturnToPool()
    {
        _isActive = false;

        foreach (var kvp in _currentlySlowed)
        {
            if (kvp.Value != null) kvp.Value.SetSpeedMultiplier(1f);
        }
        _currentlySlowed.Clear();

        // AJOUTE - meme securite pour le boss que pour les ennemis normaux.
        if (_currentlySlowedBoss != null)
        {
            _currentlySlowedBoss.SetSpeedMultiplier(1f);
            _currentlySlowedBoss = null;
        }

        if (ObjectPool.Instance != null)
            ObjectPool.Instance.ReturnToPool(_poolKey, gameObject);
        else
            Destroy(gameObject);
    }

    public void ForceExpire()
    {
        if (!_isActive) return;
        ReturnToPool();
    }

    private void OnDisable()
    {
        if (_currentlySlowed.Count > 0)
        {
            foreach (var kvp in _currentlySlowed)
            {
                if (kvp.Value != null) kvp.Value.SetSpeedMultiplier(1f);
            }
            _currentlySlowed.Clear();
        }

        // AJOUTE
        if (_currentlySlowedBoss != null)
        {
            _currentlySlowedBoss.SetSpeedMultiplier(1f);
            _currentlySlowedBoss = null;
        }
    }
}