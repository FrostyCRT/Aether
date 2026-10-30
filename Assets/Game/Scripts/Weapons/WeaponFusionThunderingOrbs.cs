using UnityEngine;
using System.Collections.Generic;

// FUSION (2026-09-27) : Orbitaux + Foudre, toutes les deux au palier max → cette arme les remplace [universelle].
//
// REFONTE COMPLÈTE (2026-10-27, retour utilisateur après la Couronne Prismatique : "est-ce qu'on repasse sur les autres
// fusions du jeu pour faire la même refonte partout ?") - les anciens Orbes Foudroyants n'étaient que les Orbitaux, plus
// une chaîne de foudre au contact : aucune mécanique nouvelle. Devient la CONSTELLATION ORAGEUSE (classe et id
// "ThunderingOrbs" conservés : référencés par l'asset de fusion et les sauvegardes) :
//
//  1. ORBES D'ORAGE - les orbes hérités des Orbitaux tournent au sol autour du joueur (A/E règle le rayon, comme les
//     Orbitaux). Chaque passage blesse, repousse légèrement et IONISE l'ennemi : une charge électrique (jusqu'à 3),
//     visible sous lui sous forme d'éclats de cristal qui tournent (un éclat par charge).
//  2. FOUDRE CONDUCTRICE - la frappe automatique de la Foudre est conservée (cible la plus proche, depuis l'orbe le plus
//     proche d'elle) ; ses rebonds PRÉFÈRENT les ennemis ionisés, sautent deux fois plus loin vers eux et ne perdent pas
//     de puissance en les traversant. Chaque ennemi frappé gagne lui aussi une charge.
//  3. SURCHARGE - à intervalle régulier, les orbes se gonflent et crépitent (le joueur voit venir le coup), puis TOUS
//     les ennemis ionisés de l'écran sont foudroyés en même temps, plus fort selon leurs charges (x1 / x1,5 / x2). La
//     décharge part des orbes et se propage d'ennemi en ennemi en traçant une constellation d'éclairs (arbre couvrant le
//     plus court), puis les charges sont consommées.
//
// Changement de jeu : entre deux Surcharges, le joueur cherche à faire passer ses orbes dans le plus d'ennemis possible
// (déplacement + A/E pour balayer un autre cercle de la horde), et à repasser sur un même groupe pour monter ses charges.
// Différent de la Couronne Prismatique (rempart géométrique continu autour du joueur) : ici, un réseau d'éclairs
// irrégulier qui s'étend dans toute la horde, en rafales.
//
// Aucun objet poolé "OrbitalProjectile" : contacts calculés géométriquement (segment parcouru par chaque orbe depuis le
// dernier scan), visuels dans StormConstellationVisual (Music & VFX/StormConstellationVFX.cs), posés au SOL en hauteur
// absolue et jamais enfants du joueur (même centre exact que l'Aura et la Boue).
public class WeaponFusionThunderingOrbs : MonoBehaviour
{
    [Header("Orbes d'orage (hérités des Orbitaux, A/E)")]
    [SerializeField] private float _orbitRadius = 4.5f;
    [SerializeField] private float _minOrbitRadius = 1.5f;
    [SerializeField] private float _maxOrbitRadius = 13f;   // MODIFIÉ (2026-10-29) - suit les Orbitaux (8 -> 12), était 9
    [SerializeField] private float _rangeChangeSpeed = 2.5f;
    [SerializeField] private float _orbitSpeed = 190f;    // degrés par seconde
    [SerializeField] private int _orbCount = 5;
    [SerializeField] private int _maxOrbCount = 8;
    [SerializeField] private float _contactRadius = 1f;
    [SerializeField] private float _contactCooldown = 0.45f;
    [SerializeField] private float _contactKnockback = 3f;

    [Header("Ionisation")]
    [SerializeField] private int _maxIonStacks = 3;
    [SerializeField] private float _ionDuration = 6f;

