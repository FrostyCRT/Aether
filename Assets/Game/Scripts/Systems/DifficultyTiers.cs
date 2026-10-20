using UnityEngine;

public enum DifficultyTier
{
    Moderate = 0,    // palier de base - carte infinie sur les 2 axes, aucun décor de bordure
    Violent = 1,     // tunnel : mur sur Z (±20), X infini. PV ennemis/boss x1,35, dégâts x1,2, spawn x1,3, récompenses x1,3
    Apocalyptic = 2  // boîte fermée ±30 sur X et Z. PV ennemis/boss x1,8, dégâts x1,45, spawn x1,7, récompenses x1,7
}

// AJOUTÉ (2026-10-18, diagnostic complet - "créer le niveau de difficulté dans le mode classique") - 3 paliers de
// difficulté UNIQUEMENT pour le mode Classique (GameModes.IsClassic), calqués sur le patron de GameModes.cs lui-même
// (valeur statique persistée par PlayerPrefs, lue par tous les systèmes concernés). Noms choisis dans le vocabulaire
// météo français réel (modéré/violent sont de vrais termes d'intensité, cohérents avec "Aether STORM Survivor"),
// différents des mots déjà utilisés par les défis horaires (Facile/Moyen/Difficile).
//
// Chaque palier fait varier, au démarrage d'une partie Classique : la taille de la carte (ZoneHalfSize, voir
// Systems/MapBoundaryUtils.cs), le rayon/les plafonds de spawn (EnemySpawner), les PV et dégâts des ennemis/boss
// (branchés sur les points d'échelle UNIQUES déjà existants - EnemySpawner.SpawnEnemy, WaveManager.SpawnBoss,
// GameModes.UpdateScaling - plutôt que d'ajouter un multiplicateur parallèle), et l'Or/les Éclats de fin de partie
// (MetaProgressionManager). Tous les multiplicateurs ci-dessous sont des PREMIERS JETS, explicitement non
// playtestés (même traitement que RushPlayerDpsPerMinute/TitansPlayerDps dans GameModes.cs) - à retoucher après
// playtest réel.
public static class DifficultyTiers
{
    private const string PrefKey = "classic_difficulty";

    private static DifficultyTier? _cached;

    // Persisté indépendamment du mode de jeu (GameModes.Current) : un joueur qui explore Sans fin/Ruée de boss puis
    // revient au Classique retrouve le palier qu'il avait laissé.
    public static DifficultyTier Current
    {
        get
        {
            if (_cached.HasValue) return _cached.Value;
            int raw = 0;
            try { raw = PlayerPrefs.GetInt(PrefKey, 0); } catch { }
            _cached = System.Enum.IsDefined(typeof(DifficultyTier), raw) ? (DifficultyTier)raw : DifficultyTier.Moderate;
            return _cached.Value;
        }
        set
        {
            _cached = value;
            try { PlayerPrefs.SetInt(PrefKey, (int)value); } catch { }
        }
    }

    public static string TierName(DifficultyTier tier)
    {
        switch (tier)
        {
            case DifficultyTier.Violent: return Loc.T("VIOLENT");
            case DifficultyTier.Apocalyptic: return Loc.T("APOCALYPTIQUE");
            default: return Loc.T("MODÉRÉ");
        }
    }

    public static string TierDescription(DifficultyTier tier)
    {
        switch (tier)
        {
            case DifficultyTier.Violent:
                return Loc.T("Carte en tunnel sans limite de côté, ennemis plus résistants et plus dangereux, mais Or et Éclats plus généreux");
            case DifficultyTier.Apocalyptic:
                return Loc.T("Carte resserrée sur les 4 côtés, ennemis nettement plus résistants et plus dangereux, Or et Éclats au maximum");
            default:
                return Loc.T("Carte sans limite, l'expérience Classique telle qu'elle a toujours été");
        }
    }

