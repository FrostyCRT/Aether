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

    // MODIFIE (2026-09-13) - doubles sur retour utilisateur : +50% (x1,5) sur
    // le palier Difficile jugé trop faible vu l'effort demandé (ex. Sans-Faute,
    // Éclair). Facile et Moyen doubles dans les memes proportions pour garder
    // un ecart coherent entre paliers (x1,2 / x1,5 / x2).
    [Header("Bonus d'or par palier (pourcentage de l'or ramassé)")]
    [SerializeField] private float _easyRewardPercent = 0.20f;
    [SerializeField] private float _mediumRewardPercent = 0.50f;
    [SerializeField] private float _hardRewardPercent = 1.00f;

    [Header("Anti-répétition")]
    [SerializeField] private int _recentHistorySize = 3;

    public ChallengeDefinition CurrentChallenge { get; private set; }
    public bool IsFailed { get; private set; } = false;
    public bool IsCompleted { get; private set; } = false;

    // AJOUTE (2026-09-15) - expose si la recompense du defi de CETTE heure a
    // deja ete touchee (lors d'une run precedente, meme heure) : au-dela,
    // ApplyGoldReward() l'ignore deja silencieusement, donc HUD/menu pause ne
    // doivent plus presenter le defi comme "a faire" pour le reste de l'heure
    // (retour utilisateur). Se base directement sur la donnee de sauvegarde
    // (pas un champ local) : ce flag doit rester correct meme au tout premier
    // frame d'une nouvelle run, avant que quoi que ce soit d'autre ne tourne.
    public bool IsRewardAlreadyClaimedThisHour =>
        MetaProgressionManager.Instance != null && MetaProgressionManager.Instance.Data != null
        && MetaProgressionManager.Instance.Data.currentChallengeRewardClaimed;

    // AJOUTE (2026-09-15) - minutes restantes avant que currentChallengeHourBucket
    // change et qu'un nouveau defi soit tire (voir EnsureCurrentChallenge) -
    // utilise pour le message "Nouveau defi dans X min" du menu pause. IMPORTANT :
    // ceci ne fait QUE lire l'heure courante pour l'affichage, ca ne declenche
    // JAMAIS le tirage d'un nouveau defi ici - ce dernier ne se produit que
    // dans EnsureCurrentChallenge(), appelee uniquement au demarrage de la
    // scene Jeu (donc HORS partie en cours, jamais en cours de run - retour
    // utilisateur, voir la note sur EnsureCurrentChallenge plus bas).
    // MODIFIE (2026-09-15) - retour utilisateur : affichait "Nouveau defi dans
    // moins d'une minute" indefiniment (meme des heures plus tard) des qu'on
    // etait deja PASSE l'heure de bascule - courant des qu'une run traverse
    // cette limite en cours de route, puisque EnsureCurrentChallenge() ne
    // re-tire qu'au PROCHAIN chargement de la scene Jeu, jamais en cours de
    // run (voir la note dessus). Retourne desormais -1 pour signaler
    // explicitement ce cas "deja du" plutot que de mentir sur un compte a
    // rebours qui n'en est plus vraiment un - a l'appelant de distinguer les
    // deux cas dans le message affiche.
    public int GetMinutesUntilNextChallenge()
    {
        if (MetaProgressionManager.Instance == null || MetaProgressionManager.Instance.Data == null) return -1;

        long bucket = MetaProgressionManager.Instance.Data.currentChallengeHourBucket;
        long nextBucketStartSeconds = (bucket + 1) * 3600;
        long nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long secondsRemaining = nextBucketStartSeconds - nowSeconds;

        if (secondsRemaining <= 0) return -1;

        return Mathf.CeilToInt(secondsRemaining / 60f);
    }

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

    // MODIFIE (2026-09-15) - retour utilisateur : s'assurer que le tirage d'un
    // nouveau defi se fait bien HORS partie (jamais en cours de run, meme si
    // l'heure change pendant qu'une run est en cours). C'est deja garanti par
    // construction : cette methode n'est appelee QUE depuis Start() ci-dessus,
    // qui ne tourne qu'a la creation de ChallengeManager - donc uniquement au
    // chargement de la scene Jeu (debut de run), jamais pendant. Si l'heure
    // change en plein milieu d'une run, le defi de CETTE run reste inchange
    // jusqu'a son terme ; le nouveau defi n'est tire qu'au PROCHAIN chargement
    // de la scene Jeu (prochaine run). Documente ici pour eviter qu'un futur
    // appel de EnsureCurrentChallenge() soit ajoute par erreur en cours de
    // partie (ex. depuis Update()).
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

    // AJOUTE (2026-09-13) - "Or xN" plutot que "+100%" (retour utilisateur :
    // "plus parlant dans le langage jeu video, on est pas en maths"). Partage
    // entre le chip de fin de run (GameUI) et le detail du menu pause
    // (PauseMenuUI) pour rester coherent partout ou la recompense s'affiche.
    // Virgule francaise explicite (pas de dependance a la culture systeme) ;
    // pas de decimale quand le multiplicateur est un nombre entier (x2, pas x2,0).
    public static string FormatRewardMultiplier(float rewardPercent)
    {
        float multiplier = 1f + rewardPercent;
        string multiplierStr = Mathf.Approximately(multiplier, Mathf.Round(multiplier))
            ? Mathf.RoundToInt(multiplier).ToString()
            : multiplier.ToString("0.0").Replace('.', ',');
        return $"Or x{multiplierStr}";
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

    // AJOUTE (2026-09-13) - le Statut du menu pause restait bloque sur "En
    // cours" meme quand la condition etait deja acquise en cours de partie
    // (ex. "Vaincre 1 boss" apres avoir tue un boss) - retour utilisateur.
    // Les defis "jalon" (kills/niveau/boss/or/dash/rapidite) sont acquis pour
    // de bon des que le seuil est franchi, peu importe la suite. Les defis
    // "endurance" (hp30never/noUltimate/noDamage) ne PEUVENT pas etre confirmes
    // avant la fin reelle de la partie (la condition doit tenir jusqu'au bout) -
    // ils restent donc "En cours" tant qu'ils n'ont pas echoue, c'est correct
    // et voulu, pas un oubli.
    public bool IsCurrentlySucceeding()
    {
        if (CurrentChallenge == null || IsFailed) return false;

        int bossKills = GameManager.Instance != null ? GameManager.Instance.BossKillCount : 0;
        int gold = MetaProgressionManager.Instance != null ? MetaProgressionManager.Instance.RunGold : 0;
        int level = XPSystem.Instance != null ? XPSystem.Instance.CurrentLevel : 1;
        int kills = GameManager.Instance != null ? GameManager.Instance.KillCount : 0;

        switch (CurrentChallenge.id)
        {
            case "kill150": return kills >= 150;
            case "level10": return level >= 10;
            case "boss1": return bossKills >= 1;
            case "dash20": return _dashUsedCount >= 20;
            case "boss2": return bossKills >= 2;
            case "level20in10min": return _level20ReachedInTime;
            case "bossUnder30s": return _fastestBossKillTime <= 30f;
            case "gold3000": return gold >= 3000;
            case "level30": return level >= 30;
            default: return false; // hp30never / noUltimate / noDamage
        }
    }

    // Public - appelee aussi depuis GameManager.AddKill() et
    // MetaProgressionManager.AddRunGold() pour rafraichir la progression en
    // temps reel des defis bases sur les kills/l'or.
    public void RefreshDisplay()
    {
        if (GameUI.Instance == null || CurrentChallenge == null) return;

        // AJOUTE (2026-09-15) - retour utilisateur : le defi ne doit plus
        // s'afficher dans le HUD pour le reste de l'heure une fois deja
        // reussi (le refaire ne rapporte plus rien, voir ApplyGoldReward).
        if (IsRewardAlreadyClaimedThisHour)
        {
            GameUI.Instance.HideChallengeDisplay();
            return;
        }

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