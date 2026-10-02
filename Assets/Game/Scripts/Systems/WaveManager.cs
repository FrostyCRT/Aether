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
            BossBase boss = go.GetComponent<BossBase>();
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

    // Toutes les améliorations au palier max (même logique que le raccourci de debug F6 de DebugCheats).
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
            GameUI.Instance.UpdateChallengeDisplay("Choc des titans", $"Titans {killed}/3", false);
        }
        else if (GameModes.IsBossRush)
        {
            int killed = GameManager.Instance != null ? GameManager.Instance.BossKillCount : 0;
            string progress;
            if (_bossAlive) progress = $"Boss {Mathf.Min(_bossCount, _rushBossTotal)}/{_rushBossTotal} en cours";
            else if (_bossCount >= _rushBossTotal) progress = $"Boss {killed}/{_rushBossTotal}";
            else
            {
                float remaining = Mathf.Max(0f, GameModes.RushBossInterval * (_bossCount + 1) - runTimer);
                progress = $"Boss {killed}/{_rushBossTotal} · prochain {Mathf.CeilToInt(remaining)} s";
            }
            GameUI.Instance.UpdateChallengeDisplay("Ruée de boss", progress, false);
        }
        else if (GameModes.IsEndless)
        {
            int loop = GameModes.BossLoop(Mathf.Max(0, _bossCount - (_bossAlive ? 1 : 0))) + 1;
            float remaining = Mathf.Max(0f, BossSpawnInterval * (_bossCount + 1) - runTimer);
            string progress = _bossAlive
                ? $"Boucle {loop} · boss en cours"
                : $"Boucle {loop} · boss dans {Mathf.FloorToInt(remaining / 60f):0}:{Mathf.FloorToInt(remaining % 60f):00}";
            GameUI.Instance.UpdateChallengeDisplay("Sans fin", progress, false);
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
            float interval = 0.6f;
            int max = 60;

            // Sans fin : au-delà de 15 min la densité continue de monter, avec un plafond pour rester fluide.
            if (GameModes.IsEndless && minutes >= GameModes.EndlessScaleStartMinutes)
            {
                float extra = minutes - GameModes.EndlessScaleStartMinutes;
                interval = Mathf.Max(0.3f, 0.6f - 0.01f * extra);
                max = Mathf.Min(110, 60 + Mathf.RoundToInt(extra * 1.5f));
            }

            _enemySpawner.SetSpawnInterval(interval);
            _maxEnemiesOnScreen = max;
        }

        _enemySpawner.SetMaxEnemies(_maxEnemiesOnScreen);
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
        Vector3    spawnPos  = player.transform.position + new Vector3(10f, 0f, 0f);
        spawnPos = MapBoundaryUtils.ClampToZone(spawnPos);

        GameObject bossPrefab = bossNumber == 1 ? _bossPrefab1 :
                                bossNumber == 2 ? _bossPrefab2 : _bossPrefab3;

        if (bossPrefab != null)
        {
            GameObject bossInstance = Instantiate(bossPrefab, spawnPos, Quaternion.identity); // MODIFIÉ — on garde la référence

            // AJOUTÉ — applique le zoom propre à CE boss, lu directement sur son prefab
            BossBase bossScript = bossInstance.GetComponent<BossBase>();

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
        _enemySpawner.gameObject.SetActive(true);

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