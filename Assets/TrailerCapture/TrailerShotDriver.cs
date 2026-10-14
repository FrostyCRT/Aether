#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// OUTIL TRAILER (2026-10-14) - met en scène une partie (ou le menu) pour enregistrer les plans du trailer Steam
// (projet Remotion "Aether Storm Trailer", à côté du projet Unity). Éditeur UNIQUEMENT (#if UNITY_EDITOR) : jamais
// compilé dans un build. Tout le dossier Assets/TrailerCapture peut être supprimé sans aucun effet sur le jeu.
//
// Capture image par image : Time.captureFramerate fige le pas de temps à 1/60 s, chaque image est lue en fin de frame
// (UI comprise) et écrite en JPG, puis Editor/TrailerCapture.cs assemble le MP4 avec ffmpeg. Le résultat est fluide
// même si l'éditeur rame ou n'a pas le focus (la vidéo ne dépend pas du temps réel).
// Ne touche ni à la sauvegarde (SaveSystem.SuppressWrites) ni aux réglages du joueur (restaurés à la fin).
//
// Retours utilisateur intégrés (2026-10-14, v2) : caméra proche du cadrage réel du jeu (pas de gros plan), pilote
// "joueur" naturel au lieu de cercles, boss cadré avec le héros, AUCUNE fusion et le moins possible d'armes
// d'amélioration (pas encore leurs vrais modèles 3D) : arme de départ + arme exclusive + bonus de stats.
public class TrailerShotDriver : MonoBehaviour
{
    public const int Fps = 60;
    public static Action<string, string, int> OnShotCaptured; // (plan, dossier d'images, nombre d'images) - branché par l'éditeur
    public static string Status = "idle";
    public static string FramesRoot;

    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private enum Pilot { None, Natural, Boss }

    private class Shot
    {
        public string Name;
        public bool Menu;                     // plan du menu principal (pas de partie)
        public int Width = 1920, Height = 1080; // résolution de capture (menus en 1440p pour pouvoir zoomer au montage)
        public int Character;                 // 0 Aether, 1 Kael, 2 Lyra
        public float RunMinutes = 2f;         // chrono de départ (densité/variété des ennemis)
        public int BossesKilled = 0;          // boss déjà "battus" (évite qu'un boss arrive pendant un plan de horde)
        public UpgradeType[] Upgrades;        // montées au max ; null = aucune (jamais de fusion)
        public int Level = 1;                 // niveau affiché au HUD
        public float Preroll = 6f;            // secondes de jeu (accélérées, non enregistrées) pour peupler la carte
        public float Duration = 8f;           // durée enregistrée
        public Pilot Pilot = Pilot.Natural;
        public float Aggro = 1f;              // >1 : le pilote reste plus au contact de la horde
        public bool AutoDash = true;          // dash réflexe quand la pression devient forte
        public int Seed = 1;
        public List<(float time, Action<TrailerShotDriver> action)> Actions = new();
        public float Zoom = 0.85f;            // x taille orthographique de la caméra (1 = cadrage du jeu)
        public int MaxEnemies;                // densité imposée (0 = celle du jeu)
        public int PerWave = 8;
        public float SpawnInterval = 0.8f;
        public Action<TrailerShotDriver> MenuSetup; // préparation du menu avant "Moteur !"
    }

    // Uniquement des bonus de stats (aucun nouveau visuel) + l'arme exclusive du perso (filtrée par le jeu).
    private static UpgradeType[] HeroBuild(UpgradeType exclusive) =>
        new[] { exclusive, UpgradeType.Damage, UpgradeType.FireRate, UpgradeType.DoubleShot };

    private Shot _shot;
    private GameObject _player;
    private HealthSystem _health;
    private bool _prevShowFps, _prevKeyLegend;
    private EnemySpawner _spawner;

