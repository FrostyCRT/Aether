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
// Ce menu branche cette police source sur l'atlas TMP (Bangers-Regular SDF, Dynamic) et le régénère.
// MODIFIÉ (2026-10-15) - l'atlas est VIDÉ puis rempli à nouveau : l'ancienne version retirait des glyphes sans
// libérer leur place dans la texture, et l'atlas finissait plein après quelques passages (lettres non générées).
// Sont régénérés : tous les caractères déjà présents + ASCII, Latin-1, Latin étendu A, l'alphabet russe (cyrillique
// ajouté à la police par Tools/patch_bangers_cyrillic.py) et la ponctuation courante, AVEC le crénage de la police.
// Le reste est généré à la demande en jeu (mode Dynamic).
// Menu : Aether > Rebuild Bangers Digits (idempotent).
public static class BangersDigitsSetup
{
    private const string AssetPath = "Assets/Game/Fonts/Bangers-Regular SDF.asset";
    private const string TtfPath = "Assets/Game/Fonts/Bangers-Regular-Chiffres.ttf";
    private const string Russian = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя";
    private const string Punctuation = "—–…«»“”„‘’•·№×÷−";

    [MenuItem("Aether/Rebuild Bangers Digits")]
    public static void Rebuild()
    {
        TMP_FontAsset fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
        Font ttf = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
        if (fa == null || ttf == null) { Debug.LogError("[BangersDigitsSetup] Bangers SDF ou " + TtfPath + " introuvable."); return; }

        // le moteur de polices garde en cache l'ancienne version du fichier .ttf retouché : on le vide
        UnityEngine.TextCore.LowLevel.FontEngine.UnloadAllFontFaces();

        // 1) police source = version corrigée
        SerializedObject so = new SerializedObject(fa);
        so.FindProperty("m_SourceFontFile").objectReferenceValue = ttf;
        so.FindProperty("m_AtlasPopulationMode").intValue = (int)AtlasPopulationMode.Dynamic;
        so.ApplyModifiedPropertiesWithoutUndo();
        fa.fallbackFontAssetTable = new List<TMP_FontAsset>();

        // 2) caractères à régénérer : ceux déjà présents + les jeux de base
        SortedSet<uint> unicodes = new SortedSet<uint>();
        foreach (TMP_Character c in fa.characterTable) unicodes.Add(c.unicode);
        for (uint u = 0x20; u <= 0x7E; u++) unicodes.Add(u);
        for (uint u = 0xA0; u <= 0x17F; u++) unicodes.Add(u);
        foreach (char c in Russian + Punctuation) unicodes.Add(c);
        System.Text.StringBuilder toRebuild = new System.Text.StringBuilder();
        foreach (uint u in unicodes) toRebuild.Append(char.ConvertFromUtf32((int)u));

        // 3) atlas vidé (même taille) puis rempli, crénage compris
        fa.ClearFontAssetData(false);
        fa.TryAddCharacters(toRebuild.ToString(), out string missing, true);
        // caractères absents de Bangers (ex. Latin étendu rare) : normal, signalés seulement s'ils sont dans les jeux de base attendus
        System.Text.StringBuilder unexpected = new System.Text.StringBuilder();
        foreach (char c in missing ?? "") if (Russian.IndexOf(c) >= 0 || (c >= '0' && c <= '9') || "ÀÂÄÇÉÈÊËÎÏÔÖÙÛÜŸàâäçéèêëîïôöùûüÿ".IndexOf(c) >= 0) unexpected.Append(c);
        if (unexpected.Length > 0) Debug.LogWarning("[BangersDigitsSetup] caractères non générés : " + unexpected);

        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string present = "";
        for (char c = '0'; c <= '9'; c++) present += fa.HasCharacter(c) ? c.ToString() : "_";
        fa.characterLookupTable.TryGetValue('1', out TMP_Character one);
        Debug.Log($"[BangersDigitsSetup] OK. Chiffres dans l'atlas : {present}. Avance du '1' = {(one != null ? one.glyph.metrics.horizontalAdvance : -1)} (attendu ~ 0,35 x taille avec le pied).");
    }
}
