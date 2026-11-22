// Bot de vérification de l'ÉCLIPSE (2026-11-21), dérivé de RealBot.cs : vraies parties (arbre + Réputation selon le palier, cartes
// choisies), va CHERCHER l'éclat (dash à travers la horde, fuite seulement sous 25 % de PV), mesure AVANT / NUIT / APRÈS, une langue
// par partie, captures (bandeaux, nuit, pause et choix de carte pendant la nuit). Plan : "mode_palier_perso_langue" (mode = classic / esc).
// Lancement : Play depuis MainMenu, régler bot.dir (fin du fichier), envoyer TOUT le fichier à Unity_RunCommand ; eb_FIN.txt à la fin.
// Escarmouche Violent / Apocalyptique : le palier doit être débloqué dans la save (sinon le bouton Jouer refuse).
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

internal class ECfg2 { public string name; public int ch; public int tier; public int buys; public int rep; public float bossKeep = 7f; public GameMode mode; public int lang; }

internal class EWin2
{
    public string name; public float t0 = -1f, t1 = -1f; public int kills; public float dmg, xp, alive, close6, fpsSum, fpsMin = 999f, frames; public int samples, deaths, levels, gold;
    public string Line()
    {
        float d = Mathf.Max(0.01f, t1 - t0); float m = d / 60f;
        return string.Format(CultureInfo.InvariantCulture,
            "{0}: duree={1:F0}s kills={2} ({3:F0}/min) degatsSubis={4:F0} ({5:F0}/min) xp={6:F0} ({7:F0}/min) niveaux={8} vivantsMoy={9:F0} proches6mMoy={10:F1} morts={11} or={12} fpsMoy={13:F0} fpsMin={14:F0}",
            name, d, kills, kills / m, dmg, dmg / m, xp, xp / m, levels, samples > 0 ? alive / samples : 0f, samples > 0 ? close6 / samples : 0f, deaths, gold, frames > 0 ? fpsSum / frames : 0f, fpsMin);
    }
}

internal struct Snap2 { public float t, dmg, xp, alive, c6, fps; public int kills, deaths, levels, gold; }

internal class EclipseBot2 : MonoBehaviour
{
    public string dir; public Gamepad pad; public float speed = 2f;
    List<ECfg2> cfgs = new List<ECfg2>();
    int idx = -1; ECfg2 c; int state = 0; GameManager gm, oldGm; GameObject player; HealthSystem hp; PlayerController pc; CrystalSystem cs;
    float nextScan, nextUlt, startReal, endReal, lastDash = -99f, lastClone = -99f, steerUntil;
    Vector3 steerDir; EnemyBase[] enemies = new EnemyBase[0]; BossBase[] bosses = new BossBase[0]; EnemyProjectile[] projs = new EnemyProjectile[0];
    bool btnDown; float prevHp; int refills; int prevLevel; float prevXp, prevToNext; bool abandoned;
    int[] savedI; bool[] savedB; int savedTier, savedMode, savedLang;
    public string[] plan;
    GameObject shard; float shardSpawnT = -1f, shardSpawnReal, shardPickT = -1f, shardDist0, shardDmg; Vector3 shardPos; bool shardLost;
    bool nightWas; float nightStartT = -1f, nightEndT = -1f, nightStartRealTime, nightStartU; int capStep;
    float sunI0 = -1f, amb0, refl0; Color sun0; Light sun;
    string notes = ""; float nextShardFind;
    List<Snap2> hist = new List<Snap2>(); float cumDmg, cumXp; int cumLevels;
    int pauseStep; float pauseAt; bool lvlCapDone; float lvlCapAt = -1f;

    static readonly string[][] PURCHASES = {
        new[] { "concentration", "cadence", "concentration", "cadence", "crystalDamage", "concentration", "cadence", "crystalDamage", "fragmentation", "crystalDamage", "overpower" },
        new[] { "vitality", "recuperation", "vitality", "recuperation", "armor", "vitality", "recuperation", "armor", "secondWind", "armor", "manaShield" },
        new[] { "dash", "dash", "novaRadius", "dash", "impulsionNova", "novaRadius", "crystalMastery", "novaRadius", "phantomDash" } };
    static readonly string[] REP = { "dmg", "spd", "dmg", "spd", "reg", "dmg", "spd", "reg", "reg", "dmg", "spd", "reg" };
    static readonly string[] CHAR = { "aether", "kael", "lyra" };
    static readonly string[] TIER = { "modere", "violent", "apocalyptique" };
    static readonly string[] LANG = { "fr", "en", "de", "es", "pt", "it", "pl", "ru", "zh", "ja", "ko" };

