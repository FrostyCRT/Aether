using UnityEngine;

[CreateAssetMenu(fileName = "SO_Upgrade", menuName = "BulletHeaven/Upgrade")]
public class UpgradeData : ScriptableObject
{
    [Header("Infos")]
    public string upgradeName;
    public string description;

    [Header("Effet")]
    public UpgradeType upgradeType;
    public float value;

    [Header("Valeurs par palier (Fireball / AuraUpgrade / Knives uniquement)")]
    [Tooltip("Utilisé UNIQUEMENT par Fireball/AuraUpgrade/Knives, dont les 3 paliers ont des effets différents (contrairement aux autres cartes qui répètent le même effet à chaque pick). Index 0 = palier 1, index 1 = palier 2, index 2 = palier 3. Ignoré par tous les autres UpgradeType, qui continuent d'utiliser le champ 'value' ci-dessus.")]
    [SerializeField] private float[] _levelValues = new float[3];

    // AJOUTÉ — lecture sécurisée d'un palier (clamp pour éviter tout IndexOutOfRange
    // si jamais l'array n'a pas été rempli à 3 cases dans l'Inspector)
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

    // Nombre total de picks autorisés sur cette carte pour la run (déblocage + paliers, ou juste paliers)
    private int TotalAllowedPicks => _requiresUnlockPick ? _maxLevel + 1 : _maxLevel;

    private GameObject GetActivePlayer()
    {
        return GameObject.FindWithTag("Player");
    }

    // Nombre BRUT de picks déjà effectués sur cette carte (inclut le pick de déblocage s'il y en a un)
    public int GetCurrentLevel()
    {
        return LevelUpManager.Instance != null ? LevelUpManager.Instance.GetLevel(this) : 0;
    }

    // AJOUTÉ — pour l'UI (les 3 petites cases). Exclut le pick de déblocage :
    // Orbital/Lightning/Boue affichent 0/3 tant qu'ils ne sont pas débloqués, puis 1/3, 2/3, 3/3 après.
    public int GetDisplayLevel()
    {
        int raw = GetCurrentLevel();
        return _requiresUnlockPick ? Mathf.Max(0, raw - 1) : raw;
    }

    private bool IsMaxed()
    {
        return GetCurrentLevel() >= TotalAllowedPicks;
    }

    // AJOUTÉ — génère le texte affiché sur la carte à partir du PROCHAIN palier réel,
    // avec le vrai pourcentage/valeur, plutôt qu'un résumé statique des 3 paliers.
    // Convention du genre (Vampire Survivors, Brotato) : chiffre concret, pas de texte vague.
    // Retombe sur le champ 'description' statique pour les UpgradeType non couverts explicitement
    // ci-dessous (AOE legacy, etc.), pour ne rien casser sur les vieux assets.
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

            // AJOUTÉ
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

    private string FormatFireballDescription(int level)
    {
        float v = GetLevelValue(level);
        switch (level)
        {
            case 1: return $"+{PercentOf(v)}% dégâts.";
            case 2: return $"+{PercentOf(v)}% vitesse de tir.";
            case 3: return $"+{PercentOf(v)}% chance d'explosion à l'impact.";
            default: return description;
        }
    }

    private string FormatAuraDescription(int level)
    {
        float v = GetLevelValue(level);
        switch (level)
        {
            case 1: return $"+{PercentOf(v)}% dégâts.";
            case 2: return $"+{PercentOf(v)}% rayon.";
            case 3: return $"+{PercentOf(v)}% ralentissement.";
            default: return description;
        }
    }

    private string FormatKnivesDescription(int level)
    {
        float v = GetLevelValue(level);
        switch (level)
        {
            case 1: return $"+{PercentOf(v)}% dégâts.";
            case 2: return $"+{Mathf.Max(1, Mathf.RoundToInt(v))} ennemi(s) perforé(s) en plus.";
            case 3: return $"+{PercentOf(v)}% vitesse de tir.";
            default: return description;
        }
    }

    // AJOUTÉ
    private string FormatBouncingOrbDescription(int level)
    {
        float v = GetLevelValue(level);
        switch (level)
        {
            // MODIFIÉ — le palier 1 CRÉE l'arme (voir Apply()), donc le texte doit le dire
            // explicitement au lieu d'afficher juste le chiffre de dégâts, qui donnait
            // l'impression au joueur d'améliorer une arme qu'il possédait déjà.
            case 1: return "Débloque un orbe rebondissant qui traverse les ennemis.";
            case 2: return $"+{PercentOf(v)}% vitesse.";
            case 3: return "+1 orbe rebondissant supplémentaire.";
            default: return description;
        }
    }

