using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public struct SpawnPhase
    {
        public string nomPhase;
        public float tempsDebutMinutes; // Ex: 0 pour le début, 1 pour la minute 1, etc.
        public int maxEnemiesSurMap;    // Limite max d'ennemis simultanés sur la map
        public int ennemisParVague;     // Nombre d'ennemis créés à chaque tic
        public float intervalleSpawn;   // Cadence d'apparition de la vague (en secondes)
    }

    [Header("Références")]
    [SerializeField] private Transform _playerTransform;

    [Header("Configuration des Phases de Jeu")]
    [SerializeField] private List<SpawnPhase> _phasesDeJeu = new List<SpawnPhase>();

    [Header("Paramètres de Rayon")]
    [SerializeField] private float _spawnRadius = 15f;

    // Variables de contrôle internes synchronisées par les phases
    private float _spawnInterval = 2f;
    private int _enemiesPerWave = 1;
    private int _maxEnemies = 15;

    private float _spawnTimer = 0f;
    private float _gameTimer = 0f;
    private int _currentPhaseIndex = 0;

    private List<Vector3> _recentSpawnPositions = new List<Vector3>();

    private void Start()
    {
        // Sécurité si la référence n'est pas glissée dans l'inspecteur
        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) _playerTransform = player.transform;
        }

        // Initialisation de la première phase si elle existe
        if (_phasesDeJeu.Count > 0)
        {
            AppliquerPhase(_phasesDeJeu[0]);
        }
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        // CORRECTION PAUSE ET FIN DE PARTIE
        if (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused) return;

        _gameTimer += Time.deltaTime;
        _spawnTimer += Time.deltaTime;

        // 1. Gestion du cycle de spawn
        if (_spawnTimer >= _spawnInterval)
        {
            SpawnWave();
            _spawnTimer = 0f;
        }

        // 2. Gestion de la progression de la timeline
        CheckPhaseProgression();
    }

    private void CheckPhaseProgression()
    {
        int nextPhaseIndex = _currentPhaseIndex + 1;
        if (nextPhaseIndex < _phasesDeJeu.Count)
        {
            // Conversion du temps de la phase (minutes) en secondes de jeu
            float tempsRequisSecondes = _phasesDeJeu[nextPhaseIndex].tempsDebutMinutes * 60f;

            if (_gameTimer >= tempsRequisSecondes)
            {
                _currentPhaseIndex = nextPhaseIndex;
                AppliquerPhase(_phasesDeJeu[_currentPhaseIndex]);
            }
        }
    }

    private void AppliquerPhase(SpawnPhase phase)
    {
        _maxEnemies = phase.maxEnemiesSurMap;
        _enemiesPerWave = phase.ennemisParVague;
        _spawnInterval = phase.intervalleSpawn;

        Debug.Log($"📈 [SPAWNER] Nouvelle phase de jeu : {phase.nomPhase} | Limite carte : {_maxEnemies} | Par vague : {_enemiesPerWave} | Cadence : {_spawnInterval}s");
    }

    private void SpawnWave()
    {
        if (_playerTransform == null) return;

        _recentSpawnPositions.Clear();

        // Optimisation : Remplacement du FindGameObjectsWithTag par un décompte des entités actives avec le tag
        int currentEnemies = GameObject.FindGameObjectsWithTag("Enemy").Length;

        if (currentEnemies >= _maxEnemies) return;

        int spaceLeft = _maxEnemies - currentEnemies;
        int spawnCount = Mathf.Min(_enemiesPerWave, spaceLeft);

        for (int i = 0; i < spawnCount; i++)
        {
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

        if (ObjectPool.Instance == null) return;

        // Déploiement via l'ObjectPool optimisé
        ObjectPool.Instance.Get(tag, spawnPos, Quaternion.identity);
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

    private Vector3 FindFreeSpawnPosition()
    {
        float minDistance = 1.5f;
        int maxAttempts = 10;

        // Détection physique filtrée sur la couche des ennemis
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        int layerMask = (enemyLayer != -1) ? (1 << enemyLayer) : Physics.DefaultRaycastLayers;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 randomCircle = Random.insideUnitCircle.normalized;
            Vector3 candidatePos = _playerTransform.position + new Vector3(
                randomCircle.x * _spawnRadius,
                0f,
                randomCircle.y * _spawnRadius
            );
            candidatePos = MapBoundaryUtils.ClampToZone(candidatePos);

            // Ignore les décors et le sol, vérifie uniquement la superposition d'ennemis
            bool isOccupied = Physics.CheckSphere(candidatePos, minDistance, layerMask, QueryTriggerInteraction.Ignore);

            if (!isOccupied)
            {
                bool tooCloseToRecent = false;
                foreach (Vector3 recentPos in _recentSpawnPositions)
                {
                    if (Vector3.SqrMagnitude(candidatePos - recentPos) < minDistance * minDistance)
                    {
                        tooCloseToRecent = true;
                        break;
                    }
                }

                if (!tooCloseToRecent)
                {
                    _recentSpawnPositions.Add(candidatePos);
                    return candidatePos;
                }
            }
        }

        return Vector3.zero;
    }

    // Getters / Setters requis par les autres managers
    public float GetSpawnInterval() => _spawnInterval;
    public void SetSpawnInterval(float value) => _spawnInterval = value;
    public void SetMaxEnemies(int max) => _maxEnemies = max;
}