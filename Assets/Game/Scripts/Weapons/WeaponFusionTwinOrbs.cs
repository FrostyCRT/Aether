using UnityEngine;
using System.Collections.Generic;

// FUSION (2026-09-27) : Double tir + Orbe rebondissant, toutes les deux au palier max → cette arme les remplace
// [universelle]. Double tir n'a pas de composant propre (il ne fait que marquer WeaponBase, qui reste toujours en
// place — c'est l'attaque de base du personnage, jamais détruite) : seul l'Orbe rebondissant est réellement remplacé.
//
// REFONTE (2026-10-05) - retour utilisateur : "pourquoi le nom Orbes Jumeaux ? en quoi c'est jumeaux si il y en a
// 6 ?" (suite aux passes précédentes qui avaient gonflé le nombre d'orbes à 6 pour stabiliser la mesure, sans
// repenser le nom). Choix utilisateur explicite : "duo iconique" plutôt qu'un essaim renommé - retour à EXACTEMENT
// 2 orbes, mais bien plus gros/forts et visuellement distincts (liés par un éclat d'énergie), fidèle au nom. Le
// risque de variance (2 orbes à direction aléatoire peuvent rater toute cible dispersée - mesuré jusqu'à ×0,90,
// plus FAIBLE qu'avant, lors des passes à faible nombre d'orbes) est traité autrement qu'en ajoutant des orbes :
// chaque orbe vise maintenant ACTIVEMENT l'ennemi le plus proche à son lancement (les 2 orbes visent 2 cibles
// DISTINCTES quand c'est possible), au lieu d'une direction purement aléatoire comme l'Orbe rebondissant seul -
// supprime la racine du problème (la malchance du tirage) plutôt que de la diluer dans la quantité.
public class WeaponFusionTwinOrbs : MonoBehaviour
{
    [Header("Stats (héritées de l'Orbe rebondissant, boostées par le Double tir)")]
    [SerializeField] private float _speed = 7f;

    [Header("Duo iconique")]
    [Tooltip("Multiplicateur de taille visuelle/physique des 2 orbes par rapport à un Orbe rebondissant normal.")]
    // AJUSTÉ (2026-10-05) - 2,2 testé en jeu : les 2 orbes devenaient si gros qu'ils recouvraient le joueur et une
    // bonne partie de l'écran autour de lui - à l'encontre du "toujours voir ce qu'il se passe" demandé. Réduit à
    // 1,6 : toujours nettement plus gros qu'un Orbe rebondissant normal (reconnaissable comme le "duo iconique"),
    // sans dominer l'écran.
    [SerializeField] private float _orbScale = 1.6f;
    [SerializeField] private float _detectionRange = 15f;
    [SerializeField] private Color _bondColor = new Color(0.6f, 0.85f, 1f, 0.8f);
    [SerializeField] private float _bondWidth = 0.1f;

    private float _damage;
    private GameObject _orbPrefab;
    private readonly List<GameObject> _orbs = new List<GameObject>();
    private LineRenderer _bondRenderer;
    private static readonly Collider[] _detectionBuffer = new Collider[32];

