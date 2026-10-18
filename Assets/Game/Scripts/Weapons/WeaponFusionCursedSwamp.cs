using UnityEngine;
using System.Collections.Generic;

// FUSION (2026-09-27) : Boue + Foudre, toutes les deux au palier max → cette arme les remplace [universelle].
// "Marécage Maudit" — nom déjà envisagé dans un commentaire de WeaponMudPuddle.cs avant même l'existence du système
// de fusion. Les zones sont identiques à WeaponMudPuddle (même prefab poolé "MudPuddleZone", ralentissement + DPS),
// mais chaque zone déclenche aussi, à intervalle régulier, une décharge de foudre en chaîne sur un ennemi qu'elle
// touche (voir LightningChainUtility).
public class WeaponFusionCursedSwamp : MonoBehaviour
{
    // MODIFIE (2026-09-27) - mesuré en jeu (retour utilisateur : "la fusion doit être plus forte que les 2 upgrades
    // card au max") : la version d'origine était en fait PLUS FAIBLE que les 2 cartes maxées séparément (0,80x mesuré,
    // voir NOTES.md) - _puddleCount=3 était même en dessous du max de la Boue seule (6, via ses 3 picks AddPuddle),
    // et la décharge de foudre ajoutée ne compensait pas cet écart. Nombre de flaques et fréquence de décharge
    // augmentés + bonus de dégâts explicite - retesté à l'équivalent des autres fusions après ce changement.
    // CORRIGÉ (2026-10-18, audit complet des fusions) - remesuré avec la méthode [[balance-testing-dummies]] : la
    // frappe de foudre ACTIVE (ci-dessous) sauve cette fusion du même bug trouvé sur Terre Calcinée (elle n'est
    // pas réduite à 0 comme Terre Calcinée l'était), mais ne faisait que ×1,73 la référence (957 DPS contre 553) -
    // une marge trop juste pour "très puissant". Cause identique : _spawnDistance=6,5 vise un anneau que les
    // ennemis ne traversent qu'une fois avant de rester plaqués au corps-à-corps (~1,2 unité). Rapproché à 3,2 (la
    // Boue elle-même n'est PAS touchée, seule cette fusion) - voir WeaponFusionScorchedEarth pour le même
    // raisonnement détaillé.
    [Header("Zones (héritées de la Boue)")]
    [SerializeField] private float _spawnInterval = 3f;
    [SerializeField] private float _puddleDuration = 4f;
    [SerializeField] private float _puddleRadius = 2f;
    [SerializeField] private float _slowMultiplier = 0.6f;
    [SerializeField] private int _puddleCount = 7;
    [SerializeField] private Color _cursedTint = new Color(0.35f, 0.55f, 0.2f); // vert maladif - distinct de la Boue normale (ambre) et de Terre Calcinée (braises)
    [SerializeField] private float _spawnDistance = 3.2f;
    [SerializeField] private float _groundY = 0.2f;
    [SerializeField] private float _rotationPerWave = 25f;

    [Header("Décharge de foudre (héritée de la Foudre, au contact d'une flaque)")]
    [SerializeField] private float _shockInterval = 0.35f;
    [SerializeField] private int _maxChains = 3;
    [SerializeField] private float _chainRange = 5f;

    // AJOUTÉ (2026-10-05) - retour utilisateur : "la Foudre va sur l'ennemi le plus proche instantanément, alors
    // que si elle est fusionnée [...] on est obligé d'attendre qu'un ennemi soit touché [...] c'est comme un nerf
    // de la Foudre" - voir WeaponFusionThunderingOrbs pour la même correction/explication détaillée. La Foudre
    // garde ici aussi sa frappe active autonome, EN PLUS du proc au contact d'une flaque (jamais à la place).
    [Header("Décharge de foudre ACTIVE (comportement de la Foudre seule, préservé tel quel)")]
    [SerializeField] private float _activeStrikeInterval = 1f;
    [SerializeField] private float _activeDetectionRange = 15f;
    private float _activeStrikeTimer = 0f;
    private static readonly Collider[] _detectionBuffer = new Collider[64];

    private float _puddleDamagePerSecond;
    private float _lightningDamage;
    private GameObject _puddlePrefab;
    private float _waveTimer = 0f;
    private float _currentWaveRotation = 0f;
    private readonly List<GameObject> _activePuddles = new List<GameObject>();
    private readonly List<float> _nextShockTime = new List<float>();
    private static readonly Collider[] _overlapBuffer = new Collider[16];

