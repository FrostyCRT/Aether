using UnityEngine.SceneManagement;

// AJOUTE (2026-09-14) - point d'entree unique pour TOUS les changements de
// scene EN COURS DE JEU (MainMenu -> Jeu, Abandon/Restart/Retour au menu
// depuis GameManager, etc.). Avant, chaque appelant faisait un
// SceneManager.LoadScene brut et synchrone directement vers sa destination :
// aucun retour visuel, aucun vrai chargement asynchrone, donc un risque de
// freeze/hitch a chaque transition (surtout MainMenu -> Jeu, la plus lourde).
//
// MODIFIE (2026-09-14, meme jour) - premiere version faisait passer TOUTE
// transition par la scene LoadingScreen (artwork + logo + barre) : retour
// utilisateur, ca donnait l'impression d'un vrai "ecran de chargement" meme
// pour rejouer immediatement apres etre mort, hors sujet pour une transition
// qui doit se sentir immediate. LoadingScreen reste reservee au tout premier
// demarrage du jeu (scene de boot dans les Build Settings, jamais appelee via
// ce script). Ici, on delegue a SceneTransitionFader (deja existant, un
// fondu noir persistant entre les scenes) qui fait le vrai chargement async
// + liberation memoire cachee derriere un fondu rapide, sans jamais quitter
// visuellement le contexte du jeu.
public static class SceneLoader
{
    public static void LoadScene(string targetSceneName)
    {
        if (SceneTransitionFader.Instance != null)
        {
            SceneTransitionFader.Instance.LoadScene(targetSceneName);
        }
        else
        {
            // Secours si le fader n'a pas encore ete instancie (ne devrait pas
            // arriver en pratique : il nait des le tout premier chargement,
            // LoadingScreen, et survit ensuite a toutes les scenes suivantes).
            SceneManager.LoadScene(targetSceneName);
        }
    }
}
