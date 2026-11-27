using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

// AJOUTÉ (2026-10-28, retour utilisateur : "ça ruine bien les fps quand il y en a massivement [...] que font les meilleurs
// jeux du genre quand l'écran est blindé d'xp sur le sol ?") - toutes les gemmes d'XP de la partie, gérées comme des
// DONNÉES dans un seul tableau au lieu d'un GameObject par gemme (méthode des jeux du genre qui ne fusionnent pas les
// gemmes : Halls of Torment, Deep Rock Galactic: Survivor...). Mesuré avant ce changement (Apocalyptique, joueur
// immobile, ennemis invincibles) : 240 images/s sans gemme, 114 avec 2 500 gemmes au sol - chaque gemme payait chaque
// image son propre Update (flottement, billboard, MaterialPropertyBlock qui empêchait tout regroupement des draw calls)
// et le déplacement de son collider de ramassage dans la physique.
//
// Règle de l'utilisateur inchangée (voir XPGemSpawner) : aucune gemme n'est jamais fusionnée, déplacée ni détruite, pas
// de plafond, l'aimant passif reste. Comportement et apparence repris à l'identique de l'ancien XPGem :
//  - Popping : petit saut en arc depuis le point de mort jusqu'au sol, échelle en rebond (EaseOutBack) ;
//  - Idle : flotte et respire, ramassée quand le joueur marche dessus (contact horizontal) ou aspirée par l'aimant ;
//  - Attracted : accélère vers le joueur avec une traînée, éclate au contact (XPGemCollectFX) et crédite l'XP.
// Les traînées d'aspiration sont des TrailRenderer réutilisés (MaxTrails à la fois : au-delà, une aspiration massive par
// l'Ultime du Cristal vole sans traînée, invisible dans la nuée).
//
// MODIFIÉ (2026-11-25, retour utilisateur : « ce qui fait chuter les fps de moitié » - mesuré dans sa partie : 9 677
// gemmes au sol à 5:50 dont 7 320 à l'écran, ~2 ms par image rien que pour elles, et ~1 000 de plus toutes les 45 s) -
// avant, CHAQUE image, toutes les gemmes visibles étaient triées, animées (flottement + respiration) puis renvoyées à la
// carte graphique : un coût qui grandissait avec la partie. Le rendu passe sur DEUX couches (même shader
// Aether/XPGemField, même apparence qu'avant) :
//  1. Gemmes POSÉES (l'immense majorité) : un maillage déjà trié du plus loin au plus proche de la caméra, que le
//     processeur ne touche plus d'une image à l'autre. Le flottement et la respiration sont calculés par la carte
//     graphique à partir d'une horloge (_XPGemClock). Une gemme ramassée ou aspirée y est seulement « éteinte » (ses 4
//     sommets passent à l'échelle 0). Le maillage n'est reconstruit que par lots (au plus toutes les RebuildInterval
//     secondes quand de nouvelles gemmes se sont posées, ou quand les trous deviennent nombreux) : le tri se fait alors
//     par FUSION de la liste déjà triée avec les quelques nouvelles gemmes (la caméra du jeu ne tourne pas : l'ordre de
//     profondeur d'une gemme posée ne change jamais).
//  2. Gemmes EN MOUVEMENT (saut d'apparition, aspiration) et gemmes posées pas encore intégrées au lot : petit maillage
//     reconstruit chaque image comme avant (quelques dizaines de gemmes en général).
public class XPGemField : MonoBehaviour
{
    private enum GemState : byte { Popping, Idle, Attracted }

    private struct Gem
    {
        public Vector3 pos, origin, target;
        public XPGem.GemType type;
        public GemState state;
        public float xp;
        public float timer;          // Popping : temps écoulé du saut ; Attracted : rampe d'accélération
        public float popDuration, popArc;
        public float phase, pulseMul, birth, speedBoost;   // birth : horloge des gemmes (_clock) à l'apparition
        public float key;            // ordre de profondeur (plus petit = plus loin), fixé quand la gemme entre dans la couche
        public int slot;             // place dans la couche des gemmes posées, -1 si elle n'y est pas
        public int trail;            // index dans _trails, -1 si aucune
    }