    private int PercentOf(float ratio) => Mathf.RoundToInt(ratio * 100f);

    public bool IsAvailable()
    {
        GameObject player = GetActivePlayer();
        if (player == null) return false;

        switch (upgradeType)
        {
            // MODIFIÉ — DoubleShot cible maintenant l'arme exclusive présente (Fireball ou
            // Knives), plus WeaponBase (retiré des prefabs — il tirait en double en parallèle
            // de l'arme exclusive, bug non voulu). Kael/Aura n'a pas de notion de "tir" à
            // dupliquer (zone qui tick en continu, pas un projectile) — cette carte
            // n'apparaît donc simplement jamais dans son pool, même logique déjà assumée
            // pour Vitesse de tir sur Aura.
            case UpgradeType.DoubleShot:
                {
                    WeaponFireball fireballDS = player.GetComponent<WeaponFireball>();
                    if (fireballDS != null) return !fireballDS.IsDoubleShotUnlocked();

                    WeaponShurikenBarrage knivesDS = player.GetComponent<WeaponShurikenBarrage>();
                    if (knivesDS != null) return !knivesDS.IsDoubleShotUnlocked();

                    return false;
                }

            // AJOUTÉ — explicite plutôt que de reposer sur le "default: return true" plus bas.
            // Choix de design assumé : Heal est une carte de sécurité ponctuelle (pas une
            // montée en puissance), donc jamais plafonnée. Rendu explicite pour que ce ne soit
            // pas confondu avec un type simplement oublié dans le switch.
            case UpgradeType.Heal:
                return true;

            case UpgradeType.AOERadius:
                return player.GetComponent<WeaponAOE>() != null;

            case UpgradeType.UnlockAOE:
                return player.GetComponent<WeaponAOE>() == null;

            // AJOUTÉ — cartes filler universelles (touchent toutes les armes actives).
            // Toujours disponibles tant que non maxées ; configure un _maxLevel élevé
            // dans l'Inspector (ex: 8-10) puisqu'elles n'ont pas d'identité propre à préserver
            // comme les exclusifs, contrairement au cap à 3 des autres cartes.
            case UpgradeType.Damage:
                return !IsMaxed();

            case UpgradeType.FireRate:
                return !IsMaxed();

            // AJOUTÉ — upgrades exclusives personnage, cap générique 3 niveaux
            case UpgradeType.Fireball:
                return player.GetComponent<WeaponFireball>() != null && !IsMaxed();

            case UpgradeType.AuraUpgrade:
                return player.GetComponent<WeaponAura>() != null && !IsMaxed();

            case UpgradeType.Knives:
                return player.GetComponent<WeaponShurikenBarrage>() != null && !IsMaxed();

            // AJOUTÉ — Orbital et Lightning fusionnés (Unlock+Add) sous le même cap générique
            case UpgradeType.Orbital:
                return !IsMaxed();

            case UpgradeType.Lightning:
                return !IsMaxed();

            // AJOUTÉ — Boue, même modèle qu'Orbital/Lightning (déblocage + compte)
            case UpgradeType.MudPuddle:
                return !IsMaxed();

            // AJOUTÉ — Orbe Rebondissant, pas de déblocage séparé : le palier 1 crée
            // l'arme directement (comme Fireball/AuraUpgrade/Knives)
            case UpgradeType.BouncingOrb:
                return !IsMaxed();

            // OBSOLETE — conservés pour compat ascendante si un asset n'a pas encore été migré,
            // mais ne plus utiliser sur de nouveaux assets (voir UpgradeType.Orbital / .Lightning)
            case UpgradeType.UnlockOrbital:
                return player.GetComponent<WeaponOrbital>() == null;

            case UpgradeType.AddOrbital:
                WeaponOrbital orb = player.GetComponent<WeaponOrbital>();
                return orb != null && !orb.IsMaxOrbital();

            case UpgradeType.UnlockLightning:
                return player.GetComponent<WeaponLightningChain>() == null;

            case UpgradeType.AddLightningChain:
                WeaponLightningChain wlc = player.GetComponent<WeaponLightningChain>();
                return wlc != null && !wlc.IsMaxChain();

            default:
                return true;
        }
    }

