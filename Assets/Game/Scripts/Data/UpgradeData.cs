using UnityEngine;

[CreateAssetMenu(fileName = "SO_Upgrade", menuName = "BulletHeaven/Upgrade")]
public class UpgradeData : ScriptableObject
{
    [Header("Infos")]
    public string upgradeName;
    public string description;

    [Header("Icône")]
    public Sprite icon;

    [Header("Effet")]
    public UpgradeType upgradeType;
    public float value;

    public UpgradeBranch Branch
    {
        get
        {
            switch (upgradeType)
            {
                case UpgradeType.Fireball: return UpgradeBranch.Aether;
                case UpgradeType.AuraUpgrade: return UpgradeBranch.Kael;
                case UpgradeType.Knives: return UpgradeBranch.Lyra;
                // Une fusion prend la couleur du personnage concerné si elle en a un (réglé à la main par fusion, voir
                // _fusionBranch) — sinon Universal, comme les armes universelles dont elle est faite.
                case UpgradeType.Fusion: return _fusionBranch;
                default: return UpgradeBranch.Universal;
            }
        }
    }

    // ---- Fusions (2026-09-27) --------------------------------------------------------------------------------------------
    // Une fusion combine 2 armes UNE FOIS TOUTES LES DEUX AU PALIER MAX en une arme unique, plus forte, qui les remplace.
    // Système générique : chaque fusion est un asset UpgradeData de type Fusion, qui référence ses 2 armes sources
    // (_fusionSource1/2) et un identifiant (_fusionResultId) qui dit à ApplyFusionResult() quel composant d'arme créer.
    // Ajouter une fusion = un nouvel asset + un nouveau "case" dans ApplyFusionResult() (et dans les cases Damage/FireRate
    // ci-dessous si la nouvelle arme doit continuer de profiter des futurs achats de dégâts/cadence).
    [Header("Fusion (uniquement pour UpgradeType.Fusion)")]
    [Tooltip("Les 2 upgrades qui doivent être TOUTES LES DEUX au palier maximum pour que cette fusion soit proposée.")]
    [SerializeField] private UpgradeType _fusionSource1;
    [SerializeField] private UpgradeType _fusionSource2;
    // Lus par GameUI/PauseMenuUI pour regrouper les 2 armes sources sur la même ligne de la grille 3x3 (voir
    // GameUI.GetArsenalRows), même avant que la fusion soit complétée.
    public UpgradeType FusionSource1 => _fusionSource1;
    public UpgradeType FusionSource2 => _fusionSource2;
    [Tooltip("Identifiant lu par UpgradeData.ApplyFusionResult() pour savoir quelle arme fusionnée créer (ex: \"ScorchedEarth\").")]
    [SerializeField] private string _fusionResultId;
    [Tooltip("Couleur de tuile dans l'arsenal (écrans de fin). Aether/Kael/Lyra si la fusion est réservée à ce personnage, Universal sinon.")]
    [SerializeField] private UpgradeBranch _fusionBranch = UpgradeBranch.Universal;

    [Header("Valeurs par palier (Fireball / AuraUpgrade / Knives uniquement)")]
    [Tooltip("Utilisé UNIQUEMENT par Fireball/AuraUpgrade/Knives, dont les 3 paliers ont des effets différents (contrairement aux autres cartes qui répètent le même effet à chaque pick). Index 0 = palier 1, index 1 = palier 2, index 2 = palier 3. Ignoré par tous les autres UpgradeType, qui continuent d'utiliser le champ 'value' ci-dessus.")]
    [SerializeField] private float[] _levelValues = new float[3];

    private float GetLevelValue(int level)
    {
        int index = Mathf.Clamp(level - 1, 0, _levelValues.Length - 1);
        return _levelValues.Length > 0 ? _levelValues[index] : 0f;
    }

    [Header("Niveaux")]
    [Tooltip("Nombre de PALIERS D'AMÉLIORATION affichés dans l'UI (3 par défaut). N'est PAS utilisé par DoubleShot, qui gère son propre déblocage unique via IsDoubleShotUnlocked().")]
    [SerializeField] private int _maxLevel = 3;
    public int MaxLevel => _maxLevel;

    [Tooltip("Coche cette case UNIQUEMENT pour les capacités qui n'existent pas au spawn et doivent être débloquées par un premier pick (Orbital, Lightning, Boue). Ce premier pick ne compte pas comme un des paliers d'amélioration ci-dessus — il s'ajoute en plus. Laisse décoché pour les armes exclusives personnage (déjà équipées au spawn) et les autres cartes à effet immédiat.")]
    [SerializeField] private bool _requiresUnlockPick = false;

    public bool RequiresUnlockPick => _requiresUnlockPick;

    private int TotalAllowedPicks => _requiresUnlockPick ? _maxLevel + 1 : _maxLevel;

    private GameObject GetActivePlayer()
    {
        return GameObject.FindWithTag("Player");
    }

    public int GetCurrentLevel()
    {
        return LevelUpManager.Instance != null ? LevelUpManager.Instance.GetLevel(this) : 0;
    }

    public int GetDisplayLevel()
    {
        int raw = GetCurrentLevel();
        return _requiresUnlockPick ? Mathf.Max(0, raw - 1) : raw;
    }

    public bool IsUnlocked()
    {
        return !_requiresUnlockPick || GetCurrentLevel() >= 1;
    }

    private bool IsMaxed()
    {
        return GetCurrentLevel() >= TotalAllowedPicks;
    }

