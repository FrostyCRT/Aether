using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// OUTIL D'ÉDITEUR (2026-09-19) - (re)construit dans la scène MainMenu la hiérarchie de
// l'onglet Compétences refondu : fiche de détail, habillage des nœuds, liaisons, lueurs du
// décor, en-tête, légendes. Menu : Aether > Rebuild Skill Tree Page.
//
// Idempotent : chaque lancement supprime ce qu'il a construit avant et le refait à neuf. Les
// composants (SkillTreeUI, SkillNode, SkillDetailPanel, SkillTreeLinks, TreeAmbientFX) ne
// construisent RIEN à l'exécution ; la scène reste modifiable à la main, ce script sert à
// régénérer proprement après un changement de charte ou de dimensions.
public static class SkillTreePageBuilder
{
    private const string Ui = "Assets/Game/Assets 2D/UI/";

    private static TMP_FontAsset _font;
    private static Material _labelMat;
    private static Sprite _circle, _roundRect, _coin, _padlock, _parchemin;
    private static Material _additive, _desaturate;

    private static readonly Color PanelDark = SkillTreeStyle.PanelDark;
    private static readonly Color Disc = SkillTreeStyle.Disc;
    private static readonly Color Muted = SkillTreeStyle.Muted;
    private static readonly Color Body = SkillTreeStyle.Body;

    private const float RowTop = 365f, RowMid = 100f, RowBottom = -165f;   // écran : y = 225 / 490 / 755
    private static readonly System.Collections.Generic.Dictionary<string, Vector2> NodePositions = new System.Collections.Generic.Dictionary<string, Vector2>
    {
        // Aether (orange) : colonnes -750 / -410, sommet -580
        { "cadence", new Vector2(-750f, RowBottom) }, { "crystalDamage", new Vector2(-750f, RowMid) },
        { "concentration", new Vector2(-410f, RowBottom) }, { "fragmentation", new Vector2(-410f, RowMid) },
        { "overpower", new Vector2(-580f, RowTop) },
        // Kael (vert) : colonnes -170 / 170, sommet 0
        { "recuperation", new Vector2(-170f, RowBottom) }, { "armor", new Vector2(-170f, RowMid) },
        { "vitality", new Vector2(170f, RowBottom) }, { "secondWind", new Vector2(170f, RowMid) },
        { "manaShield", new Vector2(0f, RowTop) },
        // Lyra (cyan) : colonnes 410 / 750, sommet 580
        { "dash", new Vector2(410f, RowBottom) }, { "novaRadius", new Vector2(410f, RowMid) },
        { "impulsionNova", new Vector2(750f, RowBottom) }, { "crystalMastery", new Vector2(750f, RowMid) },
        { "phantomDash", new Vector2(580f, RowTop) },
    };

    // Reconstruit UNIQUEMENT les nœuds (habillage, positions, cartouches, cadenas) et la config des liaisons :
    // n'écrase ni la fiche de détail, ni l'en-tête, ni les logos (réglés à la main).
    [MenuItem("Aether/Rebuild Skill Tree Nodes")]
    public static void BuildNodesOnly()
    {
        LoadAssets();
        SkillNode anyNode = Object.FindFirstObjectByType<SkillNode>(FindObjectsInactive.Include);
        if (anyNode == null) { Debug.LogError("[SkillTreePageBuilder] SkillNode introuvable."); return; }
        RectTransform treeContainer = (RectTransform)anyNode.transform.parent;
        foreach (SkillNode n in treeContainer.GetComponentsInChildren<SkillNode>(true)) StyleNode(n);
        RectTransform links = BuildLinks(treeContainer);
        SkillTreeUI ui = Object.FindFirstObjectByType<SkillTreeUI>(FindObjectsInactive.Include);
        if (ui != null)
        {
            // "Links" est recréé : on relie la nouvelle instance (sinon l'atténuation de l'arbre se perd)
            SerializedObject uso = new SerializedObject(ui);
            Set(uso, "_links", links.GetComponent<SkillTreeLinks>());
            uso.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.MarkSceneDirty(anyNode.gameObject.scene);
        Debug.Log("[SkillTreePageBuilder] Nœuds reconstruits (scène marquée modifiée : à enregistrer).");
    }

    [MenuItem("Aether/Rebuild Skill Tree Page")]
    public static void Build()
    {
        LoadAssets();

        SkillTreeUI ui = Object.FindFirstObjectByType<SkillTreeUI>(FindObjectsInactive.Include);
        SkillNode anyNode = Object.FindFirstObjectByType<SkillNode>(FindObjectsInactive.Include);
        if (ui == null || anyNode == null) { Debug.LogError("[SkillTreePageBuilder] SkillTreeUI / SkillNode introuvables."); return; }

        RectTransform treeContainer = (RectTransform)anyNode.transform.parent;
        RectTransform panel = (RectTransform)treeContainer.parent;

        foreach (SkillNode n in treeContainer.GetComponentsInChildren<SkillNode>(true)) StyleNode(n);

        RectTransform links = BuildLinks(treeContainer);
        TreeAmbientFX fx = BuildFx(treeContainer);
        var captions = BuildCaptions(treeContainer);

        Header h = BuildHeader(panel);
        DetailRefs d = BuildDetailPanel(panel, ui);

        // --- câblage SkillTreeUI ---
        SerializedObject so = new SerializedObject(ui);
        Set(so, "_panelRoot", panel);
        Set(so, "_detailPanel", d.root);
        Set(so, "_detail", d.panelComponent);
        Set(so, "_detailGroup", d.group);
        Set(so, "_goldText", h.goldText);
        Set(so, "_goldPill", h.goldPill);
        Set(so, "_hintText", h.hint);
        Set(so, "_links", links.GetComponent<SkillTreeLinks>());
        Set(so, "_fx", fx);
        Set(so, "_debugResetButton", h.reset != null ? h.reset.gameObject : null);
        SerializedProperty cp = so.FindProperty("_captions");
        cp.arraySize = captions.Length;
        for (int i = 0; i < captions.Length; i++)
        {
            SerializedProperty e = cp.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("branch").enumValueIndex = (int)captions[i].branch;
            e.FindPropertyRelative("root").objectReferenceValue = captions[i].root;
            e.FindPropertyRelative("border").objectReferenceValue = captions[i].border;
            e.FindPropertyRelative("text").objectReferenceValue = captions[i].text;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        Debug.Log("[SkillTreePageBuilder] Onglet Compétences reconstruit (scène marquée modifiée : à enregistrer).");
    }

    // ── chargement ────────────────────────────────────────────────────────

    private static void LoadAssets()
    {
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Game/Fonts/Bangers-Regular SDF.asset");
        _circle = AssetDatabase.LoadAssetAtPath<Sprite>(Ui + "Personnage/UI_Circle.png");
        _roundRect = AssetDatabase.LoadAssetAtPath<Sprite>(Ui + "Personnage/UI_RoundRect.png");
        _coin = AssetDatabase.LoadAssetAtPath<Sprite>(Ui + "Autres/Gold.png");
        _padlock = AssetDatabase.LoadAssetAtPath<Sprite>(Ui + "Autres/Cadenas.png");
        _parchemin = AssetDatabase.LoadAssetAtPath<Sprite>(Ui + "Autres/Parchemin-removebg-preview.png");
        _additive = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/UI_Additive.mat");
        _desaturate = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/UI_Desaturate.mat");

        // matériau TMP "libellé sur décor clair" : Bangers + contour sombre (les valeurs posées
        // directement sur un TMP dans l'éditeur ne survivent pas au rechargement, un preset oui)
        string mp = "Assets/Game/Fonts/Bangers-Regular SDF - Libelle.mat";
        _labelMat = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (_labelMat == null)
        {
            _labelMat = new Material(_font.material);
            AssetDatabase.CreateAsset(_labelMat, mp);
        }
        _labelMat.EnableKeyword("OUTLINE_ON");
        _labelMat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
        _labelMat.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(0x0C, 0x0A, 0x12, 0xFF));
        _labelMat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.05f);
        EditorUtility.SetDirty(_labelMat);
        AssetDatabase.SaveAssets();
    }

