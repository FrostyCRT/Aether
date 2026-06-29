using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("État du jeu")]
    [SerializeField] private bool _isGameOver = false;
    public bool IsGameOver => _isGameOver;
    public bool IsPaused { get; private set; } = false;

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
    }

    private void Update()
    {
        if (_isGameOver) return;

        // CORRECTION BUG PAUSE/UPGRADE : On bloque complètement Échap si le menu d'upgrade est ouvert
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (LevelUpManager.Instance != null && LevelUpManager.Instance.IsWaitingForChoice) return;
            TogglePause();
        }

        if (IsPaused) return;

        // CORRECTION : Si le LevelUpManager a figé le temps, on considère le jeu comme en pause
        if (LevelUpManager.Instance != null && LevelUpManager.Instance.IsWaitingForChoice) return;

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
        MetaProgressionManager.Instance.SaveRunResults(_runTimer, _killCount);
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

    // CORRECTION : Intégration des mécaniques de fin de combat à la victoire
    public void TriggerVictory()
    {
        if (_isGameOver) return;
        _isGameOver = true;

        // 1. STOPPER LE JOUEUR NET
        GameObject playerGO = GameObject.FindWithTag("Player");
        if (playerGO != null)
        {
            PlayerController playerController = playerGO.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.enabled = false; // Coupe les scripts d'inputs/mouvements/dash
            }

            Rigidbody playerRb = playerGO.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                playerRb.velocity = Vector3.zero; // Coupe toute inertie résiduelle
                playerRb.angularVelocity = Vector3.zero;
                playerRb.isKinematic = true; // Empêche les forces physiques résiduelles
            }
        }

        // 2. NETTOYER TOUS LES PROJECTILES
        if (ObjectPool.Instance != null)
        {
            ObjectPool.Instance.ClearPool("EnemyProjectile");
            ObjectPool.Instance.ClearPool("Projectile");
        }
        else
        {
            // Sécurité par tag si pas de ClearPool implémenté
            GameObject[] enemyProjectiles = GameObject.FindGameObjectsWithTag("EnemyProjectile");
            foreach (GameObject proj in enemyProjectiles) Destroy(proj);

            GameObject[] playerProjectiles = GameObject.FindGameObjectsWithTag("Projectile");
            foreach (GameObject proj in playerProjectiles) Destroy(proj);
        }

        Invoke(nameof(ShowVictory), 2f);
    }

    private void ShowVictory()
    {
        GameUI.Instance.SetHUDVisible(false);
        MetaProgressionManager.Instance.SaveRunResults(_runTimer, _killCount);
        GameUI.Instance.ShowVictory(
            _runTimer,
            _killCount,
            MetaProgressionManager.Instance.RunGold,
            XPSystem.Instance.CurrentLevel
        );
    }
}