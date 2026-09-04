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
                default: return UpgradeBranch.Universal;
            }
        }
    }

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
                    ? "Débloque 2 orbitaux tournants qui frappent au contact."
                    : "+1 orbital supplémentaire.";

            case UpgradeType.Lightning:
                return nextLevel == 1
                    ? "Débloque la foudre en chaîne (2 rebonds)."
                    : "+1 rebond de foudre supplémentaire.";

            case UpgradeType.MudPuddle:
                return nextLevel == 1
                    ? "Débloque 3 flaques de boue ralentissante autour de toi."
                    : "+1 flaque de boue supplémentaire.";

            case UpgradeType.BouncingOrb:
                return FormatBouncingOrbDescription(nextLevel);

            case UpgradeType.Damage:
                return $"+{PercentOf(value)}% dégâts sur toutes tes armes actives.";

            case UpgradeType.FireRate:
                return $"+{PercentOf(value)}% vitesse de tir sur toutes tes armes actives.";

            case UpgradeType.Heal:
                return $"Restaure {PercentOf(value)}% de tes PV max.";

            case UpgradeType.DoubleShot:
                return "Débloque un second tir simultané.";

            default:
                return description;
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
    private string FormatFireballDescription(int level)
    {
        if (level == 1)
            return "Débloque Fireball : équipe l'arme exclusive d'Aether.";

        int tier = level - 1;
        float v = GetLevelValue(tier);
        switch (tier)
        {
            case 1: return $"+{v:0.#} de rayon d'explosion.";
            case 2: return $"+{PercentOf(v)}% dégâts.";
            case 3: return "Débloque la Brûlure : les ennemis touchés brûlent sur la durée.";
            default: return description;
        }
    }

    // MODIFIE - meme principe que Fireball : AuraUpgrade passe en deblocage separe.
    private string FormatAuraDescription(int level)
    {
        if (level == 1)
            return "Débloque l'Aura : équipe l'arme exclusive de Kael.";

        int tier = level - 1;
        float v = GetLevelValue(tier);
        switch (tier)
        {
            case 1: return $"+{PercentOf(v)}% dégâts.";
            case 2: return $"+{PercentOf(v)}% rayon.";
            case 3: return $"+{PercentOf(v)}% ralentissement.";
            default: return description;
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
            return "Débloque la Salve de Couteaux : équipe l'arme exclusive de Lyra.";

        int tier = level - 1;
        float v = GetLevelValue(tier);
        switch (tier)
        {
            case 1: return "Débloque un couteau supplémentaire dans la salve.";
            case 2: return $"+{PercentOf(v)}% dégâts.";
            case 3: return $"+{Mathf.Max(1, Mathf.RoundToInt(v))} ennemi(s) perforé(s) en plus.";
            default: return description;
        }
    }

    private string FormatBouncingOrbDescription(int level)
    {
        float v = GetLevelValue(level);
        switch (level)
        {
            case 1: return "Débloque un orbe rebondissant qui traverse les ennemis.";
            case 2: return $"+{PercentOf(v)}% vitesse.";
            case 3: return "+1 orbe rebondissant supplémentaire.";
            default: return description;
        }
    }

    private int PercentOf(float ratio) => Mathf.RoundToInt(ratio * 100f);

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

            case UpgradeType.Heal:
                return true;

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

            default:
                return true;
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
        WeaponAOE aoe = playerGO.GetComponent<WeaponAOE>();

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
                            playerGO.AddComponent<WeaponFireball>();
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
                            playerGO.AddComponent<WeaponAura>();
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
                            playerGO.AddComponent<WeaponShurikenBarrage>();
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
                            playerGO.AddComponent<WeaponLightningChain>();
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
                        }
                    }
                    else
                    {
                        if (mud != null) mud.AddPuddle();
                    }
                    break;
                }

            case UpgradeType.BouncingOrb:
                {
                    WeaponBouncingOrb orb = playerGO.GetComponent<WeaponBouncingOrb>();
                    if (orb == null)
                    {
                        orb = playerGO.AddComponent<WeaponBouncingOrb>();
                        GameObject prefab = Resources.Load<GameObject>("BouncingOrbProjectile");
                        if (prefab != null)
                            orb.Init(prefab);
                        else
                            Debug.LogWarning("Prefab BouncingOrbProjectile introuvable dans Resources !");
                    }

                    float orbValue = GetLevelValue(newLevel);
                    switch (newLevel)
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

            case UpgradeType.Damage:
                {
                    WeaponFireball fireball = playerGO.GetComponent<WeaponFireball>();
                    if (fireball != null) fireball.AddDamage(value);

                    WeaponAura aura = playerGO.GetComponent<WeaponAura>();
                    if (aura != null) aura.AddDamage(value);

                    WeaponShurikenBarrage knives = playerGO.GetComponent<WeaponShurikenBarrage>();
                    if (knives != null) knives.AddDamage(value);

                    WeaponOrbital orbitalWeapon = playerGO.GetComponent<WeaponOrbital>();
                    if (orbitalWeapon != null) orbitalWeapon.AddDamage(value);

                    WeaponLightningChain lightningWeapon = playerGO.GetComponent<WeaponLightningChain>();
                    if (lightningWeapon != null) lightningWeapon.AddDamage(value);

                    WeaponMudPuddle mudWeapon = playerGO.GetComponent<WeaponMudPuddle>();
                    if (mudWeapon != null) mudWeapon.AddDamage(value);

                    WeaponBouncingOrb orbWeapon = playerGO.GetComponent<WeaponBouncingOrb>();
                    if (orbWeapon != null) orbWeapon.AddDamage(value);

                    if (weapon != null) weapon.AddDamage(value);
                    if (aoe != null) aoe.AddDamage(value);
                    break;
                }

            case UpgradeType.FireRate:
                {
                    WeaponFireball fireball = playerGO.GetComponent<WeaponFireball>();
                    if (fireball != null) fireball.AddFireRate(value);

                    WeaponShurikenBarrage knives = playerGO.GetComponent<WeaponShurikenBarrage>();
                    if (knives != null) knives.AddFireRate(value);

                    WeaponLightningChain lightningWeapon = playerGO.GetComponent<WeaponLightningChain>();
                    if (lightningWeapon != null) lightningWeapon.AddFireRate(value);

                    if (weapon != null) weapon.AddFireRate(value);
                    if (aoe != null) aoe.AddFireRate(value);
                    break;
                }

            case UpgradeType.Heal:
                if (health != null) health.Heal(value);
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
    BouncingOrb
}

public enum UpgradeBranch
{
    Aether,
    Kael,
    Lyra,
    Universal
}