    // MODIFIÉ (2026-10-19, diagnostic complet - refonte de la forme de carte) - ZoneHalfSize (un seul scalaire,
    // carte carrée) remplacé par des bornes indépendantes par axe (voir MapBoundaryUtils) : Modéré = infini sur les
    // 2 axes (±Infinity), Violent = tunnel (mur sur Z seulement, X infini), Apocalyptique = boîte fermée.
    // Coordonnées initiales données par l'utilisateur (captures d'écran de murs-témoins dans la scène : Z=20/-20
    // pour Violent, Z=30/-30 et X=30/-30 pour Apocalyptique).
    // RÉDUIT (2026-10-19, session esthétique, retour utilisateur : "je trouve qu'il y a trop d'espace... baisse la
    // valeur de 5") - Violent Z=±20 -> ±15 (moins d'écart entre les 2 murs du tunnel), Apocalyptique ±30 -> ±25 sur
    // les 2 axes (boîte plus resserrée).
    // RÉDUIT ENCORE (2026-10-19, retour utilisateur : "raccourci encore un peu la range entre chaque côté pour les
    // murs invisible") - Violent Z=±15 -> ±12, Apocalyptique ±25 -> ±22 sur les 2 axes.
    public static void GetBounds(DifficultyTier tier, out float minX, out float maxX, out float minZ, out float maxZ)
    {
        switch (tier)
        {
            case DifficultyTier.Violent:
                minX = float.NegativeInfinity;
                maxX = float.PositiveInfinity;
                minZ = -12f;
                maxZ = 12f;
                break;
            case DifficultyTier.Apocalyptic:
                minX = -22f;
                maxX = 22f;
                minZ = -22f;
                maxZ = 22f;
                break;
            default:
                minX = float.NegativeInfinity;
                maxX = float.PositiveInfinity;
                minZ = float.NegativeInfinity;
                maxZ = float.PositiveInfinity;
                break;
        }
    }

    // Multiplie _spawnRadius/_bulbSpawnRadius et chaque plafond maxEnemiesSurMap de phase (EnemySpawner).
    // RÉDUIT (2026-10-19, retour utilisateur - vraie partie jouée en Apocalyptique, arbre de compétences complet +
    // Réputation au max : "beaucoup trop dure et mal équilibré... il y a aussi bien trop d'ennemis, comme la map
    // est petite, les ennemis se pack rapidement, et quand c'est les tireurs, c'est presque inesquivable") - ce
    // multiplicateur venait du PREMIER plan ("une carte plus grande doit accueillir plus d'ennemis pour rester
    // aussi dense"), où Apocalyptique était PLUS GRAND que Modéré (±90 contre une carte infinie). Le plan a changé
    // en cours de route (voir GetBounds) : Apocalyptique est maintenant une boîte FERMÉE et plus PETITE que
    // Modéré (±22, rétrécie 2 fois sur retour utilisateur), alors que ce multiplicateur était resté à ×1,7 - plus
    // d'ennemis que la normale dans un espace plus petit que la normale, cumulé avec des murs qui empêchent toute
    // fuite : c'est la cause directe du "pack" et des tirs inesquivables. Ramené à peine au-dessus de x1 (le
    // plafond d'ennemis simultanés ne doit plus augmenter avec le palier tant que la carte rétrécit avec lui).
    public static float SpawnScaleMultiplier(DifficultyTier tier)
    {
        switch (tier)
        {
            case DifficultyTier.Violent: return 1.1f;
            case DifficultyTier.Apocalyptic: return 1.15f;
            default: return 1f;
        }
    }