    public void Init(GameObject puddlePrefab, float inheritedPuddleDamage, float inheritedLightningDamage)
    {
        _puddlePrefab = puddlePrefab;
        // CORRIGÉ (2026-10-18) - ×1,3/×1,1 dataient d'avant la correction de _spawnDistance ci-dessus. Une fois les
        // flaques capables de toucher les ennemis de façon fiable, CHAQUE flaque occupée déclenche sa propre chaîne
        // de foudre complète (jusqu'à _maxChains+1 cibles) toutes les 0,35s, EN PLUS de la frappe active toutes les
        // 1s - un comportement à PROC, pas un DPS continu plat comme Terre Calcinée, donc le gain de fiabilité se
        // multiplie bien plus fort : mesuré à ×20 la référence (11270 DPS contre 553) avec les anciens
        // multiplicateurs. Redescendus fortement pour revenir à une marge "très puissant" mais pas absurde - un
        // premier essai à ×0,3/×0,3 donnait encore ×5,35 (2957 DPS), redescendu une dernière fois à ×0,17/×0,17.
        _puddleDamagePerSecond = inheritedPuddleDamage * 0.17f;
        _lightningDamage = inheritedLightningDamage * 0.17f;
    }

    public void AddDamage(float value)
    {
        _puddleDamagePerSecond += _puddleDamagePerSecond * value;
        _lightningDamage += _lightningDamage * value;
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        _activeStrikeTimer += Time.deltaTime;
        if (_activeStrikeTimer >= _activeStrikeInterval)
        {
            Transform nearest = FindNearestEnemy();
            if (nearest != null)
            {
                _activeStrikeTimer = 0f;
                LightningChainUtility.Trigger(this, nearest, _lightningDamage, _maxChains, _chainRange);
            }
        }

        _waveTimer += Time.deltaTime;
        if (_waveTimer >= _spawnInterval)
        {
            SpawnWave();
            _waveTimer = 0f;
        }

        for (int i = 0; i < _activePuddles.Count; i++)
        {
            if (_activePuddles[i] == null) continue;
            if (Time.time < _nextShockTime[i]) continue;
            Transform enemyHere = FindEnemyTouching(_activePuddles[i].transform.position);
            if (enemyHere == null) continue;
            _nextShockTime[i] = Time.time + _shockInterval;
            LightningChainUtility.Trigger(this, enemyHere, _lightningDamage, _maxChains, _chainRange);
        }
    }

    private Transform FindEnemyTouching(Vector3 position)
    {
        int count = Physics.OverlapSphereNonAlloc(position, _puddleRadius, _overlapBuffer, EnemyLayers.Mask);
        for (int i = 0; i < count; i++)
        {
            Collider hit = _overlapBuffer[i];
            if (hit != null && hit.CompareTag("Enemy")) return hit.transform;
        }
        return null;
    }

    // Même logique que WeaponLightningChain.FindNearestEnemy - la frappe active cherche sa cible dans toute la
    // zone de détection, pas seulement au contact d'une flaque (voir note ci-dessus).
    private Transform FindNearestEnemy()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, _activeDetectionRange, _detectionBuffer, EnemyLayers.Mask);
        Transform nearest = null;
        float minDistSqr = _activeDetectionRange * _activeDetectionRange;
        for (int i = 0; i < count; i++)
        {
            Collider col = _detectionBuffer[i];
            if (col == null || !col.CompareTag("Enemy")) continue;
            float distSqr = (col.transform.position - transform.position).sqrMagnitude;
            if (distSqr < minDistSqr) { minDistSqr = distSqr; nearest = col.transform; }
        }
        return nearest;
    }

    private void SpawnWave()
    {
        if (ObjectPool.Instance == null || _puddlePrefab == null) return;
        ForceExpireAllActivePuddles();

        int slotCount = Mathf.Max(1, _puddleCount);
        float angleStep = 360f / slotCount;
        for (int i = 0; i < slotCount; i++)
        {
            float angle = _currentWaveRotation + angleStep * i;
            float rad = angle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * _spawnDistance;
            Vector3 spawnPos = transform.position + offset;
            spawnPos = WaveManager.MapBoundaryUtils.ClampToZone(spawnPos);
            spawnPos.y = _groundY;

            GameObject puddleGO = ObjectPool.Instance.Get("MudPuddleZone", spawnPos, Quaternion.identity);
            if (puddleGO == null) continue;

            MudPuddleZone zone = puddleGO.GetComponent<MudPuddleZone>();
            if (zone != null)
            {
                zone.Init(_puddleDuration, _puddleRadius, _slowMultiplier, _puddleDamagePerSecond);
                // AJOUTÉ (2026-10-05) - même raison que WeaponFusionScorchedEarth : distingue cette fusion
                // (Boue + Foudre) d'une Boue normale au premier coup d'œil.
                zone.SetColorTint(_cursedTint);
            }

            _activePuddles.Add(puddleGO);
            _nextShockTime.Add(0f);
        }
        _currentWaveRotation = (_currentWaveRotation + _rotationPerWave) % 360f;
    }

    private void ForceExpireAllActivePuddles()
    {
        for (int i = 0; i < _activePuddles.Count; i++)
        {
            if (_activePuddles[i] == null) continue;
            MudPuddleZone zone = _activePuddles[i].GetComponent<MudPuddleZone>();
            if (zone != null) zone.ForceExpire();
        }
        _activePuddles.Clear();
        _nextShockTime.Clear();
    }
}
