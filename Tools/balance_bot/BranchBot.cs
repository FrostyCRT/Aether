using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

internal class BCfg { public string name; public int ch; public bool tree; }

internal class BranchBot : MonoBehaviour
{
    public string dir; public Gamepad pad; public int repeat = 3;
    List<BCfg> cfgs = new List<BCfg>();
    int idx = -1; BCfg c; int state = 0; GameManager gm, oldGm; GameObject player; HealthSystem hp; PlayerController pc; CrystalSystem cs;
    float nextScan, nextLog, nextUlt, startReal, endReal, bossStart = -1f, lastDash = -99f, lastClone = -99f, steerUntil;
    Vector3 steerDir; EnemyBase[] enemies = new EnemyBase[0]; BossBase[] bosses = new BossBase[0]; EnemyProjectile[] projs = new EnemyProjectile[0];
    bool btnDown; int lastBoss, bossSeen; string bossTimes;
    float dmg, heal, prevHp, maxHit; int refills, dashes, clones, ults, absorbs; bool wasDashing; int prevCharges; int prevStored;
    // sauvegarde des vrais niveaux
    int[] savedI; bool[] savedB;

    void Awake() { DontDestroyOnLoad(gameObject); }
    void Log(string s) { System.IO.File.AppendAllText(dir + "bb_" + c.name + ".txt", s + "\n"); }

    public void Init()
    {
        for (int r = 0; r < repeat; r++)
            for (int ch = 0; ch < 3; ch++)
            {
                cfgs.Add(new BCfg { name = "c" + ch + "_sans_" + r, ch = ch, tree = false });
                cfgs.Add(new BCfg { name = "c" + ch + "_arbre_" + r, ch = ch, tree = true });
            }
        var d = MetaProgressionManager.Instance.Data;
        savedI = new[] { d.cadenceLevel, d.crystalDamageLevel, d.concentrationLevel, d.vitalityLevel, d.armorLevel, d.recuperationLevel, d.dashLevel, d.novaRadiusLevel, d.selectedCharacterIndex };
        savedB = new[] { d.fragmentationUnlocked, d.overpowerUnlocked, d.secondWindUnlocked, d.manaShieldUnlocked, d.crystalMasteryUnlocked, d.phantomDashUnlocked, d.impulsionNovaUnlocked, d.kaelUnlocked, d.lyraUnlocked };
    }

    void Restore()
    {
        var d = MetaProgressionManager.Instance.Data;
        d.cadenceLevel = savedI[0]; d.crystalDamageLevel = savedI[1]; d.concentrationLevel = savedI[2]; d.vitalityLevel = savedI[3]; d.armorLevel = savedI[4]; d.recuperationLevel = savedI[5]; d.dashLevel = savedI[6]; d.novaRadiusLevel = savedI[7]; d.selectedCharacterIndex = savedI[8];
        d.fragmentationUnlocked = savedB[0]; d.overpowerUnlocked = savedB[1]; d.secondWindUnlocked = savedB[2]; d.manaShieldUnlocked = savedB[3]; d.crystalMasteryUnlocked = savedB[4]; d.phantomDashUnlocked = savedB[5]; d.impulsionNovaUnlocked = savedB[6]; d.kaelUnlocked = savedB[7]; d.lyraUnlocked = savedB[8];
    }

    public void Next()
    {
        idx++;
        if (idx >= cfgs.Count)
        {
            Restore();
            System.IO.File.AppendAllText(dir + "bb_FIN.txt", "fin\n");
            if (pad != null) InputSystem.RemoveDevice(pad);
            GameInput.DebugMoveOverride = null; Time.timeScale = 1f; Destroy(gameObject); return;
        }
        c = cfgs[idx];
        var d = MetaProgressionManager.Instance.Data;
        bool full = c.tree;
        d.kaelUnlocked = true; d.lyraUnlocked = true; d.selectedCharacterIndex = c.ch;
        bool g = full && c.ch == 0, k = full && c.ch == 1, l = full && c.ch == 2;
        d.concentrationLevel = g ? 3 : 0; d.cadenceLevel = g ? 3 : 0; d.crystalDamageLevel = g ? 3 : 0; d.fragmentationUnlocked = g; d.overpowerUnlocked = g;
        d.vitalityLevel = k ? 3 : 0; d.recuperationLevel = k ? 3 : 0; d.armorLevel = k ? 3 : 0; d.secondWindUnlocked = k; d.manaShieldUnlocked = k;
        d.dashLevel = l ? 3 : 0; d.novaRadiusLevel = l ? 3 : 0; d.impulsionNovaUnlocked = l; d.crystalMasteryUnlocked = l; d.phantomDashUnlocked = l;
        oldGm = GameManager.Instance; gm = null; state = 0; lastBoss = 0; bossSeen = 0; bossStart = -1f; bossTimes = "";
        dmg = heal = maxHit = 0f; refills = dashes = clones = ults = absorbs = 0; lastDash = lastClone = -99f; steerUntil = 0f;
        Time.timeScale = 1f;
        if (GameManager.Instance != null) GameManager.Instance.RestartGame(); else Object.FindAnyObjectByType<MainMenuManager>().PlayGame();
    }

