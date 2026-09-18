using System.Collections.Generic;
using UnityEngine;

public class BoundaryDecorationPlacer : MonoBehaviour
{
    public enum DecorCategory { Tree, Rock }

    [System.Serializable]
    public class DecorEntry
    {
        public GameObject prefab;
        public DecorCategory category;
        public Vector3 rotationCorrection = new Vector3(-90f, 0f, 0f);
        public Vector3 positionCorrection = Vector3.zero;
    }

    [Header("Prefabs de décor")]
    [SerializeField] private DecorEntry[] _decorEntries;

    [Header("Zone")]
    [SerializeField] private float _zoneHalfSize = 60f;
    [SerializeField] private float _beltWidth = 30f; // élargi — plusieurs rangées de profondeur, plus une seule ligne
    [SerializeField] private int _decorCount = 400; // très augmenté pour un effet forêt dense

    [Header("Échelle par catégorie")]
    [SerializeField] private float _treeMinScale = 7.5f;
    [SerializeField] private float _treeMaxScale = 10f;
    [SerializeField] private float _rockMinScale = 4f;
    [SerializeField] private float _rockMaxScale = 7.5f;

    [Header("Ratio (poids de tirage)")]
    [Range(0f, 1f)]
    [SerializeField] private float _treeProbability = 0.75f; // 75% arbres, 25% roches

    [Header("Espacement")]
    [SerializeField] private float _minDistanceBetweenDecor = 4f;
    [SerializeField] private int _maxAttemptsPerPoint = 30;

    [ContextMenu("Générer la bordure de décor")]
    private void GenerateBorder()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        List<DecorEntry> trees = new List<DecorEntry>();
        List<DecorEntry> rocks = new List<DecorEntry>();
        foreach (DecorEntry entry in _decorEntries)
        {
            if (entry.category == DecorCategory.Tree) trees.Add(entry);
            else rocks.Add(entry);
        }

        List<Vector3> placedPositions = new List<Vector3>();
        int placedCount = 0;

        for (int i = 0; i < _decorCount; i++)
        {
            Vector3 position = Vector3.zero;
            bool foundValidSpot = false;

            for (int attempt = 0; attempt < _maxAttemptsPerPoint; attempt++)
            {
                Vector3 candidate = GetRandomBeltPosition();
                if (IsFarEnoughFromOthers(candidate, placedPositions))
                {
                    position = candidate;
                    foundValidSpot = true;
                    break;
                }
            }

            if (!foundValidSpot) continue;

            placedPositions.Add(position);
            placedCount++;

            bool pickTree = Random.value < _treeProbability;
            List<DecorEntry> pool = pickTree ? trees : rocks;
            if (pool.Count == 0) pool = pickTree ? rocks : trees; // sécurité si une catégorie est vide

            DecorEntry entry = pool[Random.Range(0, pool.Count)];

            GameObject instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(entry.prefab, transform);
            instance.transform.position = position + entry.positionCorrection;

            float randomY = Random.Range(0f, 360f);
            instance.transform.rotation = Quaternion.Euler(0f, randomY, 0f) * Quaternion.Euler(entry.rotationCorrection);

            float minScale = entry.category == DecorCategory.Tree ? _treeMinScale : _rockMinScale;
            float maxScale = entry.category == DecorCategory.Tree ? _treeMaxScale : _rockMaxScale;
            instance.transform.localScale = Vector3.one * Random.Range(minScale, maxScale);
        }

        Debug.Log($"Décor généré : {placedCount} / {_decorCount} éléments placés.");
    }

    private bool IsFarEnoughFromOthers(Vector3 candidate, List<Vector3> placedPositions)
    {
        foreach (Vector3 existing in placedPositions)
        {
            if (Vector3.Distance(candidate, existing) < _minDistanceBetweenDecor)
                return false;
        }
        return true;
    }

    private Vector3 GetRandomBeltPosition()
    {
        int side = Random.Range(0, 4);
        float alongEdge = Random.Range(-_zoneHalfSize, _zoneHalfSize);
        float depthInBelt = Random.Range(_zoneHalfSize, _zoneHalfSize + _beltWidth);

        switch (side)
        {
            case 0: return new Vector3(alongEdge, 0f, depthInBelt);
            case 1: return new Vector3(alongEdge, 0f, -depthInBelt);
            case 2: return new Vector3(depthInBelt, 0f, alongEdge);
            default: return new Vector3(-depthInBelt, 0f, alongEdge);
        }
    }
}