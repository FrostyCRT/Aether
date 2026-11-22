using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// AJOUTÉ (2026-11-22, retour utilisateur : « un event entre chaque phase de boss » ; remplace la Frénésie en Classique, jugée trop
// proche de l'Éclipse) - TEMPÊTE DE CRISTAUX, l'événement de fin de partie (Classique : 13:30 -> 14:45, avant le Boss 3).
// (Nom « Tempête d'Aether » écarté : confusion avec le personnage Aether.)
//   1. Bannière, voile violet sur les bords de l'écran.
//   2. Par salves régulières (de plus en plus serrées), des SCEAUX DE CRISTAL s'allument au sol : étoile à six branches qui se
//      remplit en WarningTime secondes, colonne de lumière qui descend du ciel. Un sceau vise le joueur (là où il va) une salve
//      sur deux pendant la première moitié, puis à chaque salve ; les autres tombent sur des groupes d'ennemis proches.
//   3. Le sceau plein, une météorite de cristal s'écrase : blesse le joueur (PlayerDamageFraction de ses PV max, esquivable au
//      dash), écrase TOUS les ennemis courants dans la zone. Le jeu : esquiver, et attirer la horde sous les sceaux.
//   4. Fin après Duration (ou dès qu'un boss apparaît) : bannière avec le nombre d'ennemis écrasés.
// Le sceau a sa propre forme (étoile de cristal) : jamais les anneaux / disques des télégraphes ennemis, ni les éclats du joueur
// (voir la grammaire visuelle dans NOTES.md).
// Modèle de la météorite : Resources/CrystalStorm/Meteor (prefab, à fournir par l'utilisateur via Tripo, pointe vers le haut,
// ~1 m de haut, pivot au pied) ; en attendant, un cristal facetté construit par le code.
// Classique : une fois. Sans fin : une fois par cycle de boss (13:30, 18:30...). Jamais en Escarmouche, Ruée, Titans,
// entraînement (voir WaveManager.Start). Minuteurs en Time.deltaTime : figés pendant les pauses et les choix de carte.
public class CrystalStormEvent : MonoBehaviour
{
    public static CrystalStormEvent Instance { get; private set; }

    // ---- Réglages -----------------------------------------------------------------------------------------------------------
    private const float Duration = 75f;
    private const float WarningTime = 1.5f;
    private const float FallTime = 0.28f;            // la météorite tombe pendant la fin de l'alerte
    private const float Radius = 2.6f;
    private const float PlayerDamageFraction = 0.15f;   // 20 % au premier essai : trop punitif avec un tir visé à chaque salve
    private const float WaveIntervalStart = 1.9f, WaveIntervalEnd = 1.1f;
    private const float EnemySearchRadius = 14f, MinStrikeSpacing = 3.6f;
    private const float FadeIn = 3f, FadeOut = 3f;
    private const float StuckTime = 1.1f;            // la météorite reste plantée avant de se briser

    public static readonly Color StormViolet = new Color(0.78f, 0.45f, 1f);
    private static readonly Color StormPale = new Color(0.9f, 0.8f, 1f);

    private enum Phase { Waiting, Storm, Done }
    private Phase _phase = Phase.Waiting;
    private int _index;
    private float _timer, _waveTimer, _duration;
    private int _kills, _wave;
    private Transform _player;
    private HealthSystem _playerHealth;
    private Vector3 _lastPlayerPos, _playerVelocity;

    private GameObject _overlay;
    private Image _overlayImage;

