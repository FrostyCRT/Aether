// Bot « vrai joueur » (2026-11-15) : 9 parties Classique de 15 min, 3 personnages x 3 paliers, progression d'arbre et de
// Réputation par palier (voir PURCHASES / REP). Envoyer TOUT ce fichier à Unity_RunCommand pendant un Play lancé depuis
// MainMenu ; résultats dans dir (rb_<config>.txt), rb_FIN.txt à la fin. SaveSystem.SuppressWrites = true ; les vrais
// niveaux d'arbre / Réputation / perso / palier / mode sont remis à la fin.
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

internal class RCfg { public string name; public int ch; public int tier; public int buys; public int rep; public float bossKeep = 12f; }

internal class RealBot : MonoBehaviour
{
    public string dir; public Gamepad pad; public float speed = 2f; public int startAt = 0;
    List<RCfg> cfgs = new List<RCfg>();
    int idx = -1; RCfg c; int state = 0; GameManager gm, oldGm; GameObject player; HealthSystem hp; PlayerController pc; CrystalSystem cs;
    float nextScan, nextLog, nextUlt, startReal, endReal, bossStart = -1f, lastDash = -99f, lastClone = -99f, steerUntil, firstNearDeath = -1f, t15 = -1f;
    Vector3 steerDir; EnemyBase[] enemies = new EnemyBase[0]; BossBase[] bosses = new BossBase[0]; EnemyProjectile[] projs = new EnemyProjectile[0];
    bool btnDown; int lastBoss; string bossTimes, nearDeaths, picks;
    float dmg, heal, prevHp, maxHit; int refills, bossRefills, dashes, clones, ults, absorbs; bool wasDashing, below30; int prevCharges;
    int samples; float sumClose6, sumAlive, sumGemTime; float minuteDmg; string perMinute;
    int[] savedI; bool[] savedB; int savedTier, savedMode;
    // Plan optionnel (2026-11-16) : liste "palier_perso" ou "palier_perso@distanceBoss" ; sinon les 9 parties.
    public string[] plan;
    // Suivi du boss en cours : distance moyenne au joueur, part du temps à moins de 8 m, PV max / restants.
    float bossDistSum, bossNear8; int bossSamples; float bossMaxHp, bossCurHp; string bossInfo;

    static readonly string[][] PURCHASES = {
        new[] { "concentration", "cadence", "concentration", "cadence", "crystalDamage", "concentration", "cadence", "crystalDamage", "fragmentation", "crystalDamage", "overpower" },
        new[] { "vitality", "recuperation", "vitality", "recuperation", "armor", "vitality", "recuperation", "armor", "secondWind", "armor", "manaShield" },
        new[] { "dash", "dash", "novaRadius", "dash", "impulsionNova", "novaRadius", "crystalMastery", "novaRadius", "phantomDash" } };
    static readonly string[] REP = { "dmg", "spd", "dmg", "spd", "reg", "dmg", "spd", "reg", "reg", "dmg", "spd", "reg" };
    static readonly string[] CHAR = { "aether", "kael", "lyra" };
    static readonly string[] TIER = { "modere", "violent", "apocalyptique" };

    void Awake() { DontDestroyOnLoad(gameObject); }
    void Log(string s) { System.IO.File.AppendAllText(dir + "rb_" + c.name + ".txt", s + "\n"); }

