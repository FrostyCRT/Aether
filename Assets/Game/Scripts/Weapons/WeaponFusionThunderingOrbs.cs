using UnityEngine;
using System.Collections.Generic;

// FUSION (2026-09-27) : Orbitaux + Foudre, toutes les deux au palier max → cette arme les remplace [universelle].
// Les orbes tournent exactement comme WeaponOrbital (même prefab poolé "OrbitalProjectile", dégâts de contact
// normaux), mais chaque orbe vérifie aussi, à intervalle régulier, s'il touche un ennemi : si oui, il déclenche une
// chaîne de foudre à partir de là (voir LightningChainUtility, algorithme repris de WeaponLightningChain).
public class WeaponFusionThunderingOrbs : MonoBehaviour
{
    [Header("Orbite (héritée des Orbitaux)")]
    [SerializeField] private float _orbitRadius = 3.5f;
    [SerializeField] private float _orbitSpeed = 200f;
    [SerializeField] private int _orbitalCount = 3;

    // AJOUTE (2026-09-27) - meme correctif que WeaponFusionManaVortex : le controle A/E de WeaponOrbital n'avait
    // jamais ete porte ici (retour utilisateur).
    [Header("Contrôle Range (A/E), hérité des Orbitaux")]
    [SerializeField] private float _minOrbitRadius = 1f;
    [SerializeField] private float _maxOrbitRadius = 9f;
    [SerializeField] private float _rangeChangeSpeed = 2f;

    // MODIFIE (2026-09-27) - 0.4 -> 1.6 : mesuré en jeu (12 cibles groupées, cf. NOTES.md) à 17,6x les dégâts des
    // 2 cartes maxées séparément - 3 orbes déclenchant chacun leur propre chaîne toutes les 0.4s cumulaient une
    // fréquence de décharge bien plus haute que prévu. Retendu pour retomber dans la même fourchette "nettement plus
    // fort, pas explosif" que les 5 autres fusions (2x à 3,3x mesurés).
    [Header("Décharge de foudre (héritée de la Foudre)")]
    [SerializeField] private float _shockInterval = 3f;     // délai minimum entre 2 décharges d'un MÊME orbe
    [SerializeField] private int _maxChains = 3;
    [SerializeField] private float _chainRange = 5f;

    private float _orbitalDamage;
    private float _lightningDamage;

    private readonly List<GameObject> _orbitals = new List<GameObject>();
    private readonly List<OrbitalProjectile> _orbitalScripts = new List<OrbitalProjectile>();
    private readonly List<float> _nextShockTime = new List<float>();
    private float _currentAngle = 0f;
    private static readonly Collider[] _overlapBuffer = new Collider[16];

    public void Init(GameObject orbitalPrefab, float inheritedOrbitalDamage, float inheritedLightningDamage)
    {
        _orbitalDamage = inheritedOrbitalDamage;
        _lightningDamage = inheritedLightningDamage;
        if (ObjectPool.Instance == null || orbitalPrefab == null) return;
        for (int i = 0; i < _orbitalCount; i++)
        {
            GameObject go = ObjectPool.Instance.Get("OrbitalProjectile", transform.position, Quaternion.identity);
            if (go == null) continue;
            go.transform.SetParent(transform);
            _orbitals.Add(go);
            _nextShockTime.Add(0f);
            OrbitalProjectile proj = go.GetComponent<OrbitalProjectile>();
            if (proj != null) { proj.SetDamage(_orbitalDamage); _orbitalScripts.Add(proj); }
        }
    }

    public void AddDamage(float value)
    {
        _orbitalDamage += _orbitalDamage * value;
        _lightningDamage += _lightningDamage * value;
        foreach (var s in _orbitalScripts) if (s != null) s.SetDamage(_orbitalDamage);
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;
        if (_orbitals.Count == 0) return;

        if (GameInput.Held(GameAction.OrbitShrink))
            _orbitRadius = Mathf.Max(_minOrbitRadius, _orbitRadius - _rangeChangeSpeed * Time.deltaTime);
        if (GameInput.Held(GameAction.OrbitGrow))
            _orbitRadius = Mathf.Min(_maxOrbitRadius, _orbitRadius + _rangeChangeSpeed * Time.deltaTime);

        _currentAngle += _orbitSpeed * Time.deltaTime;
        float angleStep = 360f / _orbitals.Count;

        for (int i = 0; i < _orbitals.Count; i++)
        {
            if (_orbitals[i] == null) continue;
            float angle = (_currentAngle + angleStep * i) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _orbitRadius;
            _orbitals[i].transform.position = transform.position + offset;

            if (Time.time < _nextShockTime[i]) continue;
            Transform enemyHere = FindEnemyTouching(_orbitals[i].transform.position);
            if (enemyHere == null) continue;

            _nextShockTime[i] = Time.time + _shockInterval;
            LightningChainUtility.Trigger(this, enemyHere, _lightningDamage, _maxChains, _chainRange);
        }
    }

    // Rayon de détection = même rayon que le collider du prefab OrbitalProjectile (petit, contact direct) : on ne
    // veut déclencher la foudre que quand un orbe TOUCHE réellement un ennemi, pas juste "à proximité".
    [SerializeField] private float _contactCheckRadius = 0.8f;
    private Transform FindEnemyTouching(Vector3 position)
    {
        int count = Physics.OverlapSphereNonAlloc(position, _contactCheckRadius, _overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider hit = _overlapBuffer[i];
            if (hit != null && hit.CompareTag("Enemy")) return hit.transform;
        }
        return null;
    }

    private void OnDestroy()
    {
        if (ObjectPool.Instance != null)
            foreach (var go in _orbitals) if (go != null) ObjectPool.Instance.ReturnToPool("OrbitalProjectile", go);
    }
}
