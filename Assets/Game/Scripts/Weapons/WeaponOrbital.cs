using UnityEngine;
using System.Collections.Generic;

public class WeaponOrbital : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _baseDamage = 150f; // MODIFIE - x10, cf. rescale global des degats/PV
    [SerializeField] private float _orbitRadius = 3f;
    [SerializeField] private float _orbitSpeed = 180f;
    [SerializeField] private int _orbitalCount = 2;

    [Header("Contrôle Range (A/E)")]
    [SerializeField] private float _minOrbitRadius = 1f;
    [SerializeField] private float _maxOrbitRadius = 8f;
    [SerializeField] private float _rangeChangeSpeed = 2f;

    [Header("Références")]
    [SerializeField] private GameObject _orbitalPrefab;

    [Header("Limites")]
    [SerializeField] private int _maxOrbitalCount = 10;

    private float _upgradeDamageModifier = 0f;
    private float _currentDamage;

    private readonly List<GameObject> _orbitals = new List<GameObject>();
    private readonly List<OrbitalProjectile> _orbitalScriptsCache = new List<OrbitalProjectile>();
    private float _currentAngle = 0f;
    private bool _isInitialized = false;

    public bool IsMaxOrbital() => _orbitalCount >= _maxOrbitalCount;

    // MODIFIE - applique le bonus de Reputation Degats AVANT le premier calcul de
    // _currentDamage, meme trou que Fireball/Aura/Knives corrige plus tot.
    private void Awake()
    {
        if (MetaProgressionManager.Instance != null)
        {
            float bonusDamage = MetaProgressionManager.Instance.GetReputationBonusDamage();
            _baseDamage += _baseDamage * bonusDamage;
        }
        UpdateCalculatedStats();
    }

    private void Start()
    {
        if (!_isInitialized && _orbitalPrefab != null)
        {
            SpawnOrbitals();
        }
    }

    public void Init(GameObject orbitalPrefab)
    {
        _orbitalPrefab = orbitalPrefab;
        SpawnOrbitals();
    }

    public void AddOrbital()
    {
        if (IsMaxOrbital())
        {
            Debug.LogWarning("[WeaponOrbital] AddOrbital() appelé alors que le plafond interne est atteint — vérifier la config UpgradeData/LevelUpManager, ce cas ne devrait jamais arriver en jeu normal.");
            return;
        }
        _orbitalCount++;
        SpawnOrbitals();
    }

    private void SpawnOrbitals()
    {
        _isInitialized = true;

        foreach (GameObject orbital in _orbitals)
        {
            if (orbital != null) ObjectPool.Instance.ReturnToPool("OrbitalProjectile", orbital);
        }

        _orbitals.Clear();
        _orbitalScriptsCache.Clear();

        for (int i = 0; i < _orbitalCount; i++)
        {
            GameObject orbital = ObjectPool.Instance.Get("OrbitalProjectile", transform.position, Quaternion.identity);

            if (orbital != null)
            {
                orbital.transform.SetParent(transform);
                _orbitals.Add(orbital);

                OrbitalProjectile projScript = orbital.GetComponent<OrbitalProjectile>();
                if (projScript != null)
                {
                    _orbitalScriptsCache.Add(projScript);
                    projScript.SetDamage(_currentDamage);
                }
            }
        }
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        if (_orbitals.Count == 0) return;

        if (GameInput.Held(GameAction.OrbitShrink))
            _orbitRadius = Mathf.Max(_minOrbitRadius, _orbitRadius - _rangeChangeSpeed * Time.deltaTime);
        if (GameInput.Held(GameAction.OrbitGrow))
            _orbitRadius = Mathf.Min(_maxOrbitRadius, _orbitRadius + _rangeChangeSpeed * Time.deltaTime);

        _currentAngle += _orbitSpeed * Time.deltaTime;
        float angleStep = 360f / _orbitalCount;

        for (int i = 0; i < _orbitals.Count; i++)
        {
            if (_orbitals[i] == null) continue;

            float angle = (_currentAngle + angleStep * i) * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * _orbitRadius;
            float z = Mathf.Sin(angle) * _orbitRadius;

            Vector3 orbitOffset = new Vector3(x, 0f, z);
            _orbitals[i].transform.position = transform.position + orbitOffset;
        }
    }

    private void UpdateCalculatedStats()
    {
        _currentDamage = _baseDamage * (1f + _upgradeDamageModifier);

        for (int i = 0; i < _orbitalScriptsCache.Count; i++)
        {
            if (_orbitalScriptsCache[i] != null)
            {
                _orbitalScriptsCache[i].SetDamage(_currentDamage);
            }
        }
    }

    public void AddDamage(float value)
    {
        _upgradeDamageModifier += value;
        UpdateCalculatedStats();
    }
}