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
        HandleFullscreenKey();
        UpdateWindowSnap();

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
        ApplyMonitor();
        ApplyDisplay();
        ApplyAudioVolumes();
        ApplyBrightness();
        ApplyShowFps();
        ApplyFpsColor();
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
            case GameSettings.Monitor:
                ApplyMonitor(); break;
            case GameSettings.VSync:
            case GameSettings.FpsCap:
                ApplyVSyncAndCap(); break;
            case GameSettings.Quality:
                ApplyQuality(); ApplyVSyncAndCap(); break;
            case GameSettings.Shadows: ApplyShadows(); break;
            case GameSettings.Brightness: ApplyBrightness(); break;
            case GameSettings.ShowFps: ApplyShowFps(); break;
            case GameSettings.FpsColor: ApplyFpsColor(); break;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _duckTarget = 1f;                                        // jamais de son étouffé qui survit à un changement de scène
        ApplyShadows();
        ApplyQualityExtras();                                    // caméras de la nouvelle scène : lissage des bords, bloom
        PlaceFps(scene);
        ApplyShowFps();
    }

    // AJOUTÉ (2026-10-25) - dans le menu, la barre d'onglets occupe le haut de l'écran (100 px) et ne laisse que 136 px
    // libres à gauche de l'onglet « Compétences » (juste en dessous, les titres des pages) : le compteur s'y loge sur
    // deux lignes, un peu plus petit, centré entre le bord de l'écran et le cadre de l'onglet (même écart des deux
    // côtés) et à la hauteur de l'onglet. En partie, il reste sur une ligne dans le coin, au-dessus des PV.
    private bool _fpsCompact;

    private void PlaceFps(Scene scene)
    {
        if (_fpsText == null) return;
        // CORRIGÉ (2026-11-11, retour utilisateur : "un bug de transition quand on change de scène [...] il faut cacher les
        // fps en fondu, et les réafficher au bon format dans l'autre scène") - le texte gardait l'ancien format jusqu'à la
        // mesure suivante (0,4 s) : il est réécrit tout de suite dans le nouveau. Le fondu suit celui de la transition (UpdateFps).
        _fpsCompact = scene.name == "MainMenu";
        _fpsText.fontSize = _fpsCompact ? 20f : 24f;
        _fpsText.alignment = _fpsCompact ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;
        _fpsText.rectTransform.sizeDelta = new Vector2(_fpsCompact ? 136f : 420f, _fpsCompact ? 60f : 40f);
        // menu : 1 px à gauche à la demande de l'utilisateur (2026-11-11), bien que mesuré à 29 px de chaque côté
        _fpsText.rectTransform.anchoredPosition = new Vector2(_fpsCompact ? -1f : 14f, _fpsCompact ? -19f : -8f);
        if (_fpsLastAvg > 0f) WriteFps(_fpsLastAvg);
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
        AudioReady = true;
    }

    // AJOUTE (2026-09-26) - vrai dès que les volumes SAUVEGARDÉS (Musique / Effets / Général) sont appliqués au mixer.
    // MusicStarter attend ce signal avant de lancer un morceau : sans lui, la musique de l'écran de chargement
    // démarrait au volume par défaut du mixer (le mixer se charge en asynchrone) puis sautait au volume réglé.
    public static bool AudioReady { get; private set; }

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
    // AJOUTÉ (2026-11-10, demande utilisateur : "quand on appuie sur F11, ça met en fenêtré, mais qui prend toute la page") -
    // F11 passe en fenêtré sans bordure (plein écran), et un second appui revient au mode d'avant (fenêtré par défaut).
    // Passe par le réglage « Mode d'affichage » : la page Paramètres et la prochaine session restent cohérentes.
    private const int BorderlessMode = 1, WindowedMode = 2;
    private int _modeBeforeBorderless = WindowedMode;

    private void HandleFullscreenKey()
    {
        UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null || !kb.f11Key.wasPressedThisFrame) return;
        int current = GameSettings.GetInt(GameSettings.DisplayMode);
        if (current == BorderlessMode)
            GameSettings.SetInt(GameSettings.DisplayMode, _modeBeforeBorderless);
        else
        {
            _modeBeforeBorderless = current;
            GameSettings.SetInt(GameSettings.DisplayMode, BorderlessMode);
        }
    }

    public static FullScreenMode ModeFromSetting(int mode)
    {
        switch (mode)
        {
            case 0: return FullScreenMode.ExclusiveFullScreen;
            case 2: return FullScreenMode.Windowed;
            default: return FullScreenMode.FullScreenWindow;
        }
    }

    // MODIFIÉ (2026-11-15, retour utilisateur : « toutes les résolutions proposées en fenêtré doivent être des rectangles
    // pour afficher tous les éléments, jusqu'à 1920x1080, plus une option qui tient compte de la barre des tâches et de la
    // barre de titre ») - en fenêtré, la liste ne propose plus que des formats 16:9 (ceux qui tiennent sur l'écran), puis
    // « Espace de travail » : le plus grand 16:9 qui tient entre la barre des tâches et le haut de l'écran, barre de titre
    // comprise (WorkAreaKey, choix par défaut). Les deux pleins écrans gardent la résolution native de l'écran.
    public const string WorkAreaKey = "work";
    private static readonly Vector2Int[] WindowPresets =
    {
        new Vector2Int(1024, 576), new Vector2Int(1280, 720), new Vector2Int(1366, 768), new Vector2Int(1600, 900),
        new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(3200, 1800), new Vector2Int(3840, 2160),
    };

    // Clés des choix du mode fenêtré, du plus petit au plus grand, « Espace de travail » en dernier.
    public static List<string> WindowedResolutionKeys()
    {
        Vector2Int native = NativeResolution();
        List<string> keys = new List<string>();
        foreach (Vector2Int p in WindowPresets)
            if (p.x <= native.x && p.y <= native.y) keys.Add(ResolutionKey(p));
        if (keys.Count == 0) keys.Add(ResolutionKey(WindowPresets[0]));
        keys.Add(WorkAreaKey);
        return keys;
    }

    // Taille de la zone de jeu (sans la barre de titre) pour une clé de résolution du mode fenêtré.
    public static Vector2Int WindowSizeForKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key == WorkAreaKey || !TryParseResolution(key, out Vector2Int v)) return WorkAreaWindowSize();
        return v;
    }

    public static string ResolutionKey(Vector2Int v) => v.x + "x" + v.y;

    // ---- choix de l'écran (AJOUTÉ 2026-10-29) ------------------------------------------------------------------------
    private static readonly List<DisplayInfo> _monitors = new List<DisplayInfo>();

    // Écrans branchés, dans l'ordre de Windows.
    public static List<DisplayInfo> Monitors()
    {
        _monitors.Clear();
        try { Screen.GetDisplayLayout(_monitors); } catch { }
        return _monitors;
    }

    // Écran choisi dans les Paramètres, sinon celui où se trouve la fenêtre.
    public static int CurrentMonitorIndex()
    {
        List<DisplayInfo> list = Monitors();
        if (int.TryParse(GameSettings.GetString(GameSettings.Monitor), out int saved) && saved >= 0 && saved < list.Count)
            return saved;
        DisplayInfo current = Screen.mainWindowDisplayInfo;
        for (int i = 0; i < list.Count; i++)
            if (list[i].Equals(current)) return i;
        return 0;
    }

    private Coroutine _monitorMove;

    // Déplace la fenêtre du jeu sur l'écran choisi (centrée), puis réapplique mode et résolution : la liste des
    // résolutions et la taille du bureau sont celles du nouvel écran une fois le déplacement terminé.
    private void ApplyMonitor()
    {
        if (Application.isEditor) return;
        if (!int.TryParse(GameSettings.GetString(GameSettings.Monitor), out int index)) return;
        List<DisplayInfo> list = Monitors();
        if (index < 0 || index >= list.Count) return;
        DisplayInfo target = list[index];
        if (target.Equals(Screen.mainWindowDisplayInfo)) return;
        if (_monitorMove != null) StopCoroutine(_monitorMove);
        _monitorMove = StartCoroutine(MoveToMonitor(target));
    }

    private System.Collections.IEnumerator MoveToMonitor(DisplayInfo target)
    {
        Vector2Int pos = Vector2Int.zero;
        if (Screen.fullScreenMode == FullScreenMode.Windowed)
            pos = new Vector2Int(Mathf.Max(0, (target.width - Screen.width) / 2), Mathf.Max(0, (target.height - Screen.height) / 2));
        AsyncOperation op = Screen.MoveMainWindowTo(target, pos);
        while (op != null && !op.isDone) yield return null;
        _monitorMove = null;
        ApplyDisplay();
    }

    public static bool TryParseResolution(string s, out Vector2Int v)
    {
        v = default;
        if (string.IsNullOrEmpty(s)) return false;
        string[] p = s.Split('x');
        if (p.Length != 2 || !int.TryParse(p[0], out int w) || !int.TryParse(p[1], out int h)) return false;
        v = new Vector2Int(w, h);
        return true;
    }

    // MODIFIÉ (2026-11-10, retours utilisateur) : les deux pleins écrans utilisent toujours la résolution native de l'écran
    // (la « Résolution » des Paramètres ne règle que la taille de la fenêtre), et la résolution est réappliquée quelques
    // images plus tard si Windows ne l'a pas prise au passage fenêtre -> plein écran.
    // MODIFIÉ (2026-11-15) - la fenêtre a exactement la taille choisie (formats 16:9, plus de réduction automatique) ;
    // « Espace de travail » est calculé avec la vraie barre de titre et la vraie barre des tâches (Win32, voir plus bas).
    // Une fenêtre qui ne tient pas dans l'espace libre est collée en haut à gauche de celui-ci (barre de titre visible).
    private Coroutine _displayFollowUp;

    private void ApplyDisplay()
    {
        FullScreenMode mode = ModeFromSetting(GameSettings.GetInt(GameSettings.DisplayMode));
        if (Application.isEditor) return;    // la fenêtre Game de l'éditeur ne se redimensionne pas

        Vector2Int native = NativeResolution();
        Vector2Int res = mode == FullScreenMode.Windowed ? WindowSizeForKey(GameSettings.GetString(GameSettings.Resolution)) : native;

        Screen.SetResolution(res.x, res.y, mode);
        if (_displayFollowUp != null) StopCoroutine(_displayFollowUp);
        _displayFollowUp = StartCoroutine(DisplayFollowUp(res, mode));
    }

    // Résolution native de l'écran du jeu (celle des deux modes plein écran).
    public static Vector2Int NativeResolution()
    {
        DisplayInfo d = Screen.mainWindowDisplayInfo;
        return d.width > 0 ? new Vector2Int(d.width, d.height)
                           : new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
    }

    // Plus grand 16:9 qui tient dans l'espace libre de l'écran (sans la barre des tâches), cadre de la fenêtre compris.
    public static Vector2Int WorkAreaWindowSize()
    {
        RectInt work = WorkAreaRect();
        Vector2Int frame = WindowFrameSize();
        int availW = Mathf.Max(320, work.width - frame.x);
        int availH = Mathf.Max(180, work.height - frame.y);
        int h = Mathf.Min(availH, Mathf.FloorToInt(availW * 9f / 16f));
        int w = Mathf.RoundToInt(h * 16f / 9f);
        if (w > availW) { w = availW; h = Mathf.FloorToInt(w * 9f / 16f); }
        return new Vector2Int(w, h);
    }

    // Quelques images après le changement : le plein écran doit avoir pris la résolution demandée (sinon on la redemande),
    // et une fenêtre est replacée dans l'espace libre de son écran.
    private System.Collections.IEnumerator DisplayFollowUp(Vector2Int res, FullScreenMode mode)
    {
        for (int i = 0; i < 3; i++) yield return null;
        if (mode != FullScreenMode.Windowed)
        {
            if (Screen.width != res.x || Screen.height != res.y || Screen.fullScreenMode != mode)
                Screen.SetResolution(res.x, res.y, mode);
        }
        else
        {
            if (Screen.width != res.x || Screen.height != res.y) Screen.SetResolution(res.x, res.y, mode);
            yield return null;
            PlaceWindowInWorkArea();
        }
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        _lastWindowTop = int.MinValue;
#endif
        _displayFollowUp = null;
    }

    // ---- fenêtre glissée en haut de l'écran = « Espace de travail » (AJOUTÉ 2026-11-15) --------------------------------
    // Retour utilisateur : « si on glisse la fenêtre en haut de l'écran et qu'on relâche, elle passe en grand dans
    // l'espace de travail, comme Windows ». La fenêtre reste non redimensionnable (toujours 16:9) : le jeu le fait lui-même.
    // Bouton gauche relâché, haut de la fenêtre contre le haut de l'espace libre, taille différente de l'espace de travail
    // -> choix « Espace de travail » (enregistré dans les Paramètres, comme si le joueur l'avait choisi).
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private float _snapCheckTimer;
    private int _lastWindowTop = int.MinValue;
