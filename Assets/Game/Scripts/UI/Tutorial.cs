using System;
using UnityEngine;

// AJOUTÉ (2026-11-16, demande utilisateur : « le tutoriel, ce qu'il se passe au lancement du jeu pour la première fois [...]
// parfait, et complet dans ses instructions, pour comprendre tout le jeu ») - état du didacticiel, enregistré dans la
// sauvegarde (SaveData.tutorial*). Deux parties :
//  - la VISITE DU MENU (MenuTutorial) : langue, principe du jeu, choix du héros, Accueil, Compétences, Réputation,
//    Paramètres, puis lancement de la 1re partie. Au tout début, l'Accueil n'a PAS de personnage (décor vide, voir
//    MenuBackgroundController) : Aether apparaît en fondu quand le joueur le sélectionne.
//  - les CONSEILS DE PARTIE (GameTutorial) : déclenchés par ce que fait le joueur, chacun une seule fois (masque de bits).
// Un joueur qui a déjà joué (parties, or, niveaux d'arbre) ne voit jamais le didacticiel : migration au 1er accès. Il se
// rejoue depuis Paramètres > « Revoir le didacticiel » (Replay).
public static class Tutorial
{
    // Un bit par conseil de partie (ne jamais renuméroter : la sauvegarde y fait référence).
    public enum Hint
    {
        Move = 0, HudTour = 1, Gems = 2, LevelUp = 3, Dash = 4, Ultimate = 5, Clone = 6, Boss = 7, Chest = 8,
        Pause = 9, Orbit = 10, Fusion = 11, AfterFirstRun = 12, SecondUltimate = 13
    }

    // Pages du menu ayant leur propre visite guidée, lancée à la 1re ouverture de la page (ne jamais renuméroter).
    public enum Page { Characters = 0, Home = 1, Skills = 2, Reputation = 3 }

    public static event Action Changed;

    public static bool PageSeen(Page p)
    {
        EnsureMigrated();
        SaveData d = Data;
        return d == null || (d.tutorialPages & (1 << (int)p)) != 0;
    }

    public static void MarkPageSeen(Page p)
    {
        SaveData d = Data;
        if (d == null) return;
        int bit = 1 << (int)p;
        if ((d.tutorialPages & bit) != 0) return;
        d.tutorialPages |= bit;
        Save();
    }

    // Visite du menu en cours : le décor reste vide tant que le héros n'est pas choisi.
    public static bool MenuTourRunning { get; private set; }

