using UnityEngine;

public enum GameMode
{
    Classic = 0,    // 3 boss (5 / 10 / 15 min), victoire au 3e
    Endless = 1,    // les boss reviennent en boucle 1-2-3-1-2-3..., tout se renforce, fin uniquement par la mort
    BossRush = 2,   // un boss par minute, seulement ceux déjà battus en classique, chacun 3 fois et de plus en plus fort
    Titans = 3      // build complet dès le départ, aucun ennemi normal, les 3 boss en même temps : les tuer tous les 3
}

// Mode de jeu choisi dans le menu principal (persistant) + toutes les règles propres à chaque mode (échelle du sans fin,
// équilibrage de la ruée de boss, ce qui compte pour la progression). Ajouter un mode = une valeur d'enum + une entrée
// dans Info() / IsUnlocked().
//
// RÈGLE GÉNÉRALE : défis, déblocages de personnages et records du CLASSIQUE ne bougent qu'en partie Classique.
public static class GameModes
{
    private const string PrefKey = "game_mode";

    public struct ModeInfo
    {
        public string name;
        public string description;
    }

    public static int Count => System.Enum.GetValues(typeof(GameMode)).Length;

    private static GameMode? _cached;

    public static GameMode Current
    {
        get
        {
            if (_cached.HasValue) return _cached.Value;
            int raw = 0;
            try { raw = PlayerPrefs.GetInt(PrefKey, 0); } catch { }
            _cached = System.Enum.IsDefined(typeof(GameMode), raw) ? (GameMode)raw : GameMode.Classic;
            return _cached.Value;
        }
        set
        {
            _cached = value;
            try { PlayerPrefs.SetInt(PrefKey, (int)value); } catch { }
        }
    }

    public static bool IsClassic => Current == GameMode.Classic;
    public static bool IsEndless => Current == GameMode.Endless;
    public static bool IsBossRush => Current == GameMode.BossRush;
    public static bool IsTitans => Current == GameMode.Titans;

    // Fusions d'armes (2026-09-27) : réservées au Classique et au Sans fin, où une run a le temps de maxer 2 armes.
    // La Ruée de boss et le Choc des titans ont leurs propres règles de build (voir WaveManager.TitansStart /
    // GrantFullBuild) et ne passent jamais par le tirage de cartes normal.
    public static bool FusionsEnabled => IsClassic || IsEndless;

    // AJOUTÉ (2026-10-23, demande utilisateur : "la fusion de 2 compétences doit être débloquable que à partir de 10
    // minutes de jeu") - avant ce temps de partie (GameManager.RunTimer), aucune carte de fusion n'est proposée, même
    // si les 2 armes sources sont déjà au max. La carte apparaît au premier tirage qui suit (level-up ou coffre de boss).
    public const float FusionUnlockTime = 600f;
    public static bool FusionsUnlockedNow =>
        FusionsEnabled && GameManager.Instance != null && GameManager.Instance.RunTimer >= FusionUnlockTime;

    // Accord singulier / pluriel pour les textes affichés (jamais de "(s)").
    public static string Plural(int count, string singular, string plural)
    {
        // Textes français en entrée, accordés et traduits selon la règle de la langue (voir Loc.PluralWord).
        return Loc.PluralWord(count, singular, plural);
    }

    // Défis, déblocages de personnages, records classiques : Classique uniquement.
    public static bool CountsForProgression => IsClassic;

    public static string ShortName(GameMode mode)
    {
        switch (mode)
        {
            case GameMode.Endless: return Loc.T("SANS FIN");
            case GameMode.BossRush: return Loc.T("RUÉE DE BOSS");
            case GameMode.Titans: return Loc.T("CHOC DES TITANS");
            default: return Loc.T("CLASSIQUE");
        }
    }

    // ---- Disponibilité --------------------------------------------------------------------------------------------------

    // Nombre de boss battus en Classique (0-3) : donne accès à la Ruée de boss et en fixe l'ampleur.
    public static int ClassicBossProgress(SaveData data)
    {
        return data == null ? 0 : Mathf.Clamp(data.bestClassicBossKills, 0, 3);
    }

