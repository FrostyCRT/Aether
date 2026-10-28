using UnityEngine;
using System.Collections.Generic;

// FUSION (2026-09-27) : Boue + Foudre, toutes les deux au palier max → cette arme les remplace [universelle].
//
// REFONTE COMPLÈTE (2026-10-27, retour utilisateur : "Revoir la fusion Boue + Foudre, elle n'est pas originale comme
// terre calcinée en pluie de météorite [...] la fusion doit être plus puissante et impactante sur la map que les 2
// sous fusion au max [...] ce qu'il pourrait manquer pour que ce soit complet dans les manières de jouer") - l'ancien
// "Marécage Maudit" n'était qu'une Boue teintée en vert dont les flaques déclenchaient des chaînes de foudre. Devient
// "Tourbière Magnétique", le seul CONTRÔLE DE FOULE du jeu (aucune autre arme ne déplace les ennemis) :
//  1. Des tourbières électrisées apparaissent sur les groupes d'ennemis proches du joueur (identité Boue : ralentit,
//     DPS continu, même prefab poolé MudPuddleZone).
//  2. Pendant la charge, elles ASPIRENT les ennemis vers leur centre (EnemyBase.PullTowards) et crépitent d'arcs de
//     plus en plus nombreux, la flaque passant du brun boueux à l'or de la foudre : le joueur voit la charge monter.
//  3. À la fin de la charge, surcharge : la foudre tombe du ciel au centre et frappe tout le groupe rassemblé
//     (identité Foudre : gros dégât instantané).
// Effets de charge et de surcharge dédiés : voir MagneticBogVFX.
// Regrouper les ennemis profite aussi à toutes les autres armes de zone du joueur. La frappe active autonome de la
// Foudre est conservée telle quelle (retour utilisateur du 2026-10-05 : la fusionner ne doit jamais la retirer).
// Les boss ne sont jamais aspirés, mais subissent la flaque et la surcharge.
// Nom de classe et fusionResultId "CursedSwamp" conservés : ils sont référencés par l'asset SO_Fusion_CursedSwamp et
// les sauvegardes.
public class WeaponFusionCursedSwamp : MonoBehaviour
{
    [Header("Tourbières (héritées de la Boue)")]
    [SerializeField] private float _bogInterval = 3f;
    [SerializeField] private int _bogCount = 3;
    [SerializeField] private float _chargeDuration = 2.5f;
    [SerializeField] private float _bogRadius = 3f;
    [SerializeField] private float _slowMultiplier = 0.5f;
    [SerializeField] private float _groundY = 0.2f;
    // Tourbe sombre (bien plus foncée que la Boue normale) qui se réchauffe à peine pendant la charge : un or franc sur
    // ce matériau éclairé rendait un disque orange uni qui noyait les effets de MagneticBogVFX. Le fond reste sombre,
    // ce sont les cristaux et l'éclat doré du cœur qui montrent la charge.
    [SerializeField] private Color _mudTint = new Color(0.17f, 0.15f, 0.13f);
    [SerializeField] private Color _chargedTint = new Color(0.4f, 0.32f, 0.14f);

    [Header("Placement")]
    [SerializeField] private float _targetSearchRange = 13f;
    [SerializeField] private float _minBogSpacing = 5f;
    [SerializeField] private float _fallbackDistance = 4f;

    [Header("Aspiration")]
    // MODIFIÉ (2026-10-27, retour utilisateur : "la range de la zone d'attraction est trop grande, fais-la presque de la
    // même taille que la flaque de boue, mais un peu plus grande") - 6 -> 3,6 (flaque : 3).
    [SerializeField] private float _pullRadius = 3.6f;
    [SerializeField] private float _pullSpeed = 6f;
    [SerializeField] private float _pullMinDistance = 0.7f;

    [Header("Arcs pendant la charge (Foudre)")]
    [SerializeField] private float _arcInterval = 0.4f;
    [SerializeField] private float _crackleIntervalStart = 0.22f;
    [SerializeField] private float _crackleIntervalEnd = 0.05f;

