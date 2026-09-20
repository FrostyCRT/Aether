using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// AJOUTE (2026-09-19) - fiche de détail d'une compétence (remplace l'ancien encart brun
// Liberation Sans : titre + paragraphe + 3 lignes qui se chevauchaient + boutons Unity par
// défaut). Même langage que la fiche Personnage : fond nuit, filet et barre à la couleur du
// personnage, médaillon d'en-tête, puces d'information, séparateurs, Bangers partout.
//
// Lecture de haut en bas :
//   1. QUI      - médaillon + nom + puces (personnage · niveau ou talent unique) ;
//   2. QUOI     - description (texte du jeu, SkillTreeData) ;
//   3. PALIERS  - 3 lignes (acquis / prochain / à venir) avec l'effet ET le prix de chacun
//                 -> on voit toute la progression, pas seulement le prochain cran ;
//   4. REQUIS   - prérequis avec leur état (rempli / manquant), lus dans SkillTreeData ;
//   5. ACHAT    - coût, or actuel, or restant après achat, raison exacte d'un blocage.
//
// Ce composant ne construit rien (la hiérarchie est dans la scène, faite par
// SkillTreePageBuilder) : il lit MetaProgressionManager / SkillTreeData et remplit les champs.
// Il ne modifie jamais la sauvegarde : l'achat reste dans SkillTreeUI.OnBuyClicked.
public class SkillDetailPanel : MonoBehaviour
{
    [System.Serializable]
    public class AccentTarget
    {
        public Graphic graphic;
        [Range(0f, 1f)] public float alpha = 1f;
    }

    [System.Serializable]
    public class Chip
    {
        public RectTransform root;
        public Image border;
        public Image fill;
        public TextMeshProUGUI label;
    }

    [System.Serializable]
    public class LevelRow
    {
        public GameObject root;
        public Image highlight;
        public Image pipRing;
        public Image pipFill;
        public TextMeshProUGUI tag;
        public TextMeshProUGUI effect;
        public TextMeshProUGUI right;
        public Image coin;
    }

    [System.Serializable]
    public class PrereqRow
    {
        public GameObject root;
        public Image pipRing;
        public Image pipFill;
        public TextMeshProUGUI text;
    }

    [Header("Couleur du personnage")]
    [SerializeField] private AccentTarget[] _accentTargets;

    [System.Serializable]
    public class IconScale
    {
        public string nodeId;
        [Range(0.3f, 1.2f)] public float scale = 1f;
    }

    [Header("En-tête")]
    [SerializeField] private Image _ring;
    [SerializeField] private Image _icon;
    [Tooltip("Correction de taille par icône (1 = taille normale dans le cadre). Une icône dont le dessin est plus grand que les autres est réduite ici, sans toucher aux autres.")]
    [SerializeField] private IconScale[] _iconScales = { new IconScale { nodeId = "concentration", scale = 0.8f } };
    [SerializeField] private TextMeshProUGUI _name;
    [SerializeField] private Chip _chipOwner;
    [SerializeField] private Chip _chipKind;

    [Header("Description")]
    [SerializeField] private TextMeshProUGUI _description;

    [Header("Paliers")]
    [SerializeField] private GameObject _levelsGroup;
    [SerializeField] private LevelRow[] _levels;

    [Header("Prérequis")]
    [SerializeField] private GameObject _prereqGroup;
    [SerializeField] private PrereqRow[] _prereqs;

    [Header("Achat")]
    [SerializeField] private TextMeshProUGUI _costLabel;
    [SerializeField] private TextMeshProUGUI _costValue;
    [SerializeField] private Image _costCoin;
    [SerializeField] private TextMeshProUGUI _goldValue;
    [SerializeField] private TextMeshProUGUI _afterValue;
    [SerializeField] private TextMeshProUGUI _statusText;
    [SerializeField] private Button _buyButton;
    [SerializeField] private Image _buyImage;
    [SerializeField] private TextMeshProUGUI _buyLabel;
    [SerializeField] private Button _closeButton;

    private static readonly Color BuyEnabledText = new Color32(0x32, 0x32, 0x32, 0xFF);
    private static readonly Color BuyDisabledText = new Color32(0x5A, 0x52, 0x66, 0xFF);

    public Button BuyButton => _buyButton;
    public Button CloseButton => _closeButton;

