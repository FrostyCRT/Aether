using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// OUTIL D'ÉDITEUR (2026-09-20) - (re)construit le fond vivant du menu principal (Accueil) dans la scène MainMenu :
// menu Aether > Rebuild Menu Background. Idempotent.
//
//  MenuBackground (conteneur plein cadre, RectMask2D, MenuBackgroundController)
//    ├─ Layer_Aether / Layer_Kael / Layer_Lyra   (les 3 illustrations, shader vent)
//    └─ Fx                                       (MenuLeavesFX : feuilles, oiseaux, poussière)
//
// Règle aussi l'import des 3 illustrations (Sprite, sans mipmaps, qualité haute : ce sont de grandes images
// dégradées qu'une compression forte marbre).
public static class MenuBackgroundBuilder
{
    private const string Dir = "Assets/Game/Assets 2D/UI/Backgrounds/Menu/";
    private static readonly string[] Files = { "MenuAether.jpg", "MenuKael.jpg", "MenuLyra.jpg" };
    private static readonly string[] Names = { "Layer_Aether", "Layer_Kael", "Layer_Lyra" };

    [MenuItem("Aether/Rebuild Menu Background")]
    public static void Build()
    {
        MainMenuManager mm = Object.FindFirstObjectByType<MainMenuManager>(FindObjectsInactive.Include);
        if (mm == null) { Debug.LogError("[MenuBackgroundBuilder] MainMenuManager introuvable."); return; }
        GameObject menuPanel = (GameObject)new SerializedObject(mm).FindProperty("_menuPanel").objectReferenceValue;
        Transform bg = menuPanel.transform.Find("MenuBackground");
        if (bg == null) { Debug.LogError("[MenuBackgroundBuilder] MenuBackground introuvable sous le panneau Accueil."); return; }

        Sprite[] sprites = new Sprite[3];
        for (int i = 0; i < 3; i++) sprites[i] = ImportSprite(Dir + Files[i]);
        Shader shader = Shader.Find("Aether/UI/WindSway");
        if (shader == null) { Debug.LogError("[MenuBackgroundBuilder] Shader Aether/UI/WindSway introuvable (script encore en compilation ?)."); return; }

        // conteneur : plein cadre, l'ancienne image est retirée
        RectTransform root = (RectTransform)bg;
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
        Image old = bg.GetComponent<Image>();
        if (old != null) Object.DestroyImmediate(old);
        MenuBackgroundController prevCtrl = bg.GetComponent<MenuBackgroundController>();
        if (prevCtrl != null) Object.DestroyImmediate(prevCtrl);
        for (int i = bg.childCount - 1; i >= 0; i--) Object.DestroyImmediate(bg.GetChild(i).gameObject);
        if (bg.GetComponent<RectMask2D>() == null) bg.gameObject.AddComponent<RectMask2D>();
        bg.SetAsFirstSibling();

        MenuBackgroundController ctrl = bg.gameObject.AddComponent<MenuBackgroundController>();

        Image[] layers = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            GameObject go = new GameObject(Names[i], typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(bg, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1920f, 1280f);
            Image img = go.GetComponent<Image>();
            img.sprite = sprites[i];
            img.raycastTarget = false;                       // le fond ne doit jamais bloquer un clic
            img.color = new Color(1, 1, 1, i == 0 ? 1f : 0f);
            layers[i] = img;
        }

        GameObject fxGo = new GameObject("Fx", typeof(RectTransform));
        fxGo.transform.SetParent(bg, false);
        RectTransform fx = (RectTransform)fxGo.transform;
        fx.anchorMin = Vector2.zero; fx.anchorMax = Vector2.one; fx.offsetMin = fx.offsetMax = Vector2.zero;
        MenuLeavesFX leaves = fxGo.AddComponent<MenuLeavesFX>();

        SerializedObject cso = new SerializedObject(ctrl);
        SerializedProperty lp = cso.FindProperty("_layers");
        lp.arraySize = 3;
        for (int i = 0; i < 3; i++) lp.GetArrayElementAtIndex(i).objectReferenceValue = layers[i];
        cso.FindProperty("_windShader").objectReferenceValue = shader;
        cso.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject lso = new SerializedObject(leaves);
        lso.FindProperty("_background").objectReferenceValue = ctrl;
        lso.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(menuPanel.scene);
        Debug.Log("[MenuBackgroundBuilder] Fond du menu reconstruit (scène marquée modifiée : à enregistrer).");
    }

    private static Sprite ImportSprite(string path)
    {
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null)
        {
            bool dirty = false;
            if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; dirty = true; }
            if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; dirty = true; }
            if (ti.mipmapEnabled) { ti.mipmapEnabled = false; dirty = true; }
            if (ti.maxTextureSize < 2048) { ti.maxTextureSize = 2048; dirty = true; }
            if (ti.textureCompression != TextureImporterCompression.CompressedHQ) { ti.textureCompression = TextureImporterCompression.CompressedHQ; dirty = true; }
            if (ti.filterMode != FilterMode.Bilinear) { ti.filterMode = FilterMode.Bilinear; dirty = true; }
            if (ti.wrapMode != TextureWrapMode.Clamp) { ti.wrapMode = TextureWrapMode.Clamp; dirty = true; }
            if (dirty) ti.SaveAndReimport();
        }
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) Debug.LogError("[MenuBackgroundBuilder] Sprite introuvable : " + path);
        return s;
    }
}