#endif

    private void UpdateWindowSnap()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (Screen.fullScreenMode != FullScreenMode.Windowed || _displayFollowUp != null || !Application.isFocused) return;
        _snapCheckTimer -= Time.unscaledDeltaTime;
        if (_snapCheckTimer > 0f) return;
        _snapCheckTimer = 0.2f;
        if ((Win32.GetAsyncKeyState(0x01) & 0x8000) != 0) return;   // glissement en cours
        System.IntPtr hwnd = Win32.GameWindow();
        if (hwnd == System.IntPtr.Zero || !Win32.GetWindowRect(hwnd, out Win32.RECT wr)) return;
        bool moved = _lastWindowTop != int.MinValue && wr.top != _lastWindowTop;
        _lastWindowTop = wr.top;
        if (!moved) return;
        RectInt work = WorkAreaRect();
        if (wr.top > work.y + 2) return;
        Vector2Int target = WorkAreaWindowSize();
        if (Screen.width == target.x && Screen.height == target.y) { PlaceWindowInWorkArea(); return; }
        if (GameSettings.GetString(GameSettings.Resolution) == WorkAreaKey) ApplyDisplay();
        else GameSettings.SetString(GameSettings.Resolution, WorkAreaKey);   // -> OnSettingChanged -> ApplyDisplay