    // Valeurs reprises de l'ancien XPGem.
    private const float PopDuration = 0.3f;
    private const float PopArcHeight = 0.8f;
    private const float IdleBobAmplitude = 0.1f;
    private const float IdleBobSpeed = 2.6f;
    private const float BasePulseSpeed = 1.5f;
    private const float MinGlow = 1.0f;
    private const float MaxGlow = 1.3f;
    private const float BaseMoveSpeed = 8f;
    private const float MaxMoveSpeed = 17f;
    private const float AttractRampDuration = 0.45f;
    private const float CollectDistSqr = 0.4f * 0.4f;
    private const float ContactRadiusXZSqr = 0.7f * 0.7f;
    private const float HalfWidth = 0.534f;
    private const float HalfHeight = 0.484f;
    private const int MaxTrails = 64;

    // Couche des gemmes posées : fréquence maximale des reconstructions (~0,4 ms pour 10 000 gemmes ; en attendant, les
    // gemmes fraîchement posées sont dessinées par la couche en mouvement, pour un coût négligeable), et compactage
    // quand les trous pèsent lourd.
    private const float RebuildInterval = 1f;
    private const int MinHolesForCompaction = 512;
    // La caméra du jeu ne tourne pas ; au-delà de ~2,5° (cos 0,999), l'ordre de profondeur est recalculé.
    private const float KeyDirTolerance = 0.999f;

    private static XPGemField _instance;
    public static XPGemField Instance
    {
        get
        {
            if (_instance == null) _instance = new GameObject("XPGemField").AddComponent<XPGemField>();
            return _instance;
        }
    }

    public static int Count => _instance != null ? _instance._count : 0;

    // didacticiel : XP total des gemmes encore au sol, et leurs positions (pour les flèches)
    public static float TotalXp
    {
        get
        {
            if (_instance == null) return 0f;
            float t = 0f;
            for (int i = 0; i < _instance._count; i++) t += _instance._gems[i].xp;
            return t;
        }
    }

    public static void CollectPositions(System.Collections.Generic.List<Vector3> into, int max)
    {
        into.Clear();
        if (_instance == null) return;
        for (int i = 0; i < _instance._count && into.Count < max; i++) into.Add(_instance._gems[i].pos);
    }

    private Gem[] _gems = new Gem[512];
    private int _count;
    // Horloge des gemmes : avance avec le temps de jeu, s'arrête en pause (comme l'ancien timeAlive par gemme).
    private float _clock;

    private Transform _player;
    private Camera _camera;

    // ------------------------------------------------------------------------------------------------------------
    // Rendu : sommet et couche
    // ------------------------------------------------------------------------------------------------------------

    // Voir Aether/XPGemField pour le rôle de chaque champ.
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct GemVertex
    {
        public Vector3 anchor;
        public Vector4 uvc;
        public Vector4 anim;
        public float fixedGlow;
    }

