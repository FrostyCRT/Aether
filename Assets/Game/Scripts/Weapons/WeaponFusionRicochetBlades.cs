using UnityEngine;
using System.Collections.Generic;

// FUSION (2026-09-27) : Couteaux + Orbe rebondissant, toutes les deux au palier max → cette arme les remplace [Lyra].
//
// REFONTE COMPLÈTE (2026-10-27, campagne de refonte des fusions : "il faut que ça crée du changement au gameplay, et ça
// doit être plus puissant que les 2 sous-fusions améliorées au max") - l'ancienne Lames Ricochet n'était que la salve des
// Couteaux avec un rebond d'ennemi en ennemi : l'Orbe rebondissant disparaissait. Devient la VALSE DES LAMES (classe et
// id "RicochetBlades" conservés : référencés par l'asset de fusion et les sauvegardes) :
//
//  1. SALVE - l'éventail des Couteaux (une lame de plus par couteau et par orbe hérités) part vers l'ennemi le plus
//     proche. Les lames de cristal tournent sur elles-mêmes et tranchent tout ce qu'elles traversent (chaque ennemi une
//     fois par trajet).
//  2. REBONDS - comme l'Orbe, elles rebondissent sur les bords de l'écran (et les murs de la carte). Chaque rebond
//     les fait grandir et les aiguise : trajet suivant plus large et plus fort.
//  3. RETOUR - après leur dernier rebond, elles reviennent vers le joueur en accélérant, plus fortes encore : le joueur
//     ramène ses lames à travers la horde en se déplaçant (choix de placement que ni les Couteaux ni l'Orbe n'offraient).
//     Chaque lame rattrapée charge le KALÉIDOSCOPE (éclats de cristal qui s'allument autour du joueur) : une fois plein,
//     la salve suivante part dans toutes les directions.
//
// Aucun objet poolé "ProjectileShuriken" : les contacts sont calculés géométriquement (segment parcouru chaque image),
// les visuels vivent dans BladeWaltzVisual (Music & VFX/BladeWaltzVFX.cs), jamais enfant du joueur.
public class WeaponFusionRicochetBlades : MonoBehaviour
{
    [Header("Salve (héritée des Couteaux)")]
    [SerializeField] private float _fireRate = 0.5f;
    [SerializeField] private float _detectionRange = 15f;
    [SerializeField] private int _bladeCount = 5;
    [SerializeField] private int _maxBladeCount = 8;
    [SerializeField] private float _fanAngleSpread = 14f;

    [Header("Vol et rebonds (hérités de l'Orbe rebondissant)")]
    [SerializeField] private float _outSpeed = 17f;
    [SerializeField] private int _wallBounces = 2;
    [SerializeField] private float _bounceDamageBonus = 0.35f;   // par rebond
    [SerializeField] private float _bounceGrowth = 0.22f;        // par rebond
    [SerializeField] private float _hitRadius = 0.5f;

    [Header("Retour")]
    [SerializeField] private float _returnSpeed = 14f;
    [SerializeField] private float _returnMaxSpeed = 28f;
    [SerializeField] private float _returnAcceleration = 22f;
    [SerializeField] private float _returnDamageMultiplier = 2f;
    [SerializeField] private float _catchRadius = 0.9f;
    [SerializeField] private float _maxFlightTime = 6f;          // filet de sécurité : rappelée de force au-delà

    [Header("Kaléidoscope")]
    [SerializeField] private int _kaleidoscopeCharge = 30;       // lames rattrapées
    [SerializeField] private int _kaleidoscopeBlades = 12;

    public const int MaxBlades = 96;
    public const int ChargeShards = 5;
    private const float BodyHitMargin = 0.3f;
    private const float HardRecallTime = 10f;   // une lame qui n'arrive pas à rejoindre le joueur est rattrapée d'office

    private float _damage;
    private float _cooldownTimer;
    private int _charge;
    private bool _nextOddFlankOnRight = true;
    private bool _initialized;

    private class Blade
    {
        public bool active;
        public Vector3 pos, prev, dir;
        public float speed;
        public int bounces;
        public bool returning;
        public float age;
        public float spin;
        public readonly HashSet<int> hit = new HashSet<int>();
    }

    private readonly Blade[] _blades = new Blade[MaxBlades];
    private BladeWaltzVisual _visual;