#endif
    }

    // Centre la fenêtre dans l'espace libre de son écran ; collée en haut si elle est aussi haute que lui.
    private static void PlaceWindowInWorkArea()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        System.IntPtr hwnd = Win32.GameWindow();
        if (hwnd == System.IntPtr.Zero || !Win32.GetWindowRect(hwnd, out Win32.RECT wr)) return;
        RectInt work = WorkAreaRect();
        int w = wr.right - wr.left, h = wr.bottom - wr.top;
        int x = work.x + Mathf.Max(0, (work.width - w) / 2);
        int y = work.y + Mathf.Max(0, (work.height - h) / 2);
        Win32.SetWindowPos(hwnd, System.IntPtr.Zero, x, y, 0, 0, Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
#endif
    }

    // Espace libre de l'écran de la fenêtre (sans la barre des tâches), en pixels du bureau.
    private static RectInt WorkAreaRect()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        System.IntPtr hwnd = Win32.GameWindow();
        if (hwnd != System.IntPtr.Zero)
        {
            System.IntPtr mon = Win32.MonitorFromWindow(hwnd, 2 /* MONITOR_DEFAULTTONEAREST */);
            Win32.MONITORINFO mi = new Win32.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Win32.MONITORINFO)) };
            if (mon != System.IntPtr.Zero && Win32.GetMonitorInfo(mon, ref mi))
                return new RectInt(mi.rcWork.left, mi.rcWork.top, mi.rcWork.right - mi.rcWork.left, mi.rcWork.bottom - mi.rcWork.top);
        }
