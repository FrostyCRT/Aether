using UnityEngine;

public class StartingUpgradeGranter : MonoBehaviour
{
    private void Start()
    {
        CharacterIdentity identity = GetComponent<CharacterIdentity>();
        if (identity == null)
        {
            Debug.LogWarning("StartingUpgradeGranter : aucun CharacterIdentity trouvé, aucune upgrade de départ accordée.");
            return;
        }

        switch (identity.Type)
        {
            case CharacterType.Aether:
                if (GetComponent<WeaponFireball>() == null)
                    gameObject.AddComponent<WeaponFireball>();
                break;

            case CharacterType.Kael:
                if (GetComponent<WeaponAura>() == null)
                    gameObject.AddComponent<WeaponAura>();
                break;

            case CharacterType.Lyra:
                if (GetComponent<WeaponShurikenBarrage>() == null)
                    gameObject.AddComponent<WeaponShurikenBarrage>();
                break;
        }
    }
}