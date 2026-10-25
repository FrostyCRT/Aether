using UnityEngine;
using System.Collections.Generic;

// FUSION (2026-10-18) : Orbe rebondissant + Boue, toutes les deux au palier max → cette arme les remplace [universelle].
// Remplace la fusion "Orbes Jumeaux" (Double Tir + Orbe rebondissant), supprimée à la demande de l'utilisateur
// ("je la trouve très incohérente et inutile" - le lien entre le tir de base dédoublé et l'orbe rebondissant était
// purement cosmétique, Double Tir n'a jamais eu vocation à fusionner). "Sillage Boueux" relie ses 2 armes sources
// par un VRAI mécanisme, pas juste un visuel : chaque orbe qui continue de rebondir dans toute l'arène (même
// comportement que WeaponBouncingOrb, même prefab poolé "BouncingOrbProjectile") laisse désormais derrière lui un
// sillage de flaques de boue (même prefab poolé "MudPuddleZone" que WeaponMudPuddle) qui ralentissent et blessent
// tout ce qu'elles touchent. Contrairement à Terre Calcinée/Marécage Maudit (anneau de flaques à distance FIXE du
// joueur, qui a nécessité un correctif de portée - voir leurs commentaires), ce sillage suit la position RÉELLE et
// mouvante de chaque orbe : aucun risque structurel de "zone qui ne croise jamais les ennemis", puisque les orbes
// parcourent déjà toute la zone visible par la caméra en permanence.
public class WeaponFusionMudOrb : MonoBehaviour
{
    [Header("Orbes (hérités de l'Orbe rebondissant)")]
    [SerializeField] private int _orbCount = 2;

    [Header("Sillage de boue (hérité de la Boue)")]
    // MODIFIÉ (2026-10-18, retour utilisateur en jeu : "la cadence de l'apparition des flaques de boue est bien trop
    // lente, comparé à 6 qui sont en permanence autour du joueur, là il y en 2 qui spawnent avec un long intervalle
    // [...] ça doit vraiment faire comme une trainée, avec un délai entre chaque boue de environ moins d'1 sec")
    // - 1.3s -> 0.45s une première fois (trop lent, grands trous entre chaque dépôt), puis réglé à 0.7s (retour
    // utilisateur explicite : "je veux que tu mette 0,7 au lieu de 0,45"). Combiné à _puddleDuration=5s, ça maintient
    // en permanence ~7 flaques actives par orbe (5/0.7) au lieu des 2 instantanées d'avant avec un grand silence
    // entre chaque dépôt - largement au-dessus des 6 flaques max de la Boue seule, condition "plus puissant que les
    // 2 sous-upgrades maxées" remplie (dégâts cumulés par chevauchement, voir MudPuddleZone.Tick qui applique les
    // dégâts de CHAQUE zone indépendamment à un ennemi qui se trouve dans plusieurs flaques superposées).
    [SerializeField] private float _trailDropInterval = 0.7f;
    [SerializeField] private float _puddleDuration = 5f;
    [SerializeField] private float _puddleRadius = 1.8f;
    [SerializeField] private float _slowMultiplier = 0.6f;
    [SerializeField] private float _groundY = 0.2f;
    // Teinte "boue limoneuse" distincte de la Boue normale (ambre), de Terre Calcinée (braises) et de Marécage
    // Maudit (vert maladif) - bleu-gris délavé, cohérent avec l'azur de l'Orbe rebondissant (ColorBouncingOrb) qui
    // "entraîne" cette boue derrière lui.
    [SerializeField] private Color _sludgeTint = new Color(0.25f, 0.5f, 0.55f);

    private float _orbDamage;
    private float _orbSpeed;
    private float _puddleDamagePerSecond;
    private GameObject _puddlePrefab;
    private float _trailTimer = 0f;

    private readonly List<GameObject> _orbs = new List<GameObject>();
    private readonly List<GameObject> _trailPuddles = new List<GameObject>();

