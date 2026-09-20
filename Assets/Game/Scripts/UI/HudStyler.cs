using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Habille le HUD en jeu au lancement de la scène, sans toucher à la hiérarchie ni aux références de GameUI :
//   • barres vie / boss / XP / esquive : HudBar (cadre de métal forgé, animations, icône) — plus de poignées grises ;
//   • barre du boss VERTICALE sur le bord droit (comme Yo-kai Watch), nom du boss en haut au centre ;
//   • groupe du bas : pastille du clone, cristaux d'ultime, pions du bouclier, légendes juste au-dessus de chaque élément ;
//   • colonne en haut à droite : icônes alignées sur une même colonne, textes alignés à droite avec de l'air entre les deux.
//
// RÉGLAGES : toutes les valeurs de placement / taille sont des champs de l'inspecteur (groupes ci-dessous). Le HUD étant
// construit au lancement, les changements se voient EN JEU (Play) : modifiez un champ pendant que le jeu tourne, le HUD se
// remet à jour aussitôt. Pour garder les valeurs réglées pendant Play : clic droit sur l'en-tête du composant → « Copy
// Component », quittez Play, puis clic droit → « Paste Component Values ».
// À poser sur l'objet « HUD » (GameUI._hudPanel).
public class HudStyler : MonoBehaviour
{
    [Header("Marges et lignes du haut (distances depuis le bord de l'écran, en px du canvas 1920x1080)")]
    [Tooltip("Marge gauche : bord du médaillon de la barre de vie.")]
    [SerializeField] private float _leftMargin = 40f;
    [Tooltip("Marge droite : bord des icônes or / éliminations / défi et de la barre du boss.")]
    [SerializeField] private float _rightMargin = 40f;
    [Tooltip("Ligne 1 : centre de la barre de vie, du chrono et de l'or (distance depuis le haut).")]
    [SerializeField] private float _row1 = 66f;
    [Tooltip("Ligne 2 : éliminations.")]
    [SerializeField] private float _row2 = 130f;
    [Tooltip("Ligne 3 : défi.")]
    [SerializeField] private float _row3 = 194f;

    [Header("Colonne en haut à droite")]
    [Tooltip("Taille commune des icônes (pièce, crâne, parchemin), toutes centrées sur la même colonne.")]
    [SerializeField] private float _rowIconSize = 58f;
    [Tooltip("Espace entre le texte et son icône.")]
    [SerializeField] private float _iconGap = 24f;
    [Tooltip("Taille de police commune à l'or, aux éliminations et au défi.")]
    [SerializeField] private float _rightTextFontSize = 38f;
    [SerializeField] private float _timerFontSize = 52f;

    [Header("Barre de vie")]
    [SerializeField] private float _hpLength = 520f;
    [Tooltip("Épaisseur intérieure de la barre (le cadre s'ajoute autour).")]
    [SerializeField] private float _hpThickness = 32f;
    [Tooltip("Diamètre du médaillon du cœur.")]
    [SerializeField] private float _hpMedallion = 56f;
    [Tooltip("Décalage du début de la barre : plus grand = la barre commence plus loin du médaillon.")]
    [SerializeField] private float _hpBarInset = 44f;
    [SerializeField] private float _hpFontSize = 26f;

    [Header("Barre d'XP")]
    [SerializeField] private float _xpThickness = 20f;

    [Header("Boss")]
    [SerializeField] private float _bossBarThickness = 30f;
    [Tooltip("Décalage vers la GAUCHE de la barre et du portrait, en plus de la marge droite.")]
    [SerializeField] private float _bossSideShift = 34f;
    [Tooltip("La barre ne clignote qu'une fois, puis de nouveau seulement après ce délai (en secondes) sans dégât.")]
    [SerializeField] private float _bossHitFlashQuietTime = 1f;
    [Tooltip("Distance entre le haut de l'écran et le début de la barre verticale.")]
    [SerializeField] private float _bossTopGap = 372f;
    [Tooltip("Distance entre le bas de l'écran et la fin de la barre verticale.")]
    [SerializeField] private float _bossBottomGap = 150f;
    [Tooltip("Distance entre le haut de l'écran et le centre du portrait du boss.")]
    [SerializeField] private float _bossIconY = 300f;
    [SerializeField] private Vector2 _bossIconSize = new Vector2(112f, 82f);
    [SerializeField] private float _bossNameFontSize = 42f;
    [Tooltip("Distance entre la ligne 1 et le nom du boss (vers le bas).")]
    [SerializeField] private float _bossNameOffsetY = 68f;