    [Header("Surcharge")]
    [SerializeField] private float _detonationRadius = 3.6f;
    [SerializeField] private int _maxDetonationBolts = 8;
    [SerializeField] private float _skyBoltHeight = 12f;

    [Header("Frappe ACTIVE (comportement de la Foudre seule, préservé tel quel)")]
    [SerializeField] private float _activeStrikeInterval = 1f;
    [SerializeField] private float _activeDetectionRange = 15f;
    [SerializeField] private int _maxChains = 3;
    [SerializeField] private float _chainRange = 5f;

    private static readonly Color LightningColor = new Color(1f, 0.95f, 0.55f); // même jaune que LightningChainUtility
    private const float VfxLift = 0.1f; // effets (MagneticBogVFX) posés juste au-dessus de la flaque (dessus à Y=0,25)

    private class Bog
    {
        public Vector3 Center;
        public MudPuddleZone Zone;
        public float Age;
        public float NextArc;
        public float NextCrackle;
    }

    private readonly List<Bog> _bogs = new List<Bog>();
    private readonly List<Vector3> _candidates = new List<Vector3>();
    private readonly List<Vector3> _chosen = new List<Vector3>();
    private readonly List<EnemyBase> _arcTargets = new List<EnemyBase>();
    private readonly HashSet<int> _hitBosses = new HashSet<int>();
    private static readonly Collider[] _buffer = new Collider[96];

    private GameObject _puddlePrefab;
    private float _bogDps;
    private float _arcDamage;
    private float _detonationDamage;
    private float _activeStrikeDamage;
    private float _bogTimer;
    private float _activeStrikeTimer;

    public void Init(GameObject puddlePrefab, float inheritedPuddleDamage, float inheritedLightningDamage)
    {
        _puddlePrefab = puddlePrefab;
        // PREMIER JET (2026-10-27) - à remesurer en jeu. Valeurs héritées déjà boostées par les picks (pas de second
        // bonus de Réputation ici, voir WeaponFusionScorchedEarth.Init). La surcharge porte l'essentiel : un groupe
        // aspiré prend 2,5x la Foudre maxée d'un coup, toutes les 3 s, sur chacune des 3 tourbières.
        _bogDps = inheritedPuddleDamage * 1.5f;
        _arcDamage = inheritedLightningDamage * 0.4f;
        _detonationDamage = inheritedLightningDamage * 2.5f;
        _activeStrikeDamage = inheritedLightningDamage;
        _bogTimer = _bogInterval; // première vague immédiate
    }

    public void AddDamage(float value)
    {
        _bogDps += _bogDps * value;
        _arcDamage += _arcDamage * value;
        _detonationDamage += _detonationDamage * value;
        _activeStrikeDamage += _activeStrikeDamage * value; // les tourbières déjà posées gardent leur DPS, les suivantes prennent le nouveau
    }

    private void Update()
    {
        if (GameManager.Instance != null && (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused)) return;

        _activeStrikeTimer += Time.deltaTime;
        if (_activeStrikeTimer >= _activeStrikeInterval)
        {
            Transform nearest = FindNearestEnemy();
            if (nearest != null)
            {
                _activeStrikeTimer = 0f;
                LightningChainUtility.Trigger(this, nearest, _activeStrikeDamage, _maxChains, _chainRange);
            }
        }

        _bogTimer += Time.deltaTime;
        if (_bogTimer >= _bogInterval)
        {
            _bogTimer = 0f;
            SpawnBogs();
        }

        for (int i = _bogs.Count - 1; i >= 0; i--)
        {
            Bog bog = _bogs[i];
            bog.Age += Time.deltaTime;
            if (bog.Age >= _chargeDuration)
            {
                Detonate(bog);
                _bogs.RemoveAt(i);
                continue;
            }
            UpdateBog(bog);
        }
    }

    private void OnDisable()
    {
        for (int i = 0; i < _bogs.Count; i++)
            if (_bogs[i].Zone != null) _bogs[i].Zone.ForceExpire();
        _bogs.Clear();
    }

