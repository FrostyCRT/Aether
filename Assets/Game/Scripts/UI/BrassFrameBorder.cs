using UnityEngine;
using UnityEngine.UI;

// AJOUTÉ (2026-11-14, retour utilisateur : « les bords du pause panel entier, tu enlèves les traits noirs et à la place tu
// mets le même style de barre que la séparation de la carte de fusion, le losange [...] aux 4 coins du rectangle, et la
// barre ne doit pas être transparente aux extrémités ») - ajouté par PauseMenuUI.Awake sur MainFrame, à la place de son
// ancien Outline noir. Quatre filets du même dessin que la barre des fusions (OrnamentSprites.Rule), mais pleins d'un bout à
// l'autre, centrés sur le bord du cadre, du même brun encre ; un losange de laiton orangé à chaque coin. Construit une fois,
// en enfants du cadre : il suit donc son fondu d'ouverture (CanvasGroup) et sa taille.
[RequireComponent(typeof(RectTransform))]
public class BrassFrameBorder : MonoBehaviour
{
    // MODIFIÉ (2026-11-14, retour utilisateur : « je veux qu'elle soit marron, mais laisse les losanges en orange ») - même brun
    // encre que la barre des fusions (GameUI.FusionRuleGold) ; les losanges gardent leur laiton orangé.
    [SerializeField] private Color _lineColor = new Color(0.84f, 0.83f, 0.80f, 0.90f);   // gris clair comme les filets intérieurs (retour 2026-11-15 ; avant : brun 0.40,0.25,0.11)
    [SerializeField] private Color _gemOutlineColor = new Color(0.30f, 0.18f, 0.07f, 1f);
    [SerializeField] private Color _gemColor = new Color(1f, 0.80f, 0.36f, 1f);
    [Tooltip("Épaisseur du filet, halo compris (le trait net fait environ 40 % de cette valeur).")]
    [SerializeField] private float _thickness = 10f;
    [Tooltip("Décalage du filet vers l'intérieur du cadre (0 = centré sur le bord).")]
    [SerializeField] private float _inset = 0f;
    [SerializeField] private float _gemSize = 30f;

    private void Awake()
    {
        if (transform.Find("BrassBorder") != null) return;
        RectTransform root = new GameObject("BrassBorder", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(transform, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = new Vector2(_inset, _inset);
        root.offsetMax = new Vector2(-_inset, -_inset);
        root.SetAsLastSibling();

        Edge(root, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), false);
        Edge(root, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), false);
        Edge(root, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), true);
        Edge(root, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), true);
        foreach (Vector2 corner in new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right })
            Gem(root, corner);
    }

    // Filet tendu le long d'un bord, d'un coin à l'autre (les ancres l'étirent : il suit la taille du cadre).
    private void Edge(RectTransform parent, string name, Vector2 from, Vector2 to, bool vertical)
    {
        Image img = NewImage(name, parent, vertical ? OrnamentSprites.RuleVertical : OrnamentSprites.Rule(fadeEnds: false), _lineColor);
        RectTransform rt = img.rectTransform;
        rt.anchorMin = from;
        rt.anchorMax = to;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = vertical ? new Vector2(_thickness, 0f) : new Vector2(0f, _thickness);
    }

    private void Gem(RectTransform parent, Vector2 corner)
    {
        Image outer = NewImage("Gem", parent, HudSprites.Diamond, _gemOutlineColor);
        outer.preserveAspect = true;
        RectTransform o = outer.rectTransform;
        o.anchorMin = o.anchorMax = corner;
        o.pivot = new Vector2(0.5f, 0.5f);
        o.anchoredPosition = Vector2.zero;
        o.sizeDelta = new Vector2(_gemSize, _gemSize);

        Image core = NewImage("Core", o, HudSprites.Diamond, _gemColor);
        core.preserveAspect = true;
        RectTransform c = core.rectTransform;
        c.anchorMin = c.anchorMax = c.pivot = new Vector2(0.5f, 0.5f);
        c.sizeDelta = new Vector2(_gemSize * 0.55f, _gemSize * 0.55f);
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        Image img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }
}