    [Header("Groupe du bas")]
    [Tooltip("Marge entre le bas de l'écran et le bas de la barre d'XP, ET entre le haut de la barre d'XP et le bas des éléments au-dessus (esquive, ultime, bouclier... ; la pastille du clone, plus grande, dépasse un peu). Le groupe reste centré sur la barre d'XP quand des éléments apparaissent.")]
    [SerializeField] private float _bottomMargin = 60f;
    [Tooltip("Décalage horizontal du texte « ESQUIVE ».")]
    [SerializeField] private float _dashCaptionX = 15f;
    [SerializeField] private float _captionFontSize = 27f;
    [SerializeField] private Color _captionColor = new Color(0.96f, 0.89f, 0.72f, 1f);
    [Tooltip("Espace entre le haut d'un élément et sa légende.")]
    [SerializeField] private float _captionGap = 6f;
    [SerializeField] private float _dashLength = 210f;
    [SerializeField] private float _dashThickness = 16f;
    [SerializeField] private float _dashMedallion = 38f;
    [Tooltip("Taille de police du « ULT x1 / x2 » à droite des cristaux.")]
    [SerializeField] private float _ultStackFontSize = 42f;
    [Tooltip("Taille d'un cristal de l'ultime.")]
    [SerializeField] private Vector2 _crystalSize = new Vector2(32f, 50f);
    [Tooltip("Sprite de cristal (optionnel) : laissé vide, un cristal dessiné en code est utilisé. Glissez ici un sprite pour le remplacer.")]
    [SerializeField] private Sprite _crystalSprite;

    private static readonly Color RimTint = Color.white;

    private TextMeshProUGUI _ref;              // texte de référence (police / matériau Bangers)
    private bool _dirty;

    // pastille du clone
    private Image _cloneFill, _cloneRim;
    private RectTransform _cloneRt;
    private float _clonePrev = -1f, _clonePulse;

    private sealed class CaptionRef
    {
        public TextMeshProUGUI text;
        public RectTransform rt;
        public System.Func<float> halfHeight;      // moitié de la hauteur de l'élément légendé
        public System.Func<float> offsetX;         // décalage horizontal de la légende
        public bool refForGap = true;              // compte pour l'écart avec la barre d'XP (faux pour la pastille du clone)
    }
    private readonly List<CaptionRef> _captions = new List<CaptionRef>();

    private void Awake()
    {
        _ref = Find<TextMeshProUGUI>("StatsGroup/TimerText");

        // chaque bloc est isolé : un souci sur un élément ne doit pas laisser le reste du HUD non habillé
        Safe(BuildOnce);
        Safe(Apply);
    }

    // Modification d'un champ dans l'inspecteur : réappliqué à l'image suivante (en jeu uniquement).
    private void OnValidate()
    {
        if (Application.isPlaying) _dirty = true;
    }

    [ContextMenu("Réappliquer les réglages")]
    private void Reapply() { _dirty = true; }

    private void Apply()
    {
        Safe(ApplyBars);
        Safe(ApplyBoss);
        Safe(ApplyCluster);
        Safe(ApplyTopRight);
        Safe(ApplyTimer);
    }

    private static void Safe(System.Action block)
    {
        try { block(); }
        catch (System.Exception e) { Debug.LogException(e); }
    }

