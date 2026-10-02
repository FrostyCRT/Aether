using System.Collections;
using UnityEngine;

// A poser sur le GameObject de la fenêtre de confirmation d'abandon (celui assigné au champ
// "Abandon Confirm Panel" du composant GameUI).
//
// Problème corrigé (2026-10-01) : Pause > Abandonner > Non refermait la confirmation mais ne rouvrait pas le
// panel de pause : on se retrouvait en jeu, figé (Time.timeScale = 0, IsPaused = true) et sans menu.
// Ce script détecte la fermeture de la confirmation (OnDisable) et, si la partie est toujours en pause,
// réaffiche le panel de pause via GameUI.ShowPausePanel(true).
//
// Le retour est fait à la frame SUIVANTE (et sur GameUI, qui reste actif) : si le bouton "Non" enchaîne
// plusieurs actions dans son OnClick (ex. fermer la confirmation PUIS masquer le panel de pause), c'est
// notre réaffichage qui passe en dernier. Fonctionne donc quel que soit le câblage du bouton.
public class AbandonConfirmReturnToPause : MonoBehaviour
{
    private void OnDisable()
    {
        // Pas pendant un déchargement de scène (Abandonner > Oui, retour menu, fermeture du jeu).
        if (!gameObject.scene.isLoaded) return;

        if (GameUI.Instance == null || GameManager.Instance == null) return;
        if (!GameUI.Instance.isActiveAndEnabled) return;
        if (!GameManager.Instance.IsPaused || GameManager.Instance.IsGameOver) return;

        GameUI.Instance.StartCoroutine(RestorePausePanelNextFrame());
    }

    private static IEnumerator RestorePausePanelNextFrame()
    {
        yield return null;

        if (GameUI.Instance == null || GameManager.Instance == null) yield break;

        // "Oui" : AbandonRun() repasse IsPaused à faux avant de charger le menu, on ne rouvre rien.
        // Échap pendant la confirmation : la partie a repris, rien à rouvrir non plus.
        if (!GameManager.Instance.IsPaused || GameManager.Instance.IsGameOver) yield break;

        // La page Paramètres gère elle-même son retour au menu pause (GameUI.OnSettingsClosed).
        if (SettingsPage.InGameOpen) yield break;

        GameUI.Instance.ShowPausePanel(true);
    }
}