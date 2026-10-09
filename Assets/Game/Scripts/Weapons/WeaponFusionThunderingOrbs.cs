using UnityEngine;
using System.Collections.Generic;

// FUSION (2026-09-27) : Orbitaux + Foudre, toutes les deux au palier max → cette arme les remplace [universelle].
// Les orbes tournent exactement comme WeaponOrbital (même prefab poolé "OrbitalProjectile", dégâts de contact
// normaux), mais chaque orbe vérifie aussi, à intervalle régulier, s'il touche un ennemi : si oui, il déclenche une
// chaîne de foudre à partir de là (voir LightningChainUtility, algorithme repris de WeaponLightningChain).
public class WeaponFusionThunderingOrbs : MonoBehaviour
{
    // CORRIGÉ (2026-10-05) - même bug que WeaponFusionManaVortex : _orbitalCount fixé à 3 en dur, alors
    // qu'Orbitaux maxé seul atteint 5 - mesuré en conditions réalistes (ennemis dispersés/mobiles) à ×0,81,
    // plus FAIBLE que les 2 cartes maxées. Valeur par défaut seulement (plancher) ; Init() hérite désormais du
    // VRAI nombre d'orbitaux (voir WeaponOrbital.OrbitalCount).
    [Header("Orbite (héritée des Orbitaux)")]
    [SerializeField] private float _orbitRadius = 3.5f;
    [SerializeField] private float _orbitSpeed = 200f;
    [SerializeField] private int _orbitalCount = 5;

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
    [Header("Décharge de foudre (héritée de la Foudre, au contact d'un orbe)")]
    [SerializeField] private float _shockInterval = 3f;     // délai minimum entre 2 décharges d'un MÊME orbe
    [SerializeField] private int _maxChains = 3;
    [SerializeField] private float _chainRange = 5f;

    // AJOUTÉ (2026-10-05) - retour utilisateur : "la Foudre va sur l'ennemi le plus proche instantanément, alors
    // que si elle est fusionnée [...] on est obligé d'attendre qu'un ennemi soit touché [...] c'est comme un nerf
    // de la Foudre". Vrai : la Foudre seule (WeaponLightningChain) cherche ACTIVEMENT sa cible à chaque cooldown,
    // peu importe où elle se trouve - la fusion ne faisait que RÉAGIR au contact d'un orbe, un comportement
    // strictement moins fiable. Corrigé en ajoutant une frappe active et autonome, EN PLUS du proc au contact
    // (jamais à la place) : cherche la cible la plus proche sur son propre cooldown, exactement comme la Foudre
    // seule - l'identité "frappe instantanée fiable" de la Foudre est donc préservée intégralement dans la fusion.
    [Header("Décharge de foudre ACTIVE (comportement de la Foudre seule, préservé tel quel)")]
    [SerializeField] private float _activeStrikeInterval = 1f;
    [SerializeField] private float _activeDetectionRange = 15f;
    private float _activeStrikeTimer = 0f;
    private static readonly Collider[] _detectionBuffer = new Collider[64];

    private float _orbitalDamage;
    private float _lightningDamage;

    private readonly List<GameObject> _orbitals = new List<GameObject>();
    private readonly List<OrbitalProjectile> _orbitalScripts = new List<OrbitalProjectile>();
    private readonly List<float> _nextShockTime = new List<float>();
    private float _currentAngle = 0f;
    private static readonly Collider[] _overlapBuffer = new Collider[16];

    public void Init(GameObject orbitalPrefab, float inheritedOrbitalDamage, float inheritedLightningDamage, int inheritedOrbitalCount)
    {
        _orbitalDamage = inheritedOrbitalDamage * 1.8f;
        // Multiplicateur plus bas que l'orbital : la frappe active (ajoutée ci-dessus) tire déjà bien plus souvent
        // que l'ancien proc au contact seul, donc moins de boost par coup nécessaire pour un DPS global équivalent.
        _lightningDamage = inheritedLightningDamage * 1.1f;
        _orbitalCount = Mathf.Max(_orbitalCount, inheritedOrbitalCount);
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

    // Même logique que WeaponLightningChain.FindNearestEnemy - la frappe active cherche sa cible dans toute la
    // zone de détection, pas seulement au contact d'un orbe (voir note ci-dessus).
    private Transform FindNearestEnemy()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, _activeDetectionRange, _detectionBuffer);
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

    private void OnDestroy()
    {
        if (ObjectPool.Instance != null)
            foreach (var go in _orbitals) if (go != null) ObjectPool.Instance.ReturnToPool("OrbitalProjectile", go);
    }
}
