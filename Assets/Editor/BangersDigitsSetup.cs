using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

// OUTIL D'ÉDITEUR (2026-09-19) - chiffres lisibles SANS changer de police.
//
// Problème : dans Bangers, un 7 se lit comme un 1. Un premier essai (chiffres en LiberationSans en police
// de repli) réglait la confusion mais donnait des chiffres "lisses" à côté de lettres et de % en Bangers :
// jugé disgracieux. Ici on garde Bangers ENTIER et on ne corrige que les deux glyphes ambigus :
// le 1 reçoit un pied, le 7 une barre (voir Tools/patch_bangers_digits.py, qui produit
// Assets/Game/Fonts/Bangers-Regular-Chiffres.ttf). Le reste de la police est identique.
//
// Ce menu branche cette police source sur l'atlas TMP existant (Bangers-Regular SDF, Dynamic) et
// re-génère les chiffres 0-9 à partir d'elle. Toutes les lettres/atlas déjà générés sont conservés.
// Menu : Aether > Rebuild Bangers Digits (idempotent).
public static class BangersDigitsSetup
{
    private const string AssetPath = "Assets/Game/Fonts/Bangers-Regular SDF.asset";
    private const string TtfPath = "Assets/Game/Fonts/Bangers-Regular-Chiffres.ttf";

    [MenuItem("Aether/Rebuild Bangers Digits")]
    public static void Rebuild()
    {
        TMP_FontAsset fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
        Font ttf = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
        if (fa == null || ttf == null) { Debug.LogError("[BangersDigitsSetup] Bangers SDF ou " + TtfPath + " introuvable."); return; }

        // 1) police source = version corrigée
        SerializedObject so = new SerializedObject(fa);
        foreach (string prop in new[] { "m_SourceFontFile", "m_SourceFontFile_EditorRef" })
        {
            SerializedProperty p = so.FindProperty(prop);
            if (p != null && p.propertyType == SerializedPropertyType.ObjectReference) p.objectReferenceValue = ttf;
            else Debug.LogWarning("[BangersDigitsSetup] propriété absente : " + prop);
        }
        so.FindProperty("m_AtlasPopulationMode").intValue = (int)AtlasPopulationMode.Dynamic;
        so.ApplyModifiedPropertiesWithoutUndo();
        fa.fallbackFontAssetTable = new List<TMP_FontAsset>();

        // 2) retirer les anciens chiffres (caractères ET glyphes), puis les régénérer depuis la source corrigée
        HashSet<uint> oldGlyphs = new HashSet<uint>();
        foreach (TMP_Character c in fa.characterTable)
            if (c.unicode >= '0' && c.unicode <= '9') oldGlyphs.Add(c.glyphIndex);
        fa.characterTable.RemoveAll(c => c.unicode >= '0' && c.unicode <= '9');
        fa.glyphTable.RemoveAll(g => oldGlyphs.Contains(g.index));
        fa.ReadFontAssetDefinition();

        fa.TryAddCharacters("0123456789", out string missing);
        if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("[BangersDigitsSetup] chiffres non générés : " + missing);

        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string present = "";
        for (char c = '0'; c <= '9'; c++) present += fa.HasCharacter(c) ? c.ToString() : "_";
        fa.characterLookupTable.TryGetValue('1', out TMP_Character one);
        Debug.Log($"[BangersDigitsSetup] OK. Chiffres dans l'atlas : {present}. Avance du '1' = {(one != null ? one.glyph.metrics.horizontalAdvance : -1)} (attendu ~ 0,35 x taille avec le pied).");
    }
}
