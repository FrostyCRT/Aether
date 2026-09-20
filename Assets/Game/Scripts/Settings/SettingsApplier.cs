using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// AJOUTE (2026-09-19) - APPLIQUE les paramètres du joueur, en direct, dans TOUTES les scènes.
//
// Objet persistant créé tout seul au lancement (RuntimeInitializeOnLoadMethod : rien à poser dans une scène) :
//  - au démarrage : applique tout ce qui est enregistré (audio, mode d'affichage, résolution, V-Sync, limite de
//    FPS, qualité) AVANT le premier écran ;
//  - à chaque changement (GameSettings.Changed) : réapplique juste le réglage touché ;
//  - à chaque scène : anticrénelage des caméras, ombres de la lumière principale ;
//  - lueur/ombre d'écran : la luminosité et le compteur de FPS sont deux petits éléments d'un Canvas Overlay
//    posé au-dessus de tout (donc aussi au-dessus de l'interface du jeu et des menus) ;
//  - fenêtre inactive : coupe le son et met la partie en pause (jamais dans l'éditeur, où le focus saute en
//    permanence).
//
// Éditeur : QualitySettings / targetFrameRate modifiés PENDANT une partie de test persistent sinon dans
// ProjectSettings ; les valeurs d'origine sont donc restaurées à la sortie du mode Play.
public class SettingsApplier : MonoBehaviour
{
    public static SettingsApplier Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("[SettingsApplier]");
        DontDestroyOnLoad(go);
        go.AddComponent<SettingsApplier>();
    }

    // ---- audio ----------------------------------------------------------------------------------------------
    private AudioMixer _mixer;
    private bool _focused = true;

    // Pause : le son du jeu baisse légèrement (≈ -7 dB) pour l'immersion, en fondu, puis remonte à la reprise.
    // Multiplie le volume général ; se remet à 1 à chaque changement de scène.
    private const float PauseDuckLevel = 0.45f, DuckSpeed = 3f;
    private float _duck = 1f, _duckTarget = 1f;

    public static void SetPauseDuck(bool paused)
    {
        if (Instance != null) Instance._duckTarget = paused ? PauseDuckLevel : 1f;
    }

    // ---- superposition (luminosité + FPS) ---------------------------------------------------------------------
    private Canvas _overlay;
    private Image _dim, _bright;
    private TextMeshProUGUI _fpsText;
    private float _fpsAccum, _fpsTime;
    private int _fpsFrames;

#if UNITY_EDITOR
    private int _editorQuality;
    private int _editorVSync;
    private int _editorTargetFps;
    private bool _editorGameViewVSync = true;

    // La fenêtre Game de l'éditeur a son propre bouton « VSync » qui PRIME sur QualitySettings.vSyncCount : sans
    // ça, la limite d'images par seconde reste sans effet en test. On le fait suivre le réglage du joueur (et on
    // remet l'état d'origine en quittant le Play). Interface interne à l'éditeur : tout est dans un try/catch.
    private static bool GetGameViewVSync()
    {
        try
        {
            System.Type t = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            System.Reflection.PropertyInfo p = t?.GetProperty("vSyncEnabled", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            foreach (Object v in Resources.FindObjectsOfTypeAll(t)) return (bool)p.GetValue(v);
        }
        catch { }
        return true;
    }

    private static void SetGameViewVSync(bool on)
    {
        try
        {
            System.Type t = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            System.Reflection.PropertyInfo p = t?.GetProperty("vSyncEnabled", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (p == null) return;
            foreach (Object v in Resources.FindObjectsOfTypeAll(t)) p.SetValue(v, on);
        }
        catch { }
    }
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

#if UNITY_EDITOR
        _editorQuality = QualitySettings.GetQualityLevel();
        _editorVSync = QualitySettings.vSyncCount;
        _editorTargetFps = Application.targetFrameRate;
        _editorGameViewVSync = GetGameViewVSync();
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif

        GameSettings.Changed += OnSettingChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;

        BuildOverlay();
        StartCoroutine(LoadMixer());
        ApplyAll();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        GameSettings.Changed -= OnSettingChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
#endif
    }

#if UNITY_EDITOR
    private void OnPlayModeChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state != UnityEditor.PlayModeStateChange.ExitingPlayMode) return;
        QualitySettings.SetQualityLevel(_editorQuality, true);
        QualitySettings.vSyncCount = _editorVSync;
        Application.targetFrameRate = _editorTargetFps;
        SetGameViewVSync(_editorGameViewVSync);
        AudioListener.volume = 1f;
    }
