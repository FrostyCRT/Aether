using UnityEngine;
using System.Collections.Generic;

// FUSION (2026-09-27) : Aura + Orbitaux, toutes les deux au palier max → cette arme les remplace [Kael].
// Réunit les deux mécaniques en une seule : le champ de dégâts/ralentissement autour du joueur (copié de WeaponAura)
// ET des éclats de mana en orbite (copiés de WeaponOrbital, même prefab poolé "OrbitalProjectile") — plus grands,
// plus nombreux, plus forts que la somme des deux armes d'origine. Aucun nouvel asset visuel nécessaire.
public class WeaponFusionManaVortex : MonoBehaviour
{
    [Header("Champ (hérité de l'Aura)")]
    [SerializeField] private float _radius = 8f;
    [SerializeField] private float _tickRate = 0.25f;
    [SerializeField] private float _slowMultiplier = 0.55f;

    // CORRIGÉ (2026-10-05) - retour utilisateur : "les fusions ne sont pas très efficaces [...] pas plus
    // puissant que 2 upgrades améliorées au max". Cause trouvée en relisant le code : _orbitalCount était fixé
    // à 3 EN DUR ici, alors qu'Orbitaux maxé seul atteint 5 orbitaux (2 de base + 3 upgrades) - fusionner perdait
    // donc jusqu'à 2 orbitaux sur 5 (-40% de dégâts orbitaux), jamais compensé par ailleurs. Mesuré en jeu avec
    // des ennemis DISPERSÉS ET MOBILES (pas des mannequins groupés comme la passe du 2026-09-27, qui masquait ce
    // genre de perte) : ×0,73 - plus FAIBLE que les 2 cartes maxées. Passé en valeur par défaut seulement (sert
    // de plancher si jamais Init() n'est pas appelé) ; Init() écrase maintenant cette valeur avec le VRAI nombre
    // d'orbitaux hérité (voir UpgradeData.ApplyFusionResult, WeaponOrbital.OrbitalCount).
    [Header("Éclats en orbite (hérités des Orbitaux)")]
    [SerializeField] private float _orbitRadius = 4f;
    [SerializeField] private float _orbitSpeed = 220f;
    [SerializeField] private int _orbitalCount = 5;

    // AJOUTE (2026-09-27) - manquait entièrement (retour utilisateur : "le A/E pour la range ne fonctionne pas") :
    // WeaponOrbital a ce contrôle, ces deux champs et le Update() associé n'avaient jamais été portés ici lors de
    // la fusion.
    [Header("Contrôle Range (A/E), hérité des Orbitaux")]
    [SerializeField] private float _minOrbitRadius = 1f;
    [SerializeField] private float _maxOrbitRadius = 10f;
    [SerializeField] private float _rangeChangeSpeed = 2f;

    private float _fieldDamagePerSecond;
    private float _orbitalDamage;

    private float _tickTimer = 0f;
    private static readonly Collider[] _overlapBuffer = new Collider[64];
    private readonly Dictionary<int, EnemyBase> _currentlySlowed = new Dictionary<int, EnemyBase>();
    private readonly HashSet<int> _inRangeThisTick = new HashSet<int>();
    private BossBase _currentlySlowedBoss = null;

    private readonly List<GameObject> _orbitals = new List<GameObject>();
    private readonly List<OrbitalProjectile> _orbitalScripts = new List<OrbitalProjectile>();
    private float _currentAngle = 0f;

    private LineRenderer _ringRenderer;
    [SerializeField] private float _ringGroundY = 0.05f;
    private const int RingSegments = 48;
    [SerializeField] private Color _ringColor = new Color(0.55f, 0.55f, 1f, 0.55f);

    // Appelé une fois par UpgradeData.ApplyFusionResult, avec les dégâts hérités des 2 armes d'origine et le VRAI
    // nombre d'orbitaux accumulé (voir note ci-dessus) - au moins _orbitalCount (5), jamais moins même si l'appelant
    // passe une valeur inférieure par erreur.
    public void Init(GameObject orbitalPrefab, float inheritedFieldDamage, float inheritedOrbitalDamage, int inheritedOrbitalCount)
    {
        _fieldDamagePerSecond = inheritedFieldDamage * 3f;
        _orbitalDamage = inheritedOrbitalDamage * 3f;
        _orbitalCount = Mathf.Max(_orbitalCount, inheritedOrbitalCount);
        CreateRingVisual();
        SpawnOrbitals(orbitalPrefab);
    }

    public void AddDamage(float value)
    {
        _fieldDamagePerSecond += _fieldDamagePerSecond * value;
        _orbitalDamage += _orbitalDamage * value;
        foreach (var s in _orbitalScripts) if (s != null) s.SetDamage(_orbitalDamage);
    }

