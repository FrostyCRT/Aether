using UnityEditor;
using UnityEngine;

// OUTIL D'ÉDITEUR (2026-09-19) - crée les axes "PadMoveX" / "PadMoveY" dans ProjectSettings/InputManager.asset.
//
// Pourquoi : l'axe "Horizontal/Vertical" du projet mélange clavier (ZQSD + flèches) ET stick de manette ; avec la
// configuration des touches (page Paramètres > Commandes) le clavier est désormais lu à part (GameInput), il faut
// donc un axe de manette SEUL, sans touches. Idempotent : ne recrée pas un axe qui existe déjà.
// Menu : Aether > Setup Input Axes (déjà exécuté ; à relancer seulement si InputManager.asset est remplacé).
public static class InputAxesSetup
{
    [MenuItem("Aether/Setup Input Axes")]
    public static void Setup()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset");
        if (assets == null || assets.Length == 0) { Debug.LogError("[InputAxesSetup] InputManager.asset introuvable."); return; }

        SerializedObject so = new SerializedObject(assets[0]);
        SerializedProperty axes = so.FindProperty("m_Axes");

        bool changed = false;
        changed |= EnsureAxis(axes, "PadMoveX", 0, false);
        changed |= EnsureAxis(axes, "PadMoveY", 1, true);

        if (changed)
        {
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("[InputAxesSetup] Axes PadMoveX / PadMoveY ajoutés à l'Input Manager.");
        }
        else
        {
            Debug.Log("[InputAxesSetup] Axes déjà présents : rien à faire.");
        }
    }

    private static bool EnsureAxis(SerializedProperty axes, string name, int joystickAxis, bool invert)
    {
        for (int i = 0; i < axes.arraySize; i++)
            if (axes.GetArrayElementAtIndex(i).FindPropertyRelative("m_Name").stringValue == name) return false;

        axes.arraySize++;
        SerializedProperty a = axes.GetArrayElementAtIndex(axes.arraySize - 1);
        a.FindPropertyRelative("m_Name").stringValue = name;
        a.FindPropertyRelative("descriptiveName").stringValue = "";
        a.FindPropertyRelative("descriptiveNegativeName").stringValue = "";
        a.FindPropertyRelative("negativeButton").stringValue = "";
        a.FindPropertyRelative("positiveButton").stringValue = "";
        a.FindPropertyRelative("altNegativeButton").stringValue = "";
        a.FindPropertyRelative("altPositiveButton").stringValue = "";
        a.FindPropertyRelative("gravity").floatValue = 0f;
        a.FindPropertyRelative("dead").floatValue = 0f;             // la zone morte est un réglage du joueur (GameInput)
        a.FindPropertyRelative("sensitivity").floatValue = 1f;
        a.FindPropertyRelative("snap").boolValue = false;
        a.FindPropertyRelative("invert").boolValue = invert;
        a.FindPropertyRelative("type").intValue = 2;                 // 2 = axe de manette
        a.FindPropertyRelative("axis").intValue = joystickAxis;      // 0 = X, 1 = Y
        a.FindPropertyRelative("joyNum").intValue = 0;               // toutes les manettes
        return true;
    }
}