    void Awake() { DontDestroyOnLoad(gameObject); }
    void Log(string s) { System.IO.File.AppendAllText(dir + "eb_" + c.name + ".txt", s + "\n"); }
    void Cap(string tag) { ScreenCapture.CaptureScreenshot(dir + c.name + "_" + tag + ".png"); }

    public void Init()
    {
        int[] buys = { 3, 7, 10 }, rep = { 2, 5, 9 };
        foreach (var e in plan)
        {
            string[] p = e.Split('_');
            int t = System.Array.IndexOf(TIER, p[1]), ch = System.Array.IndexOf(CHAR, p[2]), l = System.Array.IndexOf(LANG, p[3]);
            cfgs.Add(new ECfg2 { name = e, ch = ch, tier = t, buys = buys[t], rep = rep[t], bossKeep = ch == 1 ? 4f : 7f, mode = p[0] == "esc" ? GameMode.Skirmish : GameMode.Classic, lang = l });
        }
        var d = MetaProgressionManager.Instance.Data;
        savedI = new[] { d.cadenceLevel, d.crystalDamageLevel, d.concentrationLevel, d.vitalityLevel, d.armorLevel, d.recuperationLevel, d.dashLevel, d.novaRadiusLevel, d.selectedCharacterIndex, d.reputationDamageLevel, d.reputationSpeedLevel, d.reputationRegenLevel };
        savedB = new[] { d.fragmentationUnlocked, d.overpowerUnlocked, d.secondWindUnlocked, d.manaShieldUnlocked, d.crystalMasteryUnlocked, d.phantomDashUnlocked, d.impulsionNovaUnlocked, d.kaelUnlocked, d.lyraUnlocked };
        savedTier = (int)DifficultyTiers.Current; savedMode = (int)GameModes.Current; savedLang = GameSettings.GetInt(GameSettings.Language);
    }

    void Restore()
    {
        var d = MetaProgressionManager.Instance.Data;
        d.cadenceLevel = savedI[0]; d.crystalDamageLevel = savedI[1]; d.concentrationLevel = savedI[2]; d.vitalityLevel = savedI[3]; d.armorLevel = savedI[4]; d.recuperationLevel = savedI[5]; d.dashLevel = savedI[6]; d.novaRadiusLevel = savedI[7]; d.selectedCharacterIndex = savedI[8];
        d.reputationDamageLevel = savedI[9]; d.reputationSpeedLevel = savedI[10]; d.reputationRegenLevel = savedI[11];
        d.fragmentationUnlocked = savedB[0]; d.overpowerUnlocked = savedB[1]; d.secondWindUnlocked = savedB[2]; d.manaShieldUnlocked = savedB[3]; d.crystalMasteryUnlocked = savedB[4]; d.phantomDashUnlocked = savedB[5]; d.impulsionNovaUnlocked = savedB[6]; d.kaelUnlocked = savedB[7]; d.lyraUnlocked = savedB[8];
        DifficultyTiers.Current = (DifficultyTier)savedTier; GameModes.Current = (GameMode)savedMode;
        GameSettings.SetInt(GameSettings.Language, savedLang);
    }

    void ApplyProgression(SaveData d)
    {
        d.concentrationLevel = d.cadenceLevel = d.crystalDamageLevel = 0; d.fragmentationUnlocked = d.overpowerUnlocked = false;
        d.vitalityLevel = d.recuperationLevel = d.armorLevel = 0; d.secondWindUnlocked = d.manaShieldUnlocked = false;
        d.dashLevel = d.novaRadiusLevel = 0; d.impulsionNovaUnlocked = d.crystalMasteryUnlocked = d.phantomDashUnlocked = false;
        string[] list = PURCHASES[c.ch];
        for (int i = 0; i < Mathf.Min(c.buys, list.Length); i++)
            switch (list[i])
            {
                case "concentration": d.concentrationLevel++; break; case "cadence": d.cadenceLevel++; break; case "crystalDamage": d.crystalDamageLevel++; break;
                case "fragmentation": d.fragmentationUnlocked = true; break; case "overpower": d.overpowerUnlocked = true; break;
                case "vitality": d.vitalityLevel++; break; case "recuperation": d.recuperationLevel++; break; case "armor": d.armorLevel++; break;
                case "secondWind": d.secondWindUnlocked = true; break; case "manaShield": d.manaShieldUnlocked = true; break;
                case "dash": d.dashLevel++; break; case "novaRadius": d.novaRadiusLevel++; break; case "impulsionNova": d.impulsionNovaUnlocked = true; break;
                case "crystalMastery": d.crystalMasteryUnlocked = true; break; case "phantomDash": d.phantomDashUnlocked = true; break;
            }
        d.reputationDamageLevel = d.reputationSpeedLevel = d.reputationRegenLevel = 0;
        for (int i = 0; i < c.rep; i++) { if (REP[i] == "dmg") d.reputationDamageLevel++; else if (REP[i] == "spd") d.reputationSpeedLevel++; else d.reputationRegenLevel++; }
    }