    public string GetDynamicDescription()
    {
        int nextLevel = Mathf.Min(GetCurrentLevel() + 1, TotalAllowedPicks);

        switch (upgradeType)
        {
            case UpgradeType.Fireball:
                return FormatFireballDescription(nextLevel);

            case UpgradeType.AuraUpgrade:
                return FormatAuraDescription(nextLevel);

            case UpgradeType.Knives:
                return FormatKnivesDescription(nextLevel);

            case UpgradeType.Orbital:
                return nextLevel == 1
                    ? Loc.T("Débloque 2 orbitaux tournants qui frappent au contact")
                    : Loc.T("+1 orbital supplémentaire");

            case UpgradeType.Lightning:
                return nextLevel == 1
                    ? Loc.T("Débloque la foudre en chaîne : elle frappe 3 ennemis")
                    : Loc.T("+1 rebond de foudre supplémentaire");

            case UpgradeType.MudPuddle:
                return nextLevel == 1
                    ? Loc.T("Débloque 3 flaques de boue qui ralentissent et blessent")
                    : Loc.T("+1 flaque de boue supplémentaire");

            case UpgradeType.BouncingOrb:
                return FormatBouncingOrbDescription(nextLevel);

            case UpgradeType.Damage:
                return Loc.F("+{0}% de dégâts sur toutes tes armes", PercentOf(value));

            case UpgradeType.FireRate:
                // CORRIGÉ (2026-10-25) - "toutes tes armes" était faux : seules les armes qui TIRENT ont une cadence (tir de
                // base, Boule de feu, Couteaux, Foudre, Lames Ricochet - voir Apply plus bas), pas l'Aura, les Orbitaux, etc.
                return Loc.F("+{0}% de cadence sur toutes tes armes de tir", PercentOf(value));

            // MODIFIÉ (2026-10-27, retour utilisateur : "la compétence de soin devrait augmenter la santé maximum [...]
            // on pourrait soigner, et augmenter la vie max de quelques pourcentage, genre 25%") - voir Apply.
            // MODIFIÉ (2026-10-28, retour utilisateur : "baisse le pourcentage de vie max, passe de +25% à +15%, et met
            // que ça restaure la vie entière, pas 50%") - soin complet (value = 1 dans SO_Upgrade_Heal).
            case UpgradeType.Heal:
                return value >= 0.999f
                    ? Loc.F("Augmente tes PV max de {0}% et restaure tous tes PV", PercentOf(HealMaxHealthBonus))
                    : Loc.F("Augmente tes PV max de {0}% et en restaure {1}%", PercentOf(HealMaxHealthBonus), PercentOf(value));

            case UpgradeType.DoubleShot:
                // CORRIGÉ (2026-10-25) - le second tir n'est pas simultané : il part _doubleShotDelay (0,1 s) après le premier.
                return Loc.T("Débloque le Double Tir : ton tir de base tire deux fois");

            default:
                return Description;
        }
    }

    // MODIFIE - refonte complete de l'identite de Fireball : palier 1 = rayon
    // d'explosion (plus une "chance d'explosion", qui est desormais garantie a
    // 100% des la base, voir WeaponFireball.cs), palier 2 = degats, palier 3 =
    // debloque la Brulure (effet a la duree, pas un chiffre a afficher).
    // IMPORTANT - les 3 valeurs dans _levelValues de l'asset Fireball doivent etre
    // remises a jour dans l'Inspector pour correspondre au nouveau sens de chaque
    // index : [0] = increment de rayon en metres (flat, ex: 1.0), [1] = ratio de
    // degats (ex: 0.35 pour +35%), [2] = ignore desormais (EnableBurn n'a pas
    // besoin de valeur), laisse-le a 0.
    // MODIFIE - Fireball passe en upgrade a deblocage separe : le personnage ne
    // spawn plus avec elle equipee, le premier pick la debloque (equipe l'arme),
    // les 3 picks suivants sont les paliers rayon/degats/brulure (decales de +1
    // par rapport a avant, tier = level-1).
    // AJOUTÉ (2026-10-20, retour utilisateur : "le +1 au rayon de veut rien dire pour le joueur, il ne sait pas ce
    // que représente 1") - "+1" (mètres) était une unité abstraite sans repère pour le joueur, contrairement à un
    // "+1 orbital"/"+1 rebond" ailleurs dans ce fichier qui désigne un OBJET concret et comptable.
    // MODIFIÉ (2026-10-21, retour utilisateur : "+44% c'est pas assez, mets à la place qu'il est 2 fois plus grand,
    // comme ça on voit une vraie différence en jeu, et c'est plus clair à l'écrit") - le passage en pourcentage
    // (+44%) réglait le problème de lisibilité mais pas celui de puissance ressentie. `_levelValues[0]` dans l'asset
    // Fireball est passé de 1 (mètre) à 2,25 (= `FireballBaseFragmentRadius` ci-dessous, l'incrément EXACT du
    // rayon de base) : `WeaponFireball.AddFragmentRadius` étant additif, ce pick double maintenant VRAIMENT le
    // rayon (2,25 -> 4,5), pas juste "+44%". Texte figé ("doublé"), plus besoin de calcul dynamique puisque la
    // VALEUR elle-même est conçue pour toujours représenter un doublement exact depuis cette base, même registre
    // que "Débloque la Brûlure" juste en dessous (effet décrit en mots, pas en chiffre).
    private const float FireballBaseFragmentRadius = 2.25f;

    // Carte Soin (2026-10-27) : PV max gagnés à chaque pick, en fraction des PV max de départ (5 picks = +75 %).
    // MODIFIÉ (2026-10-28) - 0,25 -> 0,15 (retour utilisateur), le soin devenant complet.
    public const float HealMaxHealthBonus = 0.15f;

    private string FormatFireballDescription(int level)
    {
        if (level == 1)
            return Loc.T("Débloque Boule de feu : équipe l'arme exclusive d'Aether");

        int tier = level - 1;
        float v = GetLevelValue(tier);
        switch (tier)
        {
            case 1: return Loc.T("Le rayon d'explosion est 2 fois plus grand");
            case 2: return Loc.F("+{0}% dégâts", PercentOf(v));
            case 3: return Loc.T("Débloque la Brûlure : les ennemis touchés brûlent sur la durée");
            default: return Description;
        }
    }

    // MODIFIE - meme principe que Fireball : AuraUpgrade passe en deblocage separe.
    private string FormatAuraDescription(int level)
    {
        if (level == 1)
            return Loc.T("Débloque l'Aura : équipe l'arme exclusive de Kael");

        int tier = level - 1;
        float v = GetLevelValue(tier);
        switch (tier)
        {
            case 1: return Loc.F("+{0}% dégâts", PercentOf(v));
            case 2: return Loc.F("+{0}% rayon", PercentOf(v));
            // CORRIGÉ (2026-10-25) - "+50% ralentissement" ne disait pas la réalité : la valeur se RETRANCHE du
            // multiplicateur de vitesse, elle n'est pas un pourcentage du ralentissement.
            // MODIFIÉ (2026-10-28, retour utilisateur : "On garde vraiment ce 'Au lieu de X' ? Je ne vois pas ça souvent
            // dans les jeux pro") - n'affiche plus que la valeur atteinte (15% -> 40%, voir WeaponAura._minSlowMultiplier).
            case 3:
                {
                    GameObject player = GetActivePlayer();
                    WeaponAura aura = player != null ? player.GetComponent<WeaponAura>() : null;
                    if (aura == null) return Description;
                    return Loc.F("Ralentissement porté à {0}%", aura.SlowPercentAfter(v));
                }
            default: return Description;
        }
    }

