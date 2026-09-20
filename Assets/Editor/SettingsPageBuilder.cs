using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// OUTIL D'ÉDITEUR (2026-09-19) - (re)construit la page Paramètres dans la scène MainMenu (menu :
// Aether > Rebuild Settings Page). Idempotent : supprime ce qu'il avait construit et le refait à neuf ; le fond
// d'origine (SettingsBackground) est conservé. La hiérarchie créée reste modifiable à la main ; les composants
// (SettingsPage, SettingsRow, SettingsAmbientFX) ne construisent RIEN à l'exécution, sauf les lignes de la
// catégorie affichée (copies des modèles de "Templates").
public static class SettingsPageBuilder
{
    private const string Ui = "Assets/Game/Assets 2D/UI/";

    private static TMP_FontAsset _font;
    private static Sprite _circle, _roundRect, _arrowL, _arrowR;
    private static Material _additive, _labelMat;

    private static readonly Color PanelDark = SettingsStyle.PanelDark;
    private static readonly Color Field = SettingsStyle.Field;
    private static readonly Color Accent = SettingsStyle.Accent;
    private static readonly Color AccentSoft = SettingsStyle.AccentSoft;
    private static readonly Color Muted = SettingsStyle.Muted;
    private static readonly Color Body = SettingsStyle.Body;

    // Bouton "sombre" : l'image est plus CLAIRE que sa teinte normale (0,62), le survol la ramène à 1 => il s'éclaircit.
    private static readonly Color BtnBase = new Color32(0x2C, 0x25, 0x40, 0xFF);

    [MenuItem("Aether/Rebuild Settings Page")]
    public static void Build()
    {
        LoadAssets();

        SettingsPage existingPage = Object.FindFirstObjectByType<SettingsPage>(FindObjectsInactive.Include);
        RectTransform root = existingPage != null ? (RectTransform)existingPage.transform : null;
        if (root == null)
        {
            foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t.name == "SettingsPanel" && t.gameObject.scene.IsValid()) { root = (RectTransform)t; break; }
        }
        if (root == null)
        {
            Debug.LogError("[SettingsPageBuilder] SettingsPanel introuvable (ni SettingsPage, ni objet nommé SettingsPanel).");
            return;
        }

