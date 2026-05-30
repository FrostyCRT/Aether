using UnityEngine;
using System.Collections.Generic;

public class WeaponOrbital : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _damage       = 15f;
    [SerializeField] private float _orbitRadius  = 3f;
    [SerializeField] private float _orbitSpeed   = 180f;
    [SerializeField] private int   _orbitalCount = 2;

    [Header("Références")]
    [SerializeField] private GameObject _orbitalPrefab;

    [Header("Limites")]
    [SerializeField] private int _maxOrbitalCount = 5; // Maximum 5 orbes
    public bool IsMaxOrbital() => _orbitalCount >= _maxOrbitalCount;

    public void AddOrbital()
    {
        if (_orbitalCount >= _maxOrbitalCount)
        {
            Debug.Log("Nombre maximum d'orbitaux atteint !");
            return;
        }
        _orbitalCount++;
        SpawnOrbitals();
    }

    private List<GameObject> _orbitals = new List<GameObject>();
    private float            _currentAngle = 0f;

    private void Start()
    {
        if (_orbitalPrefab != null)
            SpawnOrbitals();
        else
            Debug.LogWarning("WeaponOrbital : Orbital Prefab non assigné !");
    }

    public void Init(GameObject orbitalPrefab)
    {
        _orbitalPrefab = orbitalPrefab;
        SpawnOrbitals();
    }

    private void SpawnOrbitals()
    {
        foreach (GameObject orbital in _orbitals)
            Destroy(orbital);
        _orbitals.Clear();

        for (int i = 0; i < _orbitalCount; i++)
        {
            GameObject orbital = Instantiate(_orbitalPrefab, transform.position, Quaternion.identity);
            orbital.transform.SetParent(transform);
            _orbitals.Add(orbital);
        }
    }

    private void Update()
    {
        if (GameManager.Instance.IsGameOver) return;
        if (_orbitals.Count == 0) return;

        _currentAngle += _orbitSpeed * Time.deltaTime;
        float angleStep = 360f / _orbitalCount;

        for (int i = 0; i < _orbitals.Count; i++)
        {
            float angle = (_currentAngle + angleStep * i) * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * _orbitRadius;
            float z = Mathf.Sin(angle) * _orbitRadius;
            _orbitals[i].transform.localPosition = new Vector3(x, 0f, z);
        }
    }


    public void AddDamage(float value) => _damage      += _damage * value;
    
}