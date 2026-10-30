using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    private const float ModerateEarlyHealthScale = 0.75f;   // PV des ennemis en Modéré au début de partie
    private const float ModerateEaseEndMinutes = 8f;        // retour aux PV normaux (rampe de 6 à 8 min)

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
    private bool _tierScalingActive = false;

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
        _tierScalingActive = GameModes.IsClassic || GameModes.IsEndless || GameModes.IsBossRush;
        _spawnCountMultiplier = _tierScalingActive
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
        // MODIFIÉ (2026-10-24) - + renfort de début/milieu de partie (DifficultyTiers.EarlySpawnBoost, Apocalyptique
        // seulement) pour les phases qui commencent avant 12 min ; la phase finale garde le multiplicateur normal.
        float countMultiplier = _spawnCountMultiplier;
        if (_tierScalingActive && phase.tempsDebutMinutes < DifficultyTiers.EarlySpawnBoostEndMinutes)
            countMultiplier *= DifficultyTiers.EarlySpawnBoost(DifficultyTiers.Current);

        _maxEnemies = Mathf.RoundToInt(phase.maxEnemiesSurMap * countMultiplier);
        _enemiesPerWave = Mathf.Max(1, Mathf.RoundToInt(phase.ennemisParVague * countMultiplier));
        _spawnInterval = phase.intervalleSpawn;

        // AJOUTÉ (2026-10-24, nouvelle phase "05b_La_Reprise" à 10 min, retour utilisateur : "entre 10 et 12 minutes
        // il n'y a pas assez d'ennemis") - avec le renfort Apocalyptique, cette phase (140 x 1,15 x 1,3 = 209)
        // dépasserait la finale (160 x 1,15 = 184). Le renfort ne doit jamais rendre une phase plus chargée que la
        // finale : plafond = valeurs de la dernière phase avec le multiplicateur normal du palier.
        if (_phasesDeJeu.Count > 0)
        {
            SpawnPhase finale = _phasesDeJeu[_phasesDeJeu.Count - 1];
            _maxEnemies = Mathf.Min(_maxEnemies, Mathf.RoundToInt(finale.maxEnemiesSurMap * _spawnCountMultiplier));
            _enemiesPerWave = Mathf.Min(_enemiesPerWave, Mathf.Max(1, Mathf.RoundToInt(finale.ennemisParVague * _spawnCountMultiplier)));
        }
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

        // AJOUTÉ (2026-10-29, retour utilisateur : "mettre moins de pv aux ennemis du début jusqu'à 8 min pour la
        // difficulté modéré") - en Modéré, -25 % de PV jusqu'à 6 min, puis retour progressif à la normale à 8 min (pas
        // de saut brutal). L'XP donnée ne change pas (SetXpHealthMultiplier ci-dessous lit timeBasedMultiplier).
        float earlyEase = DifficultyTiers.Current == DifficultyTier.Moderate
            ? Mathf.Lerp(ModerateEarlyHealthScale, 1f, Mathf.InverseLerp(ModerateEaseEndMinutes - 2f, ModerateEaseEndMinutes, runMinutes))
            : 1f;
        healthMultiplier *= earlyEase;

        GameObject spawned = ObjectPool.Instance.Get(tag, spawnPos, Quaternion.identity, healthMultiplier, palier.bonusHealth * earlyEase);

        if (spawned == null)
        {
            Debug.LogWarning($"[SPAWNER] Tag de pool '{tag}' introuvable — vérifie l'orthographe dans ObjectPool._pools ET dans _paliersEnnemis.");
            return;
        }

        // AJOUTÉ (2026-10-20, retour utilisateur : "trop d'xp en difficulté 3") - l'XP à la mort (EnemyBase.Die)
        // suit la partie TEMPORELLE de ce multiplicateur, jamais le palier de difficulté : plus dur à tuer ne doit
        // pas vouloir dire plus rentable à tuer, sous peine de vider le sens du palier de difficulté.
        spawned.TryGetComponent(out EnemyBase spawnedEnemy);
        if (spawnedEnemy != null) spawnedEnemy.SetXpHealthMultiplier(timeBasedMultiplier);

        // CORRIGÉ (2026-10-21, retour utilisateur : "le corbeau est un oiseau volant donc il doit être en l'air") -
        // FindFreeSpawnPosition ci-dessus place TOUJOURS Y=0 (sol réel, voir son commentaire) - un ennemi volant
        // (_isFlying, ex. le Corbeau) ajoute ici sa propre altitude par-dessus, pour flotter réellement au lieu de
        // rester au sol comme les ennemis terrestres. 0 pour tous les ennemis au sol (aucun effet).
        if (spawnedEnemy != null && spawnedEnemy.IsFlying && spawnedEnemy.FlightAltitude != 0f)
            spawned.transform.position += new Vector3(0f, spawnedEnemy.FlightAltitude, 0f);

        // AJOUTÉ (2026-10-21, retour utilisateur : "je suis niveau 16 alors qu'on est qu'à 5 min [...] si on est
        // déjà à plus de la moitié de ce compte au bout de 5 min, c'est pas normal") - même après avoir retiré le
        // bonus d'XP lié au palier de difficulté (SetXpHealthMultiplier ci-dessus), l'Apocalyptique restait plus
        // rapide à monter de niveau que les autres paliers pour une raison différente et jusqu'ici jamais
        // compensée : ce palier fait aussi spawner ~15% d'ennemis EN PLUS par vague (SpawnScaleMultiplier, voir
        // plus haut dans ce fichier), donc ~15% de morts en plus par minute à DPS égal - plus de kills, même avec
        // une XP par kill désormais identique au palier Modéré, veut quand même dire plus d'XP au total par minute.
        // Multiplicateur DIRECT (pas mélangé à SetXpHealthMultiplier, qui passe par la formule amortie "1+(x-1)*0,5"
        // de EnemyBase.Die() - y injecter cette correction aurait réduit son effet de moitié par erreur) : chaque
        // ennemi rapporte l'inverse exact du surplus de densité, pour que le total gagné par minute reste aligné
        // sur le Modéré malgré le surplus de spawns - "plus difficile" ne doit se traduire ni par plus de PV gagnés
        // (palier) ni par plus d'XP gagnée (densité), seulement par plus d'ennemis/de dégâts à affronter.
        // VOLONTAIREMENT PAS étendu au renfort de début de partie (2026-10-24, DifficultyTiers.EarlySpawnBoost) :
        // l'utilisateur a demandé le 2026-10-22 que les ennemis gardent leur XP et que le rythme se règle par le COÛT
        // des niveaux (XPSystem._baseXP, relevé le même jour) - les ennemis en plus avant 12 min donnent donc plus
        // d'XP au total en Apocalyptique, à surveiller en playtest.
        if (spawnedEnemy != null && _tierScalingActive)
            spawnedEnemy.SetXpPacingMultiplier(1f / Mathf.Max(1f, DifficultyTiers.SpawnScaleMultiplier(DifficultyTiers.Current)));
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
            // CORRIGÉ (2026-10-21, retour utilisateur : "les araignées ne prennent toujours pas les dégâts
            // d'orbitaux, regarde aussi avec les autres compétences, je ne sais pas si c'est la seule") - cette
            // position reprenait TOUTE _playerTransform.position, Y compris : le pivot du joueur est à hauteur de
            // TORSE (~1,5, voir [[player-pivot-is-torso-height-not-ground]]), pas au sol. Tous les ennemis
            // (Araignée, Loup, Bulbe...) apparaissaient donc avec leur Y figé à 1,5 pour toute leur vie (rien dans
            // EnemyBase ne corrige jamais Y après le spawn, voir Move()/ApplyKnockback() qui n'altèrent que X/Z) -
            // vérifié en Play : un Loup venait bien de spawn à Y=1,5. Seul le Bulbe (BoxCollider.size.y=3, centré)
            // s'étend encore jusqu'au sol malgré ce décalage et passait donc inaperçu ; l'Araignée (size.y=1) et le
            // Loup (size.y=2) ont leur collider qui flotte entièrement au-dessus de la zone des effets au sol
            // (Orbitaux/Boue/Nova, tous à _groundY=0,2) - aucun des deux ne peut donc jamais être touché par ces
            // effets, pas seulement l'Araignée. Y forcé à 0 (sol réel) au lieu d'hériter du joueur.
            Vector3 candidatePos = new Vector3(
                _playerTransform.position.x + randomCircle.x * radius,
                0f,
                _playerTransform.position.z + randomCircle.y * radius
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
        // AJOUTÉ (2026-10-27, retour utilisateur : "je veux qu'on doive mettre une attaque de plus pour en tuer un, ça
        // valable pour les ennemis de 0 à 12 min") - PV fixes ajoutés à chaque ennemi du palier APRÈS healthMultiplier
        // et la difficulté (voir EnemyBase._bonusHealth) : environ un tir de base moyen à ce moment de la partie (dégâts
        // mesurés en test long, Concentration à mi-chemin). Un pourcentage ne donnerait pas "un tir de plus" à la fois
        // au loup et au golem. Étendu au palier de 12 min le même jour ("il faut bien que tu augmente les pv aussi de
        // ceux qui sont après 12minutes [...] quand le joueur a 1 ou 2 fusions, le joueur est presque inatteignable") :
        // 400, un tir de base de fin de partie avec la Concentration.
        public float bonusHealth;
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