    public void Init(GameObject orbPrefab, float inheritedDamage)
    {
        _orbPrefab = orbPrefab;
        // Seulement 2 orbes désormais (contre jusqu'à 6 dans les passes précédentes) : bonus par orbe remonté en
        // conséquence pour un DPS total équivalent/supérieur, assumé plus irrégulier d'un coup à l'autre (visée
        // active sur la cible la plus proche pour limiter ce risque, voir note de tête de fichier).
        _damage = inheritedDamage * 3.5f;
        SpawnOrbs();
        CreateBondVisual();
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

    // Les 2 orbes visent chacun une cible DISTINCTE parmi les plus proches (pas une direction aléatoire comme
    // l'Orbe rebondissant seul) - supprime le risque qu'un tirage malchanceux envoie les 2 orbes loin de tout
    // ennemi. Repli sur des directions opposées aléatoires si moins de 2 ennemis sont détectés (début de partie).
    private void SpawnOrbs()
    {
        if (ObjectPool.Instance == null || _orbPrefab == null) return;

        List<Vector3> targets = FindNearestEnemyPositions(2);

        Vector2 dir0;
        if (targets.Count >= 1)
        {
            Vector2 toTarget = new Vector2(targets[0].x - transform.position.x, targets[0].z - transform.position.z);
            dir0 = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : Random.insideUnitCircle.normalized;
        }
        else
        {
            dir0 = Random.insideUnitCircle.normalized;
        }
        if (dir0.sqrMagnitude < 0.01f) dir0 = Vector2.right;
        SpawnOrbInDirection(dir0);

        Vector2 dir1;
        if (targets.Count >= 2)
        {
            Vector2 toTarget = new Vector2(targets[1].x - transform.position.x, targets[1].z - transform.position.z);
            dir1 = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : -dir0;
        }
        else
        {
            dir1 = -dir0; // repli dos-à-dos classique si une seule (ou aucune) cible trouvée
        }
        SpawnOrbInDirection(dir1);
    }

    private List<Vector3> FindNearestEnemyPositions(int count)
    {
        var result = new List<Vector3>();
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _detectionRange, _detectionBuffer, EnemyLayers.Mask);
        var candidates = new List<(float distSqr, Vector3 pos)>();
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _detectionBuffer[i];
            if (col == null || !col.CompareTag("Enemy")) continue;
            float distSqr = (col.transform.position - transform.position).sqrMagnitude;
            candidates.Add((distSqr, col.transform.position));
        }
        candidates.Sort((a, b) => a.distSqr.CompareTo(b.distSqr));
        for (int i = 0; i < Mathf.Min(count, candidates.Count); i++)
            result.Add(candidates[i].pos);
        return result;
    }

    private void SpawnOrbInDirection(Vector2 dir)
    {
        GameObject orbGO = ObjectPool.Instance.Get("BouncingOrbProjectile", transform.position, Quaternion.identity);
        if (orbGO == null) return;
        orbGO.transform.localScale = Vector3.one * _orbScale;
        BouncingOrbProjectile proj = orbGO.GetComponent<BouncingOrbProjectile>();
        if (proj != null)
        {
            proj.Init(new Vector3(dir.x, 0f, dir.y), _damage, _speed, transform.position.y);
            proj.SetOrbRadius(proj.OrbRadius * _orbScale);
        }
        _orbs.Add(orbGO);
    }

    // Éclat d'énergie reliant visuellement les 2 orbes - le "duo" doit se reconnaître d'un coup d'œil, pas juste
    // être 2 orbes qui rebondissent par hasard côte à côte.
    private void CreateBondVisual()
    {
        GameObject bondGO = new GameObject("TwinOrbsBond");
        _bondRenderer = bondGO.AddComponent<LineRenderer>();
        _bondRenderer.useWorldSpace = true;
        _bondRenderer.positionCount = 2;
        _bondRenderer.widthMultiplier = _bondWidth;
        _bondRenderer.material = new Material(Shader.Find("Sprites/Default"));
        _bondRenderer.startColor = _bondColor;
        _bondRenderer.endColor = _bondColor;
        _bondRenderer.sortingOrder = 4;
    }

    private void Update()
    {
        if (_bondRenderer == null || _orbs.Count < 2) return;
        if (_orbs[0] == null || _orbs[1] == null) { _bondRenderer.enabled = false; return; }
        _bondRenderer.enabled = true;
        _bondRenderer.SetPosition(0, _orbs[0].transform.position);
        _bondRenderer.SetPosition(1, _orbs[1].transform.position);
    }

    private void OnDestroy()
    {
        if (ObjectPool.Instance != null)
            foreach (var go in _orbs) if (go != null) ObjectPool.Instance.ReturnToPool("BouncingOrbProjectile", go);
        if (_bondRenderer != null) Destroy(_bondRenderer.gameObject);
    }
}
