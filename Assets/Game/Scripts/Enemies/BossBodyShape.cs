using System.Collections.Generic;
using UnityEngine;

// AJOUTÉ (2026-10-27, retour utilisateur : "Il faut revoir la hitbox des boss, parce que par exemple le boss 3 des fois
// quand on tire sur lui, il se prend l'impact à un endroit où il n'y a pas son corps") - chaque boss n'avait qu'un seul
// collider fixe, sans rapport avec son modèle animé. Mesuré en Play : la capsule du Boss 3 (serpent) était posée EN
// TRAVERS de son corps (sol vide touché d'un côté, moitié de la queue hors hitbox) ; la boîte du Boss 1 faisait 10 m de
// haut pour un corps de 1,8 m.
//
// Forme réelle du corps : de petites sphères accrochées aux os du modèle, calculées UNE fois depuis le maillage (voir
// Voxelize ; dans l'espace de l'os, elles suivent donc toute l'animation - ondulation du serpent, ailes du drake, saut
// du cerf). Le test se fait à plat (XZ) : la caméra de dessus montre le corps vu d'en
// haut, c'est ce que le joueur vise. La caméra étant inclinée, chaque sphère est d'abord projetée le long du regard de la
// caméra jusqu'à la hauteur du point testé (le drake vole à ~4,4 m, le cerf est à ~2,4 m, les projectiles passent à
// ~1,5 m) : l'impact tombe là où le joueur VOIT le corps à l'écran, pas à sa verticale.
// Le collider du boss (UN seul, jamais plusieurs : les armes de zone touchent chaque collider trouvé, plusieurs
// colliders feraient prendre plusieurs fois les mêmes dégâts) est recalé à chaque frame sur la boîte qui englobe ces
// sphères : il sert de détection grossière (zones, Nova, contact), les projectiles et le contact affinent ensuite
// avec ContainsXZ (voir BossBase.IsOnBody).
[DisallowMultipleComponent]
public class BossBodyShape : MonoBehaviour
{
    private struct Part
    {
        public Transform bone;
        public Vector3 localCenter; // espace local de l'os
        public float localRadius;   // unités locales de l'os
    }

    private readonly List<Part> _parts = new List<Part>();
    private Vector3[] _worldCenters;
    private float[] _worldRadii;
    private int _cachedFrame = -1;

    private BoxCollider _box;
    private float _boxMinY, _boxMaxY; // hauteur couverte, en local racine (reprise du collider d'origine)

    // Côté d'un cube de découpage du corps, en mètres : assez fin pour suivre une queue ou un bout d'aile, assez gros pour
    // garder quelques centaines de sphères par boss. Rayon des sphères : un peu plus que le demi-côté, pour que des cubes
    // voisins se recouvrent (aucun trou entre deux).
    private const float CellSize = 0.5f;
    private const float CellRadiusFactor = 0.7f;
    private const float BoxMargin = 0.2f;
    // Hauteur de vol des projectiles (pivot du joueur, à hauteur de torse) : la boîte englobe le corps vu à cette hauteur.
    private const float ProjectileHeight = 1.5f;

    private static Camera _camera;

    public bool HasShape => _parts.Count > 0;

    private void Awake()
    {
        Build();
        SetupCollider();
    }

    // Découpage d'un maillage, partagé par toutes les instances qui l'utilisent (mini-boss invoqués à répétition) :
    // calculé une fois par partie et par maillage.
    private struct CellPart
    {
        public int boneIndex;
        public Vector3 localCenter;
        public float localRadius;
    }
    private static readonly Dictionary<Mesh, CellPart[]> _cache = new Dictionary<Mesh, CellPart[]>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => _cache.Clear();

    private void Build()
    {
        _parts.Clear();
        foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh mesh = smr.sharedMesh;
            Transform[] bones = smr.bones;
            if (mesh == null || !mesh.isReadable || bones == null || bones.Length == 0) continue;

            if (!_cache.TryGetValue(mesh, out CellPart[] cells))
            {
                cells = Voxelize(mesh, bones);
                _cache[mesh] = cells;
            }
            foreach (CellPart c in cells)
                if (c.boneIndex < bones.Length && bones[c.boneIndex] != null)
                    _parts.Add(new Part { bone = bones[c.boneIndex], localCenter = c.localCenter, localRadius = c.localRadius });
        }