    public void Next()
    {
        idx++;
        if (idx >= cfgs.Count)
        {
            Restore(); HealthSystem.DebugNeverDie = false;
            System.IO.File.AppendAllText(dir + "eb_FIN.txt", "fin\n");
            if (pad != null) InputSystem.RemoveDevice(pad);
            GameInput.DebugMoveOverride = null; Time.timeScale = 1f; Destroy(gameObject); return;
        }
        c = cfgs[idx];
        var d = MetaProgressionManager.Instance.Data;
        d.kaelUnlocked = true; d.lyraUnlocked = true; d.selectedCharacterIndex = c.ch;
        DifficultyTiers.Current = (DifficultyTier)c.tier; GameModes.Current = c.mode;
        GameSettings.SetInt(GameSettings.Language, c.lang);
        ApplyProgression(d);
        oldGm = GameManager.Instance; gm = null; state = 0;
        fleeing = false; lastDir = Vector3.zero; refills = 0;
        HealthSystem.DebugPreventedDeaths = 0; HealthSystem.DebugNeverDie = true; lastDash = lastClone = -99f; steerUntil = 0f;
        shard = null; shardSpawnT = shardPickT = -1f; shardDmg = 0f; shardLost = false; nightWas = false; nightStartT = nightEndT = -1f; capStep = 0;
        sunI0 = -1f; sun = null; notes = ""; hist.Clear(); cumDmg = cumXp = 0f; cumLevels = 0; nextHist = 0f; boss2Seen = -1f; checkLightAt = -1f;
        pauseStep = 0; lvlCapDone = false; lvlCapAt = -1f;
        Time.timeScale = 1f;
        bool wasAbandoned = abandoned; abandoned = false;
        if (!wasAbandoned && GameManager.Instance != null) GameManager.Instance.RestartGame(); else Object.FindAnyObjectByType<MainMenuManager>().PlayGame();
    }

