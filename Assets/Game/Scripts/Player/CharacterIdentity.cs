using UnityEngine;

public enum CharacterType
{
    Aether,
    Kael,
    Lyra
}

public class CharacterIdentity : MonoBehaviour
{
    [SerializeField] private CharacterType _characterType;

    public CharacterType Type => _characterType;
}