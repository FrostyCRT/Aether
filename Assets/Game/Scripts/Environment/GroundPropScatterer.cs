using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// AJOUTÉ (2026-10-20, chantier graphismes - "on peut faire des choses sans Tripo pour l'instant") - couche de
// petits décors de SOL (touffes d'herbe, fleurs, cailloux, éclats de mana, bûches, champignons, mousse) dispersés
// à L'INTÉRIEUR de la zone jouable (contrairement à BoundaryDecorationPlacer/InfiniteDecorStreamer, qui ne posent
// des arbres/rochers qu'en BORDURE) - c'était le manque n°1 identifié dans A_MODELISER_3D.md §7.1 ("rien n'est posé
// à l'intérieur, le sol fait vide/papier peint").
//
// SANS collision (purement visuel, le joueur et les ennemis marchent "à travers"), SANS GameObject par instance -
// aucun Transform, aucun Renderer, aucun component par brin d'herbe. Les matrices de placement sont calculées une
// fois par "cellule" (grille carrée autour du joueur, même principe de chargement/déchargement que
// InfiniteDecorStreamer) puis soumises au GPU par lots via Graphics.RenderMeshInstanced (vrai instancing matériel,
// jusqu'à ~1000 instances par appel de dessin) - la technique demandée explicitement par l'utilisateur pour tenir
// la densité nécessaire sans exploser le nombre d'appels de dessin ni le CPU (voir
// materialpropertyblock-breaks-gpu-batching dans la mémoire du projet : on n'utilise volontairement AUCUN
// MaterialPropertyBlock par instance ici, la variété vient uniquement du CHOIX du matériau/mesh par variante, pas
// d'une teinte par instance).
//
// Les meshs sont des PLACEHOLDERS générés par code (voir GroundPropMeshFactory) - en attendant les vrais modèles
// Tripo3D listés dans A_MODELISER_3D.md §7.2. Remplacement futur : assigner les meshs importés aux champs
// "_meshOverride*" ci-dessous, aucune autre ligne de ce fichier à toucher.
//
// Placement SANS vérification de distance entre instances (contrairement aux arbres/rochers, où ça évite un
// chevauchement visuel choquant) : à cette échelle (brins d'herbe, fleurs, cailloux), un léger chevauchement est
// normal et même souhaitable (effet de touffe), et une vérification par paire serait bien trop coûteuse au nombre
// d'instances visées ici. La répartition vient d'un bruit de Perlin échantillonné en coordonnées MONDE (pas en
// coordonnées de cellule) : parfaitement continu d'une cellule à l'autre, aucune couture visible à une frontière de
// chargement, et entièrement déterministe (revisiter un endroit redonne exactement le même décor).
public class GroundPropScatterer : MonoBehaviour
{
    public enum PropCategory { GrassTuft, Wildflower, ManaShard, Pebble, Log, Mushroom, MossPatch }

    [Header("Matériaux (voir Assets/Game/Materials/Props/)")]
    [SerializeField] private Material _matGrass;
    [SerializeField] private Material _matFlowerRed;
    [SerializeField] private Material _matFlowerYellow;
    [SerializeField] private Material _matFlowerPurple;
    [SerializeField] private Material _matFlowerWhite;
    [SerializeField] private Material _matManaShard;
    [SerializeField] private Material _matPebble;
    [SerializeField] private Material _matLog;
    [SerializeField] private Material _matMushroomRed;
    [SerializeField] private Material _matMushroomBrown;
    [SerializeField] private Material _matMoss;

    [Header("Grille de chargement (autour du joueur)")]
    [Tooltip("Taille d'une cellule en unités monde. Doit être un multiple de Sample Step.")]
    [SerializeField] private float _cellSize = 14f;
    [Tooltip("Rayon de chargement en cellules (donc (2N+1)² cellules actives en permanence).")]
    [SerializeField] private int _loadRadiusCells = 3;
    [SerializeField] private int _unloadHysteresisCells = 1;
    [Tooltip("Espacement du quadrillage d'échantillonnage (candidats testés par catégorie, en unités monde).")]
    [SerializeField] private float _sampleStep = 2f;

    [Header("Densité par préréglage de Qualité (0 Performant, 1 Équilibré, 2 Haute Fidélité)")]
    [SerializeField] private float[] _countFactorByQuality = { 0.35f, 0.7f, 1f };

    private struct VariantDef
    {
        public Func<Mesh> meshFactory;
        public Material material;
        public float weight;
    }

