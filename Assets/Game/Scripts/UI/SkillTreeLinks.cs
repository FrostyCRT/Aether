using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// AJOUTE (2026-09-19) - liaisons entre les compétences de l'arbre.
//
// Avant : 15 médaillons flottants, aucune ligne. Les prérequis (SkillTreeData.prerequisites,
// déjà la source de vérité du jeu) n'étaient visibles nulle part : le joueur découvrait qu'un
// nœud était fermé en cliquant dessus. Ce composant relie chaque nœud à ses prérequis,
// EN LISANT CES MÊMES DONNÉES (aucune liste dupliquée) :
//   - prérequis manquant  : filet gris-violet éteint ;
//   - prérequis rempli, nœud pas encore pris : filet à la couleur du personnage + une
//     étincelle qui remonte vers le nœud (= "la voie est ouverte") ;
//   - nœud acquis : filet plein, lumineux.
//
// Chaque liaison = un socle sombre (lisibilité sur le décor très clair) + un filet coloré
// + un halo additif + une étincelle. Tout est généré en code, une seule fois ; l'état est
// relu par RefreshStates() (appelée par SkillTreeUI), les couleurs par frame passent par
// CanvasRenderer.SetColor (aucune reconstruction de maillage).
public class SkillTreeLinks : MonoBehaviour
{
    [SerializeField] private RectTransform _nodesParent;
    [SerializeField] private Sprite _lineSprite;      // UI_RoundRect (9-slice)
    [SerializeField] private Material _additiveMaterial;
    [Tooltip("Distance retirée à chaque extrémité (rayon du médaillon) pour que le filet parte du bord.")]
    [SerializeField] private float _trim = 64f;

    private class Link
    {
        public SkillNode from, to;
        public RectTransform bedRt, coreRt, glowRt, sparkRt;
        public CanvasRenderer bedCr, coreCr, glowCr, sparkCr;
        public Vector2 a, b;
        public Color accent;
        public bool met, owned, buyable;
        public float phase;
    }

    private readonly List<Link> _links = new List<Link>();
    private RectTransform _root;
    private CanvasGroup _group;
    private float _dim, _dimTarget;
    private bool _built;

    private void Awake() => Build();

    private void Build()
    {
        if (_built) return;
        _built = true;

        _root = (RectTransform)transform;
        _group = GetComponent<CanvasGroup>();
        if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        _group.interactable = false;

        if (_nodesParent == null) _nodesParent = (RectTransform)transform.parent;

        Dictionary<string, SkillNode> byId = new Dictionary<string, SkillNode>();
        foreach (SkillNode n in _nodesParent.GetComponentsInChildren<SkillNode>(true))
            byId[n.NodeId] = n;

        foreach (SkillNode to in byId.Values)
        {
            SkillTreeData.NodeData data = SkillTreeData.Get(to.NodeId);
            if (data == null || data.prerequisites == null) continue;
            foreach (string pre in data.prerequisites)
            {
                if (!byId.TryGetValue(pre, out SkillNode from)) continue;
                _links.Add(CreateLink(from, to, data.branch));
            }
        }
    }