    private T Find<T>(string path) where T : Component
    {
        Transform t = transform.Find(path);
        return t != null ? t.GetComponent<T>() : null;
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Une seule fois : sprites, anneau du clone, légendes (créés puis réglés par Apply).
    private void BuildOnce()
    {
        // pastille du clone : disque sombre, anneau de bronze biseauté, remplissage radial lumineux ; la touche au centre
        Transform cd = transform.Find("ActionCluster/CdClone");
        if (cd != null)
        {
            _cloneRt = (RectTransform)cd;
            Image root = cd.GetComponent<Image>();
            if (root != null) root.enabled = false;

            Transform bgT = cd.Find("Background"), fillT = cd.Find("Fill"), hubT = cd.Find("IconHub");
            if (bgT != null)
            {
                Image bg = bgT.GetComponent<Image>();
                bg.sprite = HudSprites.Disc;
                bg.color = new Color(0.11f, 0.08f, 0.17f, 1f);
            }
            if (fillT != null)
            {
                _cloneFill = fillT.GetComponent<Image>();
                _cloneFill.sprite = HudSprites.Pip;
                _cloneFill.color = new Color(0.66f, 0.34f, 1f, 1f);
            }
            if (hubT != null)
            {
                ((RectTransform)hubT).sizeDelta = new Vector2(48f, 48f);
                TextMeshProUGUI key = hubT.GetComponentInChildren<TextMeshProUGUI>(true);
                if (key != null) { ((RectTransform)key.transform).anchoredPosition = Vector2.zero; key.color = Color.white; }
            }

            var ring = new GameObject("Rim", typeof(RectTransform), typeof(Image));
            ring.transform.SetParent(cd, false);
            ring.transform.SetAsFirstSibling();
            _cloneRim = ring.GetComponent<Image>();
            _cloneRim.sprite = HudSprites.Ring;
            _cloneRim.color = RimTint;
            _cloneRim.raycastTarget = false;
            var rr = (RectTransform)ring.transform;
            rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one;
            rr.offsetMin = new Vector2(-5f, -5f); rr.offsetMax = new Vector2(5f, 5f);

            AddCaption(cd, "CLONE", () => 35f).refForGap = false;
        }

        var dash = Find<Slider>("ActionCluster/DashCooldownBar");
        if (dash != null) AddCaption(dash.transform, "ESQUIVE", () => Mathf.Max(_dashMedallion, _dashThickness) * 0.5f, () => _dashCaptionX);

        Transform crystals = transform.Find("ActionCluster/UltimateGroup/CrystalBar");
        if (crystals != null) AddCaption(crystals, "ULTIME", () => _crystalSize.y * 0.5f);

        // pions du bouclier de mana (Kael)
        Transform shield = transform.Find("ActionCluster/ManaShieldGroup");
        if (shield != null)
        {
            foreach (Image g in shield.GetComponentsInChildren<Image>(true))
            {
                if (g.transform == shield) continue;
                g.sprite = HudSprites.Pip;
            }
            AddCaption(shield, "BOUCLIER", () => 18f);
        }

        Transform focus = transform.Find("ActionCluster/ConcentrationContainer");
        if (focus != null) AddCaption(focus, "CONCENTRATION", () => 20f);
    }

    // Petite légende posée juste au-dessus d'un élément du groupe du bas (ignorée par les layouts).
    private CaptionRef AddCaption(Transform parent, string text, System.Func<float> halfHeight, System.Func<float> offsetX = null)
    {
        var go = new GameObject("Caption", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(260f, 32f);

        var le = go.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        var t = go.AddComponent<TextMeshProUGUI>();
        if (_ref != null)
        {
            t.font = _ref.font;
            // contour fin et net (celui du matériau d'origine, trop épais, empâtait le petit texte)
            t.fontSharedMaterial = HudBar.OutlinedMaterial(_ref.fontSharedMaterial, 0.14f, new Color32(14, 9, 5, 255));
        }
        t.text = text;
        t.characterSpacing = 3f;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;

        var cr = new CaptionRef { text = t, rt = rt, halfHeight = halfHeight, offsetX = offsetX };
        _captions.Add(cr);
        return cr;
    }

    // ---------------------------------------------------------------------------------------------------------------
    private void ApplyBars()
    {
        // vie : en haut à gauche, centrée sur la même ligne que le chrono et l'or
        var hp = Find<Slider>("Health/HPBar");
        if (hp != null)
        {
            var hpRt = (RectTransform)hp.transform;
            var health = hpRt.parent as RectTransform;
            if (health != null)
            {
                foreach (LayoutGroup g in health.GetComponents<LayoutGroup>()) g.enabled = false;
                var fit = health.GetComponent<ContentSizeFitter>();
                if (fit != null) fit.enabled = false;
                health.anchorMin = health.anchorMax = new Vector2(0f, 1f);
                health.pivot = new Vector2(0f, 0.5f);
                health.sizeDelta = new Vector2(_hpLength, Mathf.Max(_hpMedallion, _hpThickness + 14f));
                health.anchoredPosition = new Vector2(_leftMargin, -_row1);
            }
            hpRt.anchorMin = hpRt.anchorMax = new Vector2(0f, 0.5f);
            hpRt.pivot = new Vector2(0f, 0.5f);
            hpRt.sizeDelta = new Vector2(_hpLength, 40f);
            hpRt.anchoredPosition = Vector2.zero;
            HudBar.Attach(hp, HudBar.Kind.Health, _hpThickness, _hpBarInset, HudSprites.Heart, new Color(0.90f, 0.16f, 0.20f), _hpMedallion,
                          Find<TextMeshProUGUI>("Health/HPText"), _hpFontSize);
        }

        HudBar.Attach(Find<Slider>("XpGroup/XPBar"), HudBar.Kind.Xp, _xpThickness, 0f, null, Color.white, 0f, null, 0f, false, 1.4f);

        var dash = Find<Slider>("ActionCluster/DashCooldownBar");
        if (dash != null)
        {
            RectTransform drt = (RectTransform)dash.transform;
            drt.sizeDelta = new Vector2(_dashLength, drt.sizeDelta.y);
            HudBar.Attach(dash, HudBar.Kind.Dash, _dashThickness, _dashMedallion * 0.8f, HudSprites.Bolt, new Color(0.40f, 0.92f, 1f), _dashMedallion, null, 0f, false, 2f);
        }
    }

    // Barre du boss : verticale, sur le bord droit ; portrait au-dessus ; nom en haut au centre.
    // Ancrée en haut / en bas du HUD (et non au centre) : quelle que soit l'échelle d'interface, le portrait reste sous la
    // colonne or / éliminations / défi et la barre s'arrête au-dessus du bas de l'écran.
    private void ApplyBoss()
    {
        var bar = Find<Slider>("BossGroup/BossHPBar");
        if (bar != null)
        {
            float cx = -(_rightMargin + 22f + _bossSideShift);          // centre de la barre (depuis le bord droit)
            float w = _bossBarThickness + 10f;

            var rt = (RectTransform)bar.transform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(cx - w * 0.5f, _bossBottomGap);
            rt.offsetMax = new Vector2(cx + w * 0.5f, -_bossTopGap);
            HudBar boss = HudBar.Attach(bar, HudBar.Kind.Boss, _bossBarThickness, 0f, null, Color.white, 0f, null, 0f, true, 1.2f);
            if (boss != null) boss.hitFlashQuietTime = _bossHitFlashQuietTime;

            var icon = Find<Image>("BossGroup/BossIcon");
            if (icon != null)
            {
                var ir = icon.rectTransform;
                ir.anchorMin = ir.anchorMax = new Vector2(1f, 1f);
                ir.pivot = new Vector2(0.5f, 0.5f);
                ir.sizeDelta = _bossIconSize;
                ir.anchoredPosition = new Vector2(cx, -_bossIconY);
                icon.preserveAspect = true;
            }
        }

        // nom du boss : en haut au centre, sous le chrono (lisible d'un coup d'œil, contrairement à un texte couché)
        var name = Find<TextMeshProUGUI>("BossGroup/BossNameText");
        if (name != null)
        {
            var nr = name.rectTransform;
            nr.anchorMin = nr.anchorMax = new Vector2(0.5f, 1f);
            nr.pivot = new Vector2(0.5f, 0.5f);
            nr.sizeDelta = new Vector2(900f, _bossNameFontSize + 14f);
            nr.anchoredPosition = new Vector2(0f, -(_row1 + _bossNameOffsetY));
            nr.localRotation = Quaternion.identity;
            name.alignment = TextAlignmentOptions.Center;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.fontSize = _bossNameFontSize;
            if (_ref != null)                                          // même police (Bangers) que le reste du HUD
            {
                name.font = _ref.font;
                name.fontSharedMaterial = HudBar.OutlinedMaterial(_ref.fontSharedMaterial, 0.22f, new Color32(14, 9, 5, 255));
            }
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    private void ApplyCluster()
    {
        // cristaux de l'ultime (Aether)
        Transform crystals = transform.Find("ActionCluster/UltimateGroup/CrystalBar");
        if (crystals != null)
        {
            Sprite crystal = _crystalSprite != null ? _crystalSprite : HudSprites.Crystal;
            foreach (Image g in crystals.GetComponentsInChildren<Image>(true))
            {
                if (g.transform == crystals || g.GetComponent<TextMeshProUGUI>() != null) continue;
                g.sprite = crystal;
                g.preserveAspect = true;
                ((RectTransform)g.transform).sizeDelta = _crystalSize;
            }
        }

        // Le groupe s'adapte à son contenu : l'ultime et son « ULT x1 / x2 » prennent exactement la place qu'ils occupent,
        // et le groupe entier (pivot au centre) reste centré sur la barre d'XP (voir PlaceBottom).
        Transform cluster = transform.Find("ActionCluster");
        if (cluster != null) ((RectTransform)cluster).pivot = new Vector2(0.5f, 0.5f);
        Transform ult = transform.Find("ActionCluster/UltimateGroup");
        if (ult != null) EnsureFitter(ult.gameObject);
        Transform ultText = transform.Find("ActionCluster/UltimateGroup/UltStackText");
        if (ultText != null)
        {
            EnsureFitter(ultText.gameObject);
            var ultTmp = ultText.GetComponent<TextMeshProUGUI>();
            if (ultTmp != null) { ultTmp.enableAutoSizing = false; ultTmp.fontSize = _ultStackFontSize; }
        }

        foreach (CaptionRef c in _captions)
        {
            if (c.text == null) continue;
            c.text.fontSize = _captionFontSize;
            c.text.color = _captionColor;
            c.rt.anchoredPosition = new Vector2(c.offsetX != null ? c.offsetX() : 0f, c.halfHeight() + _captionGap);
        }
    }

    private static void EnsureFitter(GameObject go)
    {
        var fit = go.GetComponent<ContentSizeFitter>();
        if (fit == null) fit = go.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    private RectTransform _hudRt, _xpBarRt, _xpGroupRt, _clusterRt;
    private int _layoutSig;

    // Bas de l'écran : même marge sous la barre d'XP et entre la barre d'XP et le bas des éléments d'info au-dessus ;
    // ces éléments restent centrés sur la barre d'XP, même quand leur nombre change en cours de partie.
    // Calculé en coordonnées du HUD : reste juste quelle que soit l'échelle d'interface.
    private void PlaceBottom()
    {
        if (_hudRt == null)
        {
            _hudRt = (RectTransform)transform;
            var bar = Find<Slider>("XpGroup/XPBar");
            _xpBarRt = bar != null ? (RectTransform)bar.transform : null;
            Transform g = transform.Find("XpGroup");
            _xpGroupRt = g != null ? (RectTransform)g : null;
            Transform c = transform.Find("ActionCluster");
            _clusterRt = c != null ? (RectTransform)c : null;
        }
        if (_xpBarRt == null || _xpGroupRt == null) return;

        Rect hud = _hudRt.rect;
        float xpTotal = _xpThickness + 2f * (HudSprites.FrameThickness / 1.4f - 0.4f);   // barre + cadre

        Vector3 center = _hudRt.InverseTransformPoint(_xpBarRt.TransformPoint(_xpBarRt.rect.center));
        float curY = center.y - hud.yMin;
        float wantY = _bottomMargin + xpTotal * 0.5f;
        if (Mathf.Abs(wantY - curY) > 0.05f)
            _xpGroupRt.anchoredPosition += new Vector2(0f, wantY - curY);

        if (_clusterRt == null) return;

        // Quand un élément apparaît / disparaît (« ULT x1 », clone, bouclier...), la chaîne de layouts (texte → groupe de
        // l'ultime → groupe du bas) est recalculée tout de suite plutôt qu'en plusieurs images : pas de saut visible.
        int sig = 17;
        for (int i = 0; i < _clusterRt.childCount; i++)
        {
            Transform ch = _clusterRt.GetChild(i);
            sig = sig * 31 + (ch.gameObject.activeSelf ? 1 : 0);
        }
        Transform ultText = _clusterRt.Find("UltimateGroup/UltStackText");
        if (ultText != null) sig = sig * 31 + (ultText.gameObject.activeSelf ? 1 : 0) + ultText.GetComponent<TextMeshProUGUI>().text.GetHashCode();
        Transform crystals = _clusterRt.Find("UltimateGroup/CrystalBar");
        if (crystals != null)
            for (int i = 0; i < crystals.childCount; i++) sig = sig * 31 + (crystals.GetChild(i).gameObject.activeSelf ? 1 : 0);
        if (sig != _layoutSig)
        {
            _layoutSig = sig;
            if (ultText != null && ultText.gameObject.activeInHierarchy) ultText.GetComponent<TextMeshProUGUI>().ForceMeshUpdate();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_clusterRt);
        }

        // Position horizontale : centrée sur la barre d'XP. Position verticale : on MESURE le bas réel des éléments visibles
        // (esquive, cristaux, bouclier... ; pas la pastille du clone, plus grande) et on corrige jusqu'à ce que l'écart avec
        // le haut de la barre d'XP soit égal à la marge du bas.
        float x = center.x - hud.center.x;
        float wantBottom = _bottomMargin * 2f + xpTotal;               // distance écran -> bas des éléments
        float have = MeasureElementsBottom(hud);
        float y = _clusterRt.anchoredPosition.y;
        if (float.IsNaN(have)) y = wantBottom + 20f;                    // rien de mesurable : valeur de départ
        else if (Mathf.Abs(wantBottom - have) > 0.05f) y += wantBottom - have;
        _clusterRt.anchoredPosition = new Vector2(x, y);
    }

    // Bas (distance depuis le bas du HUD) du plus bas élément d'info visible du groupe ; NaN si aucun.
    private float MeasureElementsBottom(Rect hud)
    {
        float lowest = float.NaN;
        var corners = new Vector3[4];
        foreach (CaptionRef c in _captions)
        {
            if (!c.refForGap || c.rt == null || !c.rt.parent.gameObject.activeInHierarchy) continue;
            foreach (Graphic g in c.rt.parent.GetComponentsInChildren<Graphic>(false))
            {
                if (!g.enabled || g.color.a < 0.01f || g.transform.IsChildOf(c.rt)) continue;
                g.rectTransform.GetWorldCorners(corners);
                for (int i = 0; i < 4; i++)
                {
                    float b = _hudRt.InverseTransformPoint(corners[i]).y - hud.yMin;
                    if (float.IsNaN(lowest) || b < lowest) lowest = b;
                }
            }
        }
        return lowest;
    }

    // ---------------------------------------------------------------------------------------------------------------
    private void ApplyTopRight()
    {
        RightRow(Find<TextMeshProUGUI>("StatsGroup/GoldText"), "GoldIcon", -_row1, _rightTextFontSize);
        RightRow(Find<TextMeshProUGUI>("StatsGroup/KillCountText"), "KillCountIcon", -_row2, _rightTextFontSize);

        // défi : l'icône passe à DROITE (même colonne que l'or et les éliminations), texte aligné à droite juste à côté
        Transform cg = transform.Find("ChallengeGroup");
        if (cg == null) return;

        var rt = (RectTransform)cg;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-_rightMargin, -_row3);

        var layout = cg.GetComponent<HorizontalLayoutGroup>();
        if (layout != null) layout.spacing = _iconGap;
        var fit = cg.GetComponent<ContentSizeFitter>();
        if (fit != null) fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;   // le bord droit du groupe = le bord droit de l'icône

        var challengeText = cg.Find("ChallengeText");
        if (challengeText != null)
        {
            var tmp = challengeText.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.alignment = TextAlignmentOptions.MidlineRight;
                tmp.fontSize = _rightTextFontSize;
                // la scène décalait ce texte par une marge gauche négative (-61 px) pour compenser l'ancien ordre du groupe
                Vector4 m = tmp.margin;
                tmp.margin = new Vector4(0f, m.y, 0f, m.w);
                var tle = challengeText.GetComponent<LayoutElement>();
                if (tle != null) tle.minHeight = tle.preferredHeight = _rightTextFontSize + 16f;
            }
        }

        Transform icon = cg.Find("ChallengeIcon");
        if (icon != null)
        {
            icon.SetAsLastSibling();
            // l'icône est tournée de 180° autour d'un pivot à GAUCHE : dans un layout elle se dessinait à gauche de sa case.
            // Pivot au centre = elle reste dans sa case, quelle que soit la rotation.
            ((RectTransform)icon).pivot = new Vector2(0.5f, 0.5f);
            var le = icon.GetComponent<LayoutElement>();
            if (le != null) { le.minWidth = le.preferredWidth = _rowIconSize; le.minHeight = le.preferredHeight = _rowIconSize; }
            Image img = icon.GetComponent<Image>();
            if (img != null) img.preserveAspect = true;
        }
    }

    private void RightRow(TextMeshProUGUI text, string iconName, float y, float fontSize)
    {
        if (text == null) return;
        var rt = text.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(560f, fontSize + 14f);
        rt.anchoredPosition = new Vector2(-(_rightMargin + _rowIconSize + _iconGap), y);
        text.alignment = TextAlignmentOptions.MidlineRight;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.fontSize = fontSize;

        Transform icon = text.transform.Find(iconName);
        if (icon == null) return;
        var ir = (RectTransform)icon;
        ir.anchorMin = ir.anchorMax = new Vector2(1f, 0.5f);
        ir.pivot = new Vector2(0f, 0.5f);
        ir.sizeDelta = new Vector2(_rowIconSize, _rowIconSize);
        ir.anchoredPosition = new Vector2(_iconGap, 0f);
        Image img = icon.GetComponent<Image>();
        if (img != null) img.preserveAspect = true;
    }

    private void ApplyTimer()
    {
        if (_ref == null) return;
        _ref.fontSize = _timerFontSize;
        var rt = _ref.rectTransform;
        rt.sizeDelta = new Vector2(320f, _timerFontSize + 12f);
        rt.anchoredPosition = new Vector2(0f, -_row1);
    }

    // ---------------------------------------------------------------------------------------------------------------
    private void LateUpdate()
    {
        Safe(PlaceBottom);
    }

    private void Update()
    {
        if (_dirty) { _dirty = false; Apply(); }

        // Le clone redevient prêt : la pastille « pulse » une fois.
        if (_cloneFill == null || _cloneRt == null) return;
        float f = _cloneFill.fillAmount;
        if (_clonePrev >= 0f && _clonePrev < 0.999f && f >= 0.999f) _clonePulse = 1f;
        _clonePrev = f;

        if (_clonePulse > 0f)
        {
            _clonePulse = Mathf.MoveTowards(_clonePulse, 0f, Time.unscaledDeltaTime * 2.2f);
            float p = 1f - _clonePulse;
            _cloneRt.localScale = Vector3.one * (1f + 0.22f * Mathf.Sin(p * Mathf.PI));
            _cloneRim.color = Color.Lerp(RimTint, new Color(1f, 0.95f, 0.75f), Mathf.Sin(p * Mathf.PI) * 0.85f);
        }
        else if (_cloneRt.localScale != Vector3.one)
        {
            _cloneRt.localScale = Vector3.one;
            _cloneRim.color = RimTint;
        }
    }
}