    private static readonly VertexAttributeDescriptor[] VertexLayout =
    {
        new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 1),
    };

    private const MeshUpdateFlags FastUpdate = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds
                                               | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontResetBoneBounds;

    // Un maillage de quads (un par gemme) et ses tampons, dimensionnés par puissance de 2.
    private sealed class GemLayer
    {
        public readonly Mesh mesh;
        public GemVertex[] verts = new GemVertex[0];
        private int _capacity;

        public GemLayer(string name, Transform parent, Material material, int sortingOrder)
        {
            mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sortingOrder = sortingOrder;
        }

        // Réalloue (contenu perdu) seulement quand le nombre de quads dépasse la capacité.
        public void EnsureCapacity(int quads)
        {
            if (_capacity >= quads && _capacity > 0) return;
            int cap = Mathf.Max(512, Mathf.NextPowerOfTwo(quads));
            _capacity = cap;
            verts = new GemVertex[cap * 4];
            int[] indices = new int[cap * 6];
            for (int q = 0; q < cap; q++)
            {
                int v = q * 4, t = q * 6;
                indices[t] = v; indices[t + 1] = v + 1; indices[t + 2] = v + 2;
                indices[t + 3] = v; indices[t + 4] = v + 2; indices[t + 5] = v + 3;
            }
            mesh.Clear();
            mesh.SetVertexBufferParams(cap * 4, VertexLayout);
            mesh.SetIndexBufferParams(cap * 6, IndexFormat.UInt32);
            mesh.SetIndexBufferData(indices, 0, 0, cap * 6, FastUpdate);
            mesh.subMeshCount = 1;
        }

        public void Upload(int firstQuad, int quadCount)
        {
            if (quadCount <= 0) return;
            mesh.SetVertexBufferData(verts, firstQuad * 4, firstQuad * 4, quadCount * 4, 0, FastUpdate);
        }

        // Nombre de quads dessinés et boîte réelle (sert au découpage caméra ET au tri des objets transparents).
        public void SetDrawn(int quads, Bounds bounds)
        {
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, quads * 6) { bounds = bounds, firstVertex = 0, vertexCount = quads * 4 }, FastUpdate);
            mesh.bounds = bounds;
        }
    }

    private Material _material;
    private Texture2D _atlas;
    private GemLayer _settled;   // gemmes posées (presque toutes), voir l'en-tête
    private GemLayer _moving;    // gemmes en mouvement + gemmes posées en attente d'intégration

    // Couche des gemmes posées : _slotGem[slot] = index de la gemme dans _gems, -1 pour un trou.
    private int[] _slotGem = new int[512];
    private int _slotCount;
    private int _holes;
    private int _pendingIdle;    // gemmes posées pas encore intégrées à la couche (dessinées par _moving)
    private float _lastRebuild = float.NegativeInfinity;
    private Vector3 _keyDir;
    private bool _hasKeyDir;

    // Tampons de travail (tri des gemmes en mouvement, fusion de la couche posée).
    private float[] _sortKeys = new float[512];
    private int[] _sortOrder = new int[512];
    private int[] _newOrder = new int[512];
    private readonly Plane[] _frustum = new Plane[6];

    // Visibles dans le profileur (coût de chaque étape du rendu des gemmes).
    private static readonly Unity.Profiling.ProfilerMarker MarkerRebuild = new Unity.Profiling.ProfilerMarker("XPGemField.RebuildSettled");
    private static readonly Unity.Profiling.ProfilerMarker MarkerMoving = new Unity.Profiling.ProfilerMarker("XPGemField.RenderMoving");

    private static readonly int IdRight = Shader.PropertyToID("_XPGemRight");
    private static readonly int IdUp = Shader.PropertyToID("_XPGemUp");
    private static readonly int IdClock = Shader.PropertyToID("_XPGemClock");

    private TrailRenderer[] _trails;
    private bool[] _trailBusy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _instance = null;

    private void Awake()
    {
        // Le matériau vit dans Resources pour que le shader soit inclus dans la build ; copie pour y poser l'atlas.
        Material template = Resources.Load<Material>("FX/M_XPGemField");
        _material = template != null ? new Material(template) : new Material(Shader.Find("Aether/XPGemField"));
        _atlas = BuildAtlas();
        _material.mainTexture = _atlas;
        _material.SetVector("_Anim", new Vector4(IdleBobSpeed, IdleBobAmplitude, BasePulseSpeed, 0f));
        _material.SetVector("_Glow", new Vector4(MinGlow, MaxGlow, 0f, 0f));
        _material.renderQueue = 3000;

        // Sous les ennemis (qui restent à 0), comme les anciennes gemmes (-1 à -7). Les gemmes posées juste avant celles
        // en mouvement : une gemme qui saute ou qui est aspirée passe par-dessus celles du sol.
        _settled = new GemLayer("XPGemsSettled", transform, _material, -2);
        _moving = new GemLayer("XPGemsMoving", transform, _material, -1);

        _trails = new TrailRenderer[MaxTrails];
        _trailBusy = new bool[MaxTrails];
        Material trailMat = new Material(Shader.Find("Sprites/Default"));
        for (int i = 0; i < MaxTrails; i++)
        {
            TrailRenderer tr = new GameObject("XPGemTrail" + i).AddComponent<TrailRenderer>();
            tr.transform.SetParent(transform, false);
            tr.time = 0.14f;
            tr.minVertexDistance = 0.04f;
            tr.widthMultiplier = 0.18f;
            tr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            tr.sharedMaterial = trailMat;
            tr.emitting = false;
            tr.sortingOrder = 5;
            tr.shadowCastingMode = ShadowCastingMode.Off;
            tr.receiveShadows = false;
            _trails[i] = tr;
        }
    }

    // Les 3 textures (256x256 chacune) côte à côte dans une seule : un seul matériau pour TOUTES les gemmes, quel que
    // soit leur palier. Copie directe sur la carte graphique (textures non lisibles par le CPU), repli par dessin dans une
    // RenderTexture si la copie n'est pas supportée.
    private static Texture2D BuildAtlas()
    {
        Texture2D small = Resources.Load<Texture2D>("Textures/XPGem/Tex_XPGem_Small");
        Texture2D medium = Resources.Load<Texture2D>("Textures/XPGem/Tex_XPGem_Medium");
        Texture2D large = Resources.Load<Texture2D>("Textures/XPGem/Tex_XPGem_Large");
        Texture2D[] src = { small, medium, large };
        if (small == null || medium == null || large == null) return Texture2D.whiteTexture;

        int w = small.width, h = small.height;
        Texture2D atlas = new Texture2D(w * 3, h, small.graphicsFormat, TextureCreationFlags.None)
        {
            name = "XPGemAtlas",
            filterMode = small.filterMode,
            wrapMode = TextureWrapMode.Clamp
        };

        bool sameFormat = medium.graphicsFormat == small.graphicsFormat && large.graphicsFormat == small.graphicsFormat
                          && medium.width == w && large.width == w && medium.height == h && large.height == h;
        if (sameFormat && SystemInfo.copyTextureSupport != CopyTextureSupport.None)
        {
            for (int i = 0; i < 3; i++)
                Graphics.CopyTexture(src[i], 0, 0, 0, 0, w, h, atlas, 0, 0, i * w, 0);
            return atlas;
        }

        Object.Destroy(atlas);
        RenderTexture rt = new RenderTexture(w * 3, h, 0, RenderTextureFormat.ARGB32) { name = "XPGemAtlasRT" };
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, Color.clear);
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, w * 3, h, 0);
        for (int i = 0; i < 3; i++) Graphics.DrawTexture(new Rect(i * w, 0, w, h), src[i]);
        GL.PopMatrix();
        RenderTexture.active = prev;
        Texture2D copy = new Texture2D(w * 3, h, TextureFormat.RGBA32, false) { name = "XPGemAtlas", wrapMode = TextureWrapMode.Clamp };
        RenderTexture.active = rt;
        copy.ReadPixels(new Rect(0, 0, w * 3, h), 0, 0);
        copy.Apply();
        RenderTexture.active = prev;
        rt.Release();
        Object.Destroy(rt);
        return copy;
    }

    // ------------------------------------------------------------------------------------------------------------
    // API
    // ------------------------------------------------------------------------------------------------------------

    // origin : point de mort (départ du saut) ; landPosition : point d'atterrissage au sol. xpOverride : valeur exacte
    // portée par la gemme quand elle diffère de son palier (reste < 10, surplus d'une gerbe de boss).
    public void Spawn(XPGem.GemType type, Vector3 origin, Vector3 landPosition, float? xpOverride = null)
    {
        if (_count == _gems.Length) System.Array.Resize(ref _gems, _gems.Length * 2);
        _gems[_count++] = new Gem
        {
            pos = origin,
            origin = origin,
            target = landPosition,
            type = type,
            state = GemState.Popping,
            xp = xpOverride ?? XPGem.Value(type),
            timer = 0f,
            popDuration = PopDuration * Random.Range(0.85f, 1.15f),
            popArc = PopArcHeight * Random.Range(0.75f, 1.15f),
            phase = Random.Range(0f, Mathf.PI * 2f),
            pulseMul = Random.Range(0.7f, 1.4f),   // chaque gemme respire à son propre rythme, pas en choeur
            birth = _clock,
            speedBoost = 1f,
            slot = -1,
            trail = -1
        };
    }

    // Aspire toutes les gemmes à moins de range (3D, comme l'ancien CrystalSystem.AttractGems) de center.
    // fast : déjà à pleine vitesse et boostée (x1,6), sinon rampe naturelle.
    public void AttractInRange(Vector3 center, float range, bool fast)
    {
        bool all = range >= float.MaxValue;
        float rangeSqr = all ? 0f : range * range;
        for (int i = 0; i < _count; i++)
        {
            if (!all && (_gems[i].pos - center).sqrMagnitude > rangeSqr) continue;
            BeginAttraction(ref _gems[i], 1f, fast ? 1.6f : 1f);
        }
    }

    // TESTS UNIQUEMENT (bot d'équilibrage, 2026-11-15) : gemme posée au sol la plus proche de 'from' (en XZ), dans maxDist.
    public bool TryGetNearest(Vector3 from, float maxDist, out Vector3 position)
    {
        position = Vector3.zero;
        float best = maxDist * maxDist;
        bool found = false;
        for (int i = 0; i < _count; i++)
        {
            if (_gems[i].state == GemState.Attracted) continue;
            float dx = _gems[i].pos.x - from.x, dz = _gems[i].pos.z - from.z, d = dx * dx + dz * dz;
            if (d < best) { best = d; position = _gems[i].pos; found = true; }
        }
        return found;
    }

    // TESTS UNIQUEMENT (sondes de mesure de puissance, jamais appelé par le jeu) : retire toutes les gemmes sans créditer
    // leur XP, pour qu'aucun passage de niveau ne vienne interrompre une mesure.
    public void DebugClear()
    {
        for (int i = 0; i < _count; i++)
            if (_gems[i].trail >= 0) { _trails[_gems[i].trail].emitting = false; _trailBusy[_gems[i].trail] = false; }
        _count = 0;
        _slotCount = 0;
        _holes = 0;
        _pendingIdle = 0;
        if (_settled != null) _settled.SetDrawn(0, new Bounds(Vector3.zero, Vector3.zero));
    }