    public static bool IsUnlocked(GameMode mode, SaveData data)
    {
        switch (mode)
        {
            case GameMode.BossRush: return ClassicBossProgress(data) >= 1;
            // Avoir battu les 3 boss en une partie classique = avoir remporté le mode Classique (bestClassicBossKills = 3).
            case GameMode.Titans: return ClassicBossProgress(data) >= 3;
            default: return true;
        }
    }

    // AJOUTÉ (2026-10-18, diagnostic complet - décision "modes de jeu") - Choc des titans retiré de la sélection
    // pour la version finale : il donne la build ENTIÈREMENT maxée dès le départ (GrantFullBuild) et aucune XP,
    // donc zéro décision de build pendant la partie - à l'opposé du cœur stratégique du genre (level-up, fusions),
    // et ses constantes de balance (TitansPlayerDps, TitansUptime...) ne sont que des premiers jets jamais
    // playtestés. Retrait NON destructif : enum, SaveData (titansRuns/titansWins/...), GameModes.Current/Info/
    // scaling et tout le code de WaveManager restent intacts - seul le sélecteur de mode ne peut plus s'y arrêter
    // (voir GameModeSelector.Step ci-dessous et EnsureCurrentIsAvailable) - récupérable en 1 ligne pour un futur
    // mode "New Game+"/défi post-lancement sans aucun travail de reconstruction.
    public static bool IsShippable(GameMode mode) => mode != GameMode.Titans;

    public static string LockedHint(GameMode mode)
    {
        if (mode == GameMode.Titans) return Loc.T("Remporte une partie en mode Classique pour débloquer ce mode");
        return mode == GameMode.BossRush ? Loc.T("Bats au moins le Boss 1 en mode Classique pour débloquer ce mode") : "";
    }

    public static ModeInfo Info(GameMode mode, SaveData data = null)
    {
        switch (mode)
        {
            case GameMode.Endless:
                return new ModeInfo
                {
                    name = ShortName(mode),
                    description = Loc.T("Les boss reviennent en boucle, de plus en plus forts. Tiens le plus longtemps possible")
                };
            case GameMode.BossRush:
            {
                int progress = ClassicBossProgress(data);
                string desc;
                if (progress <= 0)
                    desc = LockedHint(mode);
                else
                {
                    int bosses = progress * 3;
                    string which = progress == 1 ? Loc.T("le Boss 1") : progress == 2 ? Loc.T("les Boss 1 et 2") : Loc.T("les 3 boss");
                    desc = Loc.F("Un boss par minute : {0}, chacun 3 fois et de plus en plus fort. {1} boss, {1} min", which, bosses);
                }
                return new ModeInfo { name = ShortName(mode), description = desc };
            }
            case GameMode.Titans:
                return new ModeInfo
                {
                    name = ShortName(mode),
                    description = IsUnlocked(mode, data)
                        ? Loc.T("Toutes les améliorations au maximum, aucun ennemi... mais les 3 boss en même temps. Survis et abats-les tous")
                        : LockedHint(mode)
                };
            default:
                return new ModeInfo
                {
                    name = ShortName(mode),
                    description = Loc.T("Bats les 3 boss en 15 minutes pour remporter la partie")
                };
        }
    }

    // Ramène un mode sauvegardé devenu indisponible (save édité...) au Classique.
    public static void EnsureCurrentIsAvailable(SaveData data)
    {
        if (!IsUnlocked(Current, data) || !IsShippable(Current)) Current = GameMode.Classic;
    }

    // ---- Sans fin --------------------------------------------------------------------------------------------------------
    // Le classique s'arrête à 15 min : au-delà, la courbe existante (paliers d'ennemis) est prolongée par ces multiplicateurs.
    public const float EndlessScaleStartMinutes = 15f;

    // PV des ennemis normaux : x1 jusqu'à 15 min, puis croissance qui accélère doucement (30 min ≈ x4, 45 min ≈ x10).
    public static float EndlessHealthScale(float minutes)
    {
        if (!IsEndless) return 1f;
        float m = Mathf.Max(0f, minutes - EndlessScaleStartMinutes);
        return 1f + 0.12f * m + 0.006f * m * m;
    }

