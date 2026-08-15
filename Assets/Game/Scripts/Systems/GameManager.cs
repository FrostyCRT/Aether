using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("État du jeu")]
    [SerializeField] private bool _isGameOver = false;
    public bool IsGameOver => _isGameOver;
    public bool IsPaused { get; private set; } = false;

    // AJOUTÉ — spawn du bon personnage selon la sélection
    [Header("Spawn Personnage")]
    [SerializeField] private Transform _playerSpawnPoint;
    [SerializeField] private GameObject _prefabAether;
    [SerializeField] private GameObject _prefabKael;
    [SerializeField] private GameObject _prefabLyra;

    private float _runTimer = 0f;
    private int _killCount = 0;
    public int KillCount => _killCount;
    public float RunTimer => _runTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        EnemyKaiju.ResetRunState();
        SpawnSelectedCharacter(); // remet ici
    }

    // AJOUTÉ — instancie le bon prefab au bon endroit selon MetaProgressionManager
    private void SpawnSelectedCharacter()
    {
        if (MetaProgressionManager.Instance == null)
        {
            Debug.LogWarning("GameManager : MetaProgressionManager introuvable, spawn Aether par défaut.");
            SpawnPrefab(_prefabAether);
            return;
        }

        int index = MetaProgressionManager.Instance.GetSelectedCharacterIndex();
        Debug.Log($"[SPAWN] Index sélectionné : {index}"); // TEMP

        switch (index)
        {
            case 1:  
                Debug.Log("[SPAWN] Spawn Kael"); // TEMP
                SpawnPrefab(_prefabKael);  
                break;
            case 2:  
                Debug.Log("[SPAWN] Spawn Lyra"); // TEMP
                SpawnPrefab(_prefabLyra);  
                break;
            default: 
                Debug.Log("[SPAWN] Spawn Aether"); // TEMP
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

        // AJOUTÉ — assigne automatiquement la cible à la Cinemachine après spawn
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

    public void AbandonRun()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        MetaProgressionManager.Instance.SaveRunResults(_runTimer, _killCount);
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
        Invoke(nameof(ShowGameOver), 1.5f);
    }

    private void ShowGameOver()
    {
        GameUI.Instance.SetHUDVisible(false);
        int goldEarned = MetaProgressionManager.Instance.RunGold;
        MetaProgressionManager.Instance.SaveRunResults(_runTimer, _killCount);
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
        Invoke(nameof(ShowVictory), 2f);
    }

    private void ShowVictory()
    {
        GameUI.Instance.SetHUDVisible(false);
        int goldEarned = MetaProgressionManager.Instance.RunGold;
        MetaProgressionManager.Instance.SaveRunResults(_runTimer, _killCount);
        GameUI.Instance.ShowVictory(
            _runTimer,
            _killCount,
            goldEarned,
            XPSystem.Instance.CurrentLevel
        );
    }
}