    [Header("Foudre conductrice (héritée de la Foudre)")]
    [SerializeField] private float _strikeInterval = 0.65f;
    [SerializeField] private float _strikeDetectionRange = 15f;
    [SerializeField] private int _chainCount = 3;
    [SerializeField] private float _chainRange = 5f;
    [SerializeField] private float _conductorRangeMultiplier = 2f;
    private const float ChainFalloff = 0.7f;
    private const float ChainStepDelay = 0.05f;

    [Header("Surcharge")]
    [SerializeField] private float _surgeInterval = 5f;
    [SerializeField] private float _surgeWindup = 0.7f;
    [SerializeField] private float _surgeStepDelay = 0.04f;   // propagation d'un maillon de la constellation au suivant
    [SerializeField] private float _surgeMaxDelay = 0.6f;
    [SerializeField] private float _surgeStackBonus = 0.5f;   // +50 % par charge au-delà de la première
    [Tooltip("Au-delà, le maillon n'est pas relié par un éclair horizontal (trait qui traverserait l'écran) : la foudre tombe du ciel sur lui.")]
    [SerializeField] private float _maxLinkLength = 6.5f;
    private const float SkyBoltHeight = 9f;
    private const float BoltBodyHeight = 0.65f;   // point d'impact des éclairs, en fraction de la hauteur du corps

    [SerializeField] private float _scanInterval = 0.05f;

    private float _contactDamage;
    private float _strikeDamage;
    private float _surgeDamage;

    private float _angle;
    private float _scanTimer;
    private float _strikeTimer;
    private float _surgeTimer;
    private float _arcTimer;
    private bool _initialized;

    private StormConstellationVisual _visual;

    private sealed class Ion
    {
        public EnemyBase enemy;
        public BossBase boss;
        public int stacks;
        public float expiry;
        public float height;   // haut du corps au-dessus de son pivot : les éclats tournent au-dessus de la tête
        public Vector3 Position => enemy != null ? enemy.transform.position : boss.transform.position;
        public bool Alive => enemy != null
            ? enemy.gameObject.activeInHierarchy && enemy.CurrentHealth > 0f
            : boss != null && boss.gameObject.activeInHierarchy && boss.CurrentHealth > 0f;
    }

    // Coup différé (maillons de chaîne et de Surcharge) : appliqué dans Update quand son heure arrive.
    private struct PendingHit
    {
        public EnemyBase enemy;
        public BossBase boss;
        public Collider col;
        public float time;
        public float damage;
        public Vector3 from;
        public float height;   // haut du corps (0 = inconnu) : éclairs et étoiles visés au-dessus de la tête, pas aux pieds
        public bool surge;
        public bool sky;
    }

    private readonly Dictionary<int, Ion> _ions = new Dictionary<int, Ion>();
    private readonly List<PendingHit> _pending = new List<PendingHit>();
    private readonly Dictionary<int, float> _nextContact = new Dictionary<int, float>();
    private Vector3[] _orbPos, _orbPrev;

    private static readonly Collider[] _overlapBuffer = new Collider[256];
    private readonly HashSet<int> _seenThisScan = new HashSet<int>();
    private readonly List<int> _removeBuffer = new List<int>();
    private readonly HashSet<int> _chainHit = new HashSet<int>();

    private Vector3 Center => new Vector3(transform.position.x, 0f, transform.position.z);