    private struct CategoryDef
    {
        public PropCategory category;
        public float noiseFrequency;
        public float noiseOffsetX;
        public float noiseOffsetZ;
        public float threshold;      // bruit > seuil => candidat retenu (ignoré si useNoise == false)
        public bool useNoise;        // true = pousse en patchs (plantes) ; false = tirage indépendant (roches, bûches, éclats)
        public float independentChance;
        public float minScale;
        public float maxScale;
        public float yOffset;
        public float maxTiltDegrees;
        public bool castShadows;
        public VariantDef[] variants;
    }

    // Clé plate (catégorie, variante) -> flux d'instances pour le rendu par lots.
    private struct BatchKey : IEquatable<BatchKey>
    {
        public int category;
        public int variant;
        public bool Equals(BatchKey other) => category == other.category && variant == other.variant;
        public override bool Equals(object obj) => obj is BatchKey other && Equals(other);
        public override int GetHashCode() => category * 397 + variant;
    }

    private struct Instance
    {
        public BatchKey key;
        public Matrix4x4 matrix;
        public Vector3 position;
    }

    private CategoryDef[] _categories;
    private readonly Dictionary<Vector2Int, List<Instance>> _loadedCells = new Dictionary<Vector2Int, List<Instance>>();
    private readonly Dictionary<BatchKey, List<Matrix4x4>> _mergedMatrices = new Dictionary<BatchKey, List<Matrix4x4>>();
    private readonly Dictionary<BatchKey, Bounds> _mergedBounds = new Dictionary<BatchKey, Bounds>();

    private Transform _player;
    private Vector2Int _lastPlayerCell = new Vector2Int(int.MinValue, int.MinValue);
    private float _qualityFactor = 1f;
    private const int MaxInstancesPerDrawCall = 511;

    private void Start()
    {
        GameObject playerGo = GameObject.FindWithTag("Player");
        if (playerGo == null)
        {
            enabled = false;
            return;
        }
        _player = playerGo.transform;

        int qualityIndex = Mathf.Clamp(GameSettings.GetInt(GameSettings.Quality), 0, _countFactorByQuality.Length - 1);
        _qualityFactor = _countFactorByQuality.Length > 0 ? _countFactorByQuality[qualityIndex] : 1f;

        BuildCategories();
        EnsureInstancingEnabled();

        Vector2Int startCell = WorldToCell(_player.position);
        RefreshCells(startCell);
        _lastPlayerCell = startCell;
    }

    // Teinte les éclats de mana selon le palier de difficulté courant (DifficultyTiers.LightColor), pour la même
    // cohérence visuelle que AmbientManaMotes/GameSceneSettingsApplier - ".material" clone l'asset partagé en une
    // instance d'exécution (même précaution que GameManager.ScaleGroundToZone) : on ne modifie jamais l'asset du
    // projet Mat_Prop_ManaShard.mat lui-même.
    private Material CreateTintedManaShardMaterial()
    {
        if (_matManaShard == null) return null;

        Material instance = new Material(_matManaShard);
        DifficultyTier tier = GameModes.IsClassic ? DifficultyTiers.Current : DifficultyTier.Moderate;
        Color tint = DifficultyTiers.LightColor(tier);

        instance.EnableKeyword("_EMISSION");
        instance.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        instance.SetColor("_EmissionColor", tint * 1.8f);
        instance.SetColor("_BaseColor", Color.Lerp(tint, Color.white, 0.4f));
        return instance;
    }

    private void EnsureInstancingEnabled()
    {
        // Graphics.RenderMeshInstanced exige "enableInstancing" sur le matériau, sans quoi Unity lève une
        // exception à l'exécution - on le force ici plutôt que de dépendre d'une case cochée à la main dans
        // l'inspecteur (qui pourrait être oubliée sur un futur matériau de remplacement Tripo3D).
        foreach (CategoryDef cat in _categories)
            foreach (VariantDef v in cat.variants)
                if (v.material != null) v.material.enableInstancing = true;
    }

