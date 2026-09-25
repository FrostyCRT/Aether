using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("État du jeu")]
    [SerializeField] private bool _isGameOver = false;
    public bool IsGameOver => _isGameOver;
    public bool IsPaused { get; private set; } = false;

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
        EnemyKaiju.ResetRunState();
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

        // rend toujours le curseur en quittant la partie (retour menu, rechargement)
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);   // rend le curseur système (menu principal, etc.)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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

    private void ApplyCursor(bool show)
    {
        if (_blankCursor == null)
        {
            _blankCursor = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            _blankCursor.SetPixels32(new Color32[16 * 16]);
            _blankCursor.Apply();
            _blankCursor.hideFlags = HideFlags.HideAndDontSave;
        }
        if (_cursorState != (show ? 1 : 0))
        {
            _cursorState = show ? 1 : 0;
            Cursor.SetCursor(show ? null : _blankCursor, Vector2.zero, CursorMode.ForceSoftware);
        }
        Cursor.lockState = show ? CursorLockMode.None : CursorLockMode.Confined;
        Cursor.visible = show;
    }

    private void UpdateCursor()
    {
        bool needCursor = _isGameOver || IsPaused
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
        if (WaveManager.Instance != null && WaveManager.Instance.BossAlive) return;
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
        Invoke(nameof(ShowGameOver), 1.5f);
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
        Invoke(nameof(ShowVictory), 2f);
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