    // Appelé une fois par UpgradeData.ApplyFusionResult avec ce que les 2 armes d'origine avaient accumulé (Réputation et
    // "Dégâts+" déjà inclus : pas de second bonus de Réputation ici).
    public void Init(float inheritedOrbitalDamage, float inheritedLightningDamage, int inheritedOrbitalCount, int inheritedChainCount)
    {
        // MESURÉ (2026-10-27) en partie réelle, Apocalyptique 3:20 -> 4:20, joueur invincible qui tourne lentement en rond
        // (sans A/E), tir de base et Nova coupés : Orbitaux + Foudre maxés = 1 045 PV retirés/s. Premier jet (x1,4 / x1,3 /
        // x1 pour contact / frappe / Surcharge) = 1 677 PV/s, soit x1,6 : trop faible. Retenu : x1,8 / x1,6 / x2,4, avec
        // frappe toutes les 0,65 s et ionisation de 6 s = 2 554 et 2 670 PV/s (x2,4 à x2,6, dans la fourchette des autres
        // fusions, x2,2 à x2,9). Un joueur qui balaie la horde avec A/E ionise plus d'ennemis que ce test.
        _contactDamage = inheritedOrbitalDamage * 1.8f;
        _strikeDamage = inheritedLightningDamage * 1.6f;
        _surgeDamage = inheritedLightningDamage * 2.4f;
        _orbCount = Mathf.Clamp(Mathf.Max(_orbCount, inheritedOrbitalCount), 2, _maxOrbCount);
        _chainCount = Mathf.Max(_chainCount, inheritedChainCount);
        _orbPos = new Vector3[_maxOrbCount];
        _orbPrev = new Vector3[_maxOrbCount];
        UpdateOrbPositions();
        System.Array.Copy(_orbPos, _orbPrev, _maxOrbCount);
        _initialized = true;

        _visual = new GameObject("StormConstellation").AddComponent<StormConstellationVisual>();
        _visual.Build(_maxOrbCount);
        RenderVisual(0f);
    }

    public void AddDamage(float value)
    {
        _contactDamage += _contactDamage * value;
        _strikeDamage += _strikeDamage * value;
        _surgeDamage += _surgeDamage * value;
    }

    // Lus par l'info-bulle / les tests.
    public int OrbCount => _orbCount;
    public int IonizedCount => _ions.Count;

    private void Update()
    {
        if (!_initialized) return;
        if (GameManager.Instance != null && (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused)) return;

        float dt = Time.deltaTime;

        if (GameInput.Held(GameAction.OrbitShrink))
            _orbitRadius = Mathf.Max(_minOrbitRadius, _orbitRadius - _rangeChangeSpeed * dt);
        if (GameInput.Held(GameAction.OrbitGrow))
            _orbitRadius = Mathf.Min(_maxOrbitRadius, _orbitRadius + _rangeChangeSpeed * dt);

        // Cycle de Surcharge : [repos ... charge (windup)] puis décharge, et on recommence.
        _surgeTimer += dt;
        float windup01 = 0f;
        if (_surgeTimer >= _surgeInterval)
        {
            _surgeTimer -= _surgeInterval;
            TriggerSurge();
        }
        else if (_surgeTimer >= _surgeInterval - _surgeWindup)
        {
            windup01 = (_surgeTimer - (_surgeInterval - _surgeWindup)) / _surgeWindup;
        }

        _angle = Mathf.Repeat(_angle + _orbitSpeed * (1f + 0.6f * windup01) * dt, 360f);
        UpdateOrbPositions();

        _scanTimer += dt;
        if (_scanTimer >= _scanInterval)
        {
            _scanTimer = 0f;
            ScanContacts();
            System.Array.Copy(_orbPos, _orbPrev, _maxOrbCount);
        }

        _strikeTimer += dt;
        if (_strikeTimer >= _strikeInterval)
        {
            if (ActiveStrike()) _strikeTimer = 0f;
        }

        ProcessPending();
        SpawnAmbientArcs(windup01, dt);
        RenderVisual(windup01);
    }

    private void UpdateOrbPositions()
    {
        Vector3 c = Center;
        float step = 360f / _orbCount;
        for (int i = 0; i < _orbCount; i++)
        {
            float a = (_angle + step * i) * Mathf.Deg2Rad;
            _orbPos[i] = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * _orbitRadius;
        }
    }

    private readonly List<StormConstellationVisual.Mark> _marks = new List<StormConstellationVisual.Mark>();