    private static readonly Collider[] _detectionBuffer = new Collider[64];
    private static readonly Collider[] _hitBuffer = new Collider[64];
    private readonly HashSet<int> _seenThisSweep = new HashSet<int>();

    private Vector3 Center => new Vector3(transform.position.x, 0f, transform.position.z);

    // Appelé une fois par UpgradeData.ApplyFusionResult avec ce que les 2 armes d'origine avaient accumulé (Réputation et
    // "Dégâts+" déjà inclus : pas de second bonus de Réputation ici).
    public void Init(float inheritedKnivesDamage, float inheritedOrbDamage, int inheritedKnifeCount, int inheritedOrbCount)
    {
        // MESURÉ (2026-10-28) en partie réelle, Apocalyptique 3:20 -> 4:20, joueur invincible qui tourne lentement en rond,
        // tir de base et Nova coupés, dégâts des deux côtés divisés par 3 (à pleine puissance, Couteaux + Orbe maxés tuent
        // déjà la horde aussi vite qu'elle apparaît : la mesure plafonnait). Couteaux + Orbe maxés = 684 et 535 PV/s.
        // Premier jet (x0,5, salve toutes les 1,25 s, Kaléidoscope de 16 lames toutes les 15 lames rattrapées) = x9 :
        // beaucoup trop fort. Retenu : x0,15, salve toutes les 2 s, Kaléidoscope de 12 lames toutes les 30 lames
        // rattrapées (~toutes les 12 s) = 1 497 PV/s (entre x2,2 et x2,8 selon la référence, dans la fourchette des
        // autres fusions, x2,2 à x2,9). Un joueur qui ramène ses lames à travers la horde touche plus que ce test.
        _damage = (inheritedKnivesDamage + inheritedOrbDamage) * 0.15f;
        _bladeCount = Mathf.Clamp(Mathf.Max(_bladeCount, inheritedKnifeCount + inheritedOrbCount), 1, _maxBladeCount);
        for (int i = 0; i < MaxBlades; i++) _blades[i] = new Blade();

        _visual = new GameObject("BladeWaltz").AddComponent<BladeWaltzVisual>();
        _visual.Build(MaxBlades);
        _initialized = true;
    }

    public void AddDamage(float value) => _damage += _damage * value;
    public void AddFireRate(float value) => _fireRate += _fireRate * value;

    // Lus par l'info-bulle / les tests.
    public int BladeCount => _bladeCount;
    public int ActiveBlades { get { int n = 0; for (int i = 0; i < MaxBlades; i++) if (_blades[i] != null && _blades[i].active) n++; return n; } }
    public int Charge => _charge;

    private void Update()
    {
        if (!_initialized) return;
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        if (GameManager.Instance.IsPaused) return;

        float dt = Time.deltaTime;

        _cooldownTimer += dt;
        float cooldownDuration = _fireRate > 0f ? (1f / _fireRate) : 9999f;
        if (_cooldownTimer >= cooldownDuration)
        {
            Transform target = FindNearestEnemy();
            if (target != null)
            {
                Vector3 direction = target.position - transform.position;
                direction.y = 0f;
                // Ennemi collé au joueur : direction quasi nulle, on vise n'importe où plutôt que de ne pas tirer.
                if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
                direction.Normalize();

                if (_charge >= _kaleidoscopeCharge)
                {
                    _charge = 0;
                    FireKaleidoscope(direction);
                }
                else FireSalvo(direction);
                _cooldownTimer = 0f;
            }
        }

        GetBounds(out float minX, out float maxX, out float minZ, out float maxZ);
        Vector3 c = Center;
        float buff = PlayerBuffs.OutgoingDamageMultiplier;
        for (int i = 0; i < MaxBlades; i++)
        {
            Blade b = _blades[i];
            if (!b.active) continue;
            StepBlade(b, i, dt, c, minX, maxX, minZ, maxZ, buff);
        }

        RenderVisual();
    }

    // ------------------------------------------------------------------------------------------------------------
    // Tir
    // ------------------------------------------------------------------------------------------------------------

    private void FireSalvo(Vector3 centerDirection)
    {
        Launch(centerDirection);

        int extra = _bladeCount - 1;
        int pairCount = extra / 2;
        for (int p = 1; p <= pairCount; p++)
        {
            float angle = _fanAngleSpread * p;
            Launch(Quaternion.AngleAxis(angle, Vector3.up) * centerDirection);
            Launch(Quaternion.AngleAxis(-angle, Vector3.up) * centerDirection);
        }
        if (extra % 2 == 1)
        {
            float angle = _fanAngleSpread * (pairCount + 1) * (_nextOddFlankOnRight ? 1f : -1f);
            Launch(Quaternion.AngleAxis(angle, Vector3.up) * centerDirection);
            _nextOddFlankOnRight = !_nextOddFlankOnRight;
        }
    }

