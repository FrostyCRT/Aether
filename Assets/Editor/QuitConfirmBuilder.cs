using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// AJOUTÉ (2026-10-28) - construit le prefab de la fenêtre de confirmation de fermeture du jeu (Alt+F4, croix de la
// fenêtre, voir QuitConfirm) avec les fabriques de la page Paramètres : même coque, mêmes couleurs, mêmes boutons que
// sa fenêtre « QUITTER LE JEU ? ». Prefab rangé dans Resources, chargé par QuitConfirm à la première demande.
public static class QuitConfirmBuilder
{
    private const string PrefabPath = "Assets/Resources/UI/QuitConfirmDialog.prefab";

    [MenuItem("Aether/Rebuild Quit Confirm Dialog")]
    public static void Build()
    {
        SettingsPageBuilder.LoadAssets();

        GameObject root = new GameObject("QuitConfirmDialog", typeof(RectTransform));
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;   // au-dessus de toute autre interface (menus, pause, écrans de fin)
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0f;   // comme les canevas des scènes
        root.AddComponent<GraphicRaycaster>();

        // Même gabarit que BuildConfirmDialog (SettingsPageBuilder).
        RectTransform dialog = SettingsPageBuilder.DialogShell(root.transform, "ConfirmDialog", 820, 310, out RectTransform box);
        RectTransform t = SettingsPageBuilder.NewRt("Title", box); SettingsPageBuilder.Center(t, 0, 92, 760, 62);
        TextMeshProUGUI title = SettingsPageBuilder.AddText(t, "QUITTER LE JEU ?", 54, SettingsStyle.Gold, TextAlignmentOptions.Center, false, true);
        RectTransform b = SettingsPageBuilder.NewRt("Body", box); SettingsPageBuilder.Center(b, 0, 22, 720, 90);
        TextMeshProUGUI body = SettingsPageBuilder.AddText(b, "", 30, SettingsStyle.Body, TextAlignmentOptions.Center, true);
        GameFonts.ToBody(body);   // phrase : police de lecture (voir GameFonts)
        Button no = SettingsPageBuilder.MakeButton("No", box, SettingsPageBuilder.BtnBase, 260, 58, "Annuler", 32, SettingsStyle.Body, out TextMeshProUGUI noText, SettingsStyle.AccentSoft);
        SettingsPageBuilder.Center((RectTransform)no.transform, -150, -96, 260, 58);
        Button yes = SettingsPageBuilder.MakeButton("Yes", box, new Color32(0x2F, 0x62, 0x8C, 0xFF), 260, 58, "Quitter", 32, Color.white, out TextMeshProUGUI yesText, SettingsStyle.Accent);
        SettingsPageBuilder.Center((RectTransform)yes.transform, 150, -96, 260, 58);

        QuitConfirm qc = root.AddComponent<QuitConfirm>();
        SerializedObject so = new SerializedObject(qc);
        so.FindProperty("_dialogRoot").objectReferenceValue = dialog.gameObject;
        so.FindProperty("_title").objectReferenceValue = title;
        so.FindProperty("_body").objectReferenceValue = body;
        so.FindProperty("_yesText").objectReferenceValue = yesText;
        so.FindProperty("_noText").objectReferenceValue = noText;
        so.FindProperty("_yes").objectReferenceValue = yes;
        so.FindProperty("_no").objectReferenceValue = no;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (!AssetDatabase.IsValidFolder("Assets/Resources/UI")) AssetDatabase.CreateFolder("Assets/Resources", "UI");
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("[QuitConfirmBuilder] " + PrefabPath + " reconstruit.");
    }
}
