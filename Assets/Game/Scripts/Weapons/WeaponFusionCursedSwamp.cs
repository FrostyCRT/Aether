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
    [Header("Zones (héritées de la Boue)")]
    [SerializeField] private float _spawnInterval = 3f;
    [SerializeField] private float _puddleDuration = 4f;
    [SerializeField] private float _puddleRadius = 2f;
    [SerializeField] private float _slowMultiplier = 0.6f;
    [SerializeField] private int _puddleCount = 7;
    [SerializeField] private float _spawnDistance = 6.5f;
    [SerializeField] private float _groundY = 0.2f;
    [SerializeField] private float _rotationPerWave = 25f;

    [Header("Décharge de foudre (héritée de la Foudre)")]
    [SerializeField] private float _shockInterval = 0.35f;
    [SerializeField] private int _maxChains = 3;
    [SerializeField] private float _chainRange = 5f;

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
        _puddleDamagePerSecond = inheritedPuddleDamage * 1.3f;
        _lightningDamage = inheritedLightningDamage * 1.3f;
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
        int count = Physics.OverlapSphereNonAlloc(position, _puddleRadius, _overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider hit = _overlapBuffer[i];
            if (hit != null && hit.CompareTag("Enemy")) return hit.transform;
        }
        return null;
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
            if (zone != null) zone.Init(_puddleDuration, _puddleRadius, _slowMultiplier, _puddleDamagePerSecond);

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
