using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("État du jeu")]
    [SerializeField] private bool _isGameOver = false;
    public bool IsGameOver => _isGameOver;
    public bool IsPaused   { get; private set; } = false;

    private float _runTimer  = 0f;
    private int   _killCount = 0;
    public int   KillCount => _killCount;
    public float RunTimer  => _runTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
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
        if (WaveManager.Instance != null && WaveManager.Instance.BossAlive) return; // ← pause pendant les boss

        _runTimer += Time.deltaTime;
    }

    public void TogglePause()
    {
        IsPaused       = !IsPaused;
        Time.timeScale = IsPaused ? 0f : 1f;
        GameUI.Instance.ShowPausePanel(IsPaused);
    }

    public void ResumePause()
    {
        IsPaused       = false;
        Time.timeScale = 1f;
        GameUI.Instance.ShowPausePanel(false);
    }

    public void AbandonRun()
    {
        IsPaused       = false;
        Time.timeScale = 1f;
        MetaProgressionManager.Instance.SaveRunResults(
            _runTimer,
            _killCount
        );
        SceneManager.LoadScene(0);
    }

    public void AddKill()
    {
        _killCount++;
        if (GameUI.Instance != null)
            GameUI.Instance.UpdateKillCount(_killCount); // ← mise à jour HUD
    }

    public void TriggerGameOver()
    {
        if (_isGameOver) return;
        _isGameOver = true;
        Invoke(nameof(ShowGameOver), 1.5f);
    }

    private void ShowGameOver()
    {
        MetaProgressionManager.Instance.SaveRunResults(
            _runTimer,
            _killCount
        );
        GameUI.Instance.ShowGameOver(
            _runTimer,
            _killCount,
            MetaProgressionManager.Instance.RunGold
        );
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
        Invoke(nameof(ShowGameOver), 1.5f);
    }
}