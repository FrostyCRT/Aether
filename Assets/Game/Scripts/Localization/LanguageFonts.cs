using System.Collections.Generic;
using TMPro;
using UnityEngine;

// AJOUTÉ (2026-10-15) - la police asiatique de la langue courante passe en tête des polices de repli de TextMeshPro.
//
// Chinois, japonais et coréen ont chacun leur police de repli (Noto Sans SC / JP / KR, voir LanguageFontsSetup) : quand
// Bangers n'a pas un caractère, TMP le cherche dans ces polices DANS L'ORDRE. Or beaucoup d'idéogrammes existent à la
// fois en chinois et en japonais, avec un dessin différent dans chaque pays : en japonais, le japonais doit passer avant
// le chinois (sinon un joueur japonais lit des caractères « à la chinoise »), et inversement.
// Au démarrage et à chaque changement de langue, l'ordre est mis à jour puis tous les textes sont redessinés. Les noms
// des langues (onglet Langue) restent lisibles dans toutes les langues : les trois polices restent dans la liste.
// En Éditeur, l'ordre d'origine de TMP Settings est remis en sortie de Play (l'asset n'est jamais modifié sur le disque).
public static class LanguageFonts
{
    private static List<TMP_FontAsset> _original;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        Loc.Changed -= OnLanguageChanged;
        Loc.Changed += OnLanguageChanged;
        Application.quitting -= Restore;
        Application.quitting += Restore;
        Apply();
    }

    private static void OnLanguageChanged()
    {
        Apply();
        // un texte dont la chaîne ne change pas (nom de langue, chiffres...) doit aussi être redessiné avec le nouvel ordre
        foreach (TMP_Text t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            t.SetAllDirty();
    }

    /// <summary>Préfixe du nom de l'atlas de la police propre à chaque langue asiatique (null : aucune).</summary>
    private static string FontPrefix(GameLanguage lang)
    {
        switch (lang)
        {
            case GameLanguage.ChineseSimplified: return "NotoSansSC";
            case GameLanguage.Japanese: return "NotoSansJP";
            case GameLanguage.Korean: return "NotoSansKR";
            default: return null;
        }
    }

    private static void Apply()
    {
        List<TMP_FontAsset> list = TMP_Settings.fallbackFontAssets;
        if (list == null || list.Count < 2) return;
        if (_original == null) _original = new List<TMP_FontAsset>(list);

        // ordre d'origine (chinois, japonais, coréen), puis la police de la langue courante remontée en tête
        list.Clear();
        list.AddRange(_original);
        string prefix = FontPrefix(Loc.Current);
        if (prefix == null) return;
        int index = list.FindIndex(f => f != null && f.name.StartsWith(prefix));
        if (index <= 0) return;
        TMP_FontAsset own = list[index];
        list.RemoveAt(index);
        list.Insert(0, own);
    }

    private static void Restore()
    {
        if (_original == null) return;
        List<TMP_FontAsset> list = TMP_Settings.fallbackFontAssets;
        list.Clear();
        list.AddRange(_original);
    }
}