    // MODIFIE - refonte de l'identite de Knives : palier 1 = +1 couteau dans la
    // salve (l'identite meme de l'arme, voir WeaponShurikenBarrage.cs), palier 2 =
    // degats, palier 3 = perforation (deplace du palier 2 vers le 3).
    // IMPORTANT - les 3 valeurs dans _levelValues de l'asset Knives doivent etre
    // remises a jour dans l'Inspector : [0] = ignore desormais (AddKnife() n'a pas
    // besoin de valeur), laisse-le a 0, [1] = ratio de degats (ex: 0.35), [2] =
    // nombre d'ennemis perfores en plus (ex: 2).
    // MODIFIE - meme principe : Knives passe en deblocage separe. Le tout premier
    // pick equipe la salve (2 couteaux de base), le palier "+1 couteau" qui
    // occupait avant le niveau 1 est repousse au tier 1 (donc niveau brut 2).
    private string FormatKnivesDescription(int level)
    {
        if (level == 1)
            return Loc.T("Débloque la Salve de Couteaux : équipe l'arme exclusive de Lyra");

        int tier = level - 1;
        float v = GetLevelValue(tier);
        switch (tier)
        {
            case 1: return Loc.T("Débloque un couteau supplémentaire dans la salve");
            case 2: return Loc.F("+{0}% dégâts", PercentOf(v));
            case 3: { int pierce = Mathf.Max(1, Mathf.RoundToInt(v)); return pierce > 1 ? Loc.F("+{0} ennemis perforés en plus", pierce) : Loc.F("+{0} ennemi perforé en plus", pierce); }
            default: return Description;
        }
    }

    // CORRIGE (2026-09-15) - BouncingOrb etait le seul parmi les armes
    // exclusives a deblocage separe (Fireball/AuraUpgrade/Knives) a ne PAS
    // suivre le decalage "tier = level - 1" : son palier brut 1 appliquait
    // deja un bonus de degats silencieux (alors que sa description disait
    // "Débloque...", sans mention de degats) et son palier 4 (le dernier des
    // TotalAllowedPicks=4, vu que _requiresUnlockPick=true) tombait dans le
    // defaut - aucun texte, aucun effet (retour utilisateur : avertissement
    // "palier 4 inattendu" en jeu). Meme structure que FormatKnivesDescription
    // desormais : palier brut 1 = deblocage pur (aucun chiffre), paliers 2/3/4
    // = tiers 1/2/3 (degats/vitesse/+1 orbe).
    private string FormatBouncingOrbDescription(int level)
    {
        if (level == 1)
            return Loc.T("Débloque un orbe rebondissant qui traverse les ennemis");

        int tier = level - 1;
        float v = GetLevelValue(tier);
        switch (tier)
        {
            case 1: return Loc.F("+{0}% dégâts", PercentOf(v));
            case 2: return Loc.F("+{0}% vitesse des orbes", PercentOf(v));
            case 3: return Loc.T("+1 orbe rebondissant supplémentaire");
            default: return Description;
        }
    }

    private int PercentOf(float ratio) => Mathf.RoundToInt(ratio * 100f);

    // AJOUTÉ (2026-10-26, retour utilisateur : "l'info-bulle de l'orbe rebondissant dit que ça lance 2 orbes, alors qu'en
    // jeu ça en lance qu'une [...] adapte les info-bulles par rapport à la globalité de l'état actuel de la compétence")
    // - texte de l'info-bulle du menu Pause : ce que la compétence fait MAINTENANT (nombre d'orbes, d'éclats, de flaques,
    // de couteaux, de rebonds, bonus total de Dégâts+ / Cadence+), lu sur l'arme du joueur. À ne pas confondre avec
    // GetDynamicDescription (le PROCHAIN palier, cartes de montée de niveau) ni avec Description (texte générique de
    // l'asset, toujours utilisé pour les fusions et les armes dont le texte ne dépend pas du niveau).
    public string GetCurrentStateDescription()
    {
        GameObject player = GetActivePlayer();
        LevelUpManager levels = LevelUpManager.Instance;
        switch (upgradeType)
        {
            case UpgradeType.Damage:
                return levels != null ? Loc.F("+{0}% de dégâts sur toutes tes armes", PercentOf(levels.DamageBonusTotal)) : Description;
            case UpgradeType.FireRate:
                return levels != null ? Loc.F("+{0}% de cadence sur toutes tes armes de tir", PercentOf(levels.FireRateBonusTotal)) : Description;
            case UpgradeType.BouncingOrb:
                {
                    WeaponBouncingOrb orb = player != null ? player.GetComponent<WeaponBouncingOrb>() : null;
                    if (orb == null) return Description;
                    return Loc.Plural(orb.OrbCount,
                        "Lance un orbe qui rebondit sans arrêt dans l'arène et blesse les ennemis touchés",
                        "Lance {0} orbes qui rebondissent sans arrêt dans l'arène et blessent les ennemis touchés");
                }
            // Au moins 2 dès le déblocage : la forme du pluriel suffit (passée deux fois à Loc.Plural pour le polonais et le russe).
            case UpgradeType.Orbital:
                {
                    WeaponOrbital orbital = player != null ? player.GetComponent<WeaponOrbital>() : null;
                    if (orbital == null) return Description;
                    const string key = "{0} éclats tournent autour de toi et frappent les ennemis au contact";
                    return Loc.Plural(orbital.OrbitalCount, key, key);
                }
            case UpgradeType.MudPuddle:
                {
                    WeaponMudPuddle mud = player != null ? player.GetComponent<WeaponMudPuddle>() : null;
                    if (mud == null) return Description;
                    const string key = "Fait apparaître {0} flaques de boue qui ralentissent et blessent les ennemis qui s'y trouvent";
                    return Loc.Plural(mud.PuddleCount, key, key);
                }
            case UpgradeType.Knives:
                {
                    WeaponShurikenBarrage knives = player != null ? player.GetComponent<WeaponShurikenBarrage>() : null;
                    if (knives == null) return Description;
                    return Loc.F("Lance des salves de {0} couteaux qui transpercent les ennemis", knives.KnifeCount);
                }
            case UpgradeType.Lightning:
                {
                    WeaponLightningChain chain = player != null ? player.GetComponent<WeaponLightningChain>() : null;
                    if (chain == null) return Description;
                    return Loc.F("Décharge électrique qui rebondit sur {0} ennemis proches", chain.ChainCount);
                }
            default:
                return Description;
        }
    }

