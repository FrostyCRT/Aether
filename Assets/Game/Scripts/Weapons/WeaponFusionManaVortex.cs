using UnityEngine;
using System.Collections.Generic;

// FUSION (2026-09-27) : Aura + Orbitaux, toutes les deux au palier max → cette arme les remplace [Kael].
//
// REFONTE COMPLÈTE (2026-10-27, retour utilisateur : "il faut faire un rework de la fusion aura + orbitaux [...] il faut
// que ça crée du changement au gameplay, et ça doit être plus puissant que les 2 sous-fusions améliorées au max") -
// l'ancien Vortex de Mana n'était qu'un disque d'Aura agrandi avec les mêmes orbes par-dessus : aucune mécanique
// nouvelle. Devient la COURONNE PRISMATIQUE (classe et id "ManaVortex" conservés : référencés par l'asset de fusion et
// les sauvegardes) :
//
//  1. COURONNE - les cristaux hérités des Orbitaux (un de plus) tournent au sol autour du joueur et sont RELIÉS par des
//     rayons de lumière : le cercle des Orbitaux devient un polygone fermé. Un ennemi qui touche un rayon est brûlé et
//     repoussé vers l'extérieur : la couronne est un REMPART que le joueur règle avec A/E (identité Orbitaux).
//  2. CHAMP - l'intérieur du polygone brûle et ralentit (identité Aura, ralentissement hérité). Sa puissance dépend de la
//     taille de la couronne : resserrée, elle concentre le champ (jusqu'à x2) ; élargie, elle le dilue (x0,8) mais ses
//     rayons tiennent la horde plus loin. Choix tactique permanent avec A/E, que ni l'Aura ni les Orbitaux n'offraient.
//  3. RÉFRACTION - à intervalle régulier, les cristaux se chargent (ils grossissent et blanchissent : le joueur voit
//     venir le coup), puis chacun projette un faisceau vers l'extérieur pendant que la couronne accélère : des
//     faisceaux en rotation balaient tout l'écran autour du joueur. Seule attaque du jeu qui balaie en tournant.
//
// Aucun objet poolé "OrbitalProjectile" : tous les contacts sont calculés géométriquement (segments, polygone, secteurs
// balayés par les faisceaux), les visuels vivent dans PrismaticCrownVisual (Music & VFX/PrismaticCrownVFX.cs), posés
// au SOL en hauteur absolue et jamais enfants du joueur (ni décalage de hauteur, ni rotation héritée : même centre
// exact que l'Aura et la Boue).
public class WeaponFusionManaVortex : MonoBehaviour
{
    [Header("Couronne (héritée des Orbitaux, A/E)")]
    [SerializeField] private float _crownRadius = 6.5f;   // = rayon de base de l'Aura et distance des flaques de Boue
    [SerializeField] private float _minCrownRadius = 3f;
    [SerializeField] private float _maxCrownRadius = 10f;
    [SerializeField] private float _rangeChangeSpeed = 2.5f;
    [SerializeField] private float _spinSpeed = 70f;      // degrés par seconde
    [SerializeField] private int _crystalCount = 6;
    [SerializeField] private int _maxCrystalCount = 8;

    [Header("Rayons du contour")]
    [Tooltip("Distance au rayon (corps de l'ennemi compris) en dessous de laquelle il est touché.")]
    [SerializeField] private float _rimHitRadius = 0.85f;
    [SerializeField] private float _rimHitCooldown = 0.3f;
    [SerializeField] private float _rimKnockback = 3f;

    [Header("Champ intérieur (hérité de l'Aura)")]
    [SerializeField] private float _fieldTickRate = 0.25f;
    [SerializeField] private float _referenceRadius = 6.5f;
    [SerializeField] private float _minDensity = 0.8f;
    [SerializeField] private float _maxDensity = 2f;

    [Header("Réfraction (faisceaux en rotation)")]
    [SerializeField] private float _refractionInterval = 5.5f;
    [SerializeField] private float _refractionWindup = 0.6f;
    [SerializeField] private float _refractionDuration = 1.3f;
    [SerializeField] private float _laserLength = 9f;
    [SerializeField] private float _laserHitRadius = 0.9f;
    [SerializeField] private float _laserHitCooldown = 0.2f;
    [SerializeField] private float _refractionSpinBoost = 2.2f;
    private const float LaserExtendTime = 0.15f;
    private const float LaserFadeTime = 0.25f;

    [SerializeField] private float _scanInterval = 0.06f;

    private float _rimDamage;
    private float _fieldDps;
    private float _laserDamage;
    private float _slowMultiplier = 0.6f;

    private float _angle;            // degrés, angle du cristal 0
    private float _refractionTimer;
    private float _scanTimer;
    private float _fieldTimer;
    private float _lastScanAngle;
    private bool _initialized;