    private void BuildCategories()
    {
        _categories = new[]
        {
            new CategoryDef
            {
                category = PropCategory.GrassTuft,
                noiseFrequency = 0.09f, noiseOffsetX = 0f, noiseOffsetZ = 0f,
                threshold = 0.38f, useNoise = true, independentChance = 1f,
                minScale = 0.85f, maxScale = 1.3f, yOffset = 0f, maxTiltDegrees = 8f,
                castShadows = false,
                variants = new[]
                {
                    new VariantDef { meshFactory = () => GroundPropMeshFactory.GrassTuft(0), material = _matGrass, weight = 1f },
                    new VariantDef { meshFactory = () => GroundPropMeshFactory.GrassTuft(1), material = _matGrass, weight = 1f },
                }
            },
            new CategoryDef
            {
                category = PropCategory.Wildflower,
                noiseFrequency = 0.16f, noiseOffsetX = 500f, noiseOffsetZ = 500f,
                threshold = 0.6f, useNoise = true, independentChance = 1f,
                minScale = 0.8f, maxScale = 1.2f, yOffset = 0f, maxTiltDegrees = 5f,
                castShadows = false,
                variants = new[]
                {
                    new VariantDef { meshFactory = () => GroundPropMeshFactory.Wildflower(0), material = _matFlowerRed, weight = 1f },
                    new VariantDef { meshFactory = () => GroundPropMeshFactory.Wildflower(1), material = _matFlowerYellow, weight = 1f },
                    new VariantDef { meshFactory = () => GroundPropMeshFactory.Wildflower(0), material = _matFlowerPurple, weight = 0.7f },
                    new VariantDef { meshFactory = () => GroundPropMeshFactory.Wildflower(1), material = _matFlowerWhite, weight = 0.7f },
                }
            },
            new CategoryDef
            {
                category = PropCategory.Pebble,
                noiseFrequency = 0f, noiseOffsetX = 0f, noiseOffsetZ = 0f,
                threshold = 0f, useNoise = false, independentChance = 0.05f,
                minScale = 0.7f, maxScale = 1.6f, yOffset = 0f, maxTiltDegrees = 15f,
                castShadows = false,
                variants = new[]
                {
                    new VariantDef { meshFactory = () => GroundPropMeshFactory.Pebble(0), material = _matPebble, weight = 1f },
                    new VariantDef { meshFactory = () => GroundPropMeshFactory.Pebble(1), material = _matPebble, weight = 1f },
                    new VariantDef { meshFactory = () => GroundPropMeshFactory.Pebble(2), material = _matPebble, weight = 1f },
                }
            },
            // RETIRÉ (2026-10-20, retour utilisateur : "les carrés bizarre aléatoirement fait en vert [...] ça fait
            // tâche") - le patch de mousse était un disque PLAT (maxTiltDegrees=0) à couleur UNIE et bord net, sans
            // aucun fondu alpha ni texture (voir GroundPropMeshFactory.BuildMossPatch) : sans un vrai dégradé de
            // transparence sur le bord pour le fondre dans l'herbe, toute forme plate à couleur plate POSÉE SUR une
            // texture de sol se lit comme un autocollant/une tache, pas comme un vrai patch de mousse - la bonne
            // version demanderait une vraie texture avec un bord qui s'estompe (un travail de texture, pas de
            // géométrie), donc en attente des décors 2D peints prévus via MetaIA plutôt que d'un correctif ici.
            // Catégorie retirée du tirage (jamais supprimée du code : PropCategory.MossPatch/_matMoss restent, prêts
            // à être réactivés si une texture adaptée arrive).
            new CategoryDef
            {
                category = PropCategory.Mushroom,
                noiseFrequency = 0.2f, noiseOffsetX = 3000f, noiseOffsetZ = 3000f,
                threshold = 0.78f, useNoise = true, independentChance = 0.6f, // ronds de sorcières : rares ET groupés
                minScale = 0.8f, maxScale = 1.2f, yOffset = 0f, maxTiltDegrees = 6f,
                castShadows = false,
                variants = new[]
                {
                    new VariantDef { meshFactory = () => GroundPropMeshFactory.Mushroom(0), material = _matMushroomRed, weight = 1f },
                    new VariantDef { meshFactory = () => GroundPropMeshFactory.Mushroom(1), material = _matMushroomBrown, weight = 1f },
                }
            },
            new CategoryDef
            {
                category = PropCategory.ManaShard,
                noiseFrequency = 0f, noiseOffsetX = 0f, noiseOffsetZ = 0f,
                threshold = 0f, useNoise = false, independentChance = 0.012f, // accent rare, aucune raison de former des patchs
                minScale = 0.8f, maxScale = 1.4f, yOffset = 0f, maxTiltDegrees = 10f,
                castShadows = true, // seul élément lumineux/vertical du lot : vaut la dépense d'une ombre portée
                variants = new[]
                {
                    new VariantDef { meshFactory = GroundPropMeshFactory.ManaShard, material = CreateTintedManaShardMaterial(), weight = 1f },
                }
            },
            new CategoryDef
            {
                category = PropCategory.Log,
                noiseFrequency = 0f, noiseOffsetX = 0f, noiseOffsetZ = 0f,
                threshold = 0f, useNoise = false, independentChance = 0.006f,
                minScale = 0.8f, maxScale = 1.3f, yOffset = 0f, maxTiltDegrees = 4f,
                castShadows = true,
                variants = new[]
                {
                    new VariantDef { meshFactory = GroundPropMeshFactory.Log, material = _matLog, weight = 1f },
                }
            },
        };
    }

