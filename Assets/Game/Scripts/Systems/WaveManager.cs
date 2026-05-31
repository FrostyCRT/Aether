using UnityEngine;

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Paramètres")]
    public float BossSpawnInterval = 300f; // 5 minutes en secondes

    [Header("Boss")]
    [SerializeField] private GameObject _bossPrefab1;
    [SerializeField] private GameObject _bossPrefab2;
    [SerializeField] private GameObject _bossPrefab3;

    [Header("Limite ennemis")]
    [SerializeField] private int _maxEnemiesOnScreen = 15;

    private float _runTimer = 0f;
    private int _bossCount = 0;
    private bool _bossAlive = false;

    public int CurrentWave => _bossCount + 1;
    public float RunTimer => _runTimer;

    private EnemySpawner _enemySpawner;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        _enemySpawner = FindObjectOfType<EnemySpawner>();
        ApplyDifficulty();
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        if (_bossAlive) return;

        _runTimer += Time.deltaTime;

        // Mise à jour de la difficulté
        ApplyDifficulty();

        // Spawn des boss
        if (_bossCount == 0 && _runTimer >= 300f) SpawnBoss(1);
        if (_bossCount == 1 && _runTimer >= 600f) SpawnBoss(2);
        if (_bossCount == 2 && _runTimer >= 900f) SpawnBoss(3);

        GameUI.Instance.UpdateTimer(_runTimer);
    }

    private void ApplyDifficulty()
    {
        if (_enemySpawner == null) return;

        float minutes = _runTimer / 60f;

        if (minutes < 3f)
        {
            _enemySpawner.SetSpawnInterval(3f);
            _maxEnemiesOnScreen = 15;
        }
        else if (minutes < 5f)
        {
            _enemySpawner.SetSpawnInterval(2f);
            _maxEnemiesOnScreen = 25;
        }
        else if (minutes < 8f)
        {
            _enemySpawner.SetSpawnInterval(1.5f);
            _maxEnemiesOnScreen = 30;
        }
        else if (minutes < 10f)
        {
            _enemySpawner.SetSpawnInterval(1f);
            _maxEnemiesOnScreen = 40;
        }
        else if (minutes < 13f)
        {
            _enemySpawner.SetSpawnInterval(0.8f);
            _maxEnemiesOnScreen = 50;
        }
        else
        {
            _enemySpawner.SetSpawnInterval(0.6f);
            _maxEnemiesOnScreen = 60;
        }

        _enemySpawner.SetMaxEnemies(_maxEnemiesOnScreen);
    }

    private void SpawnBoss(int bossNumber)
    {
        _bossCount++;
        _bossAlive = true;

        ClearAllEnemies();
        _enemySpawner.gameObject.SetActive(false);

        GameObject player = GameObject.FindWithTag("Player");
        Vector3 spawnPos = player.transform.position + new Vector3(10f, 0f, 0f);

        GameObject bossPrefab = bossNumber == 1 ? _bossPrefab1 :
                                bossNumber == 2 ? _bossPrefab2 : _bossPrefab3;

        if (bossPrefab != null)
            Instantiate(bossPrefab, spawnPos, Quaternion.identity);
        else
            Debug.LogWarning($"Boss {bossNumber} prefab non assigné !");

        GameUI.Instance.UpdateWave(-1);
        Debug.Log($"Boss {bossNumber} spawné !");
    }

    private void ClearAllEnemies()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject enemy in enemies)
        {
            EnemyBase eb = enemy.GetComponent<EnemyBase>();
            if (eb != null)
                ObjectPool.Instance.ReturnToPool(GetPoolTag(enemy), enemy);
            else
                Destroy(enemy);
        }
    }

    private string GetPoolTag(GameObject enemy)
    {
        if (enemy.GetComponent<EnemyShooter>() != null) return "EnemyShooter";
        if (enemy.GetComponent<EnemyTank>() != null) return "EnemyTank";
        return "Enemy";
    }

    public void OnBossDied()
    {
        _bossAlive = false;
        _enemySpawner.gameObject.SetActive(true);
        GameUI.Instance.UpdateWave(CurrentWave);

        // Victoire si c'était le boss 3
        if (_bossCount >= 3)
            GameManager.Instance.TriggerVictory();

        Debug.Log($"Boss vaincu ! Run continue — Vague {CurrentWave}");
    }
}