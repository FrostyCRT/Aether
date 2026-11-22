using System.Collections.Generic;
using UnityEngine;

// AJOUTÉ (2026-11-22, retour utilisateur : « un event entre chaque phase de boss pour garder du dynamisme ») - GOBELIN DORÉ,
// l'événement du début de partie (Classique : 2:30, avant l'Éclipse de 6:30 et la Tempête de cristaux de 13:30).
//   1. Un gobelin apparaît à 15-20 m du joueur, doré métallique et brillant (halo, lumière, étincelles), animation de course
//      accélérée. Bannière + flèche de bord d'écran tant qu'il est hors champ.
//   2. Pendant Duration secondes, il FUIT au lieu de tirer (EnemyShooter.UpdateGoldenFlee), plus rapide qu'un gobelin normal mais
//      moins que le joueur, et sème des pièces (CoinGold or chacune, ramassées en marchant dessus, CoinLifetime s au sol).
//   3. Tué : prime de CaptureGold or (palier de difficulté appliqué), ligne « Gobelin doré attrapé » sur l'écran de fin.
//   4. Pas tué à temps : il perd sa dorure, retrouve vitesse, PV (même part de vie) et tir, et redevient un gobelin normal.
// Il a HealthFactor fois les PV d'un gobelin du moment : il faut lui tirer dessus en le poursuivant plusieurs secondes. Son XP
// et son or de kill restent ceux d'un gobelin (courbe d'XP du début de partie intouchable).
// Visée auto : à moins de PriorityRange m du joueur, les armes à visée le prennent pour cible en priorité (PriorityTarget).
// Une fois par partie en Classique, une fois par cycle de boss en Sans fin. Jamais en Escarmouche (déjà l'Éclipse et la
// Frénésie en 5 min), Ruée de boss, Choc des titans ni dans la partie d'entraînement (voir WaveManager.Start).
// Tout est construit par le code. Minuteurs en Time.deltaTime : figés pendant les pauses et les choix de carte.
public class GoldenGoblinEvent : MonoBehaviour
{
    public static GoldenGoblinEvent Instance { get; private set; }

    // ---- Réglages (validés par l'utilisateur le 2026-11-22) ---------------------------------------------------------------
    public const float RunSpeed = 3.5f;                 // gobelin normal 2, joueur 5
    public const float NearDistance = 12f, FarDistance = 22f, FarSpeedFactor = 0.35f;   // ralentit quand le joueur est loin
    private const float Duration = 30f;
    private const float HealthFactor = 15f;
    private const float AnimSpeed = 1.8f;
    private const int CaptureGold = 750;
    private const int CoinGold = 15;
    private const float CoinInterval = 1.5f, CoinLifetime = 15f, CoinPickupRadius = 1.8f, CoinMagnetRadius = 3.2f;
    private const float PriorityRange = 10f;
    private const float SpawnDistMin = 15f, SpawnDistMax = 20f;
    private const float WallInset = 3f;
    private const string GoblinTag = "EnemyShooter";

    public static readonly Color Gold = new Color(1f, 0.8f, 0.25f);
    private static readonly Color GoldLight = new Color(1f, 0.85f, 0.45f);

    private enum Phase { Waiting, Running, Done }
    private Phase _phase = Phase.Waiting;
    private int _index;
    private float _timer, _coinTimer, _sparkTimer;
    private Transform _player;
    private EnemySpawner _spawner;

    private EnemyBase _goblin;
    private int _goblinLife;
    private Renderer _goblinRenderer;
    private Material _goblinOriginalMat;
    private VATAnimatedRenderer _goblinVat;
    private GameObject _fx;
    private SpriteRenderer _halo;
    private Light _light;

    private TutorialPointer _pointer;
    private readonly List<Vector3> _arrowList = new List<Vector3>(1);