    // Appelé une fois par UpgradeData.ApplyFusionResult, juste après AddComponent. inheritedOrbDamage/inheritedSpeed/
    // inheritedOrbCount viennent de WeaponBouncingOrb (déjà boostés par la Réputation/les picks précédents) ;
    // inheritedPuddleDamage vient de WeaponMudPuddle, même principe.
    public void Init(GameObject orbPrefab, GameObject puddlePrefab, float inheritedOrbDamage, float inheritedSpeed, int inheritedOrbCount, float inheritedPuddleDamage)
    {
        _puddlePrefab = puddlePrefab;

        // EN ATTENTE DE VALIDATION EN JEU (2026-10-18) - Unity MCP déconnecté au moment de la création de cette
        // fusion, impossible de mesurer le DPS réel avec la méthode [[balance-testing-dummies]] comme les 5 autres
        // fusions de cet audit. Multiplicateurs choisis par analogie : ×2,5 sur les dégâts de contact de l'orbe
        // (mécanisme fiable, garanti, comme l'orbital des autres fusions - justifie un bonus franc) et ×1,5 sur le
        // sillage de boue (mécanisme de zone mais PAS un anneau fixe, donc moins de risque de sur-performance
        // explosive que Terre Calcinée/Marécage Maudit avant leur correctif de portée - pas besoin d'un
        // multiplicateur aussi prudent que leur ×0,17 final). À REMESURER et retendre dès que possible, exactement
        // comme les 5 autres fusions de cet audit.
        _orbDamage = Mathf.Max(1f, inheritedOrbDamage) * 2.5f;
        _puddleDamagePerSecond = Mathf.Max(1f, inheritedPuddleDamage) * 1.5f;
        _orbSpeed = inheritedSpeed;
        _orbCount = Mathf.Max(_orbCount, inheritedOrbCount);

        SpawnOrbs(orbPrefab);
    }

    public void AddDamage(float value)
    {
        _orbDamage += _orbDamage * value;
        _puddleDamagePerSecond += _puddleDamagePerSecond * value;
        for (int i = 0; i < _orbs.Count; i++)
        {
            if (_orbs[i] == null) continue;
            _orbs[i].TryGetComponent(out BouncingOrbProjectile proj);
            if (proj != null) proj.SetStats(_orbDamage, _orbSpeed);
        }
    }

    private void SpawnOrbs(GameObject orbPrefab)
    {
        if (ObjectPool.Instance == null || orbPrefab == null) return;
        for (int i = 0; i < _orbCount; i++)
        {
            GameObject orbGO = ObjectPool.Instance.Get("BouncingOrbProjectile", transform.position, Quaternion.identity);
            if (orbGO == null) continue;
            orbGO.TryGetComponent(out BouncingOrbProjectile proj);
            if (proj != null)
            {
                Vector2 randomDir = Random.insideUnitCircle.normalized;
                if (randomDir.sqrMagnitude < 0.01f) randomDir = Vector2.right;
                proj.Init(new Vector3(randomDir.x, 0f, randomDir.y), _orbDamage, _orbSpeed, transform.position.y);
            }
            _orbs.Add(orbGO);
        }
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        // Purge les flaques déjà rendues au pool par leur propre timer (MudPuddleZone.ReturnToPool) - jamais
        // forcées ici, seulement constatées, pour ne jamais raccourcir la durée de vie naturelle d'une flaque.
        _trailPuddles.RemoveAll(go => go == null || !go.activeInHierarchy);

        _trailTimer += Time.deltaTime;
        if (_trailTimer >= _trailDropInterval)
        {
            _trailTimer = 0f;
            DropTrailPuddles();
        }
    }

    private void DropTrailPuddles()
    {
        if (ObjectPool.Instance == null || _puddlePrefab == null) return;
        for (int i = 0; i < _orbs.Count; i++)
        {
            if (_orbs[i] == null) continue;

            Vector3 spawnPos = _orbs[i].transform.position;
            spawnPos.y = _groundY;

            GameObject puddleGO = ObjectPool.Instance.Get("MudPuddleZone", spawnPos, Quaternion.identity);
            if (puddleGO == null) continue;

            puddleGO.TryGetComponent(out MudPuddleZone zone);
            if (zone != null)
            {
                zone.Init(_puddleDuration, _puddleRadius, _slowMultiplier, _puddleDamagePerSecond);
                zone.SetColorTint(_sludgeTint);
            }

            _trailPuddles.Add(puddleGO);
        }
    }

    // AJOUTÉ - même correctif que WeaponBouncingOrb/WeaponOrbital/WeaponAura (voir leurs OnDestroy) : sans ça, les
    // orbes et les flaques de sillage encore actifs au moment où cette fusion disparaît (remplacée par une autre,
    // run terminé en cours d'effet) resteraient orphelins dans l'arène indéfiniment.
    private void OnDestroy()
    {
        if (ObjectPool.Instance == null) return;
        foreach (GameObject orb in _orbs)
            if (orb != null) ObjectPool.Instance.ReturnToPool("BouncingOrbProjectile", orb);
        foreach (GameObject puddle in _trailPuddles)
        {
            if (puddle == null) continue;
            puddle.TryGetComponent(out MudPuddleZone zone);
            if (zone != null) zone.ForceExpire();
        }
    }
}
