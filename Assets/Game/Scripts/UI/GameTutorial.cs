using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// REFAIT (2026-11-16, retour utilisateur : « la partie lancée avec le didacticiel doit être une partie de test, qui ne compte pas
// vraiment, donc tout doit être guidé ») - PARTIE D'ENTRAÎNEMENT SCÉNARISÉE. Créée à l'entrée dans la scène Game quand le menu l'a
// demandée (Tutorial.RequestPractice) ; il n'y a PAS de vraies vagues : le spawner est coupé et chaque ennemi est placé par le
// scénario, dans cet ordre :
//   1. déplacement  2. visite du HUD (chaque élément n'apparaît qu'à son étape)  3. un loup arrive d'un côté (tir automatique)
//   4. gemmes d'expérience (flèches)  5. loups un par un jusqu'au niveau suivant (jusqu'à ce que l'XP suffise)
//   6. choix des cartes  7. un gobelin immobile : au tir, ralenti + voile + « dash » (invincibilité, absorption, Nova, Cristal)
//   8. gobelins en temps réel jusqu'à ce que le Cristal soit plein  9. ~15 ennemis autour du joueur : ralenti + voile + « Ultime »
//   10. fin : fondu et retour au menu.
// La partie ne compte pas : la sauvegarde est figée pendant l'entraînement (SaveSystem.SuppressWrites) et restaurée à la sortie, le
// joueur ne peut pas mourir, l'Ultime est verrouillé jusqu'à son étape. Les ralentis ne sont pas des pauses : le temps coule (très
// lentement) tant que le joueur agit, et se fige s'il ne fait rien pendant FreezeAfter secondes (temps réel).
public class GameTutorial : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Game") return;
        if (!Tutorial.ConsumePractice()) return;   // seulement la partie d'entraînement
        new GameObject("GameTutorial").AddComponent<GameTutorial>();
    }

    private const float SlowScale = 0.08f;       // ralenti « abusé »
    private const float FreezeAfter = 4f;        // secondes réelles sans action avant de figer
    private const string WolfTag = "Enemy";
    private const string GoblinTag = "EnemyShooter";
    private const float WolfXpScale = 2.5f;       // XP d'un loup d'entraînement : x2,5 (4 loups suffisent pour le 1er niveau)

    private GameManager _gm;
    private GameObject _player;
    private PlayerController _pc;
    private CrystalSystem _crystal;
    private XPSystem _xp;
    private TutorialPointer _pointer;
    private Func<List<Vector3>> _pointerSource;
    private readonly List<Vector3> _pointerBuf = new List<Vector3>();

    private bool _pausedByUs;
    private float _prevTimeScale = 1f;
    private bool _ended;
    private bool _wasSuppressed;
    private string _saveSnapshot;
    private bool _prevNeverDie;

    // ---- cycle de vie -----------------------------------------------------------------------------------------------------
    private void Start()
    {
        _pointer = TutorialPointer.Create();
        StartCoroutine(Script());
    }

    private void Update()
    {
        HideHud();
        if (_pointer == null) return;
        if (_pointerSource != null) _pointer.Show(_pointerSource());
        else _pointer.Hide();
    }

    private void OnDestroy()
    {
        if (_pointer != null) Destroy(_pointer.gameObject);
        RevealHud(null);
        ReleasePause();
        if (TutorialOverlay.IsShowing) TutorialOverlay.Get().Dismiss();
        HealthSystem.DebugNeverDie = _prevNeverDie;
        RestoreSave();
    }

    // ---- sauvegarde figée pendant l'entraînement --------------------------------------------------------------------------
    private void FreezeSave()
    {
        MetaProgressionManager meta = MetaProgressionManager.Instance;
        _wasSuppressed = SaveSystem.SuppressWrites;
        if (meta != null && meta.Data != null) _saveSnapshot = JsonUtility.ToJson(meta.Data);
        SaveSystem.SuppressWrites = true;
    }

    private void RestoreSave()
    {
        if (_saveSnapshot == null) return;
        MetaProgressionManager meta = MetaProgressionManager.Instance;
        if (meta != null && meta.Data != null)
            JsonUtility.FromJsonOverwrite(_saveSnapshot, meta.Data);
        _saveSnapshot = null;
        SaveSystem.SuppressWrites = _wasSuppressed;
        if (meta != null && meta.Data != null && !_wasSuppressed) SaveSystem.Save(meta.Data);
        Tutorial.FinishPractice();
    }

    // ---- fin -------------------------------------------------------------------------------------------------------------
    private void EndPractice()
    {
        if (_ended) return;
        _ended = true;
        StopAllCoroutines();
        _pointerSource = null;
        if (TutorialOverlay.IsShowing) TutorialOverlay.Get().Dismiss();
        ReleasePause();
        Time.timeScale = 1f;
        if (_crystal != null) _crystal.UltimateLocked = false;
        if (_gm != null) _gm.GoToMainMenu();   // fondu noir, puis le menu (la sauvegarde est restaurée à la destruction de la scène)
    }

    private void SkipPractice()
    {
        EndPractice();
    }

    // ---- scénario ----------------------------------------------------------------------------------------------------------
    private IEnumerator Script()
    {
        while ((_gm = GameManager.Instance) == null) yield return null;
        if (TutorialOverlay.IsShowing) TutorialOverlay.Get().Dismiss();   // reste éventuel d'une fenêtre du menu
        while ((_player = GameObject.FindWithTag("Player")) == null) yield return null;
        _pc = _player.GetComponent<PlayerController>();
        _crystal = _player.GetComponent<CrystalSystem>();
        while ((_xp = XPSystem.Instance) == null) yield return null;
        // l'annonce du défi gèle le jeu au départ : on attend la reprise
        while (Time.timeScale <= 0f || _gm.IsPaused) yield return null;

        Setup();
        yield return new WaitForSeconds(0.8f);

        // 1. déplacement ----------------------------------------------------------------------------------------------------
        yield return Modal(new TutorialStep
        {
            Title = () => Loc.T("DÉPLACEMENT"),
            Body = () => Loc.F("Déplacez-vous avec {0}.", Tutorial.MoveKeys()),
        });
        Vector3 start = _player.transform.position;
        while ((_player.transform.position - start).sqrMagnitude < 36f) yield return null;
        yield return new WaitForSeconds(0.4f);

        // 2. visite du HUD ----------------------------------------------------------------------------------------------------
        yield return HudStep("Health", "VIE", "Barre de vie. À zéro, la partie est perdue.");
        yield return HudStep("XpGroup", "EXPÉRIENCE", "Ramassez les gemmes laissées par les monstres pour monter de niveau.");
        yield return HudStep("ActionCluster", "ACTIONS", null);
        yield return new WaitForSeconds(0.5f);

        // 3. le premier loup ------------------------------------------------------------------------------------------------
        EnemyBase wolf = SpawnWolf(Side(1f), 13f, 0.7f, 0.55f);
        if (wolf != null)
        {
            _pointerSource = () => Single(wolf != null && wolf.gameObject.activeInHierarchy ? wolf.transform.position + Vector3.up * 1.2f : (Vector3?)null);
            while (wolf != null && wolf.gameObject.activeInHierarchy && Flat(wolf.transform.position, _player.transform.position) > 10.5f) yield return null;
            _pointerSource = null;
            if (wolf != null && wolf.gameObject.activeInHierarchy)
                yield return Modal(new TutorialStep
                {
                    Title = () => Loc.T("TIR AUTOMATIQUE"),
                    Body = () => Loc.T("Un ennemi approche. Votre héros vise et tire automatiquement : concentrez-vous sur vos déplacements pour garder vos distances."),
                });
            while (wolf != null && wolf.gameObject.activeInHierarchy) yield return null;
        }

        // 4. première gemme -----------------------------------------------------------------------------------------------
        yield return new WaitForSeconds(0.5f);
        if (XPGemField.Count > 0)
        {
            yield return Modal(new TutorialStep
            {
                Title = () => Loc.T("EXPÉRIENCE"),
                Body = () => Loc.T("L'ennemi vaincu laisse des gemmes d'expérience. Passez dessus pour les ramasser."),
            });
            _pointerSource = GemArrows;
            while (XPGemField.Count > 0 && !LevelUpPending()) yield return null;
            _pointerSource = null;
        }

        // 5. loups, un par un, jusqu'au niveau suivant ---------------------------------------------------------------------------
        // Nombre de loups calculé : XP manquante / XP d'un loup (+ une marge : le reste d'XP d'une gerbe de gemmes est inférieur à 10).
        int level = _xp.CurrentLevel;
        float wolfXp = wolf != null ? Mathf.Max(1f, wolf.XpReward) : 10f;
        int planned = Mathf.CeilToInt((_xp.XPToNextLevel - _xp.CurrentXP) / wolfXp) + 1;
        int spawned = 0;
        float sideSign = -1f;
        ShowHint("EXPÉRIENCE", "Éliminez les ennemis et ramassez leurs gemmes jusqu'à remplir la barre d'expérience.", TutorialStep.Place.Top);
        while (_xp.CurrentLevel == level && !LevelUpPending())
        {
            bool anyAlive = AliveCount(WolfTag) > 0;
            bool gemsOnGround = XPGemField.Count > 0;
            _pointerSource = gemsOnGround ? (Func<List<Vector3>>)GemArrows : null;
            // une génération seulement quand tout est ramassé (générations lentes)
            if (!anyAlive && !gemsOnGround && spawned < planned + 12)   // plafond de sécurité seulement
            {
                yield return new WaitForSeconds(1.3f);
                if (_xp.CurrentLevel != level || LevelUpPending()) break;
                SpawnWolf(Side(sideSign), 12f, 0.7f, 0.6f);
                sideSign = -sideSign;
                spawned++;
            }
            yield return null;
        }
        _pointerSource = null;
        HideHint();

        // 6. choix des cartes ----------------------------------------------------------------------------------------------------
        while (!(LevelUpManager.Instance != null && LevelUpManager.Instance.IsWaitingForChoice)) yield return null;
        yield return LevelUpPill();
        while (LevelUpManager.Instance != null && LevelUpManager.Instance.IsChoosing) yield return null;
        yield return new WaitForSeconds(1.2f);

        // 7. le gobelin immobile : le dash ------------------------------------------------------------------------------------------
        EnemyBase goblin = SpawnGoblin(Side(1f), 7f, true);
        bool absorbed = false;
        int chargesBefore = TotalCharges();
        while (!absorbed)
        {
            EnemyProjectile shot = null;
            while (shot == null)
            {
                KeepGoblin(goblin, Side(1f), 7f);
                shot = IncomingProjectile(3.6f);
                yield return null;
            }
            if (_pc != null) _pc.ResetDashCooldown();
            EnemyProjectile tracked = shot;
            _pointerSource = () => Single(tracked != null && tracked.gameObject.activeInHierarchy ? tracked.transform.position : (Vector3?)null);
            yield return SlowPrompt(new TutorialStep
            {
                Title = () => Loc.T("DASH"),
                Body = () => Loc.F("Dashez à travers le projectile ({0}). Le dash vous rend invincible : le projectile est absorbé, déclenche une Nova qui blesse les ennemis proches et charge le Cristal d'ultime.", Tutorial.Keys(GameAction.Dash)),
            }, SlowScale, () => TotalCharges() > chargesBefore, () => tracked == null || !tracked.gameObject.activeInHierarchy);
            _pointerSource = null;
            absorbed = TotalCharges() > chargesBefore;
        }
        yield return new WaitForSeconds(0.6f);
        yield return Modal(new TutorialStep
        {
            Title = () => Loc.T("DASH OFFENSIF"),
            Body = () => Loc.T("Le dash n'est pas qu'un déplacement : c'est une arme. Absorber un projectile inflige des dégâts avec la Nova et charge l'ultime, mais un dash mal chronométré vous laisse exposé."),
        });
        ClearEnemies(GoblinTag);
        ClearProjectiles();

        // 8. plusieurs gobelins, en temps réel, jusqu'à ce que le Cristal soit plein ----------------------------------------------
        ShowHint("ABSORPTION", "Absorbez les projectiles des gobelins avec le dash pour remplir le Cristal d'ultime.", TutorialStep.Place.Top);
        float nextGoblin = 0f;
        while (_crystal != null && _crystal.StoredUlts < 1)
        {
            if (AliveCount(GoblinTag) < 3 && Time.time >= nextGoblin)
            {
                SpawnGoblin(RandomDir(), 9f, false);
                nextGoblin = Time.time + 1.2f;
            }
            yield return null;
        }
        HideHint();
        yield return new WaitForSeconds(0.4f);
        ClearEnemies(GoblinTag);
        ClearProjectiles();

        // 9. une quinzaine d'ennemis autour du joueur : l'Ultime ---------------------------------------------------------------------------
        float ringStart = Time.time;
        int ring = 0;
        const int RingSize = 15;
        while (true)
        {
            if (ring < RingSize && Time.time - ringStart >= ring * 0.16f)
            {
                float a = ring * (360f / RingSize) * Mathf.Deg2Rad;
                SpawnWolf(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), 11f + (ring % 3), 0.8f, 0.2f, 0.2f);
                ring++;
            }
            int alive = AliveCount(WolfTag);
            float nearest = NearestEnemy(WolfTag);
            if (ring >= RingSize && alive >= 12 && nearest <= 6.5f) break;
            if (Time.time - ringStart > 12f && alive >= 8 && nearest <= 6.5f) break;
            if (ring >= RingSize && alive < RingSize && Time.time - ringStart < 10f && Time.time - ringStart > ring * 0.16f + 0.5f)
                SpawnWolf(RandomDir(), 11f, 0.8f, 0.2f, 0.2f);
            yield return null;
        }
        SpawnGemRing(6, 4.5f);
        _crystal.UltimateLocked = false;
        yield return SlowPrompt(new TutorialStep
        {
            Title = () => Loc.T("ULTIME"),
            Body = () => Loc.F("Le Cristal est chargé : {0} déclenche l'Ultime. Il blesse tous les ennemis, les ralentit et attire toutes les gemmes au sol.", Tutorial.Keys(GameAction.Ultimate)),
        }, 0.06f, () => _crystal == null || _crystal.StoredUlts < 1, null);

        // 10. fin ---------------------------------------------------------------------------------------------------------------------
        yield return new WaitForSeconds(3.2f);
        RevealHud(null);
        yield return Modal(new TutorialStep
        {
            Title = () => Loc.T("ENTRAÎNEMENT TERMINÉ"),
            Body = () => Loc.T("Vous connaissez l'essentiel. Les vraies parties vous attendent."),
            NextLabel = () => Loc.T("Terminer"),
            ShowSkip = false,
        }, false);
        EndPractice();
    }

    // ---- mise en place ------------------------------------------------------------------------------------------------------
    private void Setup()
    {
        FreezeSave();
        _prevNeverDie = HealthSystem.DebugNeverDie;
        HealthSystem.DebugNeverDie = true;   // partie de test : on ne meurt pas
        if (_crystal != null) _crystal.UltimateLocked = true;
        EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>();
        if (spawner != null) spawner.gameObject.SetActive(false);   // aucune vague : tout est placé par le scénario
        if (WaveManager.Instance != null) WaveManager.Instance.enabled = false;
        foreach (EnemyBase e in FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
            if (e != null && e.gameObject.activeInHierarchy && ObjectPool.Instance != null) ObjectPool.Instance.ReturnToPool(e.PoolTag, e.gameObject);
        HideHud();
    }

    // ---- fenêtres ----------------------------------------------------------------------------------------------------------------
    private bool _modalDone;

    // Fenêtre qui se lit : voile noir, jeu à l'arrêt, « Compris » pour continuer.
    private IEnumerator Modal(TutorialStep s, bool pause = true)
    {
        _modalDone = false;
        if (s.NextLabel == null) s.NextLabel = () => Loc.T("Compris");
        s.SkipLabel = () => Loc.T("Passer l'entraînement");
        s.SkipConfirm = false;
        s.Width = 1000f;
        if (pause) SetPause(true);
        TutorialOverlay.Get().Present(s, 0, 1, () => _modalDone = true, SkipPractice);
        while (!_modalDone) yield return null;
        TutorialOverlay.Get().Dismiss();
        if (pause) ReleasePause();
    }

    // Étape du HUD : l'élément n'apparaît qu'ici, entouré d'un cadre.
    private IEnumerator HudStep(string group, string title, string body)
    {
        TutorialStep s = new TutorialStep
        {
            Title = () => Loc.T(title),
            Body = body != null ? (Func<string>)(() => Loc.T(body)) : (() => Loc.F("Dash ({0}) et Ultime ({1}).", Tutorial.Keys(GameAction.Dash), Tutorial.Keys(GameAction.Ultimate))),
            Target = () => Hud(group),
            Tight = true,
            OnEnter = () => RevealHud(group),
            NextLabel = () => Loc.T("Suivant"),
        };
        yield return Modal(s);
    }

    // Conseil qui laisse jouer (sans voile) ; reste affiché jusqu'à HideHint.
    private bool _hintShown;
    private void ShowHint(string title, string body, TutorialStep.Place place)
    {
        TutorialStep s = new TutorialStep
        {
            Title = () => Loc.T(title),
            Body = () => Loc.T(body),
            Dim = false,
            BlockInput = false,
            RequireAction = true,
            ShowSkip = true,
            SkipLabel = () => Loc.T("Passer l'entraînement"),
            SkipConfirm = false,
            Position = place,
            Width = 880f,
            TextScale = 0.8f,
        };
        _hintShown = true;
        TutorialOverlay.Get().Present(s, 0, 1, HideHint, SkipPractice);
    }

    private void HideHint()
    {
        if (!_hintShown) return;
        _hintShown = false;
        TutorialOverlay.Get().Dismiss();
    }

    // Choix de carte : la fenêtre se place en bas (les cartes occupent le haut et le milieu), sans voile.
    private IEnumerator LevelUpPill()
    {
        TutorialStep s = new TutorialStep
        {
            Title = () => Loc.T("NIVEAU SUPÉRIEUR"),
            Body = () => Loc.F("Choisissez une carte ({0}) : une nouvelle arme ou l'amélioration d'une arme existante.", Tutorial.CardKeys()),
            Dim = false,
            BlockInput = false,
            RequireAction = true,
            ShowSkip = true,
            SkipLabel = () => Loc.T("Passer l'entraînement"),
            SkipConfirm = false,
            Position = TutorialStep.Place.Bottom,
            Width = 1500f,
            TextScale = 0.7f,
            DoneWhen = () => LevelUpManager.Instance == null || !LevelUpManager.Instance.IsWaitingForChoice,
        };
        bool done = false;
        TutorialOverlay.Get().Present(s, 0, 1, () => done = true, SkipPractice);
        while (!done) yield return null;
        TutorialOverlay.Get().Dismiss();
    }

    // Ralenti : voile léger, le temps coule à `scale` tant que le joueur agit, se fige sinon. Finit quand success() ou failed() est vrai.
    private IEnumerator SlowPrompt(TutorialStep s, float scale, Func<bool> success, Func<bool> failed)
    {
        bool done = false;
        s.Dim = true;
        s.DimAlpha = 0.5f;
        s.BlockInput = false;
        s.RequireAction = true;
        s.ShowSkip = true;
        s.SkipLabel = () => Loc.T("Passer l'entraînement");
        s.SkipConfirm = false;
        s.Position = TutorialStep.Place.Top;
        s.Width = 1000f;
        s.TextScale = 0.9f;
        s.DoneWhen = () => success() || (failed != null && failed());
        TutorialOverlay.Get().Present(s, 0, 1, () => done = true, SkipPractice);
        Time.timeScale = scale;
        bool frozen = false;
        float last = Time.unscaledTime;
        while (!done)
        {
            if (PlayerActed()) { last = Time.unscaledTime; if (frozen) { frozen = false; Time.timeScale = scale; } }
            else if (!frozen && Time.unscaledTime - last > FreezeAfter) { frozen = true; Time.timeScale = 0f; }
            yield return null;
        }
        TutorialOverlay.Get().Dismiss();
        Time.timeScale = 1f;
    }

    private static bool PlayerActed()
    {
        return Input.anyKey || Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.2f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.2f;
    }

    // ---- pause (visite, fenêtres) -----------------------------------------------------------------------------------------
    private void SetPause(bool pause)
    {
        if (pause && !_pausedByUs)
        {
            _pausedByUs = true;
            _prevTimeScale = Time.timeScale;
            _gm.SetPausedFlag(true);
            Time.timeScale = 0f;
        }
        else if (!pause) ReleasePause();
    }

    private void ReleasePause()
    {
        if (!_pausedByUs) return;
        _pausedByUs = false;
        if (_gm != null) _gm.SetPausedFlag(false);
        if (_gm != null && !_gm.IsGameOver) Time.timeScale = _prevTimeScale > 0f ? _prevTimeScale : 1f;
    }

    // ---- ennemis, gemmes, projectiles --------------------------------------------------------------------------------------------
    private Vector3 Right()
    {
        Camera cam = Camera.main;
        Vector3 r = cam != null ? cam.transform.right : Vector3.right;
        r.y = 0f;
        return r.sqrMagnitude > 0.01f ? r.normalized : Vector3.right;
    }

    private Vector3 Side(float sign) => Right() * sign;

    private static Vector3 RandomDir()
    {
        float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
    }

    private Vector3 Ground(Vector3 dir, float distance)
    {
        Vector3 p = _player.transform.position + dir.normalized * distance;
        p.y = 0f;
        return MapBoundaryUtils.ClampToZone(p);
    }

    private static float Flat(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return Mathf.Sqrt(dx * dx + dz * dz); }

    private EnemyBase SpawnWolf(Vector3 dir, float distance, float healthScale, float speedScale, float xpScale = WolfXpScale)
    {
        return SpawnEnemy(WolfTag, dir, distance, healthScale, speedScale, xpScale);
    }

    private EnemyBase SpawnEnemy(string tag, Vector3 dir, float distance, float healthScale, float speedScale, float xpScale = 1f)
    {
        if (ObjectPool.Instance == null || _player == null) return null;
        GameObject go = ObjectPool.Instance.Get(tag, Ground(dir, distance), Quaternion.identity, healthScale, 0f);
        if (go == null) return null;
        go.TryGetComponent(out EnemyBase e);
        if (e == null) return null;
        e.SetXpHealthMultiplier(xpScale);
        e.SetSpeedMultiplier(speedScale);
        return e;
    }

    // Gobelin : immobile et indestructible pour l'explication (il reste à sa place et tire), sinon un vrai gobelin.
    private EnemyBase SpawnGoblin(Vector3 dir, float distance, bool still)
    {
        EnemyBase e = SpawnEnemy(GoblinTag, dir, distance, still ? 5000f : 1f, still ? 0f : 1f);
        if (e != null && !still) e.SetXpHealthMultiplier(0.3f);
        return e;
    }

    // Le gobelin immobile reste à bonne portée de tir (il ne tire que dans sa zone d'arrêt).
    private void KeepGoblin(EnemyBase goblin, Vector3 dir, float distance)
    {
        if (goblin == null || !goblin.gameObject.activeInHierarchy) return;
        if (Flat(goblin.transform.position, _player.transform.position) > distance + 0.8f)
            goblin.transform.position = Ground(dir, distance);
    }

    private int AliveCount(string tag)
    {
        int n = 0;
        foreach (EnemyBase e in FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
            if (e != null && e.gameObject.activeInHierarchy && e.PoolTag == tag) n++;
        return n;
    }

    private float NearestEnemy(string tag)
    {
        float best = float.MaxValue;
        foreach (EnemyBase e in FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
            if (e != null && e.gameObject.activeInHierarchy && e.PoolTag == tag)
                best = Mathf.Min(best, Flat(e.transform.position, _player.transform.position));
        return best;
    }

    private void ClearEnemies(string tag)
    {
        foreach (EnemyBase e in FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
            if (e != null && e.gameObject.activeInHierarchy && e.PoolTag == tag && ObjectPool.Instance != null)
                ObjectPool.Instance.ReturnToPool(tag, e.gameObject);
    }

    private void ClearProjectiles()
    {
        foreach (EnemyProjectile p in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
            if (p != null && p.gameObject.activeInHierarchy && ObjectPool.Instance != null) ObjectPool.Instance.ReturnToPool("EnemyProjectile", p.gameObject);
    }

    // Projectile qui fonce sur le joueur et passe sous `range` mètres.
    private EnemyProjectile IncomingProjectile(float range)
    {
        EnemyProjectile best = null;
        float bestD = range;
        foreach (EnemyProjectile p in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
        {
            if (p == null || !p.gameObject.activeInHierarchy) continue;
            Vector3 to = _player.transform.position - p.transform.position; to.y = 0f;
            float d = to.magnitude;
            if (d < bestD && Vector3.Dot(p.Direction, to.normalized) > 0.3f) { bestD = d; best = p; }
        }
        return best;
    }

    // Une valeur qui ne fait que croître à chaque absorption (Ultimes prêts, puis charges du Cristal).
    private int TotalCharges() => _crystal == null ? 0 : _crystal.StoredUlts * (_crystal.MaxCharges + 1) + _crystal.CurrentCharges;

    private void SpawnGemRing(int count, float radius)
    {
        if (XPGemSpawner.Instance == null) return;
        for (int i = 0; i < count; i++)
        {
            float a = i * (360f / count) * Mathf.Deg2Rad;
            XPGemSpawner.Instance.SpawnGems(Ground(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), radius), 12f);
        }
    }

    private bool LevelUpPending()
    {
        LevelUpManager lu = LevelUpManager.Instance;
        return lu != null && lu.IsChoosing;
    }

    private static List<Vector3> Single(Vector3? p)
    {
        List<Vector3> l = new List<Vector3>(1);
        if (p.HasValue) l.Add(p.Value);
        return l;
    }

    private List<Vector3> GemArrows()
    {
        XPGemField.CollectPositions(_pointerBuf, 5);
        for (int i = 0; i < _pointerBuf.Count; i++) _pointerBuf[i] += Vector3.up * 0.3f;
        return _pointerBuf;
    }

    // ---- HUD masqué pendant l'entraînement, montré élément par élément ------------------------------------------------
    private RectTransform Hud(string name)
    {
        Transform canvas = GameUI.Instance != null ? GameUI.Instance.transform.parent : null;
        return canvas != null ? canvas.Find("HUD/" + name) as RectTransform : null;
    }

    private static readonly string[] HudGroups = { "Health", "XpGroup", "StatsGroup", "ActionCluster", "ChallengeGroup" };
    private readonly Dictionary<string, Transform> _hudHidden = new Dictionary<string, Transform>();
    private readonly Dictionary<Transform, Vector3> _hudScale = new Dictionary<Transform, Vector3>();
    private Transform _legendHidden;
    private bool _hudHideDone;

    // Masquage par échelle nulle (et non par alpha / activation) : le HUD pilote lui-même ses CanvasGroup et ses objets.
    private void HideHud()
    {
        if (_hudHideDone) return;
        Transform canvas = GameUI.Instance != null ? GameUI.Instance.transform.parent : null;
        Transform hud = canvas != null ? canvas.Find("HUD") : null;
        if (hud == null) return;
        _hudHideDone = true;
        foreach (string n in HudGroups)
        {
            Transform t = hud.Find(n);
            if (t == null) continue;
            _hudScale[t] = t.localScale;
            t.localScale = Vector3.zero;
            _hudHidden[n] = t;
        }
        HudKeyLegend legend = hud.GetComponentInChildren<HudKeyLegend>(true);
        if (legend != null)
        {
            _legendHidden = legend.transform;
            _hudScale[_legendHidden] = _legendHidden.localScale;
            _legendHidden.localScale = Vector3.zero;
        }
    }

    /// <summary>Montre un groupe du HUD (null = tout).</summary>
    private void RevealHud(string group)
    {
        if (_legendHidden != null && (group == null || group == "ActionCluster")) { _legendHidden.localScale = _hudScale[_legendHidden]; _legendHidden = null; }
        foreach (var kv in _hudHidden)
            if ((group == null || kv.Key == group) && kv.Value != null) kv.Value.localScale = _hudScale[kv.Value];
    }
}
