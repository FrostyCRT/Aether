using UnityEngine;

[CreateAssetMenu(fileName = "SO_Upgrade", menuName = "BulletHeaven/Upgrade")]
public class UpgradeData : ScriptableObject
{
    [Header("Infos")]
    public string upgradeName;
    [TextArea(2, 5)] public string description; // Permet un meilleur affichage dans l'inspecteur

    [Header("Effet")]
    public UpgradeType upgradeType;
    public float value;

    [Header("Prefabs (Optionnel)")]
    public GameObject weaponPrefab; // Plus besoin de Resources.Load, on glisse le prefab ici !
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
    DoubleShot,
    UnlockLightning,
    AddLightningChain
}