    // Dégâts subis (contact, projectiles, charges de boss) : plus lent que les PV pour rester jouable (30 min ≈ x1,75).
    public static float EndlessDamageScale(float minutes)
    {
        if (!IsEndless) return 1f;
        float m = Mathf.Max(0f, minutes - EndlessScaleStartMinutes);
        return 1f + 0.05f * m;
    }

    // Boucle de boss : 0 pour les boss 1-2-3 d'origine, 1 pour la 2e série, etc. (bossIndex = nombre de boss déjà apparus - 1)
    public static int BossLoop(int bossSpawnIndex)
    {
        return bossSpawnIndex / 3;
    }

    // PV du boss : +80 % par boucle (2e série x1,8, 3e x2,6...).
    public static float EndlessBossHealthMultiplier(int bossLoop)
    {
        return 1f + 0.8f * bossLoop;
    }

    // ---- Ruée de boss ----------------------------------------------------------------------------------------------------
    // ÉQUILIBRAGE (tout est ici, un seul endroit à retoucher après les playtests).
    //
    // Le problème : un boss par minute, alors que le joueur repart de zéro. Les PV du classique (20 000 / 90 000 / 180 000,
    // 90 000/180 000 relevés le 2026-10-11 - retour utilisateur : "boss 2 n'a pas assez de vie" / "pareil pour le boss 3")
    // sont faits pour un joueur de 5 / 10 / 15 minutes ; à la minute 1 ils sont impossibles. Deux leviers :
    //  1) le joueur monte plus vite : XP x RushXpMultiplier (boss compris, ils donnent beaucoup d'XP) ;
    //  2) les PV de chaque boss sont fixés d'après la puissance ATTENDUE du joueur à ce moment (voir RushBossHealth) :
    //       PV = dégâts/s attendus x temps de combat visé x marge de sécurité.
    //     Les dégâts/s attendus croissent linéairement avec la minute de la ruée (RushPlayerDpsPerMinute par minute).
    //     Le temps de combat visé augmente à chaque apparition d'un même boss (30 s, 35 s, 40 s) : « ses stats scalent ».
    public const float RushBossInterval = 60f;                // secondes de TEMPS DE JEU entre deux boss (le chrono ne tourne pas pendant un combat)
    public const float RushXpMultiplier = 1.5f;
    public const float RushPlayerDpsPerMinute = 250f;
    public const float RushSafetyFactor = 0.9f;               // < 1 : marge pour un joueur moyen / un arbre peu développé
    public const float RushHealthMultiplier = 3f;             // ajouté après playtest : boss 1 tué en 20 s, encore trop court à x2 puis encore trop court à x2
    // Temps de combat visé et dégâts du boss croissent à CHAQUE apparition de la ruée (pas seulement à chaque répétition) :
    // le 1er Boss 2 est donc toujours plus fort que le 3e Boss 1, et ainsi de suite jusqu'au 3e Boss 3.
    public const float RushKillSecondsStart = 30f;
    public const float RushKillSecondsPerSpawn = 2f;         // 30 s -> 46 s sur 9 apparitions
    public const float RushDamagePerSpawn = 0.12f;           // x1 -> x1,96 sur 9 apparitions

    public static int RushBossCount(SaveData data) { return ClassicBossProgress(data) * 3; }

    // Numéro du boss (1-3) pour la n-ième apparition (0 = 1re) : Boss 1 x3, puis Boss 2 x3, puis Boss 3 x3.
    public static int RushBossNumber(int spawnIndex) { return Mathf.Clamp(spawnIndex / 3 + 1, 1, 3); }
    public static int RushRepetition(int spawnIndex) { return Mathf.Clamp(spawnIndex % 3, 0, 2); }

    public static float RushBossHealth(int spawnIndex)
    {
        float minute = spawnIndex + 1;                          // la n-ième apparition a lieu à la minute n
        float dps = RushPlayerDpsPerMinute * minute;
        float ttk = RushKillSecondsStart + RushKillSecondsPerSpawn * spawnIndex;
        return Mathf.Round(dps * ttk * RushSafetyFactor * RushHealthMultiplier);
    }