    private void CreateRingVisual()
    {
        GameObject ringGO = new GameObject("ManaVortexRing");
        ringGO.transform.SetParent(transform, false);
        // CORRIGE (2026-10-16, audit) - même bug "hauteur Y" que l'Aura (corrigé là-bas le 2026-10-06) : le pivot du
        // joueur est à hauteur de torse (~1,5) et la hauteur des points était en plus multipliée par le rayon
        // (0.05 x 8 = 0.4) - l'anneau flottait vers ~1,9. Même calcul que WeaponAura : décalage local jusqu'au sol.
        ringGO.transform.localPosition = new Vector3(0f, _ringGroundY - transform.position.y, 0f);
        _ringRenderer = ringGO.AddComponent<LineRenderer>();
        _ringRenderer.useWorldSpace = false;
        _ringRenderer.loop = true;
        _ringRenderer.positionCount = RingSegments;
        _ringRenderer.widthMultiplier = 0.12f;
        _ringRenderer.material = new Material(Shader.Find("Sprites/Default"));
        _ringRenderer.startColor = _ringColor;
        _ringRenderer.endColor = _ringColor;
        for (int i = 0; i < RingSegments; i++)
        {
            float angle = (i / (float)RingSegments) * Mathf.PI * 2f;
            _ringRenderer.SetPosition(i, new Vector3(Mathf.Cos(angle) * _radius, 0f, Mathf.Sin(angle) * _radius));
        }
    }

    private void SpawnOrbitals(GameObject orbitalPrefab)
    {
        if (ObjectPool.Instance == null || orbitalPrefab == null) return;
        for (int i = 0; i < _orbitalCount; i++)
        {
            GameObject go = ObjectPool.Instance.Get("OrbitalProjectile", transform.position, Quaternion.identity);
            if (go == null) continue;
            go.transform.SetParent(transform);
            _orbitals.Add(go);
            OrbitalProjectile proj = go.GetComponent<OrbitalProjectile>();
            if (proj != null)
            {
                proj.SetDamage(_orbitalDamage);
                _orbitalScripts.Add(proj);
            }
        }
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        UpdateOrbitals();

        _tickTimer += Time.deltaTime;
        if (_tickTimer >= _tickRate)
        {
            ApplyFieldTick();
            _tickTimer = 0f;
        }
    }

    private void UpdateOrbitals()
    {
        if (_orbitals.Count == 0) return;

        if (GameInput.Held(GameAction.OrbitShrink))
            _orbitRadius = Mathf.Max(_minOrbitRadius, _orbitRadius - _rangeChangeSpeed * Time.deltaTime);
        if (GameInput.Held(GameAction.OrbitGrow))
            _orbitRadius = Mathf.Min(_maxOrbitRadius, _orbitRadius + _rangeChangeSpeed * Time.deltaTime);

        _currentAngle += _orbitSpeed * Time.deltaTime;
        float angleStep = 360f / _orbitals.Count;
        for (int i = 0; i < _orbitals.Count; i++)
        {
            if (_orbitals[i] == null) continue;
            float angle = (_currentAngle + angleStep * i) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _orbitRadius;
            _orbitals[i].transform.position = transform.position + offset;
        }
    }

    private void ApplyFieldTick()
    {
        _inRangeThisTick.Clear();
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _radius, _overlapBuffer, EnemyLayers.Mask);
        float tickDamage = _fieldDamagePerSecond * _tickRate * PlayerBuffs.OutgoingDamageMultiplier; // + Concentration / Surpuissance (2026-10-16, audit)
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
                enemy.TakeDamage(tickDamage, DamageNumberSpawner.ColorManaVortex);
                if (!_currentlySlowed.ContainsKey(enemyId)) { enemy.SetSpeedMultiplier(_slowMultiplier); _currentlySlowed[enemyId] = enemy; }
                continue;
            }
            BossBase boss = hit.GetComponentInParent<BossBase>();
            if (boss != null)
            {
                if (!_inRangeThisTick.Add(boss.GetInstanceID())) continue;
                boss.TakeDamage(tickDamage, DamageNumberSpawner.ColorManaVortex);
                boss.SetSpeedMultiplier(_slowMultiplier);
                _currentlySlowedBoss = boss;
            }
        }
        List<int> toRemove = null;
        foreach (var kvp in _currentlySlowed)
        {
            if (_inRangeThisTick.Contains(kvp.Key)) continue;
            if (kvp.Value != null) kvp.Value.SetSpeedMultiplier(1f);
            (toRemove ??= new List<int>()).Add(kvp.Key);
        }
        if (toRemove != null) foreach (int id in toRemove) _currentlySlowed.Remove(id);
        if (_currentlySlowedBoss != null && !_inRangeThisTick.Contains(_currentlySlowedBoss.GetInstanceID()))
        {
            _currentlySlowedBoss.SetSpeedMultiplier(1f);
            _currentlySlowedBoss = null;
        }
    }

    private void OnDestroy()
    {
        foreach (var kvp in _currentlySlowed) if (kvp.Value != null) kvp.Value.SetSpeedMultiplier(1f);
        if (_currentlySlowedBoss != null) _currentlySlowedBoss.SetSpeedMultiplier(1f);
        if (ObjectPool.Instance != null)
            foreach (var go in _orbitals) if (go != null) ObjectPool.Instance.ReturnToPool("OrbitalProjectile", go);
        if (_ringRenderer != null) Destroy(_ringRenderer.gameObject);
    }
}