    private void FireKaleidoscope(Vector3 centerDirection)
    {
        float step = 360f / _kaleidoscopeBlades;
        for (int i = 0; i < _kaleidoscopeBlades; i++)
            Launch(Quaternion.AngleAxis(step * i, Vector3.up) * centerDirection);
        CameraShakeFX.Shake(0.12f);
        if (_visual != null) _visual.Burst(Center);
    }

    private void Launch(Vector3 dir)
    {
        for (int i = 0; i < MaxBlades; i++)
        {
            Blade b = _blades[i];
            if (b.active) continue;
            b.active = true;
            b.pos = b.prev = Center;
            b.dir = dir;
            b.speed = _outSpeed;
            b.bounces = 0;
            b.returning = false;
            b.age = 0f;
            b.spin = Random.Range(0f, 360f);
            b.hit.Clear();
            if (_visual != null) _visual.Activate(i, b.pos);
            return;
        }
    }

    // ------------------------------------------------------------------------------------------------------------
    // Vol : rebonds, retour, contacts
    // ------------------------------------------------------------------------------------------------------------

    private void StepBlade(Blade b, int slot, float dt, Vector3 player, float minX, float maxX, float minZ, float maxZ, float buff)
    {
        b.age += dt;
        b.prev = b.pos;
        float size = 1f + _bounceGrowth * b.bounces;

        if (!b.returning && b.age >= _maxFlightTime) StartReturn(b);

        if (b.returning)
        {
            Vector3 toPlayer = player - b.pos;
            float dist = toPlayer.magnitude;
            b.speed = Mathf.Min(_returnMaxSpeed, b.speed + _returnAcceleration * dt);
            float step = b.speed * dt;
            if (dist <= _catchRadius || step >= dist || b.age >= HardRecallTime)
            {
                Sweep(b, b.prev, player, size, buff);
                Catch(b, slot);
                return;
            }
            b.dir = toPlayer / dist;
            b.pos += b.dir * step;
        }
        else
        {
            Vector3 next = b.pos + b.dir * (b.speed * dt);
            bool wall = false;
            if ((next.x <= minX && b.dir.x < 0f) || (next.x >= maxX && b.dir.x > 0f))
            {
                b.dir.x = -b.dir.x;
                next.x = Mathf.Clamp(next.x, minX, maxX);
                wall = true;
            }
            if ((next.z <= minZ && b.dir.z < 0f) || (next.z >= maxZ && b.dir.z > 0f))
            {
                b.dir.z = -b.dir.z;
                next.z = Mathf.Clamp(next.z, minZ, maxZ);
                wall = true;
            }
            b.pos = next;

            if (wall)
            {
                // Le segment de ce pas appartient encore au trajet qui s'achève.
                Sweep(b, b.prev, b.pos, size, buff);
                b.prev = b.pos;
                ImpactSparkVFX.Spawn(b.pos + Vector3.up * BladeWaltzVisual.BladeY, BladeWaltzVisual.Spark, 1.2f + 0.3f * b.bounces);
                if (b.bounces < _wallBounces)
                {
                    b.bounces++;
                    b.hit.Clear();
                }
                else StartReturn(b);
                return;
            }
        }

        Sweep(b, b.prev, b.pos, size, buff);
    }

    private void StartReturn(Blade b)
    {
        b.returning = true;
        b.speed = _returnSpeed;
        b.hit.Clear();
    }

    private void Catch(Blade b, int slot)
    {
        b.active = false;
        if (_charge < _kaleidoscopeCharge) _charge++;
        if (_visual != null) _visual.Deactivate(slot);
    }