    Vector3[] ePos = new Vector3[600]; float[] eRad = new float[600], eWt = new float[600]; int eN;
    bool fleeing; Vector3 lastDir;
    static readonly float[] LS_N = { 2.5f, 5f }, LW_N = { 1f, 0.6f }, LS_F = { 2.5f, 5f, 9f }, LW_F = { 1f, 0.8f, 0.8f };
    static float WallMargin(Vector3 q)
    {
        float m = 999f;
        if (!float.IsInfinity(MapBoundaryUtils.MinX)) m = Mathf.Min(m, q.x - MapBoundaryUtils.MinX);
        if (!float.IsInfinity(MapBoundaryUtils.MaxX)) m = Mathf.Min(m, MapBoundaryUtils.MaxX - q.x);
        if (!float.IsInfinity(MapBoundaryUtils.MinZ)) m = Mathf.Min(m, q.z - MapBoundaryUtils.MinZ);
        if (!float.IsInfinity(MapBoundaryUtils.MaxZ)) m = Mathf.Min(m, MapBoundaryUtils.MaxZ - q.z);
        return m;
    }
    Vector3 Steer(Vector3 p, Vector3 goal, bool flee, bool farOk, float dangerScale)
    {
        float[] Ls = flee ? LS_F : LS_N, Lw = flee ? LW_F : LW_N;
        Vector3 g = Vector3.ClampMagnitude(goal, 3f);
        float best = -1e9f, bestDanger = 0f; Vector3 bestDir = Vector3.zero;
        for (int i = 0; i < 16; i++)
        {
            float a = i * Mathf.PI / 8f; Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float danger = 0f, wall = 0f;
            for (int k = 0; k < Ls.Length; k++)
            {
                Vector3 q = p + d * Ls[k]; float dg = 0f;
                for (int e = 0; e < eN; e++)
                {
                    float dx = q.x - ePos[e].x, dz = q.z - ePos[e].z;
                    float t = 1f - Mathf.Sqrt(dx * dx + dz * dz) / eRad[e];
                    if (t > 0f) dg += t * eWt[e];
                }
                danger += dg * Lw[k];
                float mg = WallMargin(q);
                if (mg < -0.5f) wall += 4f * Lw[k]; else if (!farOk && mg < 6f) wall += (6f - mg) / 6f * 2.5f * Lw[k];
                if (!farOk && q.magnitude > 34f && q.magnitude > p.magnitude) wall += 1f;
            }
            danger *= dangerScale;
            float score = Vector3.Dot(d, g) - danger - wall + Vector3.Dot(d, lastDir) * 0.25f;
            if (score > best) { best = score; bestDir = d; bestDanger = danger; }
        }
        lastDir = bestDir;
        if (g.sqrMagnitude < 0.0001f && bestDanger < 0.05f) return Vector3.zero;
        return bestDir;
    }
    void Press(GamepadButton x) { InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(x)); btnDown = true; }
    float Score(UpgradeData u)
    {
        var lu = LevelUpManager.Instance; int lvl = lu.GetLevel(u); int owned = 0;
        foreach (var a in lu.AllUpgrades)
            if (a != null && lu.GetLevel(a) > 0 && (a.upgradeType == UpgradeType.Orbital || a.upgradeType == UpgradeType.Lightning || a.upgradeType == UpgradeType.MudPuddle || a.upgradeType == UpgradeType.BouncingOrb)) owned++;
        switch (u.upgradeType)
        {
            case UpgradeType.Fusion: return 100f;
            case UpgradeType.Fireball: case UpgradeType.AuraUpgrade: case UpgradeType.Knives: return 90f;
            case UpgradeType.Heal: return hp.CurrentHealth < hp.MaxHealth * 0.5f ? 85f : 5f;
            case UpgradeType.Damage: return 60f;
            case UpgradeType.FireRate: return 58f;
            case UpgradeType.DoubleShot: return 50f;
            default: return lvl > 0 ? 70f : (owned < 3 ? 65f : 30f);
        }
    }

    float NightLen => c.mode == GameMode.Skirmish ? 20f : 45f;

    EWin2 Window(string name, float t0, float t1)
    {
        var w = new EWin2 { name = name, t0 = t0, t1 = t1 };
        Snap2 a = default, b = default; bool ha = false, hb = false; float fs = 0f, fmin = 999f; int fn = 0; float al = 0f, c6 = 0f; int n = 0;
        foreach (var s in hist)
        {
            if (s.t < t0) { a = s; ha = true; continue; }
            if (s.t > t1) break;
            if (!ha) { a = s; ha = true; }
            b = s; hb = true; al += s.alive; c6 += s.c6; n++; fs += s.fps; fn++; if (s.fps < fmin) fmin = s.fps;
        }
        if (!ha || !hb) return w;
        w.kills = b.kills - a.kills; w.dmg = b.dmg - a.dmg; w.xp = b.xp - a.xp; w.levels = b.levels - a.levels;
        w.deaths = b.deaths - a.deaths; w.gold = b.gold - a.gold;
        w.alive = al; w.close6 = c6; w.samples = n; w.fpsSum = fs; w.frames = fn; w.fpsMin = fmin;
        return w;
    }

    float nextHist;
    bool wasDashing; float capAt, checkLightAt = -1f, boss2Seen = -1f;
    void Update()
    {
        Application.runInBackground = true;
        if (state == 0)
        {
            if (SceneManager.GetActiveScene().name != "Game" || GameManager.Instance == null || GameManager.Instance == oldGm) return;
            player = GameObject.FindWithTag("Player");
            if (player == null || XPSystem.Instance == null || LevelUpManager.Instance == null) return;
            gm = GameManager.Instance; hp = player.GetComponent<HealthSystem>(); pc = player.GetComponent<PlayerController>(); cs = player.GetComponent<CrystalSystem>();
            if (pad == null) pad = InputSystem.AddDevice<Gamepad>("EclipseBotPad");
            state = 1; startReal = Time.realtimeSinceStartup; prevHp = hp.CurrentHealth; prevLevel = XPSystem.Instance.CurrentLevel; prevXp = XPSystem.Instance.CurrentXP; prevToNext = XPSystem.Instance.XPToNextLevel;
            sun = RenderSettings.sun; if (sun == null) foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { sun = l; break; }
            if (sun != null) { sunI0 = sun.intensity; sun0 = sun.color; } amb0 = RenderSettings.ambientIntensity; refl0 = RenderSettings.reflectionIntensity;
            Log("DEBUT " + c.name + " perso=" + player.name + " palier=" + DifficultyTiers.Current + " mode=" + GameModes.Current + " langue=" + Loc.Current + " pvmax=" + hp.MaxHealth);
            return;
        }
        if (state == 2)
        {
            if (Time.realtimeSinceStartup - endReal < 3f) return;
            if (abandoned && (SceneManager.GetActiveScene().name != "MainMenu" || Object.FindAnyObjectByType<MainMenuManager>() == null)) return;
            Next(); return;
        }
        if (btnDown) { InputSystem.QueueStateEvent(pad, new GamepadState()); btnDown = false; }

        // Test de pause pendant la nuit : ouvre le menu Pause, capture, referme
        if (pauseStep == 1 && Time.unscaledTime >= pauseAt) { gm.TogglePause(); pauseStep = 2; pauseAt = Time.unscaledTime + 0.6f; return; }
        if (pauseStep == 2) { if (Time.unscaledTime >= pauseAt) { Cap("8_pause_nuit"); pauseStep = 3; pauseAt = Time.unscaledTime + 0.3f; } return; }
        if (pauseStep == 3) { if (Time.unscaledTime >= pauseAt) { gm.TogglePause(); pauseStep = 4; } return; }

        float cur = hp.CurrentHealth;
        if (cur < prevHp) { cumDmg += prevHp - cur; if (shardSpawnT >= 0f && shardPickT < 0f && !shardLost) shardDmg += prevHp - cur; }
        if (HealthSystem.DebugPreventedDeaths > refills) { refills = HealthSystem.DebugPreventedDeaths; hp.HealFlat(hp.MaxHealth); cur = hp.CurrentHealth; notes += string.Format(CultureInfo.InvariantCulture, "MORT@{0:F0}{1} ", gm.RunTimer, GameModes.NightActive ? "(nuit)" : ""); }
        prevHp = cur;
        var xs = XPSystem.Instance;
        if (xs.CurrentLevel > prevLevel) { cumXp += (prevToNext - prevXp) + xs.CurrentXP; cumLevels += xs.CurrentLevel - prevLevel; }
        else cumXp += Mathf.Max(0f, xs.CurrentXP - prevXp);
        prevLevel = xs.CurrentLevel; prevXp = xs.CurrentXP; prevToNext = xs.XPToNextLevel;

        bool nightNow = GameModes.NightActive;

        if (gm.IsGameOver)
        {
            GameInput.DebugMoveOverride = Vector2.zero;
            if (Time.timeScale > 0f) return;
            Finish(gm.BossKillCount >= 3 ? "VICTOIRE" : "MORT"); return;
        }
        bool classicDone = c.mode == GameMode.Classic && boss2Seen > 0f && Time.time - boss2Seen > 30f;
        bool noShardLeft = c.mode == GameMode.Classic && (shardLost || (nightEndT > 0f && gm.RunTimer > nightEndT + 50f));
        if (classicDone || noShardLeft || Time.realtimeSinceStartup - startReal > 900f) { Finish(classicDone ? "ARRET_APRES_BOSS2" : noShardLeft ? "ARRET_APRES_ECLIPSE" : "TEMPS_REEL_DEPASSE"); abandoned = true; gm.AbandonRun(); return; }

        var lu = LevelUpManager.Instance;
        if (lu.IsWaitingForChoice)
        {
            if (nightNow && !lvlCapDone)
            {
                if (lvlCapAt < 0f) { lvlCapAt = Time.unscaledTime + 0.8f; return; }
                if (Time.unscaledTime < lvlCapAt) return;
                Cap("9_carte_nuit"); lvlCapDone = true; return;
            }
            var ch = lu.CurrentChoices; int best = 0; float bs = -1f;
            for (int i = 0; i < ch.Count; i++) { float s = ch[i] != null ? Score(ch[i]) + Random.value : -1f; if (s > bs) { bs = s; best = i; } }
            lu.SelectUpgrade(best); return;
        }
        if (lu.IsChoosing || gm.IsPaused) return;
        if (Time.timeScale > 0f) Time.timeScale = speed;

        if (Time.time >= nextScan)
        {
            nextScan = Time.time + 0.1f;
            enemies = Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
            bosses = Object.FindObjectsByType<BossBase>(FindObjectsSortMode.None);
            projs = Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None);
        }
        BossBase main = null;
        foreach (var x in bosses) if (x != null && !x.IsSummoned && x.CurrentHealth > 0f) main = x;
        if (main != null && gm.BossKillCount == 1 && boss2Seen < 0f && c.mode == GameMode.Classic) boss2Seen = Time.time;

        Vector3 p = player.transform.position; p.y = 0f; float near = 999f; Vector3 np = p; int close = 0, c6 = 0, alive = 0; eN = 0;
        float R = c.ch == 1 ? 3.5f : 5f, engage = c.ch == 1 ? 3f : 7f;
        foreach (var en in enemies) { if (en == null || !en.isActiveAndEnabled) continue; alive++; Vector3 q = en.transform.position; q.y = 0f; float d = (p - q).magnitude; if (d < near) { near = d; np = q; } if (d < 3.5f) close++; if (d < 6f) c6++; if (d < 14f && eN < ePos.Length) { ePos[eN] = q; eRad[eN] = R; eWt[eN] = 1f; eN++; } }
        foreach (var x in bosses) { if (x == null) continue; Vector3 q = x.transform.position; q.y = 0f; float d = (p - q).magnitude; if (d < near) { near = d; np = q; } if (d < c.bossKeep + 8f && eN < ePos.Length) { ePos[eN] = q; eRad[eN] = c.bossKeep; eWt[eN] = 4f; eN++; } }

        if (gm.RunTimer >= nextHist)
        {
            nextHist = gm.RunTimer + 0.25f;
            hist.Add(new Snap2 { t = gm.RunTimer, dmg = cumDmg, xp = cumXp, alive = alive, c6 = c6, fps = 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), kills = gm.KillCount, deaths = refills, levels = cumLevels, gold = MetaProgressionManager.Instance.RunGold });
        }

        if (Time.unscaledTime >= nextShardFind) { nextShardFind = Time.unscaledTime + 0.2f; shard = GameObject.Find("NightShard"); }
        if (shard != null && shardSpawnT < 0f)
        {
            shardSpawnT = gm.RunTimer; shardSpawnReal = Time.time; shardPos = shard.transform.position; shardDist0 = Vector3.Distance(new Vector3(shardPos.x, 0, shardPos.z), p);
            Log(string.Format(CultureInfo.InvariantCulture, "ECLAT apparu chrono={0:F1} pos=({1:F1},{2:F1}) joueur=({3:F1},{4:F1}) distance={5:F1}m margeMur={6:F1} niv={7} vivants={8} pv={9:P0}",
                gm.RunTimer, shardPos.x, shardPos.z, p.x, p.z, shardDist0, WallMargin(shardPos), xs.CurrentLevel, alive, hp.CurrentHealth / hp.MaxHealth));
            Log("BUILD_A_L_ECLAT " + LevelUpManager.Instance.GetUpgradesSummary().Replace("\n", " | "));
            capStep = 1; capAt = Time.unscaledTime + 0.6f;
        }
        if (shardSpawnT >= 0f && shardPickT < 0f && !shardLost && shard == null && !nightNow)
        {
            Log(string.Format(CultureInfo.InvariantCulture, "ECLAT perdu (non ramasse) chrono={0:F1} apres={1:F1}s de jeu, distance restante={2:F1}m", gm.RunTimer, Time.time - shardSpawnReal, Vector3.Distance(new Vector3(shardPos.x, 0, shardPos.z), p)));
            shardLost = true; capStep = 10;
        }

        if (nightNow && !nightWas)
        {
            nightStartT = gm.RunTimer; shardPickT = gm.RunTimer; nightStartRealTime = Time.time; nightStartU = Time.unscaledTime;
            Log(string.Format(CultureInfo.InvariantCulture, "ECLAT ramasse chrono={0:F1} trajet={1:F1}s de jeu, PV perdus en route={2:F0} ({3:P0} des PV max) ; NUIT commence",
                gm.RunTimer, Time.time - shardSpawnReal, shardDmg, shardDmg / Mathf.Max(1f, hp.MaxHealth)));
            capStep = 3; capAt = Time.unscaledTime + 0.5f;
            pauseStep = 1; pauseAt = Time.unscaledTime + 5.5f;
        }
        if (!nightNow && nightWas)
        {
            nightEndT = gm.RunTimer;
            Log(string.Format(CultureInfo.InvariantCulture, "NUIT finie chrono={0:F1} duree={1:F1}s de jeu, boss present={2}, eclatsNuit={3}",
                gm.RunTimer, Time.time - nightStartRealTime, main != null, MetaProgressionManager.Instance.RunEclatsFromNight));
            capStep = 6; capAt = Time.unscaledTime + 0.4f; checkLightAt = Time.time + 1f;
        }
        nightWas = nightNow;

        if (checkLightAt > 0f && Time.time >= checkLightAt)
        {
            checkLightAt = -1f;
            bool ok = (sun == null || (Mathf.Abs(sun.intensity - sunI0) < 0.01f && ((Vector4)(sun.color - sun0)).magnitude < 0.01f)) && Mathf.Abs(RenderSettings.ambientIntensity - amb0) < 0.01f && Mathf.Abs(RenderSettings.reflectionIntensity - refl0) < 0.01f;
            Log(string.Format(CultureInfo.InvariantCulture, "LUMIERE apres nuit => {0} ; voile={1} lueurJoueur={2} fleche={3} compteur={4} nuitActive={5} multDegats={6:F2}",
                ok ? "OK" : "PAS RESTAUREE", GameObject.Find("NightOverlay") != null, GameObject.Find("NightPlayerLight") != null, GameObject.Find("TutorialPointer") != null, GameObject.Find("NightCounter") != null, GameModes.NightActive, PlayerBuffs.OutgoingDamageMultiplier));
        }

        if (capStep > 0 && capStep < 10 && Time.unscaledTime >= capAt)
        {
            switch (capStep)
            {
                case 1: Cap("1_eclat_apparu"); capStep = 2; capAt = Time.unscaledTime + 3f; break;
                case 2: Cap("2_en_route"); capStep = 9; break;
                case 3: Cap("3_eclipse_bandeau"); capStep = 4; capAt = Time.unscaledTime + 3f; break;
                case 4: Cap("4_nuit_pleine"); capStep = 5; capAt = Time.unscaledTime + 4f; break;
                case 5: Cap("5_nuit_compteur"); capStep = 9; break;
                case 6: Cap("6_jour_se_leve"); capStep = 7; capAt = Time.unscaledTime + 2f; break;
                case 7: Cap("7_apres"); capStep = 10; break;
            }
        }

        // Déplacement : l'éclat est l'objectif prioritaire (fuite seulement sous 25 % des PV)
        float hpFrac = hp.CurrentHealth / Mathf.Max(1f, hp.MaxHealth);
        bool seeking = shard != null;
        if (seeking) fleeing = hpFrac < 0.25f;
        else if (hpFrac < 0.4f || (close >= 7 && c.ch != 1)) fleeing = true; else if (hpFrac > 0.7f && close < 4) fleeing = false;
        Vector3 goal = Vector3.zero; bool farOk = seeking;
        var chest = Object.FindAnyObjectByType<BossChest>();
        Vector3 gp; Vector3 toShard = Vector3.zero;
        if (seeking) { Vector3 q = shard.transform.position; q.y = 0f; toShard = (q - p).normalized; }
        if (seeking && !fleeing) goal = toShard * 3f;
        else if (fleeing) { }
        else if (chest != null) { Vector3 q = chest.transform.position; q.y = 0f; goal = (q - p).normalized * 3f; }
        else if (near > 4.5f && XPGemField.Instance != null && XPGemField.Instance.TryGetNearest(p, 9f, out gp)) { gp.y = 0f; goal = (gp - p).normalized * 1.3f; }
        else if (near < 900f && near > engage) goal = (np - p).normalized;
        else if (near < 900f) { Vector3 radial = (np - p); radial.y = 0f; radial = radial.normalized; float side = Mathf.Sin(Time.time * 0.25f) > 0f ? 1f : -1f; goal = new Vector3(-radial.z, 0f, radial.x) * side * 0.7f; }
        else goal = new Vector3(Mathf.Cos(Time.time * 0.3f), 0f, Mathf.Sin(Time.time * 0.3f));
        Vector3 m = Steer(p, goal, fleeing, farOk, seeking && !fleeing ? 0.35f : 1f);

        float rt = Time.time;
        float dashCd = 3f - MetaProgressionManager.Instance.GetBonusDashCooldown();
        if (rt - lastDash >= dashCd + 0.05f && rt >= steerUntil)
        {
            if (seeking && !fleeing && close >= 2) { steerDir = toShard; steerUntil = rt + 0.2f; Press(GamepadButton.South); }
            else
            {
                foreach (var pr in projs)
                {
                    if (pr == null || !pr.isActiveAndEnabled) continue;
                    Vector3 q = pr.transform.position; q.y = 0f; Vector3 to = p - q; float d = to.magnitude;
                    if (d > 3.2f || d < 0.2f) continue;
                    Vector3 fw = pr.transform.forward; fw.y = 0f;
                    if (Vector3.Dot(fw.normalized, to / d) < 0.6f) continue;
                    if (WallMargin(p - to / d * 4f) < 0.5f) continue;
                    steerDir = -to / d; steerUntil = rt + 0.25f; Press(GamepadButton.South); break;
                }
                if (rt >= steerUntil && close >= (fleeing ? 3 : 5) && m.sqrMagnitude > 0.01f && WallMargin(p + m.normalized * 4f) > 0.5f) { steerDir = m.normalized; steerUntil = rt + 0.2f; Press(GamepadButton.South); }
            }
        }
        if (pc.IsDashing && !wasDashing) lastDash = Time.time; wasDashing = pc.IsDashing;
        if (rt < steerUntil) m = steerDir;
        GameInput.DebugMoveOverride = m.sqrMagnitude > 0.0001f ? new Vector2(m.x, m.z).normalized : Vector2.zero;
        if (!btnDown && rt >= nextUlt && (c6 >= 12 || main != null)) { Press(GamepadButton.North); nextUlt = rt + 1f; }
        if (!btnDown && MetaProgressionManager.Instance.HasPhantomDash() && close >= 5 && rt - lastClone >= 8.3f) { Press(GamepadButton.West); lastClone = rt; }
    }

    void Finish(string why)
    {
        float L = NightLen;
        if (nightStartT >= 0f)
        {
            float ne = nightEndT >= 0f ? nightEndT : gm.RunTimer;
            Log(Window("AVANT  ", shardSpawnT - L - 1f, shardSpawnT - 1f).Line());
            Log(Window("NUIT   ", nightStartT + 3f, ne - 3f).Line() + "  (hors fondus de 3 s)");
            Log(Window("APRES  ", ne + 2f, ne + 2f + L).Line());
        }
        else Log("PAS DE NUIT (eclatApparu=" + (shardSpawnT >= 0f) + ")");
        Log(string.Format(CultureInfo.InvariantCulture, "FIN {0} chrono={1:F0} niv={2} kills={3} boss={4} morts={5} reel={6:F0}s NOTES {7}",
            why, gm.RunTimer, XPSystem.Instance.CurrentLevel, gm.KillCount, gm.BossKillCount, refills, Time.realtimeSinceStartup - startReal, notes));
        Log("BUILD " + LevelUpManager.Instance.GetUpgradesSummary().Replace("\n", " | "));
        state = 2; endReal = Time.realtimeSinceStartup;
    }
    void OnDestroy() { GameInput.DebugMoveOverride = null; HealthSystem.DebugNeverDie = false; }
}

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        Application.runInBackground = true; SaveSystem.SuppressWrites = true;
        var go = new GameObject("EclipseBot2"); var bot = go.AddComponent<EclipseBot2>();
        bot.dir = @"C:\Users\jules\AppData\Local\Temp\claude\1d0ac4898dcdb431\f76ab443-7a9d-4afa-9ab2-2c99702e0706\scratchpad\eclipse\b2\";
        bot.plan = new[] { "classic_violent_lyra_ja", "esc_modere_kael_es", "classic_apocalyptique_aether_zh", "esc_violent_lyra_pl", "classic_modere_kael_ko", "esc_apocalyptique_aether_it" };
        bot.Init(); bot.Next();
        result.Log("scene=" + SceneManager.GetActiveScene().name);
    }
}
