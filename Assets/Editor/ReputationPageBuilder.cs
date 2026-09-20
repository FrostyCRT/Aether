using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// OUTIL D'ÉDITEUR (2026-09-20) - (re)construit l'onglet Réputation dans la scène MainMenu (menu :
// Aether > Rebuild Reputation Page). Idempotent : supprime ce qu'il avait construit (tout sauf l'illustration de fond
// « Background ») et le refait à neuf ; crée aussi Assets/Resources/SkinCatalog.asset (catalogue des skins) s'il
// n'existe pas. La hiérarchie reste modifiable à la main ; les composants (ReputationUI, ReputationStatCardUI,
// SkinCardUI, ReputationHallFX) ne construisent RIEN à l'exécution, sauf les cartes de skins (copies du modèle).
//
// Repère de construction : "Page" = zone sous la barre d'onglets (1920 x 980), coordonnées depuis le coin HAUT-GAUCHE
// (x vers la droite, y vers le bas).
public static class ReputationPageBuilder
{
    private const string Ui = "Assets/Game/Assets 2D/UI/";
    private const string Chars = "Assets/Game/Assets 2D/Player/";

    private static TMP_FontAsset _font;
    private static Material _labelMat, _additive, _desaturate;
    private static Sprite _circle, _roundRect, _coin, _eclat, _padlock, _parchemin;
    private static Sprite[] _portraits;

    private static readonly Color PanelDark = ReputationStyle.PanelDark;
    private static readonly Color Field = ReputationStyle.Field;
    private static readonly Color Muted = ReputationStyle.Muted;
    private static readonly Color Body = ReputationStyle.Body;

    [MenuItem("Aether/Rebuild Reputation Page")]
    public static void Build()
    {
        LoadAssets();

        ReputationUI existing = Object.FindFirstObjectByType<ReputationUI>(FindObjectsInactive.Include);
        if (existing == null) { Debug.LogError("[ReputationPageBuilder] ReputationUI introuvable."); return; }
        RectTransform root = (RectTransform)existing.transform;

        SkinCatalog catalog = EnsureCatalog();

        // 1) on retire tout sauf l'illustration de fond
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform c = root.GetChild(i);
            if (c.name == "Background") continue;
            Object.DestroyImmediate(c.gameObject);
        }

        // 2) fond : voile sombre (les cartes et les textes doivent ressortir) + ambiance vivante
        RectTransform dim = NewRt("Dim", root);
        Stretch(dim);
        AddImage(dim, null, new Color(0.03f, 0.02f, 0.05f, 0.46f));

        RectTransform ambient = NewRt("Ambient", root);
        Stretch(ambient);
        ReputationHallFX fx = ambient.gameObject.AddComponent<ReputationHallFX>();
        SerializedObject fxso = new SerializedObject(fx);
        fxso.FindProperty("_additiveMaterial").objectReferenceValue = _additive;
        fxso.ApplyModifiedPropertiesWithoutUndo();

        // 3) page (sous la barre d'onglets du menu : 100 px)
        RectTransform page = NewRt("Page", root);
        Stretch(page, 0, 100, 0, 0);

        // ---------------- en-tête ----------------
        CanvasGroup headerGroup = Block(page, "Header", 0, 0, 1920, 142);
        RectTransform header = (RectTransform)headerGroup.transform;

        RectTransform title = NewRt("Title", header);
        TL(title, 60, 12, 720, 88);
        AddText(title, "RÉPUTATION", 84, Color.white, TextAlignmentOptions.TopLeft, false, true);

        RectTransform subtitle = NewRt("Subtitle", header);
        TL(subtitle, 64, 96, 900, 32);
        AddText(subtitle, "Renforce tes héros pour de bon, et change leur apparence.", 27, Muted, TextAlignmentOptions.TopLeft);

        // outil de développement, discret et clairement étiqueté (comme dans l'onglet Compétences)
        RectTransform reset = Pill("DebugReset", header, 700, 102, 250, 24, new Color32(0x6B, 0x2A, 0x2A, 0xFF), new Color32(0x1A, 0x0F, 0x14, 0xF0));
        Button resetBtn = reset.gameObject.AddComponent<Button>();
        resetBtn.targetGraphic = reset.Find("Fill").GetComponent<Image>();
        ColorBlock rb = resetBtn.colors; rb.normalColor = new Color(0.85f, 0.85f, 0.85f); rb.highlightedColor = Color.white; rb.pressedColor = new Color(0.6f, 0.6f, 0.6f); rb.selectedColor = rb.normalColor;
        resetBtn.colors = rb; resetBtn.navigation = NoNav();
        RectTransform rlab = NewRt("Text", reset); Stretch(rlab);
        AddText(rlab, "DEBUG · RÉINITIALISER", 16, new Color32(0xE2, 0x7B, 0x6E, 0xFF), TextAlignmentOptions.Center);

        RectTransform eclatPill = CurrencyPill("EclatsPill", header, 60, 26, ReputationStyle.Eclat, _eclat, out TextMeshProUGUI eclatText, new Vector2(40, 52));
        RectTransform goldPill = CurrencyPill("GoldPill", header, 60 + 250 + 16, 26, ReputationStyle.Gold, _coin, out TextMeshProUGUI goldText, new Vector2(42, 42));

