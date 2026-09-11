using System;
using System.Collections.Generic;
using UnityEngine;

public class ChallengeManager : MonoBehaviour
{
    public static ChallengeManager Instance { get; private set; }

    [Header("Pondération des paliers (poids relatifs)")]
    [SerializeField] private float _easyWeight = 50f;
    [SerializeField] private float _mediumWeight = 35f;
    [SerializeField] private float _hardWeight = 15f;

    [Header("Bonus d'or par palier (pourcentage de l'or ramassé)")]
    [SerializeField] private float _easyRewardPercent = 0.10f;
    [SerializeField] private float _mediumRewardPercent = 0.25f;
    [SerializeField] private float _hardRewardPercent = 0.50f;

    [Header("Anti-répétition")]
    [SerializeField] private int _recentHistorySize = 3;

    public ChallengeDefinition CurrentChallenge { get; private set; }
    public bool IsFailed { get; private set; } = false;
    public bool IsCompleted { get; private set; } = false;

    private int _dashUsedCount = 0;
    private bool _ultimateUsedThisRun = false;
    private bool _tookDamageThisRun = false;
    private float _minHealthPercent = 1f;
    private bool _level20ReachedInTime = false;

    // Time.time plutot que GameManager.RunTimer pour la duree du combat de boss :
    // RunTimer NE PROGRESSE PAS pendant qu'un boss est vivant (bloque explicitement
    // dans GameManager.Update()), l'utiliser ici aurait rendu ce defi trivial.
    // Time.time avance normalement pendant un combat, et s'arrete bien en pause
    // (Time.timeScale = 0), ce qui est le comportement voulu.
    private float _fastestBossKillTime = float.MaxValue;
    private float _currentBossSpawnRealTime = -1f;
    private int _bossesDefeatedCount = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        EnsureCurrentChallenge();
    }

    private void EnsureCurrentChallenge()
    {
        if (MetaProgressionManager.Instance == null || MetaProgressionManager.Instance.Data == null) return;
        SaveData data = MetaProgressionManager.Instance.Data;

        long currentHourBucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 3600;

        if (data.currentChallengeHourBucket != currentHourBucket || string.IsNullOrEmpty(data.currentChallengeId))
        {
            data.currentChallengeId = PickNewChallenge(data);
            data.currentChallengeHourBucket = currentHourBucket;
            data.currentChallengeRewardClaimed = false;

            UpdateRecentHistory(data, data.currentChallengeId);
            SaveSystem.Save(data);
        }

        CurrentChallenge = ChallengeDatabase.Get(data.currentChallengeId);
        RefreshDisplay();
    }

    private void UpdateRecentHistory(SaveData data, string newId)
    {
        if (data.recentChallengeIds == null) data.recentChallengeIds = new List<string>();
        data.recentChallengeIds.Add(newId);
        while (data.recentChallengeIds.Count > _recentHistorySize)
            data.recentChallengeIds.RemoveAt(0);
    }

    private string PickNewChallenge(SaveData data)
    {
        ChallengeDifficulty difficulty = PickWeightedDifficulty();
        List<ChallengeDefinition> pool = ChallengeDatabase.GetByDifficulty(difficulty);

        List<ChallengeDefinition> available = pool.FindAll(c =>
            data.recentChallengeIds == null || !data.recentChallengeIds.Contains(c.id));

        // Filet de securite : si l'exclusion vide totalement le palier, on
        // retombe sur le palier complet plutot que de planter avec une liste vide.
        if (available.Count == 0) available = pool;

        return available[UnityEngine.Random.Range(0, available.Count)].id;
    }

    private ChallengeDifficulty PickWeightedDifficulty()
    {
        float total = _easyWeight + _mediumWeight + _hardWeight;
        float roll = UnityEngine.Random.Range(0f, total);

        if (roll < _easyWeight) return ChallengeDifficulty.Easy;
        if (roll < _easyWeight + _mediumWeight) return ChallengeDifficulty.Medium;
        return ChallengeDifficulty.Hard;
    }

    // AJOUTE - expose le pourcentage de recompense du defi en cours, pour
    // l'affichage detaille dans le menu pause (PauseMenuUI).
    public float GetCurrentRewardPercent()
    {
        if (CurrentChallenge == null) return 0f;

        switch (CurrentChallenge.difficulty)
        {
            case ChallengeDifficulty.Easy: return _easyRewardPercent;
            case ChallengeDifficulty.Medium: return _mediumRewardPercent;
            case ChallengeDifficulty.Hard: return _hardRewardPercent;
            default: return 0f;
        }
    }

    // =====================
    // NOTIFICATIONS EN COURS DE RUN
    // =====================

    public void NotifyDashUsed()
    {
        _dashUsedCount++;
        RefreshDisplay();
    }

    public void NotifyUltimateUsed()
    {
        _ultimateUsedThisRun = true;
        CheckFailureConditions();
    }

    public void NotifyDamageTaken()
    {
        _tookDamageThisRun = true;
        CheckFailureConditions();
    }

    public void NotifyHealthChanged(float current, float max)
    {
        if (max <= 0f) return;
        float percent = current / max;
        if (percent < _minHealthPercent) _minHealthPercent = percent;
        CheckFailureConditions();
    }

    // A appeler depuis le script qui gere l'XP/les niveaux, a chaque montee de
    // niveau, avec le nouveau niveau atteint.
    public void NotifyLevelChanged(int newLevel)
    {
        if (CurrentChallenge != null && CurrentChallenge.id == "level20in10min"
            && newLevel >= 20 && GameManager.Instance != null && GameManager.Instance.RunTimer <= 600f)
        {
            _level20ReachedInTime = true;
        }
        RefreshDisplay();
    }

    public void NotifyBossSpawned()
    {
        _currentBossSpawnRealTime = Time.time;
    }

    public void NotifyBossDefeated()
    {
        _bossesDefeatedCount++;

        if (_currentBossSpawnRealTime >= 0f)
        {
            float fightDuration = Time.time - _currentBossSpawnRealTime;
            if (fightDuration < _fastestBossKillTime)
                _fastestBossKillTime = fightDuration;
        }
        _currentBossSpawnRealTime = -1f;

        RefreshDisplay();
    }

    private void CheckFailureConditions()
    {
        if (CurrentChallenge == null || IsFailed) return;

        bool failed = false;
        switch (CurrentChallenge.id)
        {
            case "noUltimate":
                failed = _ultimateUsedThisRun;
                break;
            case "noDamage":
                failed = _tookDamageThisRun;
                break;
            case "hp30never":
                failed = _minHealthPercent < 0.30f;
                break;
        }

        if (failed)
        {
            IsFailed = true;
            RefreshDisplay();
        }
    }

    // Public - appelee aussi depuis GameManager.AddKill() et
    // MetaProgressionManager.AddRunGold() pour rafraichir la progression en
    // temps reel des defis bases sur les kills/l'or.
    public void RefreshDisplay()
    {
        if (GameUI.Instance != null && CurrentChallenge != null)
            GameUI.Instance.UpdateChallengeDisplay(CurrentChallenge.displayName, GetProgressText(), IsFailed);
    }

    private string GetProgressText()
    {
        if (CurrentChallenge == null) return "";
        if (IsFailed) return "Échoué";

        int kills = GameManager.Instance != null ? GameManager.Instance.KillCount : 0;
        int bossKills = GameManager.Instance != null ? GameManager.Instance.BossKillCount : 0;
        int gold = MetaProgressionManager.Instance != null ? MetaProgressionManager.Instance.RunGold : 0;
        int level = XPSystem.Instance != null ? XPSystem.Instance.CurrentLevel : 1;

        switch (CurrentChallenge.id)
        {
            case "kill150": return $"{Mathf.Min(kills, 150)}/150";
            case "level10": return $"Niv. {Mathf.Min(level, 10)}/10";
            case "boss1": return $"{Mathf.Min(bossKills, 1)}/1";
            case "dash20": return $"{Mathf.Min(_dashUsedCount, 20)}/20";
            case "hp30never": return "En cours";
            case "boss2": return $"{Mathf.Min(bossKills, 2)}/2";
            case "level20in10min": return $"Niv. {Mathf.Min(level, 20)}/20";
            case "noUltimate": return "En cours";
            case "noDamage": return "En cours";
            case "bossUnder30s": return "En cours";
            case "gold3000": return $"{Mathf.Min(gold, 3000)}/3000 Or";
            case "level30": return $"Niv. {Mathf.Min(level, 30)}/30";
            default: return "";
        }
    }

    // =====================
    // ÉVALUATION FINALE (appelée par GameManager à la fin de la run)
    // =====================

    public void EvaluateAndApplyReward(int killCount, int levelReached, int bossKillCount, int runGoldEarned)
    {
        if (CurrentChallenge == null) return;
        if (IsFailed) { IsCompleted = false; return; }

        bool success = false;
        switch (CurrentChallenge.id)
        {
            case "kill150": success = killCount >= 150; break;
            case "level10": success = levelReached >= 10; break;
            case "boss1": success = bossKillCount >= 1; break;
            case "dash20": success = _dashUsedCount >= 20; break;
            case "hp30never": success = _minHealthPercent >= 0.30f; break;
            case "boss2": success = bossKillCount >= 2; break;
            case "level20in10min": success = _level20ReachedInTime; break;
            case "noUltimate": success = !_ultimateUsedThisRun; break;
            case "noDamage": success = !_tookDamageThisRun; break;
            case "bossUnder30s": success = _fastestBossKillTime <= 30f; break;
            case "gold3000": success = runGoldEarned >= 3000; break;
            case "level30": success = levelReached >= 30; break;
        }

        IsCompleted = success;
        if (!success) return;

        ApplyGoldReward(runGoldEarned);
    }

    private void ApplyGoldReward(int runGoldEarned)
    {
        if (MetaProgressionManager.Instance == null || MetaProgressionManager.Instance.Data == null) return;
        SaveData data = MetaProgressionManager.Instance.Data;

        if (data.currentChallengeRewardClaimed)
        {
            Debug.Log("Défi déjà réussi cette heure — pas de bonus supplémentaire.");
            return;
        }

        int bonus = Mathf.RoundToInt(runGoldEarned * GetCurrentRewardPercent());
        MetaProgressionManager.Instance.AddRunGold(bonus);

        data.currentChallengeRewardClaimed = true;
        SaveSystem.Save(data);
    }
}