    public void Init()
    {
        int[] buys = { 3, 7, 10 }, rep = { 2, 5, 9 };
        idx = startAt - 1;
        if (plan != null)
        {
            var seen = new Dictionary<string, int>();
            foreach (var e in plan)
            {
                string[] kv = e.Split('@'); string[] tc = kv[0].Split('_');
                int t = System.Array.IndexOf(TIER, tc[0]), ch = System.Array.IndexOf(CHAR, tc[1]);
                float keep = kv.Length > 1 ? float.Parse(kv[1], CultureInfo.InvariantCulture) : (ch == 1 ? 4f : 7f);
                string baseName = kv[0] + (kv.Length > 1 ? "_boss" + kv[1] + "m" : "");
                int n; seen.TryGetValue(baseName, out n); n++; seen[baseName] = n;
                cfgs.Add(new RCfg { name = baseName + "_r" + n, ch = ch, tier = t, buys = buys[t], rep = rep[t], bossKeep = keep });
            }
        }
        else
        for (int t = 0; t < 3; t++)
            for (int ch = 0; ch < 3; ch++)
                cfgs.Add(new RCfg { name = TIER[t] + "_" + CHAR[ch], ch = ch, tier = t, buys = buys[t], rep = rep[t], bossKeep = ch == 1 ? 4f : 7f });
        var d =MetaProgressionManager.Instance.Data;
        savedI = new[] { d.cadenceLevel, d.crystalDamageLevel, d.concentrationLevel, d.vitalityLevel, d.armorLevel, d.recuperationLevel, d.dashLevel, d.novaRadiusLevel, d.selectedCharacterIndex, d.reputationDamageLevel, d.reputationSpeedLevel, d.reputationRegenLevel };
        savedB = new[] { d.fragmentationUnlocked, d.overpowerUnlocked, d.secondWindUnlocked, d.manaShieldUnlocked, d.crystalMasteryUnlocked, d.phantomDashUnlocked, d.impulsionNovaUnlocked, d.kaelUnlocked, d.lyraUnlocked };
        savedTier = (int)DifficultyTiers.Current; savedMode = (int)GameModes.Current;
    }

    void Restore()
    {
        var d = MetaProgressionManager.Instance.Data;
        d.cadenceLevel = savedI[0]; d.crystalDamageLevel = savedI[1]; d.concentrationLevel = savedI[2]; d.vitalityLevel = savedI[3]; d.armorLevel = savedI[4]; d.recuperationLevel = savedI[5]; d.dashLevel = savedI[6]; d.novaRadiusLevel = savedI[7]; d.selectedCharacterIndex = savedI[8];
        d.reputationDamageLevel = savedI[9]; d.reputationSpeedLevel = savedI[10]; d.reputationRegenLevel = savedI[11];
        d.fragmentationUnlocked = savedB[0]; d.overpowerUnlocked = savedB[1]; d.secondWindUnlocked = savedB[2]; d.manaShieldUnlocked = savedB[3]; d.crystalMasteryUnlocked = savedB[4]; d.phantomDashUnlocked = savedB[5]; d.impulsionNovaUnlocked = savedB[6]; d.kaelUnlocked = savedB[7]; d.lyraUnlocked = savedB[8];
        DifficultyTiers.Current = (DifficultyTier)savedTier; GameModes.Current = (GameMode)savedMode;
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
        for (int i = 0; i < c.rep; i++)
        {
            if (REP[i] == "dmg") d.reputationDamageLevel++; else if (REP[i] == "spd") d.reputationSpeedLevel++; else d.reputationRegenLevel++;
        }
    }

    string TreeLabel(SaveData d)
    {
        return string.Format("conc{0} cad{1} crist{2} frag{3} surp{4} | vit{5} recup{6} arm{7} souffle{8} bouclier{9} | dash{10} nova{11} impuls{12} maitrise{13} clone{14} | rep D{15} V{16} R{17}",
            d.concentrationLevel, d.cadenceLevel, d.crystalDamageLevel, d.fragmentationUnlocked ? 1 : 0, d.overpowerUnlocked ? 1 : 0,
            d.vitalityLevel, d.recuperationLevel, d.armorLevel, d.secondWindUnlocked ? 1 : 0, d.manaShieldUnlocked ? 1 : 0,
            d.dashLevel, d.novaRadiusLevel, d.impulsionNovaUnlocked ? 1 : 0, d.crystalMasteryUnlocked ? 1 : 0, d.phantomDashUnlocked ? 1 : 0,
            d.reputationDamageLevel, d.reputationSpeedLevel, d.reputationRegenLevel);
    }