#endif

    private void Update()
    {
        GameSettings.Flush();
        InputBindings.Flush();
        UpdateFps();

        if (!Mathf.Approximately(_duck, _duckTarget))            // temps réel : le jeu est à l'arrêt pendant la pause
        {
            _duck = Mathf.MoveTowards(_duck, _duckTarget, Time.unscaledDeltaTime * DuckSpeed);
            ApplyAudioVolumes();
        }
    }

    private void OnApplicationQuit()
    {
        GameSettings.Flush();
        InputBindings.Flush();
    }

    // ---- application -----------------------------------------------------------------------------------------
    private void ApplyAll()
    {
        ApplyQuality();
        ApplyVSyncAndCap();
        ApplyDisplay();
        ApplyAudioVolumes();
        ApplyBrightness();
        ApplyShowFps();
    }

    private void OnSettingChanged(string key)
    {
        switch (key)
        {
            case GameSettings.Master:
            case GameSettings.Music:
            case GameSettings.Sfx:
                ApplyAudioVolumes(); break;
            case GameSettings.MuteInBackground: ApplyAudioVolumes(); break;
            case GameSettings.DisplayMode:
            case GameSettings.Resolution:
                ApplyDisplay(); break;
            case GameSettings.VSync:
            case GameSettings.FpsCap:
                ApplyVSyncAndCap(); break;
            case GameSettings.Quality:
                ApplyQuality(); ApplyVSyncAndCap(); break;
            case GameSettings.Shadows: ApplyShadows(); break;
            case GameSettings.Brightness: ApplyBrightness(); break;
            case GameSettings.ShowFps: ApplyShowFps(); break;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _duckTarget = 1f;                                        // jamais de son étouffé qui survit à un changement de scène
        ApplyShadows();
    }

    // ---- audio ----------------------------------------------------------------------------------------------------
    private System.Collections.IEnumerator LoadMixer()
    {
        ResourceRequest request = Resources.LoadAsync<AudioMixer>("MainMixer");   // asynchrone : pas de gel au démarrage
        while (!request.isDone) yield return null;
        _mixer = request.asset as AudioMixer;
        if (_mixer == null) { Debug.LogError("[SettingsApplier] MainMixer introuvable dans Resources."); yield break; }
        yield return null;                                                          // laisse le moteur audio s'initialiser
        ApplyAudioVolumes();
    }

    private static float ToDb(float linear) => linear > 0.0001f ? Mathf.Log10(linear) * 20f : -80f;

    private void ApplyAudioVolumes()
    {
        bool muted = !_focused && GameSettings.GetBool(GameSettings.MuteInBackground) && !Application.isEditor;
        AudioListener.volume = muted ? 0f : GameSettings.GetFloat(GameSettings.Master) * _duck;
        if (_mixer == null) return;
        _mixer.SetFloat("MusicVolume", ToDb(GameSettings.GetFloat(GameSettings.Music)));
        _mixer.SetFloat("SFXVolume", ToDb(GameSettings.GetFloat(GameSettings.Sfx)));
    }

    private void OnApplicationFocus(bool focus)
    {
        _focused = focus;
        ApplyAudioVolumes();

        // fenêtre inactive : pause automatique de la partie (jamais dans l'éditeur, où le focus change sans cesse)
        if (!focus && !Application.isEditor && GameSettings.GetBool(GameSettings.PauseOnFocusLoss))
        {
            GameManager gm = GameManager.Instance;
            if (gm != null && !gm.IsPaused && !gm.IsGameOver
                && !(LevelUpManager.Instance != null && LevelUpManager.Instance.IsWaitingForChoice))
                gm.TogglePause();
        }
    }

    // ---- affichage -------------------------------------------------------------------------------------------------
    public static FullScreenMode ModeFromSetting(int mode)
    {
        switch (mode)
        {
            case 0: return FullScreenMode.ExclusiveFullScreen;
            case 2: return FullScreenMode.Windowed;
            default: return FullScreenMode.FullScreenWindow;
        }
    }

    // Résolutions proposées : celles du moniteur, sans doublon (largeur x hauteur), du plus petit au plus grand,
    // au moins 1024 x 576.
    public static List<Vector2Int> AvailableResolutions()
    {
        List<Vector2Int> list = new List<Vector2Int>();
        foreach (Resolution r in Screen.resolutions)
        {
            if (r.width < 1024 || r.height < 576) continue;
            Vector2Int v = new Vector2Int(r.width, r.height);
            if (!list.Contains(v)) list.Add(v);
        }
        Vector2Int cur = new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
        if (!list.Contains(cur)) list.Add(cur);
        list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return list;
    }

    public static string ResolutionKey(Vector2Int v) => v.x + "x" + v.y;

    public static bool TryParseResolution(string s, out Vector2Int v)
    {
        v = default;
        if (string.IsNullOrEmpty(s)) return false;
        string[] p = s.Split('x');
        if (p.Length != 2 || !int.TryParse(p[0], out int w) || !int.TryParse(p[1], out int h)) return false;
        v = new Vector2Int(w, h);
        return true;
    }

    private void ApplyDisplay()
    {
        FullScreenMode mode = ModeFromSetting(GameSettings.GetInt(GameSettings.DisplayMode));

        Vector2Int res;
        if (mode == FullScreenMode.FullScreenWindow)
        {
            // plein écran sans bordure : toujours la résolution du bureau
            res = new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
            if (Application.isEditor) res = new Vector2Int(Screen.width, Screen.height);
        }
        else if (!TryParseResolution(GameSettings.GetString(GameSettings.Resolution), out res))
        {
            res = new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
        }

        if (Application.isEditor) return;    // la fenêtre Game de l'éditeur ne se redimensionne pas
        Screen.SetResolution(res.x, res.y, mode);
    }

    private void ApplyQuality()
    {
        int q = Mathf.Clamp(GameSettings.GetInt(GameSettings.Quality), 0, QualitySettings.names.Length - 1);
        if (QualitySettings.GetQualityLevel() != q) QualitySettings.SetQualityLevel(q, true);
    }

    private void ApplyVSyncAndCap()
    {
        bool vsync = GameSettings.GetBool(GameSettings.VSync);
        QualitySettings.vSyncCount = vsync ? 1 : 0;
        int cap = GameSettings.FpsCaps[Mathf.Clamp(GameSettings.GetInt(GameSettings.FpsCap), 0, GameSettings.FpsCaps.Length - 1)];
        Application.targetFrameRate = cap <= 0 ? -1 : cap;     // ignoré par Unity tant que le V-Sync est actif
#if UNITY_EDITOR
        SetGameViewVSync(vsync);
#endif
    }

    // ---- graphismes -------------------------------------------------------------------------------------------------
    private void ApplyShadows()
    {
        LightShadows mode = GameSettings.ShadowsEnabled ? LightShadows.Soft : LightShadows.None;
        foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) l.shadows = mode;
    }

    // ---- superposition : luminosité + FPS --------------------------------------------------------------------------------
    private void BuildOverlay()
    {
        GameObject go = new GameObject("[ScreenOverlay]", typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        _overlay = go.GetComponent<Canvas>();
        _overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        _overlay.sortingOrder = 32760;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _dim = FullScreenImage("Dim", Color.black, false);
        _bright = FullScreenImage("Bright", Color.white, true);

        GameObject t = new GameObject("Fps", typeof(RectTransform), typeof(TextMeshProUGUI));
        t.transform.SetParent(go.transform, false);
        RectTransform rt = (RectTransform)t.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(14f, -8f);
        rt.sizeDelta = new Vector2(420f, 40f);
        _fpsText = t.GetComponent<TextMeshProUGUI>();
        _fpsText.fontSize = 24;
        _fpsText.alignment = TextAlignmentOptions.TopLeft;
        _fpsText.color = new Color(0f, 0f, 0f, 1f);          // noir : le vert se perdait sur le sol
        _fpsText.raycastTarget = false;
        _fpsText.textWrappingMode = TextWrappingModes.NoWrap;
        _fpsText.fontStyle = FontStyles.Bold;
        _fpsText.outlineWidth = 0.2f;
        _fpsText.outlineColor = new Color32(0, 0, 0, 255);          // contour noir : texte plein, bien lisible
        _fpsText.gameObject.SetActive(false);
    }

    private Image FullScreenImage(string name, Color color, bool additive)
    {
        GameObject g = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        g.transform.SetParent(_overlay.transform, false);
        RectTransform r = (RectTransform)g.transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        Image im = g.GetComponent<Image>();
        im.color = new Color(color.r, color.g, color.b, 0f);
        im.raycastTarget = false;                    // ne bloque jamais un clic
        if (additive)
        {
            // matériau "exposition" (Resources/UI_Exposure) : éclaircit par multiplication, sans relever les noirs
            Material m = Resources.Load<Material>("UI_Exposure");
            if (m != null) im.material = m;
        }
        g.SetActive(false);
        return im;
    }

    private void ApplyBrightness()
    {
        // 0,5 .. 1,2 ; 1 = image d'origine. Assombrir = voile noir (jusqu'à 72 % au minimum). Éclaircir = gain
        // MULTIPLICATIF (matériau UI_Exposure : fond x (1 + gain)) : les noirs restent noirs, l'image ne se délave pas.
        float b = GameSettings.GetFloat(GameSettings.Brightness);
        float dark = Mathf.Clamp01((1f - b) / 0.5f) * 0.72f;
        float light = Mathf.Clamp01((b - 1f) / 0.2f) * 0.5f;
        _dim.color = new Color(0f, 0f, 0f, dark);
        _dim.gameObject.SetActive(dark > 0.002f);
        _bright.color = new Color(1f, 1f, 1f, light);
        _bright.gameObject.SetActive(light > 0.002f);
    }

    private void ApplyShowFps()
    {
        bool on = GameSettings.GetBool(GameSettings.ShowFps);
        _fpsText.gameObject.SetActive(on);
        _fpsAccum = 0f; _fpsFrames = 0; _fpsTime = 0f;
    }

    private void UpdateFps()
    {
        if (!_fpsText.gameObject.activeSelf) return;
        _fpsAccum += Time.unscaledDeltaTime;
        _fpsFrames++;
        _fpsTime += Time.unscaledDeltaTime;
        if (_fpsTime < 0.4f) return;
        float avg = _fpsAccum / Mathf.Max(1, _fpsFrames);
        _fpsText.text = Mathf.RoundToInt(1f / Mathf.Max(0.0001f, avg)) + " FPS  (" + (avg * 1000f).ToString("0.0") + " ms)";
        _fpsAccum = 0f; _fpsFrames = 0; _fpsTime = 0f;
    }
}
