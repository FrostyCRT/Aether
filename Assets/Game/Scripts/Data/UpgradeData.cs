using UnityEngine;

[CreateAssetMenu(fileName = "SO_Upgrade", menuName = "BulletHeaven/Upgrade")]
public class UpgradeData : ScriptableObject
{
    [Header("Infos")]
    public string upgradeName;
    public string description;

    [Header("Effet")]
    public UpgradeType upgradeType;
    public float       value;

    public bool IsAvailable()
    {
        switch (upgradeType)
        {
            case UpgradeType.DoubleShot:
                // Disponible une seule fois
                WeaponBase wb = FindObjectOfType<WeaponBase>();
                return wb != null && !wb.IsDoubleShotUnlocked();
            
            case UpgradeType.AddOrbital:
                return FindObjectOfType<WeaponOrbital>() != null;

            case UpgradeType.AOERadius:
                return FindObjectOfType<WeaponAOE>() != null;

            case UpgradeType.UnlockAOE:
                return FindObjectOfType<WeaponAOE>() == null;

            case UpgradeType.UnlockOrbital:
                return FindObjectOfType<WeaponOrbital>() == null;

            default:
                return true;
        }
    }

    public void Apply()
    {
        PlayerController player  = FindObjectOfType<PlayerController>();
        WeaponBase        weapon  = FindObjectOfType<WeaponBase>();
        HealthSystem      health  = FindObjectOfType<HealthSystem>();
        WeaponAOE         aoe     = FindObjectOfType<WeaponAOE>();
        WeaponOrbital     orbital = FindObjectOfType<WeaponOrbital>();

        switch (upgradeType)
        {
            case UpgradeType.DoubleShot:
                if (weapon != null) weapon.UnlockDoubleShot();
                break;

            case UpgradeType.MoveSpeed:
                if (player != null) player.AddMoveSpeed(value);
                break;

            case UpgradeType.Damage:
                if (weapon != null)  weapon.AddDamage(value);
                if (aoe != null)     aoe.AddDamage(value);
                if (orbital != null) orbital.AddDamage(value);
                break;

            case UpgradeType.FireRate:
            if (weapon != null) weapon.AddFireRate(value);
            if (aoe != null)    aoe.AddFireRate(value);
            break;

            case UpgradeType.Heal:
                if (health != null) health.Heal(value);
                break;

            case UpgradeType.UnlockAOE:
                if (aoe == null)
                {
                    GameObject player_go = GameObject.FindWithTag("Player");
                    if (player_go != null)
                    {
                        WeaponAOE newAOE = player_go.AddComponent<WeaponAOE>();
                        GameObject prefab = Resources.Load<GameObject>("PulseVisual");
                        if (prefab != null)
                            newAOE.Init(prefab);
                        else
                            Debug.LogWarning("Prefab PulseVisual introuvable dans Resources !");
                    }
                }
                break;

            case UpgradeType.UnlockOrbital:
                if (orbital == null)
                {
                    GameObject player_go = GameObject.FindWithTag("Player");
                    if (player_go != null)
                    {
                        WeaponOrbital newOrbital = player_go.AddComponent<WeaponOrbital>();
                        // On récupère le prefab depuis les ressources
                        GameObject prefab = Resources.Load<GameObject>("OrbitalProjectile");
                        if (prefab != null)
                            newOrbital.Init(prefab);
                        else
                            Debug.LogWarning("Prefab OrbitalProjectile introuvable dans Resources !");
                    }
                }
                break;

            case UpgradeType.AddOrbital:
                if (orbital != null) orbital.AddOrbital();
                break;

            case UpgradeType.AOERadius:
                if (aoe != null) aoe.AddRadius(value);
                break;
        }
    }
}

public enum UpgradeType
{
    MoveSpeed,
    Damage,
    FireRate,
    Heal,
    UnlockAOE,
    UnlockOrbital,
    AddOrbital,
    AOERadius,
    DoubleShot  // ← ajoute ça
}