    private void Update()
    {
        if (_player == null) return;

        Vector2Int currentCell = WorldToCell(_player.position);
        if (currentCell != _lastPlayerCell)
        {
            _lastPlayerCell = currentCell;
            RefreshCells(currentCell);
        }

        SubmitDrawCalls();
    }

    private Vector2Int WorldToCell(Vector3 worldPos)
    {
        return new Vector2Int(Mathf.FloorToInt(worldPos.x / _cellSize), Mathf.FloorToInt(worldPos.z / _cellSize));
    }

    private void RefreshCells(Vector2Int center)
    {
        int unloadRadius = _loadRadiusCells + _unloadHysteresisCells;
        List<Vector2Int> toUnload = null;
        foreach (Vector2Int loaded in _loadedCells.Keys)
        {
            if (Mathf.Abs(loaded.x - center.x) > unloadRadius || Mathf.Abs(loaded.y - center.y) > unloadRadius)
            {
                toUnload ??= new List<Vector2Int>();
                toUnload.Add(loaded);
            }
        }
        if (toUnload != null)
            foreach (Vector2Int c in toUnload) _loadedCells.Remove(c);

        for (int cx = center.x - _loadRadiusCells; cx <= center.x + _loadRadiusCells; cx++)
        {
            for (int cz = center.y - _loadRadiusCells; cz <= center.y + _loadRadiusCells; cz++)
            {
                Vector2Int coord = new Vector2Int(cx, cz);
                if (!_loadedCells.ContainsKey(coord))
                    _loadedCells[coord] = GenerateCell(coord);
            }
        }

        RebuildMergedBuffers();
    }

    private List<Instance> GenerateCell(Vector2Int cellCoord)
    {
        List<Instance> result = new List<Instance>();

        float worldMinX = cellCoord.x * _cellSize;
        float worldMinZ = cellCoord.y * _cellSize;
        int latticeMinIX = Mathf.CeilToInt(worldMinX / _sampleStep);
        int latticeMinIZ = Mathf.CeilToInt(worldMinZ / _sampleStep);
        int steps = Mathf.Max(1, Mathf.RoundToInt(_cellSize / _sampleStep));

        for (int catIndex = 0; catIndex < _categories.Length; catIndex++)
        {
            CategoryDef cat = _categories[catIndex];
            if (cat.variants == null || cat.variants.Length == 0) continue;

            int categorySalt = catIndex * 104729 + 7919;

            for (int sx = 0; sx < steps; sx++)
            {
                for (int sz = 0; sz < steps; sz++)
                {
                    int ix = latticeMinIX + sx;
                    int iz = latticeMinIZ + sz;

                    float jitterX = (Hash01(ix, iz, categorySalt, 1) - 0.5f) * _sampleStep;
                    float jitterZ = (Hash01(ix, iz, categorySalt, 2) - 0.5f) * _sampleStep;
                    float worldX = ix * _sampleStep + _sampleStep * 0.5f + jitterX;
                    float worldZ = iz * _sampleStep + _sampleStep * 0.5f + jitterZ;

                    if (worldX <= MapBoundaryUtils.MinX || worldX >= MapBoundaryUtils.MaxX) continue;
                    if (worldZ <= MapBoundaryUtils.MinZ || worldZ >= MapBoundaryUtils.MaxZ) continue;

                    bool accepted;
                    if (cat.useNoise)
                    {
                        float noise = Mathf.PerlinNoise((worldX + cat.noiseOffsetX) * cat.noiseFrequency, (worldZ + cat.noiseOffsetZ) * cat.noiseFrequency);
                        accepted = noise > cat.threshold;
                    }
                    else
                    {
                        accepted = Hash01(ix, iz, categorySalt, 3) < cat.independentChance;
                    }
                    if (!accepted) continue;

                    if (Hash01(ix, iz, categorySalt, 4) >= _qualityFactor) continue;

                    int variantIndex = PickVariant(cat.variants, Hash01(ix, iz, categorySalt, 5));
                    VariantDef variant = cat.variants[variantIndex];
                    if (variant.material == null) continue;

                    float scale = Mathf.Lerp(cat.minScale, cat.maxScale, Hash01(ix, iz, categorySalt, 6));
                    float yaw = Hash01(ix, iz, categorySalt, 7) * 360f;
                    float tiltX = (Hash01(ix, iz, categorySalt, 8) - 0.5f) * 2f * cat.maxTiltDegrees;
                    float tiltZ = (Hash01(ix, iz, categorySalt, 9) - 0.5f) * 2f * cat.maxTiltDegrees;

                    Vector3 position = new Vector3(worldX, cat.yOffset, worldZ);
                    Quaternion rotation = Quaternion.Euler(tiltX, yaw, tiltZ);
                    Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, Vector3.one * scale);

                    result.Add(new Instance
                    {
                        key = new BatchKey { category = catIndex, variant = variantIndex },
                        matrix = matrix,
                        position = position
                    });
                }
            }
        }