    // Tous les ennemis que le segment [from, to] traverse (corps compris), chacun une seule fois par trajet de la lame.
    private void Sweep(Blade b, Vector3 from, Vector3 to, float size, float buff)
    {
        float radius = _hitRadius * size;
        Vector3 a = from + Vector3.up * BladeWaltzVisual.BladeY;
        Vector3 z = to + Vector3.up * BladeWaltzVisual.BladeY;
        int count = Physics.OverlapCapsuleNonAlloc(a, z, radius, _hitBuffer, EnemyLayers.Mask);
        if (count == 0) return;

        float mult = b.returning ? (1f + _bounceDamageBonus * b.bounces) * _returnDamageMultiplier
                                 : 1f + _bounceDamageBonus * b.bounces;
        float dmg = _damage * mult * buff;
        _seenThisSweep.Clear();

        for (int i = 0; i < count; i++)
        {
            Collider col = _hitBuffer[i];
            if (col == null || !col.CompareTag("Enemy")) continue;

            col.TryGetComponent(out EnemyBase enemy);
            BossBase boss = enemy == null ? col.GetComponentInParent<BossBase>() : null;
            if (enemy == null && boss == null) continue;

            int id = enemy != null ? enemy.GetInstanceID() : boss.GetInstanceID();
            if (!_seenThisSweep.Add(id) || b.hit.Contains(id)) continue;

            Vector3 p = enemy != null ? enemy.transform.position : boss.transform.position;
            Vector3 ab = z - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            Vector3 q = a + ab * t;
            if (boss != null && !boss.IsOnBody(q, BodyHitMargin)) continue;   // boîte englobante touchée, pas le corps

            b.hit.Add(id);
            if (enemy != null) enemy.TakeDamage(dmg, DamageNumberSpawner.ColorKnives);
            else boss.TakeDamage(dmg, DamageNumberSpawner.ColorKnives);
            ImpactSparkVFX.Spawn(q, BladeWaltzVisual.Spark, 0.7f + 0.15f * b.bounces);
        }
    }

    // Rectangle visible par la caméra, projeté au sol, croisé avec les murs de la carte (même règle que l'Orbe
    // rebondissant, voir BouncingOrbProjectile.GetEffectiveBounds) - calculé une fois par image pour toutes les lames.
    private void GetBounds(out float minX, out float maxX, out float minZ, out float maxZ)
    {
        Vector3 c = Center;
        float cMinX = c.x - 12f, cMaxX = c.x + 12f, cMinZ = c.z - 12f, cMaxZ = c.z + 12f;
        Camera cam = Camera.main;
        if (cam != null)
        {
            Plane ground = new Plane(Vector3.up, new Vector3(0f, BladeWaltzVisual.BladeY, 0f));
            Vector3 p0 = GroundPoint(cam, ground, new Vector3(0f, 0f, 0f), c);
            Vector3 p1 = GroundPoint(cam, ground, new Vector3(1f, 1f, 0f), c);
            cMinX = Mathf.Min(p0.x, p1.x); cMaxX = Mathf.Max(p0.x, p1.x);
            cMinZ = Mathf.Min(p0.z, p1.z); cMaxZ = Mathf.Max(p0.z, p1.z);
        }
        const float margin = 0.6f;   // la pointe de la lame touche le bord, pas son centre
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

    private Transform FindNearestEnemy()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, _detectionRange, _detectionBuffer, EnemyLayers.Mask);
        Transform nearest = null;
        float minDistSqr = _detectionRange * _detectionRange;
        for (int i = 0; i < count; i++)
        {
            Collider col = _detectionBuffer[i];
            if (col == null || !col.CompareTag("Enemy")) continue;
            float distSqr = (col.transform.position - transform.position).sqrMagnitude;
            if (distSqr < minDistSqr) { minDistSqr = distSqr; nearest = col.transform; }
        }
        return nearest;
    }

    // ------------------------------------------------------------------------------------------------------------
    // Rendu
    // ------------------------------------------------------------------------------------------------------------

    private void RenderVisual()
    {
        if (_visual == null) return;
        float dt = Time.deltaTime;
        for (int i = 0; i < MaxBlades; i++)
        {
            Blade b = _blades[i];
            if (!b.active) continue;
            b.spin = Mathf.Repeat(b.spin + (b.returning ? 1100f : 820f) * dt, 360f);
            _visual.SetBlade(i, b.pos, 1f + _bounceGrowth * b.bounces, b.returning ? _wallBounces + 1 : b.bounces, b.spin);
        }
        int lit = Mathf.FloorToInt(_charge * ChargeShards / (float)Mathf.Max(1, _kaleidoscopeCharge));
        _visual.Render(Center, lit, _charge >= _kaleidoscopeCharge);
    }

    private void OnDestroy()
    {
        if (_visual != null) Destroy(_visual.gameObject);
    }
}
