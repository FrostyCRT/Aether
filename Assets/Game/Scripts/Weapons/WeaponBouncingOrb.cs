using UnityEngine;
using System.Collections.Generic;
// Créé au premier pick de la carte Orbe Rebondissant (contrairement à Orbital/Lightning,
// pas de pick de déblocage séparé — le palier 1 crée l'arme ET applique son effet dégâts
// en même temps, voir UpgradeData.Apply()).
public class WeaponBouncingOrb : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _damage = 120f; // MODIFIE - x10, cf. rescale global des degats/PV
    [SerializeField] private float _speed = 6f;
    [SerializeField] private int _orbCount = 1;
    [Header("Références")]
    [SerializeField] private GameObject _orbPrefab;
    private readonly List<GameObject> _orbs = new List<GameObject>();

    // AJOUTE - meme trou que les autres armes exclusives/universelles : le bonus
    // de Reputation Degats n'etait jamais applique. Tourne avant Init() (Awake()
    // s'execute de facon synchrone des l'AddComponent, avant que UpgradeData.Apply()
    // n'appelle Init() juste apres sur la meme ligne de code).
    private void Awake()
    {
        if (MetaProgressionManager.Instance != null)
        {
            float bonusDamage = MetaProgressionManager.Instance.GetReputationBonusDamage();
            _damage += _damage * bonusDamage;
        }
    }

    public void Init(GameObject orbPrefab)
    {
        _orbPrefab = orbPrefab;
        SpawnOrbs();
    }
    public void AddDamage(float value)
    {
        _damage += _damage * value;
        PushStatsToActiveOrbs();
    }

    // Lu par UpgradeData.ApplyFusionResult() pour hériter des dégâts déjà accumulés au moment d'une fusion.
    public float CurrentDamage => _damage;

    public void AddSpeed(float value)
    {
        _speed += _speed * value;
        PushStatsToActiveOrbs();
    }
    public void AddOrb()
    {
        _orbCount++;
        SpawnOrbs();
    }

    // AJOUTE (2026-09-27, retour utilisateur : "si il y a toujours les 2 orbes rebondissant... ça ne va pas") -
    // même bug que WeaponOrbital/WeaponAura plus tôt cette session : aucun OnDestroy() ici, alors que
    // BouncingOrbProjectile (voir son commentaire d'en-tête) "ne se détruit ni ne retourne au pool TANT QUE L'ARME
    // EXISTE" - il compte entièrement sur ce composant pour le nettoyer. Quand une fusion (Lames Ricochet, Orbes
    // Jumeaux) détruit ce composant via Object.Destroy(), les orbes déjà lancés continuaient de rebondir et de
    // faire des dégâts indéfiniment, complètement détachés de l'arme fusionnée qui les remplace - exactement le
    // bug rapporté ("les 2 orbes rebondissant" encore présents après la fusion).
    private void OnDestroy()
    {
        if (ObjectPool.Instance == null) return;
        foreach (GameObject orb in _orbs)
            if (orb != null) ObjectPool.Instance.ReturnToPool("BouncingOrbProjectile", orb);
    }
    private void PushStatsToActiveOrbs()
    {
        for (int i = 0; i < _orbs.Count; i++)
        {
            if (_orbs[i] == null) continue;
            BouncingOrbProjectile proj = _orbs[i].GetComponent<BouncingOrbProjectile>();
            if (proj != null) proj.SetStats(_damage, _speed);
        }
    }
    private void SpawnOrbs()
    {
        if (ObjectPool.Instance == null || _orbPrefab == null)
        {
            Debug.LogWarning("[WeaponBouncingOrb] ObjectPool ou prefab manquant, impossible de spawner les orbes.");
            return;
        }
        while (_orbs.Count < _orbCount)
        {
            GameObject orbGO = ObjectPool.Instance.Get("BouncingOrbProjectile", transform.position, Quaternion.identity);
            if (orbGO == null) break;
            BouncingOrbProjectile proj = orbGO.GetComponent<BouncingOrbProjectile>();
            if (proj != null)
            {
                Vector2 randomDir = Random.insideUnitCircle.normalized;
                if (randomDir.sqrMagnitude < 0.01f) randomDir = Vector2.right;
                proj.Init(new Vector3(randomDir.x, 0f, randomDir.y), _damage, _speed, transform.position.y);
            }
            _orbs.Add(orbGO);
        }
    }
}