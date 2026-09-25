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

    [Tooltip("Décalage vertical (vers le haut) du texte « Éliminations » : l'accent du É baissait sa ligne de ~6,5 px par rapport à l'axe de son icône.")]
    [SerializeField] private float _killsTextOffsetY = 6.5f;
    [Tooltip("Taille de police de la ligne du défi (plus longue que les deux autres : « Montée en puissance — Niv. 2/10 »).")]
    [SerializeField] private float _challengeFontSize = 31f;
    [Tooltip("Taille de chaque icône de la colonne. Réglées pour que leur partie VISIBLE fasse la même hauteur (~49 px) : ainsi les écarts entre lignes sont identiques. Toutes sont centrées sur l'axe de la colonne.")]
    [SerializeField] private Vector2 _goldIconSize = new Vector2(51.5f, 50f);
    [SerializeField] private Vector2 _killsIconSize = new Vector2(58.5f, 58.5f);
    [SerializeField] private Vector2 _challengeIconSize = new Vector2(65.5f, 51f);

    [Header("Barre de vie")]
    [SerializeField] private float _hpLength = 520f;
    [Tooltip("Épaisseur intérieure de la barre (le cadre s'ajoute autour).")]
    [SerializeField] private float _hpThickness = 32f;
    [Tooltip("Diamètre du médaillon du cœur.")]
    [SerializeField] private float _hpMedallion = 64f;
    [Tooltip("Décalage du début de la barre : plus grand = la barre commence plus loin du médaillon.")]
    [SerializeField] private float _hpBarInset = 44f;
    [SerializeField] private float _hpFontSize = 26f;
    [Tooltip("Marge gauche (Left) du texte « 1850 / 2000 » dans la barre.")]
    [SerializeField] private float _hpTextLeft = 42f;
    [Tooltip("Marge droite (Right) du texte : comme dans l'inspecteur de Unity, une valeur négative fait dépasser le texte à droite.")]
    [SerializeField] private float _hpTextRight = -10f;
    [Tooltip("Coché : la barre de vie change de couleur selon les points de vie (vert, vert clair, orange, rouge), comme à l'origine. Décoché : couleur fixe « Life Color » de la palette.")]
    [SerializeField] private bool _lifeColorByTier = true;

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
    [SerializeField] private float _bossIconY = 310f;
    [SerializeField] private Vector2 _bossIconSize = new Vector2(120f, 90f);
    [SerializeField] private float _bossNameFontSize = 42f;
    [Tooltip("Distance entre la ligne 1 et le nom du boss (vers le bas).")]
    [SerializeField] private float _bossNameOffsetY = 68f;

    [Header("Groupe du bas")]
    [Tooltip("Marge entre le bas de l'écran et le bas de la barre d'XP.")]
    [SerializeField] private float _bottomMargin = 60f;
    [Tooltip("Position Y du groupe d'éléments au-dessus de la barre d'XP (Action Cluster), mesurée depuis le bas de l'écran. En X il reste centré sur la barre d'XP, même quand des éléments apparaissent (ULT x1 / x2...).")]
    [SerializeField] private float _clusterY = 150f;
    [Tooltip("Décalage horizontal du texte « ESQUIVE ».")]
    [SerializeField] private float _dashCaptionX = 0f;
    [SerializeField] private float _captionFontSize = 27f;
    [SerializeField] private Color _captionColor = new Color(0.96f, 0.89f, 0.72f, 1f);
    [Tooltip("Espace entre le haut d'un élément et sa légende.")]
    [SerializeField] private float _captionGap = 6f;
    [SerializeField] private float _dashLength = 210f;
    [SerializeField] private float _dashThickness = 18f;
    [Tooltip("Taille de police du « ULT x1 / x2 » à droite des cristaux.")]
    [SerializeField] private float _ultStackFontSize = 32f;
    [Tooltip("Taille d'un cristal de l'ultime.")]
    [SerializeField] private Vector2 _crystalSize = new Vector2(34f, 34f);
    [Header("Palette (une couleur = un rôle)")]
    [Tooltip("Vie du joueur, si « Life Color By Tier » est décoché : rouge cramoisi fixe (comme Hades, Brotato, Vampire Survivors).")]
    [SerializeField] private Color _lifeColor = new Color32(0xC7, 0x2E, 0x33, 255);
    [Tooltip("Boss : rouge (choix de l'utilisateur).")]
    [SerializeField] private Color _bossColor = new Color32(0xD1, 0x2B, 0x2B, 255); // rouge boss (demande utilisateur, 2026-09-24)
    [Tooltip("Nom du boss : rouge.")]
    [SerializeField] private Color _bossNameColor = new Color32(0xE8, 0x3A, 0x32, 255); // nom du boss en rouge (demande utilisateur, 2026-09-24)
    [Tooltip("Barre d'XP : bleu azur (l'or est réservé à la monnaie et aux états « prêt / maximum »).")]
    [SerializeField] private Color _xpColor = new Color32(0x3E, 0x84, 0xD6, 255);
    [Tooltip("Esquive : argent bleuté (mobilité).")]
    [SerializeField] private Color _dashColor = new Color32(0xBC, 0xD2, 0xE3, 255);
    [Tooltip("Ultime / magie : turquoise sourd (cristaux chargés, « ULT x1 », Concentration).")]
    [SerializeField] private Color _crystalFilledColor = new Color32(0x6F, 0xDC, 0xEB, 255);   // bleu clair (retour utilisateur, 2026-09-24)
    [Tooltip("Cristal vide.")]
    [SerializeField] private Color _crystalEmptyColor = new Color32(0x7C, 0x93, 0xA6, 150);   // gris-bleu translucide : lisible sur l'herbe
    [Tooltip("Palier maximum / prêt : or.")]
    [SerializeField] private Color _maxColor = new Color32(0xF2, 0xBE, 0x3A, 255);
    [Tooltip("Pions du bouclier de mana de Kael : vert émeraude (couleur de Kael).")]
    [SerializeField] private Color _shieldColor = new Color32(0x4C, 0xB0, 0x6A, 255);
    [SerializeField] private Color _shieldLockedColor = new Color32(0x8A, 0x5A, 0x5A, 200);
    [Tooltip("Clone de Lyra : lilas clair (couleur de sa magie), moins saturé que le violet du boss.")]
    [SerializeField] private Color _cloneColor = new Color32(0xA9, 0x93, 0xE6, 255);
    [Tooltip("Compteur d'or : or chaud.")]
    [SerializeField] private Color _goldTextColor = new Color32(0xF5, 0xC1, 0x4A, 255);

    [Header("Icônes personnalisées (optionnel : laissées vides, des icônes dessinées en code sont utilisées)")]
    [Tooltip("Icône de la barre de vie (cœur). Glissez ici une image peinte pour remplacer l'icône dessinée en code.")]
    [SerializeField] private Sprite _heartSprite;
    [Tooltip("Pastille du clone (Lyra). Le sprite sert de fond assombri, et se « révèle » en couleur pendant la recharge ; la touche reste écrite au centre.")]
    [SerializeField] private Sprite _cloneSprite;
    [Tooltip("Pion du bouclier de mana (Kael). Prévoir une image neutre (blanc / gris) : la palette la teinte (plein / vide / verrouillé).")]
    [SerializeField] private Sprite _shieldPipSprite;
    [Tooltip("Icône de l'or (haut à droite).")]
    [SerializeField] private Sprite _goldSprite;
    [Tooltip("Icône des éliminations (haut à droite).")]
    [SerializeField] private Sprite _killsSprite;
    [Tooltip("Icône du défi (haut à droite). Une image fournie ici est affichée à l'endroit (l'ancienne était tournée de 180°).")]
    [SerializeField] private Sprite _challengeSprite;
    [Tooltip("Portrait au-dessus de la barre du boss. Peinte directement dans la couleur du boss (pas de teinte automatique) : la palette ne la recolore pas.")]
    [SerializeField] private Sprite _bossIconSprite;
    [Tooltip("Coché : les images ci-dessus contiennent déjà leur cadre (médaillon peint). Le cadre bronze dessiné en code est alors retiré et l'image remplit tout le médaillon. Décoché : l'image est posée DANS le cadre bronze du jeu.")]
    [SerializeField] private bool _iconsHaveOwnFrame = false;
    [Tooltip("Sprite de cristal (optionnel) : laissé vide, un cristal dessiné en code est utilisé. Glissez ici un sprite pour le remplacer.")]
    [SerializeField] private Sprite _crystalSprite;

    private static readonly Color RimTint = Color.white;

    private TextMeshProUGUI _ref;              // texte de référence (police / matériau Bangers)
    private bool _dirty;

    // pastille du clone
    private Image _cloneFill, _cloneRim, _cloneBg;
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
        Safe(BuildKeyLegend);
        Safe(Apply);
    }

    // Légende des touches à gauche (masquable dans Paramètres > Interface) : voir HudKeyLegend.
    private void BuildKeyLegend()
    {
        if (GetComponentInChildren<HudKeyLegend>(true) != null) return;
        HudKeyLegend.Create(transform, _ref);
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
                _cloneBg = bg;
                bg.sprite = HudSprites.Disc;
                bg.color = Color.Lerp(Color.black, _cloneColor, 0.20f);
            }
            if (fillT != null)
            {
                _cloneFill = fillT.GetComponent<Image>();
                _cloneFill.sprite = HudSprites.Pip;
                _cloneFill.color = _cloneColor;
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
            _cloneRim.sprite = HudBar.FlatStyle ? HudSprites.RingFlat : HudSprites.Ring;   // liseré fin en style plat
            _cloneRim.color = RimTint;
            _cloneRim.raycastTarget = false;
            var rr = (RectTransform)ring.transform;
            rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one;
            float rimGrow = HudBar.FlatStyle ? 2f : 5f;
            rr.offsetMin = new Vector2(-rimGrow, -rimGrow); rr.offsetMax = new Vector2(rimGrow, rimGrow);

            AddCaption(cd, "CLONE", () => 35f).refForGap = false;
        }

        var dash = Find<Slider>("ActionCluster/DashCooldownBar");
        if (dash != null) AddCaption(dash.transform, "ESQUIVE", () => Mathf.Max(_dashThickness, _crystalSize.y) * 0.5f, () => _dashCaptionX);   // même ligne de base que « ULTIME »

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
            t.fontSharedMaterial = HudBar.OutlinedMaterial(_ref.fontSharedMaterial, 0.23f, new Color32(10, 6, 3, 255));
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
            var hpText = Find<TextMeshProUGUI>("Health/HPText");
            HudBar life = HudBar.Attach(hp, HudBar.Kind.Health, _hpThickness, _hpBarInset, _heartSprite != null ? _heartSprite : HudSprites.Heart,
                          Color.Lerp(_lifeColor, Color.white, 0.12f), _hpMedallion, hpText, _hpFontSize, false, 1f, _heartSprite != null && _iconsHaveOwnFrame);
            if (life != null)
            {
                if (_lifeColorByTier) life.ClearFixedColor();       // couleurs d'origine de GameUI (paliers de vie), en tons un peu adoucis
                else life.SetFixedColor(_lifeColor);
            }
            if (hpText != null)                                       // décalage du texte de vie (Attach remet les marges d'origine)
            {
                var tr = hpText.rectTransform;                        // valeurs Left / Right de l'inspecteur (Right négatif = dépasse)
                tr.offsetMin = new Vector2(_hpTextLeft, tr.offsetMin.y);
                tr.offsetMax = new Vector2(-_hpTextRight, tr.offsetMax.y);
            }
        }

        HudBar xp = HudBar.Attach(Find<Slider>("XpGroup/XPBar"), HudBar.Kind.Xp, _xpThickness, 0f, null, Color.white, 0f, null, 0f, false, 1.4f);
        if (xp != null) xp.SetFixedColor(_xpColor);

        var dash = Find<Slider>("ActionCluster/DashCooldownBar");
        if (dash != null)
        {
            RectTransform drt = (RectTransform)dash.transform;
            drt.sizeDelta = new Vector2(_dashLength, drt.sizeDelta.y);
            // barre moderne sans médaillon ni icône : pastille arrondie + liseré fin (voir HudBar, Kind.Dash)
            HudBar dashBar = HudBar.Attach(dash, HudBar.Kind.Dash, _dashThickness, 0f, null, _dashColor, 0f, null, 0f, false, 2f, false);
            if (dashBar != null) dashBar.SetFixedColor(_dashColor);
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
            if (boss != null) { boss.hitFlashQuietTime = _bossHitFlashQuietTime; boss.SetFixedColor(_bossColor); }

            var icon = Find<Image>("BossGroup/BossIcon");
            if (icon != null)
            {
                var ir = icon.rectTransform;
                ir.anchorMin = ir.anchorMax = new Vector2(1f, 1f);
                ir.pivot = new Vector2(0.5f, 0.5f);
                ir.sizeDelta = _bossIconSize;
                ir.anchoredPosition = new Vector2(cx, -_bossIconY);
                icon.preserveAspect = true;
                if (_bossIconSprite != null) { icon.sprite = _bossIconSprite; icon.color = Color.white; }
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
            name.color = _bossNameColor;
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
        // palette : paliers d'ultime / Concentration (partagés avec le reste de l'interface), pions du bouclier, clone
        GameUI.SetTierColors(_crystalEmptyColor, _crystalFilledColor, _maxColor);
        var gameUi = Object.FindFirstObjectByType<GameUI>();
        if (gameUi != null) gameUi.SetShieldColors(_shieldColor, _shieldLockedColor);
        if (_cloneFill != null)
        {
            if (_cloneSprite != null)
            {
                // image peinte : fond = version assombrie, remplissage radial = la même image en couleurs ; plus d'anneau de bronze
                _cloneFill.sprite = _cloneSprite; _cloneFill.color = Color.white;
                if (_cloneBg != null) { _cloneBg.sprite = _cloneSprite; _cloneBg.color = new Color(0.28f, 0.28f, 0.30f, 1f); }
                if (_cloneRim != null) _cloneRim.enabled = false;
            }
            else
            {
                _cloneFill.sprite = HudSprites.Pip; _cloneFill.color = _cloneColor;
                if (_cloneBg != null) { _cloneBg.sprite = HudSprites.Disc; _cloneBg.color = Color.Lerp(Color.black, _cloneColor, 0.20f); }
                if (_cloneRim != null) _cloneRim.enabled = true;
            }
        }
        Transform shieldGroup = transform.Find("ActionCluster/ManaShieldGroup");
        if (shieldGroup != null)
            foreach (Image g in shieldGroup.GetComponentsInChildren<Image>(true))
                if (g.transform != shieldGroup && g.GetComponent<TextMeshProUGUI>() == null) g.sprite = _shieldPipSprite != null ? _shieldPipSprite : HudSprites.Pip;
        var goldTxt = Find<TextMeshProUGUI>("StatsGroup/GoldText");
        if (goldTxt != null) goldTxt.color = _goldTextColor;

        // cristaux de l'ultime (Aether)
        Transform crystals = transform.Find("ActionCluster/UltimateGroup/CrystalBar");
        if (crystals != null)
        {
            Sprite crystal = _crystalSprite != null ? _crystalSprite : HudSprites.Diamond;   // losanges (carrés à 45°)
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

    // Bas de l'écran : la barre d'XP est placée à _bottomMargin du bas ; le groupe d'éléments au-dessus est centré sur elle.
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

        // Horizontalement le groupe reste centré sur la barre d'XP (il s'élargit des deux côtés quand un élément apparaît) ;
        // verticalement il est à la position réglée dans l'inspecteur.
        _clusterRt.anchoredPosition = new Vector2(center.x - hud.center.x, _clusterY);
    }

    // ---------------------------------------------------------------------------------------------------------------
    private void ApplyTopRight()
    {
        RightRow(Find<TextMeshProUGUI>("StatsGroup/GoldText"), "GoldIcon", -_row1, _rightTextFontSize, _goldIconSize, _goldSprite);
        RightRow(Find<TextMeshProUGUI>("StatsGroup/KillCountText"), "KillCountIcon", -_row2 + _killsTextOffsetY, _rightTextFontSize, _killsIconSize, _killsSprite, -_killsTextOffsetY);

        // défi : l'icône passe à DROITE (même colonne que l'or et les éliminations), texte aligné à droite juste à côté
        Transform cg = transform.Find("ChallengeGroup");
        if (cg == null) return;

        var rt = (RectTransform)cg;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-(_rightMargin + _rowIconSize * 0.5f) + _challengeIconSize.x * 0.5f, -_row3);   // centre de l'icône sur l'axe de la colonne

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
                tmp.fontSize = _challengeFontSize;
                // la scène décalait ce texte par une marge gauche négative (-61 px) pour compenser l'ancien ordre du groupe
                // marges haute/basse égales (0) : la scène avait 9 px en bas seulement, ce qui remontait le texte de ~4 px
                // au-dessus de l'axe de son icône
                tmp.margin = Vector4.zero;
                var tle = challengeText.GetComponent<LayoutElement>();
                if (tle != null) tle.minHeight = tle.preferredHeight = _rightTextFontSize + 16f;   // hauteur de ligne inchangée (alignement de l'icône)
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
            if (le != null) { le.minWidth = le.preferredWidth = _challengeIconSize.x; le.minHeight = le.preferredHeight = _challengeIconSize.y; }
            Image img = icon.GetComponent<Image>();
            if (img != null)
            {
                img.preserveAspect = true;
                if (_challengeSprite != null) { img.sprite = _challengeSprite; icon.localEulerAngles = Vector3.zero; }
            }
        }
    }

    private void RightRow(TextMeshProUGUI text, string iconName, float y, float fontSize, Vector2 iconSize, Sprite custom, float iconY = 0f)
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
        ir.pivot = new Vector2(0.5f, 0.5f);
        ir.sizeDelta = iconSize;
        // centre de l'icône = axe commun de la colonne (à _rightMargin + _rowIconSize/2 du bord), quelle que soit sa largeur
        ir.anchoredPosition = new Vector2(_iconGap + _rowIconSize * 0.5f, iconY);   // iconY compense un décalage du texte (l'icône garde son axe)
        Image img = icon.GetComponent<Image>();
        if (img != null) { img.preserveAspect = true; if (custom != null) img.sprite = custom; }
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