    private PrismaticCrownVisual _visual;

    private static readonly Collider[] _overlapBuffer = new Collider[256];
    private readonly HashSet<int> _seenThisScan = new HashSet<int>();
    private readonly HashSet<int> _inFieldThisTick = new HashSet<int>();
    private readonly Dictionary<int, EnemyBase> _currentlySlowed = new Dictionary<int, EnemyBase>();
    private BossBase _currentlySlowedBoss;
    private readonly Dictionary<int, float> _nextRimHit = new Dictionary<int, float>();
    private readonly Dictionary<int, float> _nextLaserHit = new Dictionary<int, float>();

    private Vector3 Center => new Vector3(transform.position.x, 0f, transform.position.z);
    private float Density => Mathf.Clamp(_referenceRadius / _crownRadius, _minDensity, _maxDensity);

    // Appelé une fois par UpgradeData.ApplyFusionResult avec ce que les 2 armes d'origine avaient accumulé (Réputation et
    // "Dégâts+" déjà inclus : pas de second bonus de Réputation ici).
    public void Init(float inheritedFieldDps, float inheritedOrbitalDamage, int inheritedOrbitalCount, float inheritedSlow)
    {
        // MESURÉ (2026-10-27) en partie réelle, Apocalyptique 3:20 -> 4:20, joueur invincible qui se déplace lentement,
        // tir de base et Nova coupés, mêmes vagues pour les 2 essais : Aura + Orbitaux maxés = 973 PV retirés/s.
        // Premier jet (x1,4 / x3 / x1,8) = 3 580 PV/s, soit x3,7 : au-delà des autres fusions (x2,2 à x2,9).
        // x1 / x2,2 / x1,3 = 2 979 PV/s (x3,1), encore haut. Retenu : x0,85 / x1,9 / x1,1.
        _rimDamage = inheritedOrbitalDamage * 0.85f;
        _fieldDps = inheritedFieldDps * 1.9f;
        _laserDamage = inheritedOrbitalDamage * 1.1f;
        _slowMultiplier = Mathf.Clamp(inheritedSlow, 0.3f, 1f);
        _crystalCount = Mathf.Clamp(Mathf.Max(_crystalCount, inheritedOrbitalCount + 1), 3, _maxCrystalCount);
        _initialized = true;

        _visual = new GameObject("PrismaticCrown").AddComponent<PrismaticCrownVisual>();
        _visual.Build(_maxCrystalCount);
        RenderVisual(0f, 0f, 0f);
    }

    public void AddDamage(float value)
    {
        _rimDamage += _rimDamage * value;
        _fieldDps += _fieldDps * value;
        _laserDamage += _laserDamage * value;
    }

    // Lus par l'info-bulle / les tests.
    public int CrystalCount => _crystalCount;
    public float CrownRadius => _crownRadius;

    private void Update()
    {
        if (!_initialized) return;
        if (GameManager.Instance != null && (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused)) return;

        float dt = Time.deltaTime;

        if (GameInput.Held(GameAction.OrbitShrink))
            _crownRadius = Mathf.Max(_minCrownRadius, _crownRadius - _rangeChangeSpeed * dt);
        if (GameInput.Held(GameAction.OrbitGrow))
            _crownRadius = Mathf.Min(_maxCrownRadius, _crownRadius + _rangeChangeSpeed * dt);

        // Cycle de Réfraction : [repos ... charge (windup)] [faisceaux (duration)] puis on recommence.
        _refractionTimer += dt;
        float cycle = _refractionInterval + _refractionDuration;
        if (_refractionTimer >= cycle) _refractionTimer -= cycle;

        float charge01 = 0f, laserExtent = 0f, laserAlpha = 0f;
        bool lasersOn = _refractionTimer >= _refractionInterval;
        if (lasersOn)
        {
            float lt = _refractionTimer - _refractionInterval;
            laserExtent = FXSprites.EaseOutCubic(lt / LaserExtendTime);
            laserAlpha = Mathf.Clamp01((_refractionDuration - lt) / LaserFadeTime);
            if (lt < dt) OnRefractionStart();
        }
        else if (_refractionTimer >= _refractionInterval - _refractionWindup)
        {
            charge01 = (_refractionTimer - (_refractionInterval - _refractionWindup)) / _refractionWindup;
        }

        _angle = Mathf.Repeat(_angle + _spinSpeed * (lasersOn ? _refractionSpinBoost : 1f) * dt, 360f);

        _scanTimer += dt;
        _fieldTimer += dt;
        if (_scanTimer >= _scanInterval)
        {
            bool fieldDue = _fieldTimer >= _fieldTickRate;
            Scan(lasersOn && laserAlpha > 0.3f ? _laserLength * laserExtent : 0f, fieldDue);
            if (fieldDue) _fieldTimer = 0f;
            _scanTimer = 0f;
            _lastScanAngle = _angle;
        }

        RenderVisual(charge01, laserExtent, laserAlpha);
    }