#if UNITY_EDITOR
    // TESTS (éditeur) : cohérence entre les gemmes et la couche des gemmes posées. Renvoie "" si tout est juste.
    public string DebugValidate()
    {
        int pending = 0, holes = 0;
        for (int i = 0; i < _count; i++)
        {
            Gem g = _gems[i];
            if (g.state != GemState.Idle)
            {
                if (g.slot >= 0) return "gemme en mouvement " + i + " encore dans la couche posée";
                continue;
            }
            if (g.slot < 0) { pending++; continue; }
            if (g.slot >= _slotCount || _slotGem[g.slot] != i) return "place " + g.slot + " ne pointe pas sur la gemme " + i;
            if (_settled.verts[g.slot * 4].anim.x <= 0f) return "gemme posée " + i + " éteinte à l'écran";
        }
        for (int s = 0; s < _slotCount; s++)
        {
            int idx = _slotGem[s];
            if (idx < 0)
            {
                holes++;
                if (_settled.verts[s * 4].anim.x != 0f) return "trou " + s + " encore visible";
                continue;
            }
            if (idx >= _count || _gems[idx].slot != s) return "place " + s + " pointe sur une gemme qui ne l'occupe pas";
            if (s > 0 && _slotGem[s - 1] >= 0 && _gems[_slotGem[s - 1]].key > _gems[idx].key) return "ordre de profondeur rompu en " + s;
        }
        if (holes != _holes) return "trous comptés " + _holes + " / réels " + holes;
        if (pending != _pendingIdle) return "en attente comptées " + _pendingIdle + " / réelles " + pending;
        return "";
    }