    // Remplit la fiche pour un nœud. Retourne rien : SkillTreeUI relit l'état du manager
    // lui-même quand il en a besoin.
    public void Populate(string nodeId, Sprite icon)
    {
        MetaProgressionManager meta = MetaProgressionManager.Instance;
        SkillTreeData.NodeData data = SkillTreeData.Get(nodeId);
        if (meta == null || data == null) return;

        Color accent = SkillTreeStyle.Accent(data.branch);
        string owner = SkillTreeStyle.CharacterName(data.branch);
        bool unique = data.isUnique;
        int max = SkillTreeStyle.MaxLevel(data);
        int level = SkillTreeStyle.CurrentLevel(nodeId);
        int gold = meta.Data.totalGold;
        int cost = meta.GetNodeCost(nodeId);            // -1 : plus rien à acheter
        bool maxed = level >= max;
        bool unlockable = meta.IsNodeUnlockable(nodeId);
        bool activeBranch = data.branch == meta.GetActiveBranch();

        // --- couleur ---
        foreach (AccentTarget t in _accentTargets)
        {
            if (t == null || t.graphic == null) continue;
            Color c = accent; c.a = t.alpha;
            t.graphic.color = c;
        }

        // --- en-tête ---
        // Le cercle garde la couleur du personnage (comme le cadre du nœud sur l'arbre) : le "maîtrisé"
        // se lit dans la puce "NIVEAU 3 / 3" et le bouton, pas en changeant la couleur d'identité.
        if (_ring != null) _ring.color = accent;
        if (_icon != null)
        {
            _icon.sprite = icon;
            float k = 1f;
            if (_iconScales != null)
                foreach (IconScale s in _iconScales)
                    if (s != null && s.nodeId == nodeId) k = s.scale;
            _icon.rectTransform.localScale = new Vector3(k, k, 1f);
        }
        _name.text = data.displayName;
        SetChip(_chipOwner, owner, accent);
        SetChip(_chipKind, unique ? "TALENT UNIQUE" : $"NIVEAU {level} / {max}", maxed ? SkillTreeStyle.Gold : accent);

        _description.text = data.description;

        // --- paliers ---
        _levelsGroup.SetActive(!unique);
        if (!unique)
        {
            string[] effects = { data.level1Desc, data.level2Desc, data.level3Desc };
            int[] costs = { data.costLevel1, data.costLevel2, data.costLevel3 };
            for (int i = 0; i < _levels.Length; i++)
            {
                int lvl = i + 1;
                bool owned = lvl <= level;
                bool next = lvl == level + 1;
                int price = next && cost > 0 ? cost : costs[i];
                FillLevelRow(_levels[i], lvl, effects[i], price, owned, next, accent);
            }
        }

        // --- prérequis ---
        List<string> pre = new List<string>();
        if (data.prerequisites != null) pre.AddRange(data.prerequisites);
        _prereqGroup.SetActive(pre.Count > 0);
        for (int i = 0; i < _prereqs.Length; i++)
        {
            PrereqRow row = _prereqs[i];
            bool used = i < pre.Count;
            row.root.SetActive(used);
            if (!used) continue;

            SkillTreeData.NodeData p = SkillTreeData.Get(pre[i]);
            bool met = SkillTreeStyle.CurrentLevel(pre[i]) >= 1;
            string what = p == null ? pre[i] : p.displayName;
            string detail = p != null && p.isUnique ? "débloqué" : "niveau 1";
            row.text.text = $"{what}  <size=80%><color=#{SkillTreeStyle.Hex(SkillTreeStyle.Muted)}>{detail}</color></size>";
            row.text.color = met ? SkillTreeStyle.Body : SkillTreeStyle.Warn;
            row.pipRing.color = met ? accent : SkillTreeStyle.Warn;
            row.pipFill.gameObject.SetActive(met);
            row.pipFill.color = accent;
        }

        // --- achat ---
        FillPurchase(data, cost, gold, maxed, unlockable, activeBranch, owner, accent);
    }

    private static void SetChip(Chip chip, string text, Color color)
    {
        if (chip == null || chip.root == null) return;
        chip.label.text = text;
        chip.border.color = color;
        chip.label.color = Color.white;
        // largeur ajustée au texte (la puce est en 9-slice)
        Vector2 pref = chip.label.GetPreferredValues(text);
        chip.root.sizeDelta = new Vector2(pref.x + 34f, chip.root.sizeDelta.y);
    }