    private void RenderVisual(float charge01, float laserExtent, float laserAlpha)
    {
        if (_visual == null) return;
        float density01 = Mathf.InverseLerp(_minDensity, _maxDensity, Density);
        _visual.Render(Center, _crownRadius, _angle, _crystalCount, density01, charge01, _laserLength * laserExtent, laserAlpha);
    }

    private void OnRefractionStart()
    {
        CameraShakeFX.Shake(0.12f);
        if (_visual != null) _visual.FlashRefraction();
        // Le balayage des faisceaux part de l'angle courant : pas de secteur fantôme hérité de la phase de repos.
        _lastScanAngle = _angle;
    }

    // ------------------------------------------------------------------------------------------------------------
    // Contacts
    // ------------------------------------------------------------------------------------------------------------

    private void Scan(float laserLength, bool fieldDue)
    {
        Vector3 c = Center;
        float scanRadius = _crownRadius + Mathf.Max(_rimHitRadius, laserLength + _laserHitRadius) + 1.5f;
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, scanRadius, _overlapBuffer, EnemyLayers.Mask);

        float now = Time.time;
        float mult = PlayerBuffs.OutgoingDamageMultiplier;
        float fieldTick = _fieldDps * Density * _fieldTickRate * mult;
        float halfStep = Mathf.PI / _crystalCount;
        float apothem = _crownRadius * Mathf.Cos(halfStep);

        _seenThisScan.Clear();
        if (fieldDue) _inFieldThisTick.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _overlapBuffer[i];
            if (col == null || !col.CompareTag("Enemy")) continue;

            col.TryGetComponent(out EnemyBase enemy);
            BossBase boss = enemy == null ? col.GetComponentInParent<BossBase>() : null;
            if (enemy == null && boss == null) continue;

            int id = enemy != null ? enemy.GetInstanceID() : boss.GetInstanceID();
            if (!_seenThisScan.Add(id)) continue;   // un boss a plusieurs colliders

            Vector3 p = enemy != null ? enemy.transform.position : boss.transform.position;
            Vector3 rel = new Vector3(p.x - c.x, 0f, p.z - c.z);
            float r = rel.magnitude;
            float phi = Mathf.Atan2(rel.z, rel.x);

            // Champ : point dans le polygone régulier (distance au centre comparée à l'apothème dans son secteur).
            float local = Mathf.Repeat(phi - _angle * Mathf.Deg2Rad, 2f * halfStep) - halfStep;
            bool inside = r * Mathf.Cos(local) <= apothem;
            if (!inside && boss != null) inside = boss.IsOnBody(c, 0.5f);

            if (fieldDue && inside)
            {
                _inFieldThisTick.Add(id);
                if (enemy != null)
                {
                    enemy.TakeDamage(fieldTick, DamageNumberSpawner.ColorManaVortex);
                    if (!_currentlySlowed.ContainsKey(id)) { enemy.SetSpeedMultiplier(_slowMultiplier); _currentlySlowed[id] = enemy; }
                }
                else
                {
                    boss.TakeDamage(fieldTick, DamageNumberSpawner.ColorManaVortex);
                    boss.SetSpeedMultiplier(_slowMultiplier);
                    _currentlySlowedBoss = boss;
                }
            }

            // Rayons du contour.
            if (!_nextRimHit.TryGetValue(id, out float nextRim) || now >= nextRim)
            {
                Vector3 contact;
                float d = DistanceToRim(c, p, out contact);
                bool touches = enemy != null ? d <= _rimHitRadius : boss.IsOnBody(contact, _rimHitRadius * 0.5f);
                if (touches)
                {
                    _nextRimHit[id] = now + _rimHitCooldown;
                    float dmg = _rimDamage * mult;
                    if (enemy != null)
                    {
                        enemy.TakeDamage(dmg, DamageNumberSpawner.ColorManaVortex);
                        if (r > 0.01f) enemy.ApplyKnockback(rel / r * _rimKnockback);
                    }
                    else boss.TakeDamage(dmg, DamageNumberSpawner.ColorManaVortex);
                    contact.y = 0.4f;
                    ImpactSparkVFX.Spawn(contact, PrismaticCrownVisual.Prism, 0.8f);
                }
            }

