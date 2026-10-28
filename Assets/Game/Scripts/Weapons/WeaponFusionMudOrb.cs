using UnityEngine;
using System.Collections.Generic;

// FUSION (2026-10-18) : Orbe rebondissant + Boue, toutes les deux au palier max → cette arme les remplace [universelle].
// Remplace la fusion "Orbes Jumeaux" (Double Tir + Orbe rebondissant), supprimée à la demande de l'utilisateur
// ("je la trouve très incohérente et inutile"). "Sillage Boueux" relie ses 2 armes sources par un vrai mécanisme : les
// orbes rebondissent dans toute la zone visible (identité Orbe rebondissant) et laissent derrière eux un sillage de
// flaques qui ralentissent et blessent (identité Boue). Le sillage suit la position réelle et mouvante des orbes : il
// croise forcément la horde, contrairement à un anneau de flaques à distance fixe du joueur.
//
// PASSE VISUELLE ET D'ÉQUILIBRAGE (2026-10-28, campagne de refonte des fusions) - la mécanique est conservée, mais :
//  - les orbes ne sont plus des "BouncingOrbProjectile" poolés (grand disque jaune, identique à l'arme source) : ce sont
//    des cristaux azur à cœur ambré dessinés par MudWakeVisual, déplacés et testés géométriquement ici ;
//  - une orbe dont la trajectoire était presque parallèle à un bord se faisait traîner le long de l'écran quand le joueur
//    avançait (mesuré en jeu : les 2 orbes collées au bord gauche sur les 2 captures). Après chaque rebond, chaque axe
//    garde au moins MinAxis de vitesse : l'orbe repart toujours franchement vers l'intérieur ;
//  - les flaques gardent leur zone d'effet (MudPuddleZone, ralentissement + dégâts) mais leur disque bleu-vert opaque
//    est remplacé par une tache de boue sombre semée d'éclats de cristal azur (MudWakeVisual) ;
//  - puissance mesurée pour la première fois (elle était "en attente de validation" depuis sa création).
public class WeaponFusionMudOrb : MonoBehaviour
{
    [Header("Orbes (hérités de l'Orbe rebondissant)")]
    [SerializeField] private int _orbCount = 2;
    [SerializeField] private int _maxOrbCount = 4;
    [SerializeField] private float _speedMultiplier = 1.25f;
    [SerializeField] private float _hitRadius = 1f;
    [SerializeField] private float _hitCooldown = 0.5f;

    [Header("Sillage de boue (hérité de la Boue)")]
    // RÉGLÉ (2026-10-18, retour utilisateur : "ça doit vraiment faire comme une trainée, avec un délai entre chaque boue
    // de environ moins d'1 sec" puis "je veux que tu mette 0,7 au lieu de 0,45").
    [SerializeField] private float _trailDropInterval = 0.7f;
    [SerializeField] private float _puddleDuration = 5f;
    [SerializeField] private float _puddleRadius = 1.8f;
    [SerializeField] private float _slowMultiplier = 0.6f;
    [SerializeField] private float _groundY = 0.2f;

    private const float MinAxis = 0.35f;
    private const float BodyHitMargin = 0.35f;

    private float _orbDamage;
    private float _orbSpeed;
    private float _puddleDamagePerSecond;
    private GameObject _puddlePrefab;
    private float _trailTimer;
    private bool _initialized;

    private Vector3[] _orbPos, _orbPrev, _orbDir;
    private readonly Dictionary<int, float> _nextHit = new Dictionary<int, float>();
    private readonly List<int> _removeBuffer = new List<int>();
    private readonly HashSet<int> _seenThisSweep = new HashSet<int>();
    private static readonly Collider[] _hitBuffer = new Collider[64];

    private readonly List<GameObject> _trailPuddles = new List<GameObject>();
    private MudWakeVisual _visual;

    private Vector3 Center => new Vector3(transform.position.x, 0f, transform.position.z);

