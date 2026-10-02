using UnityEngine;
using System.Collections.Generic;

// FUSION (2026-09-27) : Double tir + Orbe rebondissant, toutes les deux au palier max → cette arme les remplace
// [universelle]. Double tir n'a pas de composant propre (il ne fait que marquer WeaponBase, qui reste toujours en
// place — c'est l'attaque de base du personnage, jamais détruite) : seul l'Orbe rebondissant est réellement remplacé.
// Deux orbes rebondissants partent TOUJOURS ensemble, dans des directions opposées (le "jumelage" du Double tir),
// avec des dégâts hérités et boostés.
public class WeaponFusionTwinOrbs : MonoBehaviour
{
    [Header("Stats (héritées de l'Orbe rebondissant, boostées par le Double tir)")]
    [SerializeField] private float _speed = 7f;
    [SerializeField] private int _orbCount = 2;

    private float _damage;
    private GameObject _orbPrefab;
    private readonly List<GameObject> _orbs = new List<GameObject>();

    public void Init(GameObject orbPrefab, float inheritedDamage)
    {
        _orbPrefab = orbPrefab;
        _damage = inheritedDamage;
        SpawnOrbs();
    }

    public void AddDamage(float value)
    {
        _damage += _damage * value;
        foreach (var go in _orbs)
        {
            if (go == null) continue;
            BouncingOrbProjectile proj = go.GetComponent<BouncingOrbProjectile>();
            if (proj != null) proj.SetStats(_damage, _speed);
        }
    }

    private void SpawnOrbs()
    {
        if (ObjectPool.Instance == null || _orbPrefab == null) return;

        // Les 2 orbes partent dos à dos (directions opposées), c'est le "jumelage" hérité du Double tir.
        Vector2 dir0 = Random.insideUnitCircle.normalized;
        if (dir0.sqrMagnitude < 0.01f) dir0 = Vector2.right;

        for (int i = 0; i < _orbCount; i++)
        {
            GameObject orbGO = ObjectPool.Instance.Get("BouncingOrbProjectile", transform.position, Quaternion.identity);
            if (orbGO == null) break;
            BouncingOrbProjectile proj = orbGO.GetComponent<BouncingOrbProjectile>();
            Vector2 dir = i % 2 == 0 ? dir0 : -dir0;
            if (proj != null)
                proj.Init(new Vector3(dir.x, 0f, dir.y), _damage, _speed, transform.position.y);
            _orbs.Add(orbGO);
        }
    }

    private void OnDestroy()
    {
        if (ObjectPool.Instance != null)
            foreach (var go in _orbs) if (go != null) ObjectPool.Instance.ReturnToPool("BouncingOrbProjectile", go);
    }
}
