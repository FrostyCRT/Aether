using TMPro;
using UnityEngine;
using UnityEngine.UI;

// AJOUTÉ (2026-10-20, refonte de la page Accueil - retour utilisateur jamais signalé avant : "il faut revoir la
// disposition... la page principale ressemble à quoi [dans les autres jeux du genre] ?") - jusqu'ici, la page
// Accueil était la SEULE des 5 pages du menu sans aucun affichage d'Or/Éclats (voir SkillTreeUI.GoldPill sur
// l'onglet Améliorations, ReputationStyle.Gold/Eclat sur l'onglet Réputation) : un joueur qui atterrit sur l'Accueil
// n'a aucune idée de ses ressources actuelles, contrairement à la quasi-totalité des jeux du genre qui affichent
// leurs monnaies en permanence. 2 pastilles auto-construites (même recette que SkillTreeUI.GoldPill/AnimateGold :
// largeur qui épouse le texte, léger "punch" au changement, montée en compteur plutôt qu'un saut sec), tout en
// code - mêmes sprites neutres que le reste du HUD (HudSprites.Pill/Disc), aucun nouvel asset à importer.
public class MenuCurrencyReadout : MonoBehaviour
{
    [SerializeField] private Color _goldColor = new Color32(0xFF, 0xC8, 0x3D, 0xFF);   // ReputationStyle.Gold
    [SerializeField] private Color _eclatColor = new Color32(0xB7, 0x9C, 0xFF, 0xFF);  // ReputationStyle.Eclat
    [SerializeField] private float _pillHeight = 44f;
    [SerializeField] private float _spacing = 10f;

    private struct Readout
    {
        public RectTransform pill;
        public Image icon;
        public TextMeshProUGUI text;
        public float shown;
        public int targetSnapshot;
        public float punchTime;
    }

    private Readout _gold;
    private Readout _eclat;

    private void Awake()
    {
        RectTransform self = (RectTransform)transform;
        _gold = BuildPill("GoldReadout", _goldColor, self);
        _eclat = BuildPill("EclatReadout", _eclatColor, self);
        _gold.pill.anchoredPosition = Vector2.zero;
        _eclat.pill.anchoredPosition = new Vector2(0f, -(_pillHeight + _spacing));
        _gold.shown = -1f;
        _eclat.shown = -1f;
    }

    private Readout BuildPill(string name, Color accent, RectTransform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(150f, _pillHeight);

        Image bg = go.GetComponent<Image>();
        bg.sprite = HudSprites.Pill;
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.07f, 0.05f, 0.03f, 0.72f); // fond sombre translucide, même famille que le bois du bandeau

        GameObject outlineGo = new GameObject("Outline", typeof(RectTransform), typeof(Image));
        RectTransform outlineRt = outlineGo.GetComponent<RectTransform>();
        outlineRt.SetParent(rt, false);
        outlineRt.anchorMin = Vector2.zero; outlineRt.anchorMax = Vector2.one;
        outlineRt.offsetMin = Vector2.zero; outlineRt.offsetMax = Vector2.zero;
        Image outline = outlineGo.GetComponent<Image>();
        outline.sprite = HudSprites.PillOutline;
        outline.type = Image.Type.Sliced;
        outline.color = accent;
        outline.raycastTarget = false;

        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        RectTransform iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.SetParent(rt, false);
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = new Vector2(_pillHeight * 0.18f, 0f);
        iconRt.sizeDelta = Vector2.one * (_pillHeight * 0.52f);
        Image icon = iconGo.GetComponent<Image>();
        icon.sprite = HudSprites.Disc;
        icon.color = accent;
        icon.raycastTarget = false;

        GameObject textGo = new GameObject("Text", typeof(RectTransform));
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.SetParent(rt, false);
        textRt.anchorMin = new Vector2(0f, 0f); textRt.anchorMax = new Vector2(1f, 1f);
        textRt.offsetMin = new Vector2(_pillHeight * 0.18f + _pillHeight * 0.52f + 8f, 0f);
        textRt.offsetMax = new Vector2(-14f, 0f);
        TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.MidlineRight;
        text.fontSize = 24f;
        text.color = Color.white;
        text.raycastTarget = false;

        return new Readout { pill = rt, icon = icon, text = text };
    }

    private void OnEnable()
    {
        RefreshImmediate();
    }

    private void RefreshImmediate()
    {
        SaveData data = MetaProgressionManager.Instance != null ? MetaProgressionManager.Instance.Data : null;
        _gold.shown = data != null ? data.totalGold : 0;
        _eclat.shown = data != null ? data.totalEclats : 0;
        ApplyText(ref _gold);
        ApplyText(ref _eclat);
    }

    // Polling léger (même style que GameModeSelector/DifficultySelector, voir leurs commentaires) : pas d'événement
    // dédié pour un changement d'Or/Éclats, ce n'est qu'un affichage passif.
    private void Update()
    {
        SaveData data = MetaProgressionManager.Instance != null ? MetaProgressionManager.Instance.Data : null;
        if (data == null) return;

        AnimateTowards(ref _gold, data.totalGold);
        AnimateTowards(ref _eclat, data.totalEclats);
    }

    private void AnimateTowards(ref Readout r, int target)
    {
        if (Mathf.Approximately(r.shown, target)) return;

        float step = Mathf.Max(1f, Mathf.Abs(target - r.shown) * 6f * Time.unscaledDeltaTime);
        r.shown = Mathf.MoveTowards(r.shown, target, step);

        int rounded = Mathf.RoundToInt(r.shown);
        if (rounded != r.targetSnapshot)
        {
            r.targetSnapshot = rounded;
            r.punchTime = Time.unscaledTime;
        }
        ApplyText(ref r);
    }

    private void ApplyText(ref Readout r)
    {
        int shown = Mathf.RoundToInt(r.shown);
        r.text.text = Loc.Int(shown);
        float w = Mathf.Max(96f, _pillHeight * 0.18f + _pillHeight * 0.52f + 8f + r.text.GetPreferredValues(r.text.text).x + 14f);
        r.pill.sizeDelta = new Vector2(w, _pillHeight);

        float pt = Time.unscaledTime - r.punchTime;
        float punch = pt >= 0f && pt < 0.35f ? Mathf.Sin(pt / 0.35f * Mathf.PI) * 0.08f : 0f;
        r.pill.localScale = Vector3.one * (1f + punch);
    }
}