    // Appelé une fois par UpgradeData.ApplyFusionResult, juste après AddComponent. inheritedOrbDamage/inheritedSpeed/
    // inheritedOrbCount viennent de WeaponBouncingOrb (déjà boostés par la Réputation/les picks précédents) ;
    // inheritedPuddleDamage vient de WeaponMudPuddle, même principe.
    public void Init(GameObject puddlePrefab, float inheritedOrbDamage, float inheritedSpeed, int inheritedOrbCount, float inheritedPuddleDamage)
    {
        _puddlePrefab = puddlePrefab;

        // MESURÉ (2026-10-28) en partie réelle, Violent 3:20 -> 4:20, joueur invincible qui tourne lentement en rond, tir de
        // base et Nova coupés, dégâts des deux côtés divisés par 6 (au-delà, la fusion tue la horde aussi vite qu'elle
        // apparaît et la mesure plafonne vers 3 000 PV/s). Orbe + Boue maxés = 1 100 PV/s. Retenu : orbe x4 (rayon de
        // contact 1 m, celui du grand disque de l'ancien orbe), sillage x2,6 = 2 753 PV/s, soit x2,5 (fourchette des
        // autres fusions, x2,2 à x2,9). Avant cette passe : orbe x2,5, sillage x1,5.
        _orbDamage = Mathf.Max(1f, inheritedOrbDamage) * 4f;
        _puddleDamagePerSecond = Mathf.Max(1f, inheritedPuddleDamage) * 2.6f;
        _orbSpeed = Mathf.Max(1f, inheritedSpeed) * _speedMultiplier;
        _orbCount = Mathf.Clamp(Mathf.Max(_orbCount, inheritedOrbCount), 1, _maxOrbCount);

        _orbPos = new Vector3[_orbCount];
        _orbPrev = new Vector3[_orbCount];
        _orbDir = new Vector3[_orbCount];
        float start = Random.Range(0f, 360f);
        for (int i = 0; i < _orbCount; i++)
        {
            float a = (start + 360f * i / _orbCount + 45f) * Mathf.Deg2Rad;
            _orbDir[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            _orbPos[i] = _orbPrev[i] = Center;
        }

        _visual = new GameObject("MudWake").AddComponent<MudWakeVisual>();
        _visual.Build(_orbCount);
        _initialized = true;
    }

    public void AddDamage(float value)
    {
        _orbDamage += _orbDamage * value;
        _puddleDamagePerSecond += _puddleDamagePerSecond * value;
    }

    public int OrbCount => _orbCount;

    private void Update()
    {
        if (!_initialized) return;
        if (GameManager.Instance != null && (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused)) return;

        float dt = Time.deltaTime;
        GetBounds(out float minX, out float maxX, out float minZ, out float maxZ);
        float dmg = _orbDamage * PlayerBuffs.OutgoingDamageMultiplier;
        float now = Time.time;

        for (int i = 0; i < _orbCount; i++)
        {
            _orbPrev[i] = _orbPos[i];
            Vector3 d = _orbDir[i];
            Vector3 next = _orbPos[i] + d * (_orbSpeed * dt);
            bool bounced = false;
            if ((next.x <= minX && d.x < 0f) || (next.x >= maxX && d.x > 0f)) { d.x = -d.x; bounced = true; }
            if ((next.z <= minZ && d.z < 0f) || (next.z >= maxZ && d.z > 0f)) { d.z = -d.z; bounced = true; }
            if (bounced)
            {
                d = KeepAwayFromAxes(d);
                if (_visual != null) _visual.Bounce(i);
            }
            // Le bord de l'écran suit le joueur : une orbe restée derrière est ramenée à l'intérieur.
            next.x = Mathf.Clamp(next.x, minX, maxX);
            next.z = Mathf.Clamp(next.z, minZ, maxZ);
            _orbDir[i] = d;
            _orbPos[i] = next;

            Sweep(_orbPrev[i], _orbPos[i], dmg, now);
        }
        if (_nextHit.Count > 512) PruneCooldowns(now);

        _trailPuddles.RemoveAll(go => go == null || !go.activeInHierarchy);
        _trailTimer += dt;
        if (_trailTimer >= _trailDropInterval)
        {
            _trailTimer = 0f;
            DropTrailPuddles();
        }

        if (_visual != null) _visual.Render(_orbPos, _orbDir, _orbCount);
    }

    // Une direction trop rasante (un axe presque nul) reste collée au bord : chaque axe garde au moins MinAxis.
    private static Vector3 KeepAwayFromAxes(Vector3 d)
    {
        if (Mathf.Abs(d.x) < MinAxis) d.x = MinAxis * (d.x < 0f ? -1f : 1f);
        if (Mathf.Abs(d.z) < MinAxis) d.z = MinAxis * (d.z < 0f ? -1f : 1f);
        d.y = 0f;
        return d.normalized;
    }

    // Contact : tous les ennemis traversés par le segment parcouru cette image (corps compris), un coup par ennemi
    // toutes les _hitCooldown secondes (comme l'Orbe rebondissant, qui traverse la horde au lieu de disparaître).
    private void Sweep(Vector3 from, Vector3 to, float dmg, float now)
    {
        Vector3 a = from + Vector3.up * MudWakeVisual.OrbY;
        Vector3 b = to + Vector3.up * MudWakeVisual.OrbY;
        int count = Physics.OverlapCapsuleNonAlloc(a, b, _hitRadius, _hitBuffer, EnemyLayers.Mask);
        _seenThisSweep.Clear();
        for (int i = 0; i < count; i++)
        {
            Collider col = _hitBuffer[i];
            if (col == null || !col.CompareTag("Enemy")) continue;
            col.TryGetComponent(out EnemyBase enemy);
            BossBase boss = enemy == null ? col.GetComponentInParent<BossBase>() : null;
            if (enemy == null && boss == null) continue;

            int id = enemy != null ? enemy.GetInstanceID() : boss.GetInstanceID();
            if (!_seenThisSweep.Add(id)) continue;
            if (_nextHit.TryGetValue(id, out float next) && now < next) continue;
            if (boss != null && !boss.IsOnBody(b, BodyHitMargin)) continue;   // boîte englobante touchée, pas le corps

            _nextHit[id] = now + _hitCooldown;
            if (enemy != null) enemy.TakeDamage(dmg, DamageNumberSpawner.ColorBouncingOrb);
            else boss.TakeDamage(dmg, DamageNumberSpawner.ColorBouncingOrb);
            ImpactSparkVFX.Spawn(b, MudWakeVisual.Azure);
        }
    }

    private void PruneCooldowns(float now)
    {
        _removeBuffer.Clear();
        foreach (var kvp in _nextHit) if (kvp.Value <= now) _removeBuffer.Add(kvp.Key);
        for (int i = 0; i < _removeBuffer.Count; i++) _nextHit.Remove(_removeBuffer[i]);
    }

    private void DropTrailPuddles()
    {
        if (ObjectPool.Instance == null || _puddlePrefab == null) return;
        for (int i = 0; i < _orbCount; i++)
        {
            Vector3 spawnPos = _orbPos[i];
            spawnPos.y = _groundY;

            GameObject puddleGO = ObjectPool.Instance.Get("MudPuddleZone", spawnPos, Quaternion.identity);
            if (puddleGO == null) continue;

            puddleGO.TryGetComponent(out MudPuddleZone zone);
            if (zone != null)
            {
                zone.Init(_puddleDuration, _puddleRadius, _slowMultiplier, _puddleDamagePerSecond);
                zone.HideVisual();
            }
            if (_visual != null) _visual.AddPuddle(spawnPos, _puddleRadius, _puddleDuration);
            _trailPuddles.Add(puddleGO);
        }
    }

    // Rectangle visible par la caméra, projeté au sol, croisé avec les murs de la carte (même règle que l'Orbe
    // rebondissant et la Valse des Lames).
    private void GetBounds(out float minX, out float maxX, out float minZ, out float maxZ)
    {
        Vector3 c = Center;
        float cMinX = c.x - 12f, cMaxX = c.x + 12f, cMinZ = c.z - 12f, cMaxZ = c.z + 12f;
        Camera cam = Camera.main;
        if (cam != null)
        {
            Plane ground = new Plane(Vector3.up, new Vector3(0f, MudWakeVisual.OrbY, 0f));
            Vector3 p0 = GroundPoint(cam, ground, new Vector3(0f, 0f, 0f), c);
            Vector3 p1 = GroundPoint(cam, ground, new Vector3(1f, 1f, 0f), c);
            cMinX = Mathf.Min(p0.x, p1.x); cMaxX = Mathf.Max(p0.x, p1.x);
            cMinZ = Mathf.Min(p0.z, p1.z); cMaxZ = Mathf.Max(p0.z, p1.z);
        }
        const float margin = 0.6f;
        minX = Mathf.Max(cMinX, MapBoundaryUtils.MinX) + margin;
        maxX = Mathf.Min(cMaxX, MapBoundaryUtils.MaxX) - margin;
        minZ = Mathf.Max(cMinZ, MapBoundaryUtils.MinZ) + margin;
        maxZ = Mathf.Min(cMaxZ, MapBoundaryUtils.MaxZ) - margin;
        if (minX > maxX) { float m = (minX + maxX) * 0.5f; minX = maxX = m; }
        if (minZ > maxZ) { float m = (minZ + maxZ) * 0.5f; minZ = maxZ = m; }
    }

    private static Vector3 GroundPoint(Camera cam, Plane ground, Vector3 viewport, Vector3 fallback)
    {
        Ray ray = cam.ViewportPointToRay(viewport);
        return ground.Raycast(ray, out float enter) ? ray.GetPoint(enter) : fallback;
    }

    // Sans ça, les flaques de sillage encore actives au moment où cette fusion disparaît (run terminé en cours d'effet)
    // resteraient orphelines dans l'arène.
    private void OnDestroy()
    {
        if (_visual != null) Destroy(_visual.gameObject);
        foreach (GameObject puddle in _trailPuddles)
        {
            if (puddle == null) continue;
            puddle.TryGetComponent(out MudPuddleZone zone);
            if (zone != null) zone.ForceExpire();
        }
    }
}