        // ---------------- bonus permanents ----------------
        CanvasGroup statsGroup = Block(page, "Stats", 0, 146, 1920, 300);
        RectTransform stats = (RectTransform)statsGroup.transform;
        SectionHeader(stats, 60, 0, 1800, "BONUS PERMANENTS", ReputationStyle.Gold, null, "Améliorés avec des Éclats", _eclat, ReputationStyle.Eclat);

        string[] ids = { "reputationDamage", "reputationSpeed", "reputationRegen" };
        Sprite[] icons =
        {
            AssetDatabase.LoadAssetAtPath<Sprite>(Ui + "Réputation/amber_bronze_blade_final.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>(Ui + "Réputation/final_soft_mist.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>(Ui + "Réputation/golden_green_transparent.png"),
        };
        ReputationStatCardUI[] cards = new ReputationStatCardUI[3];
        for (int i = 0; i < 3; i++) cards[i] = BuildStatCard(stats, 60 + i * (570 + 45), 44, ids[i], icons[i]);

        // ---------------- skins ----------------
        CanvasGroup skinsGroup = Block(page, "Skins", 0, 456, 1920, 504);
        RectTransform skins = (RectTransform)skinsGroup.transform;

        RectTransform skinsLabel = NewRt("SkinsLabel", skins);
        TL(skinsLabel, 60, 6, 130, 40);
        AddText(skinsLabel, "SKINS", 34, ReputationStyle.Gold, TextAlignmentOptions.MidlineLeft);
        RectTransform skinsLine = NewRt("Line", skins);
        TL(skinsLine, 60, 50, 1800, 2);
        AddImage(skinsLine, null, new Color(ReputationStyle.Gold.r, ReputationStyle.Gold.g, ReputationStyle.Gold.b, 0.45f));

        string[] names = { "AETHER", "KAEL", "LYRA" };
        ReputationUI.CharTab[] tabs = new ReputationUI.CharTab[3];
        for (int i = 0; i < 3; i++) tabs[i] = BuildCharTab(skins, 190 + i * (200 + 12), 0, names[i], i);

        // aperçu (gauche)
        PreviewRefs pv = BuildPreview(skins, 60, 62);

        // rangées (droite)
        RowRefs classic = BuildRow(skins, 510, 62, "Classic", "SKINS CLASSIQUES", ReputationStyle.Gold, _coin, "Achetés avec de l'or");
        RowRefs prestige = BuildRow(skins, 510, 282, "Prestige", "SKINS PRESTIGE", ReputationStyle.Eclat, _eclat, "Achetés avec des Éclats");

        // modèle de carte de skin (inactif)
        RectTransform templates = NewRt("Templates", page);
        Stretch(templates);
        templates.gameObject.SetActive(false);
        SkinCardUI cardTemplate = BuildSkinCardTemplate(templates);

        // message court
        RectTransform toast = NewRt("Toast", page);
        TL(toast, 560, 40, 700, 54);
        CanvasGroup toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f; toastGroup.blocksRaycasts = false; toastGroup.interactable = false;
        RectTransform tb = NewRt("Border", toast); Stretch(tb); AddImage(tb, _roundRect, ReputationStyle.Gold, true);
        RectTransform tf = NewRt("Fill", toast); Stretch(tf, 2, 2, 2, 2); AddImage(tf, _roundRect, new Color(PanelDark.r, PanelDark.g, PanelDark.b, 0.97f), true);
        RectTransform tt = NewRt("Text", toast); Stretch(tt, 16, 0, 16, 0);
        TextMeshProUGUI toastText = AddText(tt, "", 28, Color.white, TextAlignmentOptions.Center);
        toastText.enableAutoSizing = true; toastText.fontSizeMin = 18; toastText.fontSizeMax = 28;

        // ---------------- câblage ReputationUI ----------------
        SerializedObject so = new SerializedObject(existing);
        Set(so, "_goldText", goldText); Set(so, "_eclatsText", eclatText);
        Set(so, "_goldPill", goldPill); Set(so, "_eclatsPill", eclatPill);
        SetArray(so, "_statCards", cards);
        SetArray(so, "_introBlocks", new Object[] { headerGroup, statsGroup, skinsGroup });
        Set(so, "_classicContent", classic.content); Set(so, "_prestigeContent", prestige.content);
        Set(so, "_cardTemplate", cardTemplate);
        so.FindProperty("_slotsPerRow").intValue = 4;

        SerializedProperty tp = so.FindProperty("_charTabs");
        tp.arraySize = tabs.Length;
        for (int i = 0; i < tabs.Length; i++)
        {
            SerializedProperty e = tp.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("button").objectReferenceValue = tabs[i].button;
            e.FindPropertyRelative("fill").objectReferenceValue = tabs[i].fill;
            e.FindPropertyRelative("border").objectReferenceValue = tabs[i].border;
            e.FindPropertyRelative("label").objectReferenceValue = tabs[i].label;
            e.FindPropertyRelative("lockIcon").objectReferenceValue = tabs[i].lockIcon;
        }

        SetPath(so, "_preview.group", pv.group); SetPath(so, "_preview.frame", pv.frame); SetPath(so, "_preview.glow", pv.glow);
        SetPath(so, "_preview.portrait", pv.portrait); SetPath(so, "_preview.portraitImage", pv.portraitImage);
        SetPath(so, "_preview.lockGroup", pv.lockGroup); SetPath(so, "_preview.lockText", pv.lockText);
        SetPath(so, "_preview.tierChip", pv.tierChip); SetPath(so, "_preview.tierText", pv.tierText);
        SetPath(so, "_preview.nameText", pv.nameText); SetPath(so, "_preview.descText", pv.descText);
        SetPath(so, "_preview.actionButton", pv.actionButton); SetPath(so, "_preview.actionBg", pv.actionBg);
        SetPath(so, "_preview.actionGlow", pv.actionGlow); SetPath(so, "_preview.actionLabel", pv.actionLabel);
        SetPath(so, "_preview.actionPriceRoot", pv.priceRoot); SetPath(so, "_preview.actionPriceIcon", pv.priceIcon);
        SetPath(so, "_preview.actionPriceText", pv.priceText);

        Set(so, "_goldIcon", _coin); Set(so, "_eclatIcon", _eclat);
        SetArray(so, "_portraits", new Object[] { _portraits[0], _portraits[1], _portraits[2] });
        Set(so, "_lockedMaterial", _desaturate);
        Set(so, "_toastGroup", toastGroup); Set(so, "_toastText", toastText);
        Set(so, "_debugResetButton", resetBtn);
        so.ApplyModifiedPropertiesWithoutUndo();

        // ordre : fond, voile, ambiance, page
        dim.SetSiblingIndex(1); ambient.SetSiblingIndex(2); page.SetSiblingIndex(3);

        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        Debug.Log("[ReputationPageBuilder] Onglet Réputation reconstruit (scène marquée modifiée : à enregistrer). Catalogue : " + catalog.skins.Count + " skin(s).");
    }

