using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("État du jeu")]
    [SerializeField] private bool _isGameOver = false;
    public bool IsGameOver => _isGameOver;
    public bool IsPaused { get; private set; } = false;

    // AJOUTE - evenement global declenche UNE SEULE FOIS des que la partie se
    // termine (victoire OU game over). Sert aux systemes qui doivent reagir
    // exactement a ce moment precis (ex: figer l'Animator des ennemis) plutot que
    // de compter sur leur propre Update(), qui s'arrete justement des que
    // IsGameOver devient vrai - sans cet evenement, rien ne les prevenait jamais
    // que la partie venait de se terminer, ils restaient figes a mi-animation.
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

    // AJOUTE - compte les VRAIS boss vaincus cette run (BossBase.Die() incremente
    // via AddBossKill(), uniquement si !IsSummoned). Sert au calcul des Eclats en
    // fin de run (niveau atteint + boss vaincus + bonus de victoire).
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
            FindObjectOfType<Cinemachine.CinemachineVirtualCamera>();

        if (vcam == null)
        {
            Debug.LogWarning("GameManager : aucune CinemachineVirtualCamera trouvée.");
            return;
        }

        vcam.Follow = playerTransform;
        vcam.LookAt = playerTransform;
    }

    private void Update()
    {
        if (_isGameOver) return;
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

    // AJOUTE - permet a d'autres systemes de gel temporaire du jeu (actuellement :
    // LevelUpManager pendant un level-up) de synchroniser IsPaused SANS passer par
    // TogglePause()/ResumePause(), qui ouvriraient/fermeraient en plus le panel de
    // pause manuel et le HUD - deux effets de bord qu'on ne veut PAS pendant un
    // level-up. Ne touche pas a Time.timeScale : chaque appelant reste responsable
    // du sien (LevelUpManager gere deja le sien de son cote).
    public void SetPausedFlag(bool paused)
    {
        IsPaused = paused;
    }

    public void AbandonRun()
    {
        IsPaused = false;
        Time.timeScale = 1f;

        // CORRIGE - troisieme site d'appel a SaveRunResults() rate lors de
        // l'extension de la signature (les 2 autres, ShowGameOver/ShowVictory,
        // avaient bien ete mis a jour). Abandon = pas une victoire.
        int levelReached = XPSystem.Instance != null ? XPSystem.Instance.CurrentLevel : 1;
        MetaProgressionManager.Instance.SaveRunResults(_runTimer, _killCount, levelReached, _bossKillCount, false);

        SceneManager.LoadScene(0);
    }

    public void AddKill()
    {
        _killCount++;
        if (GameUI.Instance != null)
            GameUI.Instance.UpdateKillCount(_killCount);
    }

    public void TriggerGameOver()
    {
        if (_isGameOver) return;
        _isGameOver = true;
        OnGameEnded?.Invoke();
        Invoke(nameof(ShowGameOver), 1.5f);
    }

    private void ShowGameOver()
    {
        GameUI.Instance.SetHUDVisible(false);
        int goldEarned = MetaProgressionManager.Instance.RunGold;

        // MODIFIE - SaveRunResults prend desormais aussi le niveau atteint, le
        // nombre de boss vaincus et si la run s'est terminee en victoire, pour
        // calculer les Eclats gagnes (independants de l'or ramasse).
        int levelReached = XPSystem.Instance != null ? XPSystem.Instance.CurrentLevel : 1;
        MetaProgressionManager.Instance.SaveRunResults(_runTimer, _killCount, levelReached, _bossKillCount, false);

        GameUI.Instance.ShowGameOver(_runTimer, _killCount, goldEarned);
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(1);
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
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
        int goldEarned = MetaProgressionManager.Instance.RunGold;
        int levelReached = XPSystem.Instance != null ? XPSystem.Instance.CurrentLevel : 1;

        // MODIFIE - meme extension que ShowGameOver(), avec victory = true cette
        // fois (bonus de victoire applique dans le calcul des Eclats).
        MetaProgressionManager.Instance.SaveRunResults(_runTimer, _killCount, levelReached, _bossKillCount, true);

        GameUI.Instance.ShowVictory(
            _runTimer,
            _killCount,
            goldEarned,
            levelReached
        );
    }
}