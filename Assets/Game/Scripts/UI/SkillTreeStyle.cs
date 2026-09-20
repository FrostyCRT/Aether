using UnityEngine;

// AJOUTE (2026-09-19) - langage visuel de l'onglet Compétences, partagé par les nœuds,
// les liaisons, la fiche de détail et les légendes : mêmes couleurs que la fiche
// Personnage (orange Aether / vert Kael / cyan Lyra, fond violet nuit, textes gris-lavande),
// pour que les deux onglets se lisent comme un seul écran.
public static class SkillTreeStyle
{
    public static readonly Color PanelDark = new Color32(0x0C, 0x0A, 0x12, 0xFF);
    public static readonly Color Disc = new Color32(0x15, 0x11, 0x1F, 0xFF);
    public static readonly Color Muted = new Color32(0x9C, 0x93, 0xAE, 0xFF);
    public static readonly Color Body = new Color32(0xD7, 0xD0, 0xE2, 0xFF);
    public static readonly Color EmptyRing = new Color32(0x4B, 0x46, 0x58, 0xFF);
    public static readonly Color EmptyPip = new Color32(0x5C, 0x56, 0x70, 0xFF);
    public static readonly Color Gold = new Color32(0xFF, 0xC8, 0x3D, 0xFF);
    public static readonly Color Warn = new Color32(0xE2, 0x60, 0x4F, 0xFF);

    public static Color Accent(SkillTreeData.CharacterBranch branch)
    {
        switch (branch)
        {
            case SkillTreeData.CharacterBranch.Gardien: return new Color32(0x52, 0xD4, 0x6B, 0xFF);  // Kael
            case SkillTreeData.CharacterBranch.Fantome: return new Color32(0x33, 0xC6, 0xF0, 0xFF);  // Lyra
            default: return new Color32(0xF2, 0x8A, 0x2E, 0xFF);                                     // Aether
        }
    }

    // Le personnage à qui appartient la branche : c'est son nom, pas celui de la branche
    // ("Gardien"), qui parle au joueur.
    public static string CharacterName(SkillTreeData.CharacterBranch branch)
    {
        switch (branch)
        {
            case SkillTreeData.CharacterBranch.Gardien: return "KAEL";
            case SkillTreeData.CharacterBranch.Fantome: return "LYRA";
            default: return "AETHER";
        }
    }

    public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

    // Nombre de "crans" d'une branche = somme des niveaux max de ses nœuds (3 par nœud à paliers, 1 par
    // talent unique), LU dans SkillTreeData : Aether et Kael comptent 11 (3 paliers x 3 + 2 uniques), Lyra 9
    // (2 nœuds à paliers + 3 uniques). Ne jamais figer "11" : la branche de Lyra est plus courte.
    public static int BranchMax(SkillTreeData.CharacterBranch branch)
    {
        int total = 0;
        foreach (SkillTreeData.NodeData n in SkillTreeData.All)
            if (n.branch == branch) total += MaxLevel(n);
        return total;
    }

    public static int MaxLevel(SkillTreeData.NodeData data) => data != null && data.isUnique ? 1 : 3;

    // Niveau actuel d'un nœud, ramené à 0..MaxLevel (un talent unique vaut 0 ou 1).
    public static int CurrentLevel(string nodeId)
    {
        MetaProgressionManager meta = MetaProgressionManager.Instance;
        SkillTreeData.NodeData data = SkillTreeData.Get(nodeId);
        if (meta == null || data == null) return 0;
        return data.isUnique ? (meta.IsNodePurchased(nodeId) ? 1 : 0)
                             : Mathf.Clamp(meta.GetNodeLevel(nodeId), 0, 3);
    }

    public static int BranchProgress(SkillTreeData.CharacterBranch branch)
    {
        int total = 0;
        foreach (SkillTreeData.NodeData n in SkillTreeData.All)
            if (n.branch == branch) total += CurrentLevel(n.id);
        return total;
    }
}
