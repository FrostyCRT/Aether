using System.Collections.Generic;

public static class SkillTreeData
{
    public class NodeData
    {
        public string displayName;
        public string description;
        public bool isUnique;
        public string level1Desc;
        public string level2Desc;
        public string level3Desc;
        public int costLevel1;
        public int costLevel2;
        public int costLevel3;
        public string[] prerequisites;
        public CharacterBranch branch; // AJOUTÉ — pour le grisage selon le personnage
    }

    // AJOUTÉ — enum pour identifier la branche de chaque nœud
    public enum CharacterBranch
    {
        Guerrier,
        Gardien,
        Fantome
    }

    private static readonly Dictionary<string, NodeData> _nodesCache;

    static SkillTreeData()
    {
        _nodesCache = new Dictionary<string, NodeData>();
        PopulateDatabase();
    }

    public static NodeData Get(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return null;
        return _nodesCache.TryGetValue(nodeId, out NodeData node) ? node : null;
    }

    private static void PopulateDatabase()
    {
        // ── GUERRIER ────────────────────────────────────────────────────

        // AJOUTÉ — remplace "damage" comme point d'entrée sans prérequis
        _nodesCache.Add("concentration", new NodeData
        {
            displayName = "Concentration",
            description = "Chaque seconde sans recevoir de dégâts augmente tes dégâts. Le compteur se réinitialise à chaque coup reçu.",
            isUnique = false,
            level1Desc = "+5% / sec, cap à +15%",
            level2Desc = "+5% / sec, cap à +25%",
            level3Desc = "+5% / sec, cap à +40%",
            costLevel1 = 100,
            costLevel2 = 300,
            costLevel3 = 700,
            prerequisites = System.Array.Empty<string>(),
            branch = CharacterBranch.Guerrier
        });

        _nodesCache.Add("cadence", new NodeData
        {
            displayName = "Cadence",
            description = "Augmente la vitesse de tir de toutes tes armes.",
            isUnique = false,
            level1Desc = "+10%",
            level2Desc = "+20%",
            level3Desc = "+35%",
            costLevel1 = 100,
            costLevel2 = 300,
            costLevel3 = 700,
            prerequisites = System.Array.Empty<string>(),
            branch = CharacterBranch.Guerrier
        });

        _nodesCache.Add("fragmentation", new NodeData
        {
            displayName = "Fragmentation",
            description = "Les projectiles ont 20% de chance d'exploser à l'impact.",
            isUnique = true,
            costLevel1 = 500,
            prerequisites = new[] { "concentration" }, // MODIFIÉ — était "damage"
            branch = CharacterBranch.Guerrier
        });

        _nodesCache.Add("crystalDamage", new NodeData
        {
            displayName = "Dégâts Cristal",
            description = "Augmente les dégâts de l'ultime et de la Nova.",
            isUnique = false,
            level1Desc = "+25%",
            level2Desc = "+50%",
            level3Desc = "+100%",
            costLevel1 = 100,
            costLevel2 = 300,
            costLevel3 = 700,
            prerequisites = new[] { "cadence" },
            branch = CharacterBranch.Guerrier
        });

        _nodesCache.Add("overpower", new NodeData
        {
            displayName = "Surpuissance",
            description = "Après l'ultime, tes dégâts sont doublés pendant 5 secondes.",
            isUnique = true,
            costLevel1 = 1000,
            prerequisites = new[] { "fragmentation", "crystalDamage" },
            branch = CharacterBranch.Guerrier
        });

        // ── GARDIEN ─────────────────────────────────────────────────────

        _nodesCache.Add("vitality", new NodeData
        {
            displayName = "Vitalité",
            description = "Augmente tes points de vie maximum.",
            isUnique = false,
            level1Desc = "+15%",
            level2Desc = "+30%",
            level3Desc = "+50%",
            costLevel1 = 100,
            costLevel2 = 300,
            costLevel3 = 700,
            prerequisites = System.Array.Empty<string>(),
            branch = CharacterBranch.Gardien
        });

        // AJOUTÉ — remplace "regen" comme point d'entrée sans prérequis
        _nodesCache.Add("recuperation", new NodeData
        {
            displayName = "Récupération",
            description = "Chaque cristal absorbé par le dash restaure des HP.",
            isUnique = false,
            level1Desc = "+2 HP par absorption",
            level2Desc = "+5 HP par absorption",
            level3Desc = "+8 HP par absorption",
            costLevel1 = 100,
            costLevel2 = 300,
            costLevel3 = 700,
            prerequisites = System.Array.Empty<string>(),
            branch = CharacterBranch.Gardien
        });

        _nodesCache.Add("secondWind", new NodeData
        {
            displayName = "Second Souffle",
            description = "Une fois par partie, survit à un coup fatal avec 1 HP.",
            isUnique = true,
            costLevel1 = 500,
            prerequisites = new[] { "vitality" },
            branch = CharacterBranch.Gardien
        });

        _nodesCache.Add("armor", new NodeData
        {
            displayName = "Armure",
            description = "Réduit les dégâts reçus de tous les ennemis.",
            isUnique = false,
            level1Desc = "-8%",
            level2Desc = "-15%",
            level3Desc = "-25%",
            costLevel1 = 100,
            costLevel2 = 300,
            costLevel3 = 700,
            prerequisites = new[] { "recuperation" }, // MODIFIÉ — était "regen"
            branch = CharacterBranch.Gardien
        });

        _nodesCache.Add("manaShield", new NodeData
        {
            displayName = "Bouclier de Mana",
            description = "Absorbe automatiquement 1 projectile ennemi toutes les 8 secondes.",
            isUnique = true,
            costLevel1 = 900,
            prerequisites = new[] { "secondWind", "armor" },
            branch = CharacterBranch.Gardien
        });

        // ── FANTÔME ─────────────────────────────────────────────────────

        // AJOUTÉ — remplace "agility" comme point d'entrée sans prérequis
        _nodesCache.Add("impulsionNova", new NodeData
        {
            displayName = "Impulsion Nova",
            description = "Si la Nova déclenchée par une absorption tue au moins un ennemi, le cooldown du dash est immédiatement réinitialisé.",
            isUnique = true,
            costLevel1 = 500,
            prerequisites = System.Array.Empty<string>(),
            branch = CharacterBranch.Fantome
        });

        _nodesCache.Add("dash", new NodeData
        {
            displayName = "Dash Amélioré",
            description = "Réduit le temps de recharge du dash.",
            isUnique = false,
            level1Desc = "-0.3s",
            level2Desc = "-0.6s",
            level3Desc = "-1.0s",
            costLevel1 = 100,
            costLevel2 = 300,
            costLevel3 = 700,
            prerequisites = System.Array.Empty<string>(),
            branch = CharacterBranch.Fantome
        });

        _nodesCache.Add("crystalMastery", new NodeData
        {
            displayName = "Maîtrise du Cristal",
            description = "Réduit le nombre de charges nécessaires pour l'ultime.",
            isUnique = true,
            costLevel1 = 600,
            prerequisites = new[] { "impulsionNova" }, // MODIFIÉ — était "agility"
            branch = CharacterBranch.Fantome
        });

        _nodesCache.Add("novaRadius", new NodeData
        {
            displayName = "Nova Étendue",
            description = "Augmente le rayon de la Nova de Cristal.",
            isUnique = false,
            level1Desc = "+30%",
            level2Desc = "+60%",
            level3Desc = "+100%",
            costLevel1 = 100,
            costLevel2 = 300,
            costLevel3 = 700,
            prerequisites = new[] { "dash" },
            branch = CharacterBranch.Fantome
        });

        _nodesCache.Add("phantomDash", new NodeData
        {
            displayName = "Dash Fantôme",
            description = "Le dash laisse un clone qui attire les ennemis proches pendant 2 secondes.",
            isUnique = true,
            costLevel1 = 1200,
            prerequisites = new[] { "crystalMastery", "novaRadius" },
            branch = CharacterBranch.Fantome
        });
    }
}