    // ====================================================================================
    // CATALOGUE DES PLANS
    // ====================================================================================
    private static Shot Build(string name)
    {
        switch (name)
        {
            // ---------------- MENU (1440p) ----------------
            // Accueil : le gantelet fait défiler les 4 modes de jeu.
            case "menu_modes":
                return new Shot { Name = name, Menu = true, Width = 2560, Height = 1440, Duration = 7.5f, Preroll = 1.5f,
                    Actions = {
                        (0.4f, d => d.CursorTo(d.ModeArrow(), 0.8f)),
                        (1.5f, d => d.ClickModeArrow()),
                        (3.0f, d => d.ClickModeArrow()),
                        (4.5f, d => d.ClickModeArrow()),
                        (6.0f, d => d.ClickModeArrow()),
                    } };

            // Personnages : Kael verrouillé (chaînes, coût en Éclats) puis débloqué sous nos yeux.
            case "menu_unlock":
                return new Shot { Name = name, Menu = true, Width = 2560, Height = 1440, Duration = 9f, Preroll = 1.5f,
                    MenuSetup = d =>
                    {
                        var meta = MetaProgressionManager.Instance;
                        meta.Data.kaelUnlocked = false; // en mémoire uniquement (SuppressWrites)
                        meta.Data.selectedCharacterIndex = 0;
                    },
                    Actions = {
                        (0.3f, d => d.CursorTo(d.Tab("_characterSelectTabImage"), 0.7f)),
                        (1.1f, d => d.ClickTab("_characterSelectTabImage", "ShowCharacterSelect")),
                        (2.2f, d => d.CursorTo(d.CharacterArrow(), 0.6f)),
                        (2.9f, d => d.ClickCharacterArrow()),
                        (4.4f, d => d.CursorTo(d.UnlockButton(), 0.8f)),
                        (5.5f, d => d.ClickUnlock()),
                        (7.6f, d => d.CursorTo(d.SelectButton(), 0.7f)),
                        (8.4f, d => d.ClickSelect()),
                    } };

            // Arbre de compétences : on ouvre la fiche d'un nœud et on achète ses niveaux (pop + gerbe d'étincelles).
            case "menu_skilltree":
                return new Shot { Name = name, Menu = true, Width = 2560, Height = 1440, Duration = 10f, Preroll = 1.5f,
                    MenuSetup = d =>
                    {
                        var meta = MetaProgressionManager.Instance;
                        meta.Data.selectedCharacterIndex = 0;
                        meta.ResetSkillTree(); // en mémoire uniquement (SuppressWrites)
                    },
                    Actions = {
                        (0.3f, d => d.CursorTo(d.Tab("_upgradesTabImage"), 0.7f)),
                        (1.1f, d => d.ClickTab("_upgradesTabImage", "ShowUpgrades")),
                        (2.0f, d => d.CursorTo(d.Node("concentration"), 0.7f)),
                        (2.8f, d => d.ClickNode("concentration")),
                        (4.2f, d => d.CursorTo(d.BuyButton(), 0.6f)),
                        (4.9f, d => d.ClickBuy()),
                        (5.9f, d => d.ClickBuy()),
                        (6.9f, d => d.ClickBuy()),
                        (8.0f, d => d.CursorTo(d.Node("cadence"), 0.6f)),
                        (8.7f, d => d.ClickNode("cadence")),
                    } };

            // Réputation : bonus permanents + skins (simple présence, le gantelet survole).
            case "menu_reputation":
                return new Shot { Name = name, Menu = true, Width = 2560, Height = 1440, Duration = 5f, Preroll = 1.5f,
                    Actions = {
                        (0.3f, d => d.CursorTo(d.Tab("_reputationTabImage"), 0.7f)),
                        (1.1f, d => d.ClickTab("_reputationTabImage", "ShowReputation")),
                    } };

            // ---------------- JEU ----------------
            case "hero_aether":
                return new Shot { Name = name, Character = 0, RunMinutes = 6f, BossesKilled = 1, Upgrades = HeroBuild(UpgradeType.Fireball),
                    Level = 12, Preroll = 14f, Duration = 8f, MaxEnemies = 110, PerWave = 10, SpawnInterval = 0.5f, Seed = 11, Aggro = 1.3f };

            case "hero_kael":
                return new Shot { Name = name, Character = 1, RunMinutes = 6f, BossesKilled = 1, Upgrades = HeroBuild(UpgradeType.AuraUpgrade),
                    Level = 12, Preroll = 14f, Duration = 8f, MaxEnemies = 120, PerWave = 12, SpawnInterval = 0.5f, Seed = 22, Aggro = 1.6f };

            case "hero_lyra":
                return new Shot { Name = name, Character = 2, RunMinutes = 6f, BossesKilled = 1, Upgrades = HeroBuild(UpgradeType.Knives),
                    Level = 12, Preroll = 14f, Duration = 8f, MaxEnemies = 110, PerWave = 10, SpawnInterval = 0.5f, Seed = 33, Aggro = 1.3f,
                    Actions = { (3.0f, d => d.PhantomClone()) } };

            // Horde de fin de partie + Ultime (déclenché à 4 s, calé sur le drop de la musique au montage).
            case "horde_ultimate":
                return new Shot { Name = name, Character = 0, RunMinutes = 13.5f, BossesKilled = 2, Upgrades = HeroBuild(UpgradeType.Fireball),
                    Level = 31, Preroll = 16f, Duration = 10f, MaxEnemies = 170, PerWave = 18, SpawnInterval = 0.35f, Seed = 44, Aggro = 1.8f,
                    AutoDash = false, Actions = { (4.0f, d => d.Ultimate(empowered: true)) } };

            // Esquive -> Absorption (Nova) -> Ultime : la boucle du Cristal.
            case "dash_absorb":
                return new Shot { Name = name, Character = 0, RunMinutes = 7f, BossesKilled = 1, Upgrades = HeroBuild(UpgradeType.Fireball),
                    Level = 14, Preroll = 14f, Duration = 8f, MaxEnemies = 120, PerWave = 12, SpawnInterval = 0.5f, Seed = 55, Aggro = 1.6f,
                    AutoDash = false,
                    Actions = { (0.8f, d => d.Dash()), (0.95f, d => d.Absorb()), (2.4f, d => d.Dash()), (2.55f, d => d.Absorb()),
                                (4.2f, d => d.Ultimate(empowered: false)) } };

            case "boss1":
                return new Shot { Name = name, Character = 0, RunMinutes = 4.9f, Upgrades = HeroBuild(UpgradeType.Fireball), Level = 15,
                    Preroll = 4f, Duration = 13f, Pilot = Pilot.Boss, Seed = 66 };
            case "boss2":
                return new Shot { Name = name, Character = 1, RunMinutes = 9.9f, BossesKilled = 1, Upgrades = HeroBuild(UpgradeType.AuraUpgrade), Level = 22,
                    Preroll = 4f, Duration = 13f, Pilot = Pilot.Boss, Seed = 77 };
            case "boss3":
                return new Shot { Name = name, Character = 2, RunMinutes = 14.9f, BossesKilled = 2, Upgrades = HeroBuild(UpgradeType.Knives), Level = 29,
                    Preroll = 4f, Duration = 13f, Pilot = Pilot.Boss, Seed = 88 };

            // Écran de choix d'amélioration (vrai panel du jeu), laissé à l'écran le temps de le lire.
            case "levelup":
                return new Shot { Name = name, Character = 0, RunMinutes = 3f, Upgrades = HeroBuild(UpgradeType.Fireball), Level = 6,
                    Preroll = 10f, Duration = 6f, MaxEnemies = 70, Seed = 99,
                    Actions = { (1.0f, d => LevelUpManager.Instance.ShowLevelUp()), (5.0f, d => LevelUpManager.Instance.SelectUpgrade(1)) } };
        }
        return null;
    }