#endif
        DisplayInfo d = Screen.mainWindowDisplayInfo;
        RectInt area = d.workArea;
        if (area.width <= 0 || area.height <= 0) area = new RectInt(0, 0, Mathf.Max(1024, d.width), Mathf.Max(576, d.height - 48));
        return area;
    }

    // Place prise par le cadre de la fenêtre autour de la zone de jeu (barre de titre + bords), en pixels.
    private static Vector2Int WindowFrameSize()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        System.IntPtr hwnd = Win32.GameWindow();
        if (hwnd != System.IntPtr.Zero && Screen.fullScreenMode == FullScreenMode.Windowed
            && Win32.GetWindowRect(hwnd, out Win32.RECT wr) && Win32.GetClientRect(hwnd, out Win32.RECT cr))
        {
            int fw = (wr.right - wr.left) - (cr.right - cr.left), fh = (wr.bottom - wr.top) - (cr.bottom - cr.top);
            if (fw >= 0 && fh > 0 && fh < 200) return new Vector2Int(fw, fh);
        }
#endif
        return new Vector2Int(16, 39);   // valeurs Windows 10/11 à 100 % (barre de titre 31 + bords) quand on ne peut pas mesurer
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private static class Win32
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct RECT { public int left, top, right, bottom; }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

        public const uint SWP_NOSIZE = 0x0001, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;

        [System.Runtime.InteropServices.DllImport("user32.dll")] public static extern System.IntPtr GetActiveWindow();
        [System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool GetWindowRect(System.IntPtr hWnd, out RECT rect);
        [System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool GetClientRect(System.IntPtr hWnd, out RECT rect);
        [System.Runtime.InteropServices.DllImport("user32.dll")] public static extern System.IntPtr MonitorFromWindow(System.IntPtr hWnd, uint flags);
        [System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool GetMonitorInfo(System.IntPtr hMonitor, ref MONITORINFO info);
        [System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool SetWindowPos(System.IntPtr hWnd, System.IntPtr after, int x, int y, int cx, int cy, uint flags);
        [System.Runtime.InteropServices.DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);

        private static System.IntPtr _hwnd;

        // Fenêtre du jeu : mémorisée la première fois qu'elle est active (GetActiveWindow ne renvoie rien sans le focus).
        public static System.IntPtr GameWindow()
        {
            if (_hwnd == System.IntPtr.Zero && Application.isFocused) _hwnd = GetActiveWindow();
            return _hwnd;
        }
    }
#endif

    private void ApplyQuality()
    {
        int q = Mathf.Clamp(GameSettings.GetInt(GameSettings.Quality), 0, QualitySettings.names.Length - 1);
        if (QualitySettings.GetQualityLevel() != q) QualitySettings.SetQualityLevel(q, true);
        ApplyQualityExtras();
    }

    // AJOUTÉ (2026-11-11, retour utilisateur : "est-ce que la Qualité graphique a vraiment un impact en jeu ? Il faut que le
    // joueur voie la différence, surtout entre Performance et Élevé") - les trois profils URP ont été refaits (Performance :
    // rendu 3D à 75 % agrandi par FSR, sans ombres ; Équilibré : ombres 2048 ; Élevé : MSAA x4, ombres 4096 nettes), et ce qui
    // ne se règle pas dans un profil URP est appliqué ici, à chaque changement de niveau et à chaque scène :
    //  - lissage des bords par caméra : SMAA en Équilibré (Élevé a déjà le MSAA x4, Performance n'en a pas) ;
    //  - lueur douce (bloom) des effets lumineux en Élevé seulement, par un volume global créé ici (la scène n'en a pas).
    private const int QualityBalanced = 1, QualityHigh = 2;
    private UnityEngine.Rendering.Volume _qualityVolume;

    private void ApplyQualityExtras()
    {
        int q = QualitySettings.GetQualityLevel();
        foreach (Camera cam in Camera.allCameras)
        {
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            if (data == null || data.renderType != CameraRenderType.Base) continue;
            data.antialiasing = q == QualityBalanced ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None;
            data.antialiasingQuality = AntialiasingQuality.Medium;
            if (q == QualityHigh) data.renderPostProcessing = true;   // nécessaire au bloom
        }

        if (_qualityVolume == null)
        {
            var go = new GameObject("[QualityVolume]");
            go.transform.SetParent(transform, false);
            _qualityVolume = go.AddComponent<UnityEngine.Rendering.Volume>();
            _qualityVolume.isGlobal = true;
            _qualityVolume.priority = 100f;
            var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);        // seuls les points plus lumineux que le blanc (effets, cristaux) rayonnent
            bloom.intensity.Override(0.45f);
            bloom.scatter.Override(0.6f);
            bloom.highQualityFiltering.Override(true);
            _qualityVolume.sharedProfile = profile;
        }
        _qualityVolume.enabled = q == QualityHigh;
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
        _fpsText.color = Color.white;                        // MODIFIÉ (2026-10-25) : blanc + contour noir, lisible sur le sol comme sur les menus sombres
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
        // MODIFIÉ (2026-10-29, retour utilisateur : "N'affiche pas les ips dans le loading panel") - jamais sur l'écran de
        // chargement du lancement, seulement dans le menu et en partie.
        bool on = GameSettings.GetBool(GameSettings.ShowFps) && SceneManager.GetActiveScene().name != "LoadingScreen";
        _fpsText.gameObject.SetActive(on);
        _fpsAccum = 0f; _fpsFrames = 0; _fpsTime = 0f;
    }

    // AJOUTÉ (2026-10-25) - couleur choisie dans les Paramètres (GameSettings.FpsColors), toujours sur le contour noir
    // d'origine : les teintes claires restent lisibles sur le sol clair comme sombre.
    private void ApplyFpsColor()
    {
        Color[] palette = GameSettings.FpsColors;
        Color c = palette[Mathf.Clamp(GameSettings.GetInt(GameSettings.FpsColor), 0, palette.Length - 1)];
        _fpsText.color = c;
        // MODIFIÉ (2026-10-25) - un texte foncé n'a pas de contour : un contour noir le noierait, un blanc l'épaissit trop.
        bool dark = c.r + c.g + c.b < 0.6f;
        _fpsText.outlineWidth = dark ? 0f : 0.2f;
        _fpsText.outlineColor = new Color32(0, 0, 0, 255);
    }

    private float _fpsLastAvg;
    private CanvasGroup _fpsGroup;

    private void UpdateFps()
    {
        if (!_fpsText.gameObject.activeSelf) return;

        // caché pendant le noir des transitions de scène, en fondu avec lui (le compteur est dessiné au-dessus du noir)
        if (_fpsGroup == null) _fpsGroup = _fpsText.gameObject.AddComponent<CanvasGroup>();
        float black = SceneTransitionFader.Instance != null ? SceneTransitionFader.Instance.BlackAlpha : 0f;
        float target = 1f - Mathf.Clamp01(black * 1.25f);
        // disparaît avec le noir ; réapparaît en 0,3 s au moins (même si le noir s'efface très vite)
        _fpsGroup.alpha = target < _fpsGroup.alpha ? target
                        : Mathf.MoveTowards(_fpsGroup.alpha, target, Mathf.Min(Time.unscaledDeltaTime, 1f / 30f) / 0.3f);

        _fpsAccum += Time.unscaledDeltaTime;
        _fpsFrames++;
        _fpsTime += Time.unscaledDeltaTime;
        if (_fpsTime < 0.4f) return;
        WriteFps(_fpsAccum / Mathf.Max(1, _fpsFrames));
        _fpsAccum = 0f; _fpsFrames = 0; _fpsTime = 0f;
    }

    private void WriteFps(float avg)
    {
        _fpsLastAvg = avg;
        // Unités et virgule selon la langue : « IPS » en français, « FPS » ailleurs, « мс » en russe (Loc).
        string fps = Mathf.RoundToInt(1f / Mathf.Max(0.0001f, avg)) + " " + Loc.T("IPS"), ms = Loc.Num(avg * 1000f, "0.0") + " " + Loc.T("ms");
        _fpsText.text = _fpsCompact ? fps + "\n" + ms : fps + "  (" + ms + ")";
    }
}
