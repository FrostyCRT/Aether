using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private GameObject _enemyPrefab;    // Le prefab ennemi
    [SerializeField] private Transform  _playerTransform; // Le joueur

    [Header("Paramètres de spawn")]
    [SerializeField] private float _spawnInterval    = 2f;  // Secondes entre chaque spawn
    [SerializeField] private float _spawnRadius      = 10f; // Distance du joueur
    [SerializeField] private int   _enemiesPerWave   = 1;   // Ennemis par vague au départ

    [Header("Difficulté croissante")]
    [SerializeField] private float _difficultyInterval = 10f; // Toutes les X secondes
    [SerializeField] private int   _enemiesIncrement   = 1;   // +X ennemis par palier

    [Header("Types d'ennemis")]
    [SerializeField] private string[] _enemyTags = { "Enemy", "EnemyTank" };
 

    private float _spawnTimer      = 0f;
    private float _difficultyTimer = 0f;

   private void Update()
   {
       // On arrête tout si le jeu est terminé
       if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

        // Timer de spawn
        _spawnTimer += Time.deltaTime;
        if (_spawnTimer >= _spawnInterval)
        {
            SpawnWave();
            _spawnTimer = 0f;
        }

        // Timer de difficulté
        _difficultyTimer += Time.deltaTime;
        if (_difficultyTimer >= _difficultyInterval)
        {
            _enemiesPerWave += _enemiesIncrement;
            _difficultyTimer = 0f;
            Debug.Log($"Difficulté augmentée ! Ennemis par vague : {_enemiesPerWave}");
        }
    }

    private void SpawnWave()
    {
        for (int i = 0; i < _enemiesPerWave; i++)
        {
            SpawnEnemy();
        }
    }

   private void SpawnEnemy()
   {
        Vector2 randomCircle = Random.insideUnitCircle.normalized;
        Vector3 spawnPos = _playerTransform.position + new Vector3(
            randomCircle.x * _spawnRadius,
            0f,
            randomCircle.y * _spawnRadius
        );

        float roll = Random.value;

        string tag;
        if (roll < 0.2f)
            tag = "EnemyTank";
        else if (roll < 0.4f)
            tag = "EnemyShooter";
        else
            tag = "Enemy";

        ObjectPool.Instance.Get(tag, spawnPos, Quaternion.identity);
   }

   public float GetSpawnInterval() => _spawnInterval;
   public void  SetSpawnInterval(float value) => _spawnInterval = value;

}