    public static float RushBossDamageScale(int spawnIndex)
    {
        return 1f + RushDamagePerSpawn * spawnIndex;
    }

    // XP gagnée : x3 en ruée de boss, x1 ailleurs.
    // Choc des titans : aucune XP (le build est déjà complet, aucune carte à proposer).
    public static float XpMultiplier => IsBossRush ? RushXpMultiplier : IsTitans ? 0f : 1f;

    // ---- Choc des titans ------------------------------------------------------------------------------------------------
    // Le joueur démarre avec TOUTES les améliorations au maximum (comme le raccourci de debug F6) et affronte les 3 boss
    // en même temps, sans aucun ennemi normal, sans niveaux ni cartes. Les PV du classique (20k / 90k / 180k) sont faits pour
    // un joueur en cours de progression : avec un build complet, les boss 1 et 2 tomberaient en quelques secondes.
    // PV = DPS d'un build complet (TitansPlayerDps, mesuré en jeu) x temps de combat visé x marge d'utilisation
    // (TitansUptime : le joueur esquive, se repositionne, et passe d'un boss à l'autre). Un seul endroit à retoucher.
    public const float TitansPlayerDps = 5000f;
    public const float TitansUptime = 0.6f;
    private static readonly float[] TitansKillSeconds = { 25f, 40f, 60f };   // Boss 1 / Boss 2 / Boss 3 : ~2 min 05 au total

    public static float TitansBossHealth(int bossNumber)
    {
        int i = Mathf.Clamp(bossNumber, 1, 3) - 1;
        return Mathf.Round(TitansPlayerDps * TitansUptime * TitansKillSeconds[i]);
    }

    // Positions de départ (décalage par rapport au joueur, x/z) : Boss 3 en haut, Boss 1 en bas à gauche, Boss 2 en bas à droite.
    public static Vector2 TitansSpawnOffset(int bossNumber)
    {
        switch (bossNumber)
        {
            case 1: return new Vector2(-22f, -14f);
            case 2: return new Vector2(22f, -14f);
            default: return new Vector2(0f, 26f);
        }
    }

    // ---- Valeurs lues par les ennemis / projectiles / boss ---------------------------------------------------------------
    // Multiplicateur de dégâts subis (mis à jour par WaveManager). En ruée, seul un boss est en jeu pendant ses combats
    // (les ennemis normaux sont retirés) : il porte donc le multiplicateur de l'apparition en cours.
    public static float EnemyDamageScale { get; private set; } = 1f;
    private static float _rushBossDamage = 1f;

    // AJOUTÉ (2026-10-18, diagnostic complet - système de difficulté) - palier de difficulté, x1 au palier Modéré.
    // ÉTENDU (2026-10-20, carrousel de difficulté - "les boss [...] dans les autres difficultés bénéficient du
    // scale [...] logique qu'il y ait un niveau de difficulté pour ça aussi") - s'applique maintenant aussi en
    // Sans fin et Ruée de boss, plus seulement le Classique (x1 pour Choc des titans, qui n'a pas de palier).
    // Toujours inclus dans EnemyDamageScale ci-dessous, quel que soit le chemin qui l'a écrit en dernier (Sans fin,
    // Ruée de boss ou reset).
    private static float DifficultyMultiplier => (GameModes.IsClassic || GameModes.IsEndless || GameModes.IsBossRush)
        ? DifficultyTiers.DamageMultiplier(DifficultyTiers.Current) : 1f;

    public static void UpdateScaling(float runMinutes)
    {
        EnemyDamageScale = EndlessDamageScale(runMinutes) * _rushBossDamage * DifficultyMultiplier;
    }

    public static void SetRushBossDamage(float scale)
    {
        _rushBossDamage = Mathf.Max(0.1f, scale);
        EnemyDamageScale = _rushBossDamage * DifficultyMultiplier;
    }

    public static void ClearRushBossDamage()
    {
        _rushBossDamage = 1f;
        EnemyDamageScale = DifficultyMultiplier;
    }

    public static void ResetScaling()
    {
        _rushBossDamage = 1f;
        EnemyDamageScale = DifficultyMultiplier;
    }
}