    // AJOUTÉ (2026-10-14) - textes à AFFICHER dans la langue courante (voir Loc). upgradeName / description restent les
    // valeurs françaises de l'asset (clés de traduction).
    public string Name => Loc.T(upgradeName);
    public string Description => Loc.T(description);

    // AJOUTE - verifie que le personnage actuellement joue correspond bien au
    // personnage proprietaire de cette upgrade exclusive.
    private bool IsForCharacter(GameObject player, CharacterType expectedType)
    {
        CharacterIdentity identity = player.GetComponent<CharacterIdentity>();
        return identity != null && identity.Type == expectedType;
    }

    public bool IsAvailable()
    {
        GameObject player = GetActivePlayer();
        if (player == null) return false;

        switch (upgradeType)
        {
            // MODIFIE - Double Tir cible maintenant WeaponBase (le tir de base commun
            // aux 3 personnages), plus Fireball/Knives. C'etait invers avant : ces
            // deux armes exclusives n'ont plus de notion de double tir depuis la
            // refonte (explosion garantie + Brulure pour Fireball, salve deja
            // multi-projectiles pour Knives - dupliquer n'aurait plus de sens).
            case UpgradeType.DoubleShot:
                {
                    WeaponBase baseWeaponDS = player.GetComponent<WeaponBase>();
                    if (baseWeaponDS != null) return !baseWeaponDS.IsDoubleShotUnlocked();
                    return false;
                }

            // Limité à MaxLevel paliers (5) comme Dégâts / Cadence (avant : illimité, servait de carte de secours).
            case UpgradeType.Heal:
                return !IsMaxed();

            case UpgradeType.Damage:
                return !IsMaxed();

            case UpgradeType.FireRate:
                return !IsMaxed();

            // CORRIGE - la verification GetComponent<WeaponX>() != null avait ete
            // retiree entierement, alors qu'elle servait DEUX roles a la fois : eviter
            // de proposer la carte avant le premier pick (correct de la retirer, le
            // composant n'existe justement pas encore) ET filtrer par personnage
            // (incorrect de la retirer - consequence reelle observee : la carte Aura
            // apparaissait en jouant Lyra). Remplace par une verification explicite du
            // personnage actuellement joue via CharacterIdentity, qui ne depend plus de
            // l'existence du composant.
            case UpgradeType.Fireball:
                return IsForCharacter(player, CharacterType.Aether) && !IsMaxed();

            case UpgradeType.AuraUpgrade:
                return IsForCharacter(player, CharacterType.Kael) && !IsMaxed();

            case UpgradeType.Knives:
                return IsForCharacter(player, CharacterType.Lyra) && !IsMaxed();

            case UpgradeType.Orbital:
                return !IsMaxed();

            case UpgradeType.Lightning:
                return !IsMaxed();

            case UpgradeType.MudPuddle:
                return !IsMaxed();

            case UpgradeType.BouncingOrb:
                return !IsMaxed();

            // Fusion : proposée une seule fois (GetCurrentLevel jamais > 1, maxLevel = 1), seulement en Classique/Sans fin
            // (voir GameModes.FusionsEnabled — pas en Ruée de boss/Choc des titans, trop courts pour y prétendre), et
            // seulement quand les 2 armes sources sont TOUTES LES DEUX au palier max.
            // MODIFIÉ (2026-10-23) - et seulement à partir de 10 min de partie (voir GameModes.FusionsUnlockedNow).
            case UpgradeType.Fusion:
                return GameModes.FusionsUnlockedNow && GetCurrentLevel() == 0 && BothFusionSourcesMaxed();

            default:
                return true;
        }
    }

    // Retrouve, parmi toutes les upgrades connues du run, celle du type donné (un seul asset par UpgradeType de base).
    private UpgradeData FindUpgradeByType(UpgradeType type)
    {
        if (LevelUpManager.Instance == null || LevelUpManager.Instance.AllUpgrades == null) return null;
        foreach (UpgradeData u in LevelUpManager.Instance.AllUpgrades)
            if (u != null && u.upgradeType == type) return u;
        return null;
    }

    // AJOUTÉ (2026-10-25) - « Orbitaux + Foudre » : les deux armes d'une fusion, affichées sous son nom sur la carte de
    // montée de niveau (la description ne les répète plus). Vide si ce n'est pas une fusion.
    public string FusionSourcesLabel()
    {
        if (upgradeType != UpgradeType.Fusion) return "";
        UpgradeData s1 = FindUpgradeByType(_fusionSource1);
        UpgradeData s2 = FindUpgradeByType(_fusionSource2);
        if (s1 == null || s2 == null) return "";
        return s1.Name + " + " + s2.Name;
    }

    private bool BothFusionSourcesMaxed()
    {
        UpgradeData s1 = FindUpgradeByType(_fusionSource1);
        UpgradeData s2 = FindUpgradeByType(_fusionSource2);
        if (s1 == null || s2 == null) return false;
        if (s1.GetDisplayLevel() < s1.MaxLevel || s2.GetDisplayLevel() < s2.MaxLevel) return false;

        // CORRIGE (2026-09-27) - certaines armes universelles sont sources de PLUSIEURS fusions différentes (ex: la
        // Foudre pour "Orbes Foudroyants" ET "Marécage Maudit") : une seule peut jamais se compléter par partie, la
        // première détruit le composant. Sans ce contrôle, l'autre fusion restait "disponible" pour toujours (le
        // niveau de l'upgrade source, lui, ne redescend jamais) et sa sélection n'aurait produit aucun effet.
        GameObject player = GetActivePlayer();
        return player != null && SourceStillEquipped(player, _fusionSource1) && SourceStillEquipped(player, _fusionSource2);
    }