        return result;
    }

    private static int PickVariant(VariantDef[] variants, float roll)
    {
        float totalWeight = 0f;
        for (int i = 0; i < variants.Length; i++) totalWeight += Mathf.Max(0f, variants[i].weight);
        if (totalWeight <= 0f) return 0;

        float target = roll * totalWeight;
        float cumulative = 0f;
        for (int i = 0; i < variants.Length; i++)
        {
            cumulative += Mathf.Max(0f, variants[i].weight);
            if (target <= cumulative) return i;
        }
        return variants.Length - 1;
    }

    private void RebuildMergedBuffers()
    {
        foreach (List<Matrix4x4> list in _mergedMatrices.Values) list.Clear();
        _mergedBounds.Clear();

        foreach (List<Instance> cellInstances in _loadedCells.Values)
        {
            foreach (Instance inst in cellInstances)
            {
                if (!_mergedMatrices.TryGetValue(inst.key, out List<Matrix4x4> list))
                {
                    list = new List<Matrix4x4>();
                    _mergedMatrices[inst.key] = list;
                }
                list.Add(inst.matrix);

                if (_mergedBounds.TryGetValue(inst.key, out Bounds bounds))
                {
                    bounds.Encapsulate(inst.position);
                    _mergedBounds[inst.key] = bounds;
                }
                else
                {
                    _mergedBounds[inst.key] = new Bounds(inst.position, Vector3.one * 2f);
                }
            }
        }

    }

    private void SubmitDrawCalls()
    {
        foreach (KeyValuePair<BatchKey, List<Matrix4x4>> entry in _mergedMatrices)
        {
            List<Matrix4x4> matrices = entry.Value;
            if (matrices.Count == 0) continue;

            CategoryDef cat = _categories[entry.Key.category];
            VariantDef variant = cat.variants[entry.Key.variant];
            if (variant.material == null) continue;

            Mesh mesh = variant.meshFactory != null ? variant.meshFactory() : null;
            if (mesh == null) continue;

            Bounds bounds = _mergedBounds.TryGetValue(entry.Key, out Bounds b) ? b : new Bounds(_player.position, Vector3.one * 50f);
            bounds.Expand(2f); // marge pour la taille propre de chaque mesh autour de son point de pivot

            RenderParams rp = new RenderParams(variant.material)
            {
                layer = gameObject.layer,
                worldBounds = bounds,
                shadowCastingMode = cat.castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                receiveShadows = true,
                lightProbeUsage = LightProbeUsage.Off,
            };

            int total = matrices.Count;
            for (int start = 0; start < total; start += MaxInstancesPerDrawCall)
            {
                int count = Mathf.Min(MaxInstancesPerDrawCall, total - start);
                Graphics.RenderMeshInstanced(rp, mesh, 0, matrices, count, start);
            }
        }
    }

    private static float Hash01(int a, int b, int c, int d)
    {
        unchecked
        {
            int h = a * 374761393 + b * 668265263 + c * 1274126177 + d * 2038074743;
            h = (h ^ (h >> 13)) * 974552461;
            h ^= h >> 16;
            return (h & 0x7fffffff) / 2147483647f;
        }
    }
}
