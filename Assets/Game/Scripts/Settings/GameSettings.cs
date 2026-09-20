using System;
using System.Collections.Generic;
using UnityEngine;

// AJOUTE (2026-09-19) - magasin UNIQUE des paramètres du joueur (refonte de la page Paramètres).
//
// Avant : 5 clés PlayerPrefs éparpillées dans SettingsManager, lues par 2 ou 3 scripts. Désormais :
//  - UNE table de définitions (clé, type, défaut, bornes) : la page Paramètres, la remise à zéro et le code
//    du jeu lisent la même vérité ; un nouveau réglage = une ligne ici + une ligne dans SettingsSchema ;
//  - un événement Changed(clé) : SettingsApplier applique le réglage EN DIRECT (pas de bouton "Appliquer") ;
//  - persistance PlayerPrefs, écrite au plus une fois par image (Flush) : glisser un curseur ne déclenche pas
//    100 écritures disque ;
//  - les 5 clés historiques (settings_music, settings_sfx, settings_fullscreen, settings_shadows,
//    settings_autofire) gardent leur nom : les préférences déjà enregistrées par un joueur sont conservées.
//
// Les TOUCHES sont dans InputBindings (même principe, mais tables de KeyCode).
public static class GameSettings
{
    // ---- clés ------------------------------------------------------------------------------------
    // Audio
    public const string Master = "settings_master";
    public const string Music = "settings_music";                 // clé historique
    public const string Sfx = "settings_sfx";                     // clé historique
    public const string MuteInBackground = "settings_mute_bg";
    // Affichage
    public const string DisplayMode = "settings_displaymode";     // 0 plein écran, 1 fenêtré sans bordure, 2 fenêtré
    public const string Resolution = "settings_resolution";       // "1920x1080" ("" = celle du bureau)
    public const string VSync = "settings_vsync";
    public const string FpsCap = "settings_fpscap";               // index dans FpsCaps
    public const string Brightness = "settings_brightness";
    public const string ShowFps = "settings_showfps";
    // Graphismes
    public const string Quality = "settings_quality";             // index QualitySettings
    public const string Shadows = "settings_shadows";             // clé historique
    public const string MenuEffects = "settings_menufx";          // 0 aucun, 1 réduits, 2 complets
    // Jeu
    public const string AutoAim = "settings_autofire";            // clé historique ("visée automatique")
    public const string PauseOnFocusLoss = "settings_pause_focus";
    public const string DamageDealt = "settings_dmg_dealt";
    public const string DamageTaken = "settings_dmg_taken";
    public const string DamageSize = "settings_dmg_size";
    // Interface
    public const string HudScale = "settings_hud_scale";
    public const string HudOpacity = "settings_hud_opacity";
    public const string ShowTimer = "settings_show_timer";
    public const string ShowGold = "settings_show_gold";
    public const string ShowKills = "settings_show_kills";
    // Manette
    public const string PadDeadZone = "settings_pad_deadzone";

    // Limites d'images par seconde proposées (0 = illimité).
    public static readonly int[] FpsCaps = { 0, 30, 60, 90, 120, 144, 165, 240 };

    private enum Kind { Bool, Int, Float, String }

    private struct Def
    {
        public Kind kind;
        public float number;    // défaut numérique (bool : 0/1)
        public string text;     // défaut texte
        public float min, max;
    }

    private static readonly Dictionary<string, Def> Defs = new Dictionary<string, Def>
    {
        { Master,           F(1.00f, 0f, 1f) },
        { Music,            F(0.75f, 0f, 1f) },
        { Sfx,              F(0.75f, 0f, 1f) },
        { MuteInBackground, B(true) },

        { DisplayMode,      I(1, 0, 2) },
        { Resolution,       S("") },
        { VSync,            B(true) },
        { FpsCap,           I(0, 0, 7) },
        { Brightness,       F(1.00f, 0.50f, 1.20f) },
        { ShowFps,          B(false) },

        { Quality,          I(2, 0, 2) },
        { Shadows,          B(false) },
        { MenuEffects,      I(2, 0, 2) },

        { AutoAim,          B(true) },
        { PauseOnFocusLoss, B(true) },
        { DamageDealt,      B(true) },
        { DamageTaken,      B(true) },
        { DamageSize,       F(1.00f, 0.60f, 1.60f) },

        { HudScale,         F(1.00f, 0.80f, 1.20f) },
        { HudOpacity,       F(1.00f, 0.40f, 1.00f) },
        { ShowTimer,        B(true) },
        { ShowGold,         B(true) },
        { ShowKills,        B(true) },

        { PadDeadZone,      F(0.20f, 0.05f, 0.50f) },
    };

    private static Def F(float d, float min, float max) => new Def { kind = Kind.Float, number = d, min = min, max = max };
    private static Def I(int d, int min, int max) => new Def { kind = Kind.Int, number = d, min = min, max = max };
    private static Def B(bool d) => new Def { kind = Kind.Bool, number = d ? 1f : 0f, min = 0, max = 1 };
    private static Def S(string d) => new Def { kind = Kind.String, text = d };

    // ---- événements / écriture -----------------------------------------------------------------
    public static event Action<string> Changed;