    // ---------- Placement ----------

    // Une tourbière par groupe : positions d'ennemis proches tirées au hasard, en gardant un écart minimum entre les
    // tourbières (et avec celles encore actives). Places restantes : autour du joueur, pour que la fusion reste
    // visible même sans ennemi à portée.
    private void SpawnBogs()
    {
        if (ObjectPool.Instance == null || _puddlePrefab == null) return;

        _candidates.Clear();
        int count = Physics.OverlapSphereNonAlloc(transform.position, _targetSearchRange, _buffer, EnemyLayers.Mask);
        for (int i = 0; i < count; i++)
        {
            Collider col = _buffer[i];
            if (col == null || !col.CompareTag("Enemy")) continue;
            _candidates.Add(col.transform.position);
        }

        _chosen.Clear();
        float spacingSqr = _minBogSpacing * _minBogSpacing;
        while (_candidates.Count > 0 && _chosen.Count < _bogCount)
        {
            int pick = Random.Range(0, _candidates.Count);
            Vector3 pos = _candidates[pick];
            _candidates[pick] = _candidates[_candidates.Count - 1];
            _candidates.RemoveAt(_candidates.Count - 1);
            if (IsFarFromOthers(pos, spacingSqr)) _chosen.Add(pos);
        }

        float startAngle = Random.Range(0f, 360f);
        for (int i = 0; _chosen.Count < _bogCount && i < _bogCount; i++)
        {
            float rad = (startAngle + 360f / _bogCount * i) * Mathf.Deg2Rad;
            _chosen.Add(transform.position + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * _fallbackDistance);
        }

        for (int i = 0; i < _chosen.Count; i++)
            SpawnBog(_chosen[i]);
    }

    private bool IsFarFromOthers(Vector3 pos, float spacingSqr)
    {
        for (int i = 0; i < _chosen.Count; i++)
            if (FlatSqr(_chosen[i] - pos) < spacingSqr) return false;
        for (int i = 0; i < _bogs.Count; i++)
            if (FlatSqr(_bogs[i].Center - pos) < spacingSqr) return false;
        return true;
    }

    private static float FlatSqr(Vector3 v) => v.x * v.x + v.z * v.z;

    private void SpawnBog(Vector3 position)
    {
        position = MapBoundaryUtils.ClampToZone(position);
        position.y = _groundY;

        GameObject puddleGO = ObjectPool.Instance.Get("MudPuddleZone", position, Quaternion.identity);
        if (puddleGO == null) return;
        puddleGO.TryGetComponent(out MudPuddleZone zone);
        if (zone == null) return;

        // Durée de la flaque un peu plus longue que la charge : c'est la surcharge qui la fait disparaître.
        zone.Init(_chargeDuration + 0.5f, _bogRadius, _slowMultiplier, _bogDps);
        zone.SetColorTint(_mudTint);
        MagneticBogVFX.SpawnCharge(position + Vector3.up * VfxLift, _bogRadius, _pullRadius, _chargeDuration);
        _bogs.Add(new Bog { Center = position, Zone = zone, NextArc = _arcInterval, NextCrackle = 0f });
    }

    // ---------- Charge ----------

