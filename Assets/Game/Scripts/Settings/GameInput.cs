using System;
using UnityEngine;

// AJOUTE (2026-09-19) - lecture des commandes du joueur À TRAVERS la configuration des touches (InputBindings).
// Le code du jeu n'appelle plus Input.GetKey(KeyCode.X) : il demande GameInput.Down(GameAction.Dash), etc.
//
//   GameInput.Held(action)   touche maintenue (l'un ou l'autre des 2 emplacements)
//   GameInput.Down(action)   touche enfoncée CETTE image
//   GameInput.Move()         vecteur de déplacement (clavier + stick de manette), norme <= 1
//
// Stick de manette : axes "PadMoveX/PadMoveY" (créés par Aether > Setup Input Axes ; à défaut, le stick est
// simplement ignoré, jamais d'exception). La zone morte est un réglage du joueur (GameSettings.PadDeadZone).
public static class GameInput
{
    private static bool _padAxesMissing;

    // AJOUTÉ (2026-10-03) - même esprit que DebugCheats.cs (F6/F8) : permet à un script de test (ex. un "bot" de
    // playtest automatisé pour juger l'équilibrage) de piloter le déplacement sans clavier, sans toucher à
    // PlayerController. Ignoré par défaut (null) ; jamais actif en dehors de l'Éditeur/d'un build de développement.
    public static Vector2? DebugMoveOverride = null;

    public static bool Held(GameAction action)
    {
        for (int s = 0; s < InputBindings.Slots; s++)
        {
            KeyCode k = InputBindings.Get(action, s);
            if (k != KeyCode.None && Input.GetKey(k)) return true;
        }
        return false;
    }

    public static bool Down(GameAction action)
    {
        for (int s = 0; s < InputBindings.Slots; s++)
        {
            KeyCode k = InputBindings.Get(action, s);
            if (k != KeyCode.None && Input.GetKeyDown(k)) return true;
        }
        return false;
    }

    public static Vector2 Move()
    {
        if (DebugMoveOverride.HasValue && (Application.isEditor || Debug.isDebugBuild))
            return DebugMoveOverride.Value;

        float x = (Held(GameAction.MoveRight) ? 1f : 0f) - (Held(GameAction.MoveLeft) ? 1f : 0f);
        float y = (Held(GameAction.MoveUp) ? 1f : 0f) - (Held(GameAction.MoveDown) ? 1f : 0f);
        Vector2 v = new Vector2(x, y);

        Vector2 pad = ReadPad();
        if (pad.sqrMagnitude > 0f) v += pad;

        return v.sqrMagnitude > 1f ? v.normalized : v;
    }

    private static Vector2 ReadPad()
    {
        if (_padAxesMissing) return Vector2.zero;
        try
        {
            Vector2 raw = new Vector2(Input.GetAxisRaw("PadMoveX"), Input.GetAxisRaw("PadMoveY"));
            float dz = GameSettings.GetFloat(GameSettings.PadDeadZone);
            float mag = raw.magnitude;
            if (mag <= dz) return Vector2.zero;
            return raw / mag * Mathf.Clamp01((mag - dz) / (1f - dz));    // zone morte radiale, sortie remise à l'échelle
        }
        catch (ArgumentException)
        {
            _padAxesMissing = true;      // axes non configurés : on n'insiste pas (une seule tentative)
            return Vector2.zero;
        }
    }

    // ---- capture d'une touche (page Commandes) ------------------------------------------------------------
    private static readonly KeyCode[] AllKeys = (KeyCode[])Enum.GetValues(typeof(KeyCode));

    public enum Capture { None, Key, Cancel, Clear }