    public static bool Exists(string name) => Build(name) != null;
    public static Vector2Int Resolution(string name) { var s = Build(name); return s == null ? new Vector2Int(1920, 1080) : new Vector2Int(s.Width, s.Height); }

    // ====================================================================================
    // DÉROULÉ
    // ====================================================================================
    public static TrailerShotDriver Launch(string shotName)
    {
        var shot = Build(shotName);
        if (shot == null) { Status = "unknown shot " + shotName; return null; }
        var go = new GameObject("[TrailerShotDriver]");
        DontDestroyOnLoad(go);
        var d = go.AddComponent<TrailerShotDriver>();
        d._shot = shot;
        d.StartCoroutine(d.Run());
        return d;
    }

    private IEnumerator Run()
    {
        Status = "setup";
        Application.runInBackground = true;
        SaveSystem.SuppressWrites = true;
        _prevShowFps = GameSettings.GetBool(GameSettings.ShowFps);
        _prevKeyLegend = GameSettings.GetBool(GameSettings.ShowKeyLegend);
        GameSettings.SetBool(GameSettings.ShowFps, false);
        GameSettings.SetBool(GameSettings.ShowKeyLegend, false);

        var meta = MetaProgressionManager.Instance;
        if (meta != null && meta.Data != null)
        {
            // Débloqués en mémoire seulement (jamais écrit sur disque) pour pouvoir filmer les 3 héros.
            meta.Data.kaelUnlocked = true;
            meta.Data.lyraUnlocked = true;
            meta.SetSelectedCharacter(_shot.Character);
        }

        if (_shot.Menu)
        {
            while (SceneManager.GetActiveScene().name != "MainMenu") yield return null;
            _shot.MenuSetup?.Invoke(this);
            // Les pages lisent l'état de la méta à l'activation : on repasse par l'accueil pour tout rafraîchir.
            var mm = FindFirstObjectByType<MainMenuManager>();
            if (mm != null) { mm.ShowSettings(); yield return null; mm.ShowMenu(); }
            if (SkillTreeUI.Instance != null) SkillTreeUI.Instance.RefreshAllNodes();
            yield return new WaitForSecondsRealtime(_shot.Preroll);
            HideDebugButtons();
        }
        else
        {
            if (SceneManager.GetActiveScene().name != "Game")
                SceneManager.LoadScene("Game");
            while (SceneManager.GetActiveScene().name != "Game" || GameManager.Instance == null) yield return null;
            while ((_player = GameObject.FindWithTag("Player")) == null) yield return null;

            // Attendre la fin de l'annonce de défi (verrou d'intro = IsPaused).
            yield return new WaitForSecondsRealtime(0.5f);
            while (GameManager.Instance.IsPaused) yield return null;

            SetupRun();

            // Préroll accéléré : la carte se remplit d'ennemis avant le "Moteur !".
            Status = "preroll";
            Time.timeScale = 3f;
            float t0 = Time.time;
            while (Time.time - t0 < _shot.Preroll)
            {
                Drive(Time.time - t0);
                AutoPickLevelUp();
                yield return null;
            }
            Time.timeScale = 1f;
        }

        // ---- Moteur ! ----
        // Invincible pendant le préroll seulement : à l'image, le héros encaisse les coups comme en vrai (flash,
        // chiffres rouges, barre de vie qui descend). Seule une vie plancher l'empêche de mourir (voir LateUpdate).
        if (!_shot.Menu && _health != null)
        {
            _health.RemoveExternalInvincibility();
            _health.Heal(1f);
        }
        Status = "recording";
        string dir = Path.Combine(FramesRoot, _shot.Name);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);