    // ── chargement ────────────────────────────────────────────────────────

    private static void LoadAssets()
    {
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Game/Fonts/Bangers-Regular SDF.asset");
        _labelMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Fonts/Bangers-Regular SDF - Libelle.mat");   // créé par Rebuild Skill Tree Page
        _circle = AssetDatabase.LoadAssetAtPath<Sprite>(Ui + "Personnage/UI_Circle.png");
        _roundRect = AssetDatabase.LoadAssetAtPath<Sprite>(Ui + "Personnage/UI_RoundRect.png");
        _coin = AssetDatabase.LoadAssetAtPath<Sprite>(Ui + "Autres/Gold.png");
        _eclat = AssetDatabase.LoadAssetAtPath<Sprite>(Ui + "Réputation/Eclat.png");
        _padlock = AssetDatabase.LoadAssetAtPath<Sprite>(Ui + "Autres/Cadenas.png");
        _parchemin = AssetDatabase.LoadAssetAtPath<Sprite>(Ui + "Autres/Parchemin-removebg-preview.png");
        _additive = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/UI_Additive.mat");
        _desaturate = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/UI_Desaturate.mat");
        _portraits = new[]
        {
            AssetDatabase.LoadAssetAtPath<Sprite>(Chars + "AetherTR_fade.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>(Chars + "KaelTR_fade.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>(Chars + "LyraTR_fade.png"),
        };
    }

    // Crée (ou complète) le catalogue de skins : la tenue d'origine de chaque personnage, avec son portrait d'aperçu.
    private static SkinCatalog EnsureCatalog()
    {
        const string path = "Assets/Resources/SkinCatalog.asset";
        SkinCatalog cat = AssetDatabase.LoadAssetAtPath<SkinCatalog>(path);
        if (cat == null)
        {
            cat = ScriptableObject.CreateInstance<SkinCatalog>();
            AssetDatabase.CreateAsset(cat, path);
        }
        cat.AddMissingDefaults();
        for (int c = 0; c < SkinCatalog.CharacterCount; c++)
        {
            SkinEntry d = cat.DefaultFor(c);
            if (d != null && d.preview == null) d.preview = _portraits[c];
        }
        EditorUtility.SetDirty(cat);
        AssetDatabase.SaveAssets();
        return cat;
    }

    // ── petites fabriques ─────────────────────────────────────────────────

    private static RectTransform NewRt(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // coin haut-gauche : x vers la droite, y vers le BAS
    private static void TL(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    // coin haut-droit : x = distance au bord droit
    private static void TR(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void Center(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void Stretch(RectTransform rt, float l = 0, float t = 0, float r = 0, float b = 0)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
    }

    private static Image AddImage(RectTransform rt, Sprite sprite, Color color, bool sliced = false, bool raycast = false)
    {
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite; img.color = color;
        img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        img.raycastTarget = raycast;
        return img;
    }

    private static Image AddGlow(RectTransform rt, Color color)
    {
        Image img = AddImage(rt, TreeSprites.Glow, color);
        img.material = _additive;
        return img;
    }

    private static TextMeshProUGUI AddText(RectTransform rt, string text, float size, Color color, TextAlignmentOptions align, bool wrap = false, bool outline = false)
    {
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = _font;
        if (outline && _labelMat != null) t.fontSharedMaterial = _labelMat;
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
        t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.richText = true; t.raycastTarget = false;
        return t;
    }

    private static Navigation NoNav() { Navigation n = new Navigation(); n.mode = Navigation.Mode.None; return n; }

    private static CanvasGroup Block(RectTransform parent, string name, float x, float y, float w, float h)
    {
        RectTransform rt = NewRt(name, parent);
        TL(rt, x, y, w, h);
        return rt.gameObject.AddComponent<CanvasGroup>();
    }

    // Pastille arrondie : contour + fond (les deux sont des enfants "Border" / "Fill").
    private static RectTransform Pill(string name, Transform parent, float x, float y, float w, float h, Color border, Color fill)
    {
        RectTransform rt = NewRt(name, parent);
        TL(rt, x, y, w, h);
        RectTransform b = NewRt("Border", rt); Stretch(b); AddImage(b, _roundRect, border, true);
        RectTransform f = NewRt("Fill", rt); Stretch(f, 2, 2, 2, 2); AddImage(f, _roundRect, fill, true, true);
        return rt;
    }

    // Panneau rectangulaire à filet de 2 px (même langage que la fiche Personnage et la carte des Paramètres).
    private static RectTransform Panel(string name, Transform parent, float x, float y, float w, float h, Color frame, Color fill, out Image frameImg, out Image fillImg)
    {
        RectTransform rt = NewRt(name, parent);
        TL(rt, x, y, w, h);
        RectTransform fr = NewRt("Frame", rt); Stretch(fr);
        frameImg = AddImage(fr, null, frame);
        RectTransform fi = NewRt("Fill", rt); Stretch(fi, 2, 2, 2, 2);
        fillImg = AddImage(fi, null, fill, false, true);
        return rt;
    }

    private static RectTransform CurrencyPill(string name, Transform parent, float rightMargin, float y, Color border, Sprite icon, out TextMeshProUGUI text, Vector2 iconSize)
    {
        RectTransform pill = NewRt(name, parent);
        TR(pill, rightMargin, y, 250, 62);
        RectTransform b = NewRt("Border", pill); Stretch(b); AddImage(b, _roundRect, border, true);
        RectTransform f = NewRt("Fill", pill); Stretch(f, 2, 2, 2, 2); AddImage(f, _roundRect, new Color32(0x12, 0x0F, 0x1B, 0xF2), true);
        RectTransform ic = NewRt("Icon", pill);
        Center(ic, 0, 0, iconSize.x, iconSize.y);
        ic.anchorMin = ic.anchorMax = new Vector2(0f, 0.5f); ic.pivot = new Vector2(0.5f, 0.5f); ic.anchoredPosition = new Vector2(34, 0);
        Image iim = AddImage(ic, icon, Color.white);
        iim.preserveAspect = true;
        RectTransform tx = NewRt("Text", pill); Stretch(tx, 62, 0, 20, 0);
        text = AddText(tx, "0", 42, Color.white, TextAlignmentOptions.MidlineRight);
        return pill;
    }

    private static void SectionHeader(RectTransform parent, float x, float y, float w, string label, Color color, Sprite unused, string note, Sprite noteIcon, Color noteColor)
    {
        RectTransform t = NewRt("Label", parent);
        TL(t, x, y, 700, 36);
        AddText(t, label, 30, color, TextAlignmentOptions.MidlineLeft);
        RectTransform line = NewRt("Line", parent);
        TL(line, x, y + 38, w, 2);
        AddImage(line, null, new Color(color.r, color.g, color.b, 0.45f));
        if (!string.IsNullOrEmpty(note))
        {
            RectTransform n = NewRt("Note", parent);
            TL(n, x + w - 520, y + 2, 490, 32);
            AddText(n, note, 22, Muted, TextAlignmentOptions.MidlineRight);
            if (noteIcon != null)
            {
                RectTransform ni = NewRt("NoteIcon", parent);
                TL(ni, x + w - 22, y + 4, 22, 28);
                AddImage(ni, noteIcon, Color.white).preserveAspect = true;
            }
        }
    }

    // ── parchemin (boutons d'action) ───────────────────────────────────────

    private static Button ParchmentButton(string name, Transform parent, float x, float y, float w, float h, string label, float size, out Image bg, out TextMeshProUGUI text, out Image glow)
    {
        RectTransform rt = NewRt(name, parent);
        TL(rt, x, y, w, h);
        RectTransform g = NewRt("Glow", rt);
        Stretch(g, -26, -22, -26, -22);
        glow = AddGlow(g, new Color(1f, 0.86f, 0.5f, 0f));
        RectTransform b = NewRt("Bg", rt); Stretch(b);
        bg = AddImage(b, _parchemin, Color.white, false, true);
        Button btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = bg;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white; cb.highlightedColor = new Color(1f, 0.93f, 0.78f); cb.pressedColor = new Color(0.78f, 0.74f, 0.66f);
        cb.selectedColor = Color.white; cb.disabledColor = Color.white; cb.colorMultiplier = 1f; cb.fadeDuration = 0.08f;
        btn.colors = cb; btn.navigation = NoNav();
        RectTransform l = NewRt("Label", rt); Stretch(l, 16, 0, 136, 0);
        text = AddText(l, label, size, ReputationStyle.ButtonText, TextAlignmentOptions.Center);
        text.enableAutoSizing = true; text.fontSizeMin = 16; text.fontSizeMax = size;
        return btn;
    }

    // Prix affiché dans un bouton : icône + montant, calé à droite.
    private static RectTransform PriceRow(Transform parent, Sprite icon, float size, Color textColor, out Image iconImage, out TextMeshProUGUI text)
    {
        RectTransform row = NewRt("PriceRow", parent);
        row.anchorMin = row.anchorMax = new Vector2(1f, 0.5f); row.pivot = new Vector2(1f, 0.5f);
        row.anchoredPosition = new Vector2(-22f, 0f); row.sizeDelta = new Vector2(118f, 42f);
        RectTransform ic = NewRt("Icon", row);
        ic.anchorMin = ic.anchorMax = new Vector2(0f, 0.5f); ic.pivot = new Vector2(0f, 0.5f);
        ic.anchoredPosition = Vector2.zero; ic.sizeDelta = new Vector2(30f, 38f);
        iconImage = AddImage(ic, icon, Color.white);
        iconImage.preserveAspect = true;
        RectTransform tx = NewRt("Text", row); Stretch(tx, 34, 0, 0, 0);
        text = AddText(tx, "0", size, textColor, TextAlignmentOptions.MidlineRight);
        return row;
    }

    // ── carte de bonus de Réputation ───────────────────────────────────────

    private static ReputationStatCardUI BuildStatCard(RectTransform parent, float x, float y, string nodeId, Sprite iconSprite)
    {
        Color accent = ReputationStyle.StatAccent(nodeId);
        RectTransform card = NewRt("Card_" + nodeId.Replace("reputation", ""), parent);
        TL(card, x, y, 570, 250);
        Image hit = AddImage(card, null, new Color(0, 0, 0, 0), false, true);     // reçoit le survol
        ReputationStatCardUI comp = card.gameObject.AddComponent<ReputationStatCardUI>();

        RectTransform body = NewRt("Body", card); Stretch(body);

        RectTransform fr = NewRt("Frame", body); Stretch(fr);
        Image frame = AddImage(fr, null, accent);
        RectTransform fi = NewRt("Fill", body); Stretch(fi, 2, 2, 2, 2);
        AddImage(fi, null, new Color(PanelDark.r, PanelDark.g, PanelDark.b, 0.985f));

        // lueur de la stat derrière l'icône
        RectTransform iconArea = NewRt("IconArea", body);
        TL(iconArea, 8, 16, 190, 216);
        RectTransform gl = NewRt("Glow", iconArea);
        Center(gl, 0, 0, 240, 240);
        gl.anchorMin = gl.anchorMax = new Vector2(0.5f, 0.5f);
        Image glow = AddGlow(gl, new Color(accent.r, accent.g, accent.b, 0.34f));
        RectTransform ic = NewRt("Icon", iconArea);
        Center(ic, 0, 0, 170, 200);
        Image iconImg = AddImage(ic, iconSprite, Color.white);
        iconImg.preserveAspect = true;

        RectTransform bar = NewRt("AccentBar", body);
        TL(bar, 0, 0, 570, 5);
        Image accentBar = AddImage(bar, null, accent);

        RectTransform name = NewRt("Name", body);
        TL(name, 208, 12, 350, 44);
        TextMeshProUGUI nameT = AddText(name, ReputationStyle.StatName(nodeId), 40, Color.white, TextAlignmentOptions.MidlineLeft);

        RectTransform value = NewRt("Value", body);
        TL(value, 208, 52, 350, 62);
        TextMeshProUGUI valueT = AddText(value, "+0%", 64, accent, TextAlignmentOptions.MidlineLeft);

        RectTransform level = NewRt("Level", body);
        TL(level, 208, 112, 350, 22);
        TextMeshProUGUI levelT = AddText(level, "NIVEAU 0 / 5", 20, Muted, TextAlignmentOptions.MidlineLeft);

        Image[] pips = new Image[5];
        for (int i = 0; i < 5; i++)
        {
            RectTransform p = NewRt("Pip" + (i + 1), body);
            TL(p, 208 + i * 70, 138, 62, 12);
            pips[i] = AddImage(p, _roundRect, ReputationStyle.EmptyPip, true);
        }

        RectTransform next = NewRt("Next", body);
        TL(next, 208, 154, 350, 28);
        TextMeshProUGUI nextT = AddText(next, "PROCHAIN NIVEAU", 24, Body, TextAlignmentOptions.MidlineLeft);

        Button buy = ParchmentButton("BuyButton", body, 208, 188, 350, 50, "AMÉLIORER", 30, out Image buyBg, out TextMeshProUGUI buyLabel, out Image buyGlow);
        RectTransform priceRow = PriceRow(buy.transform, _eclat, 30, ReputationStyle.ButtonText, out Image _, out TextMeshProUGUI costText);

        RectTransform fl = NewRt("Flash", body);
        Stretch(fl);
        Image flash = AddImage(fl, null, new Color(1, 1, 1, 0));
        flash.material = _additive;

        SerializedObject so = new SerializedObject(comp);
        so.FindProperty("_nodeId").stringValue = nodeId;
        Set(so, "_body", body); Set(so, "_frame", frame); Set(so, "_accentBar", accentBar); Set(so, "_glow", glow); Set(so, "_flash", flash);
        Set(so, "_icon", ic);
        Set(so, "_name", nameT); Set(so, "_value", valueT); Set(so, "_level", levelT); Set(so, "_next", nextT);
        SetArray(so, "_pips", pips);
        Set(so, "_costRow", priceRow); Set(so, "_costText", costText);
        Set(so, "_buy", buy); Set(so, "_buyImage", buyBg); Set(so, "_buyLabel", buyLabel); Set(so, "_buyGlow", buyGlow);
        so.ApplyModifiedPropertiesWithoutUndo();
        return comp;
    }

    // ── onglets de personnage ──────────────────────────────────────────────

    private static ReputationUI.CharTab BuildCharTab(RectTransform parent, float x, float y, string label, int index)
    {
        Color accent = ReputationStyle.CharacterAccent(index);
        RectTransform pill = Pill("Tab_" + label, parent, x, y + 2, 200, 46, accent, Field);
        Button btn = pill.gameObject.AddComponent<Button>();
        Image fill = pill.Find("Fill").GetComponent<Image>();
        btn.targetGraphic = fill;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white; cb.highlightedColor = new Color(1.25f, 1.25f, 1.3f); cb.pressedColor = new Color(0.8f, 0.8f, 0.85f);
        cb.selectedColor = Color.white; cb.disabledColor = Color.white; cb.fadeDuration = 0.08f;
        btn.colors = cb; btn.navigation = NoNav();

        RectTransform tx = NewRt("Text", pill); Stretch(tx, 10, 0, 10, 0);
        TextMeshProUGUI t = AddText(tx, label, 28, Muted, TextAlignmentOptions.Center);

        RectTransform lk = NewRt("Lock", pill);
        lk.anchorMin = lk.anchorMax = new Vector2(0f, 0.5f); lk.pivot = new Vector2(0.5f, 0.5f);
        lk.anchoredPosition = new Vector2(24f, 0f); lk.sizeDelta = new Vector2(20f, 26f);
        Image lockImg = AddImage(lk, _padlock, new Color(1f, 1f, 1f, 0.85f));
        lockImg.preserveAspect = true;

        return new ReputationUI.CharTab { button = btn, fill = fill, border = pill.Find("Border").GetComponent<Image>(), label = t, lockIcon = lockImg };
    }

    // ── aperçu du skin ─────────────────────────────────────────────────────

    private class PreviewRefs
    {
        public CanvasGroup group; public Image frame, glow, portraitImage, tierChip, actionBg, actionGlow, priceIcon;
        public RectTransform portrait; public GameObject lockGroup, priceRoot;
        public TextMeshProUGUI lockText, tierText, nameText, descText, actionLabel, priceText;
        public Button actionButton;
    }

    private static PreviewRefs BuildPreview(RectTransform parent, float x, float y)
    {
        PreviewRefs r = new PreviewRefs();
        RectTransform panel = Panel("Preview", parent, x, y, 420, 436, ReputationStyle.CharacterAccent(0), new Color(PanelDark.r, PanelDark.g, PanelDark.b, 0.95f), out r.frame, out Image _);
        r.group = panel.gameObject.AddComponent<CanvasGroup>();

        // halo du personnage, derrière le portrait
        RectTransform gl = NewRt("Glow", panel);
        Center(gl, 0, 88, 470, 470);
        r.glow = AddGlow(gl, new Color(1f, 1f, 1f, 0.3f));

        // portrait : masque (haut du personnage) — le sprite est posé en haut par ReputationStyle.FitPortrait
        RectTransform mask = NewRt("PortraitMask", panel);
        TL(mask, 4, 4, 412, 258);
        mask.gameObject.AddComponent<RectMask2D>();
        RectTransform por = NewRt("Portrait", mask);
        r.portrait = por;
        r.portraitImage = AddImage(por, _portraits[0], Color.white);

        // verrou par-dessus le portrait
        RectTransform lockGroup = NewRt("LockGroup", panel);
        TL(lockGroup, 4, 4, 412, 258);
        r.lockGroup = lockGroup.gameObject;
        RectTransform lveil = NewRt("Veil", lockGroup); Stretch(lveil); AddImage(lveil, null, new Color(0.03f, 0.02f, 0.05f, 0.55f));
        RectTransform lic = NewRt("Padlock", lockGroup); Center(lic, 0, 54, 54, 72);
        AddImage(lic, _padlock, Color.white).preserveAspect = true;
        RectTransform ltx = NewRt("Text", lockGroup); Center(ltx, 0, -32, 380, 90);
        r.lockText = AddText(ltx, "", 28, Color.white, TextAlignmentOptions.Center, true);
        lockGroup.gameObject.SetActive(false);

        // gamme
        RectTransform chip = NewRt("TierChip", panel);
        TL(chip, 14, 14, 214, 30);
        r.tierChip = AddImage(chip, _roundRect, new Color(1, 1, 1, 0.2f), true);
        RectTransform ct = NewRt("Text", chip); Stretch(ct);
        r.tierText = AddText(ct, "CLASSIQUE  ·  OR", 20, ReputationStyle.Gold, TextAlignmentOptions.Center);

        RectTransform nm = NewRt("Name", panel);
        TL(nm, 20, 266, 380, 44);
        r.nameText = AddText(nm, "TENUE D'ORIGINE", 42, Color.white, TextAlignmentOptions.MidlineLeft, false, true);

        RectTransform ds = NewRt("Desc", panel);
        TL(ds, 20, 312, 380, 50);
        r.descText = AddText(ds, "", 22, Muted, TextAlignmentOptions.TopLeft, true);
        r.descText.enableAutoSizing = true; r.descText.fontSizeMin = 16; r.descText.fontSizeMax = 22;

        Button act = ParchmentButton("ActionButton", panel, 24, 368, 372, 56, "ACHETER", 32, out r.actionBg, out r.actionLabel, out r.actionGlow);
        r.actionButton = act;
        RectTransform pr = PriceRow(act.transform, _coin, 32, ReputationStyle.ButtonText, out r.priceIcon, out r.priceText);
        r.priceRoot = pr.gameObject;
        return r;
    }

    // ── rangées de skins ───────────────────────────────────────────────────

    private class RowRefs { public RectTransform content; }

    private static RowRefs BuildRow(RectTransform parent, float x, float y, string id, string label, Color color, Sprite icon, string note)
    {
        RectTransform t = NewRt(id + "Label", parent);
        TL(t, x, y, 500, 30);
        AddText(t, label, 26, color, TextAlignmentOptions.MidlineLeft);
        RectTransform ic = NewRt(id + "Icon", parent);
        TL(ic, x + 268, y + 3, 24, 24);
        AddImage(ic, icon, Color.white).preserveAspect = true;
        RectTransform n = NewRt(id + "Note", parent);
        TL(n, x + 300, y, 480, 30);
        AddText(n, note, 22, Muted, TextAlignmentOptions.MidlineLeft);
        RectTransform line = NewRt(id + "Line", parent);
        TL(line, x, y + 31, 1350, 1);
        AddImage(line, null, new Color(color.r, color.g, color.b, 0.3f));

        RectTransform scrollRt = NewRt(id + "Row", parent);
        TL(scrollRt, x, y + 32, 1350, 184);
        ScrollRect scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = true; scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        scrollRt.gameObject.AddComponent<WheelToHorizontal>();
        AddImage(scrollRt, null, new Color(0, 0, 0, 0), false, true);      // la molette et le glisser marchent aussi entre deux cartes

        RectTransform vp = NewRt("Viewport", scrollRt); Stretch(vp);
        vp.gameObject.AddComponent<RectMask2D>();
        RectTransform content = NewRt("Content", vp);
        content.anchorMin = new Vector2(0f, 0f); content.anchorMax = new Vector2(0f, 1f); content.pivot = new Vector2(0f, 0.5f);
        content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
        HorizontalLayoutGroup hl = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 22; hl.padding = new RectOffset(8, 8, 8, 8);
        hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
        hl.childAlignment = TextAnchor.UpperLeft;
        content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = vp; scroll.content = content;
        return new RowRefs { content = content };
    }

    // ── modèle de carte de skin ────────────────────────────────────────────

    private static SkinCardUI BuildSkinCardTemplate(RectTransform parent)
    {
        RectTransform card = NewRt("SkinCardTemplate", parent);
        card.sizeDelta = new Vector2(316f, 168f);
        AddImage(card, null, new Color(0, 0, 0, 0), false, true);
        LayoutElement le = card.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 316; le.preferredHeight = 168; le.minWidth = 316; le.minHeight = 168;
        SkinCardUI comp = card.gameObject.AddComponent<SkinCardUI>();

        RectTransform body = NewRt("Body", card); Stretch(body);

        RectTransform sg = NewRt("SelectGlow", body); Stretch(sg, -28, -24, -28, -24);
        Image selectGlow = AddGlow(sg, new Color(1, 1, 1, 0));

        RectTransform fr = NewRt("Frame", body); Stretch(fr);
        Image frame = AddImage(fr, null, ReputationStyle.Gold);
        RectTransform fi = NewRt("Fill", body); Stretch(fi, 2, 2, 2, 2);
        Image fill = AddImage(fi, null, new Color(0.09f, 0.075f, 0.13f, 0.98f));

        // buste : la tête ENTIÈRE et le haut du corps à gauche (fondu vers le fond de la carte), sur un halo de la gamme
        RectTransform tg = NewRt("ThumbGlow", body);
        TL(tg, 0, 0, 168, 168);
        Image thumbGlow = AddGlow(tg, new Color(1, 1, 1, 0.26f));
        RectTransform mask = NewRt("ThumbMask", body);
        TL(mask, 6, 6, 150, 156);
        mask.gameObject.AddComponent<RectMask2D>();
        RectTransform th = NewRt("Thumb", mask);
        Image thumb = AddImage(th, _portraits[0], Color.white);
        RectTransform fd = NewRt("Fade", mask);
        fd.anchorMin = new Vector2(0f, 0f); fd.anchorMax = new Vector2(1f, 0f); fd.pivot = new Vector2(0.5f, 0f);
        fd.anchoredPosition = Vector2.zero; fd.sizeDelta = new Vector2(0f, 74f);
        Image fade = AddImage(fd, null, new Color(0.09f, 0.075f, 0.13f, 1f));
        RectTransform sh = NewRt("Shade", mask); Stretch(sh);
        Image shade = AddImage(sh, null, new Color(0, 0, 0, 0));

        // nom : jusqu'à 2 lignes à droite du buste
        RectTransform name = NewRt("Name", body);
        TL(name, 164, 16, 146, 64);
        TextMeshProUGUI nameT = AddText(name, "SKIN", 28, Color.white, TextAlignmentOptions.TopLeft, true);
        nameT.enableAutoSizing = true; nameT.fontSizeMin = 18; nameT.fontSizeMax = 28;
        nameT.overflowMode = TextOverflowModes.Truncate;

        // prix (pas encore acheté)
        RectTransform price = NewRt("Price", body);
        TL(price, 164, 128, 146, 30);
        RectTransform pic = NewRt("Icon", price);
        pic.anchorMin = pic.anchorMax = new Vector2(0f, 0.5f); pic.pivot = new Vector2(0f, 0.5f);
        pic.anchoredPosition = Vector2.zero; pic.sizeDelta = new Vector2(22f, 28f);
        Image picImg = AddImage(pic, _coin, Color.white); picImg.preserveAspect = true;
        RectTransform ptx = NewRt("Text", price); Stretch(ptx, 28, 0, 0, 0);
        TextMeshProUGUI priceT = AddText(ptx, "0", 28, Color.white, TextAlignmentOptions.MidlineLeft);

        // état (possédé / équipé)
        RectTransform chip = NewRt("Chip", body);
        TL(chip, 164, 130, 112, 26);
        Image chipImg = AddImage(chip, _roundRect, new Color(1, 1, 1, 0.14f), true);
        RectTransform ctx = NewRt("Text", chip); Stretch(ctx);
        TextMeshProUGUI chipT = AddText(ctx, "POSSÉDÉ", 18, Body, TextAlignmentOptions.Center);

        // emplacement à venir
        RectTransform ph = NewRt("Placeholder", body);
        Stretch(ph, 2, 2, 2, 2);
        RectTransform q = NewRt("Question", ph);
        Center(q, 0, 14, 120, 110);
        AddText(q, "?", 100, new Color(1f, 1f, 1f, 0.09f), TextAlignmentOptions.Center);
        RectTransform pt = NewRt("Text", ph);
        Center(pt, 0, -52, 280, 34);
        TextMeshProUGUI phT = AddText(pt, "BIENTÔT", 26, ReputationStyle.Gold, TextAlignmentOptions.Center);
        phT.characterSpacing = 6f;
        ph.gameObject.SetActive(false);

        SerializedObject so = new SerializedObject(comp);
        Set(so, "_body", body); Set(so, "_frame", frame); Set(so, "_fill", fill); Set(so, "_selectGlow", selectGlow);
        Set(so, "_thumbMask", mask); Set(so, "_thumb", thumb); Set(so, "_thumbGlow", thumbGlow); Set(so, "_thumbFade", fade); Set(so, "_thumbShade", shade);
        Set(so, "_name", nameT);
        Set(so, "_priceRoot", price.gameObject); Set(so, "_priceIcon", picImg); Set(so, "_priceText", priceT);
        Set(so, "_chip", chipImg); Set(so, "_chipText", chipT);
        Set(so, "_placeholderRoot", ph.gameObject); Set(so, "_placeholderText", phT);
        so.ApplyModifiedPropertiesWithoutUndo();
        return comp;
    }

    // ── sérialisation ──────────────────────────────────────────────────────

    private static void Set(SerializedObject so, string prop, Object value)
    {
        SerializedProperty p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning("[ReputationPageBuilder] propriété introuvable : " + prop); return; }
        p.objectReferenceValue = value;
    }

    private static void SetPath(SerializedObject so, string path, Object value) => Set(so, path, value);

    private static void SetArray<T>(SerializedObject so, string prop, T[] items) where T : Object
    {
        SerializedProperty p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning("[ReputationPageBuilder] propriété introuvable : " + prop); return; }
        p.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
}
