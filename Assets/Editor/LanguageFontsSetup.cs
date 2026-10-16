using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// OUTIL D'ÉDITEUR (2026-10-15) - polices de repli CHINOISE, JAPONAISE et CORÉENNE (Bangers n'a ni idéogrammes, ni kana,
// ni hangeul).
//
// Sources : Assets/Game/Fonts/Lang/NotoSans<SC|JP|KR>-Black-Jeu.ttf, réduites aux caractères du jeu par
// Tools/build_language_fonts.py, qui écrit aussi la liste de ces caractères (même nom, .chars.txt) : à relancer après
// toute modification de tr_zh / tr_ja / tr_ko.
// Ce menu en fait des atlas TMP STATIQUES (rien à calculer pendant une partie) et les inscrit comme polices de repli
// globales de TextMeshPro (TMP Settings), dans l'ordre chinois, japonais, coréen ; en jeu, LanguageFonts met celle de la
// langue courante en tête (un idéogramme commun au chinois et au japonais n'a pas le même dessin).
// Le cyrillique, lui, est DANS Bangers (Tools/patch_bangers_cyrillic.py + Aether > Rebuild Bangers Digits).
// Proportions : même rapport marge / taille d'échantillonnage que Bangers (9 / 90) pour que contours et ombres
// des matériaux aient la même épaisseur sur ces caractères.
// Menu : Aether > Rebuild Language Fonts (idempotent, garde le GUID des assets).
// Contrôle automatique à chaque compilation : un caractère d'une de ces langues absent de Bangers ET de l'atlas de sa
// langue -> erreur console.
public static class LanguageFontsSetup
{
    private struct Spec
    {
        public string Code, Name, Source;
        public Dictionary<string, string> Table;
        public string FontPath => "Assets/Game/Fonts/Lang/" + Source + "-Jeu.ttf";
        public string CharsPath => "Assets/Game/Fonts/Lang/" + Source + "-Jeu.chars.txt";
        public string AssetPath => "Assets/Game/Fonts/Lang/" + Source + " SDF.asset";
    }

    private static Spec[] Specs => new[]
    {
        new Spec { Code = "zh", Name = "简体中文", Source = "NotoSansSC-Black", Table = LocTableZH.Table },
        new Spec { Code = "ja", Name = "日本語", Source = "NotoSansJP-Black", Table = LocTableJA.Table },
        new Spec { Code = "ko", Name = "한국어", Source = "NotoSansKR-Black", Table = LocTableKO.Table },
    };

    private const string SettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
    private const string BangersPath = "Assets/Game/Fonts/Bangers-Regular-Chiffres.ttf";
    private const int SamplingSize = 60;
    private const int Padding = 6;