    // DoubleShot n'a pas de composant propre (il modifie WeaponBase, qui reste toujours présent) : toujours considéré
    // comme équipé tant qu'il a été pris une fois. Pour les autres, le composant doit encore exister sur le joueur.
    private static bool SourceStillEquipped(GameObject player, UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.Fireball: return player.GetComponent<WeaponFireball>() != null;
            case UpgradeType.AuraUpgrade: return player.GetComponent<WeaponAura>() != null;
            case UpgradeType.Knives: return player.GetComponent<WeaponShurikenBarrage>() != null;
            case UpgradeType.Orbital: return player.GetComponent<WeaponOrbital>() != null;
            case UpgradeType.Lightning: return player.GetComponent<WeaponLightningChain>() != null;
            case UpgradeType.MudPuddle: return player.GetComponent<WeaponMudPuddle>() != null;
            case UpgradeType.BouncingOrb: return player.GetComponent<WeaponBouncingOrb>() != null;
            default: return true;
        }
    }

    public void Apply()
    {
        GameObject playerGO = GetActivePlayer();
        if (playerGO == null) return;

        int newLevel = LevelUpManager.Instance != null ? LevelUpManager.Instance.IncrementLevel(this) : 1;

        PlayerController player = playerGO.GetComponent<PlayerController>();
        WeaponBase weapon = playerGO.GetComponent<WeaponBase>();
        HealthSystem health = playerGO.GetComponent<HealthSystem>();

        switch (upgradeType)
        {
            // MODIFIE - Fireball en deblocage separe : le premier pick (newLevel == 1)
            // cree uniquement l'arme (AddComponent), aucun effet stat ce pick-ci -
            // exactement le meme principe qu'Orbital/Lightning plus bas. Les paliers
            // rayon/degats/Brulure sont decales d'un cran (tier = newLevel - 1).
            case UpgradeType.Fireball:
                {
                    WeaponFireball fireball = playerGO.GetComponent<WeaponFireball>();

                    if (newLevel == 1)
                    {
                        if (fireball == null)
                            ApplyPastGlobalBonuses(playerGO.AddComponent<WeaponFireball>());
                        break;
                    }

                    if (fireball == null) break;

                    int tier = newLevel - 1;
                    float v = GetLevelValue(tier);
                    switch (tier)
                    {
                        case 1: fireball.AddFragmentRadius(v); break;
                        case 2: fireball.AddDamage(v); break;
                        case 3: fireball.EnableBurn(); break;
                        default:
                            Debug.LogWarning($"[UpgradeData] Fireball : palier {newLevel} inattendu (max {TotalAllowedPicks}), aucun effet appliqué.");
                            break;
                    }
                    break;
                }

            // MODIFIE - meme principe que Fireball : deblocage separe.
            case UpgradeType.AuraUpgrade:
                {
                    WeaponAura aura = playerGO.GetComponent<WeaponAura>();

                    if (newLevel == 1)
                    {
                        if (aura == null)
                            ApplyPastGlobalBonuses(playerGO.AddComponent<WeaponAura>());
                        break;
                    }

                    if (aura == null) break;

                    int tier = newLevel - 1;
                    float v = GetLevelValue(tier);
                    switch (tier)
                    {
                        case 1: aura.AddDamage(v); break;
                        case 2: aura.AddRadius(v); break;
                        case 3: aura.AddSlowStrength(v); break;
                        default:
                            Debug.LogWarning($"[UpgradeData] AuraUpgrade : palier {newLevel} inattendu (max {TotalAllowedPicks}), aucun effet appliqué.");
                            break;
                    }
                    break;
                }

            // MODIFIE - Knives en deblocage separe : le premier pick cree l'arme (2
            // couteaux de base, deja geres par defaut dans WeaponShurikenBarrage),
            // aucun effet stat ce pick-ci. Les paliers +1 couteau/degats/perforation
            // sont decales d'un cran (tier = newLevel - 1).
            case UpgradeType.Knives:
                {
                    WeaponShurikenBarrage knives = playerGO.GetComponent<WeaponShurikenBarrage>();

                    if (newLevel == 1)
                    {
                        if (knives == null)
                            ApplyPastGlobalBonuses(playerGO.AddComponent<WeaponShurikenBarrage>());
                        break;
                    }

                    if (knives == null) break;

                    int tier = newLevel - 1;
                    float v = GetLevelValue(tier);
                    switch (tier)
                    {
                        case 1: knives.AddKnife(); break;
                        case 2: knives.AddDamage(v); break;
                        case 3: knives.AddPierce(Mathf.Max(1, Mathf.RoundToInt(v))); break;
                        default:
                            Debug.LogWarning($"[UpgradeData] Knives : palier {newLevel} inattendu (max {TotalAllowedPicks}), aucun effet appliqué.");
                            break;
                    }
                    break;
                }

            case UpgradeType.Orbital:
                {
                    WeaponOrbital orbital = playerGO.GetComponent<WeaponOrbital>();
                    if (newLevel == 1)
                    {
                        if (orbital == null)
                        {
                            orbital = playerGO.AddComponent<WeaponOrbital>();
                            GameObject prefab = Resources.Load<GameObject>("OrbitalProjectile");
                            if (prefab != null)
                                orbital.Init(prefab);
                            else
                                Debug.LogWarning("Prefab OrbitalProjectile introuvable dans Resources !");
                            ApplyPastGlobalBonuses(orbital);
                        }
                    }
                    else
                    {
                        if (orbital != null) orbital.AddOrbital();
                    }
                    break;
                }

            case UpgradeType.Lightning:
                {
                    WeaponLightningChain chain = playerGO.GetComponent<WeaponLightningChain>();
                    if (newLevel == 1)
                    {
                        if (chain == null)
                            ApplyPastGlobalBonuses(playerGO.AddComponent<WeaponLightningChain>());
                    }
                    else
                    {
                        if (chain != null) chain.AddChain();
                    }
                    break;
                }

            case UpgradeType.MudPuddle:
                {
                    WeaponMudPuddle mud = playerGO.GetComponent<WeaponMudPuddle>();
                    if (newLevel == 1)
                    {
                        if (mud == null)
                        {
                            mud = playerGO.AddComponent<WeaponMudPuddle>();
                            GameObject prefab = Resources.Load<GameObject>("MudPuddleZone");
                            if (prefab != null)
                                mud.Init(prefab);
                            else
                                Debug.LogWarning("Prefab MudPuddleZone introuvable dans Resources !");
                            ApplyPastGlobalBonuses(mud);
                        }
                    }
                    else
                    {
                        if (mud != null) mud.AddPuddle();
                    }
                    break;
                }

            // CORRIGE (2026-09-15) - meme decalage "tier = newLevel - 1" que
            // Knives juste au-dessus desormais : le palier brut 1 se contente
            // de debloquer l'arme (comme sa description le dit deja), les 3
            // effets reels (degats/vitesse/+1 orbe) vivent aux paliers 2/3/4.
            // Avant ce correctif, le palier 1 appliquait deja AddDamage() en
            // silence et le palier 4 (dernier des TotalAllowedPicks=4) ne
            // faisait rien du tout - voir le commentaire sur
            // FormatBouncingOrbDescription plus haut.
            case UpgradeType.BouncingOrb:
                {
                    WeaponBouncingOrb orb = playerGO.GetComponent<WeaponBouncingOrb>();

                    if (newLevel == 1)
                    {
                        if (orb == null)
                        {
                            orb = playerGO.AddComponent<WeaponBouncingOrb>();
                            GameObject prefab = Resources.Load<GameObject>("BouncingOrbProjectile");
                            if (prefab != null)
                                orb.Init(prefab);
                            else
                                Debug.LogWarning("Prefab BouncingOrbProjectile introuvable dans Resources !");
                            ApplyPastGlobalBonuses(orb);
                        }
                        break;
                    }

                    if (orb == null) break;

                    int tier = newLevel - 1;
                    float orbValue = GetLevelValue(tier);
                    switch (tier)
                    {
                        case 1: orb.AddDamage(orbValue); break;
                        case 2: orb.AddSpeed(orbValue); break;
                        case 3: orb.AddOrb(); break;
                        default:
                            Debug.LogWarning($"[UpgradeData] BouncingOrb : palier {newLevel} inattendu (max {TotalAllowedPicks}), aucun effet appliqué.");
                            break;
                    }
                    break;
                }

            // MODIFIE - meme correction que IsAvailable() : cible WeaponBase, plus
            // Fireball/Knives qui n'ont plus cette capacite.
            case UpgradeType.DoubleShot:
                {
                    WeaponBase baseWeaponDS = playerGO.GetComponent<WeaponBase>();
                    if (baseWeaponDS != null) baseWeaponDS.UnlockDoubleShot();
                    break;
                }

            // Ne se déclenche qu'au tout premier (et seul) pick : IsAvailable() ne repropose jamais une fusion déjà prise.
            case UpgradeType.Fusion:
                if (newLevel == 1) ApplyFusionResult(playerGO);
                break;

            // MODIFIÉ (2026-10-16, audit version finale) - "Dégâts+" et "Cadence+" s'appliquent désormais de façon
            // ADDITIVE et IDENTIQUE sur toutes les armes : bonus total = 1 + somme des picks (5 x 20 % = x2,0 partout).
            // Avant, le tir de base et les Orbitaux étaient additifs (x2,0) mais toutes les autres armes composaient
            // chaque pick (x1,2^5 = x2,49), et une arme obtenue APRÈS ces picks n'en recevait aucun. Les armes à
            // formule multiplicative reçoivent maintenant le rapport (1+S+v)/(1+S) ; les armes additives (tir de base,
            // Orbitaux) reçoivent v directement ; et toute nouvelle arme rattrape le total (ApplyPastGlobalBonuses).
            case UpgradeType.Damage:
                {
                    float total = LevelUpManager.Instance != null ? LevelUpManager.Instance.DamageBonusTotal : 0f;
                    float ratio = value / (1f + total);

                    WeaponFireball fireball = playerGO.GetComponent<WeaponFireball>();
                    if (fireball != null) fireball.AddDamage(ratio);

                    WeaponAura aura = playerGO.GetComponent<WeaponAura>();
                    if (aura != null) aura.AddDamage(ratio);

                    WeaponShurikenBarrage knives = playerGO.GetComponent<WeaponShurikenBarrage>();
                    if (knives != null) knives.AddDamage(ratio);

                    WeaponOrbital orbitalWeapon = playerGO.GetComponent<WeaponOrbital>();
                    if (orbitalWeapon != null) orbitalWeapon.AddDamage(value); // additive en interne

                    WeaponLightningChain lightningWeapon = playerGO.GetComponent<WeaponLightningChain>();
                    if (lightningWeapon != null) lightningWeapon.AddDamage(ratio);

                    WeaponMudPuddle mudWeapon = playerGO.GetComponent<WeaponMudPuddle>();
                    if (mudWeapon != null) mudWeapon.AddDamage(ratio);

                    WeaponBouncingOrb orbWeapon = playerGO.GetComponent<WeaponBouncingOrb>();
                    if (orbWeapon != null) orbWeapon.AddDamage(ratio);

                    // Fusions : chaque arme fusionnée doit être listée ici pour continuer de profiter de "Dégâts+"
                    // après avoir remplacé ses armes sources (voir WeaponFusionX.AddDamage de chacune, toutes
                    // multiplicatives).
                    WeaponFusionScorchedEarth scorchedEarth = playerGO.GetComponent<WeaponFusionScorchedEarth>();
                    if (scorchedEarth != null) scorchedEarth.AddDamage(ratio);

                    WeaponFusionManaVortex manaVortex = playerGO.GetComponent<WeaponFusionManaVortex>();
                    if (manaVortex != null) manaVortex.AddDamage(ratio);

                    WeaponFusionRicochetBlades ricochetBlades = playerGO.GetComponent<WeaponFusionRicochetBlades>();
                    if (ricochetBlades != null) ricochetBlades.AddDamage(ratio);

                    WeaponFusionThunderingOrbs thunderingOrbs = playerGO.GetComponent<WeaponFusionThunderingOrbs>();
                    if (thunderingOrbs != null) thunderingOrbs.AddDamage(ratio);

                    WeaponFusionCursedSwamp cursedSwamp = playerGO.GetComponent<WeaponFusionCursedSwamp>();
                    if (cursedSwamp != null) cursedSwamp.AddDamage(ratio);

                    WeaponFusionMudOrb mudOrb = playerGO.GetComponent<WeaponFusionMudOrb>();
                    if (mudOrb != null) mudOrb.AddDamage(ratio);

                    if (weapon != null) weapon.AddDamage(value); // additive en interne

                    if (LevelUpManager.Instance != null) LevelUpManager.Instance.DamageBonusTotal = total + value;
                    break;
                }

            case UpgradeType.FireRate:
                {
                    float total = LevelUpManager.Instance != null ? LevelUpManager.Instance.FireRateBonusTotal : 0f;
                    float ratio = value / (1f + total);

                    WeaponFireball fireball = playerGO.GetComponent<WeaponFireball>();
                    if (fireball != null) fireball.AddFireRate(ratio);

                    WeaponShurikenBarrage knives = playerGO.GetComponent<WeaponShurikenBarrage>();
                    if (knives != null) knives.AddFireRate(ratio);

                    // Fusions : seule Lames Ricochet a une cadence de tir propre (salve périodique) ; les autres
                    // fusions n'ont pas de notion de cadence (zones/orbes en continu).
                    WeaponFusionRicochetBlades ricochetBladesFR = playerGO.GetComponent<WeaponFusionRicochetBlades>();
                    if (ricochetBladesFR != null) ricochetBladesFR.AddFireRate(ratio);

                    WeaponLightningChain lightningWeapon = playerGO.GetComponent<WeaponLightningChain>();
                    if (lightningWeapon != null) lightningWeapon.AddFireRate(ratio);

                    if (weapon != null) weapon.AddFireRate(value); // additive en interne

                    if (LevelUpManager.Instance != null) LevelUpManager.Instance.FireRateBonusTotal = total + value;
                    break;
                }

            // PV max d'abord (+15 % des PV max de départ), puis le soin (value, 100 %) sur les nouveaux PV max.
            case UpgradeType.Heal:
                if (health != null)
                {
                    health.AddMaxHealthPercent(HealMaxHealthBonus);
                    health.Heal(value);
                }
                break;


        }
    }

    // AJOUTÉ (2026-10-16, audit version finale) - une arme obtenue APRÈS des picks de "Dégâts+" / "Cadence+" reçoit
    // d'un coup le total déjà accumulé (avant, elle n'en profitait jamais). Multiplication par (1 + total) : pour les
    // armes multiplicatives, AddX(total) donne exactement x(1+total) ; pour les additives (Orbitaux), AddDamage(total)
    // ajoute le total à leur somme, même résultat. Le bonus de Réputation appliqué dans leur Awake/Start est un
    // simple facteur multiplicatif : l'ordre n'a pas d'importance.
    private static void ApplyPastGlobalBonuses(Component weaponComponent)
    {
        if (weaponComponent == null || LevelUpManager.Instance == null) return;
        float dmg = LevelUpManager.Instance.DamageBonusTotal;
        float rate = LevelUpManager.Instance.FireRateBonusTotal;

        switch (weaponComponent)
        {
            case WeaponFireball w: if (dmg > 0f) w.AddDamage(dmg); if (rate > 0f) w.AddFireRate(rate); break;
            case WeaponShurikenBarrage w: if (dmg > 0f) w.AddDamage(dmg); if (rate > 0f) w.AddFireRate(rate); break;
            case WeaponLightningChain w: if (dmg > 0f) w.AddDamage(dmg); if (rate > 0f) w.AddFireRate(rate); break;
            case WeaponAura w: if (dmg > 0f) w.AddDamage(dmg); break;
            case WeaponOrbital w: if (dmg > 0f) w.AddDamage(dmg); break;
            case WeaponMudPuddle w: if (dmg > 0f) w.AddDamage(dmg); break;
            case WeaponBouncingOrb w: if (dmg > 0f) w.AddDamage(dmg); break;
        }
    }

    // Détruit les 2 armes sources (elles sont remplacées, pas cumulées) et crée l'arme fusionnée correspondant à
    // _fusionResultId. Les paliers/déblocages des 2 sources restent enregistrés dans LevelUpManager (l'arsenal des
    // écrans de fin continue donc de les montrer comme obtenues) : seuls leurs COMPOSANTS en jeu disparaissent.
    private void ApplyFusionResult(GameObject playerGO)
    {
        switch (_fusionResultId)
        {
            case "ScorchedEarth":
                {
                    WeaponFireball fireball = playerGO.GetComponent<WeaponFireball>();
                    WeaponMudPuddle mud = playerGO.GetComponent<WeaponMudPuddle>();

                    // Dégâts de départ = somme des dégâts hérités des 2 armes (déjà boostés par la Réputation et les
                    // picks de Dégâts+ précédents à cet instant) : la fusion ne repart jamais de zéro.
                    float inheritedDamage = 0f;
                    if (fireball != null) inheritedDamage += fireball.CurrentDamage;
                    if (mud != null) inheritedDamage += mud.CurrentDamagePerSecond;

                    if (fireball != null) Object.Destroy(fireball);
                    if (mud != null) Object.Destroy(mud);

                    if (playerGO.GetComponent<WeaponFusionScorchedEarth>() == null)
                    {
                        WeaponFusionScorchedEarth fused = playerGO.AddComponent<WeaponFusionScorchedEarth>();
                        GameObject prefab = Resources.Load<GameObject>("MudPuddleZone");
                        if (prefab != null)
                            fused.Init(prefab, Mathf.Max(1f, inheritedDamage));
                        else
                            Debug.LogWarning("Prefab MudPuddleZone introuvable dans Resources !");
                    }
                    break;
                }

            case "ManaVortex":
                {
                    WeaponAura aura = playerGO.GetComponent<WeaponAura>();
                    WeaponOrbital orbital = playerGO.GetComponent<WeaponOrbital>();

                    float inheritedField = aura != null ? aura.CurrentDamagePerSecond : 0f;
                    float inheritedOrbital = orbital != null ? orbital.CurrentDamage : 0f;
                    int inheritedOrbitalCount = orbital != null ? orbital.OrbitalCount : 0;
                    float inheritedSlow = aura != null ? aura.SlowMultiplier : 0.6f;

                    if (aura != null) Object.Destroy(aura);
                    if (orbital != null) Object.Destroy(orbital);

                    // MODIFIÉ (2026-10-27, refonte en Couronne Prismatique) - plus d'orbes poolés : la fusion calcule ses
                    // contacts elle-même et hérite aussi du ralentissement de l'Aura.
                    if (playerGO.GetComponent<WeaponFusionManaVortex>() == null)
                    {
                        WeaponFusionManaVortex fused = playerGO.AddComponent<WeaponFusionManaVortex>();
                        fused.Init(Mathf.Max(1f, inheritedField), Mathf.Max(1f, inheritedOrbital), inheritedOrbitalCount, inheritedSlow);
                    }
                    break;
                }

            case "RicochetBlades":
                {
                    WeaponShurikenBarrage knives = playerGO.GetComponent<WeaponShurikenBarrage>();
                    WeaponBouncingOrb orb = playerGO.GetComponent<WeaponBouncingOrb>();

                    float inheritedKnives = knives != null ? knives.CurrentDamage : 0f;
                    float inheritedOrb = orb != null ? orb.CurrentDamage : 0f;
                    int inheritedKnifeCount = knives != null ? knives.KnifeCount : 0;
                    int inheritedOrbCount = orb != null ? orb.OrbCount : 0;

                    if (knives != null) Object.Destroy(knives);
                    if (orb != null) Object.Destroy(orb);

                    // MODIFIÉ (2026-10-28, refonte en Valse des Lames) - plus de projectile poolé : lames et contacts gérés
                    // par l'arme elle-même (voir WeaponFusionRicochetBlades).
                    if (playerGO.GetComponent<WeaponFusionRicochetBlades>() == null)
                    {
                        WeaponFusionRicochetBlades blades = playerGO.AddComponent<WeaponFusionRicochetBlades>();
                        blades.Init(Mathf.Max(1f, inheritedKnives), Mathf.Max(1f, inheritedOrb), inheritedKnifeCount, inheritedOrbCount);
                        // Dégâts déjà hérités des Couteaux (bonus inclus) ; la CADENCE de la fusion repart de sa propre
                        // base : elle rattrape les Cadence+ déjà pris (sinon perdus en fusionnant).
                        float pastFireRate = LevelUpManager.Instance != null ? LevelUpManager.Instance.FireRateBonusTotal : 0f;
                        if (pastFireRate > 0f) blades.AddFireRate(pastFireRate);
                    }
                    break;
                }

            case "ThunderingOrbs":
                {
                    WeaponOrbital orbital = playerGO.GetComponent<WeaponOrbital>();
                    WeaponLightningChain lightning = playerGO.GetComponent<WeaponLightningChain>();

                    float inheritedOrbital = orbital != null ? orbital.CurrentDamage : 0f;
                    float inheritedLightning = lightning != null ? lightning.CurrentDamage : 0f;
                    int inheritedOrbitalCount = orbital != null ? orbital.OrbitalCount : 0;
                    int inheritedChainCount = lightning != null ? lightning.ChainCount : 0;

                    if (orbital != null) Object.Destroy(orbital);
                    if (lightning != null) Object.Destroy(lightning);

                    // MODIFIÉ (2026-10-27, refonte en Constellation Orageuse) - plus de prefab : orbes et contacts gérés
                    // par l'arme elle-même (voir WeaponFusionThunderingOrbs).
                    if (playerGO.GetComponent<WeaponFusionThunderingOrbs>() == null)
                    {
                        WeaponFusionThunderingOrbs fused = playerGO.AddComponent<WeaponFusionThunderingOrbs>();
                        fused.Init(Mathf.Max(1f, inheritedOrbital), Mathf.Max(1f, inheritedLightning), inheritedOrbitalCount, inheritedChainCount);
                    }
                    break;
                }

            case "CursedSwamp":
                {
                    WeaponMudPuddle mud = playerGO.GetComponent<WeaponMudPuddle>();
                    WeaponLightningChain lightning = playerGO.GetComponent<WeaponLightningChain>();

                    float inheritedMud = mud != null ? mud.CurrentDamagePerSecond : 0f;
                    float inheritedLightning = lightning != null ? lightning.CurrentDamage : 0f;

                    if (mud != null) Object.Destroy(mud);
                    if (lightning != null) Object.Destroy(lightning);

                    if (playerGO.GetComponent<WeaponFusionCursedSwamp>() == null)
                    {
                        WeaponFusionCursedSwamp fused = playerGO.AddComponent<WeaponFusionCursedSwamp>();
                        GameObject prefab = Resources.Load<GameObject>("MudPuddleZone");
                        if (prefab != null)
                            fused.Init(prefab, Mathf.Max(1f, inheritedMud), Mathf.Max(1f, inheritedLightning));
                        else
                            Debug.LogWarning("Prefab MudPuddleZone introuvable dans Resources !");
                    }
                    break;
                }

            case "MudOrb":
                {
                    WeaponBouncingOrb orb = playerGO.GetComponent<WeaponBouncingOrb>();
                    WeaponMudPuddle mud = playerGO.GetComponent<WeaponMudPuddle>();

                    float inheritedOrbDamage = orb != null ? orb.CurrentDamage : 0f;
                    float inheritedSpeed = orb != null ? orb.CurrentSpeed : 6f;
                    int inheritedOrbCount = orb != null ? orb.OrbCount : 0;
                    float inheritedPuddleDamage = mud != null ? mud.CurrentDamagePerSecond : 0f;

                    if (orb != null) Object.Destroy(orb);
                    if (mud != null) Object.Destroy(mud);

                    // MODIFIÉ (2026-10-28, passe visuelle de Sillage Boueux) - plus de "BouncingOrbProjectile" : orbes et
                    // contacts gérés par l'arme elle-même (voir WeaponFusionMudOrb).
                    if (playerGO.GetComponent<WeaponFusionMudOrb>() == null)
                    {
                        WeaponFusionMudOrb fused = playerGO.AddComponent<WeaponFusionMudOrb>();
                        GameObject puddlePrefab = Resources.Load<GameObject>("MudPuddleZone");
                        if (puddlePrefab != null)
                            fused.Init(puddlePrefab, Mathf.Max(1f, inheritedOrbDamage), inheritedSpeed, inheritedOrbCount, Mathf.Max(1f, inheritedPuddleDamage));
                        else
                            Debug.LogWarning("Prefab MudPuddleZone introuvable dans Resources !");
                    }
                    break;
                }

            default:
                Debug.LogWarning($"[UpgradeData] Fusion \"{_fusionResultId}\" inconnue de ApplyFusionResult().");
                break;
        }
    }
}

public enum UpgradeType
{
    Damage,
    FireRate,
    Heal,
    DoubleShot,
    Fireball,
    AuraUpgrade,
    Knives,
    Orbital,
    Lightning,
    MudPuddle,
    BouncingOrb,
    Fusion
}

public enum UpgradeBranch
{
    Aether,
    Kael,
    Lyra,
    Universal
}