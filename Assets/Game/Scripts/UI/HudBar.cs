using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Habillage animé d'une barre du HUD (Slider) : cadre de métal forgé (bronze biseauté, liseré d'or, coins coupés), fond de
// cuir sombre, remplissage aux tons riches avec un fin liseré de lumière, médaillon d'icône. Horizontale ou verticale.
// Le Slider reste la SOURCE de vérité (GameUI continue d'écrire .value et la couleur de son « Fill ») ; ses images d'origine
// (fond, remplissage, poignée) sont masquées et ce composant dessine le résultat à la place, avec :
//   • un remplissage qui glisse (au lieu de sauter) ;
//   • une traînée claire qui rattrape lentement le remplissage quand on perd de la vie (vie, boss) ;
//   • un éclat blanc quand on est touché / quand la barre devient pleine (esquive prête) ;
//   • une barre d'XP qui se remplit jusqu'au bout puis éclate au passage de niveau ;
//   • une pulsation rouge du cadre et du cœur quand la vie est basse.
public class HudBar : MonoBehaviour
{
    public enum Kind { Health, Boss, Xp, Dash }

    private static readonly Color TrackColor = new Color(0.085f, 0.06f, 0.045f, 0.96f);   // cuir sombre
    private static readonly Color TrailColor = new Color(1f, 0.86f, 0.70f, 0.72f);
    private static readonly Color AlarmColor = new Color(1f, 0.30f, 0.24f, 1f);

    private Slider _slider;
    private Kind _kind;
    private bool _vertical;
    private Image _srcFill;
    private RectTransform _fillRt, _trailRt, _iconRt;
    private Image _fill, _trail, _frame, _medRim;
    private GameObject _trailGo;

    private float _shown, _trailV, _vel, _flash, _trailHold, _prevTarget, _lastFillAnchor = -1f, _lastTrailAnchor = -1f;
    private bool _init, _levelUp;
    private TextMeshProUGUI _valueText;

    // Éclat « coup reçu » : avec une pluie de coups (bullet heaven) la barre clignoterait sans arrêt. Si > 0, l'éclat ne
    // se rejoue qu'après ce délai (en secondes) SANS aucun coup ; chaque coup remet le compteur à zéro.
    public float hitFlashQuietTime = 0f;
    public float FlashAmount { get { return _flash; } }        // lecture seule (tests)
    private float _lastHitTime = -999f;                       // horloge réelle : insensible aux images longues

    // Matériau TMP dérivé avec un contour de largeur / couleur données (mis en cache). On passe par le matériau plutôt que par
    // TMP_Text.outlineWidth, qui plante sur un texte pas encore initialisé (objet inactif au moment du Awake).
    private static readonly System.Collections.Generic.Dictionary<string, Material> OutlineMaterials =
        new System.Collections.Generic.Dictionary<string, Material>();

    public static Material OutlinedMaterial(Material source, float width, Color32 color)
    {
        if (source == null) return null;
        string key = source.GetInstanceID() + "_" + width;
        Material m;
        if (OutlineMaterials.TryGetValue(key, out m) && m != null) return m;
        m = new Material(source) { name = source.name + " (HUD contour)", hideFlags = HideFlags.HideAndDontSave };
        m.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
        m.SetColor(ShaderUtilities.ID_OutlineColor, color);
        OutlineMaterials[key] = m;
        return m;
    }

    // ---------------------------------------------------------------------------------------------------------------
    // thickness : épaisseur intérieure de la barre (sa hauteur, ou sa largeur si verticale) ; frameScale : 1 = cadre de 7 px,
    // 2 = cadre de 3,6 px (barres fines) ; leftInset : place réservée au médaillon (barre horizontale).
    public static HudBar Attach(Slider slider, Kind kind, float thickness, float leftInset, Sprite icon, Color iconColor,
                                float medallionDiameter, TextMeshProUGUI valueText, float valueFontSize,
                                bool vertical = false, float frameScale = 1f)
    {
        if (slider == null) return null;
        HudBar bar = slider.GetComponent<HudBar>();
        if (bar != null)
        {
            bar.Reconfigure(thickness, leftInset, icon, iconColor, medallionDiameter, valueText, valueFontSize, frameScale);   // réglages modifiés en jeu
            return bar;
        }

        bar = slider.gameObject.AddComponent<HudBar>();
        bar._slider = slider;
        bar._kind = kind;
        bar._vertical = vertical;
        bar._srcFill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;

        // masque tout ce que dessinait le Slider (fond, remplissage, poignée « Knob » par défaut de Unity)
        foreach (Image g in slider.GetComponentsInChildren<Image>(true)) g.enabled = false;

        bar.Build(thickness, leftInset, icon, iconColor, medallionDiameter, valueText, valueFontSize, frameScale);
        return bar;
    }