    private Link CreateLink(SkillNode from, SkillNode to, SkillTreeData.CharacterBranch branch)
    {
        Link l = new Link { from = from, to = to, accent = SkillTreeStyle.Accent(branch), phase = Random.Range(0f, 6.28f) };

        Vector2 pa = from.Rect.anchoredPosition;
        Vector2 pb = to.Rect.anchoredPosition;
        Vector2 dir = (pb - pa).normalized;
        l.a = pa + dir * _trim;
        l.b = pb - dir * _trim;

        Vector2 mid = (l.a + l.b) * 0.5f;
        float len = Vector2.Distance(l.a, l.b);
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        MakeBar($"Link_{from.NodeId}_{to.NodeId}_glow", TreeSprites.Glow, true, mid, len + 40f, 46f, angle, out l.glowRt, out l.glowCr);
        MakeBar($"Link_{from.NodeId}_{to.NodeId}_bed", _lineSprite, false, mid, len, 15f, angle, out l.bedRt, out l.bedCr);
        MakeBar($"Link_{from.NodeId}_{to.NodeId}_core", _lineSprite, false, mid, len, 8f, angle, out l.coreRt, out l.coreCr);

        // étincelle : petit halo additif qui parcourt le filet
        GameObject go = new GameObject($"Link_{from.NodeId}_{to.NodeId}_spark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(_root, false);
        l.sparkRt = (RectTransform)go.transform;
        l.sparkRt.anchorMin = l.sparkRt.anchorMax = l.sparkRt.pivot = new Vector2(0.5f, 0.5f);
        l.sparkRt.sizeDelta = new Vector2(46f, 46f);
        Image si = go.GetComponent<Image>();
        si.sprite = TreeSprites.Glow;
        si.raycastTarget = false;
        si.material = _additiveMaterial;
        l.sparkCr = go.GetComponent<CanvasRenderer>();
        l.sparkCr.SetColor(new Color(1, 1, 1, 0));

        return l;
    }

    private void MakeBar(string objName, Sprite sprite, bool additive, Vector2 pos, float length, float thickness, float angle,
                         out RectTransform rt, out CanvasRenderer cr)
    {
        GameObject go = new GameObject(objName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(_root, false);
        rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(length, thickness);
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);

        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = additive ? Image.Type.Simple : Image.Type.Sliced;
        img.raycastTarget = false;
        if (additive) img.material = _additiveMaterial;

        cr = go.GetComponent<CanvasRenderer>();
        cr.SetColor(new Color(1, 1, 1, 0));
    }

    // --- API --------------------------------------------------------------

    public void SetDimmed(bool dimmed) => _dimTarget = dimmed ? 1f : 0f;

    public void RefreshStates()
    {
        if (!_built) Build();
        MetaProgressionManager meta = MetaProgressionManager.Instance;
        if (meta == null) return;

        foreach (Link l in _links)
        {
            l.met = SkillTreeStyle.CurrentLevel(l.from.NodeId) >= 1;
            l.owned = l.met && SkillTreeStyle.CurrentLevel(l.to.NodeId) >= 1;
            l.buyable = l.met && !l.owned;
        }
    }

    private void OnEnable() => RefreshStates();

    // --- boucle -----------------------------------------------------------

    private void Update()
    {
        float t = Time.unscaledTime;
        _dim = Mathf.MoveTowards(_dim, _dimTarget, Time.unscaledDeltaTime * 6f);
        _group.alpha = Mathf.Lerp(1f, 0.4f, _dim);

        for (int i = 0; i < _links.Count; i++)
        {
            Link l = _links[i];

            Color bed = SkillTreeStyle.PanelDark;
            bed.a = l.met ? 0.78f : 0.7f;
            l.bedCr.SetColor(bed);

            Color core; float glow;
            if (l.owned)
            {
                core = Color.Lerp(l.accent, Color.white, 0.25f);
                core.a = 1f;
                glow = 0.30f + 0.06f * Mathf.Sin(t * 0.9f + l.phase);
            }
            else if (l.buyable)
            {
                core = l.accent;
                core.a = 0.85f;
                glow = 0.12f;
            }
            else
            {
                core = new Color32(0x9A, 0x92, 0xB0, 0xFF);   // gris-lavande clair : la liaison "fermée" doit se VOIR
                core.a = 0.95f;
                glow = 0f;
            }
            l.coreCr.SetColor(core);
            l.glowCr.SetColor(new Color(l.accent.r, l.accent.g, l.accent.b, glow));

            // étincelle : n'apparaît que sur une voie ouverte (prérequis rempli)
            if (l.met)
            {
                float period = l.owned ? 4.2f : 2.6f;
                float u = Mathf.Repeat(t / period + l.phase * 0.16f, 1f);
                float env = Mathf.Sin(u * Mathf.PI);
                l.sparkRt.anchoredPosition = Vector2.Lerp(l.a, l.b, u);
                Color sc = Color.Lerp(l.accent, Color.white, 0.55f);
                sc.a = (l.owned ? 0.55f : 0.9f) * env;
                l.sparkCr.SetColor(sc);
            }
            else
            {
                l.sparkCr.SetColor(new Color(1, 1, 1, 0));
            }
        }
    }
}