    private void UpdateBog(Bog bog)
    {
        float charge = bog.Age / _chargeDuration;

        // La flaque s'illumine vers l'or de la foudre, avec un scintillement de plus en plus marqué en fin de charge.
        if (bog.Zone != null)
        {
            float flicker = charge > 0.6f ? Random.Range(-0.15f, 0.15f) * charge : 0f;
            bog.Zone.SetColorTint(Color.Lerp(_mudTint, _chargedTint, Mathf.Clamp01(charge * charge + flicker)));
        }

        _arcTargets.Clear();
        int count = Physics.OverlapSphereNonAlloc(bog.Center, _pullRadius, _buffer, EnemyLayers.Mask);
        for (int i = 0; i < count; i++)
        {
            Collider col = _buffer[i];
            if (col == null || !col.TryGetComponent(out EnemyBase enemy)) continue;
            enemy.PullTowards(bog.Center, _pullSpeed, _pullMinDistance);
            _arcTargets.Add(enemy);
        }

        if (bog.Age >= bog.NextArc)
        {
            bog.NextArc += _arcInterval;
            if (_arcTargets.Count > 0)
            {
                EnemyBase target = _arcTargets[Random.Range(0, _arcTargets.Count)];
                LightningBoltVFX.SpawnBolt(bog.Center + Vector3.up * 0.3f, target.transform.position, LightningColor);
                target.TakeDamage(_arcDamage * PlayerBuffs.OutgoingDamageMultiplier, DamageNumberSpawner.ColorLightning);
            }
        }

        // Crépitement purement visuel : petits arcs au ras de la flaque, de plus en plus fréquents.
        if (bog.Age >= bog.NextCrackle)
        {
            bog.NextCrackle = bog.Age + Mathf.Lerp(_crackleIntervalStart, _crackleIntervalEnd, charge);
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 from = bog.Center + Vector3.up * 0.15f + dir * Random.Range(0f, _bogRadius * 0.3f);
            Vector3 to = bog.Center + Vector3.up * 0.15f + dir * Random.Range(_bogRadius * 0.6f, _bogRadius);
            LightningBoltVFX.SpawnBolt(from, to, LightningColor, 0.12f, 0.06f);
        }
    }

    // ---------- Surcharge ----------

    private void Detonate(Bog bog)
    {
        Vector3 center = bog.Center;
        if (bog.Zone != null) bog.Zone.ForceExpire();

        LightningBoltVFX.SpawnBolt(center + Vector3.up * _skyBoltHeight, center, LightningColor, 0.28f, 0.4f);
        LightningBoltVFX.SpawnBolt(center + new Vector3(0.6f, _skyBoltHeight, -0.4f), center, LightningColor, 0.22f, 0.2f);
        LightningBoltVFX.SpawnStrikeFlash(center, LightningColor, 0.25f);
        MagneticBogVFX.SpawnBurst(center + Vector3.up * VfxLift, _detonationRadius);
        CameraShakeFX.Shake(0.2f);

        float damage = _detonationDamage * PlayerBuffs.OutgoingDamageMultiplier;
        int bolts = 0;
        _hitBosses.Clear();
        int count = Physics.OverlapSphereNonAlloc(center, _detonationRadius, _buffer, EnemyLayers.Mask);
        for (int i = 0; i < count; i++)
        {
            Collider col = _buffer[i];
            if (col == null) continue;

            if (col.TryGetComponent(out EnemyBase enemy))
            {
                if (bolts < _maxDetonationBolts)
                {
                    LightningBoltVFX.SpawnBolt(center + Vector3.up * 0.3f, enemy.transform.position, LightningColor, 0.2f, 0.1f);
                    bolts++;
                }
                enemy.TakeDamage(damage, DamageNumberSpawner.ColorLightning);
                continue;
            }

            BossBase boss = col.GetComponentInParent<BossBase>();
            if (boss != null && _hitBosses.Add(boss.GetInstanceID()))
                boss.TakeDamage(damage, DamageNumberSpawner.ColorLightning);
        }
    }

    // ---------- Frappe active ----------

    // Même logique que WeaponLightningChain.FindNearestEnemy.
    private Transform FindNearestEnemy()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, _activeDetectionRange, _buffer, EnemyLayers.Mask);
        Transform nearest = null;
        float minDistSqr = _activeDetectionRange * _activeDetectionRange;
        for (int i = 0; i < count; i++)
        {
            Collider col = _buffer[i];
            if (col == null || !col.CompareTag("Enemy")) continue;
            float distSqr = (col.transform.position - transform.position).sqrMagnitude;
            if (distSqr < minDistSqr) { minDistSqr = distSqr; nearest = col.transform; }
        }
        return nearest;
    }
}
