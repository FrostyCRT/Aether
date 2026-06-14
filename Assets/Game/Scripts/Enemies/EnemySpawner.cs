using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private GameObject _enemyPrefab;
    [SerializeField] private Transform _playerTransform;

    [Header("Paramètres de spawn")]
    [SerializeField] private float _spawnInterval = 2f;
    [SerializeField] private float _spawnRadius = 15f; // Augmenté de 10 à 15
    [SerializeField] private int _enemiesPerWave = 1;

    [Header("Difficulté croissante")]
    [SerializeField] private float _difficultyInterval = 10f;
    [SerializeField] private int _enemiesIncrement = 1;

    private float _spawnTimer = 0f;
    private float _difficultyTimer = 0f;
    private int _maxEnemies = 15;

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

        _spawnTimer += Time.deltaTime;
        if (_spawnTimer >= _spawnInterval)
        {
            SpawnWave();
            _spawnTimer = 0f;
        }

        _difficultyTimer += Time.deltaTime;
        if (_difficultyTimer >= _difficultyInterval)
        {
            _enemiesPerWave += _enemiesIncrement;
            _difficultyTimer = 0f;
        }
    }

    private List<Vector3> _recentSpawnPositions = new List<Vector3>();

    private void SpawnWave()
    {
        _recentSpawnPositions.Clear(); // Reset à chaque vague

        int currentEnemies = GameObject.FindGameObjectsWithTag("Enemy").Length;
        if (currentEnemies >= _maxEnemies) return;

        for (int i = 0; i < _enemiesPerWave; i++)
        {
            currentEnemies = GameObject.FindGameObjectsWithTag("Enemy").Length;
            if (currentEnemies >= _maxEnemies) break;
            SpawnEnemy();
        }
    }

    private void SpawnEnemy()
    {
        float roll = Random.value;
        string tag;
        if (roll < 0.15f) tag = "EnemyTank";
        else if (roll < 0.35f) tag = "EnemyShooter";
        else tag = "Enemy";

        Vector3 spawnPos = FindFreeSpawnPosition();
        if (spawnPos == Vector3.zero) return;

        _recentSpawnPositions.Add(spawnPos); // Enregistre la position
        ObjectPool.Instance.Get(tag, spawnPos, Quaternion.identity);
    }

    private Vector3 FindFreeSpawnPosition()
    {
        float minDistance = 1.5f;
        int maxAttempts = 10;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 randomCircle = Random.insideUnitCircle.normalized;
            Vector3 candidatePos = _playerTransform.position + new Vector3(
                randomCircle.x * _spawnRadius, 0f,
                randomCircle.y * _spawnRadius);

            // Vérifie contre la physique (ennemis déjà là)
            Collider[] nearby = Physics.OverlapSphere(candidatePos, minDistance);
            bool isFree = true;
            foreach (Collider col in nearby)
            {
                if (col.CompareTag("Enemy")) { isFree = false; break; }
            }

            // Vérifie aussi contre les spawns de cette même vague
            foreach (Vector3 recentPos in _recentSpawnPositions)
            {
                if (Vector3.Distance(candidatePos, recentPos) < minDistance)
                {
                    isFree = false;
                    break;
                }
            }

            if (isFree) return candidatePos;
        }

        return Vector3.zero;
    }

    public float GetSpawnInterval() => _spawnInterval;
    public void SetSpawnInterval(float value) => _spawnInterval = value;
    public void SetMaxEnemies(int max) => _maxEnemies = max;
}