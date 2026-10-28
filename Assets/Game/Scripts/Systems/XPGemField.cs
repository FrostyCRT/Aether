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
// Rendu : un seul maillage dynamique (un quad face caméra par gemme, textures des 3 paliers réunies dans un atlas), trié
// du plus loin au plus proche de la caméra : un seul draw call pour toutes les gemmes. Seules les gemmes à l'écran sont
// triées et dessinées (2e passe d'optimisation, mesurée dans LateUpdate) ; le flottement n'est calculé que pour elles. Les traînées d'aspiration sont
// des TrailRenderer réutilisés (MaxTrails à la fois : au-delà, une aspiration massive par l'Ultime du Cristal vole sans
// traînée, invisible dans la nuée).
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
        public float phase, pulseMul, timeAlive, speedBoost;
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

    private Gem[] _gems = new Gem[512];
    private int _count;

    private Transform _player;
    private Camera _camera;

    // Sommet entrelacé (position, couleur, UV) : un seul envoi à la carte graphique par image au lieu de trois.
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct Vertex
    {
        public Vector3 pos;
        public Color32 color;
        public Vector2 uv;
    }

    private static readonly VertexAttributeDescriptor[] VertexLayout =
    {
        new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
        new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
    };

    private const MeshUpdateFlags FastUpdate = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds
                                               | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontResetBoneBounds;

    private Mesh _mesh;
    private Material _material;
    private Texture2D _atlas;
    private Vertex[] _verts = new Vertex[0];
    private int _capacity;
    private float[] _sortKeys = new float[0];
    private int[] _sortOrder = new int[0];
    private readonly Plane[] _frustum = new Plane[6];

    private TrailRenderer[] _trails;
    private bool[] _trailBusy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _instance = null;

    private void Awake()
    {
        _mesh = new Mesh { name = "XPGemField", indexFormat = IndexFormat.UInt32 };
        _mesh.MarkDynamic();
        gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
        MeshRenderer mr = gameObject.AddComponent<MeshRenderer>();
        _material = new Material(Shader.Find("Sprites/Default"));
        _atlas = BuildAtlas();
        _material.mainTexture = _atlas;
        // La couleur finale est cuite dans les textures : le scintillement module seulement l'intensité (1 -> 1,3,
        // comme l'ancien _Color par gemme). _Color du matériau porte le maximum, la couleur de sommet la fraction.
        _material.color = new Color(MaxGlow, MaxGlow, MaxGlow, 1f);
        _material.renderQueue = 3000;
        mr.sharedMaterial = _material;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        // Sous les ennemis (qui restent à 0), comme les anciennes gemmes (-1 à -7).
        mr.sortingOrder = -1;

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

    // Les 3 textures (256x256 chacune) côte à côte dans une seule : un seul matériau, donc un seul maillage trié par
    // profondeur pour TOUTES les gemmes, quel que soit leur palier. Copie directe sur la carte graphique (textures non
    // lisibles par le CPU), repli par dessin dans une RenderTexture si la copie n'est pas supportée.
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
            timeAlive = 0f,
            speedBoost = 1f,
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

    // TESTS UNIQUEMENT (sondes de mesure de puissance, jamais appelé par le jeu) : retire toutes les gemmes sans créditer
    // leur XP, pour qu'aucun passage de niveau ne vienne interrompre une mesure.
    public void DebugClear()
    {
        for (int i = 0; i < _count; i++)
            if (_gems[i].trail >= 0) { _trails[_gems[i].trail].emitting = false; _trailBusy[_gems[i].trail] = false; }
        _count = 0;
    }

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
        Vector3 player = _player.position;
        float magnet = XPGemSpawner.Instance != null ? XPGemSpawner.Instance.AttractionRadius : 0f;
        float magnetSqr = magnet * magnet;

        // À l'envers : un ramassage remplace la gemme courante par la dernière, déjà traitée cette image.
        for (int i = _count - 1; i >= 0; i--)
        {
            ref Gem g = ref _gems[i];
            g.timeAlive += dt;

            switch (g.state)
            {
                case GemState.Popping:
                {
                    g.timer += dt;
                    float t = Mathf.Clamp01(g.timer / g.popDuration);
                    Vector3 flat = Vector3.Lerp(g.origin, g.target, t);
                    g.pos = flat + Vector3.up * (Mathf.Sin(t * Mathf.PI) * g.popArc);
                    if (t >= 1f) { g.pos = g.target; g.state = GemState.Idle; }
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
                    // Position logique = point d'atterrissage ; le flottement n'est calculé qu'au rendu, pour les gemmes
                    // visibles (écart de hauteur 0,1 au plus, sans effet sur l'aimant).
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
        // Part de la hauteur où la gemme flottait à l'écran (le flottement n'existe qu'au rendu, voir LateUpdate).
        if (g.state == GemState.Idle)
            g.pos.y += Mathf.Max(0f, Mathf.Sin(g.timeAlive * IdleBobSpeed + g.phase) * IdleBobAmplitude);
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
        _gems[i] = _gems[--_count];

        if (XPSystem.Instance != null) XPSystem.Instance.AddXP(g.xp);
        return GameManager.Instance != null && GameManager.Instance.IsPaused;
    }

    // ------------------------------------------------------------------------------------------------------------
    // Rendu (chaque image, même en pause : les gemmes restent affichées derrière les menus)
    // ------------------------------------------------------------------------------------------------------------

    private void LateUpdate()
    {
        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return;

        EnsureCapacity(_count);
        Transform camT = _camera.transform;
        Vector3 camFwd = camT.forward;
        // Même orientation que l'ancien billboard : quad local (XY, face +Z) tourné vers la caméra.
        Quaternion rot = Quaternion.LookRotation(-camFwd, Vector3.up);
        Vector3 right = rot * Vector3.right;
        Vector3 up = rot * Vector3.up;

        // 1. Seulement les gemmes à l'écran (marge 1,5 : un quad qui déborde du bord reste dessiné). Les autres existent
        //    et se ramassent normalement, elles ne sont simplement pas dessinées cette image. Caméra orthographique (celle
        //    du jeu) : 2 produits scalaires par gemme ; sinon test sur les 6 plans de la caméra.
        const float Margin = 1.5f;
        int visible = 0;
        if (_camera.orthographic)
        {
            Vector3 camPos = camT.position, camRight = camT.right, camUp = camT.up;
            float halfH = _camera.orthographicSize + Margin;
            float halfW = _camera.orthographicSize * _camera.aspect + Margin;
            for (int i = 0; i < _count; i++)
            {
                Vector3 d = _gems[i].pos - camPos;
                float x = d.x * camRight.x + d.y * camRight.y + d.z * camRight.z;
                if (x > halfW || x < -halfW) continue;
                float y = d.x * camUp.x + d.y * camUp.y + d.z * camUp.z;
                if (y > halfH || y < -halfH) continue;
                _sortOrder[visible] = i;
                _sortKeys[visible++] = -(d.x * camFwd.x + d.y * camFwd.y + d.z * camFwd.z);
            }
        }
        else
        {
            GeometryUtility.CalculateFrustumPlanes(_camera, _frustum);
            for (int i = 0; i < _count; i++)
            {
                Vector3 p = _gems[i].pos;
                bool inside = true;
                for (int f = 0; f < 6 && inside; f++) inside = _frustum[f].GetDistanceToPoint(p) > -Margin;
                if (!inside) continue;
                _sortOrder[visible] = i;
                _sortKeys[visible++] = -Vector3.Dot(p, camFwd);
            }
        }

        // 2. Du plus loin au plus proche : les gemmes qui se chevauchent se recouvrent dans le bon ordre.
        System.Array.Sort(_sortKeys, _sortOrder, 0, visible);

        // 3. Quads face caméra, ancrés par le bas (jamais sous le sol).
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        const float Third = 1f / 3f;
        for (int k = 0; k < visible; k++)
        {
            ref Gem g = ref _gems[_sortOrder[k]];
            float baseScale = XPGem.ScaleOf(g.type);
            float scale = baseScale;
            float glow;
            Vector3 pos = g.pos;
            switch (g.state)
            {
                case GemState.Popping:
                {
                    float t = Mathf.Clamp01(g.timer / g.popDuration);
                    scale = baseScale * Mathf.Max(0f, FXSprites.EaseOutBack(t, 1.5f));
                    glow = Mathf.Lerp(MinGlow, MaxGlow, 0.6f);
                    break;
                }
                case GemState.Attracted:
                    glow = Mathf.Lerp(MinGlow, MaxGlow, 0.5f + 0.5f * Mathf.Sin(g.timeAlive * BasePulseSpeed * g.pulseMul * 1.6f));
                    break;
                default:
                {
                    float bob = Mathf.Sin(g.timeAlive * IdleBobSpeed + g.phase) * IdleBobAmplitude;
                    if (bob > 0f) pos.y += bob;
                    glow = Mathf.Lerp(MinGlow, MaxGlow, 0.5f + 0.5f * Mathf.Sin(g.timeAlive * BasePulseSpeed * g.pulseMul + g.phase));
                    break;
                }
            }

            Vector3 r = right * (HalfWidth * scale);
            Vector3 u = up * (HalfHeight * 2f * scale);
            min = Vector3.Min(min, pos);
            max = Vector3.Max(max, pos);

            float u0 = (int)g.type * Third, u1 = u0 + Third;
            byte c = (byte)Mathf.Clamp((int)(glow / MaxGlow * 255f + 0.5f), 0, 255);
            Color32 col = new Color32(c, c, c, 255);
            int v = k * 4;
            _verts[v] = new Vertex { pos = pos - r, color = col, uv = new Vector2(u0, 0f) };
            _verts[v + 1] = new Vertex { pos = pos + r, color = col, uv = new Vector2(u1, 0f) };
            _verts[v + 2] = new Vertex { pos = pos + r + u, color = col, uv = new Vector2(u1, 1f) };
            _verts[v + 3] = new Vertex { pos = pos - r + u, color = col, uv = new Vector2(u0, 1f) };
        }

        _mesh.SetVertexBufferData(_verts, 0, 0, visible * 4, 0, FastUpdate);
        // Boîte réelle des gemmes dessinées (+ marge pour la taille des quads) : sert au découpage caméra ET au tri des
        // objets transparents par distance. Centrée sur la caméra, elle faisait passer les gemmes pour l'objet le plus
        // proche et les dessinait par-dessus le voile du passage de niveau.
        Bounds b = visible > 0 ? new Bounds((min + max) * 0.5f, max - min + Vector3.one * 3f) : new Bounds(camT.position, Vector3.zero);
        _mesh.SetSubMesh(0, new SubMeshDescriptor(0, visible * 6) { bounds = b, firstVertex = 0, vertexCount = visible * 4 }, FastUpdate);
        _mesh.bounds = b;
    }

    // Tampons du maillage dimensionnés par puissance de 2 : réalloués seulement quand le nombre de gemmes double.
    private void EnsureCapacity(int gems)
    {
        if (_capacity >= gems && _capacity > 0) return;
        int cap = Mathf.Max(512, Mathf.NextPowerOfTwo(gems));
        _capacity = cap;
        _verts = new Vertex[cap * 4];
        _sortKeys = new float[cap];
        _sortOrder = new int[cap];

        int[] indices = new int[cap * 6];
        for (int q = 0; q < cap; q++)
        {
            int v = q * 4, t = q * 6;
            indices[t] = v; indices[t + 1] = v + 1; indices[t + 2] = v + 2;
            indices[t + 3] = v; indices[t + 4] = v + 2; indices[t + 5] = v + 3;
        }
        _mesh.Clear();
        _mesh.SetVertexBufferParams(cap * 4, VertexLayout);
        _mesh.SetIndexBufferParams(cap * 6, IndexFormat.UInt32);
        _mesh.SetIndexBufferData(indices, 0, 0, cap * 6, FastUpdate);
        _mesh.subMeshCount = 1;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
        if (_mesh != null) Destroy(_mesh);
        if (_material != null) Destroy(_material);
        if (_atlas != null && _atlas != Texture2D.whiteTexture) Destroy(_atlas);
    }
}