    private bool Repeats => GameModes.IsEndless;
    private float TriggerTime(int index)
    {
        float interval = WaveManager.Instance != null ? WaveManager.Instance.BossSpawnInterval : 300f;
        return interval * (index + 0.5f);   // 2:30 (Sans fin : puis 7:30, 12:30...)
    }

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        RestoreLook();
        if (_goblin != null) _goblin.SetGoldenRunner(false);
        _goblin = null;
        if (_pointer != null) Destroy(_pointer.gameObject);
        foreach (Coin c in _coins) if (c.go != null) Destroy(c.go);
        _coins.Clear();
        foreach (GameObject g in _coinPool) if (g != null) Destroy(g);
        _coinPool.Clear();
    }

    private void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        if (gm.IsGameOver)
        {
            if (_phase == Phase.Running) EndSilently();
            _phase = Phase.Done;
            return;
        }
        if (gm.IsPaused)
        {
            if (_pointer != null) _pointer.Hide();
            return;
        }

        if (_player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) _player = p.transform;
            if (_player == null) return;
        }

        UpdateCoins(Time.deltaTime);

        bool bossAlive = WaveManager.Instance != null && WaveManager.Instance.BossAlive;
        switch (_phase)
        {
            case Phase.Waiting:
                if (bossAlive || gm.RunTimer < TriggerTime(_index)) break;
                if (TimeBeforeBoss() < Duration + 10f) { NextOrDone(); break; }   // trop tard dans ce cycle : il serait coupé
                if (GameModes.NightActive) break;                                 // Sans fin : jamais pendant l'Éclipse
                Spawn();
                break;
            case Phase.Running:
                if (bossAlive) { EndSilently(); break; }
                UpdateRun(Time.deltaTime);
                break;
        }
    }

    private float TimeBeforeBoss()
    {
        if (WaveManager.Instance == null || GameManager.Instance == null) return float.MaxValue;
        return WaveManager.Instance.NextBossTime - GameManager.Instance.RunTimer;
    }

    private void NextOrDone()
    {
        _index++;
        _phase = Repeats ? Phase.Waiting : Phase.Done;
    }

    // =========================================================================================================================
    //  APPARITION / FIN
    // =========================================================================================================================

    private void Spawn()
    {
        if (_spawner == null) _spawner = FindFirstObjectByType<EnemySpawner>();
        GameObject go = _spawner != null ? _spawner.SpawnSpecific(GoblinTag, PickPosition()) : null;
        EnemyBase e = go != null ? go.GetComponent<EnemyBase>() : null;
        if (e == null) { NextOrDone(); return; }

        _goblin = e;
        _goblinLife = e.Life;
        e.SetGoldenRunner(true);
        e.ScaleCurrentHealth(HealthFactor);
        ApplyLook(e);

        _timer = 0f;
        _coinTimer = 0.8f;
        _sparkTimer = 0f;
        _phase = Phase.Running;
        if (_pointer == null) _pointer = TutorialPointer.Create();
        ImpactSparkVFX.Spawn(e.transform.position + Vector3.up, Gold, 1.6f);
        if (GameUI.Instance != null)
            GameUI.Instance.ShowEventBanner(Loc.T("GOBELIN DORÉ !"), Gold, Loc.T("Rattrapez-le avant qu'il ne s'échappe !"), 2.2f);
    }

    // 15-20 m dans une direction au hasard, gardé dans la carte ; on prend le plus éloigné si la carte est étroite.
    private Vector3 PickPosition()
    {
        Vector3 origin = _player.position;
        origin.y = 0f;
        Vector3 best = KeepInside(origin + Vector3.forward * SpawnDistMin);
        float bestDist = -1f;
        for (int i = 0; i < 24; i++)
        {
            Vector2 d = Random.insideUnitCircle.normalized;
            if (d.sqrMagnitude < 0.01f) d = Vector2.right;
            Vector3 cand = KeepInside(origin + new Vector3(d.x, 0f, d.y) * Random.Range(SpawnDistMin, SpawnDistMax));
            float dist = Vector3.Distance(cand, origin);
            if (dist >= SpawnDistMin * 0.9f) return cand;
            if (dist > bestDist) { bestDist = dist; best = cand; }
        }
        return best;
    }

    private bool GoblinValid => _goblin != null && _goblin.gameObject.activeInHierarchy && _goblin.Life == _goblinLife && _goblin.IsGoldenRunner;

    private void UpdateRun(float dt)
    {
        if (!GoblinValid) { EndSilently(); return; }
        _timer += dt;
        Vector3 pos = _goblin.transform.position;

        if (_pointer != null)
        {
            _arrowList.Clear();
            _arrowList.Add(pos + Vector3.up * 1.5f);
            _pointer.Show(_arrowList);
        }

        AnimateFx(pos);

        _coinTimer -= dt;
        if (_coinTimer <= 0f)
        {
            _coinTimer = CoinInterval;
            DropCoin(pos - _goblin.transform.forward * 0.6f);
        }

        if (_timer >= Duration) Escape();
    }

    // Pas tué à temps : redevient un gobelin normal (la même part de vie, sa vitesse, son tir).
    private void Escape()
    {
        EnemyBase e = _goblin;
        RestoreLook();
        e.SetGoldenRunner(false);
        e.ScaleCurrentHealth(1f / HealthFactor);
        ImpactSparkVFX.Spawn(e.transform.position + Vector3.up, Gold, 1.2f);
        _goblin = null;
        ClearPointer();
        if (GameUI.Instance != null)
            GameUI.Instance.ShowEventBanner(Loc.T("TROP TARD !"), GoldLight, Loc.T("Le Gobelin doré s'est échappé."), 1.6f);
        NextOrDone();
    }

    // Retiré sans être tué (nettoyage d'arrivée de boss, fin de partie) : pas de bannière.
    private void EndSilently()
    {
        RestoreLook();
        if (_goblin != null && _goblin.Life == _goblinLife) _goblin.SetGoldenRunner(false);
        _goblin = null;
        ClearPointer();
        if (_phase == Phase.Running) NextOrDone();
    }

    private void ClearPointer()
    {
        if (_pointer != null) { Destroy(_pointer.gameObject); _pointer = null; }
    }

    // Appelé par EnemyBase.Die, juste avant le retour au pool (la copie qui se dissout a déjà pris le matériau doré).
    public static void NotifyKilled(EnemyBase e)
    {
        if (Instance == null || e != Instance._goblin) return;
        Instance.OnKilled(e);
    }

    // Appelé par EnemyBase.OnDisable quand le gobelin doré quitte la partie sans être tué.
    public static void NotifyLost(EnemyBase e)
    {
        if (Instance == null || e != Instance._goblin) return;
        Instance.EndSilently();
    }

    private void OnKilled(EnemyBase e)
    {
        Vector3 pos = e.transform.position;
        int added = MetaProgressionManager.Instance != null ? MetaProgressionManager.Instance.AddGoldenGoblinGold(CaptureGold, true) : 0;
        RestoreLook();
        _goblin = null;
        ClearPointer();

        ImpactSparkVFX.Spawn(pos + Vector3.up, Gold, 2.6f);
        ImpactSparkVFX.Spawn(pos + Vector3.up * 0.4f, GoldLight, 1.8f);
        CameraShakeFX.Shake(0.22f);
        for (int i = 0; i < 14; i++) BurstCoin(pos);
        if (GameUI.Instance != null)
            GameUI.Instance.ShowEventBanner(Loc.T("GOBELIN DORÉ ATTRAPÉ !"), Gold, ChallengeManager.FormatRewardGold(added), 2f);
        NextOrDone();
    }

    // =========================================================================================================================
    //  LOOK DORÉ
    // =========================================================================================================================

    private void ApplyLook(EnemyBase e)
    {
        _goblinVat = e.GetComponentInChildren<VATAnimatedRenderer>();
        _goblinRenderer = _goblinVat != null ? _goblinVat.GetComponent<Renderer>() : e.GetComponentInChildren<Renderer>();
        if (_goblinRenderer != null)
        {
            _goblinOriginalMat = _goblinRenderer.sharedMaterial;
            _goblinRenderer.sharedMaterial = GoldMaterial(_goblinOriginalMat);
        }
        if (_goblinVat != null) _goblinVat.PlaybackSpeed = AnimSpeed;

        _fx = new GameObject("GoldenGoblinFX");
        _fx.transform.SetParent(e.transform, false);
        _halo = FXSprites.MakeRenderer(_fx.transform, "Halo", FXSprites.Glow(), 40, 0.12f);
        FXSprites.SetFlat(_halo, 3.4f, 0f);
        GameObject lightGO = new GameObject("GoldLight");
        lightGO.transform.SetParent(_fx.transform, false);
        lightGO.transform.localPosition = new Vector3(0f, 2f, 0f);
        _light = lightGO.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = GoldLight;
        _light.range = 7f;
        _light.intensity = 2.5f;
        _light.shadows = LightShadows.None;
    }

    private void RestoreLook()
    {
        if (_goblinRenderer != null && _goblinOriginalMat != null) _goblinRenderer.sharedMaterial = _goblinOriginalMat;
        if (_goblinVat != null) _goblinVat.PlaybackSpeed = 1f;
        if (_fx != null) Destroy(_fx);
        _fx = null; _halo = null; _light = null;
        _goblinRenderer = null; _goblinOriginalMat = null; _goblinVat = null;
    }

    private void AnimateFx(Vector3 pos)
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(_timer * 5f);
        // 5 dernières secondes : clignote, il va s'échapper
        float blink = Duration - _timer < 5f ? 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(_timer * 8f)) : 1f;
        if (_halo != null)
        {
            _halo.color = FXSprites.A(Gold, (0.35f + 0.2f * pulse) * blink);
            FXSprites.SetFlat(_halo, 3.2f + 0.5f * pulse, 0f);
        }
        if (_light != null) _light.intensity = (2f + 1.2f * pulse) * blink;

        _sparkTimer -= Time.deltaTime;
        if (_sparkTimer <= 0f)
        {
            _sparkTimer = 0.45f;
            Vector2 r = Random.insideUnitCircle * 0.5f;
            ImpactSparkVFX.Spawn(pos + new Vector3(r.x, Random.Range(0.6f, 1.6f), r.y), GoldLight, 0.45f);
        }
    }

    // Matériau doré tiré de celui du gobelin : sa texture passée en dégradé d'or (les détails restent lisibles), métal brillant,
    // légère émission pour qu'il ressorte aussi sur l'herbe sombre et dans l'Éclipse. Calculé une fois par matériau source.
    private static readonly Dictionary<Material, Material> _goldMats = new Dictionary<Material, Material>();

    private static Material GoldMaterial(Material src)
    {
        if (src == null) return null;
        if (_goldMats.TryGetValue(src, out Material cached) && cached != null) return cached;

        Material m = new Material(src) { name = src.name + " (Gold)" };
        Texture baseTex = src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : null;
        Texture2D gold = GoldTexture(baseTex);
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", gold);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 1f);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.78f);
        if (m.HasProperty("_MetallicGlossMap")) m.SetTexture("_MetallicGlossMap", null);
        m.DisableKeyword("_METALLICSPECGLOSSMAP");
        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", gold);
            m.SetColor("_EmissionColor", new Color(0.32f, 0.24f, 0.1f));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        _goldMats[src] = m;
        return m;
    }

    private static Texture2D GoldTexture(Texture source)
    {
        const int n = 256;
        Color32[] px;
        if (source != null)
        {
            RenderTexture rt = RenderTexture.GetTemporary(n, n, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D read = new Texture2D(n, n, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, n, n), 0, 0);
            read.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            px = read.GetPixels32();
            Destroy(read);
        }
        else
        {
            px = new Color32[n * n];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(160, 160, 160, 255);
        }

        Color dark = new Color(0.42f, 0.26f, 0.05f), mid = new Color(0.95f, 0.7f, 0.2f), bright = new Color(1f, 0.93f, 0.62f);
        for (int i = 0; i < px.Length; i++)
        {
            Color32 c = px[i];
            float lum = (0.3f * c.r + 0.59f * c.g + 0.11f * c.b) / 255f;
            float k = Mathf.Clamp01(lum * 1.6f);
            Color g = k < 0.6f ? Color.Lerp(dark, mid, k / 0.6f) : Color.Lerp(mid, bright, (k - 0.6f) / 0.4f);
            px[i] = new Color32((byte)(g.r * 255f), (byte)(g.g * 255f), (byte)(g.b * 255f), 255);
        }
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "GoldenGoblin", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
        t.SetPixels32(px);
        t.Apply(true, true);
        return t;
    }

    // =========================================================================================================================
    //  PIÈCES
    // =========================================================================================================================

    private class Coin
    {
        public GameObject go;
        public Vector3 pos, velocity;
        public float age;
        public bool burst;   // gerbe de la capture : purement visuelle, vole jusqu'au joueur (la prime est déjà versée)
    }

    private readonly List<Coin> _coins = new List<Coin>();
    private readonly List<GameObject> _coinPool = new List<GameObject>();
    private static Material _coinMat;
    private const float CoinY = 0.55f;

    private void DropCoin(Vector3 pos)
    {
        pos.y = CoinY;
        _coins.Add(new Coin { go = TakeCoinObject(), pos = KeepInside(pos), age = 0f });
    }

    private void BurstCoin(Vector3 pos)
    {
        Vector2 d = Random.insideUnitCircle.normalized * Random.Range(3f, 6f);
        pos.y = 1f;
        _coins.Add(new Coin { go = TakeCoinObject(), pos = pos, velocity = new Vector3(d.x, Random.Range(5f, 8f), d.y), burst = true });
    }

    private GameObject TakeCoinObject()
    {
        GameObject go;
        if (_coinPool.Count > 0)
        {
            go = _coinPool[_coinPool.Count - 1];
            _coinPool.RemoveAt(_coinPool.Count - 1);
            go.SetActive(true);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "GoldCoin";
            Destroy(go.GetComponent<Collider>());
            go.transform.localScale = new Vector3(0.55f, 0.04f, 0.55f);
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = CoinMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        go.GetComponent<Renderer>().enabled = true;
        return go;
    }

    private void ReleaseCoin(int i)
    {
        Coin c = _coins[i];
        if (c.go != null) { c.go.SetActive(false); _coinPool.Add(c.go); }
        _coins.RemoveAt(i);
    }

    private static Material CoinMaterial()
    {
        if (_coinMat != null) return _coinMat;
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        _coinMat = new Material(lit != null ? lit : Shader.Find("Standard")) { name = "GoldCoin" };
        _coinMat.SetColor("_BaseColor", new Color(1f, 0.78f, 0.25f));
        _coinMat.SetFloat("_Metallic", 1f);
        _coinMat.SetFloat("_Smoothness", 0.8f);
        _coinMat.EnableKeyword("_EMISSION");
        _coinMat.SetColor("_EmissionColor", new Color(0.55f, 0.38f, 0.08f));
        return _coinMat;
    }

    private void UpdateCoins(float dt)
    {
        if (_coins.Count == 0) return;
        Vector3 player = _player.position;
        player.y = CoinY;
        for (int i = _coins.Count - 1; i >= 0; i--)
        {
            Coin c = _coins[i];
            c.age += dt;
            Vector3 toPlayer = player - c.pos;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;

            if (c.burst)
            {
                if (c.age < 0.45f)
                {
                    c.velocity.y -= 22f * dt;
                    c.pos += c.velocity * dt;
                }
                else
                {
                    Vector3 to = (player + Vector3.up * 0.6f) - c.pos;
                    float speed = 8f + (c.age - 0.45f) * 30f;
                    if (to.magnitude <= speed * dt + 0.3f)
                    {
                        ImpactSparkVFX.Spawn(player + Vector3.up * 0.6f, GoldLight, 0.35f);
                        ReleaseCoin(i);
                        continue;
                    }
                    c.pos += to.normalized * speed * dt;
                }
            }
            else
            {
                if (dist <= CoinPickupRadius)
                {
                    if (MetaProgressionManager.Instance != null) MetaProgressionManager.Instance.AddGoldenGoblinGold(CoinGold, false);
                    ImpactSparkVFX.Spawn(c.pos, GoldLight, 0.5f);
                    ReleaseCoin(i);
                    continue;
                }
                if (dist <= CoinMagnetRadius) c.pos += toPlayer / dist * (9f * dt);   // petit aimant : la pièce glisse vers le joueur
                if (c.age >= CoinLifetime) { ReleaseCoin(i); continue; }
            }

            float bob = c.burst ? 0f : Mathf.Sin(c.age * 4f) * 0.12f;
            c.go.transform.position = c.pos + Vector3.up * bob;
            c.go.transform.rotation = Quaternion.Euler(0f, c.age * 220f, 0f) * Quaternion.Euler(90f, 0f, 0f);   // debout, tourne sur elle-même
            if (!c.burst && CoinLifetime - c.age < 3f)
                c.go.GetComponent<Renderer>().enabled = Mathf.Repeat(c.age * 6f, 1f) > 0.35f;   // clignote avant de disparaître
        }
    }

    // =========================================================================================================================
    //  OUTILS PARTAGÉS (EnemyShooter, armes)
    // =========================================================================================================================

    // Cible prioritaire des armes à visée auto : le gobelin doré s'il est à moins de PriorityRange m (et de la portée de l'arme).
    public static Transform PriorityTarget(Vector3 from, float weaponRange)
    {
        GoldenGoblinEvent ev = Instance;
        if (ev == null || ev._phase != Phase.Running || !ev.GoblinValid) return null;
        Vector3 d = ev._goblin.transform.position - from;
        d.y = 0f;
        float range = Mathf.Min(PriorityRange, weaponRange);
        return d.sqrMagnitude <= range * range ? ev._goblin.transform : null;
    }

    // Garde un point à WallInset m des murs (Violent : couloir, Apocalyptique : boîte ; Modéré : aucun mur).
    public static Vector3 KeepInside(Vector3 p)
    {
        if (!float.IsInfinity(MapBoundaryUtils.MinX)) p.x = Mathf.Max(p.x, MapBoundaryUtils.MinX + WallInset);
        if (!float.IsInfinity(MapBoundaryUtils.MaxX)) p.x = Mathf.Min(p.x, MapBoundaryUtils.MaxX - WallInset);
        if (!float.IsInfinity(MapBoundaryUtils.MinZ)) p.z = Mathf.Max(p.z, MapBoundaryUtils.MinZ + WallInset);
        if (!float.IsInfinity(MapBoundaryUtils.MaxZ)) p.z = Mathf.Min(p.z, MapBoundaryUtils.MaxZ - WallInset);
        return p;
    }

    // Détour des murs pour la fuite : repousse vers l'intérieur à l'approche d'un mur, et fait longer le mur du côté qui
    // s'éloigne du joueur, au lieu de s'y écraser.
    public static Vector3 WallAvoidance(Vector3 pos, Vector3 away)
    {
        const float range = 6f;
        Vector3 push = Vector3.zero;
        if (!float.IsInfinity(MapBoundaryUtils.MinX)) push.x += Mathf.Clamp01(1f - (pos.x - MapBoundaryUtils.MinX - WallInset) / range);
        if (!float.IsInfinity(MapBoundaryUtils.MaxX)) push.x -= Mathf.Clamp01(1f - (MapBoundaryUtils.MaxX - WallInset - pos.x) / range);
        if (!float.IsInfinity(MapBoundaryUtils.MinZ)) push.z += Mathf.Clamp01(1f - (pos.z - MapBoundaryUtils.MinZ - WallInset) / range);
        if (!float.IsInfinity(MapBoundaryUtils.MaxZ)) push.z -= Mathf.Clamp01(1f - (MapBoundaryUtils.MaxZ - WallInset - pos.z) / range);
        float m = push.magnitude;
        if (m < 0.001f) return Vector3.zero;
        Vector3 tangent = new Vector3(-push.z, 0f, push.x) / m;
        if (Vector3.Dot(tangent, away) < 0f) tangent = -tangent;
        return (push * 1.5f) + tangent * (m * 1.5f);
    }

    // Test / debug (RunCommand) : fait apparaître le gobelin doré tout de suite.
    public void DebugSpawnNow()
    {
        if (_phase == Phase.Running) return;
        if (_player == null) { GameObject p = GameObject.FindWithTag("Player"); if (p != null) _player = p.transform; }
        if (_player != null) Spawn();
    }

    public bool DebugRunning => _phase == Phase.Running;
    public EnemyBase DebugGoblin => _goblin;
    public float DebugTimeLeft => Duration - _timer;
}