    // ── petites fabriques ─────────────────────────────────────────────────

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

    // ancrage/pivot dans un coin : (0,1) haut-gauche, (1,1) haut-droite, (0.5,1) haut-centre…
    private static void Corner(RectTransform rt, Vector2 anchor, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void Stretch(RectTransform rt, float l = 0, float t = 0, float r = 0, float b = 0)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b);
        rt.offsetMax = new Vector2(-r, -t);
    }

    private static Image AddImage(RectTransform rt, Sprite sprite, Color color, bool sliced = false, bool raycast = false)
    {
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        img.raycastTarget = raycast;
        return img;
    }

    private static TextMeshProUGUI AddText(RectTransform rt, string text, float size, Color color, TextAlignmentOptions align, bool wrap = false, bool outline = false)
    {
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = _font;
        if (outline) t.fontSharedMaterial = _labelMat;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = wrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }

    private static LayoutElement Le(GameObject go, float min = -1, float preferred = -1)
    {
        LayoutElement le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        if (min >= 0) le.minHeight = min;
        if (preferred >= 0) le.preferredHeight = preferred;
        return le;
    }

    private static void Clear(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--) Object.DestroyImmediate(t.GetChild(i).gameObject);
    }

    private static void Set(SerializedObject so, string prop, Object value)
    {
        SerializedProperty p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning("[SkillTreePageBuilder] propriété introuvable : " + prop); return; }
        p.objectReferenceValue = value;
    }

    private static void SetPath(SerializedObject so, string path, Object value)
    {
        SerializedProperty p = so.FindProperty(path);
        if (p == null) { Debug.LogWarning("[SkillTreePageBuilder] propriété introuvable : " + path); return; }
        p.objectReferenceValue = value;
    }

    // ── nœuds ─────────────────────────────────────────────────────────────

    private static void StyleNode(SkillNode node)
    {
        Transform nt = node.transform;

        // Disposition (repère TreeContainer, centre = centre de l'onglet) : trois rangées de 265 px
        // (assez pour que chaque liaison se voie entre le cartouche du haut et le médaillon du bas),
        // deux colonnes par personnage écartées de 340 px, sommet au centre de sa paire.
        // Positions ABSOLUES par identifiant -> le script reste idempotent.
        RectTransform nrt = (RectTransform)nt;
        SerializedObject idso = new SerializedObject(node);
        string nid = idso.FindProperty("_nodeId").stringValue;
        if (NodePositions.TryGetValue(nid, out Vector2 npos)) nrt.anchoredPosition = npos;

        SerializedObject nso = new SerializedObject(node);
        bool unique = nso.FindProperty("_isUnique").boolValue;

        // nettoyage de ce que le script a déjà construit
        foreach (string n in new[] { "Glow", "IconMask", "LockBadge", "NameLabel", "NamePlate" })
        {
            Transform old = nt.Find(n);
            if (old != null)
            {
                // l'icône peut être sous le masque : la sortir avant de détruire
                Transform ic = old.Find("NodeIcon");
                if (ic != null) ic.SetParent(nt, false);
                Object.DestroyImmediate(old.gameObject);
            }
        }

        RectTransform ring = (RectTransform)nt.Find("CircleBorder");
        RectTransform disc = (RectTransform)nt.Find("CircleBackground");
        RectTransform icon = (RectTransform)nt.Find("NodeIcon");
        RectTransform dots = (RectTransform)nt.Find("LevelDots");

        // le bouton ne teinte plus la racine (l'état visuel est piloté par SkillNode)
        Button btn = node.GetComponent<Button>();
        if (btn != null) btn.transition = Selectable.Transition.None;

        // halo additif derrière tout
        RectTransform glow = NewRt("Glow", nt);
        glow.SetSiblingIndex(0);
        Center(glow, 0, 0, 200, 200);
        Image glowImg = AddImage(glow, null, new Color(1, 1, 1, 0));   // sprite + couleur posés à l'exécution (SkillNode.Awake)
        glowImg.material = _additive;

        // fond : disque plein de la couleur "nuit" de la fiche
        Image discImg = disc.GetComponent<Image>();
        discImg.sprite = _circle;
        discImg.color = Disc;
        // L'Image racine du nœud est DÉSACTIVÉE dans la scène : c'est le disque (et le cartouche)
        // qui reçoivent les clics ; le Button, sur la racine, les récupère par remontée d'événement.
        // (Tout mettre en raycastTarget=false rend le nœud incliquable.)
        discImg.raycastTarget = true;
        Center(disc, 0, 0, 82, 82);
        disc.SetSiblingIndex(1);

        // icône : masquée en cercle pour ne jamais déborder du cadre
        Vector2 iconPos = icon.anchoredPosition;
        Vector2 iconSize = icon.sizeDelta;
        RectTransform mask = NewRt("IconMask", nt);
        Center(mask, 0, 0, 78, 78);
        Image maskImg = AddImage(mask, _circle, Color.white);
        Mask m = mask.gameObject.AddComponent<Mask>();
        m.showMaskGraphic = false;
        mask.SetSiblingIndex(2);
        icon.SetParent(mask, false);
        icon.anchoredPosition = iconPos;
        icon.sizeDelta = iconSize;
        icon.GetComponent<Image>().raycastTarget = false;

        // cadre orné par-dessus l'icône (son bord propre, jamais chevauché)
        ring.SetSiblingIndex(3);
        Center(ring, 0, 0, 92, 92);
        ring.GetComponent<Image>().raycastTarget = false;

        // cadenas (nœud verrouillé) : médaillon miniature en bas à droite du cadre
        RectTransform lockBadge = NewRt("LockBadge", nt);
        Center(lockBadge, 48, -25, 48, 48);   // valeurs réglées à la main par l'utilisateur
        AddImage(lockBadge, _circle, SkillTreeStyle.EmptyRing);
        RectTransform lockDisc = NewRt("Disc", lockBadge);
        Center(lockDisc, 0, 0, 42, 42);
        AddImage(lockDisc, _circle, Disc);
        RectTransform lockIcon = NewRt("Icon", lockBadge);
        Center(lockIcon, 0.2f, 0.15f, 41, 31);
        Image li = AddImage(lockIcon, _padlock, Color.white);   // cadenas dessiné par l'utilisateur (peu de détails : petit en jeu), couleurs d'origine
        li.preserveAspect = true;

        // cartouche sous le médaillon : nom + pastilles de progression, sur fond sombre.
        // Il est PAR-DESSUS les liaisons (elles passent dessous : le nom reste lisible) et son
        // liseré dit l'état du nœud (éteint / couleur du perso / or).
        RectTransform plate = NewRt("NamePlate", nt);
        Center(plate, 0, -72, 120, 36);
        RectTransform pBorder = NewRt("Border", plate);
        Stretch(pBorder);
        Image plateBorder = AddImage(pBorder, _roundRect, SkillTreeStyle.EmptyRing, true, true);
        RectTransform pFill = NewRt("Fill", plate);
        Stretch(pFill, 2, 2, 2, 2);
        AddImage(pFill, _roundRect, new Color(PanelDark.r, PanelDark.g, PanelDark.b, 0.93f), true, true);

        RectTransform label = NewRt("NameLabel", plate);
        Corner(label, new Vector2(0.5f, 1f), 0, -2, 260, 17);
        label.anchorMin = new Vector2(0f, 1f); label.anchorMax = new Vector2(1f, 1f);
        label.offsetMin = new Vector2(4, label.offsetMin.y); label.offsetMax = new Vector2(-4, label.offsetMax.y);
        TextMeshProUGUI tmp = AddText(label, "", 12, Color.white, TextAlignmentOptions.Center);
        tmp.enableAutoSizing = true; tmp.fontSizeMin = 10; tmp.fontSizeMax = 12;   // Bangers est déjà gros (réglage utilisateur)

        // pastilles : 3 pastilles (paliers) ou 1 cartouche allongé (talent unique)
        if (dots != null) Object.DestroyImmediate(dots.gameObject);
        RectTransform dotsRt = NewRt("LevelDots", plate);
        Corner(dotsRt, new Vector2(0.5f, 0f), 0, 5, 70, 12);
        Image[] pips = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            RectTransform p = NewRt($"Dot{i + 1}", dotsRt);
            if (unique && i == 0)
            {
                Center(p, 0, 0, 34, 10);
                pips[i] = AddImage(p, _roundRect, SkillTreeStyle.EmptyPip, true);
            }
            else
            {
                Center(p, (i - 1) * 18f, 0, 10, 10);
                pips[i] = AddImage(p, _circle, SkillTreeStyle.EmptyPip);
            }
        }

        CanvasGroup cg = node.GetComponent<CanvasGroup>();
        if (cg == null) cg = node.gameObject.AddComponent<CanvasGroup>();

        // câblage
        SerializedObject so = new SerializedObject(node);
        Set(so, "_glow", glowImg);
        Set(so, "_ringImage", ring.GetComponent<Image>());
        Set(so, "_discImage", discImg);
        Set(so, "_iconImage", icon.GetComponent<Image>());
        Set(so, "_lockBadge", lockBadge.gameObject);
        Set(so, "_nameLabel", tmp);
        Set(so, "_plate", plate);
        Set(so, "_plateBorder", plateBorder);
        Set(so, "_dot1", pips[0]);
        Set(so, "_dot2", pips[1]);
        Set(so, "_dot3", pips[2]);
        Set(so, "_group", cg);
        Set(so, "_desaturateMaterial", _desaturate);
        so.FindProperty("_baseScale").floatValue = 1.5f;
        nrt.localScale = new Vector3(1.5f, 1.5f, 1f);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── liaisons / lueurs / légendes ──────────────────────────────────────

    private static RectTransform BuildLinks(RectTransform treeContainer)
    {
        Transform old = treeContainer.Find("Links");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        int first = int.MaxValue;
        foreach (SkillNode n in treeContainer.GetComponentsInChildren<SkillNode>(true))
            first = Mathf.Min(first, n.transform.GetSiblingIndex());

        RectTransform links = NewRt("Links", treeContainer);
        Center(links, 0, 0, 0, 0);
        Stretch(links);
        links.SetSiblingIndex(first);
        SkillTreeLinks comp = links.gameObject.AddComponent<SkillTreeLinks>();
        SerializedObject so = new SerializedObject(comp);
        Set(so, "_nodesParent", treeContainer);
        Set(so, "_lineSprite", _roundRect);
        so.FindProperty("_trim").floatValue = 74f;
        Set(so, "_additiveMaterial", _additive);
        so.ApplyModifiedPropertiesWithoutUndo();
        return links;
    }

    private static TreeAmbientFX BuildFx(RectTransform treeContainer)
    {
        TreeAmbientFX fx = treeContainer.GetComponent<TreeAmbientFX>();
        if (fx == null) fx = treeContainer.gameObject.AddComponent<TreeAmbientFX>();

        SerializedObject so = new SerializedObject(fx);
        Set(so, "_additiveMaterial", _additive);

        string[] logos = { "Aether", "Kael", "Lyra" };
        SerializedProperty cr = so.FindProperty("_crystals");
        cr.arraySize = logos.Length;
        for (int i = 0; i < logos.Length; i++)
        {
            SerializedProperty e = cr.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("logo").objectReferenceValue = treeContainer.Find(logos[i]);
            e.FindPropertyRelative("offset").vector2Value = new Vector2(0f, 0.3f);
            e.FindPropertyRelative("color").colorValue = new Color(0.45f, 0.75f, 1f);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return fx;
    }

    private static SkillTreeUI.BranchCaption[] BuildCaptions(RectTransform treeContainer)
    {
        string[] logos = { "Aether", "Kael", "Lyra" };
        SkillTreeData.CharacterBranch[] branches =
        {
            SkillTreeData.CharacterBranch.Guerrier, SkillTreeData.CharacterBranch.Gardien, SkillTreeData.CharacterBranch.Fantome
        };
        var result = new SkillTreeUI.BranchCaption[3];

        for (int i = 0; i < 3; i++)
        {
            string name = "Caption_" + logos[i];
            Transform old = treeContainer.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            RectTransform logo = (RectTransform)treeContainer.Find(logos[i]);
            RectTransform root = NewRt(name, treeContainer);
            // logo remonté de 16 px (positions absolues : le script reste idempotent) pour dégager le bas
            // (taille et position des logos : réglées à la main, on n'y touche pas)
            Center(root, logo.anchoredPosition.x, -450f, 360f, 54f);

            RectTransform border = NewRt("Border", root);
            Stretch(border);
            Image bi = AddImage(border, _roundRect, SkillTreeStyle.EmptyRing, true);
            RectTransform fill = NewRt("Fill", root);
            Stretch(fill, 3, 3, 3, 3);
            AddImage(fill, _roundRect, new Color(PanelDark.r, PanelDark.g, PanelDark.b, 0.94f), true);
            RectTransform text = NewRt("Text", root);
            Stretch(text);
            TextMeshProUGUI t = AddText(text, "", 32, Color.white, TextAlignmentOptions.Center);

            result[i] = new SkillTreeUI.BranchCaption { branch = branches[i], root = root, border = bi, text = t };
        }
        return result;
    }

    // ── en-tête ───────────────────────────────────────────────────────────

    private class Header
    {
        public TextMeshProUGUI goldText, hint;
        public RectTransform goldPill;
        public Button reset;
    }

    private static Header BuildHeader(RectTransform panel)
    {
        Header h = new Header();

        // le grand titre "ARBRE DE COMPÉTENCES" est supprimé (l'onglet actif le dit déjà) : de la place gagnée
        Transform title = panel.Find("TreeTitle");
        if (title != null) Object.DestroyImmediate(title.gameObject);

        // sous-titre (dans une pastille sombre : lisible sur le feuillage clair)
        Transform old = panel.Find("TreeSubtitle");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        RectTransform sub = NewRt("TreeSubtitle", panel);
        Corner(sub, new Vector2(0.5f, 1f), 0, -14, 640, 38);
        AddImage(sub, _roundRect, new Color(PanelDark.r, PanelDark.g, PanelDark.b, 0.82f), true);
        RectTransform st = NewRt("Text", sub);
        Stretch(st);
        h.hint = AddText(st, "", 24, Body, TextAlignmentOptions.Center);

        // compteur d'or : pastille (même construction que les puces de la fiche Personnage)
        // GoldText est conservé d'un lancement à l'autre (l'objet est référencé ailleurs) : au
        // 2e lancement il est déjà dans l'ancienne pastille, on le sort avant de la détruire.
        RectTransform goldText = (RectTransform)panel.Find("GoldText");
        Transform oldPill = panel.Find("GoldPill");
        if (goldText == null && oldPill != null) goldText = (RectTransform)oldPill.Find("GoldText");
        if (goldText != null) goldText.SetParent(panel, false);
        if (oldPill != null) Object.DestroyImmediate(oldPill.gameObject);
        if (goldText == null)
        {
            goldText = NewRt("GoldText", panel);
            AddText(goldText, "0", 40, Color.white, TextAlignmentOptions.MidlineRight);
        }
        // l'ancien GoldText a un enfant GoldIcon : on l'enlève (recréé dans la pastille)
        Clear(goldText);

        RectTransform pill = NewRt("GoldPill", panel);
        Corner(pill, new Vector2(1f, 1f), -28, -22, 230, 58);
        RectTransform pb = NewRt("Border", pill);
        Stretch(pb);
        AddImage(pb, _roundRect, SkillTreeStyle.Gold, true);
        RectTransform pf = NewRt("Fill", pill);
        Stretch(pf, 2, 2, 2, 2);
        AddImage(pf, _roundRect, new Color(PanelDark.r, PanelDark.g, PanelDark.b, 0.94f), true);
        RectTransform coin = NewRt("Coin", pill);
        Corner(coin, new Vector2(0f, 0.5f), 10, 0, 44, 44);
        Image ci = AddImage(coin, _coin, Color.white);
        ci.preserveAspect = true;

        goldText.SetParent(pill, false);
        Stretch(goldText, 62, 0, 20, 0);
        TextMeshProUGUI gt = goldText.GetComponent<TextMeshProUGUI>();
        gt.fontSize = 40;
        gt.color = Color.white;
        gt.alignment = TextAlignmentOptions.MidlineRight;
        gt.enableWordWrapping = false;
        gt.raycastTarget = false;
        h.goldText = gt;
        h.goldPill = pill;

        // bouton de remise à zéro : outil de développement, discret et clairement étiqueté
        RectTransform reset = (RectTransform)panel.Find("Reset");
        if (reset != null)
        {
            Clear(reset);
            Corner(reset, new Vector2(0f, 1f), 24, -22, 260, 34);
            Image ri = reset.GetComponent<Image>();
            ri.sprite = _roundRect;
            ri.type = Image.Type.Sliced;
            ri.color = new Color(0.20f, 0.09f, 0.11f, 0.92f);
            RectTransform rt = NewRt("Text", reset);
            Stretch(rt);
            AddText(rt, "DEBUG · RÉINITIALISER", 18, new Color(1f, 0.62f, 0.58f), TextAlignmentOptions.Center);
            h.reset = reset.GetComponent<Button>();
        }
        return h;
    }

    // ── fiche de détail ───────────────────────────────────────────────────

    private class DetailRefs
    {
        public RectTransform root;
        public CanvasGroup group;
        public SkillDetailPanel panelComponent;
    }

    private static RectTransform Chip(RectTransform parent, string name, float x, float y, out Image border, out TextMeshProUGUI label)
    {
        RectTransform root = NewRt(name, parent);
        Corner(root, new Vector2(0f, 1f), x, y, 110, 32);
        RectTransform b = NewRt("Border", root);
        Stretch(b);
        border = AddImage(b, _roundRect, SkillTreeStyle.EmptyRing, true);
        RectTransform f = NewRt("Fill", root);
        Stretch(f, 2, 2, 2, 2);
        AddImage(f, _roundRect, Disc, true);
        RectTransform t = NewRt("Label", root);
        Stretch(t);
        label = AddText(t, "TAG", 22, Color.white, TextAlignmentOptions.Center);
        return root;
    }

    private static RectTransform Divider(Transform parent, string name, System.Collections.Generic.List<Image> accents)
    {
        RectTransform d = NewRt(name, parent);
        Image img = AddImage(d, null, Color.white);
        Le(d.gameObject, 2, 2).flexibleWidth = 1;
        accents.Add(img);
        return d;
    }

    private static VerticalLayoutGroup Vlg(GameObject go, float spacing, RectOffset padding = null)
    {
        VerticalLayoutGroup v = go.AddComponent<VerticalLayoutGroup>();
        v.spacing = spacing;
        v.padding = padding ?? new RectOffset(0, 0, 0, 0);
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        v.childAlignment = TextAnchor.UpperLeft;
        return v;
    }

    private static DetailRefs BuildDetailPanel(RectTransform panel, SkillTreeUI ui)
    {
        RectTransform root = (RectTransform)panel.Find("NodeDetailPanel");
        Clear(root);
        foreach (Component c in root.GetComponents<Component>())
            if (c is Graphic) Object.DestroyImmediate(c);
        foreach (Component c in root.GetComponents<Component>())
            if (c is SkillDetailPanel || c is VerticalLayoutGroup || c is ContentSizeFitter || c is CanvasGroup) Object.DestroyImmediate(c);

        const float W = 560f;
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(W, 600f);
        root.localScale = Vector3.one;

        CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
        Vlg(root.gameObject, 12, new RectOffset(34, 34, 26, 24));
        ContentSizeFitter csf = root.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        var accents = new System.Collections.Generic.List<Image>();   // alpha 0.45 (séparateurs)
        var strong = new System.Collections.Generic.List<Image>();    // alpha 1 (barre, filets)

        // fond + filets (hors flux de mise en page)
        RectTransform bg = NewRt("Bg", root);
        bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        Stretch(bg);
        AddImage(bg, null, new Color(PanelDark.r, PanelDark.g, PanelDark.b, 0.985f), false, true);   // capte les clics : pas de clic "à travers" la fiche

        string[] borders = { "BorderTop", "BorderBottom", "BorderLeft", "BorderRight" };
        for (int i = 0; i < 4; i++)
        {
            RectTransform b = NewRt(borders[i], root);
            b.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            b.anchorMin = i == 1 ? new Vector2(0, 0) : new Vector2(0, 1);
            b.anchorMax = i == 0 ? new Vector2(1, 1) : i == 1 ? new Vector2(1, 0) : i == 2 ? new Vector2(0, 0) : new Vector2(1, 0);
            if (i == 3) b.anchorMin = new Vector2(1, 0);
            switch (i)
            {
                case 0: b.anchorMin = new Vector2(0, 1); b.anchorMax = new Vector2(1, 1); b.pivot = new Vector2(0.5f, 1); b.sizeDelta = new Vector2(0, 2); break;
                case 1: b.anchorMin = new Vector2(0, 0); b.anchorMax = new Vector2(1, 0); b.pivot = new Vector2(0.5f, 0); b.sizeDelta = new Vector2(0, 2); break;
                case 2: b.anchorMin = new Vector2(0, 0); b.anchorMax = new Vector2(0, 1); b.pivot = new Vector2(0, 0.5f); b.sizeDelta = new Vector2(2, 0); break;
                default: b.anchorMin = new Vector2(1, 0); b.anchorMax = new Vector2(1, 1); b.pivot = new Vector2(1, 0.5f); b.sizeDelta = new Vector2(2, 0); break;
            }
            b.anchoredPosition = Vector2.zero;
            strong.Add(AddImage(b, null, Color.white));
        }
        RectTransform bar = NewRt("AccentBar", root);
        bar.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        bar.anchorMin = new Vector2(0, 1); bar.anchorMax = new Vector2(1, 1); bar.pivot = new Vector2(0.5f, 1);
        bar.sizeDelta = new Vector2(0, 5); bar.anchoredPosition = Vector2.zero;
        strong.Add(AddImage(bar, null, Color.white));

        // ---- en-tête -----------------------------------------------------
        RectTransform header = NewRt("Header", root);
        Le(header.gameObject, 114, 114);

        RectTransform medal = NewRt("Medallion", header);
        Corner(medal, new Vector2(0, 1), 0, -4, 108, 108);
        RectTransform ring = NewRt("Ring", medal); Stretch(ring);
        Image ringImg = AddImage(ring, _circle, SkillTreeStyle.EmptyRing);
        RectTransform disc = NewRt("Disc", medal); Stretch(disc, 5, 5, 5, 5);
        AddImage(disc, _circle, Disc);
        RectTransform mask = NewRt("Mask", medal); Stretch(mask, 9, 9, 9, 9);
        AddImage(mask, _circle, Color.white);
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        RectTransform icon = NewRt("Icon", mask); Stretch(icon, 3, 3, 3, 3);   // dans le masque (pas plus grand : une icône plus grande que le cercle serait rognée)
        Image iconImg = AddImage(icon, null, Color.white);
        iconImg.preserveAspect = true;

        RectTransform nameRt = NewRt("Name", header);
        Corner(nameRt, new Vector2(0, 1), 128, -2, 330, 62);
        TextMeshProUGUI nameText = AddText(nameRt, "Nom", 46, Color.white, TextAlignmentOptions.MidlineLeft);
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 30; nameText.fontSizeMax = 46;

        Image ownerBorder, kindBorder; TextMeshProUGUI ownerLabel, kindLabel;
        RectTransform chipOwner = Chip(header, "ChipOwner", 128, -74, out ownerBorder, out ownerLabel);
        RectTransform chipKind = Chip(header, "ChipKind", 250, -74, out kindBorder, out kindLabel);
        // la 2e puce se place à droite de la 1re (largeur dynamique) : layout horizontal léger
        HorizontalLayoutGroup chipRow = null;
        RectTransform row = NewRt("ChipRow", header);
        Corner(row, new Vector2(0, 1), 128, -74, 400, 32);
        chipOwner.SetParent(row, false);
        chipKind.SetParent(row, false);
        chipRow = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        chipRow.spacing = 8;
        chipRow.childControlWidth = false; chipRow.childControlHeight = false;
        chipRow.childForceExpandWidth = false; chipRow.childForceExpandHeight = false;
        chipRow.childAlignment = TextAnchor.MiddleLeft;

        RectTransform closeRt = NewRt("CloseButton", root);
        closeRt.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        Corner(closeRt, new Vector2(1, 1), -14, -14, 40, 40);
        Image closeImg = AddImage(closeRt, _circle, new Color(Disc.r, Disc.g, Disc.b, 0.95f), false, true);
        Button closeBtn = closeRt.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        ColorBlock cb = closeBtn.colors;
        cb.normalColor = Color.white; cb.highlightedColor = new Color(1f, 0.9f, 0.9f); cb.pressedColor = new Color(0.75f, 0.75f, 0.75f);
        cb.selectedColor = Color.white; cb.colorMultiplier = 1f;
        closeBtn.colors = cb;
        RectTransform closeTxt = NewRt("Text", closeRt);
        Stretch(closeTxt);
        AddText(closeTxt, "X", 26, Muted, TextAlignmentOptions.Center);

        Divider(root, "Divider1", accents);

        // ---- description -------------------------------------------------
        RectTransform descRt = NewRt("Description", root);
        TextMeshProUGUI desc = AddText(descRt, "Description", 26, Body, TextAlignmentOptions.TopLeft, true);
        Le(descRt.gameObject, 34);

        // ---- paliers -----------------------------------------------------
        RectTransform levels = NewRt("LevelsGroup", root);
        Vlg(levels.gameObject, 6);
        Divider(levels, "Divider2", accents);
        RectTransform levelsLabel = NewRt("Label", levels);
        AddText(levelsLabel, "PALIERS", 22, Muted, TextAlignmentOptions.MidlineLeft);
        Le(levelsLabel.gameObject, 30, 30);

        SkillDetailPanel.LevelRow[] levelRows = new SkillDetailPanel.LevelRow[3];
        RectTransform[] levelRts = new RectTransform[3];
        for (int i = 0; i < 3; i++)
        {
            RectTransform lr = NewRt($"Level{i + 1}", levels);
            Le(lr.gameObject, 44, 44);
            levelRts[i] = lr;
            RectTransform hl = NewRt("Highlight", lr); Stretch(hl, -8, 0, -8, 0);
            Image hlImg = AddImage(hl, _roundRect, new Color(1, 1, 1, 0), true);
            RectTransform pr = NewRt("PipRing", lr); Corner(pr, new Vector2(0, 0.5f), 4, 0, 24, 24);
            Image prImg = AddImage(pr, _circle, SkillTreeStyle.EmptyRing);
            RectTransform ph = NewRt("PipHole", pr); Center(ph, 0, 0, 16, 16);
            AddImage(ph, _circle, Disc);
            RectTransform pf = NewRt("PipFill", pr); Center(pf, 0, 0, 12, 12);
            Image pfImg = AddImage(pf, _circle, Color.white);
            RectTransform tg = NewRt("Tag", lr); Corner(tg, new Vector2(0, 0.5f), 42, 0, 70, 40);
            TextMeshProUGUI tagT = AddText(tg, "NIV 1", 24, Color.white, TextAlignmentOptions.MidlineLeft);
            RectTransform ef = NewRt("Effect", lr); Corner(ef, new Vector2(0, 0.5f), 112, 0, 278, 40);
            TextMeshProUGUI efT = AddText(ef, "Effet", 26, Color.white, TextAlignmentOptions.MidlineLeft);
            efT.enableAutoSizing = true; efT.fontSizeMin = 17; efT.fontSizeMax = 26;
            RectTransform cn = NewRt("Coin", lr); Corner(cn, new Vector2(1, 0.5f), -70, 0, 24, 24);
            Image cnImg = AddImage(cn, _coin, Color.white); cnImg.preserveAspect = true;
            RectTransform rt = NewRt("Right", lr); Corner(rt, new Vector2(1, 0.5f), 0, 0, 68, 40);
            TextMeshProUGUI rtT = AddText(rt, "100", 26, SkillTreeStyle.Gold, TextAlignmentOptions.MidlineRight);
            rtT.enableWordWrapping = false;

            levelRows[i] = new SkillDetailPanel.LevelRow { root = lr.gameObject, highlight = hlImg, pipRing = prImg, pipFill = pfImg, tag = tagT, effect = efT, right = rtT, coin = cnImg };
        }

        // ---- prérequis ---------------------------------------------------
        RectTransform pre = NewRt("PrereqGroup", root);
        Vlg(pre.gameObject, 4);
        Divider(pre, "Divider3", accents);
        RectTransform preLabel = NewRt("Label", pre);
        AddText(preLabel, "REQUIS", 22, Muted, TextAlignmentOptions.MidlineLeft);
        Le(preLabel.gameObject, 30, 30);
        SkillDetailPanel.PrereqRow[] preRows = new SkillDetailPanel.PrereqRow[2];
        for (int i = 0; i < 2; i++)
        {
            RectTransform pr = NewRt($"Prereq{i + 1}", pre);
            Le(pr.gameObject, 34, 34);
            RectTransform pring = NewRt("PipRing", pr); Corner(pring, new Vector2(0, 0.5f), 4, 0, 22, 22);
            Image prImg = AddImage(pring, _circle, SkillTreeStyle.EmptyRing);
            RectTransform phole = NewRt("PipHole", pring); Center(phole, 0, 0, 14, 14);
            AddImage(phole, _circle, Disc);
            RectTransform pfill = NewRt("PipFill", pring); Center(pfill, 0, 0, 10, 10);
            Image pfImg = AddImage(pfill, _circle, Color.white);
            RectTransform tx = NewRt("Text", pr); Corner(tx, new Vector2(0, 0.5f), 40, 0, 440, 34);
            TextMeshProUGUI txT = AddText(tx, "Prérequis", 26, Body, TextAlignmentOptions.MidlineLeft);
            txT.enableWordWrapping = false;
            preRows[i] = new SkillDetailPanel.PrereqRow { root = pr.gameObject, pipRing = prImg, pipFill = pfImg, text = txT };
        }

        // ---- achat -------------------------------------------------------
        Divider(root, "Divider4", accents);

        RectTransform costRow = NewRt("CostRow", root);
        Le(costRow.gameObject, 50, 50);
        RectTransform cl = NewRt("CostLabel", costRow); Corner(cl, new Vector2(0, 0.5f), 0, 0, 160, 40);
        TextMeshProUGUI costLabel = AddText(cl, "COÛT", 22, Muted, TextAlignmentOptions.MidlineLeft);
        RectTransform grp = NewRt("CostGroup", costRow);
        Corner(grp, new Vector2(1, 0.5f), 0, 0, 10, 50);
        HorizontalLayoutGroup hg = grp.gameObject.AddComponent<HorizontalLayoutGroup>();
        hg.spacing = 10; hg.childAlignment = TextAnchor.MiddleRight;
        hg.childControlWidth = true; hg.childControlHeight = true;
        hg.childForceExpandWidth = false; hg.childForceExpandHeight = false;
        ContentSizeFitter hf = grp.gameObject.AddComponent<ContentSizeFitter>();
        hf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        hf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        RectTransform cc = NewRt("CostCoin", grp);
        LayoutElement cle = cc.gameObject.AddComponent<LayoutElement>(); cle.preferredWidth = 38; cle.preferredHeight = 38;
        Image costCoin = AddImage(cc, _coin, Color.white); costCoin.preserveAspect = true;
        RectTransform cv = NewRt("CostValue", grp);
        cv.gameObject.AddComponent<LayoutElement>().minHeight = 50;
        TextMeshProUGUI costValue = AddText(cv, "300", 46, SkillTreeStyle.Gold, TextAlignmentOptions.MidlineRight);
        costValue.enableWordWrapping = false;

        RectTransform goldRow = NewRt("GoldRow", root);
        Le(goldRow.gameObject, 34, 34);
        RectTransform gl = NewRt("GoldLabel", goldRow); Corner(gl, new Vector2(0, 0.5f), 0, 0, 160, 34);
        AddText(gl, "TON OR", 22, Muted, TextAlignmentOptions.MidlineLeft);
        RectTransform after = NewRt("AfterValue", goldRow); Corner(after, new Vector2(0, 0.5f), 150, 0, 200, 34);
        TextMeshProUGUI afterT = AddText(after, "reste 0", 22, Muted, TextAlignmentOptions.MidlineLeft);
        RectTransform gv = NewRt("GoldValue", goldRow); Corner(gv, new Vector2(1, 0.5f), 0, 0, 140, 34);
        TextMeshProUGUI goldValue = AddText(gv, "0", 32, Color.white, TextAlignmentOptions.MidlineRight);

        RectTransform statusRt = NewRt("Status", root);
        TextMeshProUGUI status = AddText(statusRt, "", 24, Muted, TextAlignmentOptions.Center, true);
        Le(statusRt.gameObject, 30);

        RectTransform buyRow = NewRt("BuyRow", root);
        Le(buyRow.gameObject, 82, 82);
        RectTransform buy = NewRt("BuyButton", buyRow);
        Center(buy, 0, 0, 430, 80);
        Image buyImg = AddImage(buy, _parchemin, Color.white, false, true);
        Button buyBtn = buy.gameObject.AddComponent<Button>();
        buyBtn.targetGraphic = buyImg;
        ColorBlock bb = buyBtn.colors;
        bb.normalColor = Color.white; bb.highlightedColor = new Color(1f, 0.93f, 0.78f); bb.pressedColor = new Color(0.78f, 0.74f, 0.66f);
        bb.selectedColor = Color.white; bb.disabledColor = Color.white; bb.colorMultiplier = 1f;
        buyBtn.colors = bb;
        RectTransform buyTxt = NewRt("Label", buy); Stretch(buyTxt);
        TextMeshProUGUI buyLabel = AddText(buyTxt, "ACHETER", 36, new Color32(0x32, 0x32, 0x32, 0xFF), TextAlignmentOptions.Center);

        // ---- composant + câblage -----------------------------------------
        SkillDetailPanel comp = root.gameObject.AddComponent<SkillDetailPanel>();
        SerializedObject so = new SerializedObject(comp);

        SerializedProperty at = so.FindProperty("_accentTargets");
        at.arraySize = strong.Count + accents.Count;
        for (int i = 0; i < strong.Count; i++)
        {
            SerializedProperty e = at.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("graphic").objectReferenceValue = strong[i];
            e.FindPropertyRelative("alpha").floatValue = 1f;
        }
        for (int i = 0; i < accents.Count; i++)
        {
            SerializedProperty e = at.GetArrayElementAtIndex(strong.Count + i);
            e.FindPropertyRelative("graphic").objectReferenceValue = accents[i];
            e.FindPropertyRelative("alpha").floatValue = 0.45f;
        }

        Set(so, "_ring", ringImg);
        Set(so, "_icon", iconImg);
        Set(so, "_name", nameText);
        SetPath(so, "_chipOwner.root", chipOwner);
        SetPath(so, "_chipOwner.border", ownerBorder);
        SetPath(so, "_chipOwner.fill", chipOwner.Find("Fill").GetComponent<Image>());
        SetPath(so, "_chipOwner.label", ownerLabel);
        SetPath(so, "_chipKind.root", chipKind);
        SetPath(so, "_chipKind.border", kindBorder);
        SetPath(so, "_chipKind.fill", chipKind.Find("Fill").GetComponent<Image>());
        SetPath(so, "_chipKind.label", kindLabel);
        Set(so, "_description", desc);
        Set(so, "_levelsGroup", levels.gameObject);
        SerializedProperty lp = so.FindProperty("_levels");
        lp.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            SerializedProperty e = lp.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("root").objectReferenceValue = levelRows[i].root;
            e.FindPropertyRelative("highlight").objectReferenceValue = levelRows[i].highlight;
            e.FindPropertyRelative("pipRing").objectReferenceValue = levelRows[i].pipRing;
            e.FindPropertyRelative("pipFill").objectReferenceValue = levelRows[i].pipFill;
            e.FindPropertyRelative("tag").objectReferenceValue = levelRows[i].tag;
            e.FindPropertyRelative("effect").objectReferenceValue = levelRows[i].effect;
            e.FindPropertyRelative("right").objectReferenceValue = levelRows[i].right;
            e.FindPropertyRelative("coin").objectReferenceValue = levelRows[i].coin;
        }
        Set(so, "_prereqGroup", pre.gameObject);
        SerializedProperty pp = so.FindProperty("_prereqs");
        pp.arraySize = 2;
        for (int i = 0; i < 2; i++)
        {
            SerializedProperty e = pp.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("root").objectReferenceValue = preRows[i].root;
            e.FindPropertyRelative("pipRing").objectReferenceValue = preRows[i].pipRing;
            e.FindPropertyRelative("pipFill").objectReferenceValue = preRows[i].pipFill;
            e.FindPropertyRelative("text").objectReferenceValue = preRows[i].text;
        }
        Set(so, "_costLabel", costLabel);
        Set(so, "_costValue", costValue);
        Set(so, "_costCoin", costCoin);
        Set(so, "_goldValue", goldValue);
        Set(so, "_afterValue", afterT);
        Set(so, "_statusText", status);
        Set(so, "_buyButton", buyBtn);
        Set(so, "_buyImage", buyImg);
        Set(so, "_buyLabel", buyLabel);
        Set(so, "_closeButton", closeBtn);
        so.ApplyModifiedPropertiesWithoutUndo();

        root.gameObject.SetActive(false);
        return new DetailRefs { root = root, group = group, panelComponent = comp };
    }
}