            // Faisceaux de Réfraction : chaque faisceau est le rayon radial du cristal i, de R à R + longueur. Contact
            // si l'ennemi est dans cette tranche de distance ET dans le secteur angulaire BALAYÉ depuis le dernier scan
            // (le faisceau tourne vite : un test ponctuel laisserait passer les ennemis entre deux scans).
            if (laserLength > 0f && (!_nextLaserHit.TryGetValue(id, out float nextLaser) || now >= nextLaser))
            {
                if (LaserTouches(enemy, boss, c, p, r, phi, laserLength, out Vector3 laserPoint))
                {
                    _nextLaserHit[id] = now + _laserHitCooldown;
                    float dmg = _laserDamage * mult;
                    if (enemy != null) enemy.TakeDamage(dmg, DamageNumberSpawner.ColorManaVortex);
                    else boss.TakeDamage(dmg, DamageNumberSpawner.ColorManaVortex);
                    laserPoint.y = 0.4f;
                    ImpactSparkVFX.Spawn(laserPoint, PrismaticCrownVisual.WhiteHot, 0.9f);
                }
            }
        }

        if (fieldDue) RestoreSpeedOutsideField();
        if (_nextRimHit.Count > 512) PruneCooldowns(_nextRimHit, now);
        if (_nextLaserHit.Count > 512) PruneCooldowns(_nextLaserHit, now);
    }

    private Vector3 CrystalPosition(Vector3 c, int i)
    {
        float a = (_angle + 360f / _crystalCount * i) * Mathf.Deg2Rad;
        return c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * _crownRadius;
    }

    private float DistanceToRim(Vector3 c, Vector3 p, out Vector3 contact)
    {
        Vector3 flatP = new Vector3(p.x, 0f, p.z);
        float best = float.MaxValue;
        contact = flatP;
        Vector3 a = CrystalPosition(c, 0);
        for (int i = 0; i < _crystalCount; i++)
        {
            Vector3 b = CrystalPosition(c, (i + 1) % _crystalCount);
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(flatP - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            Vector3 q = a + ab * t;
            float d = (flatP - q).magnitude;
            if (d < best) { best = d; contact = q; }
            a = b;
        }
        return best;
    }

    private bool LaserTouches(EnemyBase enemy, BossBase boss, Vector3 c, Vector3 p, float r, float phi, float laserLength, out Vector3 point)
    {
        point = p;
        float step = 360f / _crystalCount;
        // Secteur balayé depuis le dernier scan (angles du cristal 0), ramené à [0, 360).
        float sweep = Mathf.Repeat(_angle - _lastScanAngle, 360f);

        for (int i = 0; i < _crystalCount; i++)
        {
            float laserAngle = _angle + step * i;
            if (enemy != null)
            {
                if (r < _crownRadius - _laserHitRadius || r > _crownRadius + laserLength + _laserHitRadius) continue;
                float halfWidthDeg = Mathf.Atan2(_laserHitRadius, Mathf.Max(0.5f, r)) * Mathf.Rad2Deg;
                // Retard angulaire de l'ennemi derrière le faisceau : contact s'il est dans [-largeur, balayage + largeur].
                float behind = Mathf.DeltaAngle(phi * Mathf.Rad2Deg, laserAngle);
                if (behind < -halfWidthDeg || behind > sweep + halfWidthDeg) continue;
                float a = laserAngle * Mathf.Deg2Rad;
                point = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                return true;
            }

            // Boss : point du faisceau le plus proche de son centre, puis test sur sa vraie silhouette.
            float la = laserAngle * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(la), 0f, Mathf.Sin(la));
            Vector3 start = c + dir * _crownRadius;
            float t = Mathf.Clamp(Vector3.Dot(new Vector3(p.x, 0f, p.z) - start, dir), 0f, laserLength);
            Vector3 q = start + dir * t;
            if (boss.IsOnBody(q, _laserHitRadius * 0.5f)) { point = q; return true; }
        }
        return false;
    }

    private void RestoreSpeedOutsideField()
    {
        List<int> toRemove = null;
        foreach (var kvp in _currentlySlowed)
        {
            if (_inFieldThisTick.Contains(kvp.Key)) continue;
            if (kvp.Value != null) kvp.Value.SetSpeedMultiplier(1f);
            (toRemove ??= new List<int>()).Add(kvp.Key);
        }
        if (toRemove != null) foreach (int id in toRemove) _currentlySlowed.Remove(id);

        if (_currentlySlowedBoss != null && !_inFieldThisTick.Contains(_currentlySlowedBoss.GetInstanceID()))
        {
            _currentlySlowedBoss.SetSpeedMultiplier(1f);
            _currentlySlowedBoss = null;
        }
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
        foreach (var kvp in _currentlySlowed) if (kvp.Value != null) kvp.Value.SetSpeedMultiplier(1f);
        if (_currentlySlowedBoss != null) _currentlySlowedBoss.SetSpeedMultiplier(1f);
        if (_visual != null) Destroy(_visual.gameObject);
    }
}
