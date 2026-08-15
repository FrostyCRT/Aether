using UnityEngine;

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Paramètres")]
    public float BossSpawnInterval = 300f;

    [Header("Boss")]
    [SerializeField] private GameObject _bossPrefab1;
    [SerializeField] private GameObject _bossPrefab2;
    [SerializeField] private GameObject _bossPrefab3;

    [Header("Limite ennemis")]
    [SerializeField] private int _maxEnemiesOnScreen = 15;

    private int  _bossCount = 0;
    private bool _bossAlive = false;
    public bool BossAlive => _bossAlive;
    public int CurrentWave => _bossCount + 1;

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
        if (GameManager.Instance.IsPaused) return;
        if (_bossAlive) return;

        // On utilise le timer de GameManager — un seul timer pour tout
        float runTimer = GameManager.Instance.RunTimer;

        ApplyDifficulty();

        if (_bossCount == 0 && runTimer >= BossSpawnInterval) SpawnBoss(1);
        if (_bossCount == 1 && runTimer >= BossSpawnInterval * 2f) SpawnBoss(2);
        if (_bossCount == 2 && runTimer >= BossSpawnInterval * 3f) SpawnBoss(3);

        GameUI.Instance.UpdateTimer(runTimer); // ← même timer partout
    }

    private void ApplyDifficulty()
    {
        if (_enemySpawner == null) return;

        float minutes = GameManager.Instance.RunTimer / 60f;

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

        GameObject player    = GameObject.FindWithTag("Player");
        Vector3    spawnPos  = player.transform.position + new Vector3(10f, 0f, 0f);
        spawnPos = MapBoundaryUtils.ClampToZone(spawnPos);

        GameObject bossPrefab = bossNumber == 1 ? _bossPrefab1 :
                                bossNumber == 2 ? _bossPrefab2 : _bossPrefab3;

        if (bossPrefab != null)
        {
            GameObject bossInstance = Instantiate(bossPrefab, spawnPos, Quaternion.identity); // MODIFIÉ — on garde la référence

            // AJOUTÉ — applique le zoom propre à CE boss, lu directement sur son prefab
            BossBase bossScript = bossInstance.GetComponent<BossBase>();
            if (bossScript != null && BossCameraZoom.Instance != null)
                BossCameraZoom.Instance.SetBossZoom(bossScript.CameraZoomMargin); // MODIFIÉ // MODIFIÉ — SetBossZoom() n'existe plus, remplacé par SetBossOffset()
        }
        else
            Debug.LogWarning($"Boss {bossNumber} prefab non assigné !");

        Debug.Log($"Boss {bossNumber} spawné !");
    }

    public static class MapBoundaryUtils
    {
        public const float ZoneHalfSize = 55f;

        public static Vector3 ClampToZone(Vector3 position)
        {
            position.x = Mathf.Clamp(position.x, -ZoneHalfSize, ZoneHalfSize);
            position.z = Mathf.Clamp(position.z, -ZoneHalfSize, ZoneHalfSize);
            return position;
        }
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
        EnemyBase eb = enemy.GetComponent<EnemyBase>();
        return eb != null ? eb.PoolTag : "Enemy"; // MODIFIÉ — lit le tag configuré sur le prefab au lieu de deviner via une chaîne de GetComponent<X>()
    }

    public void OnBossDied()
    {
        _bossAlive = false;
        _enemySpawner.gameObject.SetActive(true);

        if (BossCameraZoom.Instance != null) BossCameraZoom.Instance.ResetZoom(); 

        if (_bossCount >= 3)
            GameManager.Instance.TriggerVictory();

        Debug.Log($"Boss vaincu ! Run continue — Vague {CurrentWave}");
    }
}