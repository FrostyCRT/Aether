using UnityEngine;

public class PlayerUpgrades : MonoBehaviour
{
    [Header("Ref de base (assignées ou récupérées au Start)")]
    private PlayerController playerController;
    private HealthSystem healthSystem;
    private WeaponBase mainWeapon;

    // Références dynamiques pour savoir si l'arme est équipée
    private WeaponAOE weaponAOE;
    private WeaponOrbital weaponOrbital;
    private WeaponLightningChain weaponLightning;

    void Awake()
    {
        playerController = GetComponent<PlayerController>();
        healthSystem = GetComponent<HealthSystem>();
        mainWeapon = GetComponent<WeaponBase>();

        // On vérifie s'ils sont déjà présents au début
        weaponAOE = GetComponent<WeaponAOE>();
        weaponOrbital = GetComponent<WeaponOrbital>();
        weaponLightning = GetComponent<WeaponLightningChain>();
    }

    public bool IsUpgradeAvailable(UpgradeData upgrade)
    {
        switch (upgrade.upgradeType)
        {
            case UpgradeType.DoubleShot:
                return mainWeapon != null && !mainWeapon.IsDoubleShotUnlocked();

            case UpgradeType.AddOrbital:
                return weaponOrbital != null && !weaponOrbital.IsMaxOrbital();

            case UpgradeType.AOERadius:
                return weaponAOE != null;

            case UpgradeType.UnlockAOE:
                return weaponAOE == null;

            case UpgradeType.UnlockOrbital:
                return weaponOrbital == null;

            case UpgradeType.UnlockLightning:
                return weaponLightning == null;

            case UpgradeType.AddLightningChain:
                return weaponLightning != null && !weaponLightning.IsMaxChain();

            default:
                return true;
        }
    }

    public void ApplyUpgrade(UpgradeData upgrade)
    {
        switch (upgrade.upgradeType)
        {
            case UpgradeType.MoveSpeed:
                if (playerController != null) playerController.AddMoveSpeed(upgrade.value);
                break;

            case UpgradeType.Damage:
                if (mainWeapon != null) mainWeapon.AddDamage(upgrade.value);
                if (weaponAOE != null) weaponAOE.AddDamage(upgrade.value);
                if (weaponOrbital != null) weaponOrbital.AddDamage(upgrade.value);
                if (weaponLightning != null) weaponLightning.AddDamage(upgrade.value);
                break;

            case UpgradeType.FireRate:
                if (mainWeapon != null) mainWeapon.AddFireRate(upgrade.value);
                if (weaponAOE != null) weaponAOE.AddFireRate(upgrade.value);
                break;

            case UpgradeType.Heal:
                if (healthSystem != null) healthSystem.Heal(upgrade.value);
                break;

            case UpgradeType.DoubleShot:
                if (mainWeapon != null) mainWeapon.UnlockDoubleShot();
                break;

            case UpgradeType.UnlockAOE:
                if (weaponAOE == null && upgrade.weaponPrefab != null)
                {
                    weaponAOE = gameObject.AddComponent<WeaponAOE>();
                    weaponAOE.Init(upgrade.weaponPrefab);
                }
                break;

            case UpgradeType.AOERadius:
                if (weaponAOE != null) weaponAOE.AddRadius(upgrade.value);
                break;

            case UpgradeType.UnlockOrbital:
                if (weaponOrbital == null && upgrade.weaponPrefab != null)
                {
                    weaponOrbital = gameObject.AddComponent<WeaponOrbital>();
                    weaponOrbital.Init(upgrade.weaponPrefab);
                }
                break;

            case UpgradeType.AddOrbital:
                if (weaponOrbital != null) weaponOrbital.AddOrbital();
                break;

            case UpgradeType.UnlockLightning:
                if (weaponLightning == null)
                {
                    weaponLightning = gameObject.AddComponent<WeaponLightningChain>();
                    if (upgrade.weaponPrefab != null) weaponLightning.Init(upgrade.weaponPrefab);
                }
                break;

            case UpgradeType.AddLightningChain:
                if (weaponLightning != null) weaponLightning.AddChain();
                break;
        }
    }
}
