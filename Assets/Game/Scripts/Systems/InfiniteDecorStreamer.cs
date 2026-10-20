using System.Collections.Generic;
using UnityEngine;

// AJOUTÉ (2026-10-19, diagnostic complet - refonte de la forme de carte, palier Violent) - le tunnel du Violent est
// infini sur X (voir DifficultyTiers.GetBounds/MapBoundaryUtils) : impossible d'y poser du décor une fois pour
// toutes comme Apocalyptique (BoundaryDecorationPlacer). Ce script génère/retire le décor par "tronçons" (chunks)
// le long de X au fur et à mesure du déplacement du joueur, en 2 bandes qui suivent les murs invisibles du tunnel
// (Z = min/max du palier Violent), avec de la profondeur (bande d'une certaine largeur, pas une ligne).
//
// Perf (demande explicite : "techniques de dev pour optimiser les performances") :
//   - Seuls les tronçons à portée du joueur (±LoadRadiusChunks) existent ; les autres sont rendus au pool.
//   - Passe par l'ObjectPool partagé du projet (mêmes prefabs Tree_1/Tree_2/Rock 1/Rock 2 que BoundaryDecorationPlacer,
//     enregistrés sous des tags dédiés dans Game.unity) plutôt que Instantiate/Destroy à chaque tronçon chargé/déchargé.
//   - Le travail de (dé)chargement ne se déclenche QUE quand le joueur change de tronçon (un entier comparé chaque
//     frame, le reste ne tourne qu'au changement), pas à chaque frame.
//   - Chaque tronçon est généré avec une graine aléatoire déterministe (dérivée de son index) : revisiter un
//     tronçon déjà vu redonne EXACTEMENT le même décor, pas un nouveau tirage à chaque passage.
public class InfiniteDecorStreamer : MonoBehaviour
{
    [System.Serializable]
    public class StreamEntry
    {
        public string poolTag;
        public BoundaryDecorationPlacer.DecorCategory category;
        public Vector3 rotationCorrection = new Vector3(-90f, 0f, 0f);
        public Vector3 positionCorrection = Vector3.zero;
    }

    [Header("Prefabs de décor (tags ObjectPool, voir Game.unity)")]
    [SerializeField] private StreamEntry[] _streamEntries;

    // MODIFIÉ (2026-10-19, retour utilisateur : "augmente la densité, et qu'ils soient prolongés bien plus loin,
    // car c'est infini") - l'échelle 7,5-10 (arbres) héritée de l'ancienne "ceinture" (voir le même correctif déjà
    // fait sur BoundaryDecorationPlacer/Apocalyptique) ferait chevaucher les couronnes à la densité augmentée
    // ci-dessous ; réduite et l'espacement remonté en conséquence, AVANT d'augmenter la densité pour ne pas
    // reproduire le même bug (joueur invisible sous le feuillage).
    [Header("Échelle par catégorie")]
    [SerializeField] private float _treeMinScale = 3.5f;
    [SerializeField] private float _treeMaxScale = 5f;
    [SerializeField] private float _rockMinScale = 2f;
    [SerializeField] private float _rockMaxScale = 3.5f;

    [Header("Ratio (poids de tirage)")]
    [Range(0f, 1f)]
    [SerializeField] private float _treeProbability = 0.75f;

    [Header("Tronçons (chunks) le long de X")]
    [Tooltip("Largeur d'un tronçon en unités monde.")]
    [SerializeField] private float _chunkWidth = 50f;
    [Tooltip("Nombre de tronçons chargés de chaque côté du joueur (donc 2*N+1 tronçons actifs en permanence).")]
    [SerializeField] private int _loadRadiusChunks = 8;
    // AJOUTÉ (2026-10-19, retour utilisateur : "des fois on voit des modèles 3D se couper et revenir") - sans
    // marge, un joueur qui oscille d'un cheveu autour d'une frontière de tronçon (dash, recul au combat, simple
    // mouvement en biais) fait basculer _lastPlayerChunk dans les 2 sens à chaque frame, et RefreshChunks()
    // décharge/recharge (SetActive false/true via l'ObjectPool) le même tronçon en boucle - vu du joueur, ça
    // ressemble exactement à un décor qui "se coupe et revient". Un tronçon n'est maintenant déchargé que lorsqu'il
    // dépasse _loadRadiusChunks + cette marge (le chargement, lui, reste immédiat dès l'entrée dans le rayon).
    [Tooltip("Marge de tronçons en plus avant de décharger (hystérésis anti-scintillement sur une frontière).")]
    [SerializeField] private int _unloadHysteresisChunks = 2;
    [Tooltip("Profondeur de la bande de décor au-delà de chaque mur (Z).")]
    [SerializeField] private float _beltWidth = 20f;
    [Tooltip("Nombre de décors visés par bande ET par tronçon (plafond : l'espacement ci-dessous peut en placer moins).")]
    [SerializeField] private int _decorPerBandPerChunk = 24;
    [SerializeField] private float _minDistanceBetweenDecor = 7f;
    [SerializeField] private int _maxAttemptsPerPoint = 20;

    private struct PlacedDecor
    {
        public GameObject go;
        public string poolTag;
    }

    private readonly Dictionary<int, List<PlacedDecor>> _loadedChunks = new Dictionary<int, List<PlacedDecor>>();
    private readonly List<StreamEntry> _trees = new List<StreamEntry>();
    private readonly List<StreamEntry> _rocks = new List<StreamEntry>();

    private Transform _player;
    private int _lastPlayerChunk = int.MinValue;
    private float _minZ, _maxZ;
    private bool _active;

