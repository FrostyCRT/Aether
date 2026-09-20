using System;
using System.Collections.Generic;
using UnityEngine;

// AJOUTE (2026-09-20) - CATALOGUE DES SKINS (onglet Réputation, partie basse).
//
// Un skin = une entrée de ce catalogue. Deux gammes :
//   - CLASSIQUE : s'achète avec de l'OR ;
//   - PRESTIGE  : s'achète avec des ÉCLATS.
// Le catalogue est un asset modifiable dans l'inspecteur (Assets/Resources/SkinCatalog.asset, créé par
// Aether > Rebuild Reputation Page) : pour ajouter un skin fabriqué avec Tripo3D, il suffit d'ajouter une ligne
// (id unique, personnage, nom, gamme, prix, image d'aperçu, et le prefab du joueur avec ce skin). La page Réputation
// n'a rien à modifier : elle lit ce catalogue. Sans asset, un catalogue minimal (la tenue d'origine de chaque
// personnage) est construit en mémoire.
public enum SkinTier { Classic, Prestige }

[Serializable]
public class SkinEntry
{
    [Tooltip("Identifiant unique et STABLE (enregistré dans la sauvegarde). Ex : aether_flamme")]
    public string id;
    [Tooltip("0 Aether, 1 Kael, 2 Lyra")]
    public int characterIndex;
    public string displayName;
    [TextArea(1, 3)] public string description;
    public SkinTier tier;
    [Tooltip("Prix : Or pour un skin Classique, Éclats pour un Prestige. Ignoré pour la tenue d'origine.")]
    public int cost;
    [Tooltip("Tenue d'origine du personnage : toujours possédée, gratuite, équipée par défaut.")]
    public bool isDefault;
    [Tooltip("Image d'aperçu (portrait du personnage avec ce skin). Vide = portrait d'origine.")]
    public Sprite preview;
    [Tooltip("Prefab du joueur avec ce skin. Vide = prefab d'origine du personnage.")]
    public GameObject playerPrefab;
}

[CreateAssetMenu(fileName = "SkinCatalog", menuName = "Aether/Skin Catalog")]
public class SkinCatalog : ScriptableObject
{
    public const int CharacterCount = 3;
    public static readonly string[] CharacterNames = { "AETHER", "KAEL", "LYRA" };

    public List<SkinEntry> skins = new List<SkinEntry>();

    private static SkinCatalog _instance;

    public static SkinCatalog Instance
    {
        get
        {
            if (_instance == null) _instance = Resources.Load<SkinCatalog>("SkinCatalog");
            if (_instance == null)
            {
                _instance = CreateInstance<SkinCatalog>();
                _instance.hideFlags = HideFlags.DontUnloadUnusedAsset;
                _instance.AddMissingDefaults();
            }
            return _instance;
        }
    }

    public static string DefaultId(int characterIndex)
    {
        switch (characterIndex)
        {
            case 1: return "kael_default";
            case 2: return "lyra_default";
            default: return "aether_default";
        }
    }

    // Ajoute la tenue d'origine de chaque personnage si elle manque (le catalogue reste modifiable à la main).
    public void AddMissingDefaults()
    {
        for (int c = 0; c < CharacterCount; c++)
        {
            string id = DefaultId(c);
            if (Get(id) != null) continue;
            skins.Insert(c, new SkinEntry
            {
                id = id, characterIndex = c, isDefault = true, tier = SkinTier.Classic, cost = 0,
                displayName = "Tenue d'origine",
                description = "La tenue avec laquelle " + CharacterNames[c][0] + CharacterNames[c].Substring(1).ToLowerInvariant() + " part au combat."
            });
        }
    }

    public SkinEntry Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (SkinEntry s in skins) if (s != null && s.id == id) return s;
        return null;
    }

    public SkinEntry DefaultFor(int characterIndex)
    {
        foreach (SkinEntry s in skins) if (s != null && s.isDefault && s.characterIndex == characterIndex) return s;
        return Get(DefaultId(characterIndex));
    }

    // Skins d'une gamme pour un personnage, dans l'ordre du catalogue (la tenue d'origine d'abord).
    public List<SkinEntry> For(int characterIndex, SkinTier tier)
    {
        List<SkinEntry> list = new List<SkinEntry>();
        foreach (SkinEntry s in skins)   // la tenue d'origine d'abord, puis l'ordre du catalogue (tri stable)
            if (s != null && s.isDefault && s.characterIndex == characterIndex && s.tier == tier && !string.IsNullOrEmpty(s.id)) list.Add(s);
        foreach (SkinEntry s in skins)
            if (s != null && !s.isDefault && s.characterIndex == characterIndex && s.tier == tier && !string.IsNullOrEmpty(s.id)) list.Add(s);
        return list;
    }
}
