using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("État du jeu")]
    [SerializeField] private bool _isGameOver = false;
    public bool IsGameOver => _isGameOver;

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
        _runTimer += Time.deltaTime;
    }

    public void AddKill()
    {
        _killCount++;
    }

    public void TriggerGameOver()
    {
        if (_isGameOver) return;

        _isGameOver = true;

        // On affiche le Game Over après 1.5 secondes
        Invoke(nameof(ShowGameOver), 1.5f);
    }

    private void ShowGameOver()
    {
        // On sauvegarde les résultats
        MetaProgressionManager.Instance.SaveRunResults(
            _runTimer,
            _killCount,
            WaveManager.Instance.CurrentWave
        );

        GameUI.Instance.ShowGameOver(
            _runTimer,
            _killCount,
            WaveManager.Instance.CurrentWave,
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
        SceneManager.LoadScene(0); // 0 = MainMenu dans Build Settings
    }

    public void TriggerVictory()
    {
        if (_isGameOver) return;
        _isGameOver = true;
        Debug.Log("VICTOIRE !");
        // On affichera l'écran de victoire plus tard
        Invoke(nameof(ShowGameOver), 1.5f);
    }
}