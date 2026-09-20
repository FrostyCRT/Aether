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

    // ---- libellés (français) ---------------------------------------------------------------------------------
    public static string KeyLabel(KeyCode k)
    {
        switch (k)
        {
            case KeyCode.None: return "—";
            case KeyCode.LeftShift: return "Maj gauche";
            case KeyCode.RightShift: return "Maj droite";
            case KeyCode.LeftControl: return "Ctrl gauche";
            case KeyCode.RightControl: return "Ctrl droite";
            case KeyCode.LeftAlt: return "Alt gauche";
            case KeyCode.RightAlt: return "Alt droite";
            case KeyCode.LeftCommand: return "Cmd gauche";
            case KeyCode.RightCommand: return "Cmd droite";
            case KeyCode.Space: return "Espace";
            case KeyCode.Return: return "Entrée";
            case KeyCode.KeypadEnter: return "Entrée (pavé)";
            case KeyCode.Escape: return "Échap";
            case KeyCode.Backspace: return "Retour arrière";
            case KeyCode.Delete: return "Suppr";
            case KeyCode.Insert: return "Inser";
            case KeyCode.Tab: return "Tab";
            case KeyCode.CapsLock: return "Verr. maj";
            case KeyCode.Home: return "Début";
            case KeyCode.End: return "Fin";
            case KeyCode.PageUp: return "Page haut";
            case KeyCode.PageDown: return "Page bas";
            case KeyCode.UpArrow: return "Flèche haut";
            case KeyCode.DownArrow: return "Flèche bas";
            case KeyCode.LeftArrow: return "Flèche gauche";
            case KeyCode.RightArrow: return "Flèche droite";
            case KeyCode.Comma: return "Virgule";
            case KeyCode.Period: return "Point";
            case KeyCode.Semicolon: return "Point-virgule";
            case KeyCode.Colon: return "Deux-points";
            case KeyCode.Slash: return "Barre oblique";
            case KeyCode.Backslash: return "Barre inverse";
            case KeyCode.Minus: return "Moins";
            case KeyCode.Equals: return "Égal";
            case KeyCode.Quote: return "Apostrophe";
            case KeyCode.DoubleQuote: return "Guillemet";
            case KeyCode.BackQuote: return "Accent grave";
            case KeyCode.LeftBracket: return "Crochet gauche";
            case KeyCode.RightBracket: return "Crochet droit";
            case KeyCode.KeypadPlus: return "Pavé +";
            case KeyCode.KeypadMinus: return "Pavé −";
            case KeyCode.KeypadMultiply: return "Pavé ×";
            case KeyCode.KeypadDivide: return "Pavé ÷";
            case KeyCode.KeypadPeriod: return "Pavé .";
            case KeyCode.Mouse0: return "Clic gauche";
            case KeyCode.Mouse1: return "Clic droit";
            case KeyCode.Mouse2: return "Clic molette";
        }

        int v = (int)k;
        if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) return ((char)('0' + (v - (int)KeyCode.Alpha0))).ToString();
        if (k >= KeyCode.Keypad0 && k <= KeyCode.Keypad9) return "Pavé " + (v - (int)KeyCode.Keypad0);
        if (k >= KeyCode.Mouse3 && k <= KeyCode.Mouse6) return "Souris " + (v - (int)KeyCode.Mouse0 + 1);
        if (k >= KeyCode.JoystickButton0 && k <= KeyCode.JoystickButton19) return "Manette " + (v - (int)KeyCode.JoystickButton0);

        string name = k.ToString();
        if (name.StartsWith("Joystick")) return "Manette";   // boutons d'une manette précise (Joystick2Button3...) : libellé court
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
            case KeyCode.LeftShift: case KeyCode.RightShift: return "Maj";
            case KeyCode.LeftControl: case KeyCode.RightControl: return "Ctrl";
            case KeyCode.LeftAlt: case KeyCode.RightAlt: return "Alt";
            case KeyCode.LeftCommand: case KeyCode.RightCommand: return "Cmd";
            case KeyCode.Space: return "Esp";
            case KeyCode.Return: case KeyCode.KeypadEnter: return "↵";
            case KeyCode.Escape: return "Échap";
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
            case GameAction.MoveUp: return "Avancer";
            case GameAction.MoveDown: return "Reculer";
            case GameAction.MoveLeft: return "Aller à gauche";
            case GameAction.MoveRight: return "Aller à droite";
            case GameAction.Dash: return "Dash";
            case GameAction.Ultimate: return "Ultime";
            case GameAction.PhantomClone: return "Clone spectral";
            case GameAction.Card1: return "Carte n°1";
            case GameAction.Card2: return "Carte n°2";
            case GameAction.Card3: return "Carte n°3";
            case GameAction.OrbitShrink: return "Orbites : rapprocher";
            case GameAction.OrbitGrow: return "Orbites : éloigner";
            case GameAction.Pause: return "Pause";
        }
        return a.ToString();
    }

    public static string ActionHint(GameAction a)
    {
        switch (a)
        {
            case GameAction.Dash: return "Esquive rapide : invincible un court instant, absorbe les projectiles.";
            case GameAction.Ultimate: return "Déclenche l'ultime quand le cristal est chargé.";
            case GameAction.PhantomClone: return "Invoque le clone spectral. Nécessite la compétence correspondante.";
            case GameAction.Card1: return "Choisit la 1re carte à la montée de niveau.";
            case GameAction.Card2: return "Choisit la 2e carte à la montée de niveau.";
            case GameAction.Card3: return "Choisit la 3e carte à la montée de niveau.";
            case GameAction.OrbitShrink: return "Réduit le rayon des orbes de l'arme orbitale.";
            case GameAction.OrbitGrow: return "Augmente le rayon des orbes de l'arme orbitale.";
            case GameAction.Pause: return "Ouvre / ferme le menu pause.";
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
