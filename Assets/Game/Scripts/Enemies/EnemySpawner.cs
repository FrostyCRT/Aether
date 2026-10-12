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

    // AJOUTE (2026-10-07, retour utilisateur : "il faut que les bulbes spawn plus proche du player, parce que vu
    // qu'ils bougent pas, si on reste là où on est on ne les voit jamais" + "il faut aussi faire en sorte qu'ils ne
    // spawn pas au même endroit, donc pas collé") - le Bulbe est un tireur FIXE (ne poursuit jamais le joueur,
    // contrairement aux autres ennemis) : au _spawnRadius générique (bien au-delà de la vue), il reste invisible
    // tant qu'on ne marche pas explicitement dessus. Rayon dédié, plus proche, + distance minimale dédiée (plus
    // grande que les 1,5 génériques) pour qu'ils ne s'empilent jamais les uns sur les autres.
    [Header("Paramètres de Rayon — Bulbe (tireur fixe, voir EnemyBulb)")]
    [SerializeField] private float _bulbSpawnRadius = 10f;
    [SerializeField] private float _bulbMinSeparation = 4f;

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
        PalierEnnemis palier = GetCurrentPalier();
        string tag = PickWeightedTag(palier.composition);

        if (string.IsNullOrEmpty(tag))
        {
            Debug.LogWarning($"[SPAWNER] Aucun ennemi pondéré valide pour le palier '{palier.nom}' — vérifie la liste _paliersEnnemis dans l'inspector.");
            return;
        }

        bool isBulb = tag == "EnemyBulb";
        Vector3 spawnPos = isBulb
            ? FindFreeSpawnPosition(_bulbSpawnRadius, _bulbMinSeparation)
            : FindFreeSpawnPosition(_spawnRadius, 1.5f);
        if (spawnPos == Vector3.zero) return;
        if (ObjectPool.Instance == null) return;

        // Mode sans fin : les PV continuent de croître au-delà de 15 min (x1 en classique, voir GameModes).
        float runMinutes = GameManager.Instance != null ? GameManager.Instance.RunTimer / 60f : 0f;
        float healthMultiplier = palier.healthMultiplier * GameModes.EndlessHealthScale(runMinutes);

        GameObject spawned = ObjectPool.Instance.Get(tag, spawnPos, Quaternion.identity, healthMultiplier);

        if (spawned == null)
            Debug.LogWarning($"[SPAWNER] Tag de pool '{tag}' introuvable — vérifie l'orthographe dans ObjectPool._pools ET dans _paliersEnnemis.");
    }


    private Vector3 FindFreeSpawnPosition(float radius, float minDistance)
    {
        int maxAttempts = 10;

        // Détection physique filtrée sur la couche des ennemis
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        int layerMask = (enemyLayer != -1) ? (1 << enemyLayer) : Physics.DefaultRaycastLayers;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 randomCircle = Random.insideUnitCircle.normalized;
            Vector3 candidatePos = _playerTransform.position + new Vector3(
                randomCircle.x * radius,
                0f,
                randomCircle.y * radius
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

    [System.Serializable]
    public struct WeightedEnemyEntry
    {
        public string poolTag;   // doit correspondre exactement à un tag de ObjectPool._pools
        public float poids;      // poids relatif, pas besoin de totaliser 100 exactement
    }

    [System.Serializable]
    public struct PalierEnnemis
    {
        public string nom;
        public float debutMinutes;
        public List<WeightedEnemyEntry> composition;
        public float healthMultiplier; // scaling PV pour ce palier — valeurs à ajuster en playtest
    }

    // MODIFIE (2026-10-07, retour utilisateur : "je voulais qu'on fasse en sorte qu'on ait pas tous les ennemis en
    // même temps peu importe le temps où on est... max 2 ou 3 ennemis différents par palier, quand on atteint le
    // palier suivant... le pool change, les types qu'il y avait change, et laisse place à 2 ou 3 autres types") -
    // les paliers (réglés dans l'Inspector de ce composant, voir le GameObject EnemySpawner de la scène Game)
    // étaient ACCUMULATIFS : chaque palier ajoutait un type sans jamais en retirer, jusqu'à avoir les 6 types en
    // même temps en fin de partie. Chaque palier liste maintenant SEULEMENT ses 2-3 types actifs (remplace le
    // précédent plutôt que de s'y ajouter), et contient TOUJOURS EnemyShooter (Goblin) et/ou EnemyBulb (Bulbe) -
    // les 2 seuls ennemis qui tirent des projectiles, nécessaires pour charger le Bouclier de Mana/l'Ultime (voir
    // EnemyProjectile.OnTriggerEnter, ManaShield.TryAbsorb/CrystalSystem.AbsorbProjectile) - sans ça un palier
    // 100% mêlée (Loup/Golem/Corbeau/Araignée) rendrait ces mécaniques injouables.
    // CORRIGÉ (2026-10-11, retour utilisateur : "le pourcentage d'apparition des tireurs... en fin de partie ça
    // devient difficile car il y en a beaucoup, donc c'est presque impossible de tout esquiver") - le ratio tireur
    // seul (30 %) ne montait pas en fin de partie, mais le PLAFOND d'ennemis simultanés, lui, grimpe fortement avec
    // les phases de _phasesDeJeu (jusqu'à 160 en fin de partie, "06_L_Apocalypse_Finale") : à ratio constant, le
    // NOMBRE de tireurs à l'écran explose quand même. Poids des tireurs divisés par 2 dans les 2 derniers paliers
    // (Tisseuse : EnemyShooter 30 -> 15 ; Brute : EnemyBulb 30 -> 15), réglé directement dans l'Inspector de ce
    // composant (scène Game) - jamais à 0, pour ne pas casser le Bouclier de Mana/l'Ultime (voir commentaire
    // ci-dessus).
    [Header("Pool pondéré par palier de temps (2-3 types actifs max, voir commentaire ci-dessus)")]
    [SerializeField] private List<PalierEnnemis> _paliersEnnemis = new List<PalierEnnemis>();

    private PalierEnnemis GetCurrentPalier()
    {
        float minutes = GameManager.Instance != null ? GameManager.Instance.RunTimer / 60f : 0f;

        PalierEnnemis current = _paliersEnnemis.Count > 0 ? _paliersEnnemis[0] : default;
        for (int i = 0; i < _paliersEnnemis.Count; i++)
        {
            if (minutes >= _paliersEnnemis[i].debutMinutes)
                current = _paliersEnnemis[i];
        }
        return current;
    }

    private string PickWeightedTag(List<WeightedEnemyEntry> composition)
    {
        if (composition == null || composition.Count == 0) return null;

        float total = 0f;
        for (int i = 0; i < composition.Count; i++)
            total += composition[i].poids;

        if (total <= 0f) return null;

        float roll = Random.Range(0f, total);
        float cumulative = 0f;

        for (int i = 0; i < composition.Count; i++)
        {
            cumulative += composition[i].poids;
            if (roll <= cumulative)
                return composition[i].poolTag;
        }

        return composition[composition.Count - 1].poolTag; // filet de sécurité, arrondi flottant
    }

    
}