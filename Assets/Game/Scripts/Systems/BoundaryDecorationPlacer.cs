using System.Collections.Generic;
using UnityEngine;

// MODIFIÉ (2026-10-19, diagnostic complet - refonte de la forme de carte par palier) - l'ancien modèle ("ceinture"
// de décor juste à l'extérieur d'une zone carrée) ne correspond plus à aucun des 3 paliers : Modéré n'a plus AUCUN
// décor (retour utilisateur : ce sont de vrais modèles 3D, bizarre à longer dans un monde censé être infini - voir
// DifficultyTiers.cs), Violent est un tunnel infini sur X (décor généré dynamiquement par zones, voir
// Systems/InfiniteDecorStreamer.cs, pas ce script), et Apocalyptique est une boîte FERMÉE sur ses 4 côtés où le
// décor forme une bande STRICTEMENT EXTÉRIEURE aux murs (voir PlaceDecorationsOutsideBox - retour utilisateur
// 2026-10-19 : le joueur doit pouvoir circuler librement partout à l'intérieur), coins compris, en profondeur,
// dense mais sans superposition.
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

    // MODIFIÉ (2026-10-19, diagnostic complet - test en Play) - l'échelle 7,5-10 (arbres) héritée de l'ancienne
    // "ceinture" (décor posé hors de la zone jouable, jamais vu de près) produit des couronnes qui se chevauchent
    // visuellement dans la boîte Apocalyptique dès que 2 troncs sont à la distance minimale (4, qui ne mesurait que
    // l'écart entre PIVOTS, pas entre couronnes réelles) - le joueur disparaissait entièrement sous le feuillage à
    // l'écran. Réduit et l'espacement augmenté en conséquence pour que la boîte reste dense mais lisible.
    [Header("Échelle par catégorie")]
    [SerializeField] private float _treeMinScale = 3.5f;
    [SerializeField] private float _treeMaxScale = 5f;
    [SerializeField] private float _rockMinScale = 2f;
    [SerializeField] private float _rockMaxScale = 3.5f;

    [Header("Ratio (poids de tirage)")]
    [Range(0f, 1f)]
    [SerializeField] private float _treeProbability = 0.75f;

    [Header("Espacement")]
    [SerializeField] private float _minDistanceBetweenDecor = 9f;
    [SerializeField] private int _maxAttemptsPerPoint = 30;

    // AJOUTÉ (2026-10-19, session esthétique, retour utilisateur : "tu as mis les décors à l'intérieur du jeu,
    // alors qu'ils doivent être strictement à l'extérieur des limites") - profondeur de la bande EXTÉRIEURE à la
    // boîte où le décor peut être posé (voir PlaceDecorationsOutsideBox ci-dessous).
    [SerializeField] private float _beltWidth = 18f;

    // Modéré n'a plus de décor du tout (les anciens enfants posés à la main ont été retirés de la scène). Violent
    // est géré par InfiniteDecorStreamer. Seul Apocalyptique utilise ce script, pour remplir sa boîte fermée.
    // ÉTENDU (2026-10-20, carrousel de difficulté) - le palier de difficulté (et sa carte) s'appliquant maintenant
    // aussi en Sans fin et Ruée de boss (voir MapBoundaryUtils.ResetForNewRun), le décor qui marque visuellement
    // les murs doit suivre - sans ça, un palier Apocalyptique en Sans fin aurait des murs invisibles sans aucune
    // bande de décor pour les signaler.
    private void Start()
    {
        if (!(GameModes.IsClassic || GameModes.IsEndless || GameModes.IsBossRush)) return;
        if (DifficultyTiers.Current != DifficultyTier.Apocalyptic) return;

        DifficultyTiers.GetBounds(DifficultyTier.Apocalyptic, out float minX, out float maxX, out float minZ, out float maxZ);
        PlaceDecorationsOutsideBox(minX, maxX, minZ, maxZ, DifficultyTiers.ApocalypticDecorCount);
    }

    // CORRIGÉ (2026-10-19, session esthétique, retour utilisateur : "tu as mis les décors à l'intérieur du jeu,
    // alors qu'ils doivent être strictement à l'extérieur des limites") - remplissait TOUTE la boîte (coins
    // compris) de décor ; le joueur doit pouvoir se déplacer librement partout à l'intérieur des 4 murs. Les
    // candidats sont désormais tirés dans une boîte EXTÉRIEURE élargie de _beltWidth sur chaque côté, puis tout
    // point qui tombe encore À L'INTÉRIEUR de la boîte jouable est rejeté - ce qui, par construction, couvre aussi
    // les 4 coins (un point hors de la boîte jouable mais dans le coin de la boîte élargie passe le test) sans
    // aucun cas particulier. decorCount est un PLAFOND : le nombre réellement posé dépend de ce qui tient sans
    // violer _minDistanceBetweenDecor (voir le journal en fin de méthode).
    private void PlaceDecorationsOutsideBox(float minX, float maxX, float minZ, float maxZ, int decorCount)
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }

        List<DecorEntry> trees = new List<DecorEntry>();
        List<DecorEntry> rocks = new List<DecorEntry>();
        foreach (DecorEntry entry in _decorEntries)
        {
            if (entry.category == DecorCategory.Tree) trees.Add(entry);
            else rocks.Add(entry);
        }

        float outerMinX = minX - _beltWidth;
        float outerMaxX = maxX + _beltWidth;
        float outerMinZ = minZ - _beltWidth;
        float outerMaxZ = maxZ + _beltWidth;

        List<Vector3> placedPositions = new List<Vector3>();
        int placedCount = 0;

        for (int i = 0; i < decorCount; i++)
        {
            Vector3 position = Vector3.zero;
            bool foundValidSpot = false;

            for (int attempt = 0; attempt < _maxAttemptsPerPoint; attempt++)
            {
                float x = Random.Range(outerMinX, outerMaxX);
                float z = Random.Range(outerMinZ, outerMaxZ);
                bool insidePlayableBox = x > minX && x < maxX && z > minZ && z < maxZ;
                if (insidePlayableBox) continue; // strictement à l'extérieur des murs

                Vector3 candidate = new Vector3(x, 0f, z);
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
            if (pool.Count == 0) pool = pickTree ? rocks : trees;

            DecorEntry entry = pool[Random.Range(0, pool.Count)];

            GameObject instance;
#if UNITY_EDITOR
            instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(entry.prefab, transform);
#else
            instance = Instantiate(entry.prefab, transform);
#endif
            instance.transform.position = position + entry.positionCorrection;

            float randomY = Random.Range(0f, 360f);
            instance.transform.rotation = Quaternion.Euler(0f, randomY, 0f) * Quaternion.Euler(entry.rotationCorrection);

            float minScale = entry.category == DecorCategory.Tree ? _treeMinScale : _rockMinScale;
            float maxScale = entry.category == DecorCategory.Tree ? _treeMaxScale : _rockMaxScale;
            instance.transform.localScale = Vector3.one * Random.Range(minScale, maxScale);
        }

        Debug.Log($"Décor (boîte Apocalyptique) généré : {placedCount} / {decorCount} éléments placés.");
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
}