    // Multiplie les PV des ennemis normaux (EnemySpawner.SpawnEnemy, aux côtés du palier et de GameModes.EndlessHealthScale)
    // et des boss (WaveManager.SpawnBoss, aux côtés de SetEndlessHealthMultiplier/SetHealthOverride).
    // RÉDUIT (2026-10-19, même retour utilisateur que SpawnScaleMultiplier ci-dessus) - en plus du problème de
    // densité, des PV à x1,8 veut dire que chaque ennemi reste en vie (donc sur la carte, donc dans le "pack")
    // bien plus longtemps avant de mourir, ce qui aggrave encore l'encombrement une fois la densité elle-même
    // corrigée. Adouci pour qu'un build complet (arbre de compétences + Réputation au max, comme testé) tue
    // encore assez vite pour empêcher l'accumulation.
    public static float HealthMultiplier(DifficultyTier tier)
    {
        switch (tier)
        {
            case DifficultyTier.Violent: return 1.25f;
            case DifficultyTier.Apocalyptic: return 1.5f;
            default: return 1f;
        }
    }

    // AJOUTÉ (2026-10-20, retour utilisateur : "les boss 1 et 2 n'ont pas assez de vie en difficulté 3, il faut la
    // doubler, pareil pour les mini boss") - jusqu'ici WaveManager.SpawnBoss appliquait HealthMultiplier ci-dessus
    // aux boss, EXACTEMENT le même multiplicateur que les ennemis normaux (x1,5 en Apocalyptique). Un combat de
    // boss dure bien plus longtemps et bénéficie beaucoup moins de l'esquive/du kiting qu'un simple ennemi dans la
    // masse - x1,5 se sent trop faible une fois testé en vrai combat de boss. Boss dédié, Violent inchangé (jamais
    // signalé comme trop faible), Apocalyptique doublé (x2, pas juste +50%). Les mini-boss invoqués par le Boss 3
    // (BossCorruptedSource.InitSummonedBoss) héritent automatiquement de ce changement : ils sont une fraction du
    // _maxHealth DÉJÀ multiplié du Boss 3 (InitWithReducedHP), jamais recalculés depuis une base indépendante.
    public static float BossHealthMultiplier(DifficultyTier tier)
    {
        switch (tier)
        {
            case DifficultyTier.Violent: return 1.25f;
            case DifficultyTier.Apocalyptic: return 2f;
            default: return 1f;
        }
    }

    // Multiplie les dégâts infligés AU JOUEUR par les ennemis/boss (GameModes.EnemyDamageScale, via UpdateScaling) -
    // monte moins vite que les PV, même philosophie que GameModes.EndlessDamageScale ("pour rester jouable").
    // RÉDUIT (2026-10-19, même retour utilisateur que ci-dessus) - dans une boîte fermée resserrée, un joueur
    // encerclé par des tireurs (dégâts venant de tous les angles, aucune retraite possible derrière un mur) subit
    // ce multiplicateur BEAUCOUP plus souvent qu'à ciel ouvert ; adouci en conséquence.
    public static float DamageMultiplier(DifficultyTier tier)
    {
        switch (tier)
        {
            case DifficultyTier.Violent: return 1.15f;
            case DifficultyTier.Apocalyptic: return 1.3f;
            default: return 1f;
        }
    }

    // Multiplie l'Or (MetaProgressionManager.AddRunGold) et les Éclats (CalculateEclatsEarned) de fin de partie - en
    // Classique, Sans fin et Ruée de boss (ÉTENDU 2026-10-20, carrousel de difficulté, voir HealthMultiplier plus
    // haut), x1 au palier Modéré. Le risque accru du palier doit être payant.
    // RÉDUIT (2026-10-20, retour utilisateur : "en une partie réussie en mode apocalyptique, j'ai gagné 45 000 d'or,
    // c'est pas un peu énorme ? [...] sans le bonus de quête") - 1,7 à l'Apocalyptique jugé trop généreux une fois
    // confronté au coût réel de la progression : la totalité de l'arbre de compétences d'un personnage (tous les
    // nœuds, tous les paliers, voir MetaProgressionManager.GetNodeCost/GetLevelCost/GetReputationLevelCost) coûte
    // environ 15 000-20 000 Or au total - 45 000 en UNE SEULE partie permettait de tout débloquer d'un coup, ce qui
    // vide le sens d'une progression étalée sur plusieurs parties. Ramené à 1,5 (-12%) : garde un vrai bonus pour le
    // palier le plus dur (toujours au-dessus de Violent, avec le même écart proportionnel que les autres
    // multiplicateurs du palier - voir HealthMultiplier/DamageMultiplier ci-dessus, tous ~x1,15-1,2 par rapport à
    // Violent), sans permettre de rafler la quasi-totalité de l'arbre en une partie.
    public static float RewardMultiplier(DifficultyTier tier)
    {
        switch (tier)
        {
            case DifficultyTier.Violent: return 1.3f;
            case DifficultyTier.Apocalyptic: return 1.5f;
            default: return 1f;
        }
    }

