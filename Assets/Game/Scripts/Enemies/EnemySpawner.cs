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

    // AJOUTÉ (2026-10-20, retour utilisateur : "quand il y a plein de tireur, notamment les bulbe [...] c'est
    // impossible d'esquiver et c'est mort instantanément") - au-delà d'un certain nombre de tireurs (EnemyShooter/
    // EnemyBulb, voir EnemyBase.IsRangedAttacker) vivants EN MÊME TEMPS, plus aucune direction n'est sûre pour
    // esquiver, quels que soient les PV/dégâts de CHACUN pris séparément - le vrai problème est la DENSITÉ de
    // tireurs, pas un seul chiffre à retoucher. Un tirage qui retomberait sur un tireur une fois le plafond atteint
    // est redessiné en excluant les tireurs (toujours au moins un type de mêlée dans chaque palier, voir
    // _paliersEnnemis) - ne bloque jamais un spawn, redistribue juste son poids vers la mêlée ce tour-ci.
    [Header("Plafond de tireurs simultanés (EnemyShooter + EnemyBulb, voir EnemyBase.ActiveRangedCount)")]
    [SerializeField] private int _maxSimultaneousRanged = 6;

    // Variables de contrôle internes synchronisées par les phases
    private float _spawnInterval = 2f;
    private int _enemiesPerWave = 1;
    private int _maxEnemies = 15;

    // AJOUTÉ (2026-10-18, diagnostic complet - système de difficulté) - lu une seule fois dans Start() : une carte
    // agrandie (DifficultyTiers.ZoneHalfSize, voir MapBoundaryUtils) a plus de place, donc plus d'ennemis doivent
    // pouvoir y tenir en même temps pour rester aussi dense qu'au palier Modéré. 1 si hors Classique ou palier Modéré.
    private float _spawnCountMultiplier = 1f;

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

        // MODIFIÉ (2026-10-19, diagnostic complet - refonte de la forme de carte) - l'ancien ratio (rayon de spawn
        // proportionnel à une unique ZoneHalfSize) n'a plus de sens pour un tunnel (X infini, Z étroit) ou une
        // boîte resserrée (Apocalyptique, plus petite que l'ancien Modéré) : le rayon de spawn garde sa valeur
        // d'Inspector pour tous les paliers, et MapBoundaryUtils.ClampToZone() (déjà appelé dans
        // FindFreeSpawnPosition ci-dessous) confine naturellement chaque candidat à la forme réelle de la carte -
        // un ennemi qui "déborderait" du tunnel ou de la boîte est simplement ramené sur le mur le plus proche.
        // ÉTENDU (2026-10-20, carrousel de difficulté) - s'applique maintenant aussi en Sans fin et Ruée de boss.
        _spawnCountMultiplier = (GameModes.IsClassic || GameModes.IsEndless || GameModes.IsBossRush)
            ? DifficultyTiers.SpawnScaleMultiplier(DifficultyTiers.Current) : 1f;

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

    // MODIFIÉ (2026-10-16, audit version finale) - les phases sont désormais la SEULE source de la difficulté de spawn
    // (WaveManager.ApplyDifficulty les écrasait chaque frame avec ses propres valeurs : max 15->60, si bien que les
    // max 75/120/160 et les cadences de ces phases ne s'appliquaient jamais). Progression calée sur
    // GameManager.RunTimer (le même chronomètre que les paliers d'ennemis et les boss, figé pendant les boss) plutôt
    // qu'un compteur local ; `while` pour rattraper plusieurs phases d'un coup si besoin.
    private void CheckPhaseProgression()
    {
        float runSeconds = GameManager.Instance != null ? GameManager.Instance.RunTimer : _gameTimer;
        while (_currentPhaseIndex + 1 < _phasesDeJeu.Count
               && runSeconds >= _phasesDeJeu[_currentPhaseIndex + 1].tempsDebutMinutes * 60f)
        {
            _currentPhaseIndex++;
            AppliquerPhase(_phasesDeJeu[_currentPhaseIndex]);
        }
    }

    private void AppliquerPhase(SpawnPhase phase)
    {
        // MODIFIÉ (2026-10-18, diagnostic complet - système de difficulté) - plafond et taille de vague mis à
        // l'échelle du palier de difficulté (_spawnCountMultiplier, voir Start()) ; la cadence (intervalleSpawn)
        // reste inchangée pour ne pas cumuler 3 leviers de difficulté à la fois sur un système jamais playtesté.
        _maxEnemies = Mathf.RoundToInt(phase.maxEnemiesSurMap * _spawnCountMultiplier);
        _enemiesPerWave = Mathf.Max(1, Mathf.RoundToInt(phase.ennemisParVague * _spawnCountMultiplier));
        _spawnInterval = phase.intervalleSpawn;
    }

    private void SpawnWave()
    {
        if (_playerTransform == null) return;

        _recentSpawnPositions.Clear();

        // MODIFIÉ (2026-10-16) - compteur de la grille spatiale (ennemis actifs) au lieu de FindGameObjectsWithTag,
        // qui allouait un tableau à chaque vague (jusqu'à ~2 fois par seconde avec 160 ennemis).
        int currentEnemies = EnemySpatialGrid.Count;

        if (currentEnemies >= _maxEnemies) return;

        int spaceLeft = _maxEnemies - currentEnemies;
        int spawnCount = Mathf.Min(_enemiesPerWave, spaceLeft);

        for (int i = 0; i < spawnCount; i++)
        {
            SpawnEnemy();
        }
    }

    private static bool IsRangedTag(string tag) => tag == "EnemyShooter" || tag == "EnemyBulb";

    private void SpawnEnemy()
    {
        PalierEnnemis palier = GetCurrentPalier();
        string tag = PickWeightedTag(palier.composition);

        if (IsRangedTag(tag) && EnemyBase.ActiveRangedCount >= _maxSimultaneousRanged)
        {
            string meleeTag = PickWeightedTag(palier.composition, excludeRanged: true);
            // Palier 100% tireurs (ne devrait jamais arriver, voir commentaire sur _maxSimultaneousRanged) :
            // on saute ce spawn plutôt que de forcer un tireur au-delà du plafond.
            if (string.IsNullOrEmpty(meleeTag)) return;
            tag = meleeTag;
        }

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
        // AJOUTÉ (2026-10-18, diagnostic complet - système de difficulté) - palier de difficulté, x1 au palier
        // Modéré. ÉTENDU (2026-10-20, carrousel de difficulté) - s'applique maintenant aussi en Sans fin et Ruée
        // de boss, plus seulement le Classique.
        float difficultyMultiplier = (GameModes.IsClassic || GameModes.IsEndless || GameModes.IsBossRush)
            ? DifficultyTiers.HealthMultiplier(DifficultyTiers.Current) : 1f;
        float timeBasedMultiplier = palier.healthMultiplier * GameModes.EndlessHealthScale(runMinutes);
        float healthMultiplier = timeBasedMultiplier * difficultyMultiplier;

        GameObject spawned = ObjectPool.Instance.Get(tag, spawnPos, Quaternion.identity, healthMultiplier);

        if (spawned == null)
        {
            Debug.LogWarning($"[SPAWNER] Tag de pool '{tag}' introuvable — vérifie l'orthographe dans ObjectPool._pools ET dans _paliersEnnemis.");
            return;
        }

        // AJOUTÉ (2026-10-20, retour utilisateur : "trop d'xp en difficulté 3") - l'XP à la mort (EnemyBase.Die)
        // suit la partie TEMPORELLE de ce multiplicateur, jamais le palier de difficulté : plus dur à tuer ne doit
        // pas vouloir dire plus rentable à tuer, sous peine de vider le sens du palier de difficulté.
        EnemyBase spawnedEnemy = spawned.GetComponent<EnemyBase>();
        if (spawnedEnemy != null) spawnedEnemy.SetXpHealthMultiplier(timeBasedMultiplier);
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

    private string PickWeightedTag(List<WeightedEnemyEntry> composition, bool excludeRanged = false)
    {
        if (composition == null || composition.Count == 0) return null;

        float total = 0f;
        for (int i = 0; i < composition.Count; i++)
        {
            if (excludeRanged && IsRangedTag(composition[i].poolTag)) continue;
            total += composition[i].poids;
        }

        if (total <= 0f) return null;

        float roll = Random.Range(0f, total);
        float cumulative = 0f;
        string lastValidTag = null;

        for (int i = 0; i < composition.Count; i++)
        {
            if (excludeRanged && IsRangedTag(composition[i].poolTag)) continue;
            lastValidTag = composition[i].poolTag;
            cumulative += composition[i].poids;
            if (roll <= cumulative)
                return composition[i].poolTag;
        }

        return lastValidTag; // filet de sécurité, arrondi flottant
    }

    
}