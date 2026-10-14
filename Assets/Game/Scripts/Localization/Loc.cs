using System;
using System.Collections.Generic;
using UnityEngine;

// AJOUTÉ (2026-10-14) - langue du jeu (Paramètres > Langue). Le français est la langue d'écriture du jeu ; traductions :
// anglais, allemand, espagnol, portugais (Brésil), italien, polonais (une table par langue : LocTableEN, LocTableDE...).
//
// Principe : le TEXTE FRANÇAIS EST LA CLÉ. Le code continue d'écrire ses textes en français, simplement enveloppés :
//   label.text = Loc.T("Reprendre");                         // texte fixe
//   label.text = Loc.F("Niveau {0}", level);                 // texte avec valeurs (string.Format)
// En français, T/F renvoient le texte tel quel (le jeu reste strictement identique) ; sinon la traduction vient de la
// table de la langue. Une clé absente : repli sur l'anglais (puis le français) ET signalement une seule fois dans la
// console (Éditeur / build de dev), pour ne jamais laisser passer un oubli silencieusement.
// La table ANGLAISE est la référence de la liste des clés : toute nouvelle clé s'y ajoute d'abord, puis dans les autres
// (contrôle : node Tools/loc/check_langs.js).
//
// Textes posés à la main dans les scènes / prefabs : composant LocalizedText (même table, même règle).
// Changement de langue EN DIRECT : événement Loc.Changed (les pages et le HUD se rafraîchissent d'eux-mêmes).
// MODIFIÉ (2026-10-14) - les valeurs de l'enum sont enregistrées dans les réglages : ne jamais les renuméroter.
public enum GameLanguage { French = 0, English = 1, German = 2, Spanish = 3, PortugueseBR = 4, Italian = 5, Polish = 6 }

public static class Loc
{
    public static event Action Changed;

    public const int LanguageCount = 7;

    private static bool _init;
    private static GameLanguage _lang;
    private static readonly HashSet<string> _reportedMissing = new HashSet<string>();

    public static GameLanguage Current { get { Init(); return _lang; } }
    public static bool IsEnglish => Current == GameLanguage.English;
    public static bool IsFrench => Current == GameLanguage.French;

    // Langue proposée au tout premier lancement : celle de Windows si le jeu la parle, l'anglais sinon.
    public static GameLanguage SystemDefault
    {
        get
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.French: return GameLanguage.French;
                case SystemLanguage.German: return GameLanguage.German;
                case SystemLanguage.Spanish: return GameLanguage.Spanish;
                case SystemLanguage.Portuguese: return GameLanguage.PortugueseBR;
                case SystemLanguage.Italian: return GameLanguage.Italian;
                case SystemLanguage.Polish: return GameLanguage.Polish;
                default: return GameLanguage.English;
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot() => Init();

    private static void Init()
    {
        if (_init) return;
        _init = true;
        _lang = ReadSetting();
        GameSettings.Changed += key =>
        {
            if (key != GameSettings.Language) return;
            GameLanguage now = ReadSetting();
            if (now == _lang) return;
            _lang = now;
            Changed?.Invoke();
        };
    }

    private static GameLanguage ReadSetting()
    {
        int v = GameSettings.GetInt(GameSettings.Language);
        return v >= 0 && v < LanguageCount ? (GameLanguage)v : GameLanguage.English;
    }

    private static Dictionary<string, string> TableFor(GameLanguage lang)
    {
        switch (lang)
        {
            case GameLanguage.English: return LocTableEN.Table;
            case GameLanguage.German: return LocTableDE.Table;
            case GameLanguage.Spanish: return LocTableES.Table;
            case GameLanguage.PortugueseBR: return LocTablePT.Table;
            case GameLanguage.Italian: return LocTableIT.Table;
            case GameLanguage.Polish: return LocTablePL.Table;
            default: return null;
        }
    }

    /// <summary>Texte (écrit en français) dans la langue courante.</summary>
    public static string T(string fr)
    {
        if (string.IsNullOrEmpty(fr)) return fr;
        GameLanguage lang = Current;
        if (lang == GameLanguage.French) return fr;
        if (TableFor(lang).TryGetValue(fr, out string tr)) return tr;
        if (!HasLetters(fr)) return fr;   // "+10%", "-0.3s", "12" : rien à traduire
        ReportMissing(fr, lang);
        return lang != GameLanguage.English && LocTableEN.Table.TryGetValue(fr, out string en) ? en : fr;
    }

    /// <summary>Texte à trous ({0}, {1}...) dans la langue courante, puis rempli (string.Format).</summary>
    public static string F(string frFormat, params object[] args) => string.Format(T(frFormat), args);

    /// <summary>Choix singulier / pluriel (les deux formes sont des clés françaises, avec {0} pour le nombre).</summary>
    public static string Plural(int n, string frOne, string frMany) => F(PluralKey(n, frOne, frMany), n);

    /// <summary>Mot seul accordé au nombre ("victoire" / "victoires"), déjà traduit.</summary>
    public static string PluralWord(int n, string frOne, string frMany) => T(PluralKey(n, frOne, frMany));

    // Règle de pluriel de la langue courante. Le polonais a une 3e forme (2-4, sauf 12-14 : "2 wygrane" mais
    // "5 wygranych") : clé française du pluriel suivie de "#few", présente seulement dans la table polonaise.
    private static string PluralKey(int n, string frOne, string frMany)
    {
        switch (Current)
        {
            case GameLanguage.French:
                return n > 1 ? frMany : frOne;
            case GameLanguage.Polish:
            {
                if (n == 1) return frOne;
                int d = Math.Abs(n) % 10, h = Math.Abs(n) % 100;
                bool few = d >= 2 && d <= 4 && (h < 12 || h > 14);
                return few && LocTablePL.Table.ContainsKey(frMany + "#few") ? frMany + "#few" : frMany;
            }
            default:
                return n == 1 ? frOne : frMany;
        }
    }

    /// <summary>Vrai si `text` est la traduction de `fr` dans l'une des langues (sert à LocalizedText).</summary>
    public static bool IsTranslationOf(string fr, string text)
    {
        if (string.IsNullOrEmpty(fr) || string.IsNullOrEmpty(text)) return false;
        for (int i = 1; i < LanguageCount; i++)
            if (TableFor((GameLanguage)i).TryGetValue(fr, out string tr) && tr == text) return true;
        return false;
    }

    /// <summary>Vrai si ce texte français a une traduction (sert à LocalizedText pour ignorer les textes de remplissage).</summary>
    public static bool Has(string fr) => !string.IsNullOrEmpty(fr) && LocTableEN.Table.ContainsKey(fr);

    private static bool HasLetters(string s)
    {
        int run = 0;
        foreach (char c in s)
        {
            run = char.IsLetter(c) ? run + 1 : 0;
            if (run >= 2) return true;
        }
        return false;
    }

    private static void ReportMissing(string fr, GameLanguage lang)
    {
        if (!(Application.isEditor || Debug.isDebugBuild)) return;
        if (!_reportedMissing.Add(lang + "|" + fr)) return;
        Debug.LogWarning("[Loc] Traduction manquante (" + lang + ") : \"" + fr.Replace("\n", "\\n") + "\"");
    }
}