    // Reconstruit l'habillage avec de nouvelles dimensions (réglages de l'inspecteur modifiés pendant le jeu).
    private void Reconfigure(float t, float inset, Sprite icon, Color iconColor, float medD, TextMeshProUGUI valueText, float valueFontSize, float frameScale)
    {
        if (_valueText != null) _valueText.transform.SetParent(transform, false);      // le sortir du cadre avant de le détruire
        Transform old = transform.Find("HudBarVisual");
        if (old != null) DestroyImmediate(old.gameObject);
        old = transform.Find("Medallion");
        if (old != null) DestroyImmediate(old.gameObject);
        _lastFillAnchor = _lastTrailAnchor = -1f;
        _init = false;
        Build(t, inset, icon, iconColor, medD, valueText, valueFontSize, frameScale);
    }

    // À l'apparition d'un boss : la barre se remplit devant le joueur (appelé par GameUI.ShowBossHP).
    public void PlayIntro()
    {
        _shown = 0f; _trailV = 0f; _vel = 0f; _prevTarget = 0f; _init = true; _levelUp = false; _lastHitTime = -999f;
        _lastFillAnchor = _lastTrailAnchor = -1f;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        RectTransform rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static void Stretch(RectTransform rt, float grow)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-grow, -grow);
        rt.offsetMax = new Vector2(grow, grow);
    }

    private static Image Sliced(string name, Transform parent, Sprite sprite, Color color, float mult, float grow)
    {
        Image img = NewImage(name, parent, sprite, color);
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = mult;
        Stretch(img.rectTransform, grow);
        return img;
    }

    private void Build(float t, float inset, Sprite icon, Color iconColor, float medD, TextMeshProUGUI valueText, float valueFontSize, float frameScale)
    {
        RectTransform root = NewRect("HudBarVisual", transform);
        root.SetAsFirstSibling();
        if (_vertical)
        {
            root.anchorMin = new Vector2(0.5f, 0f);
            root.anchorMax = new Vector2(0.5f, 1f);
            root.offsetMin = new Vector2(-t * 0.5f, 0f);
            root.offsetMax = new Vector2(t * 0.5f, 0f);
        }
        else
        {
            root.anchorMin = new Vector2(0f, 0.5f);
            root.anchorMax = new Vector2(1f, 0.5f);
            root.offsetMin = new Vector2(inset, -t * 0.5f);
            root.offsetMax = new Vector2(0f, t * 0.5f);
        }

        float frameGrow = HudSprites.FrameThickness / frameScale - 0.4f;
        _frame = Sliced("Frame", root, HudSprites.Frame, Color.white, frameScale, frameGrow);
        Sliced("Track", root, HudSprites.Inset, TrackColor, frameScale, 0f);

        // zone de remplissage : traînée derrière, remplissage devant (étendue pilotée par les ancres)
        RectTransform area = NewRect("FillArea", root);
        Stretch(area, 0f);

        Image trail = Sliced("Trail", area, HudSprites.Inset, TrailColor, frameScale, 0f);
        _trailRt = trail.rectTransform;
        _trail = trail;
        _trailGo = trail.gameObject;
        _trailGo.SetActive(false);                       // allumée par SetAnchor dès qu'il y a quelque chose à montrer
        if (_kind != Kind.Health && _kind != Kind.Boss) Destroy(_trailGo);

        Image fill = Sliced("Fill", area, HudSprites.Inset, Color.white, frameScale, 0f);
        _fillRt = fill.rectTransform;
        _fill = fill;
        Sliced("Sheen", _fillRt, _vertical ? HudSprites.SheenV : HudSprites.SheenH, Color.white, frameScale, 0f);

        _valueText = valueText;
        if (valueText != null)
        {
            valueText.transform.SetParent(root, false);
            RectTransform vt = valueText.rectTransform;
            vt.anchorMin = Vector2.zero;
            vt.anchorMax = Vector2.one;
            vt.offsetMin = new Vector2(16f, -6f);
            vt.offsetMax = new Vector2(-16f, 6f);
            valueText.alignment = TextAlignmentOptions.Center;
            valueText.fontSize = valueFontSize;
            valueText.textWrappingMode = TextWrappingModes.NoWrap;
            valueText.overflowMode = TextOverflowModes.Overflow;
            valueText.raycastTarget = false;
            // contour sombre épais : lisible même sur la traînée claire
            Material outlined = OutlinedMaterial(valueText.fontSharedMaterial, 0.28f, new Color32(12, 8, 5, 255));
            if (outlined != null) valueText.fontSharedMaterial = outlined;
            var le = valueText.GetComponent<LayoutElement>();
            if (le == null) le = valueText.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            valueText.transform.SetAsLastSibling();
        }

        if (icon != null && medD > 0f)
        {
            RectTransform med = NewRect("Medallion", transform);
            med.anchorMin = med.anchorMax = new Vector2(0f, 0.5f);
            med.pivot = new Vector2(0.5f, 0.5f);
            med.sizeDelta = new Vector2(medD, medD);
            med.anchoredPosition = new Vector2(medD * 0.5f, 0f);

            _medRim = NewImage("Ring", med, HudSprites.Ring, Color.white);
            Stretch(_medRim.rectTransform, 0f);
            Image ic = NewImage("Icon", med, icon, iconColor);
            ic.preserveAspect = true;
            _iconRt = ic.rectTransform;
            _iconRt.anchorMin = _iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            _iconRt.sizeDelta = new Vector2(medD * 0.5f, medD * 0.5f);
            _iconRt.anchoredPosition = Vector2.zero;
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    private void OnEnable()
    {
        _init = false;   // le HUD est masqué pendant la pause : au retour, la barre reprend sa valeur sans rejouer d'animation
        _levelUp = false;
    }

    // Tons plus profonds que les couleurs vives d'origine (vert / orange / rouge d'arcade) : plus « pigment » que « néon ».
    private static Color Tone(Color c)
    {
        float h, s, v;
        Color.RGBToHSV(c, out h, out s, out v);
        return Color.HSVToRGB(h, s * 0.80f, v * 0.86f);
    }

    private void Update()
    {
        if (_slider == null || _fillRt == null) return;
        float dt = Time.unscaledDeltaTime;
        float target = _slider.normalizedValue;

        if (!_init) { _shown = _trailV = _prevTarget = target; _vel = 0f; _init = true; }

        // ----- événements -----
        if (_kind == Kind.Xp && target < _prevTarget - 0.35f) _levelUp = true;
        else if ((_kind == Kind.Health || _kind == Kind.Boss) && target < _prevTarget - 0.002f)
        {
            if (Time.realtimeSinceStartup - _lastHitTime >= hitFlashQuietTime) _flash = Mathf.Max(_flash, 0.55f);
            _lastHitTime = Time.realtimeSinceStartup;
            _trailHold = 0.40f;
        }
        if (_kind == Kind.Dash && _prevTarget < 0.999f && target >= 0.999f) _flash = 1f;
        _prevTarget = target;

        // ----- remplissage -----
        if (_levelUp)
        {
            _shown = Mathf.MoveTowards(_shown, 1f, dt * 4f);
            if (_shown >= 0.999f) { _levelUp = false; _flash = 1f; _shown = 0f; _vel = 0f; }
        }
        else
        {
            float smooth = _kind == Kind.Boss ? 0.16f : 0.10f;
            _shown = Mathf.SmoothDamp(_shown, target, ref _vel, smooth, Mathf.Infinity, dt);
            if (Mathf.Abs(_shown - target) < 0.0004f) _shown = target;
        }

        // ----- traînée : elle attend, puis rattrape le remplissage -----
        if (_trailV < _shown) _trailV = _shown;
        else if (_trailHold > 0f) _trailHold -= dt;
        else _trailV = Mathf.MoveTowards(_trailV, _shown, dt * 0.55f);

        _flash = Mathf.MoveTowards(_flash, 0f, dt * 3.5f);

        // ----- vie basse -----
        float pulse = 0f;
        if (_kind == Kind.Health && target > 0f && target < 0.25f)
            pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f);

        // ----- rendu -----
        Color c = _srcFill != null ? _srcFill.color : Color.white;
        c.a = 1f;
        c = Tone(c);
        c = Color.Lerp(c, Color.white, _flash * 0.75f);
        c = Color.Lerp(c, Color.white, pulse * 0.30f);
        _fill.color = c;

        Color tint = Color.Lerp(Color.white, AlarmColor, pulse * 0.9f);   // le bronze rougit en alerte
        _frame.color = tint;
        if (_medRim != null) _medRim.color = tint;
        if (_iconRt != null) _iconRt.localScale = Vector3.one * (1f + 0.14f * pulse);

        SetAnchor(_fillRt, _shown, ref _lastFillAnchor);
        if (_kind == Kind.Health || _kind == Kind.Boss) SetAnchor(_trailRt, _trailV, ref _lastTrailAnchor);
    }

    private void SetAnchor(RectTransform rt, float amount, ref float last)
    {
        amount = Mathf.Clamp01(amount);
        if (Mathf.Approximately(amount, last)) return;
        last = amount;
        bool show = amount > 0.0015f;
        if (rt.gameObject.activeSelf != show) rt.gameObject.SetActive(show);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = _vertical ? new Vector2(1f, amount) : new Vector2(amount, 1f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