    private void Start()
    {
        // ÉTENDU (2026-10-20, carrousel de difficulté) - le palier Violent (et son tunnel) s'applique maintenant
        // aussi en Sans fin et Ruée de boss, voir MapBoundaryUtils.ResetForNewRun.
        _active = (GameModes.IsClassic || GameModes.IsEndless || GameModes.IsBossRush) && DifficultyTiers.Current == DifficultyTier.Violent;
        if (!_active) return;

        DifficultyTiers.GetBounds(DifficultyTier.Violent, out _, out _, out _minZ, out _maxZ);

        foreach (StreamEntry entry in _streamEntries)
        {
            if (entry.category == BoundaryDecorationPlacer.DecorCategory.Tree) _trees.Add(entry);
            else _rocks.Add(entry);
        }

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null) _player = player.transform;
    }

    private void Update()
    {
        if (!_active || _player == null || ObjectPool.Instance == null) return;

        int currentChunk = Mathf.FloorToInt(_player.position.x / _chunkWidth);
        if (currentChunk == _lastPlayerChunk) return;
        _lastPlayerChunk = currentChunk;

        RefreshChunks(currentChunk);
    }

    private void RefreshChunks(int centerChunk)
    {
        HashSet<int> wanted = new HashSet<int>();
        for (int c = centerChunk - _loadRadiusChunks; c <= centerChunk + _loadRadiusChunks; c++)
            wanted.Add(c);

        int unloadRadius = _loadRadiusChunks + _unloadHysteresisChunks;
        List<int> toUnload = null;
        foreach (int loaded in _loadedChunks.Keys)
        {
            if (Mathf.Abs(loaded - centerChunk) > unloadRadius)
            {
                toUnload ??= new List<int>();
                toUnload.Add(loaded);
            }
        }
        if (toUnload != null)
            foreach (int c in toUnload) UnloadChunk(c);

        foreach (int c in wanted)
            if (!_loadedChunks.ContainsKey(c)) LoadChunk(c);
    }

    private void LoadChunk(int chunkIndex)
    {
        // Graine déterministe par tronçon (2 grands nombres premiers pour éviter tout motif visible entre
        // tronçons voisins) - sauvegarde/restaure Random.state pour ne pas perturber les autres systèmes du jeu
        // qui partagent le même flux aléatoire global (ennemis, loot...).
        Random.State savedState = Random.state;
        Random.InitState(chunkIndex * 7919 + 104729);

        float chunkMinX = chunkIndex * _chunkWidth;
        float chunkMaxX = chunkMinX + _chunkWidth;

        List<PlacedDecor> placed = new List<PlacedDecor>();
        PlaceBand(chunkMinX, chunkMaxX, topSide: true, placed);
        PlaceBand(chunkMinX, chunkMaxX, topSide: false, placed);
        _loadedChunks[chunkIndex] = placed;

        Random.state = savedState;
    }

    private void PlaceBand(float chunkMinX, float chunkMaxX, bool topSide, List<PlacedDecor> placed)
    {
        float wallZ = topSide ? _maxZ : _minZ;
        float beltSign = topSide ? 1f : -1f;

        for (int i = 0; i < _decorPerBandPerChunk; i++)
        {
            Vector3 candidate = Vector3.zero;
            bool found = false;

            for (int attempt = 0; attempt < _maxAttemptsPerPoint; attempt++)
            {
                float x = Random.Range(chunkMinX, chunkMaxX);
                float depth = Random.Range(0f, _beltWidth);
                candidate = new Vector3(x, 0f, wallZ + beltSign * depth);

                if (IsFarEnough(candidate, placed)) { found = true; break; }
            }
            if (!found) continue;

            StreamEntry entry = PickEntry();
            if (entry == null || string.IsNullOrEmpty(entry.poolTag)) continue;

            GameObject go = ObjectPool.Instance.Get(entry.poolTag, candidate + entry.positionCorrection, Quaternion.identity);
            if (go == null) continue;

            float randomY = Random.Range(0f, 360f);
            go.transform.rotation = Quaternion.Euler(0f, randomY, 0f) * Quaternion.Euler(entry.rotationCorrection);

            float minScale = entry.category == BoundaryDecorationPlacer.DecorCategory.Tree ? _treeMinScale : _rockMinScale;
            float maxScale = entry.category == BoundaryDecorationPlacer.DecorCategory.Tree ? _treeMaxScale : _rockMaxScale;
            go.transform.localScale = Vector3.one * Random.Range(minScale, maxScale);

            placed.Add(new PlacedDecor { go = go, poolTag = entry.poolTag });
        }
    }

    private StreamEntry PickEntry()
    {
        bool pickTree = Random.value < _treeProbability;
        List<StreamEntry> pool = pickTree ? _trees : _rocks;
        if (pool.Count == 0) pool = pickTree ? _rocks : _trees;
        if (pool.Count == 0) return null;
        return pool[Random.Range(0, pool.Count)];
    }

    private bool IsFarEnough(Vector3 candidate, List<PlacedDecor> placed)
    {
        foreach (PlacedDecor d in placed)
        {
            if (d.go == null) continue;
            if (Vector3.Distance(candidate, d.go.transform.position) < _minDistanceBetweenDecor)
                return false;
        }
        return true;
    }

    private void UnloadChunk(int chunkIndex)
    {
        if (!_loadedChunks.TryGetValue(chunkIndex, out List<PlacedDecor> placed)) return;

        foreach (PlacedDecor d in placed)
            if (d.go != null) ObjectPool.Instance.ReturnToPool(d.poolTag, d.go);

        _loadedChunks.Remove(chunkIndex);
    }
}