    public void Next()
    {
        idx++;
        if (idx >= cfgs.Count)
        {
            Restore(); HealthSystem.DebugNeverDie = false;
            System.IO.File.AppendAllText(dir + "rb_FIN.txt", "fin\n");
            if (pad != null) InputSystem.RemoveDevice(pad);
            GameInput.DebugMoveOverride = null; Time.timeScale = 1f; Destroy(gameObject); return;
        }
        c = cfgs[idx];
        var d = MetaProgressionManager.Instance.Data;
        d.kaelUnlocked = true; d.lyraUnlocked = true; d.selectedCharacterIndex = c.ch;
        DifficultyTiers.Current = (DifficultyTier)c.tier; GameModes.Current = GameMode.Classic;
        ApplyProgression(d);
        oldGm = GameManager.Instance; gm = null; state = 0; lastBoss = 0; bossStart = -1f; bossTimes = ""; bossInfo = ""; bossDistSum = bossNear8 = bossMaxHp = bossCurHp = 0f; bossSamples = 0; nearDeaths = ""; picks = ""; perMinute = "";
        fleeing = false; wallSamples = fleeSamples = 0; lastDir = Vector3.zero;
        dmg = heal = maxHit = minuteDmg = 0f; samples = 0; sumClose6 = sumAlive = sumGemTime = 0f; firstNearDeath = -1f; t15 = -1f;
        refills = bossRefills = dashes = clones = ults = absorbs = 0; below30 = false; HealthSystem.DebugPreventedDeaths = 0; HealthSystem.DebugNeverDie = true; lastDash = lastClone = -99f; steerUntil = 0f;
        Time.timeScale = 1f;
        if (GameManager.Instance != null) GameManager.Instance.RestartGame(); else Object.FindAnyObjectByType<MainMenuManager>().PlayGame();
    }

    // boss N : durée du combat, PV max, PV restants (%), distance moyenne au joueur, part du temps à moins de 8 m.
    string BossLine(int n, float dur, bool alive)
    {
        return string.Format(CultureInfo.InvariantCulture, "boss{0}[{1:F0}s pvmax={2:F0} restant={3:P0} dps={4:F0} dist={5:F1}m proche8m={6:P0}] ",
            n, dur, bossMaxHp, bossMaxHp > 0f ? (alive ? bossCurHp : 0f) / bossMaxHp : 0f,
            dur > 0f ? (bossMaxHp - (alive ? bossCurHp : 0f)) / dur : 0f,
            bossSamples > 0 ? bossDistSum / bossSamples : 0f, bossSamples > 0 ? bossNear8 / bossSamples : 0f);
    }

    // ---- Pilotage (2026-11-16) : 16 directions notées (danger de la horde / du boss, murs, objectif, inertie) ----
    Vector3[] ePos = new Vector3[600]; float[] eRad = new float[600], eWt = new float[600]; int eN;
    bool fleeing; int wallSamples, fleeSamples; Vector3 lastDir; float nextDbg;
    static readonly float[] LS_N = { 2.5f, 5f }, LW_N = { 1f, 0.6f }, LS_F = { 2.5f, 5f, 9f }, LW_F = { 1f, 0.8f, 0.8f };

    // Distance au mur le plus proche (999 = pas de mur ; négatif = hors carte). Violent : tunnel Z +-12, Apocalyptique : boîte +-22.
    static float WallMargin(Vector3 q)
    {
        float m = 999f;
        if (!float.IsInfinity(MapBoundaryUtils.MinX)) m = Mathf.Min(m, q.x - MapBoundaryUtils.MinX);
        if (!float.IsInfinity(MapBoundaryUtils.MaxX)) m = Mathf.Min(m, MapBoundaryUtils.MaxX - q.x);
        if (!float.IsInfinity(MapBoundaryUtils.MinZ)) m = Mathf.Min(m, q.z - MapBoundaryUtils.MinZ);
        if (!float.IsInfinity(MapBoundaryUtils.MaxZ)) m = Mathf.Min(m, MapBoundaryUtils.MaxZ - q.z);
        return m;
    }