    private bool Repeats => GameModes.IsEndless;
    private float TriggerTime(int index)
    {
        float interval = WaveManager.Instance != null ? WaveManager.Instance.BossSpawnInterval : 300f;
        return interval * (index + 2) + interval * 0.7f;   // 13:30 (Sans fin : puis 18:30, 23:30...)
    }

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        ClearStrikes();
        if (_overlay != null) Destroy(_overlay);
    }

    private void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || _phase == Phase.Done) return;

        if (gm.IsGameOver)
        {
            if (_phase == Phase.Storm) EndStorm(false);
            _phase = Phase.Done;
            return;
        }
        if (gm.IsPaused) return;

        if (_player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) { _player = p.transform; _playerHealth = p.GetComponent<HealthSystem>(); _lastPlayerPos = _player.position; }
            if (_player == null) return;
        }

        float dt = Time.deltaTime;
        if (dt > 0f)
        {
            Vector3 v = (_player.position - _lastPlayerPos) / dt;
            v.y = 0f;
            _playerVelocity = Vector3.Lerp(_playerVelocity, Vector3.ClampMagnitude(v, 12f), Mathf.Clamp01(dt * 6f));
        }
        _lastPlayerPos = _player.position;

        bool bossAlive = WaveManager.Instance != null && WaveManager.Instance.BossAlive;
        switch (_phase)
        {
            case Phase.Waiting:
                if (bossAlive || gm.RunTimer < TriggerTime(_index)) break;
                float left = TimeBeforeBoss() - 10f;          // 10 s de calme avant le boss
                if (left < 20f) { NextOrDone(); break; }
                if (GameModes.NightActive) break;              // Sans fin : jamais pendant l'Éclipse
                StartStorm(Mathf.Min(Duration, left));
                break;
            case Phase.Storm:
                if (bossAlive) { EndStorm(false); break; }
                UpdateStorm(dt);
                break;
        }
        UpdateStrikes(dt);
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
    //  LA TEMPÊTE
    // =========================================================================================================================

    private void StartStorm(float duration)
    {
        _phase = Phase.Storm;
        _duration = duration;
        _timer = 0f;
        _waveTimer = 2.2f;   // première salve après la bannière
        _kills = 0;
        _wave = 0;
        BuildOverlay();
        if (GameUI.Instance != null)
            GameUI.Instance.ShowEventBanner(Loc.T("TEMPÊTE DE CRISTAUX"), StormViolet,
                Loc.T("Évitez les sceaux au sol et attirez-y les ennemis !"), 2.4f);
    }

    private void UpdateStorm(float dt)
    {
        _timer += dt;
        float k = Mathf.Min(Mathf.Clamp01(_timer / FadeIn), Mathf.Clamp01((_duration - _timer) / FadeOut));
        if (_overlayImage != null) _overlayImage.color = FXSprites.A(new Color(0.35f, 0.12f, 0.55f), 0.55f * k);

        float progress = Mathf.Clamp01(_timer / _duration);
        _waveTimer -= dt;
        if (_waveTimer <= 0f && _duration - _timer > WarningTime + 0.5f)
        {
            _waveTimer = Mathf.Lerp(WaveIntervalStart, WaveIntervalEnd, progress);
            SpawnWave(progress < 0.4f ? 2 : 3, progress >= 0.5f || _wave % 2 == 0);
            _wave++;
        }
        if (_timer >= _duration) EndStorm(true);
    }

    private void EndStorm(bool announce)
    {
        if (_overlay != null) { Destroy(_overlay); _overlay = null; _overlayImage = null; }
        if (!announce) ClearStrikes();   // coupure (boss, mort) : plus rien ne tombe
        if (announce && GameUI.Instance != null)
            GameUI.Instance.ShowEventBanner(Loc.T("LA TEMPÊTE SE CALME"), StormPale,
                _kills > 0 ? Loc.F("{0} ennemis écrasés", Loc.Int(_kills)) : null, 1.6f);
        NextOrDone();
    }

    private static readonly Collider[] _buffer = new Collider[128];
    private readonly List<Vector3> _waveTargets = new List<Vector3>(4);

    private void SpawnWave(int count, bool aimAtPlayer)
    {
        _waveTargets.Clear();
        Vector3 origin = _player.position;
        origin.y = 0f;

        // 1) là où le joueur sera à l'impact (une partie de son élan), un peu au hasard
        if (aimAtPlayer)
        {
            Vector2 jitter = Random.insideUnitCircle * 1.2f;
            Vector3 aimed = origin + _playerVelocity * (WarningTime * 0.6f) + new Vector3(jitter.x, 0f, jitter.y);
            TryAddTarget(GoldenGoblinEvent.KeepInside(aimed));
        }

        // 2) sur des ennemis proches (les groupes sont plus souvent tirés, ils ont plus d'ennemis)
        int n = Physics.OverlapSphereNonAlloc(origin, EnemySearchRadius, _buffer, EnemyLayers.Mask);
        for (int tries = 0; tries < 12 && _waveTargets.Count < count && n > 0; tries++)
        {
            Collider col = _buffer[Random.Range(0, n)];
            if (col == null || !col.CompareTag("Enemy")) continue;
            Vector3 p = col.transform.position;
            p.y = 0f;
            if ((p - origin).sqrMagnitude < 4f * 4f) continue;   // pas collé au joueur : celui-là, c'est le tir visé
            TryAddTarget(GoldenGoblinEvent.KeepInside(p));
        }

        // 3) repli : au hasard autour du joueur
        for (int tries = 0; tries < 12 && _waveTargets.Count < count; tries++)
        {
            Vector2 d = Random.insideUnitCircle.normalized * Random.Range(4f, 12f);
            TryAddTarget(GoldenGoblinEvent.KeepInside(origin + new Vector3(d.x, 0f, d.y)));
        }

        for (int i = 0; i < _waveTargets.Count; i++) SpawnStrike(_waveTargets[i], i * 0.12f);
    }

    private void TryAddTarget(Vector3 p)
    {
        float min = MinStrikeSpacing * MinStrikeSpacing;
        foreach (Vector3 t in _waveTargets) if ((t - p).sqrMagnitude < min) return;
        foreach (Strike s in _strikes) if (s.age < WarningTime && (s.center - p).sqrMagnitude < min) return;
        _waveTargets.Add(p);
    }

    // =========================================================================================================================
    //  FRAPPES
    // =========================================================================================================================

    private class Strike
    {
        public Vector3 center;
        public float age;              // < 0 : décalage avant d'apparaître
        public bool impacted;
        public GameObject root;
        public SpriteRenderer sigil, fill, glow, column, meteorGlow;
        public Transform meteor;
        public Vector3 fallFrom;
        public float spin;
        public Light flash;
    }

    private readonly List<Strike> _strikes = new List<Strike>();

    private void SpawnStrike(Vector3 center, float delay)
    {
        Strike s = new Strike { center = center, age = -delay, spin = Random.Range(0f, 60f) };
        s.root = new GameObject("CrystalStrike");
        s.root.transform.position = center;
        s.glow = FXSprites.MakeRenderer(s.root.transform, "Glow", FXSprites.Glow(), 38, 0.1f);
        s.fill = FXSprites.MakeRenderer(s.root.transform, "Fill", SigilFill(), 39, 0.12f);
        s.sigil = FXSprites.MakeRenderer(s.root.transform, "Sigil", Sigil(), 40, 0.14f);
        s.column = FXSprites.MakeRenderer(s.root.transform, "Column", FXSprites.Pillar(), 41, 0.2f);

        s.meteor = BuildMeteor(s.root.transform);
        s.meteor.gameObject.SetActive(false);
        s.meteorGlow = FXSprites.MakeRenderer(s.meteor, "MeteorGlow", FXSprites.Glow(), 42, 0.5f);
        Vector2 side = Random.insideUnitCircle.normalized * 5f;
        s.fallFrom = center + new Vector3(side.x, 22f, side.y);
        _strikes.Add(s);
        AnimateStrike(s);
    }

    private void UpdateStrikes(float dt)
    {
        for (int i = _strikes.Count - 1; i >= 0; i--)
        {
            Strike s = _strikes[i];
            s.age += dt;
            if (!s.impacted && s.age >= WarningTime) Impact(s);
            if (s.age >= WarningTime + StuckTime + 0.35f)
            {
                if (s.root != null) Destroy(s.root);
                _strikes.RemoveAt(i);
                continue;
            }
            AnimateStrike(s);
        }
    }

    private void ClearStrikes()
    {
        foreach (Strike s in _strikes) if (s.root != null) Destroy(s.root);
        _strikes.Clear();
    }

    private void AnimateStrike(Strike s)
    {
        if (s.root == null) return;
        bool visible = s.age >= 0f;
        s.root.SetActive(visible);
        if (!visible) return;

        Camera cam = Camera.main;
        Quaternion face = cam != null ? cam.transform.rotation : Quaternion.identity;
        float t = s.age;
        float diameter = Radius * 2f / 0.86f;   // pointes de l'étoile = bord de la zone de dégâts

        if (t < WarningTime)
        {
            float k = t / WarningTime;
            float appear = FXSprites.EaseOutBack(Mathf.Clamp01(t / 0.25f));
            float urgency = 0.5f + 0.5f * Mathf.Sin(t * Mathf.Lerp(8f, 26f, k));   // clignote de plus en plus vite
            FXSprites.SetFlat(s.sigil, diameter * appear, s.spin + t * 25f);
            s.sigil.color = FXSprites.A(Color.Lerp(StormViolet, Color.white, 0.25f * urgency), 0.75f + 0.25f * urgency);
            FXSprites.SetFlat(s.fill, diameter * Mathf.Max(0.05f, k), s.spin + t * 25f);   // l'étoile se remplit : quand elle est pleine, ça tombe
            s.fill.color = FXSprites.A(StormViolet, 0.28f + 0.2f * k);
            FXSprites.SetFlat(s.glow, diameter * 1.6f, 0f);
            s.glow.color = FXSprites.A(StormViolet, 0.18f + 0.22f * k);

            float colK = Mathf.Clamp01((t - 0.3f) / (WarningTime - 0.3f));
            s.column.transform.rotation = face;
            s.column.transform.localScale = new Vector3(0.9f / s.column.sprite.bounds.size.x * (0.6f + 0.6f * colK), 20f / s.column.sprite.bounds.size.y, 1f);
            s.column.color = FXSprites.A(StormPale, 0.35f * colK);

            float fallStart = WarningTime - FallTime;
            if (t >= fallStart)
            {
                float f = (t - fallStart) / FallTime;
                s.meteor.gameObject.SetActive(true);
                s.meteor.position = Vector3.Lerp(s.fallFrom, s.center, f * f);
                Vector3 dir = (s.center - s.fallFrom).normalized;
                s.meteor.rotation = Quaternion.FromToRotation(Vector3.up, -dir);   // pointe vers le ciel d'où il vient
                s.meteorGlow.transform.rotation = face;
                s.meteorGlow.transform.localScale = Vector3.one * (3f / Mathf.Max(0.01f, s.meteorGlow.sprite.bounds.size.x));
                s.meteorGlow.color = FXSprites.A(StormViolet, 0.8f);
            }
        }
        else
        {
            float u = t - WarningTime;                        // après l'impact
            float fade = 1f - Mathf.Clamp01(u / 0.35f);
            FXSprites.SetFlat(s.sigil, diameter * (1f + 0.25f * (1f - fade)), s.spin + WarningTime * 25f);
            s.sigil.color = FXSprites.A(Color.white, fade);
            FXSprites.SetFlat(s.fill, diameter, s.spin + WarningTime * 25f);
            s.fill.color = FXSprites.A(StormPale, 0.6f * fade);
            s.glow.color = FXSprites.A(StormViolet, 0.4f * fade);
            s.column.color = FXSprites.A(StormPale, 0.5f * fade);
            if (s.meteorGlow != null) s.meteorGlow.color = FXSprites.A(StormViolet, 0.5f * fade);

            // planté dans le sol, puis se brise
            float shrink = Mathf.Clamp01((u - StuckTime) / 0.3f);
            if (s.meteor != null) s.meteor.localScale = Vector3.one * MeteorScale * (1f - FXSprites.Smooth(shrink));
            if (s.flash != null) s.flash.intensity = 7f * Mathf.Clamp01(1f - u / 0.3f);
        }
    }

    private void Impact(Strike s)
    {
        s.impacted = true;
        Vector3 c = s.center;
        if (s.meteor != null)
        {
            s.meteor.gameObject.SetActive(true);
            s.meteor.position = c + Vector3.down * 0.25f;   // un peu enfoncé
        }

        GameObject lightGO = new GameObject("StrikeFlash");
        lightGO.transform.SetParent(s.root.transform, false);
        lightGO.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        s.flash = lightGO.AddComponent<Light>();
        s.flash.type = LightType.Point;
        s.flash.color = StormViolet;
        s.flash.range = 8f;
        s.flash.intensity = 7f;
        s.flash.shadows = LightShadows.None;

        ImpactSparkVFX.Spawn(c + Vector3.up * 0.5f, StormViolet, 2.3f);
        ImpactSparkVFX.Spawn(c + Vector3.up * 0.3f, StormPale, 1.3f);

        // joueur
        if (_player != null)
        {
            Vector3 d = _player.position - c;
            d.y = 0f;
            if (d.sqrMagnitude <= Radius * Radius && _playerHealth != null)
                _playerHealth.TakeDamage(_playerHealth.MaxHealth * PlayerDamageFraction, "horde");
            if (d.sqrMagnitude <= 14f * 14f) CameraShakeFX.Shake(d.sqrMagnitude <= Radius * Radius ? 0.3f : 0.12f);
        }

        // ennemis courants : écrasés (les boss n'apparaissent jamais pendant la tempête)
        int n = Physics.OverlapSphereNonAlloc(c, Radius, _buffer, EnemyLayers.Mask);
        for (int i = 0; i < n; i++)
        {
            Collider col = _buffer[i];
            if (col == null || !col.TryGetComponent(out EnemyBase e) || e.IsDead) continue;
            Vector3 d = col.transform.position - c;
            d.y = 0f;
            if (d.sqrMagnitude > Radius * Radius) continue;
            e.TakeDamage(e.CurrentHealth + 1f, StormViolet);
            if (e.IsDead) _kills++;
        }
    }

    // =========================================================================================================================
    //  VISUELS
    // =========================================================================================================================

    private const float MeteorScale = 2.2f;
    private static GameObject _meteorPrefab;
    private static bool _meteorPrefabLooked;
    private static Mesh _crystalMesh;
    private static Material _crystalMat;

    private static Transform BuildMeteor(Transform parent)
    {
        if (!_meteorPrefabLooked)
        {
            _meteorPrefabLooked = true;
            _meteorPrefab = Resources.Load<GameObject>("CrystalStorm/Meteor");
        }
        GameObject go;
        if (_meteorPrefab != null)
        {
            go = Instantiate(_meteorPrefab, parent);
            foreach (Collider col in go.GetComponentsInChildren<Collider>()) Destroy(col);
        }
        else
        {
            go = new GameObject("CrystalMeteor", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = CrystalMesh();
            MeshRenderer r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = CrystalMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        go.transform.localScale = Vector3.one * MeteorScale;
        return go.transform;
    }

    // Cristal à six faces, deux pointes (la grande vers le haut), facettes nettes (sommets non partagés).
    private static Mesh CrystalMesh()
    {
        if (_crystalMesh != null) return _crystalMesh;
        const int sides = 6;
        Vector3 top = new Vector3(0f, 1.15f, 0f), bottom = new Vector3(0f, -0.35f, 0f);
        Vector3[] ring = new Vector3[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            float r = i % 2 == 0 ? 0.34f : 0.28f;    // légère irrégularité
            ring[i] = new Vector3(Mathf.Cos(a) * r, 0.25f + (i % 3) * 0.04f, Mathf.Sin(a) * r);
        }
        List<Vector3> v = new List<Vector3>();
        List<int> tri = new List<int>();
        for (int i = 0; i < sides; i++)
        {
            Vector3 a = ring[i], b = ring[(i + 1) % sides];
            AddTri(v, tri, top, b, a);
            AddTri(v, tri, bottom, a, b);
        }
        _crystalMesh = new Mesh { name = "CrystalMeteor" };
        _crystalMesh.SetVertices(v);
        _crystalMesh.SetTriangles(tri, 0);
        _crystalMesh.RecalculateNormals();
        _crystalMesh.RecalculateBounds();
        return _crystalMesh;
    }

    private static void AddTri(List<Vector3> v, List<int> tri, Vector3 a, Vector3 b, Vector3 c)
    {
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c);
        tri.Add(i); tri.Add(i + 1); tri.Add(i + 2);
    }

    private static Material CrystalMaterial()
    {
        if (_crystalMat != null) return _crystalMat;
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        _crystalMat = new Material(lit != null ? lit : Shader.Find("Standard")) { name = "CrystalMeteor" };
        _crystalMat.SetColor("_BaseColor", new Color(0.6f, 0.38f, 1f));
        _crystalMat.SetFloat("_Metallic", 0.15f);
        _crystalMat.SetFloat("_Smoothness", 0.92f);
        _crystalMat.EnableKeyword("_EMISSION");
        _crystalMat.SetColor("_EmissionColor", new Color(0.55f, 0.25f, 1f) * 1.3f);
        return _crystalMat;
    }

    // Sceau de cristal : étoile à six branches (deux triangles) en trait, petits losanges aux pointes et au centre.
    // Forme réservée à la Tempête (ni anneau, ni disque).
    private static Sprite _sigil, _sigilFill, _vignette;

    private static Sprite Sigil()
    {
        if (_sigil == null) _sigil = BuildSigil(false);
        return _sigil;
    }

    private static Sprite SigilFill()
    {
        if (_sigilFill == null) _sigilFill = BuildSigil(true);
        return _sigilFill;
    }

    private static Sprite BuildSigil(bool fill)
    {
        const int n = 256;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color32[] px = new Color32[n * n];
        Vector2[] t1 = Triangle(90f), t2 = Triangle(-90f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / n * 2f - 1f, (y + 0.5f) / n * 2f - 1f);
                float sdf = Mathf.Min(TriSdf(p, t1), TriSdf(p, t2));   // < 0 dedans
                float a;
                if (fill)
                    a = FXSprites.Smooth(-sdf / 0.02f);
                else
                {
                    float line = FXSprites.Smooth(1f - Mathf.Abs(sdf) / 0.03f);
                    // trait intérieur plus fin : hexagone central formé par les deux triangles
                    float inner = FXSprites.Smooth(1f - Mathf.Abs(Mathf.Max(TriSdf(p, t1), TriSdf(p, t2))) / 0.018f) * 0.7f;
                    float tips = 0f;
                    for (int i = 0; i < 3; i++)
                    {
                        tips = Mathf.Max(tips, Diamond(p, t1[i] * 1.06f, 0.07f));
                        tips = Mathf.Max(tips, Diamond(p, t2[i] * 1.06f, 0.07f));
                    }
                    float core = Diamond(p, Vector2.zero, 0.12f) * 0.9f;
                    a = Mathf.Max(Mathf.Max(line, inner), Mathf.Max(tips, core));
                }
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n / 2f, 0, SpriteMeshType.FullRect);
    }

    private static Vector2[] Triangle(float startDeg)
    {
        Vector2[] t = new Vector2[3];
        for (int i = 0; i < 3; i++)
        {
            float a = (startDeg + i * 120f) * Mathf.Deg2Rad;
            t[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.86f;
        }
        return t;
    }

    // Distance signée approchée à un triangle convexe (max des distances aux côtés).
    private static float TriSdf(Vector2 p, Vector2[] t)
    {
        float d = float.MinValue;
        for (int i = 0; i < 3; i++)
        {
            Vector2 a = t[i], b = t[(i + 1) % 3];
            Vector2 e = (b - a).normalized;
            Vector2 nrm = new Vector2(e.y, -e.x);              // normale extérieure (sommets dans le sens trigonométrique)
            d = Mathf.Max(d, Vector2.Dot(p - a, nrm));
        }
        return d;
    }

    private static float Diamond(Vector2 p, Vector2 c, float size)
    {
        float d = (Mathf.Abs(p.x - c.x) + Mathf.Abs(p.y - c.y)) / size;
        return FXSprites.Smooth((1f - d) / 0.25f);
    }

    // Voile violet sur les bords de l'écran pendant la tempête, sous le HUD (même principe que l'Éclipse).
    private void BuildOverlay()
    {
        if (_overlay != null) return;
        _overlay = new GameObject("CrystalStormOverlay");
        Canvas c = _overlay.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = -490;
        CanvasScaler sc = _overlay.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.matchWidthOrHeight = 0.5f;
        GameObject img = new GameObject("Tint", typeof(RectTransform));
        img.transform.SetParent(_overlay.transform, false);
        RectTransform rt = (RectTransform)img.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        _overlayImage = img.AddComponent<Image>();
        _overlayImage.sprite = Vignette();
        _overlayImage.raycastTarget = false;
        _overlayImage.color = Color.clear;
    }

    private static Sprite Vignette()
    {
        if (_vignette != null) return _vignette;
        const int n = 128;
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = FXSprites.Smooth((r - 0.55f) / 0.8f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        t.SetPixels32(px);
        t.Apply();
        _vignette = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        return _vignette;
    }

    // Test / debug (RunCommand) : lance la tempête tout de suite.
    public void DebugStartNow(float duration = Duration)
    {
        if (_phase == Phase.Storm) return;
        if (_player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) { _player = p.transform; _playerHealth = p.GetComponent<HealthSystem>(); _lastPlayerPos = _player.position; }
        }
        if (_player != null) StartStorm(duration);
    }

    public bool DebugActive => _phase == Phase.Storm;
    public int DebugKills => _kills;
    public int DebugStrikeCount => _strikes.Count;
}