    [MenuItem("Aether/Rebuild Language Fonts")]
    public static void Rebuild()
    {
        // le moteur de polices garde en cache la version précédente des fichiers (régénérés par build_language_fonts.py) :
        // sans ce vidage, aucun glyphe n'est trouvé après une mise à jour de la police
        FontEngine.UnloadAllFontFaces();
        List<TMP_FontAsset> built = new List<TMP_FontAsset>();
        foreach (Spec spec in Specs)
        {
            TMP_FontAsset fa = Build(spec);
            if (fa == null) return;
            built.Add(fa);
        }

        // polices de repli globales de TextMeshPro, dans l'ordre des langues (LanguageFonts réordonne en jeu)
        Object settings = AssetDatabase.LoadAssetAtPath<Object>(SettingsPath);
        SerializedObject sso = new SerializedObject(settings);
        SerializedProperty list = sso.FindProperty("m_fallbackFontAssets");
        List<Object> others = new List<Object>();
        for (int i = 0; i < list.arraySize; i++)
        {
            Object o = list.GetArrayElementAtIndex(i).objectReferenceValue;
            if (o != null && !built.Contains(o as TMP_FontAsset)) others.Add(o);
        }
        list.arraySize = built.Count + others.Count;
        for (int i = 0; i < built.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = built[i];
        for (int i = 0; i < others.Count; i++) list.GetArrayElementAtIndex(built.Count + i).objectReferenceValue = others[i];
        sso.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    private static TMP_FontAsset Build(Spec spec)
    {
        Font font = AssetDatabase.LoadAssetAtPath<Font>(spec.FontPath);
        TextAsset charsFile = AssetDatabase.LoadAssetAtPath<TextAsset>(spec.CharsPath);
        if (font == null || charsFile == null)
        {
            Debug.LogError("[LanguageFonts] " + spec.FontPath + " ou sa liste de caractères introuvable : lancer python Tools/build_language_fonts.py.");
            return null;
        }
        string chars = charsFile.text;

        TMP_FontAsset fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(spec.AssetPath);
        bool created = fa == null;
        // atlas le plus petit qui contient tout (2048 suffit pour ~780 caractères à cette taille)
        int[,] sizes = { { 2048, 2048 }, { 4096, 2048 }, { 4096, 4096 } };
        string missing = chars;
        for (int i = 0; i < sizes.GetLength(0) && missing.Length > 0; i++)
        {
            int w = sizes[i, 0], h = sizes[i, 1];
            if (created)
            {
                if (fa != null) Object.DestroyImmediate(fa, true);
                fa = TMP_FontAsset.CreateFontAsset(font, SamplingSize, Padding, GlyphRenderMode.SDFAA, w, h, AtlasPopulationMode.Dynamic, false);
                fa.name = spec.Source + " SDF";
            }
            else
            {
                SerializedObject so = new SerializedObject(fa);
                so.FindProperty("m_SourceFontFile").objectReferenceValue = font;
                so.FindProperty("m_AtlasPopulationMode").intValue = (int)AtlasPopulationMode.Dynamic;
                so.FindProperty("m_AtlasWidth").intValue = w;
                so.FindProperty("m_AtlasHeight").intValue = h;
                so.FindProperty("m_AtlasPadding").intValue = Padding;
                so.ApplyModifiedPropertiesWithoutUndo();
                fa.ClearFontAssetData(true);
            }
            fa.TryAddCharacters(chars, out missing);
            if (missing.Length > 0) Debug.Log($"[LanguageFonts] {spec.Code} : atlas {w}x{h} trop petit ({missing.Length} caractères ne tiennent pas), essai plus grand.");
        }
        if (missing.Length > 0) { Debug.LogError("[LanguageFonts] " + spec.Code + " : caractères impossibles à ajouter : " + missing); return null; }

        fa.atlasPopulationMode = AtlasPopulationMode.Static;   // atlas figé : aucun calcul de glyphe pendant le jeu
        if (created)
        {
            AssetDatabase.CreateAsset(fa, spec.AssetPath);
            fa.material.name = fa.name + " Material";
            fa.atlasTexture.name = fa.name + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            AssetDatabase.AddObjectToAsset(fa.atlasTexture, fa);
        }
        EditorUtility.SetDirty(fa);
        Debug.Log($"[LanguageFonts] OK {spec.Code} : {chars.Length} caractères dans {spec.AssetPath} (atlas {fa.atlasWidth}x{fa.atlasHeight}, statique).");
        return fa;
    }

    /// <summary>Caractères d'une langue absents à la fois de Bangers et de l'atlas de cette langue (vide = tout va bien).</summary>
    public static string MissingCharacters(string code)
    {
        foreach (Spec spec in Specs)
        {
            if (spec.Code != code) continue;
            TMP_FontAsset fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(spec.AssetPath);
            Font bangers = AssetDatabase.LoadAssetAtPath<Font>(BangersPath);
            if (fa == null || bangers == null || FontEngine.LoadFontFace(bangers) != FontEngineError.Success) return "?";
            SortedSet<char> miss = new SortedSet<char>();
            List<string> texts = new List<string>(spec.Table.Values) { spec.Name };
            foreach (string v in texts)
            {
                bool tag = false;
                foreach (char c in v)
                {
                    if (c == '<') { tag = true; continue; }
                    if (tag) { if (c == '>') tag = false; continue; }
                    if (c == '\n' || c == ' ' || fa.characterLookupTable.ContainsKey(c)) continue;
                    if (FontEngine.TryGetGlyphIndex(c, out uint g) && g != 0) continue;   // dessiné par Bangers
                    miss.Add(c);
                }
            }
            StringBuilder sb = new StringBuilder();
            foreach (char c in miss) sb.Append(c);
            return sb.ToString();
        }
        return "?";
    }

    [InitializeOnLoadMethod]
    private static void CheckCoverage()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (Spec spec in Specs)
            {
                if (!File.Exists(spec.AssetPath)) continue;
                string miss = MissingCharacters(spec.Code);
                if (miss.Length > 0)
                    Debug.LogError("[LanguageFonts] " + spec.Code + " : " + miss.Length + " caractère(s) absents de la police (" + miss +
                                   ") : lancer python Tools/build_language_fonts.py puis Aether > Rebuild Language Fonts.");
            }
        };
    }
}
