using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// AJOUTÉ (2026-11-18) - NUIT DES ÉCLATS : événement de mi-partie.
//   1. Au milieu de la partie, un éclat d'Aether apparaît à 30-40 m du joueur (hors écran : il faut marcher) (pilier de lumière + flèche de bord d'écran).
//   2. Le ramasser (passer à moins de PickupRadius) déclenche la nuit : éclairage sombre et bleuté, lueur autour du joueur.
//   3. Pendant la nuit : dégâts du joueur x GameModes.NightDamageMultiplier, mais GameModes.NightSpawnMultiplier fois plus
//      d'ennemis par vague. L'XP par minute et l'économie (or, éclats) ne bougent pas (voir GameModes.XpMultiplier).
//   4. Fin après NightDuration, ou dès qu'un boss apparaît.
// Une fois par partie (Classique, Escarmouche) ; une fois par cycle de boss en Sans fin. Jamais en Ruée de boss, Choc des
// titans, ni dans la partie d'entraînement du didacticiel (voir WaveManager.Start, qui crée ce composant).
// Tout est construit par le code (aucun objet de scène). Le minuteur suit Time.deltaTime : figé pendant les pauses et les
// choix de carte. Le chrono de partie (RunTimer) n'est pas touché : les défis horaires ne changent pas.
public class NightOfShards : MonoBehaviour
{
    public static NightOfShards Instance { get; private set; }

    private enum Phase { Waiting, Shard, Night, Done }
    private Phase _phase = Phase.Waiting;
    private int _index;                 // nombre de nuits déjà passées (Sans fin : une par cycle de boss)

    // ---- Réglages -----------------------------------------------------------------------------------------------------------
    private const float PickupRadius = 2.3f;
    private const float SpawnDistMin = 30f, SpawnDistMax = 40f, SpawnDistFloor = 22f;   // hors écran : il faut marcher un peu
    private const float FadeIn = 3f, FadeOut = 3f;
    private static readonly Color NightBlue = new Color(0.45f, 0.75f, 1f);
    private static readonly Color DawnGold = new Color(1f, 0.85f, 0.45f);

    // Escarmouche (5 min, boss à 1:30 / 3:00 / 5:00) : éclat à 2:05, 45 s pour le ramasser, nuit de 20 s => finie avant 3:00.
    // Classique (15 min, boss à 5 / 10 / 15 min) : éclat à 7:30 (milieu), 80 s pour le ramasser, nuit de 45 s.
    // Sans fin : même chose à 7:30, puis 12:30, 17:30...
    private float TriggerTime(int index)
    {
        if (GameModes.IsSkirmish) return 125f;
        float interval = WaveManager.Instance != null ? WaveManager.Instance.BossSpawnInterval : 300f;
        return interval * (index + 1) + interval * 0.5f;
    }
    private float ShardLifetime => GameModes.IsSkirmish ? 45f : 80f;
    private float NightDuration => GameModes.IsSkirmish ? 20f : 45f;
    private bool Repeats => GameModes.IsEndless;

    // ---- État ---------------------------------------------------------------------------------------------------------------
    private float _timer;               // temps passé dans la phase courante
    private float _k;                   // intensité de la nuit, 0..1
    private Transform _player;

    // éclat
    private GameObject _shard;
    private SpriteRenderer _halo, _ring, _pillar, _gem, _gemGlow;
    private Light _shardLight;
    private TutorialPointer _pointer;
    private readonly List<Vector3> _arrowList = new List<Vector3>(1);

    // nuit
    private Light _sun;
    private Color _sunColor;
    private float _sunIntensity, _ambientIntensity, _reflectionIntensity;
    private bool _lightingCaptured;
    private Light _playerLight;
    private GameObject _overlay;
    private Image _overlayImage;
    private float _nextEnvUpdate;
    private static Sprite _vignette;