    Vector3 Steer(Vector3 p, Vector3 goal, bool flee)
    {
        float[] Ls = flee ? LS_F : LS_N, Lw = flee ? LW_F : LW_N;
        Vector3 g = Vector3.ClampMagnitude(goal, 1.5f);
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
                if (mg < 0f) wall += 4f * Lw[k]; else if (mg < 6f) wall += (6f - mg) / 6f * 2.5f * Lw[k];
                if (q.magnitude > 34f && q.magnitude > p.magnitude) wall += 1f;
            }
            float score = Vector3.Dot(d, g) - danger - wall + Vector3.Dot(d, lastDir) * 0.25f;
            if (score > best) { best = score; bestDir = d; bestDanger = danger; }
        }
        lastDir = bestDir;
        if (g.sqrMagnitude < 0.0001f && bestDanger < 0.05f) return Vector3.zero;
        return bestDir;
    }

    void Press(GamepadButton x) { InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(x)); btnDown = true; }

    string Stats()
    {
        float min = Mathf.Max(0.01f, gm.RunTimer / 60f);
        return string.Format(CultureInfo.InvariantCulture, "chrono={0:F0} niv={1} kills={2} boss={3} degatsSubis={4:F0} ({5:F0}/min) soins={6:F0} morts={7} dashs={8} absorptions={9} passagesSous30={10} clones={11} pvmax={12:F0} plusGrosCoup={13:F0} proches6m={14:F1} vivants={15:F0} tempsGemmes={16:P0} pres2mMur={17:P0} fuite={18:P0}",
            gm.RunTimer, XPSystem.Instance.CurrentLevel, gm.KillCount, gm.BossKillCount, dmg, dmg / min, heal, refills, dashes, absorbs, bossRefills, clones, hp.MaxHealth, maxHit,
            samples > 0 ? sumClose6 / samples : 0f, samples > 0 ? sumAlive / samples : 0f, samples > 0 ? sumGemTime / samples : 0f,
            samples > 0 ? (float)wallSamples / samples : 0f, samples > 0 ? (float)fleeSamples / samples : 0f);
    }

    // Choix de carte « vrai joueur » : fusion > arme exclusive > soin si blessé > amélioration d'une arme déjà prise >
    // nouvelle arme tant qu'on en a moins de 3 > dégâts / cadence / double tir.
    float Score(UpgradeData u)
    {
        var lu = LevelUpManager.Instance;
        int lvl = lu.GetLevel(u);
        int owned = 0;
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

    void Update()
    {
        Application.runInBackground = true;
        if (state == 0)
        {
            if (SceneManager.GetActiveScene().name != "Game" || GameManager.Instance == null || GameManager.Instance == oldGm) return;
            player = GameObject.FindWithTag("Player");
            if (player == null || XPSystem.Instance == null || LevelUpManager.Instance == null) return;
            gm = GameManager.Instance; hp = player.GetComponent<HealthSystem>(); pc = player.GetComponent<PlayerController>(); cs = player.GetComponent<CrystalSystem>();
            if (pad == null) pad = InputSystem.AddDevice<Gamepad>("RealBotPad");
            state = 1; startReal = Time.realtimeSinceStartup; nextLog = 60f; prevHp = hp.CurrentHealth; prevCharges = cs.CurrentCharges;
            Log("DEBUT " + c.name + " perso=" + player.name + " palier=" + DifficultyTiers.Current + " mode=" + GameModes.Current + " pvmax=" + hp.MaxHealth + " achatsArbre=" + c.buys + " upReputation=" + c.rep);
            Log("ARBRE " + TreeLabel(MetaProgressionManager.Instance.Data));
            return;
        }
        if (state == 2) { if (Time.realtimeSinceStartup - endReal > 3f) Next(); return; }
        if (btnDown) { InputSystem.QueueStateEvent(pad, new GamepadState()); btnDown = false; }

        // PV : le jeu ne tue jamais le joueur (HealthSystem.DebugNeverDie) : un coup mortel le laisse à 1 PV = 1 MORT comptée.
        // Il repart alors avec ses PV au max (sinon le coup suivant compterait une 2e mort dans la même seconde).
        float cur = hp.CurrentHealth;
        if (cur < prevHp) { dmg += prevHp - cur; minuteDmg += prevHp - cur; if (prevHp - cur > maxHit) maxHit = prevHp - cur; } else if (cur > prevHp) heal += cur - prevHp;
        if (HealthSystem.DebugPreventedDeaths > refills)
        {
            refills = HealthSystem.DebugPreventedDeaths; if (firstNearDeath < 0f) firstNearDeath = gm.RunTimer;
            nearDeaths += string.Format(CultureInfo.InvariantCulture, "{0:F0},", gm.RunTimer);
            hp.HealFlat(hp.MaxHealth); cur = hp.CurrentHealth;
        }
        // Information : passages sous 30 % des PV (sans effet sur la partie)
        if (cur < hp.MaxHealth * 0.3f) { if (!below30) { below30 = true; bossRefills++; } } else if (cur > hp.MaxHealth * 0.5f) below30 = false;
        prevHp = cur;
        int chg = cs.CurrentCharges; if (chg > prevCharges || (chg == 0 && prevCharges == cs.MaxCharges - 1)) absorbs++; prevCharges = chg;
        if (pc.IsDashing && !wasDashing) { dashes++; lastDash = Time.time; }
        wasDashing = pc.IsDashing;

        if (gm.IsGameOver)
        {
            GameInput.DebugMoveOverride = Vector2.zero;
            if (Time.timeScale > 0f) return;
            Log("FIN " + (gm.BossKillCount >= 3 ? "VICTOIRE " : "MORT ") + Stats() + " bossTemps=" + bossTimes + " mortsA=" + nearDeaths + string.Format(CultureInfo.InvariantCulture, " reel={0:F0}s", Time.realtimeSinceStartup - startReal));
            Log("BOSSINFO " + bossInfo);
            Log("PARMINUTE " + perMinute);
            Log("CARTES " + picks);
            Log("BUILD " + LevelUpManager.Instance.GetUpgradesSummary().Replace("\n", " | "));
            state = 2; endReal = Time.realtimeSinceStartup; return;
        }
        // Classique : Boss 3 à 15:00 (le chrono s'arrête pendant les combats de boss) -> on laisse le combat se jouer ;
        // abandon seulement si le Boss 3 tient plus de 240 s de jeu, ou au bout de 1 500 s réelles.
        if (gm.RunTimer >= 899.5f && t15 < 0f) t15 = Time.time;
        if ((t15 >= 0f && Time.time - t15 > 240f) || Time.realtimeSinceStartup - startReal > 1500f)
        {
            Log("FIN15 BOSS3_NON_TUE " + Stats() + " bossTemps=" + bossTimes + " mortsA=" + nearDeaths);
            Log("BOSSINFO " + bossInfo + BossLine(3, Time.time - bossStart, true));
            Log("PARMINUTE " + perMinute);
            Log("CARTES " + picks);
            Log("BUILD " + LevelUpManager.Instance.GetUpgradesSummary().Replace("\n", " | "));
            gm.AbandonRun(); state = 2; endReal = Time.realtimeSinceStartup; return;
        }
        var lu = LevelUpManager.Instance;
        if (lu.IsWaitingForChoice)
        {
            var ch = lu.CurrentChoices; int best = 0; float bs = -1f;
            for (int i = 0; i < ch.Count; i++) { float s = ch[i] != null ? Score(ch[i]) + Random.value : -1f; if (s > bs) { bs = s; best = i; } }
            if (ch.Count > 0 && ch[best] != null) picks += ch[best].upgradeType + (ch[best].upgradeType == UpgradeType.Fusion ? "(" + ch[best].FusionSource1 + "+" + ch[best].FusionSource2 + ")" : "") + string.Format(CultureInfo.InvariantCulture, "@{0:F0},", gm.RunTimer);
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
        if (main != null && bossStart < 0f) { bossStart = Time.time; bossDistSum = bossNear8 = 0f; bossSamples = 0; }
        if (main != null)
        {
            bossMaxHp = main.MaxHealth; bossCurHp = main.CurrentHealth;
            Vector3 bq = main.transform.position - player.transform.position; bq.y = 0f; float bd = bq.magnitude;
            bossDistSum += bd; if (bd < 8f) bossNear8 += 1f; bossSamples++;
        }
        if (gm.BossKillCount > lastBoss)
        {
            lastBoss = gm.BossKillCount; bossCurHp = 0f;
            bossInfo += BossLine(lastBoss, Time.time - bossStart, false);
            bossTimes += string.Format(CultureInfo.InvariantCulture, "{0:F0}s,", Time.time - bossStart); bossStart = -1f;
        }

        // Déplacement : s'écarte des ennemis à moins de 5 m (12 m pour un boss), revient au contact de la horde à la
        // distance de son arme (Kael 3 m, aura ; Aether / Lyra 7 m, armes à distance), ramasse les gemmes proches quand
        // rien ne menace, va chercher le coffre de boss.
        float engage = c.ch == 1 ? 3f : 7f;
        float R = c.ch == 1 ? 3.5f : 5f;
        Vector3 p = player.transform.position; p.y = 0f; float near = 999f; Vector3 np = p; int close = 0, c6 = 0, alive = 0; eN = 0;
        foreach (var en in enemies) { if (en == null || !en.isActiveAndEnabled) continue; alive++; Vector3 q = en.transform.position; q.y = 0f; float d = (p - q).magnitude; if (d < near) { near = d; np = q; } if (d < 3.5f) close++; if (d < 6f) c6++; if (d < 14f && eN < ePos.Length) { ePos[eN] = q; eRad[eN] = R; eWt[eN] = 1f; eN++; } }
        float bk = c.bossKeep;
        foreach (var x in bosses) { if (x == null) continue; Vector3 q = x.transform.position; q.y = 0f; float d = (p - q).magnitude; if (d < near) { near = d; np = q; } if (d < bk + 8f && eN < ePos.Length) { ePos[eN] = q; eRad[eN] = bk; eWt[eN] = 4f; eN++; } }
        // Fuite : sous 40 % de PV (ou presque encerclé), jusqu'à 70 % ; on oublie alors gemmes / coffre / contact.
        float hpFrac = hp.CurrentHealth / Mathf.Max(1f, hp.MaxHealth);
        // Kael (aura 6,5 m) doit rester dans la horde : pour lui, fuite seulement sur les PV ; les autres fuient aussi encerclés.
        if (hpFrac < 0.4f || (close >= 7 && c.ch != 1)) fleeing = true; else if (hpFrac > 0.7f && close < 4) fleeing = false;
        Vector3 goal = Vector3.zero; bool gem = false;
        var chest = Object.FindAnyObjectByType<BossChest>();
        Vector3 gp;
        if (fleeing) { }
        else if (chest != null) { Vector3 q = chest.transform.position; q.y = 0f; goal = (q - p).normalized * 3f; }
        else if (near > 4.5f && XPGemField.Instance != null && XPGemField.Instance.TryGetNearest(p, 9f, out gp)) { gp.y = 0f; goal = (gp - p).normalized * 1.3f; gem = true; }
        else if (near < 900f && near > engage) goal = (np - p).normalized;
        else if (near < 900f)
        {
            // À bonne distance : ne reste pas planté, tourne autour de la cible la plus proche (boss compris), comme un joueur qui esquive.
            Vector3 radial = (np - p); radial.y = 0f; radial = radial.normalized;
            float side = Mathf.Sin(Time.time * 0.25f) > 0f ? 1f : -1f;
            goal = new Vector3(-radial.z, 0f, radial.x) * side * 0.7f;
        }
        else if (near >= 900f) goal = new Vector3(Mathf.Cos(Time.time * 0.3f), 0f, Mathf.Sin(Time.time * 0.3f));
        Vector3 m = Steer(p, goal, fleeing);
        samples++; sumClose6 += c6; sumAlive += alive; if (gem) sumGemTime += 1f;
        if (WallMargin(p) < 2f) wallSamples++; if (fleeing) fleeSamples++;

        float rt = Time.time;
        float dashCd = 3f - MetaProgressionManager.Instance.GetBonusDashCooldown();
        bool dashReady = rt - lastDash >= dashCd + 0.05f;
        if (dashReady && rt >= steerUntil)
        {
            // Absorption : dash vers un projectile ennemi qui arrive (comme un joueur qui joue le cristal)
            foreach (var pr in projs)
            {
                if (pr == null || !pr.isActiveAndEnabled) continue;
                Vector3 q = pr.transform.position; q.y = 0f; Vector3 to = p - q; float d = to.magnitude;
                if (d > 3.2f || d < 0.2f) continue;
                Vector3 fw = pr.transform.forward; fw.y = 0f;
                if (Vector3.Dot(fw.normalized, to / d) < 0.6f) continue;
                if (WallMargin(p - to / d * 4f) < 0.5f) continue;   // pas d'absorption qui jette contre un mur
                steerDir = -to / d; steerUntil = rt + 0.25f; Press(GamepadButton.South); break;
            }
            // Dash pour sortir d'un encerclement (ou à travers la horde pour Lyra, dont le dash blesse) ; en fuite dès 3 ennemis
            if (rt >= steerUntil && close >= (fleeing ? 3 : 5) && m.sqrMagnitude > 0.01f && WallMargin(p + m.normalized * 4f) > 0.5f) { steerDir = m.normalized; steerUntil = rt + 0.2f; Press(GamepadButton.South); }
        }
        if (rt < steerUntil) m = steerDir;
        // Diagnostic (2026-11-16) : une ligne DBG toutes les 15 s réelles à partir de 10:00 (position, PV, dash bloqué ?).
        if (gm.RunTimer >= 600f && Time.unscaledTime >= nextDbg)
        {
            nextDbg = Time.unscaledTime + 15f;
            Log(string.Format(CultureInfo.InvariantCulture, "DBG t={0:F0} pos=({1:F1},{2:F1}) hp={3:F0}/{4:F0} isDashing={5} dashReady={6} sinceDash={7:F1}s close={8} c6={9} flee={10} m=({11:F2},{12:F2}) steerLeft={13:F2} mur={14:F1} timeScale={15:F2} boss={16} eN={17}",
                gm.RunTimer, p.x, p.z, hp.CurrentHealth, hp.MaxHealth, pc.IsDashing, dashReady, rt - lastDash, close, c6, fleeing, m.x, m.z, steerUntil - rt, WallMargin(p), Time.timeScale, main != null, eN));
        }
        GameInput.DebugMoveOverride = m.sqrMagnitude > 0.0001f ? new Vector2(m.x, m.z).normalized : Vector2.zero;

        // Ultime : sur un boss, ou quand 12+ ennemis sont à moins de 6 m
        if (!btnDown && rt >= nextUlt && (c6 >= 12 || main != null))
        {
            Press(GamepadButton.North); nextUlt = rt + 1f;
        }
        if (!btnDown && MetaProgressionManager.Instance.HasPhantomDash() && close >= 5 && rt - lastClone >= 8.3f)
        { Press(GamepadButton.West); lastClone = rt; clones++; }
        if (gm.RunTimer >= nextLog)
        {
            perMinute += string.Format(CultureInfo.InvariantCulture, "{0:F0}m:niv{1}/k{2}/d{3:F0}/morts{4}/sous30_{5} ", gm.RunTimer / 60f, XPSystem.Instance.CurrentLevel, gm.KillCount, minuteDmg, refills, bossRefills);
            minuteDmg = 0f; nextLog += 60f; Log("T " + Stats());
        }
    }
    void OnDestroy() { GameInput.DebugMoveOverride = null; HealthSystem.DebugNeverDie = false; }
}

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        Application.runInBackground = true; SaveSystem.SuppressWrites = true;
        var go = new GameObject("RealBot"); var bot = go.AddComponent<RealBot>();
        bot.dir = @"C:\Users\jules\AppData\Local\Temp\claude\a0f3fc273feb60cb\c6844d23-6213-4e17-b36d-582dfe1c3239\scratchpad\rb3\";
        // Bot corrigé (16 directions, murs, fuite) : les 3 persos en Violent. Distance de fuite du boss par défaut :
        // 7 m (Kael 4 m, son aura fait 6,5 m).
        bot.plan = new[] { "violent_lyra" };
        System.IO.Directory.CreateDirectory(bot.dir);
        bot.Init(); bot.Next();
        result.Log("scene=" + SceneManager.GetActiveScene().name);
    }
}