        Time.captureFramerate = Fps;
        int frames = Mathf.RoundToInt(_shot.Duration * Fps);
        int next = 0;
        _shot.Actions.Sort((a, b) => a.time.CompareTo(b.time));
        var wait = new WaitForEndOfFrame();

        for (int i = 0; i < frames; i++)
        {
            float t = i / (float)Fps;
            _now = t;
            _capturing = true;
            while (next < _shot.Actions.Count && t >= _shot.Actions[next].time)
            {
                try { _shot.Actions[next].action(this); } catch (Exception e) { Debug.LogException(e); }
                next++;
            }
            if (!_shot.Menu)
            {
                Drive(t + _shot.Preroll);
                if (_shot.Name != "levelup") AutoPickLevelUp();
            }

            yield return wait;
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(dir, $"f_{i:00000}.jpg"), tex.EncodeToJPG(94));
            Destroy(tex);
        }

        Time.captureFramerate = 0;
        Cleanup();
        Status = "encoding " + _shot.Name;
        OnShotCaptured?.Invoke(_shot.Name, dir, frames);
        Destroy(gameObject);
    }

    private void SetupRun()
    {
        var gm = GameManager.Instance;
        gm.DebugSetRunTimer(_shot.RunMinutes * 60f);
        SetField(gm, "_bossKillCount", _shot.BossesKilled);
        if (WaveManager.Instance != null) SetField(WaveManager.Instance, "_bossCount", _shot.BossesKilled);
        // Le spawner a son propre chrono de phases (densité) : on l'aligne sur le chrono de la run.
        _spawner = FindFirstObjectByType<EnemySpawner>();
        if (_spawner != null) SetField(_spawner, "_gameTimer", _shot.RunMinutes * 60f);
        ApplyZoom();

        _health = _player.GetComponent<HealthSystem>();
        if (_health != null) { _health.AddExternalInvincibility(); _health.Heal(1f); }

        var lum = LevelUpManager.Instance;
        if (lum != null && lum.AllUpgrades != null && _shot.Upgrades != null)
        {
            var wanted = new HashSet<UpgradeType>(_shot.Upgrades);
            foreach (var u in lum.AllUpgrades)
            {
                if (u == null || u.upgradeType == UpgradeType.Fusion || !wanted.Contains(u.upgradeType)) continue;
                int guard = 0;
                while (u.IsAvailable() && guard++ < 10) u.Apply();
            }
        }

        // Niveau affiché cohérent avec le build, et XP ralentie pour qu'aucun level-up ne coupe le plan.
        if (XPSystem.Instance != null)
        {
            SetField(XPSystem.Instance, "<CurrentLevel>k__BackingField", _shot.Level);
            var baseXp = XPSystem.Instance.GetType().GetField("_baseXP", Any);
            if (baseXp != null) baseXp.SetValue(XPSystem.Instance, (float)baseXp.GetValue(XPSystem.Instance) * 25f);
            // Effets normalement déclenchés par la montée de niveau : magnétisme des gemmes (niv. 3+) et HUD.
            if (XPGemSpawner.Instance != null) XPGemSpawner.Instance.OnLevelUp(_shot.Level);
            if (GameUI.Instance != null)
                GameUI.Instance.UpdateXPBar(XPSystem.Instance.CurrentXP, XPSystem.Instance.XPToNextLevel, _shot.Level);
        }

        if (_shot.Pilot == Pilot.Boss) SetupBossFraming();
    }

    // ====================================================================================
    // PILOTE "JOUEUR NATUREL" : esquive la horde (répulsion pondérée par la proximité), dérive avec un bruit
    // de Perlin (trajectoire jamais répétitive), reste dans l'arène, ramasse les gemmes proches quand c'est calme,
    // et dash par réflexe quand la pression devient forte. La direction est lissée (pas de virage sec).
    // ====================================================================================
    private Vector2 _dir = Vector2.right;
    private float _nextAutoDash;
    private readonly List<EnemyBase> _enemies = new();

    private void Drive(float t)
    {
        if (_player == null) return;
        Vector3 p = _player.transform.position;
        float seed = _shot.Seed * 17.31f;

        // Dérive : angle piloté par un bruit lent (+ un peu de bruit rapide pour les micro-corrections).
        float ang = Mathf.PerlinNoise(t * 0.18f, seed) * Mathf.PI * 4f + Mathf.PerlinNoise(t * 0.9f, seed + 5f) * 1.2f;
        Vector2 wander = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));

        Vector2 flee = Vector2.zero, toward = Vector2.zero;
        int close = 0;
        _enemies.Clear();
        _enemies.AddRange(FindObjectsByType<EnemyBase>(FindObjectsSortMode.None));
        foreach (var e in _enemies)
        {
            if (e == null || !e.isActiveAndEnabled) continue;
            Vector3 d3 = p - e.transform.position;
            Vector2 d = new Vector2(d3.x, d3.z);
            float m = d.magnitude;
            if (m < 0.01f) continue;
            if (m < 6f) { flee += d / (m * m); if (m < 2.5f) close++; }
            else if (m < 14f) toward -= d / (m * m * m);
        }
        Vector2 move;
        if (_shot.Pilot == Pilot.Boss) move = BossSteer(t, wander);
        else
        {
            // Aggro > 1 : on fuit moins fort et on revient vers la horde quand elle s'éloigne (plan plus rempli).
            move = wander * 0.9f + flee * (2.2f / _shot.Aggro) + toward * (20f * _shot.Aggro);
            // Reste près du centre de l'arène.
            Vector2 home = new Vector2(-p.x, -p.z);
            if (home.magnitude > 18f) move += home.normalized * 1.5f;
        }
        if (move.sqrMagnitude > 0.0001f) _dir = Vector2.Lerp(_dir, move.normalized, 0.08f).normalized;

        // Petites pauses naturelles (le joueur relâche les touches un instant).
        float pause = Mathf.PerlinNoise(t * 0.5f, seed + 9f);
        GameInput.DebugMoveOverride = pause < 0.22f && close == 0 ? Vector2.zero : _dir;

        if (_shot.AutoDash && close >= 3 && _now > _nextAutoDash && _capturing)
        {
            _nextAutoDash = _now + 2.8f;
            Dash();
        }
    }

    // ====================================================================================
    // BOSS : le héros garde le boss à distance moyenne en tournant autour (sens qui change de temps en temps), et la
    // caméra suit un point entre les deux -> boss toujours dans le cadre, y compris le boss 3 qui ne bouge pas.
    // ====================================================================================
    private Transform _camAnchor;
    private BossBase _boss;

    private void SetupBossFraming()
    {
        var bz = BossCameraZoom.Instance;
        var vcam = bz != null ? bz.GetType().GetField("_virtualCamera", Any)?.GetValue(bz) as Cinemachine.CinemachineVirtualCamera : null;
        if (vcam == null) return;
        _camAnchor = new GameObject("[TrailerCamAnchor]").transform;
        _camAnchor.position = _player.transform.position;
        vcam.Follow = _camAnchor;
    }

    private Vector2 BossSteer(float t, Vector2 wander)
    {
        if (_boss == null || !_boss.isActiveAndEnabled) _boss = FindFirstObjectByType<BossBase>();
        Vector3 p = _player.transform.position;
        if (_camAnchor != null)
        {
            Vector3 target = _boss != null ? Vector3.Lerp(p, _boss.transform.position, 0.4f) : p;
            target.y = p.y;
            _camAnchor.position = Vector3.Lerp(_camAnchor.position, target, 0.06f);
        }
        if (_boss == null) return wander;

        Vector3 d3 = _boss.transform.position - p;
        Vector2 to = new Vector2(d3.x, d3.z);
        float dist = to.magnitude;
        Vector2 dir = dist > 0.01f ? to / dist : Vector2.up;
        float side = Mathf.PerlinNoise(t * 0.12f, _shot.Seed) > 0.5f ? 1f : -1f;
        Vector2 tangent = new Vector2(-dir.y, dir.x) * side;
        float want = 5.5f + 1.5f * Mathf.Sin(t * 0.7f);
        float radial = Mathf.Clamp((dist - want) * 0.6f, -1.2f, 1.2f);
        return tangent + dir * radial + wander * 0.25f;
    }

    private void AutoPickLevelUp()
    {
        var lum = LevelUpManager.Instance;
        if (lum != null && lum.IsWaitingForChoice) lum.SelectUpgrade(0);
    }

    private void Cleanup()
    {
        GameInput.DebugMoveOverride = null;
        Time.timeScale = 1f;
        Time.captureFramerate = 0;
        GameSettings.SetBool(GameSettings.ShowFps, _prevShowFps);
        GameSettings.SetBool(GameSettings.ShowKeyLegend, _prevKeyLegend);
    }

    private void OnDestroy()
    {
        GameInput.DebugMoveOverride = null;
        Time.captureFramerate = 0;
    }

    // Cadrage : on change la taille "par défaut" de BossCameraZoom (sinon il ramènerait la caméra à sa taille
    // d'origine) et on l'applique tout de suite au virtual camera. Le dézoom d'arrivée de boss reste relatif.
    private void ApplyZoom()
    {
        if (Mathf.Approximately(_shot.Zoom, 1f) || BossCameraZoom.Instance == null) return;
        var bz = BossCameraZoom.Instance;
        var vcam = bz.GetType().GetField("_virtualCamera", Any)?.GetValue(bz) as Cinemachine.CinemachineVirtualCamera;
        if (vcam == null) return;
        float size = vcam.m_Lens.OrthographicSize * _shot.Zoom;
        SetField(bz, "_defaultOrthoSize", size);
        SetField(bz, "_targetOrthoSize", size);
        var lens = vcam.m_Lens;
        lens.OrthographicSize = size;
        vcam.m_Lens = lens;
    }

    // Vie plancher : le héros prend ses coups normalement mais ne descend jamais sous ce seuil (pas de Game Over en
    // plein plan). Plus haut contre les boss : un seul de leurs coups (saut du Cerf : 800) peut enlever beaucoup.
    private void EnforceHealthFloor()
    {
        if (_shot.Menu || _health == null) return;
        float floor = _health.MaxHealth * (_shot.Pilot == Pilot.Boss ? 0.5f : 0.35f);
        if (_health.CurrentHealth >= floor) return;
        SetField(_health, "_currentHealth", floor);
        if (GameUI.Instance != null) GameUI.Instance.UpdateHPBar(floor, _health.MaxHealth);
    }

    // Densité imposée, réappliquée à chaque frame (WaveManager.ApplyDifficulty réécrit la cadence à chaque Update).
    private void EnforceDensity()
    {
        // Le Bulbe est encore un cube provisoire (NOTES.md, suite 39) : pas montrable dans un trailer.
        foreach (var bulb in FindObjectsByType<EnemyBulb>(FindObjectsSortMode.None)) bulb.gameObject.SetActive(false);
        if (_shot.MaxEnemies <= 0 || _spawner == null) return;
        SetField(_spawner, "_maxEnemies", _shot.MaxEnemies);
        SetField(_spawner, "_enemiesPerWave", _shot.PerWave);
        SetField(_spawner, "_spawnInterval", _shot.SpawnInterval);
    }

    // ====================================================================================
    // ACTIONS DE JEU (appels directs aux méthodes du jeu, comme si la touche avait été pressée)
    // ====================================================================================
    private void Dash()
    {
        var pc = _player.GetComponent<PlayerController>();
        Vector2 m = GameInput.DebugMoveOverride is Vector2 v && v.sqrMagnitude > 0.01f ? v : _dir;
        Call(pc, "StartDash", new Vector3(m.x, 0f, m.y).normalized);
    }

    // Absorption d'un projectile pendant le dash : charge le Cristal et déclenche la Nova (CrystalSystem).
    private void Absorb() => _player.GetComponent<CrystalSystem>().AbsorbProjectile();

    private void Ultimate(bool empowered)
    {
        var cs = _player.GetComponent<CrystalSystem>();
        SetField(cs, "_storedUlts", empowered ? 2 : 1);
        Call(cs, "TriggerUlt");
    }

    private void PhantomClone()
    {
        var pc = _player.GetComponent<PlayerController>();
        var m = pc.GetType().GetMethod("SpawnPhantomClone", Any);
        if (m != null) pc.StartCoroutine((IEnumerator)m.Invoke(pc, null));
    }

    // ====================================================================================
    // MENU : curseur gantelet piloté + clics sur les vrais boutons
    // ====================================================================================
    private Vector2 _cursorFrom, _cursorTo;
    private float _cursorStart = -1f, _cursorDur = 1f, _clickTime = -10f, _now;
    private bool _cursorInit, _capturing;

    private RectTransform Tab(string field)
    {
        var mm = FindFirstObjectByType<MainMenuManager>();
        var img = mm != null ? mm.GetType().GetField(field, Any)?.GetValue(mm) as Image : null;
        return img != null ? img.rectTransform : null;
    }

    private T FieldOf<T>(MonoBehaviour owner, string field) where T : class =>
        owner != null ? owner.GetType().GetField(field, Any)?.GetValue(owner) as T : null;

    private RectTransform CharacterArrow() => (RectTransform)FieldOf<Button>(FindFirstObjectByType<CharacterSelectUI>(), "_rightArrow")?.transform;
    private RectTransform UnlockButton() => (RectTransform)FieldOf<Button>(FindFirstObjectByType<CharacterSelectUI>(), "_unlockWithEclatsButton")?.transform;
    private RectTransform SelectButton() => (RectTransform)FieldOf<Button>(FindFirstObjectByType<CharacterSelectUI>(), "_selectButton")?.transform;
    private RectTransform ModeArrow() => (RectTransform)FieldOf<Button>(FindFirstObjectByType<GameModeSelector>(), "_rightButton")?.transform;

    private RectTransform Node(string id)
    {
        foreach (var n in FindObjectsByType<SkillNode>(FindObjectsSortMode.None)) if (n.NodeId == id) return n.Rect;
        return null;
    }

    private RectTransform BuyButton()
    {
        var detail = FindFirstObjectByType<SkillDetailPanel>();
        return detail != null && detail.BuyButton != null ? (RectTransform)detail.BuyButton.transform : null;
    }

    private void Press(RectTransform rt)
    {
        _clickTime = _now;
        var b = rt != null ? rt.GetComponent<Button>() ?? rt.GetComponentInParent<Button>() : null;
        if (b != null && b.interactable) b.onClick.Invoke();
    }

    private void ClickTab(string field, string fallbackMethod)
    {
        _clickTime = _now;
        var rt = Tab(field);
        var btn = rt != null ? rt.GetComponentInParent<Button>() : null;
        if (btn != null && btn.onClick.GetPersistentEventCount() > 0) { btn.onClick.Invoke(); return; }
        var mm = FindFirstObjectByType<MainMenuManager>();
        if (mm != null) Call(mm, fallbackMethod);
    }

    private void ClickCharacterArrow() => Press(CharacterArrow());
    private void ClickModeArrow() => Press(ModeArrow());
    private void ClickUnlock() => Press(UnlockButton());
    private void ClickSelect() => Press(SelectButton());
    private void ClickBuy() => Press(BuyButton());

    private void ClickNode(string id)
    {
        _clickTime = _now;
        var rt = Node(id);
        var b = rt != null ? rt.GetComponent<Button>() : null;
        if (b != null) b.onClick.Invoke();
        else if (SkillTreeUI.Instance != null && rt != null) SkillTreeUI.Instance.OnNodeClicked(id, rt);
    }

    private void CursorTo(RectTransform target, float duration)
    {
        if (target == null) return;
        Vector3[] c = new Vector3[4];
        target.GetWorldCorners(c);
        var canvas = target.GetComponentInParent<Canvas>().rootCanvas;
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 center = RectTransformUtility.WorldToScreenPoint(cam, (c[0] + c[2]) * 0.5f);
        _cursorFrom = CurrentCursor();
        _cursorTo = center + new Vector2(8f, -10f) * (Screen.height / 1080f);
        _cursorStart = _now;
        _cursorDur = duration;
    }

    private Vector2 CurrentCursor()
    {
        if (!_cursorInit) { _cursorInit = true; _cursorTo = new Vector2(Screen.width * 0.62f, Screen.height * 0.32f); _cursorFrom = _cursorTo; }
        if (_cursorStart < 0f) return _cursorTo;
        float k = Mathf.Clamp01((_now - _cursorStart) / _cursorDur);
        k = k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) / 2f; // easeInOutCubic
        return Vector2.Lerp(_cursorFrom, _cursorTo, k);
    }

    // Après CustomCursor.Update (qui suit la vraie souris) et avant le rendu : on impose notre trajectoire.
    private void LateUpdate()
    {
        if (_shot != null && !_shot.Menu) EnforceDensity();
        if (!_capturing) return;
        EnforceHealthFloor();
        UpdateFakeCursor(_now);
        if (_shot.Menu) HideDebugButtons(); // une page peut le réactiver dans son OnEnable
    }

    private void UpdateFakeCursor(float t)
    {
        if (!_shot.Menu || CustomCursor.Instance == null) return;
        var img = CustomCursor.Instance.GetType().GetField("_image", Any)?.GetValue(CustomCursor.Instance) as Image;
        if (img == null) return;
        img.enabled = true;
        var rt = img.rectTransform;
        // Même conversion que CustomCursor.Update (ancré en bas à gauche, divisé par l'échelle du CanvasScaler).
        float scale = img.canvas.scaleFactor > 0f ? img.canvas.scaleFactor : 1f;
        rt.anchoredPosition = CurrentCursor() / scale;
        // Un peu plus gros qu'en jeu pour rester lisible dans une vidéo, avec un léger enfoncement au clic.
        float press = Mathf.Clamp01(1f - Mathf.Abs(t - _clickTime - 0.06f) / 0.12f);
        rt.localScale = Vector3.one * 1.4f * (1f - 0.18f * press);
    }

    // Masque les boutons de debug visibles dans le menu ("Réinitialiser" de Personnages et de l'arbre).
    private static void HideDebugButtons()
    {
        foreach (var txt in FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (txt.text != null && txt.text.Trim() == "Réinitialiser")
            {
                var b = txt.GetComponentInParent<Button>(true);
                (b != null ? b.gameObject : txt.gameObject).SetActive(false);
            }
        foreach (var txt in FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (txt.text != null && txt.text.Trim() == "Réinitialiser")
            {
                var b = txt.GetComponentInParent<Button>(true);
                (b != null ? b.gameObject : txt.gameObject).SetActive(false);
            }
    }

    // ====================================================================================
    private static void SetField(object target, string field, object value)
    {
        var f = target.GetType().GetField(field, Any);
        if (f == null) { Debug.LogWarning($"[Trailer] champ introuvable : {target.GetType().Name}.{field}"); return; }
        f.SetValue(target, value);
    }

    private static void Call(object target, string method, params object[] args)
    {
        var m = target.GetType().GetMethod(method, Any);
        if (m == null) { Debug.LogWarning($"[Trailer] méthode introuvable : {target.GetType().Name}.{method}"); return; }
        m.Invoke(target, args.Length == 0 ? null : args);
    }
}
#endif