    public void Apply()
    {
        GameObject playerGO = GetActivePlayer();
        if (playerGO == null) return;

        // AJOUTÉ — incrémente et récupère le niveau courant AVANT d'appliquer l'effet,
        // pour savoir si c'est un premier pick (déblocage) ou un pick suivant (renfort)
        int newLevel = LevelUpManager.Instance != null ? LevelUpManager.Instance.IncrementLevel(this) : 1;

        PlayerController player = playerGO.GetComponent<PlayerController>();
        WeaponBase weapon = playerGO.GetComponent<WeaponBase>();
        HealthSystem health = playerGO.GetComponent<HealthSystem>();
        WeaponAOE aoe = playerGO.GetComponent<WeaponAOE>();

        switch (upgradeType)
        {
            // MODIFIÉ — routage vers la bonne arme exclusive selon le personnage actif.
            // Chaque palier fait quelque chose de différent (voir _levelValues) :
            // palier 1 = dégâts, palier 2 = trait signature, palier 3 = payoff de fin de run.
            case UpgradeType.Fireball:
                {
                    WeaponFireball fireball = playerGO.GetComponent<WeaponFireball>();
                    if (fireball == null) break;

                    float v = GetLevelValue(newLevel);
                    switch (newLevel)
                    {
                        case 1: fireball.AddDamage(v); break;
                        case 2: fireball.AddFireRate(v); break;
                        case 3: fireball.AddFragmentationChance(v); break;
                        default:
                            Debug.LogWarning($"[UpgradeData] Fireball : palier {newLevel} inattendu (max {TotalAllowedPicks}), aucun effet appliqué.");
                            break;
                    }
                    break;
                }

            case UpgradeType.AuraUpgrade:
                {
                    WeaponAura aura = playerGO.GetComponent<WeaponAura>();
                    if (aura == null) break;

                    float v = GetLevelValue(newLevel);
                    switch (newLevel)
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

            case UpgradeType.Knives:
                {
                    WeaponShurikenBarrage knives = playerGO.GetComponent<WeaponShurikenBarrage>();
                    if (knives == null) break;

                    float v = GetLevelValue(newLevel);
                    switch (newLevel)
                    {
                        case 1: knives.AddDamage(v); break;
                        case 2: knives.AddPierce(Mathf.Max(1, Mathf.RoundToInt(v))); break;
                        case 3: knives.AddFireRate(v); break;
                        default:
                            Debug.LogWarning($"[UpgradeData] Knives : palier {newLevel} inattendu (max {TotalAllowedPicks}), aucun effet appliqué.");
                            break;
                    }
                    break;
                }

            // AJOUTÉ — Orbital fusionné : niveau 1 = déblocage, niveaux 2-3 = ajout d'un orbital
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

            // AJOUTÉ — Lightning fusionné : niveau 1 = déblocage, niveaux 2-3 = ajout d'un maillon
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

            // AJOUTÉ — Boue : niveau 1 = déblocage (3 flaques), niveaux 2-4 = +1 flaque chacun
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

            // AJOUTÉ — Orbe Rebondissant : le palier 1 crée l'arme ET applique son effet
            // (contrairement à Orbital/Lightning/Boue, pas de pick de déblocage séparé).
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

            // MODIFIÉ — même logique que IsAvailable() : cible l'arme exclusive présente
            case UpgradeType.DoubleShot:
                {
                    WeaponFireball fireballDS = playerGO.GetComponent<WeaponFireball>();
                    if (fireballDS != null) { fireballDS.UnlockDoubleShot(); break; }

                    WeaponShurikenBarrage knivesDS = playerGO.GetComponent<WeaponShurikenBarrage>();
                    if (knivesDS != null) knivesDS.UnlockDoubleShot();
                    break;
                }

            // MODIFIÉ — Damage est un filler universel : cible TOUTES les armes actives
            // présentes sur le joueur, pas juste l'ancien WeaponBase. Chaque GetComponent
            // renvoie null si l'arme n'est pas équipée par ce personnage/cette run, donc
            // aucun risque à tout tenter systématiquement.
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

                    // AJOUTÉ — Boue et Orbe Rebondissant touchés eux aussi, cohérent avec
                    // "Dégâts+ affecte toutes les armes actives présentes sur le joueur"
                    WeaponMudPuddle mudWeapon = playerGO.GetComponent<WeaponMudPuddle>();
                    if (mudWeapon != null) mudWeapon.AddDamage(value);

                    WeaponBouncingOrb orbWeapon = playerGO.GetComponent<WeaponBouncingOrb>();
                    if (orbWeapon != null) orbWeapon.AddDamage(value);

                    // Compat ascendante avec l'ancien WeaponBase/WeaponAOE si encore présents
                    if (weapon != null) weapon.AddDamage(value);
                    if (aoe != null) aoe.AddDamage(value);
                    break;
                }

            // MODIFIÉ — FireRate est un filler universel : cible toutes les armes qui ont
            // une notion de cadence de tir. Aura n'a volontairement PAS de AddFireRate() —
            // elle tick en continu sur un timer fixe (_tickRate), pas un cooldown de tir,
            // donc cette carte n'a simplement aucun effet sur Kael. C'est voulu, pas un bug.
            case UpgradeType.FireRate:
                {
                    WeaponFireball fireball = playerGO.GetComponent<WeaponFireball>();
                    if (fireball != null) fireball.AddFireRate(value);

                    WeaponShurikenBarrage knives = playerGO.GetComponent<WeaponShurikenBarrage>();
                    if (knives != null) knives.AddFireRate(value);

                    WeaponLightningChain lightningWeapon = playerGO.GetComponent<WeaponLightningChain>();
                    if (lightningWeapon != null) lightningWeapon.AddFireRate(value);

                    // Compat ascendante avec l'ancien WeaponBase/WeaponAOE si encore présents
                    if (weapon != null) weapon.AddFireRate(value);
                    if (aoe != null) aoe.AddFireRate(value);
                    break;
                }

            case UpgradeType.Heal:
                if (health != null) health.Heal(value);
                break;

            case UpgradeType.UnlockAOE:
                if (aoe == null)
                {
                    WeaponAOE newAOE = playerGO.AddComponent<WeaponAOE>();
                    GameObject prefab = Resources.Load<GameObject>("PulseVisual");
                    if (prefab != null)
                        newAOE.Init(prefab);
                    else
                        Debug.LogWarning("Prefab PulseVisual introuvable dans Resources !");
                }
                break;

            case UpgradeType.AOERadius:
                if (aoe != null) aoe.AddRadius(value);
                break;

            // OBSOLETE — compat ascendante, ne plus assigner à de nouveaux assets
            case UpgradeType.UnlockOrbital:
                if (playerGO.GetComponent<WeaponOrbital>() == null)
                {
                    WeaponOrbital legacyOrbital = playerGO.AddComponent<WeaponOrbital>();
                    GameObject prefab = Resources.Load<GameObject>("OrbitalProjectile");
                    if (prefab != null) legacyOrbital.Init(prefab);
                }
                break;

            case UpgradeType.AddOrbital:
                {
                    WeaponOrbital legacyOrbital = playerGO.GetComponent<WeaponOrbital>();
                    if (legacyOrbital != null) legacyOrbital.AddOrbital();
                    break;
                }

            case UpgradeType.UnlockLightning:
                if (playerGO.GetComponent<WeaponLightningChain>() == null)
                    playerGO.AddComponent<WeaponLightningChain>();
                break;

            case UpgradeType.AddLightningChain:
                {
                    WeaponLightningChain legacyChain = playerGO.GetComponent<WeaponLightningChain>();
                    if (legacyChain != null) legacyChain.AddChain();
                    break;
                }
        }
    }
}

public enum UpgradeType
{
    Damage,
    FireRate,
    Heal,
    UnlockAOE,
    UnlockOrbital,      // OBSOLETE — remplacé par Orbital, ne plus assigner à un nouvel asset
    AddOrbital,         // OBSOLETE — remplacé par Orbital
    AOERadius,
    DoubleShot,
    UnlockLightning,    // OBSOLETE — remplacé par Lightning
    AddLightningChain,  // OBSOLETE — remplacé par Lightning
    Fireball,           // AJOUTÉ — upgrade exclusive Aether
    AuraUpgrade,        // AJOUTÉ — upgrade exclusive Kael
    Knives,             // AJOUTÉ — upgrade exclusive Lyra
    Orbital,            // AJOUTÉ — fusion Unlock+Add, cap générique 3
    Lightning,          // AJOUTÉ — fusion Unlock+Add, cap générique 3
    MudPuddle,          // AJOUTÉ — Boue, même modèle qu'Orbital/Lightning
    BouncingOrb         // AJOUTÉ — Orbe Rebondissant, 3 paliers différenciés sans déblocage séparé
}