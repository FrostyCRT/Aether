using UnityEngine;

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Vagues")]
    [SerializeField] private float _waveDuration            = 45f;
    [SerializeField] private int   _maxWave                 = 10;
    [SerializeField] private float _spawnIntervalMin        = 0.5f;
    [SerializeField] private float _spawnIntervalDecrement  = 0.08f;

    [Header("Boss")]
    public  float          BossSpawnInterval = 30f; // Public pour tester facilement
    [SerializeField] private GameObject _bossPrefab;

    private float _waveTimer  = 0f;
    private float _runTimer   = 0f;
    private float _bossTimer  = 0f;
    private int   _currentWave = 1;
    private bool  _bossAlive   = false;

    public int   CurrentWave => _currentWave;
    public float RunTimer    => _runTimer;

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
        UpdateDifficulty();
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        if (_bossAlive) return; // On pause les vagues pendant le boss

        _runTimer  += Time.deltaTime;
        _waveTimer += Time.deltaTime;
        _bossTimer += Time.deltaTime;

        // Nouvelle vague
        if (_waveTimer >= _waveDuration && _currentWave < _maxWave)
        {
            _waveTimer = 0f;
            _currentWave++;
            UpdateDifficulty();
            GameUI.Instance.UpdateWave(_currentWave);
            Debug.Log($"Vague {_currentWave} !");
        }

        // Spawn du boss
        if (_bossTimer >= BossSpawnInterval)
        {
            _bossTimer = 0f;
            SpawnBoss();
        }

        GameUI.Instance.UpdateTimer(_runTimer);
    }

    private void SpawnBoss()
    {
        if (_bossPrefab == null)
        {
            Debug.LogWarning("Boss Prefab non assigné !");
            return;
        }

        // On tue tous les ennemis sans donner d'XP
        ClearAllEnemies();

        // On arrête le spawner pendant le combat
        _enemySpawner.gameObject.SetActive(false);
        _bossAlive = true;

        // On spawn le boss à distance du joueur
        GameObject player = GameObject.FindWithTag("Player");
        Vector3 spawnPos  = player.transform.position + new Vector3(10f, 0f, 0f);

        Instantiate(_bossPrefab, spawnPos, Quaternion.identity);

        GameUI.Instance.UpdateWave(-1); // -1 = affiche "BOSS !"
        Debug.Log("BOSS SPAWNÉ !");
    }

    private void ClearAllEnemies()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject enemy in enemies)
        {
            // On remet dans le pool sans donner d'XP
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
        if (enemy.GetComponent<EnemyTank>()    != null) return "EnemyTank";
        return "Enemy";
    }

    public void OnBossDied()
    {
        _bossAlive = false;
        _bossTimer = 0f;

        // On relance le spawner
        _enemySpawner.gameObject.SetActive(true);
        GameUI.Instance.UpdateWave(_currentWave);
        Debug.Log("Boss vaincu ! Les vagues reprennent !");
    }

    private void UpdateDifficulty()
    {
        if (_enemySpawner == null) return;

        float newInterval = Mathf.Max(
            _spawnIntervalMin,
            _enemySpawner.GetSpawnInterval() - _spawnIntervalDecrement * (_currentWave - 1)
        );

        _enemySpawner.SetSpawnInterval(newInterval);
    }
}