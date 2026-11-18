using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// OUTIL DE TEST (2026-11-18, « revérifie toutes les langues et tous les endroits de texte ») - en Play, passe chaque langue,
// affiche les pages / fenêtres et signale les textes qui dépassent : de leur propre cadre, du bouton / de la carte qui les porte,
// ou qui chevauchent un autre texte. Écrit un rapport texte + une capture par page fautive. Piloté par RunCommand :
//   TextFitAudit.Begin(scenario, langs)  -> démarre ;  TextFitAudit.Status -> avancement ;  rapport dans OutDir.
// Ne modifie rien au jeu (sauf langue / mode / palier temporaires, remis à la fin).
public static class TextFitAudit
{
    public static string OutDir = "";
    public static string Status = "idle";
    public static bool Running;
    public static int IssueCount;
    private static StringBuilder _report = new StringBuilder();
    private static IEnumerator _routine;
    private static double _wakeAt;
    public static string Scenario = "menu";
    public static int[] Langs = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
    private static string _ctx = "";
    private static int _shots;
    public static bool Shots = true;

    public static void Begin(string scenario, int[] langs)
    {
        if (Running) return;
        Scenario = scenario;
        if (langs != null) Langs = langs;
        _report = new StringBuilder();
        IssueCount = 0; _shots = 0;
        Running = true;
        Status = "start";
        _routine = scenario == "game" ? GameRoutine() : MenuRoutine();
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!Running) { EditorApplication.update -= Tick; return; }
        if (!EditorApplication.isPlaying) { Finish("arrêt de Play"); return; }
        if (EditorApplication.timeSinceStartup < _wakeAt) return;
        try
        {
            if (!_routine.MoveNext()) { Finish("terminé"); return; }
            object y = _routine.Current;
            _wakeAt = EditorApplication.timeSinceStartup + (y is float f ? f : 0.0);
        }
        catch (System.Exception e) { _report.AppendLine("EXCEPTION " + e); Finish("exception " + e.Message); }
    }

    private static void Finish(string why)
    {
        Running = false;
        Status = "done: " + why + " issues=" + IssueCount;
        try { File.WriteAllText(Path.Combine(OutDir, "textfit_" + Scenario + ".txt"), _report.ToString()); } catch { }
    }

    private static readonly string[] LangCodes = { "fr", "en", "de", "es", "pt", "it", "pl", "ru", "zh", "ja", "ko" };

    private static void SetLang(int l)
    {
        GameSettings.SetInt(GameSettings.Language, l);
    }

    // =========================================================================================================================
    //  SCÉNARIOS
    // =========================================================================================================================
    private static IEnumerator MenuRoutine()
    {
        MainMenuManager menu = Object.FindFirstObjectByType<MainMenuManager>();
        if (menu == null) { Status = "pas de MainMenuManager"; yield break; }
        var tutorial = Object.FindFirstObjectByType<MenuTutorial>();
        GameMode prevMode = GameModes.Current; DifficultyTier prevTier = DifficultyTiers.Current;
        SaveData data = MetaProgressionManager.Instance.Data;
        LastRunSummary prevRun = data.lastRun;
        int prevPages = data.tutorialPages, prevHints = data.tutorialHints;
        data.tutorialPages = -1; data.tutorialHints = -1;   // pas de visite de page pendant l'audit
        data.lastRun = new LastRunSummary
        {
            valid = true, gold = 12345678, goldKills = 4111111, goldTime = 2222222, goldBosses = 3333333, goldVictory = 1666666, goldChallenge = 1012346,
            eclats = 99999, eclatsTime = 11111, eclatsBosses = 22222, eclatsVictory = 33333, eclatsFirstWin = 9999, eclatsDiscoveries = 12345, eclatsNight = 11111,
            runTime = 900f, kills = 123456, level = 99, bossKills = 3, victory = true, mode = (int)GameMode.Classic, tier = 2, character = 1,
            unixTime = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 5,
        };
        foreach (int l in Langs)
        {
            Status = "lang " + LangCodes[l];
            SetLang(l);
            yield return 0.6f;

            // Accueil : tous les modes x tous les paliers
            menu.ShowMenu(); yield return 0.9f;
            foreach (GameMode m in System.Enum.GetValues(typeof(GameMode)))
                foreach (DifficultyTier t in System.Enum.GetValues(typeof(DifficultyTier)))
                {
                    if (m != GameMode.Classic && t != DifficultyTier.Moderate) continue;   // 7 combinaisons au lieu de 15
                    GameModes.Current = m; DifficultyTiers.Current = t;
                    yield return 0.45f;
                    Scan(LangCodes[l], "home/" + m + "/" + t);
                }
            GameModes.Current = prevMode; DifficultyTiers.Current = prevTier;

            // dernière partie : tous les montants remplis, onglet or puis onglet éclats
            LastRunPanel lrp = Object.FindFirstObjectByType<LastRunPanel>(FindObjectsInactive.Include);
            if (lrp != null)
            {
                lrp.gameObject.SetActive(false); lrp.gameObject.SetActive(true);
                yield return 2.0f;
                Scan(LangCodes[l], "home/lastrun-or");
                Button et = FindButton(lrp.transform, "EclatSourcesTab");
                if (et != null) { et.onClick.Invoke(); yield return 2.0f; Scan(LangCodes[l], "home/lastrun-eclats"); }
                Button gt = FindButton(lrp.transform, "GoldSourcesTab");
                if (gt != null) gt.onClick.Invoke();
            }

            menu.ShowCharacterSelect(); yield return 1.0f; Scan(LangCodes[l], "characters");
            menu.ShowUpgrades(); yield return 1.0f; Scan(LangCodes[l], "skills");
            // détail de chaque nœud
            var tree = Object.FindFirstObjectByType<SkillTreeUI>();
            if (tree != null)
            {
                SkillNode[] nodes = Object.FindObjectsByType<SkillNode>(FindObjectsSortMode.None);
                foreach (SkillNode n in nodes)
                {
                    if (n == null || !n.gameObject.activeInHierarchy) continue;
                    tree.OnNodeClicked(n.NodeId, (RectTransform)n.transform);
                    yield return 0.35f;
                    Scan(LangCodes[l], "skills/node/" + n.NodeId);
                }
                tree.ClosePanel();
            }
            menu.ShowReputation(); yield return 1.0f; Scan(LangCodes[l], "reputation");
            menu.ShowSettings(); yield return 1.0f; Scan(LangCodes[l], "settings");
            var tabs = new List<Button>();
            foreach (Button bt in menu.SettingsPanelObject.GetComponentsInChildren<Button>(true)) if (bt.name.StartsWith("Tab_")) tabs.Add(bt);
            foreach (Button bt in tabs) { bt.onClick.Invoke(); yield return 0.8f; Scan(LangCodes[l], "settings/" + bt.name); }
            menu.ShowMenu(); yield return 0.5f;
            // grimoire
            GrimoireUI gr = Object.FindFirstObjectByType<GrimoireUI>(FindObjectsInactive.Include);
            if (gr != null) { gr.Open(); yield return 1.2f; { GameObject gw = GameObject.Find("GrimoireWindow"); Scan(LangCodes[l], "grimoire", gw != null ? gw.transform : null); } gr.SendMessage("Close", true, SendMessageOptions.DontRequireReceiver); yield return 0.5f; }
            QuitConfirm.Show(); yield return 1.0f; { GameObject qw = GameObject.Find("QuitConfirm"); Scan(LangCodes[l], "quit", qw != null ? qw.transform : null); }
            { QuitConfirm qc = Object.FindFirstObjectByType<QuitConfirm>(); if (qc != null) qc.SendMessage("Close", SendMessageOptions.DontRequireReceiver); }
            yield return 0.5f;
            menu.ShowMenu(); yield return 0.6f;
            // fenêtres du didacticiel (visite du menu)
            if (tutorial != null)
            {
                var steps = new List<TutorialStep>();
                foreach (string mname in new[] { "WelcomeSteps", "PointToCharacters", "ReputationSteps", "SkillSteps" })
                {
                    object r = Call(tutorial, mname);
                    if (r is List<TutorialStep> ls) steps.AddRange(ls); else if (r is TutorialStep one) steps.Add(one);
                }
                { object r = Call(tutorial, "CharacterSteps"); if (r is List<TutorialStep> ls) steps.AddRange(ls); }
                { object r = Call(tutorial, "HomeSteps", true); if (r is List<TutorialStep> ls) steps.AddRange(ls); }
                int si = 0;
                foreach (TutorialStep st in steps)
                {
                    st.Panel = null;   // pas de changement de page pendant l'audit
                    TutorialOverlay.Get().Present(st, 0, 1, () => { }, () => { });
                    yield return 0.7f;
                    GameObject ov = GameObject.Find("TutorialOverlay");
                    Scan(LangCodes[l], "tutoriel/menu" + (si++), ov != null ? ov.transform : null);
                    TutorialOverlay.Get().Dismiss();
                    yield return 0.15f;
                }
            }
        }
        data.lastRun = prevRun;
        data.tutorialPages = prevPages; data.tutorialHints = prevHints;
        SetLang(0);
    }

    private static object Call(object target, string method, params object[] args)
    {
        var mi = target.GetType().GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        return mi != null ? mi.Invoke(target, args) : null;
    }

    private static Button FindButton(Transform root, string name)
    {
        foreach (Button b in root.GetComponentsInChildren<Button>(true)) if (b.name == name) return b;
        return null;
    }

    private static IEnumerator GameRoutine()
    {
        GameUI ui = GameUI.Instance;
        GameManager gm = GameManager.Instance;
        if (ui == null || gm == null) { Status = "pas de GameUI"; yield break; }
        gm.SetIntroLock(false);
        EnemyBase.DebugInvincible = true; BossBase.DebugInvincible = true;
        GameObject pl = GameObject.FindWithTag("Player");
        if (pl != null) pl.GetComponent<HealthSystem>().AddExternalInvincibility();
        EnemySpawner spawner = Object.FindFirstObjectByType<EnemySpawner>();
        if (spawner != null) spawner.gameObject.SetActive(false);
        if (WaveManager.Instance != null) WaveManager.Instance.enabled = false;
        SaveData data = MetaProgressionManager.Instance.Data;
        int prevPages = data.tutorialPages, prevHints = data.tutorialHints;
        data.tutorialPages = -1; data.tutorialHints = -1;

        var ups = new List<UpgradeData>();
        foreach (string guid in AssetDatabase.FindAssets("t:UpgradeData"))
        {
            UpgradeData u = AssetDatabase.LoadAssetAtPath<UpgradeData>(AssetDatabase.GUIDToAssetPath(guid));
            if (u != null) ups.Add(u);
        }
        UpgradeUI upg = Object.FindFirstObjectByType<UpgradeUI>(FindObjectsInactive.Include);
        var lum = LevelUpManager.Instance;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        GameObject levelPanel = lum != null ? (GameObject)typeof(LevelUpManager).GetField("_levelUpPanel", flags).GetValue(lum) : null;
        GameObject victoryPanel = (GameObject)typeof(GameUI).GetField("_victoryPanel", flags).GetValue(ui);
        GameObject gameOverPanel = (GameObject)typeof(GameUI).GetField("_gameOverPanel", flags).GetValue(ui);
        Color blue = new Color(0.45f, 0.75f, 1f);
        yield return 1.0f;

        foreach (int l in Langs)
        {
            Status = "game lang " + LangCodes[l] + " (" + ups.Count + " cartes)";
            SetLang(l);
            yield return 0.7f;
            Scan(LangCodes[l], "hud");

            // bandeaux
            string[][] banners = {
                new[]{"FRÉNÉSIE !", null}, new[]{"UN ÉCLAT APPARAÎT !", "Allez le ramasser pour déclencher l'Éclipse."},
                new[]{"ÉCLIPSE", "Vos dégâts augmentent, mais les ennemis affluent."}, new[]{"LE JOUR SE LÈVE", "+{0} éclats gagnés"} };
            int bi = 0;
            foreach (string[] b in banners)
            {
                string sub = b[1] == null ? null : (b[1].Contains("{0}") ? Loc.F(b[1], 12) : Loc.T(b[1]));
                ui.ShowEventBanner(Loc.T(b[0]), blue, sub, 40f);
                yield return 0.8f;
                Scan(LangCodes[l], "banner" + (bi++));
            }
            ui.ShowEventBanner("", blue, null, 0.01f); yield return 0.9f;
            ui.UpdateNightCounter(12, false); yield return 0.4f; Scan(LangCodes[l], "night-counter"); ui.HideNightCounter();

            // barre de boss
            foreach (string bn in new[] { "Les 3 Titans", "La Source Corrompue", "Le Cerf Ancestral" })
            {
                ui.ShowBossHP(Loc.T(bn)); yield return 0.5f; Scan(LangCodes[l], "boss/" + bn);
            }
            ui.HideBossHP(); yield return 0.2f;

            // cartes de niveau : toutes les améliorations, par 3
            if (upg != null && levelPanel != null)
            {
                levelPanel.SetActive(true); upg.gameObject.SetActive(true);
                for (int i = 0; i < ups.Count; i += 3)
                {
                    var batch = new List<UpgradeData>();
                    for (int k = i; k < Mathf.Min(i + 3, ups.Count); k++) batch.Add(ups[k]);
                    upg.DisplayUpgrades(batch);
                    double t0 = EditorApplication.timeSinceStartup;
                    yield return 0.5f;
                    while (upg.CardsBusy && EditorApplication.timeSinceStartup - t0 < 4.0) yield return 0.2f;
                    foreach (CanvasGroup cg in upg.GetComponentsInChildren<CanvasGroup>(true)) if (cg.alpha < 1f && cg.gameObject.activeInHierarchy) cg.alpha = 1f;
                    yield return 0.2f;
                    Scan(LangCodes[l], "cartes/" + (i / 3));
                }
                levelPanel.SetActive(false); upg.gameObject.SetActive(false);
            }

            // pause
            ui.ShowPausePanel(true); yield return 0.9f; Scan(LangCodes[l], "pause");
            ui.ShowAbandonConfirm(true); yield return 0.5f; Scan(LangCodes[l], "pause/abandon");
            ui.ShowAbandonConfirm(false); ui.ShowPausePanel(false); yield return 0.3f;

            // fenêtres du didacticiel de la partie
            string[][] tut = {
                new[]{"DÉPLACEMENT","Déplacez-vous avec {0}."}, new[]{"VIE","Votre santé. Si elle tombe à 0, la partie est terminée."},
                new[]{"EXPÉRIENCE","Ramassez les gemmes laissées par les monstres pour monter de niveau."}, new[]{"ACTIONS","Dash ({0}) et Ultime ({1})."},
                new[]{"TIR AUTOMATIQUE","Un ennemi approche. Votre héros vise et tire automatiquement : concentrez-vous sur vos déplacements pour garder vos distances."},
                new[]{"EXPÉRIENCE","L'ennemi vaincu laisse des gemmes d'expérience. Passez dessus pour les ramasser."},
                new[]{"DASH","Orientez-vous vers le projectile, puis effectuez un dash à travers lui ({0}). Le dash vous rend invincible : le projectile est absorbé, déclenche une Nova qui blesse les ennemis proches et charge le Cristal d'ultime."},
                new[]{"DASH OFFENSIF","Le dash n'est pas qu'un déplacement : c'est une arme. Absorber un projectile inflige des dégâts avec la Nova et charge l'ultime, mais un dash mal chronométré vous laisse exposé."},
                new[]{"ABSORPTION","Absorbez les projectiles des gobelins avec le dash pour remplir le Cristal d'ultime."},
                new[]{"ULTIME","Le Cristal est chargé : {0} déclenche l'Ultime. Il blesse tous les ennemis, les ralentit et attire toutes les gemmes au sol."},
                new[]{"ENTRAÎNEMENT TERMINÉ","Vous connaissez l'essentiel. Les vraies parties vous attendent."},
                new[]{"","Choisissez une carte pour obtenir une nouvelle arme ou améliorer une arme existante."} };
            int ti = 0;
            foreach (string[] tt in tut)
            {
                string[] cur = tt;
                TutorialStep st = new TutorialStep { Title = () => cur[0].Length > 0 ? Loc.T(cur[0]) : "", Body = () => Loc.F(cur[1], "Z Q S D", "F"), NextLabel = () => Loc.T("Compris"), ShowSkip = false, Width = 780f, Position = TutorialStep.Place.TopRight };
                TutorialOverlay.Get().Present(st, 0, 1, () => { }, null);
                yield return 0.7f;
                GameObject ov = GameObject.Find("TutorialOverlay");
                Scan(LangCodes[l], "tutoriel/jeu" + (ti++), ov != null ? ov.transform : null);
                TutorialOverlay.Get().Dismiss();
                yield return 0.1f;
            }

            // écrans de fin
            ui.ShowVictory(900f, 12345, 8000, 9500, 30, 250, true, 1500); yield return 5.0f;
            Scan(LangCodes[l], "victoire", victoryPanel != null ? victoryPanel.transform : null);
            if (victoryPanel != null) victoryPanel.SetActive(false);
            ui.ShowGameOver(600f, 4321, 3000, 3500, 22, 90, true, 500, "Le Cerf Ancestral", 1); yield return 5.0f;
            Scan(LangCodes[l], "defaite", gameOverPanel != null ? gameOverPanel.transform : null);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            yield return 0.3f;
        }
        data.tutorialPages = prevPages; data.tutorialHints = prevHints;
        SetLang(0);
    }

    // =========================================================================================================================
    //  ANALYSE
    // =========================================================================================================================
    private static bool Visible(TMP_Text t)
    {
        if (!t.gameObject.activeInHierarchy || !t.enabled) return false;
        if (string.IsNullOrWhiteSpace(t.text)) return false;
        if (t.color.a < 0.05f) return false;
        for (Transform p = t.transform; p != null; p = p.parent)
        {
            CanvasGroup cg = p.GetComponent<CanvasGroup>();
            if (cg != null && cg.alpha < 0.05f) return false;
        }
        return true;
    }

    private static Rect ScreenRect(RectTransform rt, Canvas canvas)
    {
        Vector3[] c = new Vector3[4];
        rt.GetWorldCorners(c);
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, c[0]);
        Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }

    private static Rect TextRect(TMP_Text t, Canvas canvas)
    {
        Bounds b = t.textBounds;
        Vector3[] w = new Vector3[4];
        Transform tr = t.transform;
        w[0] = tr.TransformPoint(new Vector3(b.min.x, b.min.y, 0));
        w[2] = tr.TransformPoint(new Vector3(b.max.x, b.max.y, 0));
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, w[0]);
        Vector2 c = RectTransformUtility.WorldToScreenPoint(cam, w[2]);
        return Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.y, c.y), Mathf.Max(a.x, c.x), Mathf.Max(a.y, c.y));
    }

    private static string Path_(Transform t)
    {
        var sb = new StringBuilder(t.name);
        for (Transform p = t.parent; p != null && sb.Length < 120; p = p.parent) sb.Insert(0, p.name + "/");
        return sb.ToString();
    }

    private struct Item { public TMP_Text t; public Rect r; public Canvas canvas; }

    public static int Scan(string lang, string page, Transform only = null)
    {
        _ctx = lang + " | " + page;
        var all = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var items = new List<Item>();
        int issues = 0;
        float screenW = Screen.width, screenH = Screen.height;
        foreach (TMP_Text t in all)
        {
            if (t == null || !Visible(t)) continue;
            if (only != null && !t.transform.IsChildOf(only)) continue;
            t.ForceMeshUpdate();
            Canvas canvas = t.canvas;
            Rect tr = TextRect(t, canvas);
            if (tr.width < 1f || tr.height < 1f) continue;
            if (!ClipByMasks(t, canvas, ref tr)) continue;   // entièrement masqué (liste qui défile)
            items.Add(new Item { t = t, r = tr, canvas = canvas });
            Rect box = ScreenRect(t.rectTransform, canvas);
            string txt = t.text.Replace("\n", " ");
            if (txt.Length > 70) txt = txt.Substring(0, 70) + "…";

            // a) dépasse de son propre cadre
            bool auto = t.enableAutoSizing;
            float tolX = Mathf.Max(2f, box.width * 0.01f);
            if (tr.width > box.width + tolX && (!auto || t.fontSize <= t.fontSizeMin + 0.05f))
            { Report("SORT_LARGEUR", t, txt, "texte " + (int)tr.width + "px > cadre " + (int)box.width + "px"); issues++; }
            else if (!IsCjk(lang) && tr.height > box.height + Mathf.Max(3f, box.height * 0.05f) && (!auto || t.fontSize <= t.fontSizeMin + 0.05f))
            { Report("SORT_HAUTEUR", t, txt, "texte " + (int)tr.height + "px > cadre " + (int)box.height + "px"); issues++; }
            if (t.isTextTruncated) { Report("TRONQUÉ", t, txt, ""); issues++; }

            // b) dépasse du bouton / de la carte qui le porte (1er parent avec un fond, plus petit que l'écran)
            for (Transform p = t.transform.parent; p != null; p = p.parent)
            {
                Graphic g = p.GetComponent<Graphic>();
                if (g == null || g is TMP_Text || !g.enabled || g.color.a < 0.05f) continue;
                Rect pr = ScreenRect((RectTransform)p, canvas);
                if (pr.width > screenW * 0.55f || pr.height > screenH * 0.6f) break;   // grand fond : pas un contenant
                float m = 2f;
                if (tr.xMin < pr.xMin - m || tr.xMax > pr.xMax + m || tr.yMin < pr.yMin - m || tr.yMax > pr.yMax + m)
                { Report("SORT_DU_FOND", t, txt, "texte " + R(tr) + " fond '" + p.name + "' " + R(pr)); issues++; }
                break;
            }
            // c) hors écran
            if (tr.xMin < -2f || tr.xMax > screenW + 2f || tr.yMin < -2f || tr.yMax > screenH + 2f)
            { Report("HORS_ECRAN", t, txt, R(tr)); issues++; }
        }

        // e) texte resté en français / en latin dans une langue qui n'est pas la sienne
        if (lang != "fr")
            foreach (Item it in items)
            {
                string raw = StripTags(it.t.text);
                string why = Untranslated(lang, raw);
                if (why != null) { _report.AppendLine("[" + _ctx + "] NON_TRADUIT  '" + Cut(raw) + "'  " + Path_(it.t.transform) + "  " + why); issues++; IssueCount++; }
            }

        // d) chevauchements entre textes
        for (int i = 0; i < items.Count; i++)
            for (int j = i + 1; j < items.Count; j++)
            {
                Item a = items[i], b = items[j];
                if (a.canvas != b.canvas) continue;
                if (a.t.transform.IsChildOf(b.t.transform) || b.t.transform.IsChildOf(a.t.transform)) continue;
                float ox = Mathf.Min(a.r.xMax, b.r.xMax) - Mathf.Max(a.r.xMin, b.r.xMin);
                float oy = Mathf.Min(a.r.yMax, b.r.yMax) - Mathf.Max(a.r.yMin, b.r.yMin);
                if (ox <= 3f || oy <= 3f) continue;
                float area = ox * oy, minArea = Mathf.Min(a.r.width * a.r.height, b.r.width * b.r.height);
                if (area < minArea * 0.15f) continue;
                if (a.t.text == b.t.text) continue;   // ombre / double
                string ta = a.t.text.Replace("\n", " "), tb = b.t.text.Replace("\n", " ");
                _report.AppendLine("[" + _ctx + "] CHEVAUCHE  '" + Cut(ta) + "' (" + Path_(a.t.transform) + ")  X  '" + Cut(tb) + "' (" + Path_(b.t.transform) + ")  " + (int)(100f * area / minArea) + "%");
                issues++; IssueCount++;
            }

        if (issues > 0 && Shots && _shots < 400) Shot(lang, page);
        return issues;
    }

    private static bool IsCjk(string lang) => lang == "ja" || lang == "ko" || lang == "zh";

    // Rectangle du texte limité par les masques (RectMask2D / Mask) de ses parents ; faux si plus rien de visible.
    private static bool ClipByMasks(TMP_Text t, Canvas canvas, ref Rect r)
    {
        for (Transform p = t.transform.parent; p != null; p = p.parent)
        {
            bool mask = p.GetComponent<RectMask2D>() != null || (p.GetComponent<Mask>() != null && p.GetComponent<Mask>().enabled);
            if (!mask) continue;
            Rect m = ScreenRect((RectTransform)p, canvas);
            float x0 = Mathf.Max(r.xMin, m.xMin), x1 = Mathf.Min(r.xMax, m.xMax), y0 = Mathf.Max(r.yMin, m.yMin), y1 = Mathf.Min(r.yMax, m.yMax);
            if (x1 - x0 < 1f || y1 - y0 < 1f) return false;
            r = Rect.MinMaxRect(x0, y0, x1, y1);
        }
        return true;
    }

    private static string StripTags(string s) => System.Text.RegularExpressions.Regex.Replace(s ?? "", "<[^>]+>", "");

    private static readonly System.Text.RegularExpressions.Regex FrWords = new System.Text.RegularExpressions.Regex(@"(avec|pour|dans|vous|votre|vos|nous|sont|cette|ces|chaque|ennemis|partie|niveau|de la|du|des|les|le|la|un|une|est|sur|sans|aux)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    private static readonly System.Text.RegularExpressions.Regex FrStrong = new System.Text.RegularExpressions.Regex(@"(avec|pour|dans|vous|votre|vos|nous|sont|cette|ces|chaque|ennemis|partie|niveau|aux|sans|sur)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    private static readonly System.Text.RegularExpressions.Regex LatinWord = new System.Text.RegularExpressions.Regex(@"[A-Za-z]{4,}");
    private static readonly HashSet<string> LatinOk = new HashSet<string> { "aether", "kael", "lyra", "fps", "shift", "ctrl", "enter", "space", "esc", "tab", "alt", "backspace", "stick", "boss", "gold", "level", "lang" , "language", "english", "deutsch", "espanol" , "portugues", "italiano", "polski", "azerty", "qwerty", "zqsd", "wasd", "vsync", "hdr", "fsr" };

    private static string Untranslated(string lang, string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length < 4) return null;
        // clé française (insensible à la casse) restée telle quelle
        string low = raw.Trim().ToLowerInvariant();
        foreach (string k in FrKeysLower) if (k == low) return "= clé française";
        if (FrStrong.IsMatch(raw) && lang != "ja" && lang != "ko" && lang != "zh" && lang != "ru" && lang != "pl")
        {
            // es / pt / it / en / de : mots français sans équivalent
            if (lang == "en" || lang == "de" || FrStrongOnly(raw)) return "mots français";
        }
        if (lang == "ru" || lang == "zh" || lang == "ja" || lang == "ko" || lang == "pl")
        {
            if (FrWords.Matches(raw).Count >= 2) return "mots français";
            foreach (System.Text.RegularExpressions.Match m in LatinWord.Matches(raw))
                if (lang != "pl" && !LatinOk.Contains(m.Value.ToLowerInvariant())) return "mot latin '" + m.Value + "'";
        }
        return null;
    }

    private static bool FrStrongOnly(string raw)
    {
        foreach (System.Text.RegularExpressions.Match m in FrStrong.Matches(raw))
        {
            string w = m.Value.ToLowerInvariant();
            if (w == "avec" || w == "vous" || w == "votre" || w == "vos" || w == "nous" || w == "cette" || w == "chaque" || w == "ennemis" || w == "dans" || w == "pour") return true;
        }
        return false;
    }

    private static HashSet<string> _frKeys;
    private static HashSet<string> FrKeysLower
    {
        get
        {
            if (_frKeys == null)
            {
                _frKeys = new HashSet<string>();
                foreach (string k in LocTableEN.Table.Keys)
                {
                    string kl = StripTags(k).Trim().ToLowerInvariant();
                    // une clé qui ne change pas d'une langue à l'autre (nom propre, chiffre) n'est pas une absence de traduction
                    string en;
                    if (LocTableEN.Table.TryGetValue(k, out en) && StripTags(en).Trim().ToLowerInvariant() == kl) continue;
                    if (kl.Length >= 4) _frKeys.Add(kl);
                }
            }
            return _frKeys;
        }
    }

    private static string R(Rect r) => "[" + (int)r.xMin + "," + (int)r.yMin + " " + (int)r.width + "x" + (int)r.height + "]";
    private static string Cut(string s) => s.Length > 50 ? s.Substring(0, 50) + "…" : s;

    private static void Report(string kind, TMP_Text t, string txt, string detail)
    {
        _report.AppendLine("[" + _ctx + "] " + kind + "  '" + txt + "'  " + Path_(t.transform) + "  " + detail);
        IssueCount++;
    }

    private static void Shot(string lang, string page)
    {
        try
        {
            Camera main = Camera.main;
            GameObject go = new GameObject("auditCam");
            Camera cam = go.AddComponent<Camera>();
            if (main != null) cam.CopyFrom(main);
            RenderTexture rt = new RenderTexture(1920, 1080, 24);
            cam.targetTexture = rt;
            var changed = new List<Canvas>();
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; changed.Add(c); }
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); tex.Apply();
            string dir = Path.Combine(OutDir, "shots"); Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, lang + "_" + page.Replace('/', '_') + ".png"), tex.EncodeToPNG());
            foreach (Canvas c in changed) c.renderMode = RenderMode.ScreenSpaceOverlay;
            RenderTexture.active = null; Object.Destroy(go); rt.Release(); Object.Destroy(tex);
            _shots++;
        }
        catch (System.Exception e) { _report.AppendLine("SHOT ERR " + e.Message); }
    }
}