#endif

    // ------------------------------------------------------------------------------------------------------------
    // Simulation
    // ------------------------------------------------------------------------------------------------------------

    private void Update()
    {
        if (GameManager.Instance != null && (GameManager.Instance.IsPaused || GameManager.Instance.IsGameOver)) return;
        if (_player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p == null) return;
            _player = p.transform;
        }

        float dt = Time.deltaTime;
        _clock += dt;
        Vector3 player = _player.position;
        float magnet = XPGemSpawner.Instance != null ? XPGemSpawner.Instance.AttractionRadius : 0f;
        float magnetSqr = magnet * magnet;

        // À l'envers : un ramassage remplace la gemme courante par la dernière, déjà traitée cette image.
        for (int i = _count - 1; i >= 0; i--)
        {
            ref Gem g = ref _gems[i];

            switch (g.state)
            {
                case GemState.Popping:
                {
                    g.timer += dt;
                    float t = Mathf.Clamp01(g.timer / g.popDuration);
                    Vector3 flat = Vector3.Lerp(g.origin, g.target, t);
                    g.pos = flat + Vector3.up * (Mathf.Sin(t * Mathf.PI) * g.popArc);
                    if (t >= 1f)
                    {
                        g.pos = g.target;
                        g.state = GemState.Idle;
                        g.slot = -1;
                        _pendingIdle++;   // intégrée à la couche des gemmes posées au prochain lot
                    }
                    break;
                }
                case GemState.Idle:
                {
                    // Ramassée en marchant dessus (contact horizontal, Y ignoré) - jamais pendant le saut d'apparition.
                    float dx = g.pos.x - player.x, dz = g.pos.z - player.z;
                    if (dx * dx + dz * dz <= ContactRadiusXZSqr)
                    {
                        if (Collect(i)) return;
                        continue;
                    }
                    // Position logique = point d'atterrissage ; le flottement n'existe qu'au rendu (écart de hauteur
                    // 0,1 au plus, sans effet sur l'aimant).
                    if (magnetSqr > 0f && (player - g.pos).sqrMagnitude <= magnetSqr) BeginAttraction(ref g, 0f, 1f);
                    break;
                }
                case GemState.Attracted:
                {
                    g.timer += dt;
                    float rampT = Mathf.Clamp01(g.timer / AttractRampDuration);
                    float eased = rampT * (2f - rampT);
                    float speed = Mathf.Lerp(BaseMoveSpeed, MaxMoveSpeed, eased) * g.speedBoost;
                    Vector3 offset = player - g.pos;
                    float sqrDist = offset.sqrMagnitude;
                    float step = speed * dt;
                    if (sqrDist <= CollectDistSqr || step * step >= sqrDist)
                    {
                        if (Collect(i)) return;
                        continue;
                    }
                    g.pos += offset * (step / Mathf.Sqrt(sqrDist));
                    if (g.trail >= 0) _trails[g.trail].transform.position = g.pos;
                    break;
                }
            }
        }
    }

    private void BeginAttraction(ref Gem g, float rampProgress, float speedBoost)
    {
        if (g.state == GemState.Idle)
        {
            // Part de la hauteur où la gemme flottait à l'écran (même calcul que le shader).
            g.pos.y += Mathf.Max(0f, Mathf.Sin((_clock - g.birth) * IdleBobSpeed + g.phase) * IdleBobAmplitude);
            LeaveSettledLayer(ref g);
        }
        g.state = GemState.Attracted;
        g.timer = AttractRampDuration * rampProgress;
        g.speedBoost = speedBoost;
        if (g.trail >= 0) return;
        for (int t = 0; t < MaxTrails; t++)
        {
            if (_trailBusy[t]) continue;
            _trailBusy[t] = true;
            g.trail = t;
            TrailRenderer tr = _trails[t];
            Color c = XPGem.ColorOf(g.type);
            tr.startColor = FXSprites.A(c, 0.55f);
            tr.endColor = FXSprites.A(c, 0f);
            tr.transform.position = g.pos;
            tr.Clear();
            tr.emitting = true;
            return;
        }
    }

    // Renvoie true si le ramassage a mis le jeu en pause (passage de niveau) : la simulation s'arrête pour cette image,
    // comme les anciennes gemmes qui vérifiaient la pause avant tout ramassage (pas de ramassage en cascade derrière
    // le panneau de niveau).
    private bool Collect(int i)
    {
        Gem g = _gems[i];
        XPGemCollectFX.Play(g.pos, XPGem.ColorOf(g.type), XPGem.ScaleOf(g.type));
        if (g.trail >= 0)
        {
            _trails[g.trail].emitting = false;
            _trailBusy[g.trail] = false;
        }
        if (g.state == GemState.Idle) LeaveSettledLayer(ref g);

        // Retrait par échange avec la dernière gemme : sa place dans la couche posée pointe maintenant sur i.
        _count--;
        if (i != _count)
        {
            _gems[i] = _gems[_count];
            if (_gems[i].slot >= 0) _slotGem[_gems[i].slot] = i;
        }

        if (XPSystem.Instance != null) XPSystem.Instance.AddXP(g.xp);
        return GameManager.Instance != null && GameManager.Instance.IsPaused;
    }

    // Une gemme posée quitte la couche (ramassée ou aspirée) : sa place devient un trou, éteint tout de suite à l'écran.
    private void LeaveSettledLayer(ref Gem g)
    {
        if (g.slot < 0) { _pendingIdle--; return; }
        int slot = g.slot;
        g.slot = -1;
        _slotGem[slot] = -1;
        _holes++;
        GemVertex[] v = _settled.verts;
        for (int k = slot * 4; k < slot * 4 + 4; k++) v[k].anim.x = 0f;   // échelle 0 : quad sans surface
        _settled.Upload(slot, 1);
    }

    // ------------------------------------------------------------------------------------------------------------
    // Rendu (chaque image, même en pause : les gemmes restent affichées derrière les menus)
    // ------------------------------------------------------------------------------------------------------------

    private void LateUpdate()
    {
        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return;

        Transform camT = _camera.transform;
        Vector3 camFwd = camT.forward;
        // Même orientation que l'ancien billboard : quad local (XY, face +Z) tourné vers la caméra.
        Quaternion rot = Quaternion.LookRotation(-camFwd, Vector3.up);
        Shader.SetGlobalVector(IdRight, rot * Vector3.right);
        Shader.SetGlobalVector(IdUp, rot * Vector3.up);
        Shader.SetGlobalFloat(IdClock, _clock);

        // 1. Couche des gemmes posées : reconstruite seulement par lots.
        if (!_hasKeyDir || Vector3.Dot(_keyDir, camFwd) < KeyDirTolerance) RebuildSettled(camFwd, true);
        else if ((_pendingIdle > 0 && Time.unscaledTime - _lastRebuild >= RebuildInterval)
                 || (_holes >= MinHolesForCompaction && _holes * 4 >= _slotCount))
            RebuildSettled(camFwd, false);

        // 2. Couche des gemmes en mouvement : chaque image.
        RenderMoving(camT, camFwd);
    }

    private static void Grow<T>(ref T[] array, int needed)
    {
        if (array.Length < needed) array = new T[Mathf.NextPowerOfTwo(needed)];
    }

    // Quad d'une gemme : coins en unités monde (largeur 2 x HalfWidth, hauteur 2 x HalfHeight, ancré par le bas),
    // tiers de l'atlas selon le palier.
    private static void WriteQuad(GemVertex[] verts, int quad, Vector3 anchor, XPGem.GemType type, float scale,
                                  float phase, float pulseMul, float birth, float fixedGlow)
    {
        const float Third = 1f / 3f;
        float u0 = (int)type * Third, u1 = u0 + Third;
        const float W = HalfWidth, H = HalfHeight * 2f;
        Vector4 anim = new Vector4(scale, phase, pulseMul, birth);
        int v = quad * 4;
        verts[v] = new GemVertex { anchor = anchor, uvc = new Vector4(u0, 0f, -W, 0f), anim = anim, fixedGlow = fixedGlow };
        verts[v + 1] = new GemVertex { anchor = anchor, uvc = new Vector4(u1, 0f, W, 0f), anim = anim, fixedGlow = fixedGlow };
        verts[v + 2] = new GemVertex { anchor = anchor, uvc = new Vector4(u1, 1f, W, H), anim = anim, fixedGlow = fixedGlow };
        verts[v + 3] = new GemVertex { anchor = anchor, uvc = new Vector4(u0, 1f, -W, H), anim = anim, fixedGlow = fixedGlow };
    }

    // Reconstruit la couche des gemmes posées, triée du plus loin au plus proche. resort : la caméra a tourné (ou
    // premier passage), toutes les clés sont recalculées et triées ; sinon fusion de la liste déjà triée (trous
    // ignorés) avec les gemmes posées depuis le dernier lot, triées entre elles.
    private void RebuildSettled(Vector3 camFwd, bool resort)
    {
        using var _ = MarkerRebuild.Auto();
        _keyDir = camFwd;
        _hasKeyDir = true;
        _lastRebuild = Time.unscaledTime;

        Grow(ref _sortKeys, _count);
        Grow(ref _sortOrder, _count);
        int pending = 0;
        for (int i = 0; i < _count; i++)
        {
            ref Gem g = ref _gems[i];
            if (g.state != GemState.Idle || (!resort && g.slot >= 0)) continue;
            g.key = -Vector3.Dot(g.pos, camFwd);
            _sortKeys[pending] = g.key;
            _sortOrder[pending++] = i;
        }
        System.Array.Sort(_sortKeys, _sortOrder, 0, pending);

        int oldCount = resort ? 0 : _slotCount;
        int total = (resort ? 0 : _slotCount - _holes) + pending;
        Grow(ref _newOrder, total);
        int a = 0, b = 0, n = 0;
        while (true)
        {
            while (a < oldCount && _slotGem[a] < 0) a++;
            bool hasA = a < oldCount, hasB = b < pending;
            if (!hasA && !hasB) break;
            if (hasA && (!hasB || _gems[_slotGem[a]].key <= _sortKeys[b])) _newOrder[n++] = _slotGem[a++];
            else _newOrder[n++] = _sortOrder[b++];
        }

        _settled.EnsureCapacity(n);
        Grow(ref _slotGem, n);
        GemVertex[] verts = _settled.verts;
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        for (int q = 0; q < n; q++)
        {
            int idx = _newOrder[q];
            ref Gem g = ref _gems[idx];
            g.slot = q;
            _slotGem[q] = idx;
            WriteQuad(verts, q, g.pos, g.type, XPGem.ScaleOf(g.type), g.phase, g.pulseMul, g.birth, 0f);
            min = Vector3.Min(min, g.pos);
            max = Vector3.Max(max, g.pos);
        }
        _slotCount = n;
        _holes = 0;
        _pendingIdle = 0;
        _settled.Upload(0, n);
        // Marge : taille des quads (~1 de haut) + flottement.
        _settled.SetDrawn(n, n > 0 ? new Bounds((min + max) * 0.5f, max - min + Vector3.one * 3f) : new Bounds(camFwd, Vector3.zero));
    }

    // Gemmes en mouvement (saut, aspiration) et gemmes posées en attente du prochain lot : seulement celles à l'écran
    // (marge 1,5 : un quad qui déborde du bord reste dessiné), triées du plus loin au plus proche.
    private void RenderMoving(Transform camT, Vector3 camFwd)
    {
        using var _ = MarkerMoving.Auto();
        const float Margin = 1.5f;
        Grow(ref _sortKeys, _count);
        Grow(ref _sortOrder, _count);
        int visible = 0;
        bool ortho = _camera.orthographic;
        Vector3 camPos = camT.position, camRight = camT.right, camUp = camT.up;
        float halfH = _camera.orthographicSize + Margin;
        float halfW = _camera.orthographicSize * _camera.aspect + Margin;
        if (!ortho) GeometryUtility.CalculateFrustumPlanes(_camera, _frustum);
        for (int i = 0; i < _count; i++)
        {
            ref Gem g = ref _gems[i];
            if (g.state == GemState.Idle && g.slot >= 0) continue;   // déjà dans la couche des gemmes posées
            Vector3 d = g.pos - camPos;
            if (ortho)
            {
                // Caméra orthographique (celle du jeu) : 2 produits scalaires par gemme.
                float x = d.x * camRight.x + d.y * camRight.y + d.z * camRight.z;
                if (x > halfW || x < -halfW) continue;
                float y = d.x * camUp.x + d.y * camUp.y + d.z * camUp.z;
                if (y > halfH || y < -halfH) continue;
            }
            else
            {
                bool inside = true;
                for (int f = 0; f < 6 && inside; f++) inside = _frustum[f].GetDistanceToPoint(g.pos) > -Margin;
                if (!inside) continue;
            }
            _sortOrder[visible] = i;
            _sortKeys[visible++] = -(d.x * camFwd.x + d.y * camFwd.y + d.z * camFwd.z);
        }
        System.Array.Sort(_sortKeys, _sortOrder, 0, visible);

        _moving.EnsureCapacity(visible);
        GemVertex[] verts = _moving.verts;
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        for (int k = 0; k < visible; k++)
        {
            ref Gem g = ref _gems[_sortOrder[k]];
            float baseScale = XPGem.ScaleOf(g.type);
            switch (g.state)
            {
                case GemState.Popping:
                {
                    float t = Mathf.Clamp01(g.timer / g.popDuration);
                    float scale = baseScale * Mathf.Max(0f, FXSprites.EaseOutBack(t, 1.5f));
                    WriteQuad(verts, k, g.pos, g.type, scale, g.phase, g.pulseMul, g.birth, Mathf.Lerp(MinGlow, MaxGlow, 0.6f));
                    break;
                }
                case GemState.Attracted:
                {
                    float glow = Mathf.Lerp(MinGlow, MaxGlow, 0.5f + 0.5f * Mathf.Sin((_clock - g.birth) * BasePulseSpeed * g.pulseMul * 1.6f));
                    WriteQuad(verts, k, g.pos, g.type, baseScale, g.phase, g.pulseMul, g.birth, glow);
                    break;
                }
                default:   // posée, en attente du prochain lot : animée par le shader, comme dans la couche posée
                    WriteQuad(verts, k, g.pos, g.type, baseScale, g.phase, g.pulseMul, g.birth, 0f);
                    break;
            }
            min = Vector3.Min(min, g.pos);
            max = Vector3.Max(max, g.pos);
        }
        _moving.Upload(0, visible);
        _moving.SetDrawn(visible, visible > 0 ? new Bounds((min + max) * 0.5f, max - min + Vector3.one * 3f) : new Bounds(camT.position, Vector3.zero));
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
        if (_settled != null) Destroy(_settled.mesh);
        if (_moving != null) Destroy(_moving.mesh);
        if (_material != null) Destroy(_material);
        if (_atlas != null && _atlas != Texture2D.whiteTexture) Destroy(_atlas);
    }
}