    private static bool _dirty;
    private static bool _migrated;

    // Une clé historique change de sens : l'ancien "plein écran" (bool) devient un mode d'affichage à 3 valeurs.
    private static void MigrateOnce()
    {
        if (_migrated) return;
        _migrated = true;
        if (!PlayerPrefs.HasKey(DisplayMode) && PlayerPrefs.HasKey("settings_fullscreen"))
            PlayerPrefs.SetInt(DisplayMode, PlayerPrefs.GetInt("settings_fullscreen", 1) == 1 ? 1 : 2);
    }

    // ---- lecture ---------------------------------------------------------------------------------
    public static bool GetBool(string key)
    {
        MigrateOnce();
        return PlayerPrefs.GetInt(key, Defs[key].number > 0.5f ? 1 : 0) == 1;
    }

    public static int GetInt(string key)
    {
        MigrateOnce();
        Def d = Defs[key];
        return Mathf.Clamp(PlayerPrefs.GetInt(key, Mathf.RoundToInt(d.number)), Mathf.RoundToInt(d.min), Mathf.RoundToInt(d.max));
    }

    public static float GetFloat(string key)
    {
        MigrateOnce();
        Def d = Defs[key];
        return Mathf.Clamp(PlayerPrefs.GetFloat(key, d.number), d.min, d.max);
    }

    public static string GetString(string key)
    {
        MigrateOnce();
        return PlayerPrefs.GetString(key, Defs[key].text ?? "");
    }

    // ---- écriture --------------------------------------------------------------------------------
    public static void SetBool(string key, bool value)
    {
        MigrateOnce();
        if (PlayerPrefs.HasKey(key) && GetBool(key) == value) return;
        PlayerPrefs.SetInt(key, value ? 1 : 0);
        Touched(key);
    }

    public static void SetInt(string key, int value)
    {
        MigrateOnce();
        Def d = Defs[key];
        value = Mathf.Clamp(value, Mathf.RoundToInt(d.min), Mathf.RoundToInt(d.max));
        if (PlayerPrefs.HasKey(key) && GetInt(key) == value) return;
        PlayerPrefs.SetInt(key, value);
        Touched(key);
    }

    public static void SetFloat(string key, float value)
    {
        MigrateOnce();
        Def d = Defs[key];
        value = Mathf.Clamp(value, d.min, d.max);
        if (PlayerPrefs.HasKey(key) && Mathf.Approximately(GetFloat(key), value)) return;
        PlayerPrefs.SetFloat(key, value);
        Touched(key);
    }

    public static void SetString(string key, string value)
    {
        MigrateOnce();
        value = value ?? "";
        if (PlayerPrefs.HasKey(key) && GetString(key) == value) return;
        PlayerPrefs.SetString(key, value);
        Touched(key);
    }

    private static void Touched(string key)
    {
        _dirty = true;
        Changed?.Invoke(key);
    }

    // Écrit sur le disque si quelque chose a changé (appelé par la page à sa fermeture et par SettingsApplier
    // à chaque image / à la fermeture du jeu : pas d'écriture pendant qu'on glisse un curseur).
    public static void Flush()
    {
        if (!_dirty) return;
        _dirty = false;
        PlayerPrefs.Save();
    }

    // ---- défauts / remise à zéro -------------------------------------------------------------------
    public static bool IsDefault(string key)
    {
        MigrateOnce();
        Def d = Defs[key];
        switch (d.kind)
        {
            case Kind.Bool: return GetBool(key) == (d.number > 0.5f);
            case Kind.Int: return GetInt(key) == Mathf.RoundToInt(d.number);
            case Kind.Float: return Mathf.Approximately(GetFloat(key), d.number);
            default: return GetString(key) == (d.text ?? "");
        }
    }

    public static void Reset(string key)
    {
        MigrateOnce();
        Def d = Defs[key];
        switch (d.kind)
        {
            case Kind.Bool: SetBool(key, d.number > 0.5f); break;
            case Kind.Int: SetInt(key, Mathf.RoundToInt(d.number)); break;
            case Kind.Float: SetFloat(key, d.number); break;
            default: SetString(key, d.text ?? ""); break;
        }
        // une clé remise à sa valeur par défaut doit exister pour que IsDefault/Set fonctionnent sans surprise
    }

    public static void Reset(IEnumerable<string> keys)
    {
        foreach (string k in keys) Reset(k);
    }

    public static void ResetAll()
    {
        foreach (string k in new List<string>(Defs.Keys)) Reset(k);
        InputBindings.ResetAll();
    }

    // ---- raccourcis lus par le code du jeu (garde les anciens noms utiles de SettingsManager) ----------
    public static bool AutoAimEnabled => GetBool(AutoAim);
    public static bool ShadowsEnabled => GetBool(Shadows);

    // Intensité des effets d'ambiance des menus : 0 = aucun, 0,5 = réduits, 1 = complets (Graphismes > Effets d'ambiance).
    public static float MenuEffectsFactor
    {
        get
        {
            int v = GetInt(MenuEffects);
            return v <= 0 ? 0f : (v == 1 ? 0.5f : 1f);
        }
    }
}