    // MODIFIÉ (2026-10-19, diagnostic complet - refonte de la forme de carte) - retour utilisateur : les décors de
    // bordure du Modéré sont de vrais modèles 3D, visuellement bizarre à traverser dans un survivor en monde
    // "infini" (même sans collision, voir BoundaryDecorationPlacer) - décision : AUCUN décor en Modéré (carte
    // infinie, rien à border). Violent/Apocalyptique gardent du décor, mais plus comme une "ceinture" proportionnée
    // à une carte carrée : Violent suit le mur du tunnel en continu (généré dynamiquement par zones le long de
    // l'axe X infini, voir Systems/InfiniteDecorStreamer.cs), Apocalyptique remplit toute la boîte fermée
    // (voir BoundaryDecorationPlacer.PlaceDecorationsInBox) - decorCount y est un PLAFOND haut, le nombre réel
    // posé dépend de ce qui tient sans superposition (_minDistanceBetweenDecor).
    public static int ApocalypticDecorCount => 500;

    // AJOUTÉ (2026-10-19, session esthétique - retour utilisateur : "la lumière est blanche neutre, aucune
    // identité") - teinte la lumière directionnelle par palier pour renforcer l'identité "Fantasy Médiéval avec
    // magie et mana" (au lieu du blanc neutre 6570K d'origine, jamais retouché). Appliqué par
    // GameSceneSettingsApplier.Start(), AUSSI pour les modes non Classiques (qui utilisent alors Modéré comme
    // teinte par défaut - purement visuel, DifficultyTiers n'a aucun effet de jeu hors Classique).
    public static Color LightColor(DifficultyTier tier)
    {
        switch (tier)
        {
            case DifficultyTier.Violent: return new Color(1f, 0.72f, 0.45f);        // ambre/orage
            case DifficultyTier.Apocalyptic: return new Color(0.85f, 0.5f, 0.85f);  // violet arcanique
            default: return new Color(1f, 0.93f, 0.78f);                           // or chaud
        }
    }

    public static float LightIntensity(DifficultyTier tier)
    {
        switch (tier)
        {
            case DifficultyTier.Violent: return 1.1f;
            case DifficultyTier.Apocalyptic: return 1.2f;
            default: return 1f;
        }
    }

    // Couleur HDR (volontairement > 1 par canal) des motes de mana ambiantes (voir AmbientManaMotes) : même teinte
    // que la lumière ci-dessus pour une ambiance cohérente, mais poussée au-delà de 1 pour accrocher le Bloom
    // (voir GameAmbienceProfile) même à travers un sprite alpha-blendé classique (Sprites/Default, via FXSprites) -
    // aucun nouveau shader/texture nécessaire, Unity ne borne pas Color à [0,1] avant la carte graphique.
    public static Color AmbientMoteColor(DifficultyTier tier)
    {
        const float boost = 2.6f;
        Color baseColor = LightColor(tier);
        return new Color(baseColor.r * boost, baseColor.g * boost, baseColor.b * boost, 1f);
    }
}