    private void RenderVisual(float windup01)
    {
        if (_visual == null) return;
        PruneIons();
        _marks.Clear();
        float now = Time.time;
        foreach (var kvp in _ions)
        {
            Ion ion = kvp.Value;
            _marks.Add(new StormConstellationVisual.Mark
            {
                position = ion.Position,
                height = ion.height,
                stacks = ion.stacks,
                fade = (ion.expiry - now) / 0.4f,   // s'éteint dans les 0,4 dernières secondes
                seed = kvp.Key
            });
        }
        _visual.Render(Center, _orbPos, _orbCount, windup01, _marks);
    }

    // ------------------------------------------------------------------------------------------------------------
    // Orbes : contact + ionisation
    // ------------------------------------------------------------------------------------------------------------

    private void ScanContacts()
    {
        Vector3 c = Center;
        float scanRadius = _orbitRadius + _contactRadius + 1.5f;
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, scanRadius, _overlapBuffer, EnemyLayers.Mask);

        float now = Time.time;
        float dmg = _contactDamage * PlayerBuffs.OutgoingDamageMultiplier;
        _seenThisScan.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _overlapBuffer[i];
            if (col == null || !col.CompareTag("Enemy")) continue;

            col.TryGetComponent(out EnemyBase enemy);
            BossBase boss = enemy == null ? col.GetComponentInParent<BossBase>() : null;
            if (enemy == null && boss == null) continue;

            int id = enemy != null ? enemy.GetInstanceID() : boss.GetInstanceID();
            if (!_seenThisScan.Add(id)) continue;   // un boss a plusieurs colliders
            if (_nextContact.TryGetValue(id, out float next) && now < next) continue;

            Vector3 p = enemy != null ? enemy.transform.position : boss.transform.position;
            Vector3 flatP = new Vector3(p.x, 0f, p.z);

            // Segment parcouru par chaque orbe depuis le dernier scan : un orbe rapide ne saute jamais par-dessus un ennemi.
            int touchedBy = -1;
            Vector3 contact = flatP;
            for (int o = 0; o < _orbCount && touchedBy < 0; o++)
            {
                Vector3 a = _orbPrev[o], b = _orbPos[o];
                Vector3 ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(flatP - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
                Vector3 q = a + ab * t;
                bool touches = enemy != null ? (flatP - q).sqrMagnitude <= _contactRadius * _contactRadius
                                             : boss.IsOnBody(q, _contactRadius * 0.5f);
                if (touches) { touchedBy = o; contact = q; }
            }
            if (touchedBy < 0) continue;

            _nextContact[id] = now + _contactCooldown;
            if (enemy != null)
            {
                enemy.TakeDamage(dmg, DamageNumberSpawner.ColorOrbital);
                Vector3 away = flatP - c;
                if (away.sqrMagnitude > 0.0001f) enemy.ApplyKnockback(away.normalized * _contactKnockback);
            }
            else boss.TakeDamage(dmg, DamageNumberSpawner.ColorOrbital);

            Ionize(id, enemy, boss, col, now);
            contact.y = 0.4f;
            ImpactSparkVFX.Spawn(contact, StormConstellationVisual.Spark, 0.8f);
        }