    private void Awake()
    {
        Instance = this;
        GameModes.NightActive = false;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        RestoreLighting();
        GameModes.NightActive = false;
        PlayerBuffs.SetNightActive(false);
        DestroyShard();
        if (_overlay != null) Destroy(_overlay);
        if (_playerLight != null) Destroy(_playerLight.gameObject);
        if (GameUI.Instance != null) GameUI.Instance.HideNightCounter();
    }

    private void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || _phase == Phase.Done) return;

        if (gm.IsGameOver)
        {
            if (_phase == Phase.Night) EndNight(false);
            if (_phase == Phase.Shard) { DestroyShard(); _phase = Phase.Done; }
            return;
        }
        if (gm.IsPaused)
        {
            if (_pointer != null) _pointer.Hide();   // flèche cachée pendant la pause et les choix de carte
            return;
        }

        bool bossAlive = WaveManager.Instance != null && WaveManager.Instance.BossAlive;
        if (bossAlive)
        {
            // un boss met fin à l'événement : éclat retiré, nuit terminée tout de suite
            if (_phase == Phase.Night) EndNight(false);
            else if (_phase == Phase.Shard) { DestroyShard(); NextNightOrDone(); }
            return;
        }

        if (_player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) _player = p.transform;
            if (_player == null) return;
        }

        switch (_phase)
        {
            case Phase.Waiting:
                // pas d'éclat si le boss arrive dans moins de 15 s : il serait retiré aussitôt
                if (gm.RunTimer >= TriggerTime(_index) && TimeBeforeBoss() > 15f) SpawnShard();
                break;
            case Phase.Shard:
                UpdateShard();
                break;
            case Phase.Night:
                UpdateNight();
                break;
        }
    }

    // Secondes de chrono avant le prochain boss (l'éclat et la nuit ne doivent jamais lui survivre).
    private float TimeBeforeBoss()
    {
        if (WaveManager.Instance == null || GameManager.Instance == null) return float.MaxValue;
        return WaveManager.Instance.NextBossTime - GameManager.Instance.RunTimer;
    }

    private void NextNightOrDone()
    {
        _index++;
        _timer = 0f;
        _phase = Repeats ? Phase.Waiting : Phase.Done;
    }

    // =========================================================================================================================
    //  L'ÉCLAT À RAMASSER
    // =========================================================================================================================

    private void SpawnShard()
    {
        Vector3 pos = PickShardPosition();
        BuildShard(pos);
        _timer = 0f;
        _phase = Phase.Shard;
        if (_pointer == null) _pointer = TutorialPointer.Create();
        if (GameUI.Instance != null)
            GameUI.Instance.ShowEventBanner(Loc.T("UN ÉCLAT APPARAÎT !"), NightBlue,
                Loc.T("Allez le ramasser pour déclencher l'Éclipse."), 2.2f);
    }

    // Distance 30-40 m dans une direction au hasard, ramenée dans la carte (zone fermée ou tunnel). 24 essais ; si la carte est
    // trop étroite pour en trouver un assez loin, on garde le plus éloigné du joueur.
    private Vector3 PickShardPosition()
    {
        Vector3 origin = _player.position;
        origin.y = 0f;
        Vector3 best = origin + Vector3.right * SpawnDistMin;
        float bestDist = -1f;
        for (int i = 0; i < 24; i++)
        {
            Vector2 d = Random.insideUnitCircle.normalized;
            if (d.sqrMagnitude < 0.01f) d = Vector2.right;
            Vector3 cand = origin + new Vector3(d.x, 0f, d.y) * Random.Range(SpawnDistMin, SpawnDistMax);
            cand = MapBoundaryUtils.ClampToZone(cand);
            cand.y = 0f;
            float dist = Vector3.Distance(cand, origin);
            if (dist > bestDist) { bestDist = dist; best = cand; }
            if (dist >= SpawnDistFloor) return cand;
        }
        return best;
    }

    private void BuildShard(Vector3 pos)
    {
        DestroyShard();
        _shard = new GameObject("NightShard");
        _shard.transform.position = pos;

        _halo = FXSprites.MakeRenderer(_shard.transform, "Halo", FXSprites.Glow(), 40, 0.15f);
        FXSprites.SetFlat(_halo, 7f, 0f);
        _ring = FXSprites.MakeRenderer(_shard.transform, "Ring", FXSprites.ThinRing(), 41, 0.18f);
        FXSprites.SetFlat(_ring, 4f, 0f);
        _pillar = FXSprites.MakeRenderer(_shard.transform, "Pillar", FXSprites.Pillar(), 42, 0.2f);
        _gemGlow = FXSprites.MakeRenderer(_shard.transform, "GemGlow", FXSprites.Glow(), 43, 1.9f);
        _gem = FXSprites.MakeRenderer(_shard.transform, "Gem", FXSprites.Gem(), 44, 1.9f);

        GameObject lightGO = new GameObject("ShardLight");
        lightGO.transform.SetParent(_shard.transform, false);
        lightGO.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        _shardLight = lightGO.AddComponent<Light>();
        _shardLight.type = LightType.Point;
        _shardLight.color = NightBlue;
        _shardLight.range = 9f;
        _shardLight.intensity = 3f;
        _shardLight.shadows = LightShadows.None;

        AnimateShard(0f);
    }

    private void DestroyShard()
    {
        if (_shard != null) Destroy(_shard);
        _shard = null;
        if (_pointer != null) { Destroy(_pointer.gameObject); _pointer = null; }
    }

    private void UpdateShard()
    {
        _timer += Time.deltaTime;
        if (_shard == null) { NextNightOrDone(); return; }

        AnimateShard(_timer);

        // flèche : le joueur sait où aller même hors de l'écran
        if (_pointer != null)
        {
            _arrowList.Clear();
            _arrowList.Add(_shard.transform.position + Vector3.up * 1.5f);
            _pointer.Show(_arrowList);
        }

        Vector3 a = _player.position, b = _shard.transform.position;
        a.y = b.y = 0f;
        if ((a - b).sqrMagnitude <= PickupRadius * PickupRadius)
        {
            Collect();
            return;
        }

        if (_timer >= ShardLifetime || TimeBeforeBoss() <= 0f)
        {
            // non ramassé à temps (ou le boss arrive) : l'éclat s'éteint, la nuit n'a pas lieu
            DestroyShard();
            NextNightOrDone();
        }
    }

    private void AnimateShard(float t)
    {
        if (_shard == null) return;
        Camera cam = Camera.main;
        Quaternion face = cam != null ? cam.transform.rotation : Quaternion.identity;
        float appear = Mathf.Clamp01(t / 0.8f);
        // clignote dans les 10 dernières secondes de sa vie : il va disparaître
        float blink = 1f;
        float left = Mathf.Min(ShardLifetime - t, TimeBeforeBoss());
        if (left < 10f) blink = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * 7f));
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 3f);

        _halo.color = FXSprites.A(NightBlue, (0.30f + 0.15f * pulse) * appear * blink);
        _ring.color = FXSprites.A(Color.white, (0.55f + 0.25f * pulse) * appear * blink);
        FXSprites.SetFlat(_ring, 3.6f + 0.8f * pulse, t * 40f);

        // pilier : face à la caméra, base au sol
        _pillar.transform.rotation = face;
        float pw = 2.4f / _pillar.sprite.bounds.size.x;
        float ph = 15f / _pillar.sprite.bounds.size.y;
        _pillar.transform.localScale = new Vector3(pw, ph * appear, 1f);
        _pillar.color = FXSprites.A(NightBlue, 0.45f * appear * blink);

        // gemme qui flotte
        float bob = Mathf.Sin(t * 2.2f) * 0.25f;
        _gem.transform.position = _shard.transform.position + new Vector3(0f, 1.9f + bob, 0f);
        _gem.transform.rotation = face;
        _gem.transform.localScale = Vector3.one * (1.7f * appear / Mathf.Max(0.01f, _gem.sprite.bounds.size.y));
        _gem.color = FXSprites.A(new Color(0.75f, 0.95f, 1f), blink);
        _gemGlow.transform.position = _gem.transform.position;
        _gemGlow.transform.rotation = face;
        _gemGlow.transform.localScale = Vector3.one * (4.2f * appear / Mathf.Max(0.01f, _gemGlow.sprite.bounds.size.x)) * (0.9f + 0.15f * pulse);
        _gemGlow.color = FXSprites.A(NightBlue, 0.55f * blink);
        if (_shardLight != null) _shardLight.intensity = (2.6f + 1.2f * pulse) * appear * blink;
    }

    private void Collect()
    {
        Vector3 pos = _shard.transform.position;
        ExpandingRingVFX.Spawn(pos, 7f, NightBlue, 0.8f);
        ImpactSparkVFX.Spawn(pos + Vector3.up, NightBlue, 2.2f);
        CameraShakeFX.Shake(0.25f);
        DestroyShard();
        StartNight();
    }

    // =========================================================================================================================
    //  LA NUIT
    // =========================================================================================================================

    private void StartNight()
    {
        _phase = Phase.Night;
        _timer = 0f;
        _k = 0f;
        _nightKills = _nightEclats = 0;
        if (GameUI.Instance != null) GameUI.Instance.UpdateNightCounter(0, false);
        GameModes.NightActive = true;
        PlayerBuffs.SetNightActive(true);
        CaptureLighting();
        BuildOverlay();
        BuildPlayerLight();
        if (GameUI.Instance != null)
            GameUI.Instance.ShowEventBanner(Loc.T("ÉCLIPSE"), NightBlue,
                Loc.T("Vos dégâts augmentent, mais les ennemis affluent."), 2.4f);
        ApplyNight(0f);
    }

    private void UpdateNight()
    {
        _timer += Time.deltaTime;
        float dur = NightDuration;
        float k = Mathf.Min(Mathf.Clamp01(_timer / FadeIn), Mathf.Clamp01((dur - _timer) / FadeOut));
        ApplyNight(k);
        if (_timer >= dur) EndNight(true);
    }

    // announce = true : fin naturelle (bandeau « le jour se lève ») ; false : coupure (boss, mort), retour immédiat à la normale.
    private void EndNight(bool announce)
    {
        GameModes.NightActive = false;
        PlayerBuffs.SetNightActive(false);
        RestoreLighting();
        if (_overlay != null) { Destroy(_overlay); _overlay = null; _overlayImage = null; }
        if (_playerLight != null) { Destroy(_playerLight.gameObject); _playerLight = null; }
        if (GameUI.Instance != null) GameUI.Instance.HideNightCounter();
        if (announce && GameUI.Instance != null)
            GameUI.Instance.ShowEventBanner(Loc.T("LE JOUR SE LÈVE"), DawnGold,
                _nightEclats > 0 ? Loc.F("+{0} éclats gagnés", _nightEclats) : null, 1.6f);
        NextNightOrDone();
    }

    // Éclats (monnaie) gagnés pendant la nuit : 1 par NightKillsPerEclat ennemis tués, ajoutés à ceux de la partie
    // (MetaProgressionManager.AddNightEclats). Appelé par MetaProgressionManager.AddKillGold tant que la nuit est active.
    private const int NightKillsPerEclat = 8;
    private int _nightKills, _nightEclats;

    public void RegisterKill()
    {
        if (_phase != Phase.Night) return;
        _nightKills++;
        if (_nightKills % NightKillsPerEclat != 0) return;
        _nightEclats++;
        if (MetaProgressionManager.Instance != null) MetaProgressionManager.Instance.AddNightEclats(1);
        if (GameUI.Instance != null) GameUI.Instance.UpdateNightCounter(_nightEclats, true);
    }

    private void CaptureLighting()
    {
        if (_lightingCaptured) return;
        _sun = RenderSettings.sun;
        if (_sun == null)
        {
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l != null && l.type == LightType.Directional) { _sun = l; break; }
        }
        if (_sun != null) { _sunColor = _sun.color; _sunIntensity = _sun.intensity; }
        _ambientIntensity = RenderSettings.ambientIntensity;
        _reflectionIntensity = RenderSettings.reflectionIntensity;
        _lightingCaptured = true;
    }

    private void RestoreLighting()
    {
        if (!_lightingCaptured) return;
        if (_sun != null) { _sun.color = _sunColor; _sun.intensity = _sunIntensity; }
        RenderSettings.ambientIntensity = _ambientIntensity;
        RenderSettings.reflectionIntensity = _reflectionIntensity;
        _lightingCaptured = false;
        _k = 0f;
    }

    private void ApplyNight(float k)
    {
        _k = k;
        if (_sun != null)
        {
            _sun.color = Color.Lerp(_sunColor, new Color(0.42f, 0.52f, 1f), k);
            _sun.intensity = Mathf.Lerp(_sunIntensity, _sunIntensity * 0.55f, k);
        }
        RenderSettings.ambientIntensity = Mathf.Lerp(_ambientIntensity, _ambientIntensity * 0.65f, k);
        RenderSettings.reflectionIntensity = Mathf.Lerp(_reflectionIntensity, _reflectionIntensity * 0.6f, k);
        // l'ambiance venant du ciel est recalculée par Unity ; pendant les fondus seulement, et pas à chaque frame
        if ((k > 0f && k < 1f) && Time.unscaledTime >= _nextEnvUpdate)
        {
            _nextEnvUpdate = Time.unscaledTime + 0.5f;
            DynamicGI.UpdateEnvironment();
        }
        if (_overlayImage != null) _overlayImage.color = new Color(0.02f, 0.05f, 0.22f, k);
        if (_playerLight != null) _playerLight.intensity = 4f * k;
    }

    // Voile bleu nuit avec bords plus sombres, sous tout le HUD (ordre de tri très bas) : indépendant du moteur de lumière,
    // donc la nuit se voit aussi à la qualité graphique la plus basse.
    private void BuildOverlay()
    {
        if (_overlay != null) return;
        _overlay = new GameObject("NightOverlay");
        Canvas c = _overlay.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = -500;
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
        _overlayImage.color = new Color(0.02f, 0.05f, 0.22f, 0f);
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
                float r = Mathf.Sqrt(dx * dx + dy * dy);                    // 0 au centre, 1 au milieu d'un bord, 1,41 aux coins
                float e = FXSprites.Smooth((r - 0.3f) / 1.0f);
                float a = Mathf.Lerp(0.10f, 0.62f, e);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        t.SetPixels32(px);
        t.Apply();
        _vignette = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        return _vignette;
    }

    // Lueur bleutée autour du joueur : lui et ses ennemis proches restent lisibles dans le noir.
    private void BuildPlayerLight()
    {
        if (_playerLight != null || _player == null) return;
        GameObject go = new GameObject("NightPlayerLight");
        go.transform.SetParent(_player, false);
        go.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        _playerLight = go.AddComponent<Light>();
        _playerLight.type = LightType.Point;
        _playerLight.color = new Color(0.6f, 0.8f, 1f);
        _playerLight.range = 14f;
        _playerLight.intensity = 0f;
        _playerLight.shadows = LightShadows.None;
    }

    // Test / debug : fait apparaître l'éclat tout de suite (RunCommand).
    public void DebugSpawnNow()
    {
        if (_phase == Phase.Shard || _phase == Phase.Night) return;
        _phase = Phase.Waiting;
        if (_player == null) { GameObject p = GameObject.FindWithTag("Player"); if (p != null) _player = p.transform; }
        if (_player != null) SpawnShard();
    }

    // Test / debug (Ctrl+F1) : lance la nuit tout de suite, sans passer par l'éclat. Sans effet si une nuit est déjà en cours.
    public void DebugStartNightNow()
    {
        if (_phase == Phase.Night) return;
        if (_shard != null) DestroyShard();
        if (_player == null) { GameObject p = GameObject.FindWithTag("Player"); if (p != null) _player = p.transform; }
        StartNight();
    }
}
