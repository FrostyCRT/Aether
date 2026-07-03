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
        public int costLevel1;   // coût du niveau 1 (ou achat unique)
        public int costLevel2;
        public int costLevel3;
        public string[] prerequisites; // IDs des nœuds requis
    }

    // Cache statique unique pour éviter l'allocation par le Garbage Collector
    private static readonly Dictionary<string, NodeData> _nodesCache;

    static SkillTreeData()
    {
        _nodesCache = new Dictionary<string, NodeData>();
        PopulateDatabase();
    }

    public static NodeData Get(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return null;

        // Recherche instantanée O(1) sans aucune allocation de mémoire
        return _nodesCache.TryGetValue(nodeId, out NodeData node) ? node : null;
    }

    private static void PopulateDatabase()
    {
        // ── GUERRIER ────────────────────────────────────────────────────
        _nodesCache.Add("damage", new NodeData
        {
            displayName = "Dégâts",
            description = "Augmente les dégâts de toutes tes armes.",
            isUnique = false,
            level1Desc = "+10%",
            level2Desc = "+25%",
            level3Desc = "+50%",
            costLevel1 = 50,
            costLevel2 = 100,
            costLevel3 = 200,
            prerequisites = System.Array.Empty<string>() // Optimisation mémoire (.NET standard)
        });

        _nodesCache.Add("cadence", new NodeData
        {
            displayName = "Cadence",
            description = "Augmente la vitesse de tir de toutes tes armes.",
            isUnique = false,
            level1Desc = "+10%",
            level2Desc = "+20%",
            level3Desc = "+35%",
            costLevel1 = 50,
            costLevel2 = 100,
            costLevel3 = 200,
            prerequisites = System.Array.Empty<string>()
        });

        _nodesCache.Add("fragmentation", new NodeData
        {
            displayName = "Fragmentation",
            description = "Les projectiles ont 20% de chance d'exploser à l'impact.",
            isUnique = true,
            costLevel1 = 150,
            prerequisites = new[] { "damage" }
        });

        _nodesCache.Add("crystalDamage", new NodeData
        {
            displayName = "Dégâts Cristal",
            description = "Augmente les dégâts de l'ultime et de la Nova.",
            isUnique = false,
            level1Desc = "+25%",
            level2Desc = "+50%",
            level3Desc = "+100%",
            costLevel1 = 75,
            costLevel2 = 150,
            costLevel3 = 300,
            prerequisites = new[] { "cadence" }
        });

        _nodesCache.Add("overpower", new NodeData
        {
            displayName = "Surpuissance",
            description = "Après l'ultime, tes dégâts sont doublés pendant 5 secondes.",
            isUnique = true,
            costLevel1 = 400,
            prerequisites = new[] { "fragmentation", "crystalDamage" }
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
            costLevel1 = 50,
            costLevel2 = 100,
            costLevel3 = 200,
            prerequisites = System.Array.Empty<string>()
        });

        _nodesCache.Add("regen", new NodeData
        {
            displayName = "Régénération",
            description = "Régenère des HP chaque seconde.",
            isUnique = false,
            level1Desc = "+2 HP/sec",
            level2Desc = "+4 HP/sec",
            level3Desc = "+6 HP/sec",
            costLevel1 = 50,
            costLevel2 = 100,
            costLevel3 = 200,
            prerequisites = System.Array.Empty<string>()
        });

        _nodesCache.Add("secondWind", new NodeData
        {
            displayName = "Second Souffle",
            description = "Une fois par partie, survit à un coup fatal avec 1 HP.",
            isUnique = true,
            costLevel1 = 150,
            prerequisites = new[] { "vitality" }
        });

        _nodesCache.Add("armor", new NodeData
        {
            displayName = "Armure",
            description = "Réduit les dégâts reçus de tous les ennemis.",
            isUnique = false,
            level1Desc = "-8%",
            level2Desc = "-15%",
            level3Desc = "-25%",
            costLevel1 = 75,
            costLevel2 = 150,
            costLevel3 = 300,
            prerequisites = new[] { "regen" }
        });

        _nodesCache.Add("manaShield", new NodeData
        {
            displayName = "Bouclier de Mana",
            description = "Absorbe automatiquement 1 projectile ennemi toutes les 8 secondes.",
            isUnique = true,
            costLevel1 = 400,
            prerequisites = new[] { "secondWind", "armor" }
        });

        // ── FANTÔME ─────────────────────────────────────────────────────
        _nodesCache.Add("agility", new NodeData
        {
            displayName = "Agilité",
            description = "Augmente ta vitesse de déplacement.",
            isUnique = false,
            level1Desc = "+8%",
            level2Desc = "+18%",
            level3Desc = "+30%",
            costLevel1 = 50,
            costLevel2 = 100,
            costLevel3 = 200,
            prerequisites = System.Array.Empty<string>()
        });

        _nodesCache.Add("dash", new NodeData
        {
            displayName = "Dash Amélioré",
            description = "Réduit le temps de recharge du dash.",
            isUnique = false,
            level1Desc = "-0.3s",
            level2Desc = "-0.6s",
            level3Desc = "-1.0s",
            costLevel1 = 50,
            costLevel2 = 100,
            costLevel3 = 200,
            prerequisites = System.Array.Empty<string>()
        });

        _nodesCache.Add("crystalMastery", new NodeData
        {
            displayName = "Maîtrise du Cristal",
            description = "Réduit le nombre de charges nécessaires pour l'ultime.",
            isUnique = true,
            costLevel1 = 150,
            prerequisites = new[] { "agility" }
        });

        _nodesCache.Add("novaRadius", new NodeData
        {
            displayName = "Nova Étendue",
            description = "Augmente le rayon de la Nova de Cristal.",
            isUnique = false,
            level1Desc = "+30%",
            level2Desc = "+60%",
            level3Desc = "+100%",
            costLevel1 = 75,
            costLevel2 = 150,
            costLevel3 = 300,
            prerequisites = new[] { "dash" }
        });

        _nodesCache.Add("phantomDash", new NodeData
        {
            displayName = "Dash Fantôme",
            description = "Le dash laisse un clone qui attire l'ennemi le plus proche pendant 2 secondes.",
            isUnique = true,
            costLevel1 = 400,   
            prerequisites = new[] { "crystalMastery", "novaRadius" }
        });
    }
}