        // 1) on retire l'éventuelle version précédente et tout sauf le fond d'origine
        SettingsPage prev = root.GetComponent<SettingsPage>();
        if (prev != null) Object.DestroyImmediate(prev);
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform c = root.GetChild(i);
            if (c.name == "SettingsBackground") continue;
            Object.DestroyImmediate(c.gameObject);
        }
        RectTransform bg = (RectTransform)root.Find("SettingsBackground");

        // 2) fond : assombri pour que la carte et le texte ressortent, lueurs vivantes par-dessus
        RectTransform dim = NewRt("Dim", root);
        Stretch(dim);
        AddImage(dim, null, new Color(0.02f, 0.015f, 0.04f, 0.42f));

        RectTransform ambient = NewRt("Ambient", root);
        Stretch(ambient);
        SettingsAmbientFX fx = ambient.gameObject.AddComponent<SettingsAmbientFX>();
        new SerializedObject(fx).FindProperty("_additiveMaterial").objectReferenceValue = _additive;
        new SerializedObject(fx).ApplyModifiedPropertiesWithoutUndo();
        SerializedObject fxso = new SerializedObject(fx);
        fxso.FindProperty("_additiveMaterial").objectReferenceValue = _additive;
        fxso.ApplyModifiedPropertiesWithoutUndo();

        // 3) en-tête
        RectTransform title = NewRt("Title", root);
        Center(title, 0, 424, 1000, 84);
        TextMeshProUGUI titleT = AddText(title, "AFFICHAGE", 78, Accent, TextAlignmentOptions.Center, false, true);

        RectTransform subtitle = NewRt("Subtitle", root);
        Center(subtitle, 0, 368, 1000, 34);
        TextMeshProUGUI subT = AddText(subtitle, "", 30, Muted, TextAlignmentOptions.Center);

        RectTransform tabs = NewRt("Tabs", root);
        Center(tabs, 0, 318, 1180, 46);
        HorizontalLayoutGroup hl = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 10; hl.childAlignment = TextAnchor.MiddleCenter;
        hl.childControlWidth = false; hl.childControlHeight = false; hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;

        Button tabTpl = MakeButton("TabTemplate", root, BtnBase, 176, 44, "TAB", 25, Color.white, out TextMeshProUGUI _);
        Center((RectTransform)tabTpl.transform, 0, 0, 176, 44);
        tabTpl.gameObject.SetActive(false);
        tabTpl.transform.SetParent(tabs, false);   // reste dans la barre, désactivé : SettingsPage en fait des copies


        // 4) carte
        RectTransform card = NewRt("Card", root);
        Center(card, 0, -55, 1240, 650);
        RectTransform cardBg = NewRt("Bg", card); Stretch(cardBg);
        AddImage(cardBg, null, new Color(PanelDark.r, PanelDark.g, PanelDark.b, 0.965f), false, true);   // capte les clics : rien ne passe à travers
        string[] sides = { "Top", "Bottom", "Left", "Right" };
        for (int i = 0; i < 4; i++)
        {
            RectTransform b = NewRt("Border" + sides[i], card);
            switch (i)
            {
                case 0: b.anchorMin = new Vector2(0, 1); b.anchorMax = new Vector2(1, 1); b.pivot = new Vector2(0.5f, 1); b.sizeDelta = new Vector2(0, 2); break;
                case 1: b.anchorMin = new Vector2(0, 0); b.anchorMax = new Vector2(1, 0); b.pivot = new Vector2(0.5f, 0); b.sizeDelta = new Vector2(0, 2); break;
                case 2: b.anchorMin = new Vector2(0, 0); b.anchorMax = new Vector2(0, 1); b.pivot = new Vector2(0, 0.5f); b.sizeDelta = new Vector2(2, 0); break;
                default: b.anchorMin = new Vector2(1, 0); b.anchorMax = new Vector2(1, 1); b.pivot = new Vector2(1, 0.5f); b.sizeDelta = new Vector2(2, 0); break;
            }
            b.anchoredPosition = Vector2.zero;
            AddImage(b, null, Accent);
        }
        RectTransform bar = NewRt("AccentBar", card);
        bar.anchorMin = new Vector2(0, 1); bar.anchorMax = new Vector2(1, 1); bar.pivot = new Vector2(0.5f, 1);
        bar.sizeDelta = new Vector2(0, 5); bar.anchoredPosition = Vector2.zero;
        AddImage(bar, null, Accent);

        // liste défilante
        RectTransform scrollRt = NewRt("Scroll", card);
        Stretch(scrollRt, 26, 20, 26, 14);
        ScrollRect scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 46f;

        RectTransform viewport = NewRt("Viewport", scrollRt);
        Stretch(viewport, 0, 0, 16, 0);
        viewport.gameObject.AddComponent<RectMask2D>();
        AddImage(viewport, null, new Color(0, 0, 0, 0), false, true);     // la roulette fonctionne aussi entre deux lignes

        RectTransform content = NewRt("Content", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero; content.sizeDelta = new Vector2(0, 0);
        VerticalLayoutGroup vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 2; vl.padding = new RectOffset(0, 0, 4, 10);
        vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        CanvasGroup contentGroup = content.gameObject.AddComponent<CanvasGroup>();

        // barre de défilement fine
        RectTransform sb = NewRt("Scrollbar", scrollRt);
        sb.anchorMin = new Vector2(1, 0); sb.anchorMax = new Vector2(1, 1); sb.pivot = new Vector2(1, 0.5f);
        sb.sizeDelta = new Vector2(8, 0); sb.anchoredPosition = Vector2.zero;
        AddImage(sb, _roundRect, new Color(1, 1, 1, 0.06f), true);
        Scrollbar scrollbar = sb.gameObject.AddComponent<Scrollbar>();
        RectTransform slide = NewRt("Sliding Area", sb); Stretch(slide);
        RectTransform handle = NewRt("Handle", slide); Stretch(handle);
        Image handleImg = AddImage(handle, _roundRect, AccentSoft, true, true);
        scrollbar.handleRect = handle; scrollbar.targetGraphic = handleImg;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.navigation = NoNav();
        scroll.viewport = viewport; scroll.content = content;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;     // affichée ou non par SettingsPage
        scroll.verticalScrollbarSpacing = -4;

        // 5) pied de page
        Button resetPage = MakeButton("ResetPageButton", root, BtnBase, 310, 50, "Réinitialiser cette page", 28, Body, out TextMeshProUGUI _, AccentSoft);
        Center((RectTransform)resetPage.transform, -458, -432, 310, 50);
        Button resetAll = MakeButton("ResetAllButton", root, BtnBase, 310, 50, "Tout réinitialiser", 28, Body, out TextMeshProUGUI _, AccentSoft);
        Center((RectTransform)resetAll.transform, -136, -432, 310, 50);
        Button quit = MakeButton("QuitButton", root, new Color32(0x4A, 0x22, 0x26, 0xFF), 310, 50, "Quitter le jeu", 28, new Color32(0xFF, 0xB4, 0xAA, 0xFF), out TextMeshProUGUI _, SettingsStyle.Warn);
        Center((RectTransform)quit.transform, 458, -432, 310, 50);

        // 6) modèles de lignes (désactivés)
        RectTransform templates = NewRt("Templates", root);
        Stretch(templates);
        templates.gameObject.SetActive(false);
        SettingsRow rowTpl = BuildRowTemplate(templates);
        RectTransform headerTpl = BuildHeaderTemplate(templates);
        RectTransform noteTpl = BuildNoteTemplate(templates);

        // 6 bis) liste déroulante (vit à la racine : la zone de défilement des lignes la couperait)
        SettingsDropdown dropdown = BuildDropdown(root);

        // 7) message court
        RectTransform toast = NewRt("Toast", root);
        Center(toast, 0, 366, 1180, 46);
        CanvasGroup toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f; toastGroup.blocksRaycasts = false; toastGroup.interactable = false;
        RectTransform toastB = NewRt("Border", toast); Stretch(toastB);
        AddImage(toastB, _roundRect, Accent, true);
        RectTransform toastF = NewRt("Fill", toast); Stretch(toastF, 2, 2, 2, 2);
        AddImage(toastF, _roundRect, new Color(PanelDark.r, PanelDark.g, PanelDark.b, 0.97f), true);
        RectTransform toastTx = NewRt("Text", toast); Stretch(toastTx, 16, 0, 16, 0);
        TextMeshProUGUI toastT = AddText(toastTx, "", 28, Color.white, TextAlignmentOptions.Center);
        toastT.enableAutoSizing = true; toastT.fontSizeMin = 18; toastT.fontSizeMax = 28;

        // 8) fenêtres
        BuildCaptureDialog(root, out GameObject captureRoot, out TextMeshProUGUI captureTitle, out TextMeshProUGUI captureHint);
        BuildConfirmDialog(root, out GameObject confirmRoot, out TextMeshProUGUI confirmTitle, out TextMeshProUGUI confirmBody,
            out Button confirmYes, out TextMeshProUGUI confirmYesText, out Button confirmNo);
        BuildRevertDialog(root, out GameObject revertRoot, out TextMeshProUGUI revertText, out Button revertKeep, out Button revertUndo);

        // 9) composant + câblage
        SettingsPage page = root.gameObject.AddComponent<SettingsPage>();
        SerializedObject so = new SerializedObject(page);
        Set(so, "_title", titleT); Set(so, "_subtitle", subT);
        Set(so, "_tabsRoot", tabs); Set(so, "_tabTemplate", tabTpl);
        Set(so, "_scroll", scroll); Set(so, "_content", content); Set(so, "_contentGroup", contentGroup);
        Set(so, "_rowTemplate", rowTpl); Set(so, "_headerTemplate", headerTpl); Set(so, "_noteTemplate", noteTpl);
        Set(so, "_resetPageButton", resetPage); Set(so, "_resetAllButton", resetAll); Set(so, "_quitButton", quit);
        Set(so, "_dropdown", dropdown);
        Set(so, "_toastGroup", toastGroup); Set(so, "_toastText", toastT);
        Set(so, "_captureRoot", captureRoot); Set(so, "_captureTitle", captureTitle); Set(so, "_captureHint", captureHint);
        Set(so, "_confirmRoot", confirmRoot); Set(so, "_confirmTitle", confirmTitle); Set(so, "_confirmBody", confirmBody);
        Set(so, "_confirmYes", confirmYes); Set(so, "_confirmYesText", confirmYesText); Set(so, "_confirmNo", confirmNo);
        Set(so, "_revertRoot", revertRoot); Set(so, "_revertText", revertText); Set(so, "_revertKeep", revertKeep); Set(so, "_revertUndo", revertUndo);
        so.ApplyModifiedPropertiesWithoutUndo();

        // ordre : fond, voile, ambiance, en-tête, carte, pied, modèles, message, fenêtres
        bg.SetAsFirstSibling();
        captureRoot.transform.SetAsLastSibling();
        confirmRoot.transform.SetAsLastSibling();
        revertRoot.transform.SetAsLastSibling();

        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        Debug.Log("[SettingsPageBuilder] Page Paramètres reconstruite (scène marquée modifiée : à enregistrer).");
    }

    // ── chargement ─────────────────────────────────────────────────────────

    private static void LoadAssets()
    {
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Game/Fonts/Bangers-Regular SDF.asset");
        _circle = AssetDatabase.LoadAssetAtPath<Sprite>(Ui + "Personnage/UI_Circle.png");
        _roundRect = AssetDatabase.LoadAssetAtPath<Sprite>(Ui + "Personnage/UI_RoundRect.png");
        _arrowL = AssetDatabase.LoadAssetAtPath<Sprite>(Ui + "Autres/fleche_gauche_finale.png");
        _arrowR = AssetDatabase.LoadAssetAtPath<Sprite>(Ui + "Autres/fleche_droite_finale.png");
        _additive = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/UI_Additive.mat");
        _labelMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Fonts/Bangers-Regular SDF - Libelle.mat");   // créé par Rebuild Skill Tree Page
    }

    // ── petites fabriques ──────────────────────────────────────────────────

    private static RectTransform NewRt(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Center(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void Anchored(RectTransform rt, Vector2 anchor, Vector2 pivot, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot;
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

    private static TextMeshProUGUI AddText(RectTransform rt, string text, float size, Color color, TextAlignmentOptions align, bool wrap = false, bool outline = false)
    {
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = _font;
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
        t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.richText = true; t.raycastTarget = false;
        if (outline && _labelMat != null) t.fontSharedMaterial = _labelMat;
        return t;
    }

    private static Navigation NoNav() { Navigation n = new Navigation(); n.mode = Navigation.Mode.None; return n; }

    // Bouton sombre "pilule" : contour (couleur `border`) + fond ; le survol l'éclaircit (voir BtnBase).
    private static Button MakeButton(string name, Transform parent, Color fill, float w, float h, string label, float fontSize, Color textColor,
                                     out TextMeshProUGUI text, Color? border = null)
    {
        RectTransform rt = NewRt(name, parent);
        rt.sizeDelta = new Vector2(w, h);
        Color b = border ?? AccentSoft;
        RectTransform ring = NewRt("Border", rt); Stretch(ring);
        AddImage(ring, _roundRect, b, true);
        RectTransform fillRt = NewRt("Fill", rt); Stretch(fillRt, 2, 2, 2, 2);
        Image fillImg = AddImage(fillRt, _roundRect, fill, true, true);
        Button btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = fillImg;
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.64f, 0.64f, 0.66f, 1f);
        cb.highlightedColor = Color.white;
        cb.pressedColor = new Color(0.45f, 0.45f, 0.5f, 1f);
        cb.selectedColor = new Color(0.64f, 0.64f, 0.66f, 1f);
        cb.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        cb.colorMultiplier = 1f; cb.fadeDuration = 0.08f;
        btn.colors = cb;
        btn.navigation = NoNav();
        RectTransform tx = NewRt("Text", rt); Stretch(tx, 8, 0, 8, 0);
        text = AddText(tx, label, fontSize, textColor, TextAlignmentOptions.Center);
        text.enableAutoSizing = true; text.fontSizeMin = 16; text.fontSizeMax = fontSize;
        return btn;
    }

    private static Button MakeArrow(string name, Transform parent, Sprite sprite, float x, float y)
    {
        RectTransform rt = NewRt(name, parent);
        Center(rt, x, y, 92, 110);
        Image img = AddImage(rt, sprite, Color.white, false, true);
        img.preserveAspect = true;
        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        ColorBlock cb = b.colors;
        cb.normalColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        cb.highlightedColor = Color.white;
        cb.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        cb.selectedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        cb.colorMultiplier = 1f; cb.fadeDuration = 0.08f;
        b.colors = cb;
        b.navigation = NoNav();
        return b;
    }

    private static void Set(SerializedObject so, string prop, Object value)
    {
        SerializedProperty p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning("[SettingsPageBuilder] propriété introuvable : " + prop); return; }
        p.objectReferenceValue = value;
    }

    private static void SetArray<T>(SerializedObject so, string prop, T[] items) where T : Object
    {
        SerializedProperty p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning("[SettingsPageBuilder] propriété introuvable : " + prop); return; }
        p.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    private static SettingsDropdown BuildDropdown(Transform parent)
    {
        RectTransform root = NewRt("Dropdown", parent);
        Stretch(root);
        SettingsDropdown dd = root.gameObject.AddComponent<SettingsDropdown>();

        RectTransform panel = NewRt("Panel", root);
        Center(panel, 0, 0, 410, 300);
        RectTransform ring = NewRt("Border", panel); Stretch(ring);
        AddImage(ring, _roundRect, Accent, true);
        RectTransform bg = NewRt("Bg", panel); Stretch(bg, 2, 2, 2, 2);
        AddImage(bg, _roundRect, new Color32(0x0E, 0x0B, 0x16, 0xFF), true, true);      // capte les clics et la molette

        RectTransform scrollRt = NewRt("Scroll", panel);
        Stretch(scrollRt, 8, 8, 8, 8);
        ScrollRect scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 38f;
        RectTransform viewport = NewRt("Viewport", scrollRt);
        Stretch(viewport, 0, 0, 0, 0);
        viewport.gameObject.AddComponent<RectMask2D>();
        AddImage(viewport, null, new Color(0, 0, 0, 0), false, true);
        RectTransform content = NewRt("Content", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero; content.sizeDelta = new Vector2(0, 100);
        VerticalLayoutGroup vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 3; vl.childControlWidth = true; vl.childControlHeight = false; vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
        scroll.viewport = viewport; scroll.content = content;

        // modèle d'une valeur : liseré (valeur courante) + fond + texte
        RectTransform item = NewRt("ItemTemplate", root);
        item.sizeDelta = new Vector2(0, 50);
        RectTransform iBorder = NewRt("Border", item); Stretch(iBorder);
        AddImage(iBorder, _roundRect, Accent, true);
        RectTransform iFill = NewRt("Fill", item); Stretch(iFill, 2, 2, 2, 2);
        Image fillImg = AddImage(iFill, _roundRect, new Color32(0x1A, 0x15, 0x27, 0xFF), true, true);
        Button btn = item.gameObject.AddComponent<Button>();
        btn.targetGraphic = fillImg;
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.78f, 0.78f, 0.8f, 1f);
        cb.highlightedColor = new Color(1.35f, 1.35f, 1.4f, 1f);
        cb.pressedColor = new Color(0.6f, 0.6f, 0.65f, 1f);
        cb.selectedColor = new Color(0.78f, 0.78f, 0.8f, 1f);
        cb.colorMultiplier = 1f; cb.fadeDuration = 0.06f;
        btn.colors = cb;
        btn.navigation = NoNav();
        RectTransform iText = NewRt("Text", item); Stretch(iText, 16, 0, 16, 0);
        TextMeshProUGUI tx = AddText(iText, "Valeur", 30, Body, TextAlignmentOptions.Center);
        tx.enableAutoSizing = true; tx.fontSizeMin = 18; tx.fontSizeMax = 30;
        LayoutElement le = item.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 50;
        item.gameObject.SetActive(false);
        item.SetParent(root, false);

        SerializedObject so = new SerializedObject(dd);
        Set(so, "_panel", panel); Set(so, "_content", content); Set(so, "_scroll", scroll); Set(so, "_itemTemplate", btn);
        so.ApplyModifiedPropertiesWithoutUndo();

        root.gameObject.SetActive(false);
        return dd;
    }

    // ── lignes ─────────────────────────────────────────────────────────────

    private const float ControlRight = -112f;   // les commandes s'arrêtent 112 px avant le bord droit (place du bouton DÉFAUT)

    private static SettingsRow BuildRowTemplate(Transform parent)
    {
        RectTransform row = NewRt("Row", parent);
        row.sizeDelta = new Vector2(0, 88);
        LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 88; le.flexibleWidth = 1;
        Image highlight = AddImage(row, _roundRect, new Color(1, 1, 1, 0), true, true);
        CanvasGroup group = row.gameObject.AddComponent<CanvasGroup>();

        RectTransform label = NewRt("Label", row);
        Anchored(label, new Vector2(0, 1), new Vector2(0, 1), 12, -8, 640, 40);
        TextMeshProUGUI labelT = AddText(label, "Libellé", 34, Color.white, TextAlignmentOptions.TopLeft);
        labelT.enableAutoSizing = true; labelT.fontSizeMin = 24; labelT.fontSizeMax = 34;

        RectTransform desc = NewRt("Description", row);
        Anchored(desc, new Vector2(0, 1), new Vector2(0, 1), 12, -46, 640, 40);
        TextMeshProUGUI descT = AddText(desc, "Description", 21, Muted, TextAlignmentOptions.TopLeft, true);
        descT.enableAutoSizing = true; descT.fontSizeMin = 16; descT.fontSizeMax = 21;

        RectTransform divider = NewRt("Divider", row);
        divider.anchorMin = new Vector2(0, 0); divider.anchorMax = new Vector2(1, 0); divider.pivot = new Vector2(0.5f, 0);
        divider.sizeDelta = new Vector2(-24, 1); divider.anchoredPosition = new Vector2(0, 0);
        AddImage(divider, null, new Color(1, 1, 1, 0.07f));

        // bouton "DÉFAUT"
        Button defBtn = MakeButton("DefaultButton", row, BtnBase, 90, 34, "DÉFAUT", 20, SettingsStyle.Gold, out TextMeshProUGUI _, new Color32(0x6B, 0x55, 0x1E, 0xFF));
        Anchored((RectTransform)defBtn.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), -13, 0, 90, 34);

        // curseur
        RectTransform sliderRoot = NewRt("SliderRoot", row);
        Anchored(sliderRoot, new Vector2(1, 0.5f), new Vector2(1, 0.5f), ControlRight, 0, 410, 44);
        RectTransform sliderRt = NewRt("Slider", sliderRoot);
        Anchored(sliderRt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 0, 0, 310, 40);
        Slider slider = sliderRt.gameObject.AddComponent<Slider>();
        RectTransform track = NewRt("Background", sliderRt);
        track.anchorMin = new Vector2(0, 0.5f); track.anchorMax = new Vector2(1, 0.5f); track.pivot = new Vector2(0.5f, 0.5f);
        track.sizeDelta = new Vector2(0, 10); track.anchoredPosition = Vector2.zero;
        AddImage(track, _roundRect, SettingsStyle.Track, true, true);
        RectTransform fillArea = NewRt("Fill Area", sliderRt);
        fillArea.anchorMin = new Vector2(0, 0.5f); fillArea.anchorMax = new Vector2(1, 0.5f); fillArea.pivot = new Vector2(0.5f, 0.5f);
        fillArea.sizeDelta = new Vector2(-20, 10); fillArea.anchoredPosition = Vector2.zero;
        RectTransform fill = NewRt("Fill", fillArea);
        fill.anchorMin = new Vector2(0, 0); fill.anchorMax = new Vector2(0, 1); fill.pivot = new Vector2(0, 0.5f);
        fill.sizeDelta = new Vector2(10, 0); fill.anchoredPosition = Vector2.zero;
        AddImage(fill, _roundRect, Accent, true);
        RectTransform handleArea = NewRt("Handle Slide Area", sliderRt);
        Stretch(handleArea, 10, 0, 10, 0);
        RectTransform handle = NewRt("Handle", handleArea);
        handle.anchorMin = new Vector2(0, 0); handle.anchorMax = new Vector2(0, 1); handle.pivot = new Vector2(0.5f, 0.5f);
        handle.sizeDelta = new Vector2(30, 0);
        RectTransform ring = NewRt("Ring", handle); Center(ring, 0, 0, 30, 30);
        AddImage(ring, _circle, Accent);
        RectTransform knob = NewRt("Knob", handle); Center(knob, 0, 0, 22, 22);
        Image handleImg = AddImage(knob, _circle, Color.white, false, true);
        slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0; slider.maxValue = 1; slider.value = 0.5f;
        slider.navigation = NoNav();
        RectTransform val = NewRt("Value", sliderRoot);
        Anchored(val, new Vector2(1, 0.5f), new Vector2(1, 0.5f), 0, 0, 84, 44);
        TextMeshProUGUI valT = AddText(val, "100%", 32, Color.white, TextAlignmentOptions.MidlineRight);

        // interrupteur
        RectTransform toggleRoot = NewRt("ToggleRoot", row);
        Anchored(toggleRoot, new Vector2(1, 0.5f), new Vector2(1, 0.5f), ControlRight, 0, 132, 44);
        RectTransform tTrack = NewRt("Track", toggleRoot); Stretch(tTrack);
        Image trackImg = AddImage(tTrack, _roundRect, SettingsStyle.Track, true, true);
        Button toggleBtn = tTrack.gameObject.AddComponent<Button>();
        toggleBtn.targetGraphic = trackImg; toggleBtn.transition = Selectable.Transition.None; toggleBtn.navigation = NoNav();
        RectTransform tText = NewRt("Text", tTrack); Stretch(tText, 16, 0, 16, 0);
        TextMeshProUGUI tTextT = AddText(tText, "OUI", 26, Color.white, TextAlignmentOptions.MidlineLeft);
        RectTransform tKnob = NewRt("Knob", tTrack);
        Anchored(tKnob, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 4, 0, 36, 36);
        AddImage(tKnob, _circle, Color.white);

        // choix : un seul bouton large ; le clic ouvre la liste des valeurs (SettingsDropdown)
        RectTransform choiceRoot = NewRt("ChoiceRoot", row);
        Anchored(choiceRoot, new Vector2(1, 0.5f), new Vector2(1, 0.5f), ControlRight, 0, 410, 48);
        Button center = MakeButton("Center", choiceRoot, BtnBase, 410, 48, "Valeur", 30, Color.white, out TextMeshProUGUI centerT, AccentSoft);
        Anchored((RectTransform)center.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 0, 410, 48);
        centerT.rectTransform.offsetMin = new Vector2(58f, 0f);      // le texte reste centré dans l'espace laissé par la flèche
        centerT.rectTransform.offsetMax = new Vector2(-58f, 0f);
        RectTransform chev = NewRt("Chevron", center.transform);
        Anchored(chev, new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), -30, 0, 34, 46);
        chev.localRotation = Quaternion.Euler(0, 0, 90f);            // flèche de la page Personnages tournée vers le bas
        Image chevImg = AddImage(chev, _arrowL, Color.white);
        chevImg.preserveAspect = true;

        // touches
        RectTransform keysRoot = NewRt("KeysRoot", row);
        Anchored(keysRoot, new Vector2(1, 0.5f), new Vector2(1, 0.5f), ControlRight, 0, 400, 48);
        Button[] keyBtns = new Button[2]; TextMeshProUGUI[] keyTxt = new TextMeshProUGUI[2]; Image[] keyBorders = new Image[2];
        for (int i = 0; i < 2; i++)
        {
            keyBtns[i] = MakeButton("Key" + (i + 1), keysRoot, BtnBase, 195, 48, "—", 27, Color.white, out keyTxt[i], AccentSoft);
            Anchored((RectTransform)keyBtns[i].transform, new Vector2(i == 0 ? 0f : 1f, 0.5f), new Vector2(i == 0 ? 0f : 1f, 0.5f), 0, 0, 195, 48);
            keyBorders[i] = keyBtns[i].transform.Find("Border").GetComponent<Image>();
        }

        // préréglages
        RectTransform presetsRoot = NewRt("PresetsRoot", row);
        Anchored(presetsRoot, new Vector2(1, 0.5f), new Vector2(1, 0.5f), -16, 0, 490, 48);
        Button[] pBtns = new Button[3]; TextMeshProUGUI[] pTxt = new TextMeshProUGUI[3]; Image[] pBorders = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            pBtns[i] = MakeButton("Preset" + (i + 1), presetsRoot, BtnBase, 156, 48, "AZERTY", 25, Color.white, out pTxt[i], AccentSoft);
            Anchored((RectTransform)pBtns[i].transform, new Vector2(i / 2f, 0.5f), new Vector2(i / 2f, 0.5f), 0, 0, 156, 48);
            pBorders[i] = pBtns[i].transform.Find("Border").GetComponent<Image>();
        }

        // composant
        SettingsRow comp = row.gameObject.AddComponent<SettingsRow>();
        SerializedObject so = new SerializedObject(comp);
        Set(so, "_label", labelT); Set(so, "_description", descT); Set(so, "_highlight", highlight); Set(so, "_group", group); Set(so, "_defaultButton", defBtn);
        Set(so, "_sliderRoot", sliderRoot.gameObject); Set(so, "_slider", slider); Set(so, "_sliderValue", valT);
        Set(so, "_toggleRoot", toggleRoot.gameObject); Set(so, "_toggleButton", toggleBtn); Set(so, "_toggleTrack", trackImg); Set(so, "_toggleKnob", tKnob); Set(so, "_toggleText", tTextT);
        Set(so, "_choiceRoot", choiceRoot.gameObject); Set(so, "_choiceCenter", center); Set(so, "_choiceText", centerT);
        Set(so, "_keysRoot", keysRoot.gameObject); SetArray(so, "_keyButtons", keyBtns); SetArray(so, "_keyTexts", keyTxt); SetArray(so, "_keyBorders", keyBorders);
        Set(so, "_presetsRoot", presetsRoot.gameObject); SetArray(so, "_presetButtons", pBtns); SetArray(so, "_presetTexts", pTxt); SetArray(so, "_presetBorders", pBorders);
        so.ApplyModifiedPropertiesWithoutUndo();
        return comp;
    }

    private static RectTransform BuildHeaderTemplate(Transform parent)
    {
        RectTransform h = NewRt("HeaderRow", parent);
        h.sizeDelta = new Vector2(0, 58);
        LayoutElement le = h.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 58; le.flexibleWidth = 1;
        RectTransform t = NewRt("Text", h);
        Anchored(t, new Vector2(0, 0), new Vector2(0, 0), 12, 8, 900, 34);
        AddText(t, "SECTION", 28, Accent, TextAlignmentOptions.BottomLeft);
        RectTransform line = NewRt("Line", h);
        line.anchorMin = new Vector2(0, 0); line.anchorMax = new Vector2(1, 0); line.pivot = new Vector2(0.5f, 0);
        line.sizeDelta = new Vector2(-24, 2); line.anchoredPosition = Vector2.zero;
        AddImage(line, null, new Color(Accent.r, Accent.g, Accent.b, 0.45f));
        return h;
    }

    private static RectTransform BuildNoteTemplate(Transform parent)
    {
        RectTransform n = NewRt("NoteRow", parent);
        n.sizeDelta = new Vector2(0, 62);
        LayoutElement le = n.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 62; le.flexibleWidth = 1;
        RectTransform t = NewRt("Text", n);
        Stretch(t, 12, 4, 12, 4);
        TextMeshProUGUI tx = AddText(t, "Note", 23, Muted, TextAlignmentOptions.MidlineLeft, true);
        tx.enableAutoSizing = true; tx.fontSizeMin = 16; tx.fontSizeMax = 23;
        return n;
    }

    // ── fenêtres ────────────────────────────────────────────────────────────

    private static RectTransform DialogShell(Transform parent, string name, float w, float h, out RectTransform box)
    {
        RectTransform root = NewRt(name, parent);
        Stretch(root);
        RectTransform dim = NewRt("Dim", root); Stretch(dim);
        AddImage(dim, null, new Color(0.02f, 0.015f, 0.04f, 0.78f), false, true);
        box = NewRt("Box", root);
        Center(box, 0, -20, w, h);
        RectTransform bg = NewRt("Bg", box); Stretch(bg);
        AddImage(bg, null, PanelDark, false, true);
        string[] sides = { "Top", "Bottom", "Left", "Right" };
        for (int i = 0; i < 4; i++)
        {
            RectTransform b = NewRt("Border" + sides[i], box);
            switch (i)
            {
                case 0: b.anchorMin = new Vector2(0, 1); b.anchorMax = new Vector2(1, 1); b.pivot = new Vector2(0.5f, 1); b.sizeDelta = new Vector2(0, 2); break;
                case 1: b.anchorMin = new Vector2(0, 0); b.anchorMax = new Vector2(1, 0); b.pivot = new Vector2(0.5f, 0); b.sizeDelta = new Vector2(0, 2); break;
                case 2: b.anchorMin = new Vector2(0, 0); b.anchorMax = new Vector2(0, 1); b.pivot = new Vector2(0, 0.5f); b.sizeDelta = new Vector2(2, 0); break;
                default: b.anchorMin = new Vector2(1, 0); b.anchorMax = new Vector2(1, 1); b.pivot = new Vector2(1, 0.5f); b.sizeDelta = new Vector2(2, 0); break;
            }
            b.anchoredPosition = Vector2.zero;
            AddImage(b, null, Accent);
        }
        RectTransform bar = NewRt("AccentBar", box);
        bar.anchorMin = new Vector2(0, 1); bar.anchorMax = new Vector2(1, 1); bar.pivot = new Vector2(0.5f, 1);
        bar.sizeDelta = new Vector2(0, 5); bar.anchoredPosition = Vector2.zero;
        AddImage(bar, null, Accent);
        root.gameObject.SetActive(false);
        return root;
    }

    private static void BuildCaptureDialog(Transform parent, out GameObject rootGo, out TextMeshProUGUI title, out TextMeshProUGUI hint)
    {
        RectTransform root = DialogShell(parent, "CaptureDialog", 820, 250, out RectTransform box);
        RectTransform lab = NewRt("Caption", box); Center(lab, 0, 80, 760, 34);
        AddText(lab, "ASSIGNER UNE TOUCHE À", 28, Muted, TextAlignmentOptions.Center);
        RectTransform t = NewRt("Title", box); Center(t, 0, 28, 760, 70);
        title = AddText(t, "ACTION", 62, SettingsStyle.Gold, TextAlignmentOptions.Center, false, true);
        RectTransform h = NewRt("Hint", box); Center(h, 0, -62, 760, 90);
        hint = AddText(h, "", 30, Color.white, TextAlignmentOptions.Center, true);
        rootGo = root.gameObject;
    }

    private static void BuildConfirmDialog(Transform parent, out GameObject rootGo, out TextMeshProUGUI title, out TextMeshProUGUI body,
                                           out Button yes, out TextMeshProUGUI yesText, out Button no)
    {
        RectTransform root = DialogShell(parent, "ConfirmDialog", 820, 310, out RectTransform box);
        RectTransform t = NewRt("Title", box); Center(t, 0, 92, 760, 62);
        title = AddText(t, "TITRE", 54, SettingsStyle.Gold, TextAlignmentOptions.Center, false, true);
        RectTransform b = NewRt("Body", box); Center(b, 0, 22, 720, 90);
        body = AddText(b, "", 30, Body, TextAlignmentOptions.Center, true);
        no = MakeButton("No", box, BtnBase, 260, 58, "Annuler", 32, Body, out TextMeshProUGUI _, AccentSoft);
        Center((RectTransform)no.transform, -150, -96, 260, 58);
        yes = MakeButton("Yes", box, new Color32(0x2F, 0x62, 0x8C, 0xFF), 260, 58, "Confirmer", 32, Color.white, out yesText, Accent);
        Center((RectTransform)yes.transform, 150, -96, 260, 58);
        rootGo = root.gameObject;
    }

    private static void BuildRevertDialog(Transform parent, out GameObject rootGo, out TextMeshProUGUI text, out Button keep, out Button undo)
    {
        RectTransform root = DialogShell(parent, "RevertDialog", 900, 330, out RectTransform box);
        RectTransform t = NewRt("Title", box); Center(t, 0, 98, 840, 110);
        AddText(t, "CONSERVER CES PARAMÈTRES D'AFFICHAGE ?", 50, SettingsStyle.Gold, TextAlignmentOptions.Center, true, true);
        RectTransform b = NewRt("Countdown", box); Center(b, 0, 12, 840, 44);
        text = AddText(b, "", 30, Body, TextAlignmentOptions.Center);
        undo = MakeButton("Undo", box, BtnBase, 300, 58, "Rétablir", 32, Body, out TextMeshProUGUI _, AccentSoft);
        Center((RectTransform)undo.transform, -170, -104, 300, 58);
        keep = MakeButton("Keep", box, new Color32(0x2F, 0x62, 0x8C, 0xFF), 300, 58, "Conserver", 32, Color.white, out TextMeshProUGUI _, Accent);
        Center((RectTransform)keep.transform, 170, -104, 300, 58);
        rootGo = root.gameObject;
    }
}