    void Press(GamepadButton x) { InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(x)); btnDown = true; }

    string Stats()
    {
        float min = Mathf.Max(0.01f, gm.RunTimer / 60f);
        return string.Format(CultureInfo.InvariantCulture, "chrono={0:F0} niv={1} kills={2} boss={3} degats={4:F0} ({5:F0}/min) soins={6:F0} recharges={7} dashs={8} absorptions={9} clones={10} pvmax={11:F0} plusGrosCoup={12:F0}",
            gm.RunTimer, XPSystem.Instance.CurrentLevel, gm.KillCount, gm.BossKillCount, dmg, dmg / min, heal, refills, dashes, absorbs, clones, hp.MaxHealth, maxHit);
    }

    void Update()
    {
        Application.runInBackground = true;
        if (state == 0)
        {
            if (SceneManager.GetActiveScene().name != "Game" || GameManager.Instance == null || GameManager.Instance == oldGm) return;
            player = GameObject.FindWithTag("Player");
            if (player == null || XPSystem.Instance == null || LevelUpManager.Instance == null) return;
            gm = GameManager.Instance; hp = player.GetComponent<HealthSystem>(); pc = player.GetComponent<PlayerController>(); cs = player.GetComponent<CrystalSystem>();
            if (pad == null) pad = InputSystem.AddDevice<Gamepad>("BranchBotPad");
            state = 1; startReal = Time.realtimeSinceStartup; nextLog = 60f; prevHp = hp.CurrentHealth; prevCharges = cs.CurrentCharges;
            Log("DEBUT " + c.name + " perso=" + player.name + " branche=" + MetaProgressionManager.Instance.GetActiveBranch() + " pvmax=" + hp.MaxHealth + " cadence=" + MetaProgressionManager.CadenceMultiplier);
            return;
        }
        if (state == 2) { if (Time.realtimeSinceStartup - endReal > 3f) Next(); return; }
        if (btnDown) { InputSystem.QueueStateEvent(pad, new GamepadState()); btnDown = false; }

        // PV : dégâts subis, soins du jeu, recharges de secours (= morts évitées)
        float cur = hp.CurrentHealth;
        if (cur < prevHp) { dmg += prevHp - cur; if (prevHp - cur > maxHit) maxHit = prevHp - cur; } else if (cur > prevHp) heal += cur - prevHp;
        if (cur < hp.MaxHealth * 0.4f && !gm.IsGameOver) { refills++; hp.HealFlat(hp.MaxHealth); cur = hp.CurrentHealth; }
        prevHp = cur;
        // absorptions : chaque charge de cristal gagnée (ou ultime stocké)
        int chg = cs.CurrentCharges; if (chg > prevCharges || (chg == 0 && prevCharges == cs.MaxCharges - 1)) absorbs++; prevCharges = chg;
        if (pc.IsDashing && !wasDashing) { dashes++; lastDash = Time.time; }
        wasDashing = pc.IsDashing;

        if (gm.IsGameOver)
        {
            GameInput.DebugMoveOverride = Vector2.zero;
            if (Time.timeScale > 0f) return;
            Log("FIN " + Stats() + " dernierPV=" + prevHp.ToString("F0") + " bossTemps=" + bossTimes + string.Format(CultureInfo.InvariantCulture, " reel={0:F0}s", Time.realtimeSinceStartup - startReal));
            state = 2; endReal = Time.realtimeSinceStartup; return;
        }
        if (gm.RunTimer >= 900f || Time.realtimeSinceStartup - startReal > 600f)
        {
            Log("FIN15 " + Stats() + " bossTemps=" + bossTimes);
            gm.AbandonRun(); state = 2; endReal = Time.realtimeSinceStartup; return;
        }
        var lu = LevelUpManager.Instance;
        if (lu.IsWaitingForChoice) { lu.SelectUpgrade(0); return; }
        if (lu.IsChoosing || gm.IsPaused) return;
        if (Time.timeScale > 0f) Time.timeScale = 3f;

        if (Time.time >= nextScan)
        {
            nextScan = Time.time + 0.1f;
            enemies = Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
            bosses = Object.FindObjectsByType<BossBase>(FindObjectsSortMode.None);
            projs = Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None);
        }
        BossBase main = null;
        foreach (var x in bosses) if (x != null && !x.IsSummoned && x.CurrentHealth > 0f) main = x;
        if (main != null && bossStart < 0f) { bossStart = Time.time; bossSeen++; }
        if (gm.BossKillCount > lastBoss) { lastBoss = gm.BossKillCount; bossTimes += string.Format(CultureInfo.InvariantCulture, "{0:F0}s,", Time.time - bossStart); bossStart = -1f; }

        Vector3 p = player.transform.position; p.y = 0f; Vector3 rep = Vector3.zero; float near = 999f; Vector3 np = p; int close = 0;
        foreach (var en in enemies) { if (en == null || !en.isActiveAndEnabled) continue; Vector3 q = en.transform.position; q.y = 0f; float d = (p - q).magnitude; if (d < near) { near = d; np = q; } if (d < 3.5f) close++; if (d < 5f && d > 0.01f) rep += (p - q) / d * (5f - d) / 5f; }
        foreach (var x in bosses) { if (x == null) continue; Vector3 q = x.transform.position; q.y = 0f; float d = (p - q).magnitude; if (d < near) { near = d; np = q; } if (d < 12f && d > 0.01f) rep += (p - q) / d * (12f - d) / 12f * 4f; }
        Vector3 goal = Vector3.zero;
        var chest = Object.FindAnyObjectByType<BossChest>();
        if (chest != null) { Vector3 q = chest.transform.position; q.y = 0f; goal = (q - p).normalized * 3f; }
        else if (near < 900f && near > 3.5f) goal = (np - p).normalized;
        else if (near >= 900f) goal = new Vector3(Mathf.Cos(Time.time * 0.3f), 0f, Mathf.Sin(Time.time * 0.3f));
        if (p.magnitude > 34f) goal += -p.normalized * 2f;
        Vector3 m = rep + goal;

        float rt = Time.time;
        float dashCd = 3f - MetaProgressionManager.Instance.GetBonusDashCooldown();
        bool dashReady = rt - lastDash >= dashCd + 0.05f;
        // Absorption : dash vers un projectile ennemi qui arrive (comme un joueur qui joue le cristal)
        if (dashReady && rt >= steerUntil)
        {
            foreach (var pr in projs)
            {
                if (pr == null || !pr.isActiveAndEnabled) continue;
                Vector3 q = pr.transform.position; q.y = 0f; Vector3 to = p - q; float d = to.magnitude;
                if (d > 3.2f || d < 0.2f) continue;
                Vector3 fw = pr.transform.forward; fw.y = 0f;
                if (Vector3.Dot(fw.normalized, to / d) < 0.6f) continue;
                steerDir = -to / d; steerUntil = rt + 0.25f; Press(GamepadButton.South); break;
            }
            // Dash de secours si encerclé
            if (rt >= steerUntil && close >= 5 && m.sqrMagnitude > 0.01f) { steerDir = m.normalized; steerUntil = rt + 0.2f; Press(GamepadButton.South); }
        }
        if (rt < steerUntil) m = steerDir;
        GameInput.DebugMoveOverride = m.sqrMagnitude > 0.0001f ? new Vector2(m.x, m.z).normalized : Vector2.zero;

        if (!btnDown && rt >= nextUlt && (enemies.Length >= 15 || main != null))
        {
            Press(GamepadButton.North); nextUlt = rt + 1f;
        }
        if (!btnDown && MetaProgressionManager.Instance.HasPhantomDash() && close >= 5 && rt - lastClone >= 8.3f)
        { Press(GamepadButton.West); lastClone = rt; clones++; }
        if (gm.RunTimer >= nextLog) { nextLog += 60f; Log("T " + Stats()); }
    }
    void OnDestroy() { GameInput.DebugMoveOverride = null; }
}

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        Application.runInBackground = true; SaveSystem.SuppressWrites = true;
        var go = new GameObject("BranchBot"); var bot = go.AddComponent<BranchBot>();
        bot.dir = @"C:\Users\jules\AppData\Local\Temp\claude\1d0ac4898dcdb431\f76ab443-7a9d-4afa-9ab2-2c99702e0706\scratchpad\";
        bot.Init(); bot.Next();
        result.Log("scene=" + SceneManager.GetActiveScene().name);
    }
}