    private void FillLevelRow(LevelRow row, int level, string effect, int price, bool owned, bool next, Color accent)
    {
        Color dim = new Color(SkillTreeStyle.Muted.r, SkillTreeStyle.Muted.g, SkillTreeStyle.Muted.b, 0.75f);

        row.tag.text = $"NIV {level}";
        row.effect.text = effect;

        if (owned)
        {
            row.highlight.color = new Color(accent.r, accent.g, accent.b, 0.0f);
            row.pipRing.color = accent;
            row.pipFill.gameObject.SetActive(true);
            row.pipFill.color = accent;
            row.tag.color = accent;
            row.effect.color = Color.white;
            row.right.text = "ACQUIS";
            row.right.color = accent;
            row.coin.gameObject.SetActive(false);
        }
        else if (next)
        {
            row.highlight.color = new Color(SkillTreeStyle.Gold.r, SkillTreeStyle.Gold.g, SkillTreeStyle.Gold.b, 0.14f);
            row.pipRing.color = SkillTreeStyle.Gold;
            row.pipFill.gameObject.SetActive(false);
            row.tag.color = SkillTreeStyle.Gold;
            row.effect.color = Color.white;
            row.right.text = price.ToString();
            row.right.color = SkillTreeStyle.Gold;
            row.coin.gameObject.SetActive(true);
            row.coin.color = Color.white;
        }
        else
        {
            row.highlight.color = new Color(1, 1, 1, 0);
            row.pipRing.color = SkillTreeStyle.EmptyRing;
            row.pipFill.gameObject.SetActive(false);
            row.tag.color = dim;
            row.effect.color = dim;
            row.right.text = price.ToString();
            row.right.color = dim;
            row.coin.gameObject.SetActive(true);
            row.coin.color = new Color(1, 1, 1, 0.45f);
        }
    }

    private void FillPurchase(SkillTreeData.NodeData data, int cost, int gold, bool maxed, bool unlockable,
                              bool activeBranch, string owner, Color accent)
    {
        bool hasCost = cost > 0;
        bool canBuy = false;
        string label, status = "";
        Color statusColor = SkillTreeStyle.Muted;

        if (maxed)
        {
            label = data.isUnique ? "ACQUIS" : "NIVEAU MAX";
            status = data.isUnique ? "Ce talent est actif." : "Cette compétence est au maximum.";
            statusColor = SkillTreeStyle.Gold;
        }
        else if (!activeBranch)
        {
            label = "INDISPONIBLE";
            status = $"Sélectionne {owner} dans l'onglet Personnages pour acheter.";
        }
        else if (!unlockable)
        {
            label = "VERROUILLÉ";
            status = "Débloque d'abord les prérequis ci-dessus.";
            statusColor = SkillTreeStyle.Warn;
        }
        else if (gold < cost)
        {
            label = "OR INSUFFISANT";
            status = $"Il te manque {cost - gold} or.";
            statusColor = SkillTreeStyle.Warn;
        }
        else
        {
            label = "ACHETER";
            canBuy = true;
        }

        // ligne coût : masquée quand il n'y a plus rien à acheter
        // La LIGNE entière disparaît (pas seulement ses éléments) : sinon elle garde ses 50 px et laisse un
        // vide entre le dernier séparateur et "TON OR" ; la fiche, en mise en page automatique, rétrécit.
        _costLabel.transform.parent.gameObject.SetActive(hasCost);
        _costLabel.gameObject.SetActive(hasCost);
        _costValue.gameObject.SetActive(hasCost);
        _costCoin.gameObject.SetActive(hasCost);
        _afterValue.gameObject.SetActive(hasCost && canBuy);
        if (hasCost)
        {
            _costValue.text = cost.ToString();
            _costValue.color = gold >= cost ? SkillTreeStyle.Gold : SkillTreeStyle.Warn;
        }
        _goldValue.text = gold.ToString();
        if (hasCost && canBuy) _afterValue.text = $"reste {gold - cost}";

        _statusText.gameObject.SetActive(!string.IsNullOrEmpty(status));
        _statusText.text = status;
        _statusText.color = statusColor;

        _buyLabel.text = label;
        _buyButton.interactable = canBuy;
        _buyLabel.color = canBuy ? BuyEnabledText : BuyDisabledText;
        if (_buyImage != null) _buyImage.color = canBuy ? Color.white : new Color(0.5f, 0.48f, 0.55f, 0.9f);
    }
}