        _worldCenters = new Vector3[_parts.Count];
        _worldRadii = new float[_parts.Count];
    }

    private static int DominantBone(BoneWeight w)
    {
        int best = w.boneIndex0;
        float bestW = w.weight0;
        if (w.weight1 > bestW) { best = w.boneIndex1; bestW = w.weight1; }
        if (w.weight2 > bestW) { best = w.boneIndex2; bestW = w.weight2; }
        if (w.weight3 > bestW) { best = w.boneIndex3; }
        return best;
    }

    // Surface du maillage échantillonnée triangle par triangle (pas seulement les sommets : un grand triangle d'aile n'a
    // de sommets qu'à ses coins), chaque échantillon rangé dans l'espace de l'os dominant de son triangle, puis regroupé en
    // cubes de CellSize (mètres, à la taille du boss à l'apparition) : une sphère par cube occupé. Suit fidèlement le
    // volume quelle que soit sa forme (ailes, tête enroulée, bois du cerf), là où une sphère par os ne couvrait que 20 à
    // 70 % du corps visible (mesuré).
    private static CellPart[] Voxelize(Mesh mesh, Transform[] bones)
    {
        Vector3[] vertices = mesh.vertices;
        BoneWeight[] weights = mesh.boneWeights;
        Matrix4x4[] bindposes = mesh.bindposes;
        int[] triangles = mesh.triangles;
        if (weights.Length != vertices.Length || bindposes.Length == 0) return new CellPart[0];

        int[] dominant = new int[vertices.Length];
        for (int v = 0; v < vertices.Length; v++) dominant[v] = DominantBone(weights[v]);

        // Taille d'un cube dans l'espace de chaque os : CellSize mètres ramenés à l'échelle monde de l'os.
        float[] localCell = new float[bones.Length];
        for (int b = 0; b < bones.Length; b++)
        {
            Vector3 s = bones[b] != null ? bones[b].lossyScale : Vector3.one;
            localCell[b] = CellSize / Mathf.Max(0.0001f, (Mathf.Abs(s.x) + Mathf.Abs(s.y) + Mathf.Abs(s.z)) / 3f);
        }

        var sums = new Dictionary<(int, int, int, int), (Vector3 sum, int count)>();
        for (int t = 0; t + 2 < triangles.Length; t += 3)
        {
            int i0 = triangles[t], i1 = triangles[t + 1], i2 = triangles[t + 2];
            int b = dominant[i0];
            if (b < 0 || b >= bones.Length || b >= bindposes.Length || bones[b] == null) continue;

            Matrix4x4 bp = bindposes[b];
            Vector3 p0 = bp.MultiplyPoint3x4(vertices[i0]);
            Vector3 p1 = bp.MultiplyPoint3x4(vertices[i1]);
            Vector3 p2 = bp.MultiplyPoint3x4(vertices[i2]);
            float cell = localCell[b];

            float longest = Mathf.Max((p1 - p0).magnitude, Mathf.Max((p2 - p1).magnitude, (p0 - p2).magnitude));
            int n = Mathf.Clamp(Mathf.CeilToInt(longest / (cell * 0.5f)), 1, 24);
            for (int u = 0; u <= n; u++)
                for (int w = 0; u + w <= n; w++)
                {
                    Vector3 q = p0 + (p1 - p0) * (u / (float)n) + (p2 - p0) * (w / (float)n);
                    var key = (b, Mathf.FloorToInt(q.x / cell), Mathf.FloorToInt(q.y / cell), Mathf.FloorToInt(q.z / cell));
                    sums.TryGetValue(key, out var acc);
                    sums[key] = (acc.sum + q, acc.count + 1);
                }
        }

        var cells = new CellPart[sums.Count];
        int k = 0;
        foreach (var kv in sums)
        {
            int b = kv.Key.Item1;
            cells[k++] = new CellPart
            {
                boneIndex = b,
                localCenter = kv.Value.sum / kv.Value.count,
                localRadius = localCell[b] * CellRadiusFactor,
            };
        }
        return cells;
    }

    // Remplace le collider de la racine (boîte ou capsule du prefab) par UNE boîte recalée chaque frame sur le corps. La
    // hauteur couverte reste celle du collider d'origine (les projectiles volent à hauteur de torse).
    private void SetupCollider()
    {
        if (!HasShape) return;

        Collider original = GetComponent<Collider>();
        if (original == null) return;

        Bounds local = LocalBoundsOf(original);
        _boxMinY = local.min.y;
        _boxMaxY = local.max.y;

        _box = original as BoxCollider;
        if (_box == null)
        {
            bool isTrigger = original.isTrigger;
            original.enabled = false;
            Destroy(original);
            _box = gameObject.AddComponent<BoxCollider>();
            _box.isTrigger = isTrigger;
        }
        FitCollider();
    }

    private static Bounds LocalBoundsOf(Collider c)
    {
        switch (c)
        {
            case BoxCollider b: return new Bounds(b.center, b.size);
            case SphereCollider s: return new Bounds(s.center, Vector3.one * s.radius * 2f);
            case CapsuleCollider cap:
                {
                    Vector3 size = Vector3.one * cap.radius * 2f;
                    size[cap.direction] = Mathf.Max(cap.height, cap.radius * 2f);
                    return new Bounds(cap.center, size);
                }
            default: return new Bounds(Vector3.zero, Vector3.one);
        }
    }

    private void LateUpdate()
    {
        if (_box != null && _box.enabled) FitCollider();
    }

    private void FitCollider()
    {
        UpdateWorldParts();
        Vector3 scale = transform.lossyScale;
        float sx = Mathf.Max(Mathf.Abs(scale.x), 0.0001f), sz = Mathf.Max(Mathf.Abs(scale.z), 0.0001f);

        // Boîte = corps vu à hauteur de projectile (projeté) + corps à sa verticale (armes de zone au sol).
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        for (int i = 0; i < _worldCenters.Length; i++)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                Vector3 world = _worldCenters[i];
                float radius = _worldRadii[i];
                if (pass == 1) ProjectToHeight(ref world, ref radius, ProjectileHeight);
                Vector3 c = transform.InverseTransformPoint(world);
                float rx = (radius + BoxMargin) / sx, rz = (radius + BoxMargin) / sz;
                minX = Mathf.Min(minX, c.x - rx); maxX = Mathf.Max(maxX, c.x + rx);
                minZ = Mathf.Min(minZ, c.z - rz); maxZ = Mathf.Max(maxZ, c.z + rz);
            }
        }
        _box.center = new Vector3((minX + maxX) * 0.5f, (_boxMinY + _boxMaxY) * 0.5f, (minZ + maxZ) * 0.5f);
        _box.size = new Vector3(maxX - minX, _boxMaxY - _boxMinY, maxZ - minZ);
    }

    private void UpdateWorldParts()
    {
        if (_cachedFrame == Time.frameCount) return;
        _cachedFrame = Time.frameCount;
        for (int i = 0; i < _parts.Count; i++)
        {
            Part p = _parts[i];
            if (p.bone == null) { _worldRadii[i] = 0f; continue; }
            _worldCenters[i] = p.bone.TransformPoint(p.localCenter);
            Vector3 s = p.bone.lossyScale;
            _worldRadii[i] = p.localRadius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
        }
    }

    // Projette une sphère du corps (centre + rayon) le long du regard de la caméra jusqu'au plan horizontal `height` :
    // là où un objet à cette hauteur se superpose à elle à l'écran. Caméra perspective : rayon partant de la caméra ;
    // orthographique : direction de visée.
    private static void ProjectToHeight(ref Vector3 center, ref float radius, float height)
    {
        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return;
        Transform cam = _camera.transform;

        if (_camera.orthographic)
        {
            Vector3 f = cam.forward;
            if (Mathf.Abs(f.y) < 0.05f) return;
            center += f * ((height - center.y) / f.y);
            return;
        }

        Vector3 origin = cam.position;
        float denom = origin.y - center.y;
        if (denom < 0.5f) return; // partie du corps au niveau de la caméra ou au-dessus : pas de projection fiable
        float k = (origin.y - height) / denom;
        center = origin + (center - origin) * k;
        radius *= k;
    }

    // Le point est-il sur le corps tel que le joueur le voit (corps projeté à la hauteur du point), à `margin` près (rayon
    // du projectile ou du joueur) ?
    public bool ContainsXZ(Vector3 point, float margin)
    {
        if (!HasShape) return true;
        UpdateWorldParts();
        for (int i = 0; i < _worldCenters.Length; i++)
        {
            Vector3 c = _worldCenters[i];
            float radius = _worldRadii[i];
            if (radius <= 0f) continue;
            ProjectToHeight(ref c, ref radius, point.y);
            float r = radius + margin;
            float dx = point.x - c.x, dz = point.z - c.z;
            if (dx * dx + dz * dz <= r * r) return true;
        }
        return false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_worldCenters == null) return;
        UpdateWorldParts();
        Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.6f);
        for (int i = 0; i < _worldCenters.Length; i++)
            Gizmos.DrawWireSphere(_worldCenters[i], _worldRadii[i]);
    }
#endif
}