        if (_nextContact.Count > 512) PruneCooldowns(_nextContact, now);
    }

    private void Ionize(int id, EnemyBase enemy, BossBase boss, Collider col, float now)
    {
        if (!_ions.TryGetValue(id, out Ion ion))
        {
            Transform root = enemy != null ? enemy.transform : boss.transform;
            float top = col != null ? col.bounds.max.y - root.position.y : 1.5f;
            ion = new Ion { enemy = enemy, boss = boss, height = Mathf.Clamp(top, 0.8f, 4f) };
            _ions[id] = ion;
        }
        ion.stacks = Mathf.Min(_maxIonStacks, ion.stacks + 1);
        ion.expiry = now + _ionDuration;
    }

    private void PruneIons()
    {
        float now = Time.time;
        _removeBuffer.Clear();
        foreach (var kvp in _ions)
            if (kvp.Value.expiry <= now || !kvp.Value.Alive) _removeBuffer.Add(kvp.Key);
        foreach (int id in _removeBuffer) _ions.Remove(id);
    }

    // ------------------------------------------------------------------------------------------------------------
    // Foudre conductrice
    // ------------------------------------------------------------------------------------------------------------

    private bool ActiveStrike()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, _strikeDetectionRange, _overlapBuffer, EnemyLayers.Mask);
        Collider nearest = null;
        float best = _strikeDetectionRange * _strikeDetectionRange;
        for (int i = 0; i < count; i++)
        {
            Collider col = _overlapBuffer[i];
            if (col == null || !col.CompareTag("Enemy")) continue;
            float d = (col.transform.position - transform.position).sqrMagnitude;
            if (d < best) { best = d; nearest = col; }
        }
        if (nearest == null) return false;

        // L'éclair part de l'orbe le plus proche de sa cible : la foudre "sort" visiblement des orbes.
        Vector3 target = nearest.transform.position;
        Vector3 origin = _orbPos[0];
        float bestOrb = float.MaxValue;
        for (int o = 0; o < _orbCount; o++)
        {
            float d = (_orbPos[o] - target).sqrMagnitude;
            if (d < bestOrb) { bestOrb = d; origin = _orbPos[o]; }
        }
        origin.y = StormConstellationVisual.OrbY;

        _chainHit.Clear();
        float damage = _strikeDamage * PlayerBuffs.OutgoingDamageMultiplier;
        float time = Time.time;
        Vector3 from = origin;
        Collider current = nearest;
        for (int i = 0; i <= _chainCount && current != null; i++)
        {
            current.TryGetComponent(out EnemyBase enemy);
            BossBase boss = enemy == null ? current.GetComponentInParent<BossBase>() : null;
            if (enemy == null && boss == null) break;
            int id = enemy != null ? enemy.GetInstanceID() : boss.GetInstanceID();
            _chainHit.Add(id);

            Vector3 at = current.transform.position;
            float height = Mathf.Clamp(current.bounds.max.y - at.y, 0.8f, 4f);
            _pending.Add(new PendingHit { enemy = enemy, boss = boss, col = current, time = time, damage = damage, from = from, height = height, surge = false });
            Vector3 feet = at;
            at.y += height * BoltBodyHeight;

            // Rebond : un ennemi ionisé conduit la foudre sans l'affaiblir ; sinon elle perd 30 % comme la Foudre seule.
            current = FindNextChainTarget(feet, out bool conductor);
            if (current != null && !conductor) damage *= ChainFalloff;
            from = at;
            time += ChainStepDelay;
        }
        return true;
    }

    private Collider FindNextChainTarget(Vector3 from, out bool conductor)
    {
        conductor = false;
        float conductorRange = _chainRange * _conductorRangeMultiplier;
        int count = Physics.OverlapSphereNonAlloc(from, conductorRange, _overlapBuffer, EnemyLayers.Mask);
        Collider bestIon = null, bestPlain = null;
        float bestIonD = conductorRange * conductorRange, bestPlainD = _chainRange * _chainRange;
        for (int i = 0; i < count; i++)
        {
            Collider col = _overlapBuffer[i];
            if (col == null || !col.CompareTag("Enemy")) continue;
            col.TryGetComponent(out EnemyBase enemy);
            BossBase boss = enemy == null ? col.GetComponentInParent<BossBase>() : null;
            if (enemy == null && boss == null) continue;
            int id = enemy != null ? enemy.GetInstanceID() : boss.GetInstanceID();
            if (_chainHit.Contains(id)) continue;
            float d = (col.transform.position - from).sqrMagnitude;
            if (_ions.ContainsKey(id)) { if (d < bestIonD) { bestIonD = d; bestIon = col; } }
            else if (d < bestPlainD) { bestPlainD = d; bestPlain = col; }
        }
        if (bestIon != null) { conductor = true; return bestIon; }
        return bestPlain;
    }

    // ------------------------------------------------------------------------------------------------------------
    // Surcharge : constellation (arbre couvrant le plus court, enraciné sur les orbes)
    // ------------------------------------------------------------------------------------------------------------

    private readonly List<Ion> _nodes = new List<Ion>();
    private Vector3[] _nodePos = new Vector3[64];
    private float[] _bestDist = new float[64];
    private int[] _parent = new int[64];      // < 0 : relié à l'orbe (-1 - index)
    private int[] _depth = new int[64];
    private bool[] _inTree = new bool[64];

    private void TriggerSurge()
    {
        PruneIons();
        CameraShakeFX.Shake(_ions.Count > 0 ? 0.14f : 0.05f);
        if (_visual != null) _visual.FlashSurge();
        if (_ions.Count == 0) return;

        _nodes.Clear();
        foreach (var ion in _ions.Values) _nodes.Add(ion);
        int n = _nodes.Count;
        if (_nodePos.Length < n)
        {
            int size = Mathf.NextPowerOfTwo(n);
            _nodePos = new Vector3[size]; _bestDist = new float[size]; _parent = new int[size];
            _depth = new int[size]; _inTree = new bool[size];
        }

        // Prim multi-source : chaque ennemi commence relié à l'orbe le plus proche, puis on rattache à chaque étape le
        // plus proche de l'arbre (orbe ou ennemi déjà relié). O(n²) une fois toutes les ~5 s : quelques millisecondes au
        // pire avec plusieurs centaines d'ennemis ionisés.
        for (int i = 0; i < n; i++)
        {
            Vector3 p = _nodes[i].Position;
            _nodePos[i] = new Vector3(p.x, 0f, p.z);
            _inTree[i] = false;
            _bestDist[i] = float.MaxValue;
            for (int o = 0; o < _orbCount; o++)
            {
                float d = (_nodePos[i] - _orbPos[o]).sqrMagnitude;
                if (d < _bestDist[i]) { _bestDist[i] = d; _parent[i] = -1 - o; }
            }
        }

        float now = Time.time;
        float mult = PlayerBuffs.OutgoingDamageMultiplier;
        for (int step = 0; step < n; step++)
        {
            int pick = -1;
            float pickD = float.MaxValue;
            for (int i = 0; i < n; i++)
                if (!_inTree[i] && _bestDist[i] < pickD) { pickD = _bestDist[i]; pick = i; }
            if (pick < 0) break;
            _inTree[pick] = true;

            int parent = _parent[pick];
            // Maillon trop loin de l'arbre (groupe isolé) : frappé du ciel, il devient la racine de son propre groupe.
            bool sky = pickD > _maxLinkLength * _maxLinkLength;
            _depth[pick] = parent < 0 || sky ? 1 : _depth[parent] + 1;
            Vector3 from = sky ? _nodePos[pick] + new Vector3(0.7f, SkyBoltHeight, -0.5f)
                         : parent < 0 ? _orbPos[-1 - parent] : _nodePos[parent];
            if (!sky) from.y = parent < 0 ? StormConstellationVisual.OrbY : _nodes[parent].height * BoltBodyHeight;

            Ion ion = _nodes[pick];
            float damage = _surgeDamage * mult * (1f + _surgeStackBonus * (ion.stacks - 1));
            float delay = Mathf.Min(_surgeMaxDelay, (_depth[pick] - 1) * _surgeStepDelay);
            _pending.Add(new PendingHit { enemy = ion.enemy, boss = ion.boss, time = now + delay, damage = damage, from = from, height = ion.height, surge = true, sky = sky });

            for (int i = 0; i < n; i++)
            {
                if (_inTree[i]) continue;
                float d = (_nodePos[i] - _nodePos[pick]).sqrMagnitude;
                if (d < _bestDist[i]) { _bestDist[i] = d; _parent[i] = pick; }
            }
        }

        // Charges consommées : il faut ré-ioniser la horde avant la prochaine Surcharge.
        _ions.Clear();
    }

    private void ProcessPending()
    {
        float now = Time.time;
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            PendingHit h = _pending[i];
            if (h.time > now) continue;
            _pending.RemoveAt(i);

            bool alive = h.enemy != null ? h.enemy.gameObject.activeInHierarchy && h.enemy.CurrentHealth > 0f
                                         : h.boss != null && h.boss.gameObject.activeInHierarchy && h.boss.CurrentHealth > 0f;
            if (!alive) continue;

            Vector3 p = h.enemy != null ? h.enemy.transform.position : h.boss.transform.position;
            if (h.enemy != null) h.enemy.TakeDamage(h.damage, DamageNumberSpawner.ColorLightning);
            else h.boss.TakeDamage(h.damage, DamageNumberSpawner.ColorLightning);

            // Les éclairs visent le haut du corps : au niveau des pieds, les grands ennemis les cachaient.
            float body = h.height > 0f ? h.height : 1.2f;
            Vector3 to = new Vector3(p.x, p.y + body * BoltBodyHeight, p.z);
            if (h.surge)
            {
                // Deux éclairs superposés (zigzags différents) : un large doré et un fin blanc, plus dense qu'un trait seul.
                LightningBoltVFX.SpawnBolt(h.from, to, StormConstellationVisual.Bolt, 0.34f, h.sky ? 0.4f : 0.32f);
                LightningBoltVFX.SpawnBolt(h.from, to, StormConstellationVisual.Spark, 0.24f, 0.12f);
                if (_visual != null) _visual.SpawnStar(new Vector3(p.x, p.y + body + 0.35f, p.z), h.sky);
            }
            else
            {
                LightningBoltVFX.SpawnBolt(h.from, to, StormConstellationVisual.Bolt);
                LightningBoltVFX.SpawnStrikeFlash(to, StormConstellationVisual.Bolt);
                int id = h.enemy != null ? h.enemy.GetInstanceID() : h.boss.GetInstanceID();
                Ionize(id, h.enemy, h.boss, h.col, now);
            }
        }
    }

    // Petits arcs entre les orbes et le sol (et, pendant la charge de Surcharge, vers les ennemis ionisés proches) :
    // les orbes crépitent en permanence, et de plus en plus fort avant la décharge.
    private void SpawnAmbientArcs(float windup01, float dt)
    {
        _arcTimer -= dt;
        if (_arcTimer > 0f) return;
        _arcTimer = Mathf.Lerp(0.16f, 0.035f, windup01);

        int o = Random.Range(0, _orbCount);
        Vector3 orb = _orbPos[o];
        orb.y = StormConstellationVisual.OrbY;

        if (windup01 > 0f && _ions.Count > 0)
        {
            Ion closest = null;
            float best = 16f;
            foreach (var ion in _ions.Values)
            {
                Vector3 p = ion.Position;
                float d = (new Vector3(p.x, 0f, p.z) - new Vector3(orb.x, 0f, orb.z)).sqrMagnitude;
                if (d < best) { best = d; closest = ion; }
            }
            if (closest != null)
            {
                Vector3 p = closest.Position;
                LightningBoltVFX.SpawnBolt(orb, new Vector3(p.x, 0.3f, p.z), StormConstellationVisual.Bolt, 0.1f, 0.05f);
                return;
            }
        }

        Vector2 r = Random.insideUnitCircle.normalized * Random.Range(0.5f, 1.1f);
        LightningBoltVFX.SpawnBolt(orb, new Vector3(orb.x + r.x, 0.05f, orb.z + r.y), StormConstellationVisual.Bolt, 0.09f, 0.04f);
    }

    private static readonly List<int> _pruneBuffer = new List<int>();
    private static void PruneCooldowns(Dictionary<int, float> map, float now)
    {
        _pruneBuffer.Clear();
        foreach (var kvp in map) if (kvp.Value <= now) _pruneBuffer.Add(kvp.Key);
        foreach (int id in _pruneBuffer) map.Remove(id);
    }

    private void OnDestroy()
    {
        if (_visual != null) Destroy(_visual.gameObject);
    }
}