    private static SaveData Data => MetaProgressionManager.Instance != null ? MetaProgressionManager.Instance.Data : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        MenuTourRunning = false;
        PracticeRequested = false;
        Changed = null;
    }

    /// <summary>Première fois qu'on lit l'état : un joueur qui a déjà de la progression n'a pas besoin du didacticiel.</summary>
    public static void EnsureMigrated()
    {
        SaveData d = Data;
        if (d == null || d.tutorialInit) return;
        d.tutorialInit = true;
        bool veteran = d.totalRuns > 0 || d.totalGold > 0 || d.totalEclats > 0 || d.kaelUnlocked || d.lyraUnlocked
                       || d.concentrationLevel > 0 || d.cadenceLevel > 0 || d.vitalityLevel > 0 || d.dashLevel > 0;
        if (veteran)
        {
            d.tutorialMenuDone = true;
            d.tutorialHeroChosen = true;
            d.tutorialHints = -1;   // tous les bits : aucun conseil
            d.tutorialPages = -1;
        }
        Save();
    }

    /// <summary>La visite du menu doit-elle se lancer (1er lancement, ou après « Revoir le didacticiel ») ?</summary>
    public static bool ShouldRunMenuTour
    {
        get { EnsureMigrated(); SaveData d = Data; return d != null && !d.tutorialMenuDone; }
    }

    /// <summary>Vrai tant que le joueur n'a pas encore choisi son héros dans la visite : l'Accueil n'a pas de personnage.</summary>
    public static bool NoHeroYet
    {
        get { SaveData d = Data; return MenuTourRunning && d != null && !d.tutorialHeroChosen; }
    }

    public static bool HeroChosen
    {
        get { SaveData d = Data; return d == null || d.tutorialHeroChosen; }
    }

    public static void BeginMenuTour()
    {
        MenuTourRunning = true;
        Changed?.Invoke();
    }

    public static void NotifyHeroChosen()
    {
        SaveData d = Data;
        if (d == null || d.tutorialHeroChosen) return;
        d.tutorialHeroChosen = true;
        Save();
        Changed?.Invoke();
    }

    public static void FinishMenuTour()
    {
        SaveData d = Data;
        MenuTourRunning = false;
        if (d != null)
        {
            d.tutorialMenuDone = true;
            d.tutorialHeroChosen = true;
            Save();
        }
        Changed?.Invoke();
    }

    // ---- partie d'entraînement ------------------------------------------------------------------------------------
    // Les conseils de partie n'apparaissent QUE dans la partie d'entraînement proposée à la fin de la visite du menu (ou par
    // le bouton « ? ») : un joueur qui lance une partie normale n'est jamais interrompu.
    public static bool PracticeRequested { get; private set; }

    public static void RequestPractice() { PracticeRequested = true; }

    /// <summary>Vrai une seule fois, à l'entrée de la partie lancée depuis l'entraînement.</summary>
    public static bool ConsumePractice()
    {
        bool p = PracticeRequested;
        PracticeRequested = false;
        return p;
    }

    // ---- conseils de partie ----------------------------------------------------------------------------------------
    public static bool Seen(Hint h)
    {
        EnsureMigrated();
        SaveData d = Data;
        return d == null || (d.tutorialHints & (1 << (int)h)) != 0;
    }

    public static void MarkSeen(Hint h)
    {
        SaveData d = Data;
        if (d == null) return;
        int bit = 1 << (int)h;
        if ((d.tutorialHints & bit) != 0) return;
        d.tutorialHints |= bit;
        Save();
    }

    /// <summary>Conseils de partie actifs : le joueur n'a pas désactivé le didacticiel et c'est (au plus) sa 1re partie.</summary>
    public static bool HintsEnabled
    {
        get { EnsureMigrated(); SaveData d = Data; return d != null && d.tutorialHints != -1; }
    }

    /// <summary>Fin (ou abandon) de la partie d'entraînement : elle ne se relance plus d'elle-même. Les visites de page restent.</summary>
    public static void FinishPractice()
    {
        SaveData d = Data;
        if (d == null) return;
        d.tutorialHints = -1;
        Save();
    }

    /// <summary>« Plus de conseils » : tous les conseils de partie sont marqués vus.</summary>
    public static void DisableHints()
    {
        SaveData d = Data;
        if (d == null) return;
        d.tutorialHints = -1;
        d.tutorialPages = -1;
        Save();
    }

    /// <summary>Paramètres > Revoir le didacticiel : la visite du menu et tous les conseils reviennent.</summary>
    public static void Replay()
    {
        SaveData d = Data;
        if (d == null) return;
        d.tutorialInit = true;
        d.tutorialMenuDone = false;
        d.tutorialHeroChosen = true;   // un joueur qui revoit le didacticiel garde son héros (pas de décor vide)
        d.tutorialHints = 0;
        d.tutorialPages = 0;
        Save();
        Changed?.Invoke();
    }

    private static void Save()
    {
        SaveData d = Data;
        if (d != null) SaveSystem.Save(d);
    }

    // ---- libellés de touches pour les textes (suivent les touches choisies et la manette) -------------------------
    public static string Keys(GameAction a) { string k = GameInput.ShortBindingLabel(a); return string.IsNullOrEmpty(k) ? GameInput.BindingText(a) : k; }

    /// <summary>Les 4 touches de déplacement (« Z Q S D » ou « Stick gauche » à la manette).</summary>
    public static string MoveKeys()
    {
        if (GameInput.UsingGamepad) return Loc.T("le stick gauche");
        return GameInput.ShortBindingLabel(GameAction.MoveUp) + " " + GameInput.ShortBindingLabel(GameAction.MoveLeft) + " "
               + GameInput.ShortBindingLabel(GameAction.MoveDown) + " " + GameInput.ShortBindingLabel(GameAction.MoveRight);
    }

    /// <summary>« 1 / 2 / 3 » (ou le bouton de la manette) pour choisir une carte.</summary>
    public static string CardKeys()
    {
        if (GameInput.UsingGamepad) return Loc.T("le curseur de la manette");
        return GameInput.ShortBindingLabel(GameAction.Card1) + " / " + GameInput.ShortBindingLabel(GameAction.Card2) + " / " + GameInput.ShortBindingLabel(GameAction.Card3);
    }
}
