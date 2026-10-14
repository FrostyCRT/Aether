using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("État du jeu")]
    [SerializeField] private bool _isGameOver = false;
    public bool IsGameOver => _isGameOver;
    public bool IsPaused { get; private set; } = false;

    // AJOUTÉ (2026-10-13, retour utilisateur : "pendant l'animation [...] le player ne peut pas bouger et la game
    // n'a pas commencé, le timer est à 0") - verrou posé par GameUI pendant toute la séquence d'annonce du défi au
    // lancement (aller au centre -> lecture -> retour au HUD). Réutilise IsPaused + Time.timeScale=0 : TOUT le
    // gameplay (PlayerController, EnemyBase, WeaponBase, EnemySpawner, le chrono ci-dessous) vérifie déjà
    // IsPaused, donc ce seul booléen gèle tout sans dupliquer une garde dans 6 fichiers différents. Un flag séparé
    // (_introLockActive) sert uniquement à empêcher Échap de rouvrir le jeu via TogglePause() pendant ce verrou
    // (sinon IsPaused repasserait à false avant la fin de l'animation) et à garder le curseur masqué (voir
    // UpdateCursor ci-dessous - pas de panneau à cliquer pendant cette cinématique).
    private bool _introLockActive = false;

    public void SetIntroLock(bool active)
    {
        _introLockActive = active;
        IsPaused = active;
        Time.timeScale = active ? 0f : 1f;
    }

    // AJOUTÉ (2026-10-01) - délais entre la fin de partie et l'apparition du panel, réglables dans l'Inspector.
    // Avant : valeurs codées en dur (1.5 s Game Over / 2 s Victoire). Le panel de Game Over arrivait avant la fin
    // de l'animation de mort du joueur. Le délai doit être AU MOINS égal à la durée du clip de mort.
    [Header("Délais de fin de partie")]
    [Tooltip("Secondes entre la mort du joueur et l'apparition du panel de Game Over. Doit couvrir toute l'animation de mort (durée du clip Death + chute du corps).")]
    [SerializeField] private float _gameOverDelay = 3f;
    [Tooltip("Secondes entre la mort du dernier boss et l'apparition du panel de Victoire.")]
    [SerializeField] private float _victoryDelay = 2f;

    // AJOUTE - retient la cause de la mort transmise par HealthSystem.Die(),
    // pour le message d'ambiance contextuel du Game Over.
    private string _deathCause = "horde";

    // MODIFIE - ne se contente plus de verifier que le panel est visible : passe
    // a vrai uniquement quand GameUI signale (OnEndScreenRevealComplete) que
    // TOUTE la sequence de reveal est terminee (comptage de l'Or, defi eventuel,
    // apercu de palier). Avant ce changement, le raccourci se debloquait des
    // l'affichage du panel, ce qui permettait de sauter par-dessus le message
    // de defi/record en spammant Entree - exactement ce qu'on veut eviter.
    private bool _replayShortcutReady = false;

    public static System.Action OnGameEnded;

    [Header("Spawn Personnage")]
    [SerializeField] private Transform _playerSpawnPoint;
    [SerializeField] private GameObject _prefabAether;
    [SerializeField] private GameObject _prefabKael;
    [SerializeField] private GameObject _prefabLyra;

    private float _runTimer = 0f;
    private int _killCount = 0;
    public int KillCount => _killCount;
    public float RunTimer => _runTimer;

    // AJOUTE (2026-09-27) - DEBUG/TEST uniquement (voir DebugCheats.cs) : avance directement le chrono de la run, pour
    // atteindre instantanément l'heure d'un boss classique (5/10/15 min) sans attendre.
    public void DebugSetRunTimer(float seconds) { _runTimer = Mathf.Max(0f, seconds); }

    private int _bossKillCount = 0;
    public int BossKillCount => _bossKillCount;

    public void AddBossKill()
    {
        _bossKillCount++;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        SpawnSelectedCharacter();
    }

    private void SpawnSelectedCharacter()
    {
        if (MetaProgressionManager.Instance == null)
        {
            Debug.LogWarning("GameManager : MetaProgressionManager introuvable, spawn Aether par défaut.");
            SpawnPrefab(_prefabAether);
            return;
        }

        int index = MetaProgressionManager.Instance.GetSelectedCharacterIndex();

        GameObject prefab;
        switch (index)
        {
            case 1: prefab = _prefabKael; break;
            case 2: prefab = _prefabLyra; break;
            default: prefab = _prefabAether; break;
        }

        // AJOUTE (2026-09-20) - skin équipé (onglet Réputation) : son prefab remplace celui d'origine s'il en a un.
        SkinEntry skin = MetaProgressionManager.Instance.GetEquippedSkin(index);
        if (skin != null && skin.playerPrefab != null) prefab = skin.playerPrefab;

        SpawnPrefab(prefab);
    }

    private void SpawnPrefab(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("GameManager : prefab personnage non assigné !");
            return;
        }

        Vector3 spawnPos = _playerSpawnPoint != null
            ? _playerSpawnPoint.position
            : Vector3.zero;

        GameObject playerInstance = Instantiate(prefab, spawnPos, Quaternion.identity);

        AssignCinemachineTarget(playerInstance.transform);
    }

    private void AssignCinemachineTarget(Transform playerTransform)
    {
        Cinemachine.CinemachineVirtualCamera vcam =
            FindFirstObjectByType<Cinemachine.CinemachineVirtualCamera>();

        if (vcam == null)
        {
            Debug.LogWarning("GameManager : aucune CinemachineVirtualCamera trouvée.");
            return;
        }

        vcam.Follow = playerTransform;
        vcam.LookAt = playerTransform;
    }

    // AJOUTE - s'abonne/se desabonne proprement a l'evenement statique de GameUI :
    // necessaire car GameManager n'est PAS en DontDestroyOnLoad (une nouvelle
    // instance existe a chaque rechargement de scene) - sans desabonnement dans
    // OnDisable, chaque nouvelle instance s'accumulerait comme abonne
    // supplementaire sur l'evenement statique, jamais nettoye.
    private void OnEnable()
    {
        GameUI.OnEndScreenRevealComplete += HandleEndScreenRevealComplete;
    }

    private void OnDisable()
    {
        GameUI.OnEndScreenRevealComplete -= HandleEndScreenRevealComplete;

        // CORRIGÉ (2026-10-11, retour utilisateur : "je veux le même système qu'avant") - remettait ici la vraie
        // flèche système (Cursor.SetCursor(null,...), visible=true) en quittant la partie ; comme CustomCursor
        // (le gantelet) reste actif dans TOUTES les scènes, ça aurait affiché deux curseurs superposés en revenant
        // au menu. Le curseur système reste maintenant masqué partout, et NeedsCursor=false laisse CustomCursor
        // revenir à son propre comportement par défaut (gantelet affiché, voir CustomCursor.Update).
        NeedsCursor = false;
        Cursor.SetCursor(_blankCursor, Vector2.zero, CursorMode.ForceSoftware);
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = false;
        _cursorState = -1;
    }

    private void HandleEndScreenRevealComplete()
    {
        _replayShortcutReady = true;
    }

    // AJOUTÉ (2026-09-24) - curseur invisible en pleine partie ; visible dès qu'un panneau doit être cliqué
    // (pause, paramètres, choix de niveau, Game Over, Victoire). Le curseur personnalisé viendra plus tard.
    // Cursor.visible = false ne suffit pas : dans l'éditeur, Échap (touche Pause) remet le curseur système visible et
    // ignore Cursor.visible jusqu'au prochain clic. On remplace donc AUSSI l'image du curseur par un carré 100 %
    // transparent : même si le système le déclare « visible », on ne voit rien, dans tous les cas.
    private static Texture2D _blankCursor;
    private int _cursorState = -1;   // 0 = caché, 1 = visible

    // AJOUTÉ (2026-10-11, retour utilisateur : "je veux le même système qu'avant") - lu par CustomCursor pour savoir
    // s'il doit afficher le gantelet (true : pause/paramètres/level-up/Game Over, le curseur doit pouvoir cliquer)
    // ou le masquer (false : pleine partie, comme le faisait l'ancien système avant que le gantelet n'existe).
    public bool NeedsCursor { get; private set; }

    private void ApplyCursor(bool show)
    {
        NeedsCursor = show;

        if (_blankCursor == null)
        {
            _blankCursor = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            _blankCursor.SetPixels32(new Color32[16 * 16]);
            _blankCursor.Apply();
            _blankCursor.hideFlags = HideFlags.HideAndDontSave;
        }
        // CORRIGÉ (2026-10-11, retour utilisateur : "je veux le même système qu'avant") - remettait ici la vraie
        // flèche système (Cursor.SetCursor(null,...)) quand show=true, ce qui aurait affiché la flèche blanche de
        // l'OS PAR-DESSUS le gantelet de CustomCursor (qui gère maintenant seul la visibilité via NeedsCursor
        // ci-dessus). Le curseur système reste désormais TOUJOURS masqué (texture transparente) ; seul show change,
        // lu par CustomCursor pour afficher ou non son image.
        if (_cursorState != 0)
        {
            _cursorState = 0;
            Cursor.SetCursor(_blankCursor, Vector2.zero, CursorMode.ForceSoftware);
        }
        // CORRIGÉ (2026-10-11, retour utilisateur : "dans le menu je peux faire sortir la souris du cadre de
        // 1920x1080p") - lockState repassait à None dès qu'un panneau s'affichait (show=true), désactivant le
        // confinement à la fenêtre pendant justement tous les écrans où la souris doit cliquer (pause, paramètres,
        // level-up, game over) - c'est là que le joueur la fait le plus facilement sortir. Confined reste actif
        // dans tous les cas : la souris peut toujours bouger librement et cliquer DANS la fenêtre, seulement plus
        // en sortir.
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = false;
    }

    private void UpdateCursor()
    {
        bool needCursor = _isGameOver || (IsPaused && !_introLockActive)
            || SettingsPage.InGameOpen
            || (LevelUpManager.Instance != null && LevelUpManager.Instance.IsWaitingForChoice);
        ApplyCursor(needCursor);
    }

    // au retour dans la fenêtre / l'éditeur, le système peut avoir rendu son curseur : on force la réapplication
    private void OnApplicationFocus(bool focus)
    {
        if (focus) _cursorState = -1;
    }

    private void Update()
    {
        UpdateCursor();

        // AJOUTÉ (2026-10-13) - verrou de l'annonce de défi : bloque tout, y compris le toggle pause (Échap) et
        // l'incrément du chrono juste en-dessous (_runTimer += ... n'est jamais atteint).
        if (_introLockActive) return;

        // MODIFIE - raccourci de relance instantanee (Entree/Espace), desormais
        // verrouille tant que _replayShortcutReady n'est pas passe a vrai par
        // HandleEndScreenRevealComplete().
        if (_isGameOver)
        {
            if (_replayShortcutReady &&
                (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)))
            {
                RestartGame();
            }
            // AJOUTÉ (2026-09-24) - Échap (ou la touche Pause rebindée) sur l'écran de Game Over / Victoire = retour au menu
            else if (_replayShortcutReady && (Input.GetKeyDown(KeyCode.Escape) || GameInput.Down(GameAction.Pause)))
            {
                _replayShortcutReady = false; // évite un double chargement
                GoToMainMenu();
            }
            return;
        }

        // la page Paramètres (ouverte depuis le menu pause) garde la touche Pause pour se refermer
        if (GameInput.Down(GameAction.Pause) && !SettingsPage.InGameOpen && SettingsPage.ClosedFrame != Time.frameCount)
        {
            if (LevelUpManager.Instance != null && LevelUpManager.Instance.IsWaitingForChoice) return;
            TogglePause();
        }
        if (IsPaused) return;
        // Choc des titans : les boss sont là dès le début, le chrono (= temps de victoire) doit tourner pendant le combat.
        if (WaveManager.Instance != null && WaveManager.Instance.BossAlive && !GameModes.IsTitans) return;
        _runTimer += Time.deltaTime;
    }

    public void TogglePause()
    {
        IsPaused = !IsPaused;
        Time.timeScale = IsPaused ? 0f : 1f;
        GameUI.Instance.SetHUDVisible(!IsPaused);
        GameUI.Instance.ShowPausePanel(IsPaused);
    }

    public void ResumePause()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        GameUI.Instance.SetHUDVisible(true);
        GameUI.Instance.ShowPausePanel(false);
    }

    public void SetPausedFlag(bool paused)
    {
        IsPaused = paused;
    }

    public void AbandonRun()
    {
        IsPaused = false;
        Time.timeScale = 1f;

        int levelReached = XPSystem.Instance != null ? XPSystem.Instance.CurrentLevel : 1;

        // CORRIGE (2026-09-17) - abandonner sautait entierement l'evaluation du
        // defi : contrairement a ShowGameOver()/ShowVictory(), cet appel
        // manquait, donc un defi deja "Reussi" dans le HUD en cours de run
        // (ex. "Tuer 150 ennemis") ne rapportait jamais son bonus d'Or si le
        // joueur abandonnait au lieu de mourir/gagner - meme progres reel,
        // recompense perdue sans raison. Meme traitement que les 2 autres fins
        // de run desormais.
        if (ChallengeManager.Instance != null)
            ChallengeManager.Instance.EvaluateAndApplyReward(_killCount, levelReached, _bossKillCount, MetaProgressionManager.Instance.RunGold);

        MetaProgressionManager.Instance.SaveRunResults(_runTimer, _killCount, levelReached, _bossKillCount, false);

        // MODIFIE (2026-09-14) - passe par SceneLoader/LoadingScreen (vrai
        // chargement async + retour visuel) au lieu d'un SceneManager.LoadScene
        // brut et synchrone, source de hitch. Voir SceneLoader.cs.
        SceneLoader.LoadScene("MainMenu");
    }

    public void AddKill()
    {
        _killCount++;
        if (GameUI.Instance != null)
            GameUI.Instance.UpdateKillCount(_killCount);

        // AJOUTE - rafraichit la progression des defis bases sur les kills
        // (ex. "Tuer 150 ennemis") en temps reel, pas seulement en fin de run.
        if (ChallengeManager.Instance != null)
            ChallengeManager.Instance.RefreshDisplay();
    }

    public void TriggerGameOver(string deathCause = "horde")
    {
        if (_isGameOver) return;
        _isGameOver = true;
        _deathCause = deathCause;
        OnGameEnded?.Invoke();
        // MODIFIÉ (2026-10-01) - délai réglable (_gameOverDelay) au lieu de 1.5 s en dur.
        Invoke(nameof(ShowGameOver), _gameOverDelay);
    }

    private void ShowGameOver()
    {
        GameUI.Instance.SetHUDVisible(false);

        // CORRIGE (2026-09-17) - ClearPool existait deja (commentaire d'origine :
        // "requise... pour nettoyer l'ecran a la victoire") mais n'etait jamais
        // appelee nulle part - les projectiles ennemis continuaient de voler a
        // l'ecran derriere le panneau de fin. Meme correctif sur les 2 ecrans de
        // fin (Victoire ET Game Over, le probleme visuel est identique).
        if (ObjectPool.Instance != null)
        {
            ObjectPool.Instance.ClearPool("EnemyProjectile");
            ObjectPool.Instance.ClearPool("Projectile");
        }

        int levelReached = XPSystem.Instance != null ? XPSystem.Instance.CurrentLevel : 1;

        // MODIFIE - baseGold capture AVANT le bonus de defi, totalGold APRES -
        // les deux sont necessaires pour l'animation en 2 temps (compte jusqu'a
        // baseGold, puis si defi reussi, reprend jusqu'a totalGold).
        int baseGold = MetaProgressionManager.Instance.RunGold;

        bool challengeCompleted = false;
        float challengeRewardPercent = 0f;

        if (ChallengeManager.Instance != null)
        {
            ChallengeManager.Instance.EvaluateAndApplyReward(_killCount, levelReached, _bossKillCount, baseGold);
            challengeCompleted = ChallengeManager.Instance.IsCompleted;
            challengeRewardPercent = ChallengeManager.Instance.GetCurrentRewardPercent();
        }

        int totalGold = MetaProgressionManager.Instance.RunGold;

        MetaProgressionManager.Instance.SaveRunResults(_runTimer, _killCount, levelReached, _bossKillCount, false);
        int eclatsEarned = MetaProgressionManager.Instance.LastRunEclatsEarned;

        // MODIFIE - ajout de levelReached, meme ordre de parametres que ShowVictory
        // desormais que GameUI.ShowGameOver() affiche aussi le niveau atteint.
        GameUI.Instance.ShowGameOver(_runTimer, _killCount, baseGold, totalGold, levelReached, eclatsEarned, challengeCompleted, challengeRewardPercent, _deathCause, _bossKillCount);
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneLoader.LoadScene("Game");
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneLoader.LoadScene("MainMenu");
    }

    public void TriggerVictory()
    {
        if (_isGameOver) return;
        _isGameOver = true;
        OnGameEnded?.Invoke();
        // MODIFIÉ (2026-10-01) - délai réglable (_victoryDelay) au lieu de 2 s en dur.
        Invoke(nameof(ShowVictory), _victoryDelay);
    }

    private void ShowVictory()
    {
        GameUI.Instance.SetHUDVisible(false);

        if (ObjectPool.Instance != null)
        {
            ObjectPool.Instance.ClearPool("EnemyProjectile");
            ObjectPool.Instance.ClearPool("Projectile");
        }

        int levelReached = XPSystem.Instance != null ? XPSystem.Instance.CurrentLevel : 1;

        // MODIFIE - meme logique que ShowGameOver() : baseGold/totalGold separes
        // pour l'animation en 2 temps.
        int baseGold = MetaProgressionManager.Instance.RunGold;

        bool challengeCompleted = false;
        float challengeRewardPercent = 0f;

        if (ChallengeManager.Instance != null)
        {
            ChallengeManager.Instance.EvaluateAndApplyReward(_killCount, levelReached, _bossKillCount, baseGold);
            challengeCompleted = ChallengeManager.Instance.IsCompleted;
            challengeRewardPercent = ChallengeManager.Instance.GetCurrentRewardPercent();
        }

        int totalGold = MetaProgressionManager.Instance.RunGold;

        MetaProgressionManager.Instance.SaveRunResults(_runTimer, _killCount, levelReached, _bossKillCount, true);
        int eclatsEarned = MetaProgressionManager.Instance.LastRunEclatsEarned;

        GameUI.Instance.ShowVictory(
            _runTimer,
            _killCount,
            baseGold,
            totalGold,
            levelReached,
            eclatsEarned,
            challengeCompleted,
            challengeRewardPercent
        );
    }
}