using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// AJOUTÉ (2026-10-15) - pas d'italique en chinois, japonais ni coréen.
// TextMeshPro fabrique l'italique en penchant les glyphes ; sur des idéogrammes, des kana ou du hangeul, ce faux
// italique ne se fait pas en typographie asiatique et nuit à la lecture. Au chargement de chaque scène, les textes en
// italique sont repérés (actifs ou non) ; en chinois, japonais et coréen ils passent en droit, dans les autres langues
// ils gardent le style posé dans la scène.
// Suit les changements de langue en direct. Les scènes elles-mêmes ne sont jamais modifiées.
public static class CjkTextStyle
{
    private static readonly List<TMP_Text> _italics = new List<TMP_Text>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Loc.Changed -= ApplyAll;
        Loc.Changed += ApplyAll;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _italics.RemoveAll(t => t == null);
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
                if ((t.fontStyle & FontStyles.Italic) != 0 && !_italics.Contains(t)) _italics.Add(t);
        ApplyAll();
    }

    private static void ApplyAll()
    {
        GameLanguage l = Loc.Current;
        bool upright = l == GameLanguage.ChineseSimplified || l == GameLanguage.Japanese || l == GameLanguage.Korean;
        _italics.RemoveAll(t => t == null);
        foreach (TMP_Text t in _italics)
            t.fontStyle = upright ? t.fontStyle & ~FontStyles.Italic : t.fontStyle | FontStyles.Italic;
    }
}