    // À appeler chaque image pendant l'attente d'une touche. Échap = annuler, Retour arrière / Suppr = effacer.
    // Le clic gauche n'est jamais une touche assignable (c'est lui qui a ouvert la capture / sert à la fermer).
    public static Capture PollCapture(out KeyCode key)
    {
        key = KeyCode.None;
        if (!Input.anyKeyDown) return Capture.None;

        if (Input.GetKeyDown(KeyCode.Escape)) return Capture.Cancel;
        if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Delete)) return Capture.Clear;

        foreach (KeyCode k in AllKeys)
        {
            if (k == KeyCode.None || k == KeyCode.Mouse0) continue;
            if (!Input.GetKeyDown(k)) continue;
            key = k;
            return Capture.Key;
        }
        return Capture.None;
    }

    // ---- libellés (français, traduits par Loc) ---------------------------------------------------------------------------------
    public static string KeyLabel(KeyCode k)
    {
        switch (k)
        {
            case KeyCode.None: return "—";
            case KeyCode.LeftShift: return Loc.T("Maj gauche");
            case KeyCode.RightShift: return Loc.T("Maj droite");
            case KeyCode.LeftControl: return Loc.T("Ctrl gauche");
            case KeyCode.RightControl: return Loc.T("Ctrl droite");
            case KeyCode.LeftAlt: return Loc.T("Alt gauche");
            case KeyCode.RightAlt: return Loc.T("Alt droite");
            case KeyCode.LeftCommand: return Loc.T("Cmd gauche");
            case KeyCode.RightCommand: return Loc.T("Cmd droite");
            case KeyCode.Space: return Loc.T("Espace");
            case KeyCode.Return: return Loc.T("Entrée");
            case KeyCode.KeypadEnter: return Loc.T("Entrée (pavé)");
            case KeyCode.Escape: return Loc.T("Échap");
            case KeyCode.Backspace: return Loc.T("Retour arrière");
            case KeyCode.Delete: return Loc.T("Suppr");
            case KeyCode.Insert: return Loc.T("Inser");
            case KeyCode.Tab: return Loc.T("Tab");
            case KeyCode.CapsLock: return Loc.T("Verr. maj");
            case KeyCode.Home: return Loc.T("Début");
            case KeyCode.End: return Loc.T("Fin");
            case KeyCode.PageUp: return Loc.T("Page haut");
            case KeyCode.PageDown: return Loc.T("Page bas");
            case KeyCode.UpArrow: return Loc.T("Flèche haut");
            case KeyCode.DownArrow: return Loc.T("Flèche bas");
            case KeyCode.LeftArrow: return Loc.T("Flèche gauche");
            case KeyCode.RightArrow: return Loc.T("Flèche droite");
            case KeyCode.Comma: return Loc.T("Virgule");
            case KeyCode.Period: return Loc.T("Point");
            case KeyCode.Semicolon: return Loc.T("Point-virgule");
            case KeyCode.Colon: return Loc.T("Deux-points");
            case KeyCode.Slash: return Loc.T("Barre oblique");
            case KeyCode.Backslash: return Loc.T("Barre inverse");
            case KeyCode.Minus: return Loc.T("Moins");
            case KeyCode.Equals: return Loc.T("Égal");
            case KeyCode.Quote: return Loc.T("Apostrophe");
            case KeyCode.DoubleQuote: return Loc.T("Guillemet");
            case KeyCode.BackQuote: return Loc.T("Accent grave");
            case KeyCode.LeftBracket: return Loc.T("Crochet gauche");
            case KeyCode.RightBracket: return Loc.T("Crochet droit");
            case KeyCode.KeypadPlus: return Loc.T("Pavé +");
            case KeyCode.KeypadMinus: return Loc.T("Pavé −");
            case KeyCode.KeypadMultiply: return Loc.T("Pavé ×");
            case KeyCode.KeypadDivide: return Loc.T("Pavé ÷");
            case KeyCode.KeypadPeriod: return Loc.T("Pavé .");
            case KeyCode.Mouse0: return Loc.T("Clic gauche");
            case KeyCode.Mouse1: return Loc.T("Clic droit");
            case KeyCode.Mouse2: return Loc.T("Clic molette");
        }

        int v = (int)k;
        if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) return ((char)('0' + (v - (int)KeyCode.Alpha0))).ToString();
        if (k >= KeyCode.Keypad0 && k <= KeyCode.Keypad9) return Loc.F("Pavé {0}", v - (int)KeyCode.Keypad0);
        if (k >= KeyCode.Mouse3 && k <= KeyCode.Mouse6) return Loc.F("Souris {0}", v - (int)KeyCode.Mouse0 + 1);
        if (k >= KeyCode.JoystickButton0 && k <= KeyCode.JoystickButton19) return Loc.F("Manette {0}", v - (int)KeyCode.JoystickButton0);

        string name = k.ToString();
        if (name.StartsWith("Joystick")) return Loc.T("Manette");   // boutons d'une manette précise (Joystick2Button3...) : libellé court
        return name.Length == 1 ? name.ToUpperInvariant() : name;
    }

    // Libellé très court d'une touche, pour une pastille de l'interface (ex. l'icône du clone dans le HUD) :
    // une lettre / un chiffre tel quel ; « Maj », « Ctrl », « Alt », « Esp », « Tab », « ↵ », flèches, « M1 » (souris),
    // « B3 » (manette).
    public static string ShortKeyLabel(KeyCode k)
    {
        switch (k)
        {
            case KeyCode.None: return "";
            case KeyCode.LeftShift: case KeyCode.RightShift: return Loc.T("Maj");
            case KeyCode.LeftControl: case KeyCode.RightControl: return "Ctrl";
            case KeyCode.LeftAlt: case KeyCode.RightAlt: return "Alt";
            case KeyCode.LeftCommand: case KeyCode.RightCommand: return "Cmd";
            case KeyCode.Space: return Loc.T("Esp");
            case KeyCode.Return: case KeyCode.KeypadEnter: return "↵";
            case KeyCode.Escape: return Loc.T("Échap");
            case KeyCode.Backspace: return "⌫";
            case KeyCode.Tab: return "Tab";
            case KeyCode.UpArrow: return "↑";
            case KeyCode.DownArrow: return "↓";
            case KeyCode.LeftArrow: return "←";
            case KeyCode.RightArrow: return "→";
            case KeyCode.Mouse0: return "M1";
            case KeyCode.Mouse1: return "M2";
            case KeyCode.Mouse2: return "M3";
        }
        int v = (int)k;
        if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) return ((char)('0' + (v - (int)KeyCode.Alpha0))).ToString();
        if (k >= KeyCode.Keypad0 && k <= KeyCode.Keypad9) return "P" + (v - (int)KeyCode.Keypad0);
        if (k >= KeyCode.Mouse3 && k <= KeyCode.Mouse6) return "M" + (v - (int)KeyCode.Mouse0 + 1);
        if (k >= KeyCode.JoystickButton0 && k <= KeyCode.JoystickButton19) return "B" + (v - (int)KeyCode.JoystickButton0);
        string full = KeyLabel(k);
        return full.Length <= 4 ? full : full.Substring(0, 4);
    }

    // Touche à afficher pour une action : celle du 1er emplacement, sinon celle du 2e.
    public static string ShortBindingLabel(GameAction a)
    {
        KeyCode k0 = InputBindings.Get(a, 0), k1 = InputBindings.Get(a, 1);
        return ShortKeyLabel(k0 != KeyCode.None ? k0 : k1);
    }

    public static string ActionLabel(GameAction a)
    {
        switch (a)
        {
            case GameAction.MoveUp: return Loc.T("Avancer");
            case GameAction.MoveDown: return Loc.T("Reculer");
            case GameAction.MoveLeft: return Loc.T("Aller à gauche");
            case GameAction.MoveRight: return Loc.T("Aller à droite");
            case GameAction.Dash: return Loc.T("Dash");
            case GameAction.Ultimate: return Loc.T("Ultime");
            case GameAction.PhantomClone: return Loc.T("Clone spectral");
            case GameAction.Card1: return Loc.T("Carte n°1");
            case GameAction.Card2: return Loc.T("Carte n°2");
            case GameAction.Card3: return Loc.T("Carte n°3");
            case GameAction.OrbitShrink: return Loc.T("Orbites : rapprocher");
            case GameAction.OrbitGrow: return Loc.T("Orbites : éloigner");
            case GameAction.Pause: return Loc.T("Pause");
        }
        return a.ToString();
    }

    public static string ActionHint(GameAction a)
    {
        switch (a)
        {
            case GameAction.Dash: return Loc.T("Dash : déplacement rapide, invincible un court instant, absorbe les projectiles");
            case GameAction.Ultimate: return Loc.T("Déclenche l'ultime quand le cristal est chargé");
            case GameAction.PhantomClone: return Loc.T("Invoque le clone spectral. Nécessite la compétence correspondante");
            case GameAction.Card1: return Loc.T("Choisit la 1re carte à la montée de niveau");
            case GameAction.Card2: return Loc.T("Choisit la 2ème carte à la montée de niveau");
            case GameAction.Card3: return Loc.T("Choisit la 3ème carte à la montée de niveau");
            case GameAction.OrbitShrink: return Loc.T("Réduit le rayon des orbes de l'arme orbitale");
            case GameAction.OrbitGrow: return Loc.T("Augmente le rayon des orbes de l'arme orbitale");
            case GameAction.Pause: return Loc.T("Ouvre / ferme le menu pause");
        }
        return "";
    }

    // "F  /  Manette 2" : texte des touches d'une action (pour des indices d'interface).
    public static string BindingText(GameAction a)
    {
        KeyCode k0 = InputBindings.Get(a, 0), k1 = InputBindings.Get(a, 1);
        if (k0 != KeyCode.None && k1 != KeyCode.None) return KeyLabel(k0) + " / " + KeyLabel(k1);
        return KeyLabel(k0 != KeyCode.None ? k0 : k1);
    }
}
