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

        switch (index)
        {
            case 1:
                SpawnPrefab(_prefabKael);
                break;
            case 2:
                SpawnPrefab(_prefabLyra);
                break;
            default:
                SpawnPrefab(_prefabAether);
                break;
        }
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
    }

    private void HandleEndScreenRevealComplete()
    {
        _replayShortcutReady = true;
    }

    private void Update()
    {
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
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
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