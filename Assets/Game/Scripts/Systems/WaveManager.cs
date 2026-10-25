using System.Collections;
using System.Collections.Generic;
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
        GameModes.ResetScaling();
        _enemySpawner = FindFirstObjectByType<EnemySpawner>();

        // Ruée de boss : nombre de boss à battre = 3 par boss déjà vaincu en classique (3, 6 ou 9). Filet de sécurité :
        // si le mode est sélectionné sans progression (save modifié...), on retombe sur le Classique.
        SaveData data = MetaProgressionManager.Instance != null ? MetaProgressionManager.Instance.Data : null;
        GameModes.EnsureCurrentIsAvailable(data);
        _rushBossTotal = GameModes.RushBossCount(data);

        ApplyDifficulty();
        UpdateModeHud(0f);

        if (GameModes.IsTitans)
        {
            if (GameUI.Instance != null) GameUI.Instance.HideXpHud();
            StartCoroutine(TitansStart());
        }
    }

    // ---- Choc des titans ------------------------------------------------------------------------------------------------
    // Build complet au départ, aucun ennemi normal, les 3 boss apparaissent ensemble (Boss 3 en haut, Boss 1 en bas à gauche,
    // Boss 2 en bas à droite, à distance du joueur). Victoire = les 3 boss abattus (voir OnBossDied).
    private readonly List<BossBase> _titans = new List<BossBase>();
    private float _titansMaxTotal = 0f;
    private int _titansRemaining = 0;
    public int TitansRemaining => _titansRemaining;

    // Somme des PV des titans encore en vie / PV max cumulés : alimente l'unique barre de boss du HUD.
    public void GetTitansHealth(out float current, out float max)
    {
        current = 0f;
        foreach (BossBase b in _titans)
            if (b != null) current += Mathf.Max(0f, b.CurrentHealth);
        max = Mathf.Max(1f, _titansMaxTotal);
    }

    private IEnumerator TitansStart()
    {
        // Laisse les autres systèmes (joueur, LevelUpManager, EnemySpawner, HUD) finir leur Start().
        yield return null;
        yield return null;

        if (_enemySpawner == null) _enemySpawner = FindFirstObjectByType<EnemySpawner>();
        if (_enemySpawner != null) _enemySpawner.gameObject.SetActive(false);
        ClearAllEnemies();

        GrantFullBuild();

        GameObject player = GameObject.FindWithTag("Player");
        Vector3 origin = player != null ? player.transform.position : Vector3.zero;

        _bossAlive = true;
        _titansRemaining = 0;
        _titansMaxTotal = 0f;
        float maxZoom = 0f;

        for (int number = 1; number <= 3; number++)
        {
            GameObject prefab = number == 1 ? _bossPrefab1 : number == 2 ? _bossPrefab2 : _bossPrefab3;
            if (prefab == null) { Debug.LogWarning($"Boss {number} prefab non assigné !"); continue; }

            Vector2 o = GameModes.TitansSpawnOffset(number);
            Vector3 pos = MapBoundaryUtils.ClampToZone(origin + new Vector3(o.x, 0f, o.y));
            GameObject go = Instantiate(prefab, pos, Quaternion.identity);
            go.TryGetComponent(out BossBase boss);
            if (boss == null) continue;

            float hp = GameModes.TitansBossHealth(number);
            boss.SetHealthOverride(hp);
            _titans.Add(boss);
            _titansMaxTotal += hp;
            _titansRemaining++;
            maxZoom = Mathf.Max(maxZoom, boss.CameraZoomMargin);
        }
        _bossCount = 3;

        if (BossCameraZoom.Instance != null) BossCameraZoom.Instance.SetBossZoom(maxZoom);

        // Les boss fixent leurs PV / affichent leur propre barre une frame après leur Start() : on prend la main juste après.
        yield return null;
        yield return null;
        if (GameUI.Instance != null)
        {
            GameUI.Instance.ShowBossHP("Les 3 Titans");
            GameUI.Instance.UpdateBossHP(_titansMaxTotal, _titansMaxTotal);
        }
    }

    // Toutes les améliorations au palier max (même logique que le raccourci de debug F4 de DebugCheats).
    private void GrantFullBuild()
    {
        if (LevelUpManager.Instance == null || LevelUpManager.Instance.AllUpgrades == null) return;
        foreach (UpgradeData upgrade in LevelUpManager.Instance.AllUpgrades)
        {
            if (upgrade == null) continue;
            if (upgrade.upgradeType == UpgradeType.Heal) continue;   // le soin n'a pas de sens ici (les boss soignent de 50 % à leur mort)
            int picks = 0;
            while (upgrade.IsAvailable() && picks < 10)
            {
                upgrade.Apply();
                picks++;
            }
        }
    }

    private int _rushBossTotal = 0;
    private float _nextModeHudRefresh = 0f;

    // HUD : la ligne du défi (masquée hors classique, voir ChallengeManager) affiche l'état du mode.
    private void UpdateModeHud(float runTimer)
    {
        if (GameUI.Instance == null || GameModes.IsClassic) return;
        if (Time.unscaledTime < _nextModeHudRefresh) return;
        _nextModeHudRefresh = Time.unscaledTime + 0.5f;

        if (GameModes.IsTitans)
        {
            int killed = GameManager.Instance != null ? GameManager.Instance.BossKillCount : 0;
            GameUI.Instance.UpdateChallengeDisplay(Loc.T("Choc des titans"), Loc.F("Titans {0}/3", killed), false);
        }
        else if (GameModes.IsBossRush)
        {
            int killed = GameManager.Instance != null ? GameManager.Instance.BossKillCount : 0;
            string progress;
            if (_bossAlive) progress = Loc.F("Boss {0}/{1} en cours", Mathf.Min(_bossCount, _rushBossTotal), _rushBossTotal);
            else if (_bossCount >= _rushBossTotal) progress = Loc.F("Boss {0}/{1}", killed, _rushBossTotal);
            else
            {
                float remaining = Mathf.Max(0f, GameModes.RushBossInterval * (_bossCount + 1) - runTimer);
                progress = Loc.F("Boss {0}/{1} · prochain {2} s", killed, _rushBossTotal, Mathf.CeilToInt(remaining));
            }
            GameUI.Instance.UpdateChallengeDisplay(Loc.T("Ruée de boss"), progress, false);
        }
        else if (GameModes.IsEndless)
        {
            int loop = GameModes.BossLoop(Mathf.Max(0, _bossCount - (_bossAlive ? 1 : 0))) + 1;
            float remaining = Mathf.Max(0f, BossSpawnInterval * (_bossCount + 1) - runTimer);
            string progress = _bossAlive
                ? Loc.F("Boucle {0} · boss en cours", loop)
                : Loc.F("Boucle {0} · boss dans {1:0}:{2:00}", loop, Mathf.FloorToInt(remaining / 60f), Mathf.FloorToInt(remaining % 60f));
            GameUI.Instance.UpdateChallengeDisplay(Loc.T("Sans fin"), progress, false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) GameModes.ResetScaling();
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        if (GameManager.Instance.IsPaused) return;

        // Choc des titans : pas de planning de boss ; on ne fait qu'afficher le chrono et l'état du mode.
        if (GameModes.IsTitans)
        {
            float t = GameManager.Instance.RunTimer;
            UpdateModeHud(t);
            if (GameUI.Instance != null) GameUI.Instance.UpdateTimer(t);
            return;
        }

        if (_bossAlive) return;

        // On utilise le timer de GameManager — un seul timer pour tout
        float runTimer = GameManager.Instance.RunTimer;

        ApplyDifficulty();

        // MODIFIE (2026-09-26) - mode sans fin : après le boss 3, les boss reviennent en boucle 1-2-3-1-2-3... (toujours
        // un boss tous les BossSpawnInterval de temps de jeu, le chrono ne tournant pas pendant un combat de boss).
        // En classique, comportement identique à avant : 3 boss puis plus rien (la victoire vient de OnBossDied).
        if (GameModes.IsBossRush)
        {
            // RUÉE DE BOSS : un boss par minute de temps de jeu, Boss 1 x3 puis Boss 2 x3 puis Boss 3 x3 (selon la
            // progression du joueur en classique). Le chrono ne tourne pas pendant un combat, donc entre deux boss il y a
            // toujours ~60 s d'ennemis normaux (XP, montée de niveau).
            if (_bossCount < _rushBossTotal && runTimer >= GameModes.RushBossInterval * (_bossCount + 1))
                SpawnBoss(GameModes.RushBossNumber(_bossCount));
        }
        else if (_bossCount < 3 || GameModes.IsEndless)
        {
            if (runTimer >= BossSpawnInterval * (_bossCount + 1))
                SpawnBoss((_bossCount % 3) + 1);
        }

        GameModes.UpdateScaling(runTimer / 60f);
        UpdateModeHud(runTimer);

        GameUI.Instance.UpdateTimer(runTimer); // ← même timer partout
    }

    // MODIFIÉ (2026-10-16, audit version finale) - cette méthode imposait CHAQUE FRAME une cadence et un plafond
    // d'ennemis (15/25/30/40/50/60) qui écrasaient les phases de l'EnemySpawner (20/45/75/120/105/160, avec leurs
    // propres cadences) : seul "ennemisParVague" des phases comptait réellement. Les phases de l'EnemySpawner sont
    // désormais la seule source de la difficulté de spawn (réglables dans l'Inspector, objet EnemySpawner de la
    // scène Game). Il ne reste ici que la montée propre au mode Sans fin au-delà de 15 min, appliquée par-dessus la
    // dernière phase (0,6 s / 160 ennemis) : cadence qui accélère jusqu'à 0,3 s et plafond qui monte jusqu'à 200
    // (+1,5 par minute) - même pente qu'avant, partant du plafond réel de la dernière phase.
    private const float EndlessBaseInterval = 0.6f;
    private const int EndlessBaseMaxEnemies = 160;
    private const int EndlessMaxEnemiesCap = 200;

    private void ApplyDifficulty()
    {
        if (_enemySpawner == null || GameManager.Instance == null) return;
        if (!GameModes.IsEndless) return;

        float minutes = GameManager.Instance.RunTimer / 60f;
        if (minutes < GameModes.EndlessScaleStartMinutes) return;

        float extra = minutes - GameModes.EndlessScaleStartMinutes;
        _enemySpawner.SetSpawnInterval(Mathf.Max(0.3f, EndlessBaseInterval - 0.01f * extra));
        _enemySpawner.SetMaxEnemies(Mathf.Min(EndlessMaxEnemiesCap, EndlessBaseMaxEnemies + Mathf.RoundToInt(extra * 1.5f)));
    }

    private void SpawnBoss(int bossNumber)
    {
        _bossCount++;
        _bossAlive = true;

        // Déblocage de Kael : atteindre le Boss 2 en une partie (le fait de le
        // faire apparaître suffit — mourir dessus juste après compte quand même).
        // (Classique uniquement : les autres modes ne touchent pas aux déblocages.)
        if (bossNumber == 2 && GameModes.CountsForProgression && MetaProgressionManager.Instance != null)
            MetaProgressionManager.Instance.UnlockCharacter(1);

        ClearAllEnemies();
        _enemySpawner.gameObject.SetActive(false);

        GameObject player    = GameObject.FindWithTag("Player");
        // CORRIGÉ (2026-10-18, diagnostic complet - audit §2.5) - l'ancien décalage fixe (+10 en X) plaçait le boss
        // PILE sur le joueur si celui-ci se trouvait près du bord est de la carte (le clamp qui suit ramenait le
        // boss à la même position que le joueur). Décalage maintenant calculé VERS LE CENTRE de la zone depuis la
        // position du joueur : le boss apparaît toujours du côté où il y a de la place, quel que soit l'endroit où
        // se trouve le joueur sur la carte.
        Vector3 towardCenter = -player.transform.position;
        towardCenter.y = 0f;
        if (towardCenter.sqrMagnitude < 0.01f) towardCenter = Vector3.right;
        towardCenter.Normalize();
        // CORRIGÉ (2026-10-21) - même bug que EnemySpawner.FindFreeSpawnPosition (voir son commentaire) : reprendre
        // player.transform.position tel quel donnait un boss à Y=1,5 (hauteur de torse du joueur) au lieu du sol.
        Vector3 spawnPos = player.transform.position + towardCenter * 10f;
        spawnPos.y = 0f;
        spawnPos = MapBoundaryUtils.ClampToZone(spawnPos);

        GameObject bossPrefab = bossNumber == 1 ? _bossPrefab1 :
                                bossNumber == 2 ? _bossPrefab2 : _bossPrefab3;

        if (bossPrefab != null)
        {
            GameObject bossInstance = Instantiate(bossPrefab, spawnPos, Quaternion.identity); // MODIFIÉ — on garde la référence

            // AJOUTÉ (2026-10-20, chantier satisfaction - "tu peux faire les animations pour l'arrivée du boss") -
            // impact au sol à l'instant où le boss apparaît (onde de choc + étincelles + secousse de caméra) : le
            // seul retour avant ce correctif était un zoom de caméra (voir SetBossZoom plus bas) + la barre de vie
            // qui se remplissait, rien ne marquait le moment précis de L'ARRIVÉE elle-même. CameraShakeFX est
            // volontairement utilisée ici (contrairement aux dégâts courants, où elle est proscrite - voir son
            // en-tête) : l'arrivée d'un boss EST justement l'un des "gros moments rares" auxquels elle est réservée.
            ExpandingRingVFX.Spawn(spawnPos, 4f, new Color(0.85f, 0.15f, 0.15f), 0.6f);
            ImpactSparkVFX.Spawn(spawnPos, new Color(0.85f, 0.15f, 0.15f), 2.2f);
            CameraShakeFX.Shake(0.55f);

            // AJOUTÉ — applique le zoom propre à CE boss, lu directement sur son prefab
            bossInstance.TryGetComponent(out BossBase bossScript);

            // Mode sans fin : chaque nouvelle série de boss (boucle) a plus de PV. Appliqué par le boss lui-même
            // une fois son Start() passé (voir BossBase.InitUI), car certains boss fixent leurs PV dans Start().
            if (bossScript != null && GameModes.IsEndless)
            {
                int loop = GameModes.BossLoop(_bossCount - 1);
                if (loop > 0)
                    bossScript.SetEndlessHealthMultiplier(GameModes.EndlessBossHealthMultiplier(loop));
            }

            // Ruée de boss : PV imposés d'après la puissance attendue du joueur à cette minute + dégâts qui montent à chaque
            // réapparition du même boss (voir GameModes, section Ruée de boss).
            if (bossScript != null && GameModes.IsBossRush)
            {
                int spawnIndex = _bossCount - 1;
                bossScript.SetHealthOverride(GameModes.RushBossHealth(spawnIndex));
                GameModes.SetRushBossDamage(GameModes.RushBossDamageScale(spawnIndex));
            }

            // AJOUTÉ (2026-10-18, diagnostic complet - système de difficulté) - palier de difficulté ; x1 au palier
            // Modéré, donc SetDifficultyHealthMultiplier(1f) ne change rien (la garde Mathf.Max(1f,...) dans
            // BossBase laisse _maxHealth intact dans ce cas).
            // ÉTENDU (2026-10-20, carrousel de difficulté - "les boss [...] dans les autres difficultés bénéficient
            // du scale") - s'applique maintenant aussi en Sans fin et Ruée de boss, EN PLUS de leur propre
            // multiplicateur/valeur imposée (voir BossBase.InitUI, qui les cumule maintenant au lieu de les
            // traiter comme mutuellement exclusifs).
            if (bossScript != null && (GameModes.IsClassic || GameModes.IsEndless || GameModes.IsBossRush))
            {
                // MODIFIÉ (2026-10-21, retour utilisateur : "le boss 2 a trop de vie dans apocalyptique") - le Boss 2
                // (BossDeer, PV de base 90000 à l'époque, bien au-dessus du Boss 1 à 20000) devenait disproportionné
                // une fois multiplié par le x2 partagé avec les 2 autres boss (180000, presque autant que le Boss 3
                // À SA BASE NON multipliée, 180000 lui aussi) - nerf spécifique à ce boss précis, uniquement à
                // l'Apocalyptique (Violent jamais signalé comme trop faible, reste sur la valeur partagée).
                // MODIFIÉ ENCORE (2026-10-22, retour utilisateur : "le boss 2 à toujours trop de vie, baisse la de
                // 30% dans chaque niveau de difficulté") - cette fois le PV DE BASE lui-même (BossDeer._maxHealth,
                // 90000 -> 63000, voir BossDeer.prefab), pas un multiplicateur de palier : s'applique donc déjà
                // identiquement aux 3 paliers (Modéré x1, Violent x1,25, Apocalyptique x1,5 via le nerf ci-dessus),
                // aucun changement nécessaire ici.
                float bossHealthMult = DifficultyTiers.BossHealthMultiplier(DifficultyTiers.Current);
                if (bossNumber == 2 && DifficultyTiers.Current == DifficultyTier.Apocalyptic)
                    bossHealthMult = 1.5f;
                bossScript.SetDifficultyHealthMultiplier(bossHealthMult);

                // AJOUTÉ (2026-10-21, retour utilisateur : "rendre les boss plus fort aussi dans leur vitesse de
                // déplacement et le délai entre chaque attaques [...] pour le boss 2, mettre plus de projectiles
                // dans ce même scale") - voir BossBase/BossDeer/BossCorruptedSource pour le détail de ce que chaque
                // boss fait de ces 2 multiplicateurs (très différent d'un boss à l'autre).
                bossScript.SetDifficultySpeedMultiplier(DifficultyTiers.BossSpeedMultiplier(DifficultyTiers.Current));
                bossScript.SetDifficultyAttackSpeedMultiplier(DifficultyTiers.BossAttackSpeedMultiplier(DifficultyTiers.Current));

                if (bossNumber == 2 && bossScript is BossDeer deer)
                {
                    int extraArms = DifficultyTiers.Current == DifficultyTier.Apocalyptic ? 2
                                  : DifficultyTiers.Current == DifficultyTier.Violent ? 1
                                  : 0;
                    deer.SetDifficultyExtraSpiralArms(extraArms);
                }
            }

            if (bossScript != null && BossCameraZoom.Instance != null)
                BossCameraZoom.Instance.SetBossZoom(bossScript.CameraZoomMargin);
        }
        else
            Debug.LogWarning($"Boss {bossNumber} prefab non assigné !");

        Debug.Log($"Boss {bossNumber} spawné !");
    }

    // SUPPRIMÉ (2026-10-18, diagnostic complet - audit §10) - classe MapBoundaryUtils imbriquée ici, en double avec
    // la classe autonome Systems/MapBoundaryUtils.cs. Tous les appels `MapBoundaryUtils.ClampToZone(...)` de ce
    // fichier résolvaient vers CETTE copie (le compilateur préfère un type imbriqué à un type de plus haut niveau
    // du même nom) ; ils résolvent maintenant naturellement vers la classe autonome, sans rien changer à leur
    // écriture - voir Systems/MapBoundaryUtils.cs pour le détail et la nouvelle taille de zone variable.

    // AJOUTE (2026-10-04) - DEBUG/TEST uniquement (voir DebugCheats.cs) : simple enrobage public pour exposer
    // ClearAllEnemies() (privee) a un raccourci clavier - "enlever les ennemis" a la demande.
    public void DebugClearAllEnemies()
    {
        ClearAllEnemies();
    }

    private void ClearAllEnemies()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject enemy in enemies)
        {
            enemy.TryGetComponent(out EnemyBase eb);
            if (eb != null)
                ObjectPool.Instance.ReturnToPool(GetPoolTag(enemy), enemy);
            else
                Destroy(enemy);
        }
    }

    private string GetPoolTag(GameObject enemy)
    {
        enemy.TryGetComponent(out EnemyBase eb);
        return eb != null ? eb.PoolTag : "Enemy"; // MODIFIÉ — lit le tag configuré sur le prefab au lieu de deviner via une chaîne de GetComponent<X>()
    }

    // AJOUTE (2026-09-16) - DEBUG/TEST uniquement (voir DebugCheats.cs) : saute
    // directement au Boss 3 sans avoir a battre les Boss 1/2 ni attendre les
    // delais entre eux - retour utilisateur, "battre les 2 boss et attendre
    // le delai entre les boss c'est long" pour tester le Boss 3 en iteration.
    // Force _bossCount a 2 AVANT de spawner, pour qu'OnBossDied() declenche
    // correctement la Victoire une fois ce Boss 3 tue (comme un vrai run).
    public void DebugSkipToBoss3()
    {
        // Detruit un boss deja en vie s'il y en a un (ex: skip declenche en
        // plein combat de Boss 1/2), pour ne jamais en avoir deux en meme temps.
        BossBase[] existingBosses = FindObjectsByType<BossBase>(FindObjectsSortMode.None);
        foreach (BossBase b in existingBosses)
            if (b != null) Destroy(b.gameObject);

        // Filet de securite - _enemySpawner n'est normalement assigne qu'une
        // fois dans Start() ; le re-verifier ici evite un NullReferenceException
        // si ce raccourci est declenche avant que Start() ait pu s'executer
        // correctement (observe en testant via des rechargements de scene
        // scriptes rapprochés - improbable en jeu normal, mais coute rien a
        // securiser vu que c'est un outil de debug).
        if (_enemySpawner == null)
            _enemySpawner = FindFirstObjectByType<EnemySpawner>();

        _bossAlive = false;
        _bossCount = 2;
        SpawnBoss(3);
    }

    // DEBUG/TEST uniquement : fait apparaître le boss d'indice `spawnIndex` (0 = boss 1, 3 = boss 1 de la 2e série du sans fin...).
    public void DebugSpawnBossIndex(int spawnIndex)
    {
        BossBase[] existingBosses = FindObjectsByType<BossBase>(FindObjectsSortMode.None);
        foreach (BossBase b in existingBosses)
            if (b != null) Destroy(b.gameObject);

        if (_enemySpawner == null)
            _enemySpawner = FindFirstObjectByType<EnemySpawner>();

        _bossAlive = false;
        _bossCount = Mathf.Max(0, spawnIndex);
        SpawnBoss(GameModes.IsBossRush ? GameModes.RushBossNumber(_bossCount) : (_bossCount % 3) + 1);
    }

    public void OnBossDied()
    {
        if (GameModes.IsTitans)
        {
            _titansRemaining = Mathf.Max(0, _titansRemaining - 1);
            if (_titansRemaining > 0) return;   // il reste des titans : le combat continue

            _bossAlive = false;
            if (BossCameraZoom.Instance != null) BossCameraZoom.Instance.ResetZoom();
            GameManager.Instance.TriggerVictory();
            return;
        }

        _bossAlive = false;
        // CORRIGÉ (2026-10-03) - trouvé en testant un boss tué très tôt après le chargement de la scène (avant que
        // Start() n'ait eu l'occasion de peupler _enemySpawner) : NullReferenceException ici. Même garde défensive
        // que les autres usages de _enemySpawner dans ce fichier (TitansStart, SetSpawnInterval...).
        if (_enemySpawner == null) _enemySpawner = FindFirstObjectByType<EnemySpawner>();
        if (_enemySpawner != null) _enemySpawner.gameObject.SetActive(true);

        if (BossCameraZoom.Instance != null) BossCameraZoom.Instance.ResetZoom();

        GameModes.ClearRushBossDamage();

        // Victoire : Classique = 3e boss vaincu ; Ruée de boss = tous les boss prévus vaincus ; Sans fin = jamais (la partie
        // ne s'arrête qu'à la mort ou à l'abandon).
        if (GameModes.IsBossRush)
        {
            if (_bossCount >= _rushBossTotal && _rushBossTotal > 0)
                GameManager.Instance.TriggerVictory();
        }
        else if (_bossCount >= 3 && !GameModes.IsEndless)
            GameManager.Instance.TriggerVictory();

        Debug.Log($"Boss vaincu ! Run continue — Vague {CurrentWave}");
    }
}